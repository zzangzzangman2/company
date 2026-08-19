# Main Navigation HUD V2 — ImageGen 전면 재디자인

## 현재 통합 상태

- `MainNavigationV2` 구현 `884c53f`, dependency route `bc19d0c`, compile 보강 `4cf6e50`이 local main 기준선에 통합되어 있다.
- 거부된 V1 UI/자산은 runtime에서 제거되었으며 현재 진입 정본은 V2 하나다.
- 회사의 build editor, 사업의 계약/제품, 투자의 주식 route가 실제 adapter에 연결되어 있다.
- 최종 seating/stamina 결합 SHA와 새 Windows build 상태는 [PROJECT_STATE.md](PROJECT_STATE.md)를 따른다.

## 결과 계약

- 상단은 generated Sprite 기반 회사명 배지, `GameState.Time.Now` 날짜·시간, `PrototypeBootstrap.WorldTimeScale`의 1x/2x/4x만 표시한다.
- 기존 가족별 긴 상태줄, LIVE, 저장 완료, 단축키 도움, 관리 화면 버튼은 사무실 메인 HUD에서 숨긴다. 저장/시간 정본은 변경하지 않는다.
- 하단은 회사, 인사, 사업, 연구, 투자 5개 탭만 표시한다. 모든 탭·카드 ID, 한글명, 설명, 상태, 아이콘 경로는 `MainNavigationCatalog`가 한 곳에서 공급한다.
- 패널은 탭을 열었을 때만 나타나며, 26% dim 뒤로 월드를 유지한다. 탭 클릭, 선택 상태, hover/pressed, `← 사무실`, ESC가 실제 uGUI Button/스택으로 동작한다.
- 최소 본문 글꼴은 15px이며, 회사명 24px, 시간 20px, 탭 18px, 헤더 32/18px, 카드 제목 21–26px, 설명 16–17px다.
- 캔버스는 1920×1080, width/height match 0.5, `Screen.safeArea`를 사용한다. 1920×1080, 1600×900, 1600×1000, 2560×1440 계산 계약과 실제 캡처를 검증한다.
- 한국어는 기존 `ManagementUiFontCatalog_v1`의 TMP 동적 폰트와 fallback 체인을 재사용한다. PNG에는 글자·날짜·숫자·가격이 없다.
- generated Sprite가 없으면 검은 debug fallback을 만들지 않고 `MAIN_NAVIGATION_V2_ASSET_MISSING` 예외로 QA를 실패시킨다.

## 프레임 스케일 계약 (2026-08-19)

- 모든 sliced 프레임 `Image`에는 `UiNineSliceFitter`가 붙는다. `CreateSpritePanel`이 sliced로 만들 때
  자동으로 붙이며, rect가 바뀔 때마다 `pixelsPerUnitMultiplier`를 다시 계산한다.
- 멀티플라이어는 스프라이트 높이를 rect 높이에 맞추는 값이 기본이고, 중앙 stretch 영역이 어느 축에서든
  10% 미만이 될 때만 더 키운다. 절대 1 미만으로 내려가지 않는다.
- 이 계약이 없으면 Unity가 border를 rect에 맞춰 눌러 중앙을 0으로 만들고, 프레임이 양 끝 cap만 남아
  캡슐·동그라미로 렌더된다. 상단 배지 3종, 하단 탭 5개, 카드와 리본이 모두 그 상태였다.
- 상단 HUD 84px, 배지 64px, 하단 dock 120px, 탭 92px가 현재 값이다. 이보다 낮추면 배지의 원형 medallion과
  탭의 모서리 장식이 다시 눌린다.
- 배지 위 오버레이는 배지 높이의 비율로 배치한다. `company_badge_v2`(1015×220)의 medallion 중심 x=121은
  `0.550h`, `time_badge_v2`(1012×233)의 socket 중심 x=120은 `0.515h`다. 날짜 배지의 teal socket에는 요일
  한 글자를 넣어 날짜가 plaque 중앙에 오도록 한다.
- 프레임 아트를 다시 내보내면 `MainNavigationHudPresenter`의 배지 비율 상수를 다시 재야 한다.
- 카드의 첫 줄과 마지막 줄은 `CardCornerOrnamentInset`만큼 들여 쓴다. `card_normal_v2`는 코럴 장식을
  스프라이트 x 33..60에 그리는데 카드 padding 18로는 그 위에 글자가 얹힌다. 하청 계약 카드의
  `T0 동네 사업자`와 `지금 배정 가능 N명`이 그 경우였다.
- 패널을 교체할 때 `ClearChildren`은 `Destroy` 전에 `SetParent(null, false)`로 떼어 낸다. `Destroy`는
  프레임 끝에 적용되므로 떼어 내지 않으면 이전 패널과 새 패널의 글자가 한 프레임 겹쳐 렌더된다.

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

## 실제 어댑터 연동

- 회사 허브 `사무실 관리`는 route `company.hub.build_editor`에서 `OfficeBuildEditorNavigationAdapter.EntryId/TryOpen`을 호출한다. 카테고리별 구매와 타일 중심 배치를 제공하며, 편집기가 열린 동안만 시뮬레이션을 일시정지하고 메인 HUD를 숨기고 닫기 뒤 회사 허브로 복귀한다.
- 사업 허브 `하청 계약`과 `자체 제품`은 `ContractBusinessRuntimeAdapter`를 `KoreaHistoryV1RuntimeCatalog`와 함께 구성하고 각각 `business.contracts`, `business.products`를 연다.
- 고객사명, 제안, 제품 진행도는 adapter가 공개한 view data만 TMP로 표시한다. UI 브랜치에 기업 목록·계약·보상·해금·저장 로직을 복제하지 않는다.
- 인사, 연구, 그 밖의 회사·투자 카드는 모두 클릭 가능한 전용 화면으로 진입하고 현재 미구현임을 `준비 중`으로 명시한다.

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

### Unity 6000.3.21f1 PASS

- `Tools/Validate-MainNavigationHud.ps1`: 5개 탭, 21개 전역 고유 카드, 모든 카드 route, 투자 전용 주식 route, 회사/사업 adapter route, feature back/ESC, 네 layout 계산, runtime/editor 외부 compile, V1 자산 부재, V2 Sprite import/9-slice를 검증했다.
- Editor batch: `MAIN_NAVIGATION_HUD_VALIDATION: PASS`; V2 runtime Sprite 34장과 한국어 glyph 193개를 확인했다.
- D3D11 Development Player: `PLAYER_QA_PASS`; 실제 포인터 route 52회, 키보드 submit 1회, 21개 카드, 1x/2x/4x, 계약/제품/건축/주식, 주식 저장·불러오기, 계약 loaded-state rebind, ESC/back, build-only pause를 검증했다.
- 캡처: 1920×1080, 1600×900, 1600×1000은 실제 창 크기, 2560×1440은 물리 디스플레이 높이 제한 때문에 1280×720 D3D11 렌더의 Unity `CaptureScreenshot` supersize 2로 정확히 출력했다. 같은 16:9 Canvas의 목표 pixel geometry는 Editor layout validator가 별도로 검증했다.
- `git diff --check` PASS. 외부 compile 경고 2건은 기존 `OfficeFurnitureCollisionCatalog`의 미할당 직렬화 필드이며 오류는 0건이다.

### 강제 시각 QA 판정

| 항목 | 판정 | 근거 |
|---|---|---|
| black flat bar visible 0 | PASS | 실제 네 해상도 캡처에 검은 상·하단 바 없음 |
| unstyled gray card 0 | PASS | cream/coral/teal generated V2 surface만 사용 |
| clipped Korean 0 | PASS | 허브, 계약, 제품, 준비중 전용 화면 실제 캡처 확인 |
| body font `<15px` 0 | PASS | 최소 15px 계약과 실제 렌더 확인 |
| selected ambiguity 0 | PASS | coral/gold selected, teal hover/pressed 상태 캡처 확인 |
| generated asset missing 0 | PASS | runtime Sprite 34개 실제 Unity import 및 player render |
| 9-slice corner distortion 0 | PASS | 네 해상도 실제 캡처와 Editor layout validator 확인 |

## 시각 QA 산출물

- V2 visual target: `Assets/Art/UI/Resources/MainNavigationV2/Reference/main_navigation_v2_visual_target.png`
- 실제 D3D11 캡처와 report: `Artifacts/MainNavigationHudFinal/D3D11-Final/`
- 대표 화면: `menu-company-1920x1080.png`, `detail-contract-board-1920x1080.png`, `detail-product-opportunities-1920x1080.png`, `build-editor-from-company-1920x1080.png`, `menu-investment-hub-1920x1080.png`, `stock-market-from-investment-1920x1080.png`
- 해상도: `menu-company-1600x900.png`, `menu-company-1600x1000.png`, `menu-company-2560x1440.png`
- 로그: `Artifacts/MainNavigationHudFinal/unity-main-navigation-validation-final.log`, `unity-main-navigation-player-build.log`, `main-navigation-d3d11-player-final.log`

## 통합 상태

Local main `4cf6e50` 기준 **통합 완료**다. 계약·건축·주식 adapter와 V2 UI가 함께 연결되었고 V1/거절본은 runtime과 tracked 자산에서 제거되었다. seating/stamina까지 합친 최종 main 재검증과 fresh portable build는 [PROJECT_STATE.md](PROJECT_STATE.md)의 pending 항목이다.
