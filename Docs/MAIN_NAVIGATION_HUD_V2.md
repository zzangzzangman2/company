# Main Navigation HUD V2 — ImageGen 전면 재디자인

## 격리 기준

- worktree: `C:/Users/godho/Documents/Codex/2026-08-14/family-company-main-navigation-ui/worktree`
- branch: `codex/main-navigation-hud`
- 작업 시작 HEAD: `9ad8eb7b88e85b5f6ff70161a770add48793b84b`
- 작업 시작 `origin/main`: `9ad8eb7b88e85b5f6ff70161a770add48793b84b`
- 최종 재확인 `origin/main`: `52a787f7c821b3297c1118299bad003089b7362c`
- 이 작업에서는 commit, push, main 통합, 배포 빌드를 하지 않는다.

## 결과 계약

- 상단은 generated Sprite 기반 회사명 배지, `GameState.Time.Now` 날짜·시간, `PrototypeBootstrap.WorldTimeScale`의 1x/2x/4x만 표시한다.
- 기존 가족별 긴 상태줄, LIVE, 저장 완료, 단축키 도움, 관리 화면 버튼은 사무실 메인 HUD에서 숨긴다. 저장/시간 정본은 변경하지 않는다.
- 하단은 회사, 인사, 사업, 연구, 투자 5개 탭만 표시한다. 모든 탭·카드 ID, 한글명, 설명, 상태, 아이콘 경로는 `MainNavigationCatalog`가 한 곳에서 공급한다.
- 패널은 탭을 열었을 때만 나타나며, 26% dim 뒤로 월드를 유지한다. 탭 클릭, 선택 상태, hover/pressed, `← 사무실`, ESC가 실제 uGUI Button/스택으로 동작한다.
- 최소 본문 글꼴은 15px이며, 회사명 24px, 시간 20px, 탭 18px, 헤더 32/18px, 카드 제목 21–26px, 설명 16–17px다.
- 캔버스는 1920×1080, width/height match 0.5, `Screen.safeArea`를 사용한다. 1280×720, 1920×1080, 1920×1200, 3440×1080 계산 계약을 검증한다.
- 한국어는 기존 `ManagementUiFontCatalog_v1`의 TMP 동적 폰트와 fallback 체인을 재사용한다. PNG에는 글자·날짜·숫자·가격이 없다.
- generated Sprite가 없으면 검은 debug fallback을 만들지 않고 `MAIN_NAVIGATION_V2_ASSET_MISSING` 예외로 QA를 실패시킨다.

## 주식시장 정본과 진입 스택

기존 정본을 이동하거나 복제하지 않았다.

- state: `Assets/FamilyCompany/Simulation/Game/GameState.cs`
- DTO/runtime: `Assets/FamilyCompany/Simulation/Market/StockMarketSessionStateDto.cs`, `BrokerageAccountStateDto.cs`, `StockMarketRuntimeSession.cs`
- company/brokerage transfer: `CompanyBrokerageTransferService.cs`
- state bridge: `StockMarketGameStateBridge.cs`
- save: `Assets/FamilyCompany/Save/GameSaveDto.cs`, `GameSaveMapper.cs`
- 기존 화면/public entry: `Assets/FamilyCompany/Presentation.Unity/StockMarketFullscreenPanel.cs`의 `OpenNow`/`CloseNow`
- 새 얇은 연결: `Assets/FamilyCompany/Presentation.Unity/MainNavigation/StockMarketNavigationAdapter.cs`

사용자 진입은 `사무실 → 투자 → 주식시장` 하나뿐이다. 주식 화면 ESC/뒤로는 투자 허브, 투자 허브 ESC/뒤로는 사무실로 돌아간다. F3는 주식시장이 이미 열린 경우 닫기만 하므로 메인 HUD 직접 진입점이 아니다. 투자 요약은 같은 `GameState.StockMarket` 인스턴스에서 회사 현금, 증권 예수금, 보유 종목 수만 읽는다.

## 병렬 연동 대기 계약

- 건축 thread `019ffe03-7dc2-7751-99e4-1806af95adc6`: 회사 허브 `건축·편집`, route `company.building-editor`. `MainNavigationHudPresenter.ConfigureFeatureRoute`의 `COMMANDER INTEGRATION HOOK`에서 public adapter만 연결한다.
- 계약 thread `019fea50-0c8a-7ad0-86ea-546193c90ff2`: 사업 허브 `하청 계약`/`자체 제품`, routes `business.contracts`/`business.products`. 같은 파일의 route hook과 `ResolveFeaturePresentation` view-model hook만 연결한다.
- 실제 고객사명은 계약 adapter가 공개한 display name을 그대로 TMP로 표시한다. UI 브랜치에 기업 목록·계약·보상·해금·저장 로직 또는 실존 기업 로고를 복제하지 않는다.

## built-in ImageGen 증거와 자산

- 생성 경로: OpenAI built-in `image_gen`
- CLI/API fallback: 사용하지 않음
- exact prompt, 결과 `exec-*` ID, 최종 프로젝트 경로, 참조 이미지, alpha-edit 계보:
  `Assets/Art/UI/Resources/MainNavigationV2/Generation/main_navigation_imagegen_ledger_v2.json`
- 시각 target:
  `Assets/Art/UI/Resources/MainNavigationV2/Reference/main_navigation_v2_visual_target.png`
- 런타임 자산: `Frames` 22장, `Icons/Bottom` 5장, `Icons/Investment` 5장, `Markers` 2장, 합계 34장
- 후처리: `Tools/Prepare-MainNavigationV2Assets.ps1`가 RGBA 정규화, invisible RGB zero, alpha audit, 프레임/마커 8px 투명 gutter trim을 재현한다.
- GUID/import: `Tools/Write-MainNavigationV2Meta.ps1`가 프로젝트 상대 경로 기반 결정론적 GUID와 Sprite meta를 재현한다.
- Unity import: PPU 100, Bilinear, Clamp, mipmap off, uncompressed, icon max 512, frame max 2048.

### Sprite border (left, bottom, right, top)

| 자산군 | border |
|---|---|
| top HUD | `80,52,80,52` |
| company badge | `250,80,120,80` |
| time badge | `170,82,116,82` |
| speed normal/hover/selected/pressed | `70,44,70,44` / `70,46,70,46` / `70,36,70,36` / `70,46,70,46` |
| dock | `120,82,120,82` |
| tab normal/hover/selected/pressed | `104,70,104,70` / `104,92,104,92` / `104,70,104,70` / `104,66,104,66` |
| modal / header | `132,132,132,132` / `150,92,150,92` |
| card normal/hover/disabled | `142,112,142,112` |
| featured / featured hover | `188,132,188,132` |
| close normal/hover/pressed | `110,110,110,110` |
| notification / ribbon | `82,54,82,54` / `102,54,102,54` |

초기 정적 9-slice 검사에서 생성용 대형 투명 padding 때문에 작은 상태 프레임이 사라지는 결함을 발견했다. 런타임 PNG 외부 gutter를 8px로 정리하고 위 border로 재산정한 뒤, 실제 목표 크기와 좁은 stress 크기에서 재렌더링해 모든 코너와 중심이 유지되는 것을 육안 확인했다.

## 검증 결과

### PASS

- `Tools/Validate-MainNavigationHud.ps1`
  - 5개 definition, 전역 중복 ID 없음, 5개 탭 route, 투자 전용 주식 route, 회사/사업 placeholder, 사무실 복귀, ESC 우선순위
  - 1280×720, 1920×1080 safe area, 1920×1200, 3440×1080 layout contract
  - Unity 6000.3.21f1 bundled Roslyn으로 runtime 전체와 Editor validator 외부 compile
  - generated Sprite, `Image.Type.Sliced`, SpriteSwap, asset-missing hard fail, 제거 대상 HUD 문자열 비노출 정적 계약
- alpha audit: runtime PNG 34장 모두 RGBA, alpha `0..255`, corner `0`, transparent pixel 존재, invisible halo `0`
- GUID/meta: 프로젝트 PNG 35장(visual target 포함)과 ledger에 결정론적 `.meta`; GUID 중복 없음
- 시각 확인: 100%, 50%, runtime-scale contact sheet와 목표 크기 9-slice sheet를 `view_image`로 확인
- state 수정: tab hover는 normal 실루엣, card hover는 normal 실루엣, featured hover는 featured 실루엣을 참조한 별도 built-in 편집 결과로 교체
- `git diff --check`

### 실제 Unity 실행 차단

설치된 정확한 에디터 `C:/Users/godho/Documents/Codex/UnityEditors/6000.3.21f1/Editor/Unity.exe`를 batch/no-graphics와 GUI license 경로로 실제 실행했다. 두 실행 모두 Unity Licensing Client가 entitlement 0을 반환하고 import 전에 exit code 198로 종료했다.

- `Artifacts/MainNavigationHudV2/unity-v2-validation.log`
- `Artifacts/MainNavigationHudV2/unity-v2-validation-gui-license.log`
- 핵심 메시지: `No valid Unity Editor license found. Please activate your license.`

따라서 V2에 대해 실제 Editor import, PlayMode, D3D11 player build/클릭 캡처는 PASS로 주장하지 않는다. 이전 V1 D3D11 캡처는 기능 회귀 증거일 뿐 사용자가 거절한 시각이라 V2 완료 캡처로 재사용하지 않는다. 라이선스가 복구되면 `MainNavigationHudCaptureQa.BuildAndCapture`로 닫힌 HUD, 5개 허브, 투자→주식, hover/pressed/selected, 16:10을 다시 생성해야 한다.

## 강제 시각 QA 판정

| 항목 | 현재 판정 | 근거/한계 |
|---|---|---|
| black flat bar visible 0 | 정적 PASS | 모든 생성 surface/contact sheet에 검은 바 없음; 실제 player는 차단 |
| unstyled gray card 0 | 정적 PASS | cream/mint/teal generated card만 사용 |
| clipped Korean 0 | 구조 PASS / player 차단 | TMP safe layout·fallback 계약 검증, 실제 렌더 캡처 미실행 |
| body font `<15px` 0 | PASS | `MinimumBodyFontSize = 15f` 및 모든 정의 15px 이상 |
| selected ambiguity 0 | 정적 PASS | coral/gold selected와 teal pressed가 명확히 구분됨 |
| generated asset missing 0 | 정적 PASS | resource 경로 34개·meta 존재; 실제 AssetDatabase import는 차단 |
| 9-slice corner distortion 0 | 정적 PASS | 실제 target-size 독립 9-slice 렌더 육안 검사; Unity import 렌더는 차단 |

## 시각 QA 산출물

- 거절 전: `Artifacts/MainNavigationHudV2/before-rejected-1920x1080.png`
- V2 visual target: `Assets/Art/UI/Resources/MainNavigationV2/Reference/main_navigation_v2_visual_target.png`
- 100%: `Artifacts/MainNavigationHudV2/main-navigation-v2-assets-100pct.png`
- 50%: `Artifacts/MainNavigationHudV2/main-navigation-v2-assets-50pct.png`
- 실제 게임 축소: `Artifacts/MainNavigationHudV2/main-navigation-v2-assets-runtime-scale.png`
- 실제 목표 크기 9-slice: `Artifacts/MainNavigationHudV2/main-navigation-v2-nine-slice-qa.png`

## 통합 상태

통합 준비 후보 상태이며 commit/push/main 통합/배포는 보류한다. 최신 main과 병렬 벽·착석·체력/욕구·건축·계약 작업이 합쳐진 뒤, commander가 순서를 지정하면 최신 main 위에서 adapter hook을 연결하고 Unity Editor/PlayMode/D3D11 전체 회귀를 재실행해야 한다.
