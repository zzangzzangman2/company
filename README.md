# 가족회사 (가제)

14살 플레이어가 가족과 작은 회사를 만들고, 집·학교·거리·사무실을 오가며 사람과 사업을 함께 키우는 싱글플레이 생활 경영 게임이다.

## 시작하기

- Unity: 6000.3.21f1
- 시작 씬: Assets/FamilyCompany/Scenes/Prototype01.unity
- 정본 문서: Docs/PROJECT_STATE.md, Docs/CANON.md, Docs/DECISIONS.md, Docs/ART_STYLE.md
- 집 PC 재개: Docs/HOME_PC_CONTINUATION_GUIDE.md
- 역사 데이터 분업: Docs/CLAUDE_HANDOFF_HISTORY_DATA.md
- 원본 Flutter 저장소: C:/Users/godho/Documents/Codex/simul (읽기 전용 참고)

프로젝트를 만지는 사람이나 에이전트는 먼저 루트의 AGENTS.md 또는 CLAUDE.md를 읽고 정본 문서를 확인한다.

2026-08-10 통합 브랜치는 `agent/contract-lifecycle-v0-3`이다. 집 PC에서는 저장소가 clean인지 확인한 뒤 `git fetch origin`, `git switch agent/contract-lifecycle-v0-3`, `git pull --ff-only` 순서로 재개한다. dirty worktree에서 브랜치를 전환하거나 `Library`, `Temp`, `Logs`, `work` 산출물을 커밋하지 않는다.

## 자동화

- Tools/BuildPrototype.ps1: Office V0.2가 포함된 Prototype01 씬 재생성
- Tools/ValidatePrototype.ps1: 시간, RNG, 가족 나이, 이벤트, 회계, 저장, 에셋 검증
