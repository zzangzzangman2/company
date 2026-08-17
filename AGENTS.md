# AGENTS.md

이 저장소는 가족회사 Unity 게임의 공동 작업 공간이다. Codex는 작업 전에 다음 문서를 순서대로 읽는다.

1. Docs/PROJECT_STATE.md
2. Docs/CANON.md
3. Docs/DECISIONS.md
4. Docs/ARCHITECTURE.md
5. 시각/씬 작업이면 Docs/ART_STYLE.md, Docs/ARCHITECTURE.md, Docs/OFFICE_BUILD_EDITOR_V1.md, Docs/MAIN_NAVIGATION_HUD_V2.md
6. 회사 역사·시장·계약 작업이면 Docs/ULTIMATE_VISION.md, Docs/REAL_COMPANY_ALT_HISTORY.md, Docs/SIMUL_MARKET_PORT.md, Docs/CONTRACTS_V0_3.md
7. 코드를 고치고 확인해야 하면 Docs/ITERATION_LOOP.md
8. 작업과 직접 관련된 추가 문서
9. 가족 보행 프레임을 생성·편집·가져오기·검증하면 작업 시작 전에
   Docs/FAMILY_WALK_ART_GUARDRAILS.md를 읽고 `FC-WALK-GUARDRAIL-V1` 확인 문구를 작업 로그에 남긴다.

## 필수 작업 규칙

- 현재 `b397af9`는 사무실 관리 구매 클릭과 빈 사무실 자율 산책의 차단 회귀가 확인된 재현 기준일 뿐
  정상 릴리스가 아니다. `Docs/PROJECT_STATE.md` 맨 위의 차단 회귀를 먼저 읽고, 삭제된 이전 작업방
  outputs나 `Docs/History/Reports`의 PASS를 현재 정상 증거로 재사용하지 않는다. normal 새 게임에서 다시
  재현·검증한다.
- 정본 개발 브랜치는 `main` 하나다. `agent/*`, 기능·임시 브랜치, 새 branch와 별도 worktree를 만들거나 전환하지 않는다.
- 한 채팅에서 한 작업씩 순차 진행한다. 사용자가 다시 명시적으로 허용하지 않는 한 하위 에이전트·다른 채팅·새 작업에 위임하지 않는다.
- 회사 PC·집 PC·다른 AI나 도구에서 작업해도 먼저 이 문서와 README의 문서 표를 읽고, clean `main`에서만 `git pull --ff-only origin main`으로 시작한다.
- 예상하지 못한 tracked·untracked 변경이 있으면 삭제·복원·일괄 stage하지 않고 소유권과 생성 시각을 먼저 확인한다.
- 회사 PC에서는 사용자의 업무 화면을 방해하지 않도록 Unity Editor와 플레이테스트 EXE를 전면 실행하지 않는다. 컴파일·로직 검증은 `-batchmode -nographics -quit`, 렌더·PlayMode 캡처는 `-batchmode`를 사용해 백그라운드로 실행하고 로그의 PASS/FAIL까지 확인한다.
- 시각 검증에서 `Camera.Render`가 필요하면 `-nographics`를 사용하지 않으며, 자동 종료가 검증 coroutine을 끊는 경우 `-quit`도 사용하지 않는다. GUI나 EXE의 직접 조작이 꼭 필요하면 먼저 사용자에게 알린다.
- Unity 버전은 6000.3.21f1로 고정한다.

### 빌드와 검증 명령

- 한 곳을 고치고 결과를 확인하는 반복 작업에는 `FAST_QA_WINDOWS.cmd`를 쓴다. 기본 `-Profile auto`가 바뀐
  파일을 보고 가장 싼 경로를 고르며, 출력은 `Artifacts/FastQa`에만 쓴다. 변경 종류별 명령과 실측 근거는
  Docs/ITERATION_LOOP.md가 정본이다.
- `BUILD_WINDOWS.cmd`와 `DEPLOY_WINDOWS.cmd`는 배포 후보 HEAD가 확정되고 clean일 때만 실행한다. 한 줄 수정을
  확인하려고 릴리스 빌드를 돌리지 않는다.
- `Library`, `Library/Bee`, `Artifacts/FastQa`의 플레이어 캐시는 일상 실행 사이에 삭제하지 않는다. 이 캐시가
  식으면 같은 변경의 확인 비용이 7~20초에서 100초 이상으로 늘어난다.
- 새 worktree를 만들면 그 경로의 첫 Unity 실행이 80~104초짜리 최초 임포트를 처음부터 다시 낸다. 반복 작업은
  `Library`가 이미 warm인 기존 worktree 한 곳에서 수행하고, 병합이 끝난 worktree는 `git worktree remove`로
  정리한다.
- 2026-08-17 정리 후 기능 작업 경로는 `fc_agents/integration_p0` 하나다. 이 경로의 `Library/Bee`를 보존하고
  삭제된 옛 작업트리 이름이나 stash를 탐색해 코드를 가져오지 않는다.
- 느리다고 느끼면 추측하지 말고 그 실행의 Unity 로그에서 `Asset Pipeline Refresh ... Total: <n> seconds`와
  `Require frontend run. Library/Bee/*.dag couldn't be loaded`를 먼저 확인한다. 두 값이 작은데도 느릴 때만
  빌드 파이프라인을 의심한다.
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
- Docs/PROJECT_STATE.md는 현재 상태만 기록한다. `Docs/History/Reports/`의 완료 보고서는 당시 증거이며 현재 정본이나 미완료 목록을 덮어쓰지 않는다.
- Observer/FAST_QA의 PASS 조건이 사용자 증상을 직접 계측하지 않으면 정상 증거가 아니다. 편집기는 실제
  pointer click→confirm→state mutation을, 자율 이동은 실제 coordinator intent→destination→path와
  direction/displacement를 normal Player에서 검증한다.

## History 데이터 소유권

- 사용자가 실제 회사 역사 데이터 작업을 명시적으로 지시한 경우에만 Docs/CLAUDE_HANDOFF_HISTORY_DATA.md의 전용 경로를 사용한다.
- History 담당은 Docs/CLAUDE_HISTORY_PROGRESS.md만 진행 기록으로 갱신하며, PROJECT_STATE와 DECISIONS 반영은 검토 후 별도 순서에서 수행한다.
- History 작업 중 Assets/FamilyCompany/Content/History, HistoryTools, Docs/CLAUDE_HISTORY_PROGRESS.md 밖을 수정하지 않는다.
- History를 포함한 모든 작업은 현재 `main` 한 곳에서 순차적으로 수행하며, 병렬 작업을 위해 branch나 worktree를 만들지 않는다.

## 완료 조건

코드 컴파일, 헤드리스 검증, 씬 생성/열기 중 작업 범위에 맞는 검증을 실행하고 결과를 PROJECT_STATE에 기록한다. 검증하지 못했다면 이유와 정확한 다음 명령을 적는다.
