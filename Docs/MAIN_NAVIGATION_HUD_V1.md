# Main Navigation HUD V1

## 작업 격리와 기준점

- worktree: `C:/Users/godho/Documents/Codex/2026-08-14/family-company-main-navigation-ui/worktree`
- branch: `codex/main-navigation-hud`
- 시작 HEAD: `9ad8eb7b88e85b5f6ff70161a770add48793b84b`
- 시작 `origin/main`: `9ad8eb7b88e85b5f6ff70161a770add48793b84b`
- 이 브랜치에서는 commit, push, main 통합, 배포용 최종 빌드를 하지 않는다.

## 런타임 계약

- Playing 화면의 상단 HUD는 회사 이름, `GameState.Time.Now` 날짜·시간, `PrototypeBootstrap.WorldTimeScale`에 연결된 1x/2x/4x만 표시한다.
- 기존 가족별 상태 카드, LIVE, 저장 알림, 단축키 도움, 관리 화면 버튼은 Playing HUD에서 숨긴다. 관리 화면, 저장, 시간 시스템은 제거하지 않는다.
- 하단에는 회사, 인사, 사업, 연구, 투자 다섯 탭만 항상 표시한다. 탭 정의와 카드 데이터는 `MainNavigationCatalog` 한 곳이 소유한다.
- 중앙 패널은 탭을 열었을 때만 표시하며, 실제 uGUI Button hit target, 활성 탭 상태, `사무실로`, ESC 우선 닫기를 제공한다.
- 주식시장의 유일한 사용자 진입 경로는 `사무실 → 투자 → 주식시장`이다. F3는 주식시장이 이미 열렸을 때 닫기만 수행한다.
- 주식시장 첫 ESC는 투자 허브, 다음 ESC는 사무실로 돌아간다. 주식 화면 중 메인 HUD Canvas는 숨겨 겹침을 막는다.
- 투자 허브의 회사 현금·증권 예수금·보유 종목 수는 현재 `GameState.StockMarket`에서만 읽으며 가짜 값을 만들지 않는다.
- Canvas는 1920×1080 reference, 0.5 width/height match, `Screen.safeArea` 기반 anchor를 사용한다.
- 한국어는 프로젝트 `ManagementUiFontCatalog_v1`의 Pretendard/Maplestory 동적 TMP font와 fallback 체인을 재사용한다. 이미지에는 글자를 굽지 않는다.

## 기존 주식시장 정본과 새 연결

기존 정본(수정·복제하지 않고 재사용):

- state: `Assets/FamilyCompany/Simulation/Game/GameState.cs`의 `StockMarket`/`ReplaceStockMarketState`
- DTO: `Assets/FamilyCompany/Simulation/Market/StockMarketSessionStateDto.cs`, `BrokerageAccountStateDto.cs`
- runtime/trading: `StockMarketRuntimeSession.cs`, `CompanyBrokerageTransferService.cs`
- state bridge: `StockMarketGameStateBridge.cs`의 `Load`/`Flush`
- save: `Assets/FamilyCompany/Save/GameSaveDto.cs`, `GameSaveMapper.cs`
- UI/controller/public entry: `Assets/FamilyCompany/Presentation.Unity/StockMarketFullscreenPanel.cs`의 `OpenNow`/`CloseNow`
- canonical regression: `Assets/FamilyCompany/Editor/StockMarketLandscapeValidation.cs`

새 연결은 `Assets/FamilyCompany/Presentation.Unity/MainNavigation/StockMarketNavigationAdapter.cs` 하나다. 이 어댑터는 활성 탭이 `investment`인지 검사하고 기존 단일 `StockMarketFullscreenPanel.OpenNow()`만 호출한다. 별도 시세·계좌·주문·저장 상태나 Update loop를 만들지 않는다.

## 통합 대기 route 계약

- 건축 작업 `019ffe03-7dc2-7751-99e4-1806af95adc6`: 회사 허브 `건축·편집`, placeholder `company.building-editor`. 현재 비활성/준비 중이며 editor/shop/placement/economy 로직은 없다.
- 계약 성장 작업 `019fea50-0c8a-7ad0-86ea-546193c90ff2`: 사업 허브 `하청 계약`/`자체 제품`, placeholders `business.contracts`/`business.products`. 현재 비활성/준비 중이며 계약·보상·해금·저장 로직은 없다.
- 정확한 연결 지점: `MainNavigationHudPresenter.ConfigureFeatureRoute(...)`의 `COMMANDER INTEGRATION HOOK`.
- 정확한 view-model 지점: `MainNavigationHudPresenter.ResolveFeaturePresentation(...)`의 `COMMANDER VIEW-MODEL HOOK`.
- 실제 고객사명은 전담 adapter의 visible client display name을 그대로 사용한다. day-one 대기업 비노출/실적 해금도 adapter 결과를 따른다. 기존 로고 참조만 재사용하며 새 실존 기업 로고를 생성·변형하지 않는다.

## ImageGen 자산

- 생성 도구: OpenAI built-in `image_gen`
- CLI/API fallback: 사용하지 않음
- 정확한 최종 프롬프트 원문, built-in 생성 결과 경로, 프로젝트 경로, SHA-256:
  `Assets/Art/UI/Resources/MainNavigation/Generation/main_navigation_imagegen_ledger_v1.json`
- 프레임은 생성 결과의 투명 여백만 결정론적으로 crop했다. `(30,168)-(1642,745)`, 최종 1612×577 RGBA이며 아트 픽셀은 변경하지 않았다.
- 축소 QA: 64px contact sheet에서 건물, 직원, 프로젝트 공구, 연구 플라스크, 투자 차트가 서로 구분된다.

| 자산 | 프로젝트 경로 | SHA-256 |
|---|---|---|
| 9-slice frame | `Assets/Art/UI/Resources/MainNavigation/main_nav_frame_v1.png` | `5CFFBEEDDF36A650ACE0CC5949EDFE13E81D85BC66C566513B7434552A8933B2` |
| company | `Assets/Art/UI/Resources/MainNavigation/main_nav_icon_company_v1.png` | `05DD7DB0B432E1AB576F2C810E86581F7F1CF88DF5C0A28DD4BAA792DB82C2EC` |
| people | `Assets/Art/UI/Resources/MainNavigation/main_nav_icon_people_v1.png` | `4745ADDDA70D43A533FBCD2894450166E8B05D3EFD8385B7006B258F49C32F88` |
| projects | `Assets/Art/UI/Resources/MainNavigation/main_nav_icon_projects_v1.png` | `43CFCEBD7DF42CB7BFA5DB54D6AB4E85F86E19F942C7621C060F8C183DE817FD` |
| research | `Assets/Art/UI/Resources/MainNavigation/main_nav_icon_research_v1.png` | `32A6FB368DB59FBBB467AF784145BC238166CD2B371F027F0AF25BDC041E34F0` |
| investment | `Assets/Art/UI/Resources/MainNavigation/main_nav_icon_investment_v1.png` | `4C1E28AD174333DAEDF41CFE8D727B20DD2C33A819198962B49327171892EEF3` |

## 검증

- `Tools/Validate-MainNavigationHud.ps1`
  - 5 definitions, 전역 중복 ID 없음, 5 routes, 투자 전용 주식 route, 회사/사업 placeholder 계약, 사무실 복귀, ESC: PASS
  - 1280×720, 1920×1080 safe area, 1920×1200, 3440×1080 layout contract: PASS
  - runtime/Editor validator 외부 Roslyn compile: PASS
- Unity `6000.3.21f1` 실제 Editor validator:
  - catalog/routes, 9-slice/import, 한국어 source glyph 193개, safe-area layout: PASS
- 기존 `StockMarketLandscapeValidation.Run`:
  - live 매수/매도, 회사↔증권계좌, 저장 왕복, listing carry, residual reopen: PASS
- Windows Development QA Player, `-force-d3d11`, Intel(R) Graphics:
  - build warnings 0
  - 1x/2x/4x actual pointer routes: PASS
  - 5 tab + 투자 주식 카드 + 재진입 + office-return 포함 actual pointer 13 routes: PASS
  - main HUD 직접 주식 버튼 0, 투자 전용 진입, canonical GameState 동일 인스턴스: PASS
  - 동일 state 재진입 runtime session 생성 1회, adapter Update/subscription 0: PASS
  - 격리 `UnityJsonSaveRepository` JSON 저장/로드 후 portfolio 및 route 유지: PASS
  - 주식시장 → 투자 → 사무실 back stack: PASS
  - 1920×1080 7장(투자 허브/주식시장 포함), 1680×1050 1장: PASS
  - 캡처 PNG 비검은 픽셀 검사: PASS
  - glyph error 및 QA exception: 없음
- 리포트: `Artifacts/MainNavigationHudQa/main-navigation-hud-player-qa.txt`
- 로그: `Artifacts/MainNavigationHudQa/unity-main-navigation-validation.log`, `unity-stock-market-validation.log`, `unity-main-navigation-player-build.log`, `main-navigation-d3d11-player.log`
- 격리 저장 QA: `Artifacts/MainNavigationHudQa/stock-save-route-qa/family-company-save-slot-3.json`

## 통합 메모

- 작업 중 `origin/main`은 `52a787f`(Starter Office perimeter wall redesign)로 한 커밋 전진했다.
- 새 main의 런타임 변경 파일과 HUD 런타임 변경 파일은 직접 겹치지 않는다.
- 새 main이 `Docs/PROJECT_STATE.md`, `Docs/DECISIONS.md`, `Docs/ASSET_MANIFEST.md`를 수정했으므로 이 브랜치는 해당 공유 문서를 일부러 수정하지 않고 이 전용 문서와 ImageGen ledger에 기록했다.
- 통합 시 최신 main 위에 HUD 변경을 적용한 뒤, 벽·착석·체력/욕구 작업을 모두 포함한 최종 Windows 회귀 캡처를 다시 실행해야 한다.
- 건축·계약 성장 전담 작업의 최종 public route ID/adapter/view model이 도착하면 위 placeholder 상수와 hook만 얇게 연결하고 동일 QA를 재실행한다.
