-- ============================================================
-- RLS (Row Level Security)
--
-- 원칙: 클라이언트는 자기 데이터를 "읽기"만 한다.
--       재화·진행도의 "쓰기"는 security definer 함수를 통해서만 일어난다.
--       Unity WebGL은 anon key가 그대로 노출되므로 이 경계가 유일한 방어선이다.
-- ============================================================

alter table profiles         enable row level security;
alter table survey_responses enable row level security;
alter table survey_followups enable row level security;
alter table analyses         enable row level security;
alter table characters       enable row level security;
alter table care_logs        enable row level security;
alter table ledger           enable row level security;
alter table paw_state        enable row level security;
alter table game_sessions    enable row level security;
alter table recommendations  enable row level security;
alter table shelter_animals  enable row level security;
alter table config           enable row level security;

-- ---------- 본인 데이터 읽기 ----------

create policy own_read on profiles         for select using (user_id = auth.uid());
create policy own_read on analyses         for select using (user_id = auth.uid());
create policy own_read on characters       for select using (user_id = auth.uid());
create policy own_read on care_logs        for select using (user_id = auth.uid());
create policy own_read on paw_state        for select using (user_id = auth.uid());
create policy own_read on game_sessions    for select using (user_id = auth.uid());
create policy own_read on recommendations  for select using (user_id = auth.uid());
create policy own_read on survey_followups for select using (user_id = auth.uid());

-- 포인트 내역 화면(G-02)은 원장을 직접 읽는다. 읽기만 허용하고 쓰기 정책은 만들지 않는다.
create policy own_read on ledger for select using (user_id = auth.uid());

-- ---------- 설문만 클라가 직접 쓴다 ----------
-- 문항 단위 즉시 저장이 필요하고(A-03), 조작해도 얻을 이득이 없다(추천 품질만 나빠진다).
create policy own_write on survey_responses
  for all using (user_id = auth.uid()) with check (user_id = auth.uid());

-- ---------- 공개 데이터 ----------
-- 보호견·설정은 로그인 사용자면 누구나 읽는다. 쓰기는 서비스 롤(CRON)만.
create policy public_read on shelter_animals for select using (auth.uid() is not null);
create policy public_read on config          for select using (auth.uid() is not null);

-- ============================================================
-- 여기 없는 것이 곧 방어다.
--
--   ledger        INSERT 정책 없음 → 클라는 재화를 만들 수 없다.
--                 유일한 통로는 ledger_append() (security definer).
--   characters    UPDATE 정책 없음 → 레벨·경험치를 클라가 못 올린다.
--   paw_state     쓰기 정책 없음 → 발바닥을 클라가 못 채운다.
--   game_sessions INSERT 정책 없음 → 점수를 클라가 못 써넣는다.
--                 반드시 game_submit()을 거쳐 서버가 재계산한다.
--   shelter_animals 쓰기 정책 없음 → CRON(service_role)만 동기화한다.
-- ============================================================
