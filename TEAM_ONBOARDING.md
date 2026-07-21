# 팀원 온보딩

이 문서는 GitHub 저장소가 생성된 뒤 각 팀원이 처음 한 번 수행합니다.

## 1. 준비

- Git 설치
- Obsidian 설치
- GitHub 계정이 Repository collaborator로 초대됨
- 팀에서 전달받은 HTTPS Repository URL

## 2. Vault 받기

```bash
git clone https://github.com/<account>/local-ai-boost-team-vault.git
cd local-ai-boost-team-vault
git status
```

Obsidian에서 `local-ai-boost-team-vault` 폴더를 Vault로 엽니다.

## 3. 작업 시작 전

1. `AGENTS.md`를 읽습니다.
2. `00-dashboard/current-status.md`를 읽습니다.
3. `02-decisions/decision-log.md`를 읽습니다.
4. `03-tasks/task-board.md`에서 같은 작업이 진행 중인지 확인합니다.
5. 담당 작업을 `in_progress`로 등록한 뒤 작업합니다.

## 4. 작업 중 충돌 줄이기

- 조사 결과는 `04-research/` 아래 자신의 파일에 기록합니다.
- 실행 근거는 `08-evidence/` 아래 자신의 파일에 기록합니다.
- 인수인계는 `06-handoffs/` 아래 별도 파일에 기록합니다.
- `current-status.md`, `task-board.md`, `decision-log.md`는 동시에 여러 명이 크게 수정하지 않습니다.

## 5. 작업 종료 전

```bash
git pull --rebase origin main
git status
git diff --check
git add <내가-수정한-파일>
git commit -m "docs: record <작업 내용>"
git push origin main
```

`git pull`에서 충돌이 나면 임의로 덮어쓰지 말고 팀에 알립니다. 충돌 상태에서는 파일을 삭제하거나 강제 push하지 않습니다.

## 6. 완료 기준

작업 보드의 상태를 `done`으로 바꾸려면 결과물, 실행·검토 근거, 다음 사람이 이어받을 수 있는 설명이 모두 있어야 합니다. 단순히 파일을 만들었다는 이유만으로 `done`으로 바꾸지 않습니다.
