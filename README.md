# 가족회사 (가제)

14살 플레이어가 엄마·아빠·누나와 2000년의 작은 사무실에서 시작해, 하청을 버티고 자체 사업을 세우며 실제 기업들과 경쟁하는 싱글플레이 생활 경영 RPG입니다.

## 최종 통합 후보 상태

- **가족 캐릭터 신규 구현은 2026-08-24부터 3D 전용이다.** Player/Father/Mother/Older Sister의 기존 2D
  sprite·atlas·PSB·R-series·분리 신체·보행 프레임은 mesh/texture/motion donor나 fallback으로 사용하지
  않는다. 시작 문서는 `Docs/FAMILY_3D_CHARACTER_CANON_2026-08-24.md`, 실행 순서는
  `Docs/FAMILY_3D_CHARACTER_HANDOFF_2026-08-24.md`다.
- 네 가족 identity turnaround는 준비됐고, 공통 3D 보행·방향·루트·mute 구조는 Styloo proxy V3로만
  검증됐다. 최종 네 가족 textured Humanoid mesh는 아직 없으며, proxy와 Blender V1/V2를 최종 캐릭터로
  오인하지 않는다. production/default/Downloads 실행본은 사용자 승인 전 바꾸지 않는다.

- **`b397af9`의 두 차단 회귀는 `a9c6885e`에서 수정되어 더 이상 현재 기준선이 아니다.** 배치 편집기의 pending
  분기는 preview 셀을 갱신한 뒤 같은 frame에서 `Input.GetMouseButtonDown(0)` → `ConfirmPreview()`까지
  진행하고, 빈 사무실 fallback은 `OfficeAutonomyCoordinator`의 destination 있는 타일 중심 산책으로 바뀌었다.
  실제 Windows native pointer 1회 클릭에서 자금 `5,000,000→4,986,250`, furniture `52→53`, 실제 Release
  Player normal 빈 사무실 08:50→09:50 관측에서 `currentLook=0`, `duplicatePivot=0`을 확인했다.
- **현재 열린 gate는 가족 4명의 최종 3D mesh·rig·실게임 검증이다.** 각 역할은 한 몸/한 Humanoid Avatar,
  공통 clip/clock/cadence, SW/NW/NE/SE 여러 주기, P0/P3 앞발 교대, bottom-centre 이동, 충돌 회피와 mute를
  실제 D3D11에서 통과해야 한다.
- R18 arrival `ce9e3ae4d94a7365c0447103d2ad904013ef58a1`는 독립 static과 Unity `6000.3.21f1` capture-free Player exit 0 검증을 통과한 뒤 현재 integration에 단일 merge되었습니다. 가족 4명의 Work 0..5, 동일 좌석 atomic 정렬, first-walk와 safe egress, 가구 무변형이 확인되었습니다.
- 과거·회귀 실행 payload는 evidence 보존 후 허용 root에서 제거되었고, GitHub history·tags·Releases·Actions 감사 결과 executable payload는 0입니다. `da5c6e7f9f9d48f0eada245cff727435536c91dd`에서 도입한 CI guard가 향후 tracked Windows Player payload를 fail-closed 차단합니다.
- 현재 실행 payload는 두 곳뿐이다. 저장소 `Builds/Windows/FamilyCompany_Playtest`는 clean HEAD `8fa5fa74`의
  Release build이고, 배포본 `%USERPROFILE%\Downloads\Family`는 한 커밋 앞선 `befe937e`다. LKG는
  `Downloads\Family.last-known-good.<UTC>.<sha>` 한 개로만 보존한다. `%USERPROFILE%\Downloads\FamilyCompany_Playtest`
  경로는 현재 존재하지 않으며, `Tools/FamilyCompanyBuild.Common.ps1`의 기본값만 아직 그 옛 이름을 가리킨다.
- 배포본이 HEAD보다 뒤에 있으므로 `Downloads\Family`의 실행 결과를 현재 HEAD의 증거로 사용하지 않는다.

## 현재 구현 기준선

- 새 게임은 `2000-01-03 08:50`, 가족 4명, 자본금 500만 원으로 시작합니다.
- 사무실은 13×13 바닥과 외곽만 있는 빈 상태로 시작합니다. `회사 → 사무실 관리`에서 책상·의자·정수기 등 카테고리별 가구를 구매하고, 모든 가구를 회전된 footprint의 정확한 타일 중심에 배치합니다.
- 가족 4명만 `09:00`~`09:03`에 1분 간격으로 출근하고 `18:00`부터 퇴근합니다. 직원 8명은 시작 인원이 아니라 향후 채용 후보입니다.
- `MainNavigationV2`의 회사·인사·사업·연구·투자 5개 허브를 사용합니다. 회사 허브는 사무실 편집, 사업 허브는 계약/제품, 투자 허브는 주식으로 연결됩니다.
- 계약 고객은 `T0 → T1 → T2 → T3 → T4` 순차 해금과 등급 하락/회복 규칙을 가집니다.
- 사무실 편집기의 배치·회전·이동·회수·재고·저장 계약을 유지하며, pending 구매 좌클릭은 같은 frame에
  `ConfirmPreview()`까지 도달합니다. 전체 저장 스키마는 `v10`이고 `v1`~`v9`을 읽어 이관합니다.
- Title·Loading·HUD·5개 허브·인사 roster는 UI Remaster V3 공용 스킨과 프로젝트에 포함된 Maplestory Light/Bold 폰트를 사용합니다.
- 가족 4명은 같은 10,000 체력 기준으로 시작하며, 업무 중 체력이 25%까지 내려가면 실제 배치·접근·사용 가능한 정수기·자판기·휴식 좌석을 찾아 회복한 뒤 원래 자리와 남은 업무로 돌아갑니다.
- 캐릭터 방향과 걷기 애니메이션의 정본은 요청 방향이 아니라 프레임의 실제 이동량입니다. 빈 사무실 산책도
  destination을 가진 타일 중심 경로를 쓰며, 각 cardinal 구간의 pivot을 끝낸 뒤 translation합니다.
- 현재 production의 2D 캐릭터는 3D migration 전 보호되는 legacy runtime일 뿐 신규 캐릭터 제작 입력이
  아닙니다. 최종 3D 네 가족이 모두 사용자 승인을 받은 뒤 별도 migration commit으로만 교체합니다.
- 기본 렌더는 `1920×1080`, native scale 1, pixel snap을 사용하고 작은 창은 compact UI로 대응합니다.
- 주식은 회사 자금과 연결되며 시장 시간, 7+7 호가, 가격·시간 우선 FIFO, 수수료·세금, 결정론적 저장 규칙을 유지합니다.

기능별 현재 통합 상태, 미완료 항목, 최신 검증은 [PROJECT_STATE.md](Docs/PROJECT_STATE.md)가 유일한 정본입니다.

## 고치고 확인하는 반복 루프

**한 곳을 고치고 결과만 보려면 `BUILD_WINDOWS.cmd`를 쓰지 않습니다.** 릴리스 빌드는 매번 새 staging 폴더와
전역 lock, 사전 validator, 배포 manifest를 모두 처리하므로 반복 확인에는 낭비입니다.

```bat
FAST_QA_WINDOWS.cmd
```

기본 `-Profile auto`가 바뀐 파일을 보고 가장 싼 경로를 고릅니다. Simulation `.cs`만 고쳤으면 Unity를 아예
켜지 않고 15초, 런타임 `.cs`면 scripts-only 빌드로 60초가 목표입니다. 출력은 `Artifacts/FastQa`에만 쓰므로
배포본을 건드리지 않습니다.

반복 루프가 느려지는 진짜 원인은 빌드 옵션이 아니라 worktree마다 warm `Library`를 버리는 것입니다. 실측과
변경 종류별 명령표는 [ITERATION_LOOP.md](Docs/ITERATION_LOOP.md)가 정본이고, Fast QA 도구의 선택 계약은
[FAST_QA_WINDOWS.md](Docs/FAST_QA_WINDOWS.md)에 있습니다.

## Windows에서 바로 실행하기

Unity `6000.3.21f1`이 설치된 저장소 루트에서 다음 명령을 사용합니다. 이 경로는 그 PC의 실행본을 처음 만들
때와 배포 후보를 확정할 때만 사용합니다.

```powershell
.\BUILD_WINDOWS.cmd
.\RUN_WINDOWS.cmd
```

- 빌드 출력: `Builds/Windows/FamilyCompany_Playtest/FamilyCompany.exe`
- 빌드 출처: 같은 폴더의 `BUILD_INFO.txt`에서 commit SHA와 Unity 버전을 현재 `git rev-parse HEAD`와 비교합니다.
- `Builds/`는 Git에 포함되지 않습니다. 다른 PC에서는 pull 후 직접 빌드하거나 검증된 빌드 폴더 전체를 복사해야 합니다.
- 상세 절차와 오류 해결은 [PLAYTEST_BUILD.md](Docs/PLAYTEST_BUILD.md)를 따릅니다.
- user-visible regression, failed gate, stale/unverified provenance, self-PASS-only candidate는 current/Downloads에 둘 수 없습니다. 비실행 evidence를 먼저 보존한 뒤 해당 실행 payload 전체를 즉시 삭제하는 강제 규칙은 [REGRESSION_BUILD_POLICY.md](Docs/REGRESSION_BUILD_POLICY.md)를 따릅니다.

Editor에서 실행하려면 `Assets/FamilyCompany/Scenes/Prototype01.unity`를 열고 Play를 누릅니다.

## 문서 정본

| 순서·분야 | 문서 | 역할 |
| --- | --- | --- |
| 1 | [AGENTS.md](AGENTS.md) | 작업·검증·파일 소유권 규칙 |
| 2 | [PROJECT_STATE.md](Docs/PROJECT_STATE.md) | 현재 통합/대기/미완료와 최신 검증 |
| 3 | [CANON.md](Docs/CANON.md) | 가족·직원 후보·시각 콘텐츠 정본 |
| 4 | [DECISIONS.md](Docs/DECISIONS.md) | 구조와 방향 결정의 이유 |
| 가족 3D | [FAMILY_3D_CHARACTER_CANON_2026-08-24.md](Docs/FAMILY_3D_CHARACTER_CANON_2026-08-24.md), [FAMILY_3D_CHARACTER_HANDOFF_2026-08-24.md](Docs/FAMILY_3D_CHARACTER_HANDOFF_2026-08-24.md) | 3D-only 캐릭터 정본·비용·제작·검수·다른 PC 인계 |
| 구조 | [ARCHITECTURE.md](Docs/ARCHITECTURE.md) | 순수 시뮬레이션·저장·Unity 경계 |
| 반복 루프 | [ITERATION_LOOP.md](Docs/ITERATION_LOOP.md), [FAST_QA_WINDOWS.md](Docs/FAST_QA_WINDOWS.md) | 변경 종류별 명령과 warm 캐시 규칙 |
| 사무실·UI | [ART_STYLE.md](Docs/ART_STYLE.md), [OFFICE_BUILD_EDITOR_V1.md](Docs/OFFICE_BUILD_EDITOR_V1.md), [MAIN_NAVIGATION_HUD_V2.md](Docs/MAIN_NAVIGATION_HUD_V2.md), [FRONTEND_V0_4.md](Docs/FRONTEND_V0_4.md) | 현재 런타임 시각·편집·내비게이션 |
| 계약 | [CONTRACTS_V0_3.md](Docs/CONTRACTS_V0_3.md), [CONTRACT_CLIENT_PROGRESSION_V1.md](Docs/CONTRACT_CLIENT_PROGRESSION_V1.md) | 계약 실행과 T0~T4 성장 |
| 주식 | [SIMUL_MARKET_PORT.md](Docs/SIMUL_MARKET_PORT.md), [STOCK_MARKET_LANDSCAPE_V1.md](Docs/STOCK_MARKET_LANDSCAPE_V1.md) | 시장 코어와 가로형 UI |
| 실제 회사 역사 | [CLAUDE_HANDOFF_HISTORY_DATA.md](Docs/CLAUDE_HANDOFF_HISTORY_DATA.md), [CLAUDE_HISTORY_PROGRESS.md](Docs/CLAUDE_HISTORY_PROGRESS.md) | History 전용 경로와 데이터 상태 |
| 다른 PC 재개 | [HOME_PC_CONTINUATION_GUIDE.md](Docs/HOME_PC_CONTINUATION_GUIDE.md) | pull·빌드·실행·검증 순서 |
| 빌드 회귀 삭제 | [REGRESSION_BUILD_POLICY.md](Docs/REGRESSION_BUILD_POLICY.md) | 실패/회귀 실행본의 evidence·삭제·rollback·재빌드 강제 계약 |

`Docs/History/Reports/`의 문서는 당시 구현 증거를 보존한 역사 보고서이며 현재 상태를 덮어쓰지 않습니다.

## 개발 규칙 요약

- 정본 개발 브랜치는 `main` 하나이며, clean 상태에서만 `git pull --ff-only origin main`을 실행합니다.
- `Library`, `Temp`, `Logs`, `work`, `Builds`는 Git에 넣지 않고 `Assets`의 `.meta`는 반드시 추적합니다.
- 회사 PC에서는 Unity/EXE를 전면 실행하지 않습니다. 컴파일·순수 로직 검증은 숨김 batchmode, 실제 렌더·IMGUI 캡처는 숨김 또는 비활성 오프스크린 Windows D3D11 창을 사용합니다.
- 반복 확인은 `FAST_QA_WINDOWS.cmd`로 하고 `BUILD_WINDOWS.cmd`는 배포 후보에만 씁니다. `Library`, `Library/Bee`, `Artifacts/FastQa` 캐시는 일상 실행 사이에 삭제하지 않고, 새 worktree를 만들어 warm `Library`를 버리지 않습니다.
- 제안서나 완료 보고서는 자동으로 정본이 아닙니다. 구현과 검증 후 `PROJECT_STATE.md`에 반영된 내용만 현재 상태입니다.
- 기존 2D 가족 캐릭터 문서와 연구 산출물은 신규 mesh/texture/animation/fallback 권한이 아닙니다.
- 회귀·실패·출처 미검증·self-PASS-only 실행본은 이름 변경이나 격리로 보존하지 않습니다. exact-root fence와 evidence-before-delete를 지키며 관련 payload만 삭제하고, 모든 regression oracle과 독립 gate를 통과한 새 build identity만 새로 빌드·승격합니다.
- 최종 push 전에는 `.gitignore`와 tracked tree뿐 아니라 `origin/main`, 모든 active branch/tag tree, remote release asset에서 회귀·구 executable payload와 unknown identity가 0인지 확인합니다. tracked build는 일반 cleanup commit으로 제거하되, history rewrite/force-push는 exact audit·검증된 backup·collaborator re-clone 영향 승인 전에는 금지합니다.

## 기본 자동화

- `Tools/BuildPrototype.ps1`: `Prototype01` 재생성
- `Tools/ValidatePrototype.ps1`: 시간·RNG·가족·회계·저장·에셋 검증
- `Tools/Build-FamilyCompanyWindows.ps1`: 독립 실행 Windows 플레이테스트 빌드
- `FAST_QA_WINDOWS.cmd`: 변경 분류·순수/Editor/scripts-only/D3D 캡처 Fast QA
