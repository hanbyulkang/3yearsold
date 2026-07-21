# Agent Handoff

- agent: Claude (Claude Code)
- human_owner: hanbyulkang
- updated_at: 2026-07-22
- task: 백엔드 P0 — 스키마·경제 무결성·RLS·보호견 동기화
- status: verified

## 이번 세션에서 확인한 사실

- **로컬 검증만으로 P0를 끝낼 수 있다.** Docker가 없어 `supabase start`를 못 쓰지만, 로컬 PostgreSQL 16 + Deno로 스키마·RLS·파서·실 API를 모두 검증했다. `auth` 스키마만 스텁으로 대체하면 된다.
- **`[성격]` 헤더가 없는 보호견 레코드가 실존한다** (seq 508 '미요'). 성격 항목이 `[보호 센터]` 뒤에 헤더 없이 나열돼 있어 헤더 기반 파싱이 실패했다. 항목 라벨 기반 폴백으로 24건 전건 추출된다.
- **임시보호 가능은 24건 중 2건뿐이다.** §4.4 참여 퍼널의 임보 단계 대상이 매우 적다. 화면 설계에 반영 필요.
- 실 API 기준 입양문의가능 11건, 성별 female 13 / male 11.

## 실제로 실행한 작업

- 마이그레이션 3종 + 시드 작성, 로컬 Postgres에 적용
- 경제 무결성 함수 구현 (`ledger_append`, `convert_jerky_to_point`, `paw_sync/consume`, `care_perform`)
- RLS 정책 작성 — **쓰기 정책을 만들지 않는 방식**으로 방어
- vPetInfo 정규화 파서 + CRON 동기화 핸들러
- 테스트 5종 작성 및 실행

## 생성·수정한 파일

- `11-backend/` 전체 (신규)
- `03-tasks/task-board.md` — backend-p0 등록

## 검증 결과

`./tests/run.sh` — **단언 40건 전부 통과**

| 구분 | 건수 | 방식 |
|---|---|---|
| 경제 무결성 | 13 | 실제 Postgres 공격 |
| RLS 방어선 | 11 | anon 권한으로 실제 공격 |
| 파서 | 10 | 픽스처 24건 |
| 전 구간 | 6 | **실제 vPetInfo API 호출** |

RLS 테스트는 `anon`에게 **테이블 권한을 전부 부여한 상태**에서 공격한다. Supabase가 실제로 그렇게 동작하므로, 권한 부족으로 막힌 것을 RLS가 막았다고 착각하지 않기 위해서다.

## 아직 확정하지 못한 것

- Supabase 프로젝트 미연결. 실 배포 시 `profiles.user_id`를 `auth.users`에 FK로 걸어야 한다
- 밸런스 수치는 전부 가안 (`seed.sql`) — 밸런스 시트 확정 대상
- 임보 대상이 2건뿐인 상황에서 임보 CTA를 어떻게 노출할지 (화면 설계 판단 필요)
- 커머스 웹훅 실패 처리 표(PRD §7.6)는 스키마조차 미작성

## 다음 에이전트가 바로 할 일

1. `survey-analyze` 핸들러 구현 — `10-survey-engine/`의 설문 결과를 받아 단일 `analyses` 레코드 생성. 견종은 화이트리스트 검증 필수(와이어프레임 A-09).
2. `traits` 채우기 — `shelter_animals.content_raw`의 성격 섹션을 LLM으로 5축 구조화. 프롬프트에는 `groundingFacts()` 결과만 넣는다(창작 방지, D-03).
3. `game-submit` 핸들러 — `moves`를 서버가 재계산해 `verified_score` 확정 후 `ledger_append(origin='play')`.

## 주의할 점

- **인증키는 저장소에 없다.** `.env.local`(gitignore)에 `SEOUL_API_KEY`, `ANIMAL_API_KEY`. 팀원은 각자 발급.
- **Edge Function 핸들러를 import 하지 말 것.** 최상단에서 `Deno.serve()`를 호출하므로 import 만으로 서버가 뜬다. 재사용할 로직은 `_shared/`에 둔다 (이 실수로 e2e가 멈췄었다).
- 재화 증감은 반드시 `ledger_append()`를 거친다. 테이블 직접 INSERT는 RLS가 막지만, service_role로는 뚫리므로 서버 코드에서도 지켜야 한다.
- 밸런스 상수를 클라에 복제하지 말 것 (와이어프레임 A-05·B-04). 전부 `config` 테이블에서 읽는다.
