# Decision Log

팀의 주요 결정은 삭제하지 않고 누적 기록합니다. 이전 결정이 바뀌면 `superseded`로 표시하고 새 결정을 추가합니다.

## D-001 — 외부 협업은 GitHub Private Repository와 HTTPS를 사용

- date: 2026-07-21
- decision: 팀원 담당자가 GitHub에 Private Repository를 만들고, 팀원은 HTTPS로 각자 clone·pull·push한다.
- alternatives: Obsidian Sync, Obsidian Publish, SSH 접속
- reason: 해커톤 장소가 외부이고 각 팀원의 로컬 에이전트가 Markdown 파일에 직접 접근해야 하며, SSH 서버 의존성을 없애기 위해서다.
- evidence: `REMOTE_SETUP.md`, 로컬 Git 저장소의 `main` 기준본
- tradeoff: 실시간 공동 편집은 아니며, 같은 파일을 동시에 수정하면 Git 충돌이 생길 수 있다.
- owner: 팀
- status: active
