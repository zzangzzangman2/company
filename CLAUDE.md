# CLAUDE.md

이 파일은 Claude가 같은 Unity 프로젝트를 이어받기 위한 입구다.

정본 브랜치는 `main` 하나다. 새 branch, `agent/*`, 임시 branch와 worktree를 만들지 않는다. 한 채팅에서 한 작업씩 순차 진행하며, 사용자가 다시 명시적으로 허용하지 않는 한 다른 채팅이나 에이전트에 위임하지 않는다.

작업 전 README.md의 문서 표와 AGENTS.md를 읽은 뒤 Docs/PROJECT_STATE.md, Docs/CANON.md, Docs/DECISIONS.md, Docs/ARCHITECTURE.md를 순서대로 읽는다. 사무실이나 캐릭터 화면을 작업할 때는 Docs/ART_STYLE.md, Docs/OFFICE_BUILD_EDITOR_V1.md, Docs/MAIN_NAVIGATION_HUD_V2.md도 읽는다. clean `main`인지 확인한 뒤에만 수정한다.

단독으로 일반 작업을 이어받을 때는 작업 후 반드시 Docs/PROJECT_STATE.md를 갱신한다. 설정이나 구조 결정이 바뀌면 DECISIONS, 캐릭터나 에셋 정본이 바뀌면 CANON과 ASSET_MANIFEST도 갱신한다. 문서에 없는 추측을 정본처럼 만들지 말고, 임시 가정은 임시라고 표시한다.

실제 회사 역사 데이터를 명시적으로 맡은 경우에는 Docs/CLAUDE_HANDOFF_HISTORY_DATA.md를 추가로 읽고 그 문서의 전용 경로만 수정한다. 진행 기록은 Docs/CLAUDE_HISTORY_PROGRESS.md에만 남기며 PROJECT_STATE와 DECISIONS 반영은 검토 후 다음 순서에서 수행한다.
