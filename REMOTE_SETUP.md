# GitHub 원격 공유 설정

현재 상태:

- 로컬 Git 저장소: 준비 완료
- 기본 브랜치: `main`
- 첫 커밋: 로컬 기준본 생성 완료
- GitHub 로그인: 이 컴퓨터에서는 불필요 — 저장소 생성 담당자가 자신의 계정으로 처리
- 원격 저장소: 아직 연결하지 않음

저장소 이름은 예시로 `local-ai-boost-team-vault`를 사용합니다. 팀원 담당자가 실제 저장소를 만들 때 이름을 바꿔도 됩니다.

## 저장소 담당자가 할 일

1. GitHub에서 **Private** Repository를 생성합니다.
2. README 자동 생성은 선택하지 않습니다. 이 Vault의 `README.md`가 기준본입니다.
3. 팀원을 Repository collaborator로 초대합니다.
4. 생성된 HTTPS URL을 팀에 공유합니다.

저장소를 만든 사람은 이 폴더에서 아래 명령을 실행합니다.

```bash
cd "/Users/lzent/Local AI Boost Club Team Vault"
git remote add origin https://github.com/<account>/local-ai-boost-team-vault.git
git push -u origin main
```

이미 `origin`이 있다면 `git remote set-url origin <HTTPS_URL>`을 사용합니다.

## GitHub CLI 방식 — 저장소 담당자가 CLI를 사용할 때

```bash
gh auth login --hostname github.com --git-protocol https --web
gh repo create local-ai-boost-team-vault --private --source . --remote origin --push
```

## 팀원 공유

```bash
git clone https://github.com/<account>/local-ai-boost-team-vault.git
```

Obsidian에서 clone한 폴더를 Vault로 엽니다. 첫 작업 전에는 `TEAM_ONBOARDING.md`를 읽습니다.

## push 직전 점검

```bash
git status --short
git diff --check
git ls-files | grep -Ei '(^|/)(\.env|.*secret.*|.*token.*|.*password.*|.*credential.*)$' || true
```

비밀값이나 개인 인증 파일이 발견되면 push하지 말고 `.gitignore`와 파일 내용을 먼저 확인합니다.
