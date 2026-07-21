# Agent Handoff

- agent: Claude (Claude Code)
- human_owner: hanbyulkang
- updated_at: 2026-07-21
- task: 보호견 데이터 공개 API 조사 및 실호출 검증
- status: verified

## 이번 세션에서 확인한 사실

- 국가동물보호정보시스템 구조동물 조회 API(`abandonmentPublicService_v2`)의 오퍼레이션 5종이 전부 정상 동작한다. 개 전체 4,521건(공고중 1,344건).
- 서울 열린데이터광장 `vPetInfo`가 24건(개 15·고양이 9)에 대해 **이름·성격 서술·유튜브 영상·임시보호 가능 여부**를 제공한다.
- **성격이 구조화 필드로 있는 전국 단위 공개 API는 확인되지 않았다.** 전국 단위에서 가장 근접한 것은 국가 API의 `specialMark` 자유서술 한 줄이며, 100건 샘플에서 채워진 비율은 100%이나 품질 편차가 크다("엄청 순하고, 사람 잘따름" vs "관리번호 6-088M").
- 서울 `CONT`는 HTML 원문에 템플릿 2종·섹션 마커 2종이 섞여 있어 **정규식 파싱이 불가능하다. LLM 파싱이 필요하다.**
- 두 API의 **성별 코드가 다르다**: 국가 `F`/`M`/`Q` vs 서울 **`W`**/`M`. 정규화 없이 섞으면 성별이 뒤집힌다.

## 실제로 실행한 작업

- 두 API에 실제 HTTP 호출 (오퍼레이션 5종 + vPetInfo 전량)
- 100건 샘플로 필드 채움 비율·값 분포 실측, 이미지 URL 접근성 확인(HTTP 200)
- 샘플 44건 저장 (국가 20 + 서울 24)
- 로컬 저장소를 원격 기준본(`origin/main`)에 맞춤 — 세션 초반의 Next.js 스캐폴드는 폐기

## 생성·수정한 파일

- `04-research/animal-data-api-research.md` (신규)
- `05-data/animal-data-samples.md` (신규)
- `05-data/sample-national-rescue-dogs.json` (신규, 20건)
- `05-data/sample-seoul-shelter-pets.json` (신규, 24건)
- `03-tasks/task-board.md` (완료·검토 항목 추가)

## 검증 결과

- 오퍼레이션 5종 HTTP 200, `resultCode: 00`
- `specialMark` 채움 100% · `popfile1` 존재 100% (100건 기준)
- 이미지 직접 접근 HTTP 200 (182KB)
- 샘플 JSON 2종은 위 호출의 원본 응답에서 배열만 추출한 것 (값 미변형)

## 아직 확정하지 못한 것

- 두 소스를 함께 쓸지 — 팀 결정 필요 (`task-board`의 `animal-api-source-decision`)
- 서울 vPetInfo의 갱신 주기
- 국가 API 이미지의 핫링크 허용 여부 및 상업적 이용 조건
- `happenPlace` + `happenDt`로 가족(어미·형제)을 묶는 규칙이 전국적으로 유효한지 — **가설 단계**
- 경기·인천 등 타 지자체 성격 데이터 유무 (포털이 외부 접근을 차단해 미확인)

## 다음 에이전트가 바로 할 일

1. `04-research/animal-data-api-research.md` §5 제약 6개를 구현 설계에 반영한다 (특히 개별 조회 API 부재 → 자체 DB 스냅샷 필수, 이미지 HTTP → 프록시 필수).
2. 서울 `CONT` 24건을 LLM으로 파싱해 성격 5축(사람 친화력·타동물 친화력·에너지·좋아하는 것·필요 교육) 구조화 스키마로 뽑고, 결과를 `05-data/`에 저장한다.
3. 두 소스의 공통 도메인 모델(성별·나이·체중·식별자 정규화)을 정의한다.

## 주의할 점

- **인증키는 이 Vault에 없다.** 팀 규칙 §8에 따라 로컬 `.env.local`에만 보관하며 `.gitignore`의 `.env.*`로 커밋이 차단된다. 다른 팀원은 각자 발급받아야 한다 (둘 다 무료·자동승인).
  - 국가 API: https://www.data.go.kr/data/15098931/openapi.do
  - 서울: https://data.seoul.go.kr/together/mypage/actkeyMain.do
- 국가 API는 **활용신청 직후 401(`Unauthorized`)이 정상**이다. 수 분 뒤 자동 해소된다. 경로에 `_v2`가 빠지면 `API not found`가 뜬다 — 두 오류를 구분할 것.
- 국가 API 일일 한도 10,000건. 개발 중에는 `05-data/`의 샘플 JSON을 쓰고 실호출을 아낄 것.
- `careOwnerNm`에 개인 성명이 들어가는 경우가 있다(예: "민성식"). **화면 노출 금지.**
- PRD 용어 원칙에 따라 문서·UI 모두 "보호견"으로 쓴다. 이 세션 초반 산출물에 "유기견" 표현이 있었으나 전부 정리했다.
