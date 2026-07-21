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

## 완료

## 막힘

```markdown
- [ ] 문제 제목
  - owner:
  - blocker:
  - attempted:
  - needed:
```
