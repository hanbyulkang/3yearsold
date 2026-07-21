# Task Board

## 진행 중

<!-- 작업 시작 전에 아래 형식으로 등록합니다. -->

```markdown
- [ ] 작업 제목
  - id: unique-task-id
  - owner: 팀원 또는 에이전트명
  - status: in_progress
  - input:
  - output:
  - next_action:
  - evidence:
```

- [x] GitHub Private Repository 생성 및 기준본 push
  - id: github-private-repo-bootstrap
  - owner: 팀원 담당자 + Alfred
  - status: done
  - input: 이 Vault의 `REMOTE_SETUP.md`, 로컬 `main` 브랜치
  - output: `https://github.com/hanbyulkang/3yearsold`의 `main` 브랜치
  - next_action: 팀원이 Repository에서 파일을 확인하고 각자 clone
  - evidence: 원격 `main` 커밋 `c03b791cc91169adc5d8b04ef958d5672ac12977`

- [ ] 각 팀원 clone 및 Obsidian Vault 열기 확인
  - id: team-member-onboarding
  - owner: 각 팀원
  - status: idea
  - input: GitHub Repository URL, `TEAM_ONBOARDING.md`
  - output: 팀원별 로컬 Vault와 첫 작업 등록
  - next_action: 저장소 URL 공유 후 각자 clone하고 체크리스트 수행
  - evidence: 팀원별 첫 작업 또는 인수인계 파일

## 검토 필요

- [ ] 보호견 데이터 소스 2종 채택 여부 결정
  - id: animal-api-source-decision
  - owner: 팀 (조사: Claude / hanbyulkang 세션)
  - status: review
  - input: `04-research/animal-data-api-research.md`, `05-data/animal-data-samples.md`
  - output: 추천 카드에 쓸 데이터 소스 확정 및 결정 로그 등록
  - next_action: 국가 API(전국 4,521건·성격 빈약)와 서울 vPetInfo(24건·성격 풍부)를 함께 쓸지 팀이 판단
  - evidence: 샘플 JSON 2종, 실제 HTTP 호출로 검증한 필드·품질 수치

## 완료

- [x] 보호견 공개 API 2종 조사 및 실호출 검증
  - id: animal-api-research
  - owner: Claude (hanbyulkang 세션)
  - status: verified
  - input: 공공데이터포털 구조동물 조회 API, 서울 열린데이터광장 `vPetInfo`
  - output: `04-research/animal-data-api-research.md`, `05-data/animal-data-samples.md`, 샘플 JSON 2종
  - next_action: 위 `animal-api-source-decision` 검토
  - evidence: 5개 오퍼레이션 전부 HTTP 200 확인, 100건 샘플 품질 실측, 샘플 44건 저장

- [x] 온보딩 설문 엔진 (화면과 분리된 로직 + AI 되묻기)
  - id: survey-engine
  - owner: Claude (hanbyulkang 세션)
  - status: verified
  - input: PRD §4.3, 와이어프레임 A-02·A-03, `05-data/survey-evidence.md`
  - output: `10-survey-engine/` (engine · spec · 프롬프트 명세 · 데모)
  - next_action: 설문 화면 디자인이 나오면 `demo.html`만 교체. 서버 `/api/survey/probe` 구현 필요
  - evidence: Node 상태머신 테스트 통과(검증·되묻기·건너뛰기·원문보존), 브라우저 실동작 확인

- [x] 백엔드 P0 — 스키마·경제 무결성·RLS·보호견 동기화
  - id: backend-p0
  - owner: Claude (hanbyulkang 세션)
  - status: verified
  - input: PRD §4·§5, 와이어프레임 개발 주석 36건, D-019
  - output: `11-backend/` (마이그레이션 3 · 시드 · Edge Function · 테스트 5종)
  - next_action: Supabase 프로젝트 연결 후 배포. survey-analyze·game-submit 핸들러와 LLM 연동 구현
  - evidence: 단언 40건 통과 — 경제 무결성 13 · RLS 공격 11 · 파서 10 · 실 API 전 구간 6 (`./tests/run.sh`)

- [x] LLM 파이프라인 — AI 상황 분석 · 보호견 성격 구조화 · 추천
  - id: backend-llm
  - owner: Claude (hanbyulkang 세션)
  - status: verified
  - input: PRD §4.3, 와이어프레임 A-06~A-09·D-01~D-04, `10-survey-engine/survey-prompts.md`
  - output: `11-backend/supabase/functions/` (핸들러 5 · 공유 모듈 5), `0006_breeds.sql`
  - next_action: Edge Function 배포 + 시크릿 등록, `shelter-traits` 1회 실행해 프로덕션 traits 채우기
  - evidence: 로컬 41건 + 실 LLM 호출 16건. 상반된 두 설문 → 견종·참여 단계가 갈림, 겹치는 보호견도 사용자별 다른 이유

- [x] 프론트엔드 ↔ 백엔드 연결 (D 추천 실데이터 · MG1 서버 재화 · 씬 네비)
  - id: frontend-backend-wiring
  - owner: Claude (hanbyulkang 세션)
  - status: verified
  - input: d-recommend/mini-game-1 씬, RecLoading.WaitForApi 훅, IRewardClient 인터페이스
  - output: `19Team/Assets/Scripts/Backend/` 8종, RecBootstrap·RecData·MG1GameManager 수정, DemoNavBar, 빌드 씬 4개
  - next_action: 팀원이 Unity에서 열어 Play 확인. A 온보딩 씬이 오면 OnboardingApi로 연결
  - evidence: 서버 e2e — 데모 계정 추천 3마리 캐시, game-start 발바닥 5→4, bones 45→30(상한), 재제출 차단, 잔액 30. Unity 배치 컴파일 로그

## 막힘

```markdown
- [ ] 문제 제목
  - owner:
  - blocker:
  - attempted:
  - needed:
```
