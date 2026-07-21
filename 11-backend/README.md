# 백엔드 (P0)

Supabase(Postgres + Edge Functions) 기반. **경제 무결성과 보호견 데이터 파이프라인**을 먼저 구현했습니다.

- 상태: `verified` (2026-07-22)
- 검증: 스키마·RLS 24건 단언 + 파서 10건 + 실 API 전 구간 6건 = **40건 통과**
- 관련 결정: D-019 (보호견 1차 소스 = 서울 vPetInfo)

```bash
./tests/run.sh            # 스키마·RLS·파서 (오프라인)
E2E=1 ./tests/run.sh      # + 실제 vPetInfo API 호출
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
  seed.sql               config — 밸런스 상수는 전부 여기 (클라 복제 금지)
  functions/
    _shared/shelter.ts   vPetInfo 정규화·CONT 섹션 분리
    _shared/vpet.ts      API 호출 (부작용 없음 — 테스트에서 재사용)
    shelter-sync/        CRON 동기화 핸들러
tests/
  00_local_auth_stub.sql Supabase auth 흉내 (로컬 전용, 배포 안 함)
  integrity_test.sql     경제 공격 13건
  rls_test.sql           RLS 공격 11건
  shelter_test.ts        파서 10건 (픽스처)
  e2e_shelter_sync.ts    실 API → Postgres 6건
  run.sh
```

---

## 3. 검증한 것

### 경제 무결성 (13건)
원장 UPDATE·DELETE 차단 / 잔액 초과 차감 차단 / 멱등(웹훅 재전송) / 일일 상한 / 육포→포인트 전환 / **역방향 전환 함수 부재** / 랭킹에서 과금 유래 제외 / **기부해도 랭킹 점수 유지** / 발바닥 소비·회복 / 돌봄 멱등 / 목욕 거부는 실패가 아님

### RLS 방어선 (11건)
`anon`에게 **테이블 권한을 전부 준 상태에서** 공격합니다. Supabase가 실제로 그렇게 동작하므로, "권한이 없어 막힌 것"과 "RLS가 막은 것"을 구분하기 위해서입니다.

원장 직접 INSERT·UPDATE / 레벨 조작 / 발바닥 충전 / 게임 점수 삽입 / 돌봄 기록 위조 / 타인 데이터 조회 / 보호견 위조 / **전환 비율 조작** / 설문 저장은 정상 허용(과잉 차단 아님) / 공격 전후 잔액 불변

### 보호견 파이프라인 (16건)
파서 10건은 픽스처, e2e 6건은 **실제 API 호출**입니다.

실 API 결과: 24건 수신 → 성격 섹션 전건 추출 → Postgres 적재 → `W`→`female` 매핑 확인 → 입양문의가능 11건 / 임시보호가능 2건 → 재동기화 멱등.

---

## 4. 구현하며 발견한 것

**`[성격]` 헤더가 없는 레코드가 있습니다** (seq 508 '미요'). 성격 항목이 `[보호 센터]` 뒤에 헤더 없이 나열돼 있어, 헤더 기반 파싱이 실패했습니다. 항목 라벨(`사람 친화력`, `에너지 레벨` 등)로 찾는 폴백을 넣어 24건 전부 추출됩니다. — 픽스처가 아니라 실데이터로 테스트했기에 잡힌 결함입니다.

**임시보호 가능은 24건 중 2건뿐입니다.** §4.4 참여 퍼널의 임보 단계 대상이 생각보다 적습니다. 임보 CTA를 상시 노출하면 대부분 빈 화면이 되므로, 화면 설계에서 고려가 필요합니다.

---

## 5. 아직 안 한 것

- **Edge Function 핸들러 대부분** — `shelter-sync`만 작성. `survey-analyze`·`game-submit`·`commerce/*`는 미구현
- **LLM 연동 전부** — `traits` 구조화, 추천 이유 생성, 설문 되묻기(`10-survey-engine/survey-prompts.md`에 명세만)
- **레벨 곡선·방치 하락** — config에 상수만 있고 함수 미구현
- **커머스 웹훅** — HMAC 검증·order_token·CRON 재대조 (PRD §7.6)
- **Supabase 프로젝트 미연결** — 로컬 Postgres로만 검증. 실 배포 시 `auth.users` FK 연결 필요

## 6. 주의

- **인증키는 이 저장소에 없습니다.** `.env.local`(gitignore)에만 두고, 팀원은 각자 발급받습니다.
- 로컬 검증은 Docker 없이 돌도록 만들었습니다. `supabase start`를 쓸 수 있는 환경이면 `00_local_auth_stub.sql`은 불필요합니다.
- `seed.sql`의 밸런스 수치는 **전부 가안**입니다 (PRD §5.3·C-04 주석). 밸런스 시트 확정 시 이 파일만 고칩니다.
