# 백엔드 (P0)

Supabase(Postgres + Edge Functions) 기반. **경제 무결성과 보호견 데이터 파이프라인**을 먼저 구현했습니다.

- 상태: `verified` — **Supabase 프로젝트 `balang`에 배포 완료** (2026-07-22)
- 검증: **오프라인 56건** (SQL 33 · Deno 23) + **실호출 23건** (LLM 17 · 공공 API 6). 보호견 실데이터 24건 적재
- 관련 결정: D-019 (보호견 1차 소스 = 서울 vPetInfo) · D-020 (LLM 프로바이더) · D-021 (보더콜리 고정)

```bash
./tests/run.sh                              # 오프라인 56건
E2E=1 ./tests/run.sh                        # + vPetInfo 실호출 6건
deno run -A tests/e2e_analysis.ts           # AI 분석 실호출 8건
deno run -A tests/e2e_recommend.ts          # 성격 구조화·추천 실호출 9건
```

---

## 1. 왜 이 순서로 만들었나

PRD §5.5는 **"Unity WebGL은 코드가 통째로 노출된다"**를 전제로 합니다. anon key도 함께 노출되므로, 공격자는 임의의 SQL을 던질 수 있습니다. 그래서 화면 기능보다 **재화를 만들 수 없게 만드는 것**을 먼저 했습니다.

방어는 3겹입니다.

| 겹 | 수단 | 막는 것 |
|---|---|---|
| 1 | **RLS** — 쓰기 정책을 아예 만들지 않음 | 클라의 직접 INSERT/UPDATE |
| 2 | **security definer 함수** — 유일한 통로 | 잔액 음수, 중복 지급, 일일 상한 초과 |
| 3 | **DB 제약·트리거** | 원장 수정·삭제, 음수 재화 |

핵심은 **없는 것이 방어**라는 점입니다. `ledger`에 INSERT 정책이 없고, `characters`에 UPDATE 정책이 없고, 포인트→육포 전환 함수가 존재하지 않습니다. 조건문으로 막는 것보다 통로 자체를 없애는 쪽이 안전합니다.

---

## 2. 구성

```
supabase/
  migrations/
    0001_schema.sql      테이블 12 · 뷰 2 (balances, ranking_scores)
    0002_integrity.sql   ledger_append · convert_jerky_to_point · paw_* · care_perform
    0003_rls.sql         RLS 정책 (쓰기 정책의 부재가 곧 방어)
    0004_auth.sql        auth.users 연동 — 가입 시 프로필 자동 생성
    0005_account_deletion.sql  탈퇴 시 원장 익명 보존 (G-01)
    0006_breeds.sql      견종 화이트리스트 + 고정 견종 (A-09, D-021)
  seed.sql               config — 밸런스 상수는 전부 여기 (클라 복제 금지)
  functions/
    _shared/shelter.ts   vPetInfo 정규화·CONT 섹션 분리
    _shared/vpet.ts      API 호출 (부작용 없음 — 테스트에서 재사용)
    _shared/llm.ts       LLM 어댑터 (OpenAI / Claude 자리) — D-020
    _shared/analysis.ts  AI 상황 분석 프롬프트·검증 (단일 엔진)
    _shared/traits.ts    보호견 성격 5축 구조화 (D-03 창작 금지)
    _shared/recommend.ts 보호견 추천·이유 생성
    _shared/http.ts      인증·응답 공통
    shelter-sync/        CRON 보호견 동기화
    shelter-traits/      CRON 성격 구조화
    survey-analyze/      AI 상황 분석 (A-08)
    survey-probe/        설문 되묻기 (A-06·A-07)
    recommend/           보호견 추천 (D-01~D-03)
tests/
  00_local_auth_stub.sql Supabase auth 흉내 (로컬 전용, 배포 안 함)
  integrity_test.sql     경제 공격 13건
  auth_test.sql          Auth 연동·탈퇴 9건
  rls_test.sql           RLS 공격 11건
  shelter_test.ts        파서 10건 (픽스처)
  analysis_test.ts       분석 검증 13건 (오프라인)
  e2e_shelter_sync.ts    실 API → Postgres 6건
  e2e_analysis.ts        AI 분석 8건 (실 LLM)
  e2e_recommend.ts       성격 구조화·추천 9건 (실 LLM)
  run.sh
```

---

## 3. 검증한 것

### 경제 무결성 (13건)
원장 UPDATE·DELETE 차단 / 잔액 초과 차감 차단 / 멱등(웹훅 재전송) / 일일 상한 / 육포→포인트 전환 / **역방향 전환 함수 부재** / 랭킹에서 과금 유래 제외 / **기부해도 랭킹 점수 유지** / 발바닥 소비·회복 / 돌봄 멱등 / 목욕 거부는 실패가 아님

### Auth 연동 (9건)
가입 시 프로필 자동 생성 / 생년월일 메타데이터 전달 / 유령 계정 차단 / `ensure_profile` 멱등 / 탈퇴 시 cascade / **탈퇴해도 원장은 익명 보존** / 익명 항목 재연결 차단 / 익명 항목은 잔액·랭킹 제외 / **익명화 예외가 append-only를 뚫지 않음**

### RLS 방어선 (11건)
`anon`에게 **테이블 권한을 전부 준 상태에서** 공격합니다. Supabase가 실제로 그렇게 동작하므로, "권한이 없어 막힌 것"과 "RLS가 막은 것"을 구분하기 위해서입니다.

원장 직접 INSERT·UPDATE / 레벨 조작 / 발바닥 충전 / 게임 점수 삽입 / 돌봄 기록 위조 / 타인 데이터 조회 / 보호견 위조 / **전환 비율 조작** / 설문 저장은 정상 허용(과잉 차단 아님) / 공격 전후 잔액 불변

### LLM 파이프라인 (실호출 17건)
**AI 상황 분석 8건** — 견종 화이트리스트 준수 / 사용자 문장 인용 / 서로 다른 설문 → 서로 다른 결과 / 금칙어 미사용 / **여건 빠듯한 사용자에게 입양을 서두르지 않음** / 보더콜리 고정 포함 / 성격 프리필

**성격 구조화·추천 9건** — traits 항목이 원문에 근거(36/36) / 모르는 축은 null 유지 / 추천이 후보 안에서만 / 이유가 사용자 문장 인용 / **겹치는 보호견도 사용자별로 다른 이유** / 금칙어 미사용

### 보호견 파이프라인 (16건)
파서 10건은 픽스처, e2e 6건은 **실제 API 호출**입니다.

실 API 결과: 24건 수신 → 성격 섹션 전건 추출 → Postgres 적재 → `W`→`female` 매핑 확인 → 입양문의가능 11건 / 임시보호가능 2건 → 재동기화 멱등.

---

## 4. 배포 상태 (2026-07-22)

**Supabase 프로젝트 `balang`(ap-northeast-1, Postgres 17.6)에 배포 완료.**

```bash
supabase link --project-ref <ref> -p "$SUPABASE_DB_PASSWORD"
supabase db push --linked -p "$SUPABASE_DB_PASSWORD"
psql "$DB_URL" -f supabase/seed.sql
```

원격 검증 결과:

| 항목 | 결과 |
|---|---|
| 테이블 / 뷰 | 12 / 2 |
| RLS 활성 테이블 / 정책 | 12 / 12 |
| 경제 무결성 (PG17에서 재실행) | **13건 전부 통과** |
| 보호견 실데이터 적재 | **24건** (입양문의가능 11 · 임시보호가능 2) |
| 실제 `anon` 롤 공격 | 원장 직접 INSERT 차단 확인 |
| **실제 회원가입 (admin API)** | 프로필 자동 생성 + 생년월일 전달 확인, 탈퇴 시 정리 확인 |
| **Edge Function 5종 배포** | survey-analyze · survey-probe · recommend · shelter-sync · shelter-traits |
| **프로덕션 데모 경로** | 가입 → 설문 → AI 분석 → 보호견 추천까지 배포된 함수로 완주 |
| **보호견 성격 구조화** | 17/24 생성 (입양문의가능 11건 전건 완료) |

로컬 검증은 Postgres 16, 운영은 17이므로 **원격에서 무결성 테스트를 다시 돌렸습니다.**

### 운영 시 알아야 할 것 두 가지

**1. 탈퇴는 삭제가 아니라 익명화입니다.**
계정을 지우면 `ledger`의 행은 남고 `user_id`만 `null`이 됩니다(0005). 금액·출처·시각이 보존되므로 기부 집행 증빙이 유지되고, 개인과의 연결은 끊어집니다 — G-01의 "익명화 보존" 요구를 만족합니다. 익명 항목은 `balances`·`ranking_scores`에서 제외되고, 다른 계정에 재연결할 수 없습니다.

append-only 예외는 **이 한 가지뿐**입니다. `user_id`를 `null`로 바꾸면서 금액·출처·시각 중 하나라도 함께 바꾸면 트리거가 막습니다(테스트 9번이 이걸 확인합니다).

단, **테이블 소유자 권한으로 `TRUNCATE`하면 append-only가 우회됩니다** (TRUNCATE는 행 트리거를 발화시키지 않음). anon·authenticated는 RLS로 막히므로 실사용 경로에 구멍은 없지만, service_role 키를 쓰는 서버 코드에서는 규율로 지켜야 합니다.

**2. 비로그인 사용자에게는 보호견이 0건으로 보입니다.**
`shelter_animals` 정책이 `auth.uid() is not null`이라, 로그인 전에는 목록이 비어 있습니다. 로그인 후 24건이 보입니다. PRD의 온보딩이 로그인부터 시작하므로 의도와 맞지만(A-01), 만약 **비로그인 상태에서 보호견을 미리 보여주는 화면**을 만들 계획이라면 정책을 바꿔야 합니다.

---

## 5. 구현하며 발견한 것

**`[성격]` 헤더가 없는 레코드가 있습니다** (seq 508 '미요'). 성격 항목이 `[보호 센터]` 뒤에 헤더 없이 나열돼 있어, 헤더 기반 파싱이 실패했습니다. 항목 라벨(`사람 친화력`, `에너지 레벨` 등)로 찾는 폴백을 넣어 24건 전부 추출됩니다. — 픽스처가 아니라 실데이터로 테스트했기에 잡힌 결함입니다.

**임시보호 가능은 24건 중 2건뿐입니다.** §4.4 참여 퍼널의 임보 단계 대상이 생각보다 적습니다. 임보 CTA를 상시 노출하면 대부분 빈 화면이 되므로, 화면 설계에서 고려가 필요합니다.

---

## 6. 아직 안 한 것

- **레벨 곡선·방치 하락** — config에 상수만 있고 함수 미구현
- **미니게임 서버 검증** — `game_sessions` 테이블만 있고 재계산 로직 미구현 (C-02)
- **커머스 전체** — 결제 의도·웹훅·스킨 지급·미성년 한도 스키마 자체가 없음 (PRD §7)
- **돌봄 요구량 계산** — 성격에서 요구량을 뽑는 로직 (B-02)
- **CRON 미등록** — `shelter-sync`·`shelter-traits` 주기 실행 스케줄 필요 (함수는 배포됨)
- **Claude 어댑터 미구현** — 키 확보 시 `_shared/llm.ts`의 `anthropic()`만 채우면 된다

## 7. 주의

- **인증키는 이 저장소에 없습니다.** `.env.local`(gitignore)에만 두고, 팀원은 각자 발급받습니다.
- 로컬 검증은 Docker 없이 돌도록 만들었습니다. `supabase start`를 쓸 수 있는 환경이면 `00_local_auth_stub.sql`은 불필요합니다.
- `seed.sql`의 밸런스 수치는 **전부 가안**입니다 (PRD §5.3·C-04 주석). 밸런스 시트 확정 시 이 파일만 고칩니다.
