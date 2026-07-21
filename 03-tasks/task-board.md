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

## 막힘

```markdown
- [ ] 문제 제목
  - owner:
  - blocker:
  - attempted:
  - needed:
```
