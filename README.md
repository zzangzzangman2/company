# 가족회사 (가제)

14살 플레이어가 가족과 작은 회사를 만들고, 집·학교·거리·사무실을 오가며 사람과 사업을 함께 키우는 싱글플레이 생활 경영 게임이다.

## 시작하기

- Unity: 6000.3.21f1
- 시작 씬: Assets/FamilyCompany/Scenes/Prototype01.unity
- 정본 문서: Docs/PROJECT_STATE.md, Docs/CANON.md, Docs/DECISIONS.md
- 원본 Flutter 저장소: C:/Users/godho/Documents/Codex/simul (읽기 전용 참고)

프로젝트를 만지는 사람이나 에이전트는 먼저 루트의 AGENTS.md 또는 CLAUDE.md를 읽고 정본 문서를 확인한다.

## 자동화

- Tools/BuildPrototype.ps1: Prototype01 씬 재생성
- Tools/ValidatePrototype.ps1: 시간, RNG, 가족 나이, 이벤트, 회계, 저장, 에셋 검증
