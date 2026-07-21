-- ============================================================
-- D+ 백엔드 스키마 (PRD §4·§5, D-019)
--
-- 설계 대전제 (PRD §5.5)
--  1. 모든 재화 증감은 서버에서만. 클라(Unity WebGL)는 코드가 통째로 노출된다.
--  2. 원장(ledger)은 append-only. 잔액은 파생값이며 저장하지 않는다.
--  3. 포인트마다 origin을 기록한다. 랭킹은 과금 유래를 제외한다.
-- ============================================================

create extension if not exists "pgcrypto";

-- ---------- 열거형 ----------

-- 재화. 발바닥은 시간 회복형이라 원장이 아니라 별도 상태 테이블로 관리한다.
create type currency as enum ('point', 'jerky');

-- 포인트 출처. 랭킹 집계는 play·level만 본다 (PRD §5.5, 와이어프레임 C-05).
create type ledger_origin as enum (
  'level',      -- 캐릭터견 레벨업 (주 경로)
  'care',       -- 일일 돌봄 완주
  'play',       -- 미니게임 클리어
  'rank',       -- 주간 랭킹 보상
  'convert',    -- 육포 → 포인트 (과금 유래)
  'purchase',   -- 실물 커머스 구매 적립 (과금 유래)
  'topup',      -- 육포 충전 (결제)
  'donate',     -- 사료 기부 / 지정 후원 (차감)
  'shop'        -- 스킨·마당 확장 구매 (차감)
);

create type adopt_status as enum ('입양문의가능', '입양진행중', '신청마감', '입양완료', '미표출');

-- ---------- 계정 ----------

create table profiles (
  user_id      uuid primary key,
  -- 생년월일은 결제 한도·법정대리인 동의 판단에만 쓴다 (와이어프레임 A-01).
  -- 기능 분기·추천 입력에 사용 금지 (D-014).
  birth_date   date,
  created_at   timestamptz not null default now()
);

-- ---------- 설문 (PRD §4.3) ----------

-- 문항 단위 즉시 저장 — 이탈 후 이어하기 (와이어프레임 A-03)
create table survey_responses (
  user_id      uuid not null references profiles(user_id) on delete cascade,
  question_id  text not null,
  value        jsonb not null,
  updated_at   timestamptz not null default now(),
  primary key (user_id, question_id)
);

-- 되묻기 응답은 원문을 덮지 않고 별도로 쌓는다 (와이어프레임 A-06)
create table survey_followups (
  id           uuid primary key default gen_random_uuid(),
  user_id      uuid not null references profiles(user_id) on delete cascade,
  question_id  text not null,
  probe        text not null,
  answer       text,
  skipped      boolean not null default false,
  created_at   timestamptz not null default now()
);

-- 분석은 단일 레코드. 견종·보호견·참여 추천이 전부 이걸 참조한다 (PRD §4.3 단일 엔진).
create table analyses (
  id            uuid primary key default gen_random_uuid(),
  user_id       uuid not null references profiles(user_id) on delete cascade,
  input         jsonb not null,          -- 분석에 넣은 설문 스냅샷
  result        jsonb not null,          -- 견종 후보·참여 단계·근거 문장
  created_at    timestamptz not null default now(),
  superseded_by uuid references analyses(id)   -- 설문 수정 시 재생성 이력 (와이어프레임 D-06)
);

create index on analyses (user_id, created_at desc);

-- ---------- 캐릭터견 (PRD §4.1) ----------

create table characters (
  user_id      uuid primary key references profiles(user_id) on delete cascade,
  name         text not null,
  breed        text not null,           -- 사전 정의 목록에서만 (와이어프레임 A-09 화이트리스트)
  -- 성격이 페르소나 연출과 돌봄 요구량 계산의 입력 (와이어프레임 A-10)
  personality  jsonb not null,          -- {activity:1..5, timid:1..5, affection:1..5}
  level        int  not null default 1 check (level >= 1),
  exp          int  not null default 0  check (exp >= 0),
  last_care_at timestamptz,
  created_at   timestamptz not null default now()
);

-- 돌봄 기록. 같은 슬롯 중복 지급 차단이 목적 (와이어프레임 B-03 멱등).
create table care_logs (
  user_id     uuid not null references profiles(user_id) on delete cascade,
  care_date   date not null,
  care_type   text not null,
  slot_no     int  not null,
  -- 목욕 거부는 실패가 아니라 분기다 (와이어프레임 B-02). 값에 fail을 쓰지 않는다.
  result      text not null default 'done' check (result in ('done', 'declined')),
  created_at  timestamptz not null default now(),
  primary key (user_id, care_date, care_type, slot_no)
);

-- ---------- 재화 원장 (PRD §5.5) ----------

-- append-only. UPDATE·DELETE는 트리거로 차단한다 (0002_integrity.sql).
create table ledger (
  id           bigserial primary key,
  user_id      uuid not null references profiles(user_id) on delete cascade,
  currency     currency not null,
  delta        int not null check (delta <> 0),
  origin       ledger_origin not null,
  ref          text,                    -- 멱등키 (게임 세션 id, 주문 id 등)
  created_at   timestamptz not null default now()
);

create index on ledger (user_id, currency);
create index on ledger (user_id, created_at desc);
-- 같은 출처의 같은 참조는 한 번만 기록된다. 웹훅 재전송·중복 제출 방어.
create unique index ledger_idempotency on ledger (user_id, origin, ref) where ref is not null;

-- 잔액은 저장하지 않고 원장에서 파생한다 (와이어프레임 G-02: 클라 합산 금지).
create view balances as
select user_id, currency, sum(delta)::int as amount
from ledger
group by user_id, currency;

-- 랭킹 점수: play·level 유래 양수 엔트리만 (PRD §5.5, 와이어프레임 C-05).
-- 기부 차감분은 빼지 않는다 — 기부하면 순위가 떨어지는 역인센티브를 막는다.
create view ranking_scores as
select user_id,
       date_trunc('week', created_at) as week,
       sum(delta)::int as score
from ledger
where currency = 'point'
  and delta > 0
  and origin in ('play', 'level')
group by user_id, date_trunc('week', created_at);

-- ---------- 발바닥 (PRD §5.1) ----------

-- 시간 회복형이라 원장에 넣지 않는다. 회복 시각도 서버가 내려준다 (와이어프레임 C-01).
create table paw_state (
  user_id        uuid primary key references profiles(user_id) on delete cascade,
  count          int not null default 5 check (count >= 0 and count <= 5),
  next_refill_at timestamptz,
  updated_at     timestamptz not null default now()
);

-- ---------- 미니게임 (PRD §4.2) ----------

-- 클라 점수를 신뢰하지 않는다. 플레이 로그를 받아 서버가 재계산한다 (와이어프레임 C-02).
create table game_sessions (
  id           uuid primary key default gen_random_uuid(),
  user_id      uuid not null references profiles(user_id) on delete cascade,
  game         text not null,
  moves        jsonb not null,
  claimed_score int not null,
  verified_score int,
  accepted     boolean,
  created_at   timestamptz not null default now()
);

create index on game_sessions (user_id, created_at desc);

-- ---------- 보호견 (D-019: 서울 vPetInfo) ----------

-- 개별 조회 API가 없어 CRON 스냅샷이 필수다.
create table shelter_animals (
  seq            int primary key,              -- vPetInfo SEQ
  name           text not null,                -- ANIMAL_NM
  animal_type    text not null,                -- DOG / CAT
  breed          text,
  -- vPetInfo는 암컷이 'W'다. 국가 API('F')와 다르므로 원본값을 그대로 두고 파생 컬럼을 쓴다.
  sex_raw        text,
  sex            text generated always as (
                   case sex_raw when 'M' then 'male' when 'W' then 'female' else 'unknown' end
                 ) stored,
  birth_ymd      date,
  weight_kg      numeric,
  adopt_status   adopt_status,
  -- §4.4 참여 퍼널의 임시보호 단계와 연결되는 유일한 필드 (D-019)
  foster_ok      boolean not null default false,
  movie_url      text,
  content_raw    text,                          -- CONT 원문 HTML. AI 소개문의 근거 (와이어프레임 D-03)
  -- CONT를 LLM으로 파싱한 구조화 결과. 정규식으로는 못 자른다 (템플릿 2종·마커 2종 혼재).
  traits         jsonb,
  synced_at      timestamptz not null default now()
);

create index on shelter_animals (adopt_status) where adopt_status = '입양문의가능';
create index on shelter_animals (foster_ok) where foster_ok;

-- 같은 보호견도 사람마다 다른 이유를 받는다 (PRD §4.3).
create table recommendations (
  id           uuid primary key default gen_random_uuid(),
  user_id      uuid not null references profiles(user_id) on delete cascade,
  analysis_id  uuid not null references analyses(id) on delete cascade,
  animal_seq   int  not null references shelter_animals(seq) on delete cascade,
  reason       text not null,
  rank         int  not null,
  created_at   timestamptz not null default now(),
  unique (analysis_id, animal_seq)
);

-- ---------- 서버 설정 (와이어프레임 A-05·B-04: 클라 상수 복제 금지) ----------

create table config (
  key        text primary key,
  value      jsonb not null,
  updated_at timestamptz not null default now()
);
