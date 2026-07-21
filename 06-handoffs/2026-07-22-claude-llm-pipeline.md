# Agent Handoff

- agent: Claude (Claude Code)
- human_owner: hanbyulkang
- updated_at: 2026-07-22
- task: LLM 파이프라인 — AI 상황 분석 · 보호견 성격 구조화 · 추천
- status: verified

## 이번 세션에서 확인한 사실

- **개인화가 실제로 작동한다.** 여건이 상반된 두 설문을 넣으면 견종·참여 단계가 갈린다. 여건 넉넉 → `adopt(ready)`, 여건 빠듯(원룸·2시간 미만·5만원 미만) → `learn(not_yet)`. §1.2 원칙 4("입양을 서두르게 하지 않는다")가 프롬프트 수준에서 지켜진다.
- **같은 보호견이라도 사용자마다 다른 이유가 나온다** (PRD §4.3의 핵심 주장). 두 사용자의 추천이 2마리 겹쳤는데, 이유 문장은 서로 달랐다.
- **traits는 원문에 근거한다.** likes·care_needs 36개 항목 전부가 공고 원문에 뿌리를 두고 있었다 (D-03 창작 금지).
- LLM 응답 지연은 회당 **4~7초**. 데모 4분 30초 안에서 분석 1회 + 추천 1회면 15초 안쪽이다.

## 실제로 실행한 작업

- `_shared/llm.ts` — 프로바이더 어댑터 (OpenAI 구현, Claude 자리 비움 · D-020)
- `_shared/analysis.ts` + `survey-analyze/` — 단일 엔진 분석. 견종 화이트리스트·3개 고정·금칙어 검증
- `_shared/traits.ts` + `shelter-traits/` — 보호견 성격 5축 구조화 (CRON)
- `_shared/recommend.ts` + `recommend/` — 보호견 추천·사용자별 이유
- `survey-probe/` — 설문 되묻기 (`10-survey-engine`의 클라 엔진이 호출)
- `0006_breeds.sql` — 견종 16종 + 고정 견종(보더콜리, D-021)

## 검증 결과

- 로컬 41건 (스키마·RLS·파서·분석 검증) — `./tests/run.sh`
- **실 LLM 호출 16건** — `tests/e2e_analysis.ts` 8건, `tests/e2e_recommend.ts` 8건
- 핸들러 5개 `deno check` 통과
- `0006`까지 Supabase 배포 완료

## 아직 확정하지 못한 것

- **Edge Function 미배포.** 코드만 있고 `supabase functions deploy` 전이다. 배포 시 `OPENAI_API_KEY`를 함수 시크릿으로 등록해야 한다.
- CRON 미등록 (`shelter-sync`, `shelter-traits`)
- 프로덕션 `shelter_animals.traits`가 아직 비어 있다. `shelter-traits` 배포 후 1회 실행 필요.
- Claude 어댑터 미구현 — 키 확보 시 `_shared/llm.ts`의 `anthropic()`만 채우면 된다

## 다음 에이전트가 바로 할 일

1. `supabase functions deploy survey-analyze survey-probe recommend shelter-sync shelter-traits` 후 시크릿 등록(`OPENAI_API_KEY`, `SEOUL_API_KEY`).
2. `shelter-traits`를 1회 실행해 프로덕션 24건의 `traits`를 채운다. 이걸 미리 해두면 데모 중 LLM 호출이 분석·추천 2회로 줄어든다.
3. 남은 비-LLM 구현(레벨 곡선·방치 하락, 미니게임 서버 검증, 커머스)을 이어간다. **이 세 가지는 이번 세션에서 한 번 구현했다가 범위를 LLM으로 좁히며 들어냈다** — 필요하면 아래 경로에 백업이 있다.

## 주의할 점

- **`_shared/`에 로직을, 핸들러는 얇게.** 핸들러(`index.ts`)는 최상단에서 `Deno.serve()`를 호출하므로 import 하면 서버가 뜬다. 테스트에서 재사용할 코드는 반드시 `_shared/`에 둔다.
- **LLM 응답을 그대로 믿지 않는다.** 견종은 화이트리스트로, 참여 단계는 enum으로, 금칙어("유기견")는 정규식으로 막는다. 검증 실패 시 온도를 낮춰 1회 재생성한다.
- **보더콜리 고정은 에셋 제약이지 추천 품질이 아니다** (D-021). 3D 에셋이 늘면 `config.breeds.pinned`를 비우는 것으로 즉시 해제된다.
- 되묻기(`survey-probe`)는 실패해도 **200 + probe:null**로 응답한다. 온보딩을 막으면 안 된다.
- API 키는 저장소에 없다. `.env.local`(gitignore)에 `OPENAI_API_KEY`·`SEOUL_API_KEY`·Supabase 자격증명.
