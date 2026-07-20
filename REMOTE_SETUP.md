# GitHub 원격 공유 설정

현재 상태:

- 로컬 Git 저장소: 준비 완료
- 기본 브랜치: `main`
- 첫 커밋: 로컬 기준본 생성 완료
- GitHub 로그인: 아직 필요
- 원격 저장소: 아직 연결하지 않음

## GitHub CLI 방식

```bash
gh auth login --hostname github.com --git-protocol https --web
gh repo create local-ai-boost-team-vault --private --source . --remote origin --push
```

## 팀원 공유

```bash
git clone https://github.com/<account>/local-ai-boost-team-vault.git
```

Obsidian에서 clone한 폴더를 Vault로 엽니다.
