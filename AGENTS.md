# AGENTS.md

이 저장소는 가족회사 Unity 게임의 공동 작업 공간이다. Codex는 작업 전에 다음 문서를 순서대로 읽는다.

1. Docs/PROJECT_STATE.md
2. Docs/CANON.md
3. Docs/DECISIONS.md
4. Docs/ARCHITECTURE.md
5. 시각/씬 작업이면 Docs/ART_STYLE.md와 Docs/OFFICE_V0_2.md
6. 회사 역사·시장 작업이면 Docs/ULTIMATE_VISION.md, Docs/REAL_COMPANY_ALT_HISTORY.md, Docs/SIMUL_MARKET_PORT.md
7. 작업과 직접 관련된 추가 문서

## 필수 작업 규칙

- Unity 버전은 6000.3.21f1로 고정한다.
- 매 작업 종료 전에 Docs/PROJECT_STATE.md의 현재 상태, 완료 항목, 다음 작업, 검증 결과를 갱신한다.
- 설정·구조·콘텐츠 방향을 바꾸면 Docs/DECISIONS.md에 날짜와 이유를 남긴다.
- 캐릭터·나이·복장·에셋 정본을 바꾸면 Docs/CANON.md와 Docs/ASSET_MANIFEST.md를 함께 갱신한다.
- MonoBehaviour와 ScriptableObject에 장기 런타임 상태를 저장하지 않는다. 시뮬레이션 상태는 순수 C# 객체가 소유한다.
- 시간은 시작 시점으로부터 흐른 정수 분 단위, 돈은 정수 원(long), 확률은 seed와 안정 키에 기반한 결정론으로 다룬다.
- 저장 파일에는 씬 Transform이 아니라 게임의 의미 상태만 기록한다.
- C:/Users/godho/Documents/Codex/simul은 이관 참고용이다. 사용자가 명시하지 않으면 수정하지 않는다.
- 생성 에셋은 사용자가 권리를 보유한다고 명시했다. 출처와 생성/편집 이력은 Docs/ASSET_MANIFEST.md에 남긴다.
- Library, Temp, Logs, UserSettings는 Git에 넣지 않는다. Assets의 .meta는 반드시 추적한다.
- 다른 작업자의 변경을 삭제하거나 되돌리지 않는다.

## Codex와 Claude의 동시 작업

- Claude 역사 데이터 작업은 Docs/CLAUDE_HANDOFF_HISTORY_DATA.md의 전용 경로만 사용한다.
- 동시 작업 중 Claude는 Docs/CLAUDE_HISTORY_PROGRESS.md만 갱신하며, PROJECT_STATE와 DECISIONS 반영은 Codex가 검토 후 맡는다.
- Codex는 Claude 작업 중 Assets/FamilyCompany/Content/History, HistoryTools, Docs/CLAUDE_HISTORY_PROGRESS.md를 수정하지 않는다.
- 같은 Git 작업 폴더에서 브랜치를 전환하지 않는다. 별도 브랜치가 필요하면 별도 worktree를 사용한다.

## 완료 조건

코드 컴파일, 헤드리스 검증, 씬 생성/열기 중 작업 범위에 맞는 검증을 실행하고 결과를 PROJECT_STATE에 기록한다. 검증하지 못했다면 이유와 정확한 다음 명령을 적는다.
