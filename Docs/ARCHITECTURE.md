# ARCHITECTURE

## Fast QA boundary

`FAST_QA_WINDOWS.cmd` owns short feedback selection, project-local locking, compatibility fingerprints,
timing JSON, and hidden process ownership. The Editor-only `FastQaEditorEntry` invokes existing validations
or a dedicated Windows Fast QA build. Release ownership stays with `WindowsPlayerBuild` and
`BUILD_WINDOWS.cmd`; Fast QA output can never be promoted by release scripts.

`FamilyCompany.Simulation` keeps `noEngineReferences`; its fast profile uses the compiler bundled with the
selected Unity installation and runs deterministic smoke/stamina checks without starting Unity. Runtime
scripts-only builds require a compatible prebuilt Fast QA player. Asset and serialization-layout changes
cross the data-build boundary and fall back.

## 계층

- FamilyCompany.Simulation: Unity 참조가 없는 시간, RNG, 이벤트, 가족, 회사, 회계, 역사 resolver, 시장 코어, 게임 상태
- FamilyCompany.Simulation.Contracts: 실제 고객 회사 ID, 소형 하청 제안, 4인 팀 수락 용량 정책
- FamilyCompany.Simulation.Banking: 연도별 금리, 예금·대출·어음 할인 순수 계산 규칙
- FamilyCompany.Simulation.Organization: 업종별 채용 직군, S~F 후보 제안과 인사 순수 규칙
- FamilyCompany.Simulation.Workforce: 가족·채용 직원 공용 6능력, 잠재력 등급, 업무 프로필·성장·스트레스 저항 규칙
- FamilyCompany.Simulation.Progression: 은행·R&D·채용·법인계좌·인수합병 의미 접근 조건
- FamilyCompany.Simulation.Leisure: 시대·요일·참가자 조건을 가진 회복 활동 카탈로그, 결정론적 추천·실행·오디오 큐와 완료 활동 추억 의미 데이터
- FamilyCompany.Save: 저장 DTO와 저장소 인터페이스
- FamilyCompany.Infrastructure.Unity: JsonUtility 기반 Korea History V1 로더와 persistentDataPath 저장 어댑터
- FamilyCompany.Presentation.Unity: 입력, 카메라, 화면 표시, 씬 오브젝트 연결
- FamilyCompany.Editor: 프로토타입 씬 생성과 헤드리스 검증
- FamilyCompany.Content.History: 실제 회사·사건·출처의 읽기 전용 JSON 데이터

## 핵심 불변식

- 시간의 원천은 캠페인 시작 이후 흐른 정수 분 하나다.
- 게임 플레이 RNG는 seed와 안정 키로부터 재현된다. UnityEngine.Random을 사용하지 않는다.
- 같은 시각의 예약 이벤트는 dueMinute, priority, eventId 순서로 처리한다.
- 돈은 long 원 단위다. 소수 부동소수점으로 돈을 보관하지 않는다.
- 모든 회계 거래는 차변 합계와 대변 합계가 같다.
- 저장 대상은 의미 상태다. Transform, 렌더러 캐시, UI 선택 상태는 저장하지 않는다.
- 가족 자율 행동은 30분 절대 경계에서 진행하며 현재 행동·의미 목적지·처리 시각·누적 업무/휴식·사건만 저장한다. 같은 seed와 목표 시각이면 시간 진행 호출을 나눠도 결과가 같다.
- P1.5 `OfficeInteractions`는 Unity 참조가 없는 Interaction Definition/Catalog와 정수 Utility 점수 추적을 소유한다. 현재 선택 정본은 기존 `WeightedPick`이며 Shadow 선택과 score trace는 저장 상태나 행동 결과를 변경하지 않는다.
- 전체 저장 스키마는 `GameSaveDto v10`이며 v1~v9를 읽어 결정론적으로 이관한다. 계약 페이로드는 v2에서 도입되었고 제안 원본, 수락·납기·해결 시각, 상태, 완료 인시와 가족별 기여 인시를 보존한다. 계약 이전 v1 저장은 빈 계약 목록으로 이관한다. v9에서 도입한 semantic stamina를 유지하고 v10은 공용 업무 능력·XP remainder·스트레스 증가 배율을 추가한다.
- 별도 Speed 능력은 없다. 업무 진행률은 `WorkTaskProfile`의 6능력 가중 점수로 계산하고 1인시마다 필요한 정수 GameTime 분을 확정한 뒤에만 계약 기여·XP를 기록한다. E키 유지 실시간이나 프레임 시간은 입력이 아니다. legacy Speed/Stamina/Mental은 v1~v9 이관 경계 밖에서 읽지 않는다. Mental은 계약 품질이 아니라 GameTime 스트레스 증가량을 보정하는 스트레스 저항으로만 이관한다.
- 주식시장 session·호가·체결 계산은 순수 C#이며 `companyId + date + minute + pulse`를 안정 키로 사용한다.
- 플레이어 지정가 대기주문은 가격우선·시간우선 FIFO와 queue-ahead를 순수 C#으로 유지하고, Unity UI·저장·원장은 이 코어의 결과만 투영한다.
- 호가 프레젠테이션은 내부 10단계를 유지하고 화면에 최우선 7매도+7매수를 표시한다.
- 체결 replay는 batch identity 중복을 막는 FIFO이며 한 단계마다 Arriving과 Draining을 각각 한 렌더 프레임 이상 공개한다. pause 중 cursor는 변하지 않는다.

## 실제 역사와 회차 상태

- HistoricalBaseline은 검증된 읽기 전용 입력이며 저장 게임이 수정하지 않는다.
- WorldState는 현재 회차의 회사, 소유관계, 제품, 기술, 재무, 주식과 지급능력 상태를 가진다.
- DivergenceLog는 기준 역사에서 취소·지연·대체·이전된 사건과 원인을 기록한다.
- 실제 회사명은 날짜별 데이터이고 영구 참조와 저장은 불변 companyId를 사용한다.
- 역사 사건은 조건부 후보이며, 선행 조건이 깨지면 원래 결과를 강제로 발생시키지 않는다.
- Korea History V1 JSON은 JsonUtility DTO로 읽은 뒤 불변 `HistoricalCompanyRegistry`로 투영한다.
- `ListedSecuritiesAt(date)`는 실제 이름과 KOSPI/KOSDAQ listing 구간을 시장 종목으로 만들고, KOSDAQ은 SIMUL의 `도전시장` 호가 규칙에 대응한다.

상세 규칙은 Docs/REAL_COMPANY_ALT_HISTORY.md, 시장 이식 경계는 Docs/SIMUL_MARKET_PORT.md를 따른다.

## 씬 경계

Prototype01은 집, 거리, 작은 사무실을 한 씬의 구역으로 보여 준다. 구역 이동은 현재 우선순위가 공간 감각 검증이므로 포털 없이 직접 걸어서 가능하게 시작한다. 향후 맵이 커지면 의미 위치 ID와 전환 시스템을 추가한다.

## 등각 도트 프레젠테이션과 OfficeGrid

- `FamilyCompany.Simulation.OfficeLayout.OfficeGrid`가 폭·높이·바닥·통행 가능 셀·배치 가구·좌석 슬롯을 의미 상태로 소유한다. Unity Transform과 화면 픽셀은 저장 정본이 아니다.
- `StarterOfficeV1`은 13×13, 실내 가구 17개와 외곽 bay 52개, 가족 workstation 4개를 가진 실제 새 게임 레이아웃이다. `CreateMigrationPreview()`는 회귀 fixture일 뿐 게임 기본값이 아니다.
- `OfficeGridTilemapPresenter`는 320×160, 180 PPU 등각 Tile을 투영하고 `OfficeGridFurniturePresenter`는 같은 placement anchor/footprint를 렌더한다.
- 전체 저장은 v10이며 `officeGrid` 하위 스키마 v4와 가구 재고 하위 스키마 v1을 보존한다. v1~v9 이관 뒤 `ComputeLayoutHash()`가 같아야 한다.
- 가구 시각 정본은 `OfficeFurnitureVisualCatalog` calibration v3, 착석 정본은 `OfficeCharacterSeatPoseCatalog` v5다. 의미 root는 scale 1이며 가구 보정은 승인된 균등 scale/socket, 착석 보정은 실제 pelvis/hand 기반 translation만 허용한다.
- `OfficeGridCharacterMover`의 SpriteRenderer는 균등 scale 1.55를 사용한다. 1.69는 화면을 과도하게 점유해 폐기된 값이다.
- 16:9가 아닌 화면에서도 타일·캐릭터를 비균등하게 늘리지 않는다. `OfficeGridCameraFitter`가 aspect-safe하게 균등 직교 크기를 조정한다.
- 월드는 final backbuffer native resolution에서 Point/pixel snap으로 렌더한다. 폐기된 `OfficeVisualV2` 통짜 화면과 저해상도 중간 버퍼를 런타임 폴백으로 되살리지 않는다.
- 고동작 시트의 실제 실루엣은 오프라인 분리기가 상체 중심·발 기준선으로 정렬한 256×256 단일 PNG 48개다. 런타임 코드가 원본 PNG를 다시 자르지 않는다.

## 실제 회사 이동

- `StarterOfficeRuntimeBootstrap`이 플레이어와 가족 4인의 `OfficeRuntimeAgent`를 단일 소유한다. legacy `PrototypePlayerController`, `OfficeWorkerAgent`, 3D `CharacterController` 경로는 Starter Runtime이 준비되면 비활성이다.
- `OfficeRuntimeOccupancy`와 결정론적 cardinal `OfficeRuntimePathService`가 static/interaction/dynamic occupancy와 예약을 처리한다.
- 플레이어·가족·계약 이동은 `OfficeSharedLocomotionRules`를 공유한다. 방향과 gait는 요청 벡터가 아니라 실제 frame displacement/speed로 결정하며 막힌 입력은 걷기 위상을 진행하지 않는다.
- `OfficeAutonomyCoordinator`의 의미 목적지와 계약 coordinator의 업무 목적지는 같은 actor/path/occupancy로 투영된다. 계약 업무는 실제 도착·정지·작업 상태에서만 `RecordWork`를 호출한다.
- 평일 학교·외부 일정·영업·가사와 수면은 `FamilyScheduleRules`에서 계산하며 계약 코어와 UI가 같은 가용성 판정을 사용한다.
- 배치 직후 이동은 현재 `OfficeFurnitureCollisionCatalog` lookup을 거친다. 이를 `OfficeBuildEditorGeometryQuery`의 배치 geometry로 교체하는 hand-off는 아직 열린 기술 부채다.

## Native Smart Interaction P1.5

- `OfficeInteractionDefinition`은 행동, 의미 위치, target template, 가구 kind, 지속 시간, capacity, cooldown, 접근·예약 정책과 역할별 기존 weight를 순수 C# 데이터로 묶는다.
- `OfficeInteractionCatalog`는 현재 13종 Micro Action의 표준·회의·fallback 후보 20개를 광고한다. 기존 후보 생성은 아직 정본이며 Editor QA가 action/location/target/weight 1:1 parity를 검사한다.
- `OfficeInteractionScoring`은 기존 weight×20, macro compatibility, Energy/Stress 기반 need, 미방문 novelty, availability와 repetition을 정수로 합산한다. 후보는 OfferId 정렬 뒤 StableRandom top-band로 Shadow 선택하므로 입력 배열 순서에 독립적이다.
- `OfficeInteractionSelectionTrace`는 legacy 선택, Shadow 선택, duration, resolved target, partner와 후보별 점수 분해를 진단 이벤트로 노출한다. 구독자가 없으면 retained state가 없으며 이 진단은 전체 저장 스키마 v10에 별도 상태를 추가하지 않는다.
- `OfficeInteractionOfferFactory`는 Definition을 현재 `OfficeGrid.Furniture`에 투영해 실제 `FurnitureId`별 Offer를 만든다. 접근 칸과 capacity는 Definition에서 파생되며, passability/reachability는 호출자가 주입하므로 Simulation은 Unity를 참조하지 않는다.
- `OfficeRuntimeInteractionOfferResolver`는 현재 Occupancy와 cardinal PathService로 열린 접근 칸·도달 가능한 접근 칸만 남긴다. 결과는 캐시하지 않으며 Occupancy revision이 바뀌면 동일 intent도 다시 해석해 이동·삭제된 가구의 예전 접근 칸을 사용하지 않는다.
- Micro Action 목적지는 `OfficeInteractionCatalog`의 Interaction ID를 런타임까지 전달하고, OfferId/FurnitureId를 가진 목적지로 해석한다. 물리 회의 테이블을 사용하는 직접 플레이어/계약 경로만 작성 좌석이 생길 때까지 기존 계약을 유지한다.
- 외부 Behavior Tree·Utility AI·GOAP 패키지는 설치하지 않는다. 기존 `WeightedPick`은 계속 정본이며 Shadow Utility 활성화와 명시적 실행/중단 lifecycle은 별도 단계다.

## 오디오 프레젠테이션

- GameAudioCoordinator는 Resources의 라이선스 확인된 BGM·SFX를 지연 로드하고, 타이틀과 실제 회사 세션의 BGM을 교차 전환한다.
- 계약 수락·작업·수익·실패·저장·이동 효과음은 프레젠테이션 결과만 표현하며 시뮬레이션 상태나 RNG를 변경하지 않는다.
- OfficeSoundscapeController는 NPC의 최초 관측을 무음으로 시드하고 Inside/Outside·Walking/도착 전이만 감시한다. 퇴실·복귀 문, 프린터 종이, 계약 업무/회의 환경음은 전역 쿨다운을 거쳐 중복 폭주 없이 재생한다.
- LeisureAudioCueCatalog는 회복 활동 ID와 ImageGen 장면 ID를 1:1로 유지하고 각 장면의 진입 SFX·반복 BGM·완료 SFX를 의미 데이터로 제공한다.
- `simul`의 세로 화면 배치와 좌표는 아키텍처 입력이 아니다. 재사용 대상은 순수 규칙, 검증 교훈, 라이선스가 확인된 원본 자산이며 UI는 1920×1080 reference에서 설계하고 1600×1000 같은 16:10과 compact 창에도 반응형으로 투영한다.

## Starter Office Runtime V1

```text
PrototypeBootstrap / GameState.OfficeGrid
    ├─ StarterOfficeRuntimeBootstrap (단일 소유권·재빌드·Coordinator binding)
    ├─ OfficeRuntimeWorld
    │   ├─ OfficeRuntimeOccupancy (Static / Interaction / Dynamic / Revision)
    │   ├─ OfficeRuntimePathService (결정론적 cardinal path)
    │   ├─ OfficeNavigationTrafficRules + MotionIntegrator
    │   └─ OfficeRuntimeWorkstationService (seat claim·approach·socket)
    └─ OfficeRuntimeActorRegistry
        ├─ player + OfficeRuntimePlayerController
        ├─ older_sister
        ├─ father
        └─ mother
```

- memberId별 활성 Runtime Actor는 정확히 하나다. Starter Runtime이 준비되면 Legacy NPC/player/navigation과 Preview mover는 비활성이다.
- Occupancy는 모든 이동 후보의 start→end 구간을 actor radius로 표본 검사한다. World Update는 `OfficeNavigationMotionIntegrator`의 안정 substep으로 1×·2×·4× 시간 배속의 tunneling을 막는다.
- `OfficeSharedLocomotionRules`는 requested displacement, actual displacement/speed, display facing, gait phase를 분리한 순수 C# 경계다. 수치 오차보다 큰 실제 root 변위가 있는 모든 프레임은 실제 변위의 최근접 8방향만 표시하며 semantic/requested heading과 충돌 투영은 이를 덮어쓸 수 없다.
- 135° 이상 급반전은 감속 후 실제 정지, 인접 45° 방향을 거치는 제자리 pivot, 새 방향 가속 순서로 진행한다. 막힌 입력과 상호작용 도착도 변위 없이 같은 pivot 규칙을 사용하고 Pivot/Idle 중에는 보행 거리를 누적하지 않는다.
- `DirectionalSpriteAnimator`는 공용 규칙의 Presentation adapter다. 모든 Actor는 `OfficeLocomotionGaitRules.DefaultStrideLength` 하나를 사용하며 member ID별 보폭·회전시간·방향 허용치는 두지 않는다. 상호작용은 실제 정지, Idle, 목표 facing이 모두 성립한 뒤에만 Performing으로 전환한다.
- Actor는 현재 셀과 최대 두 개의 예정 셀을 예약하고, 실제 위치·desired velocity·stuck seconds를 기존 교통 규칙에 제공한다. 0.8초 회피, 1.1초 재탐색, 2초 예약 해제로 교착을 복구한다.
- 좌석 셀은 일반 경로에서 Interaction Occupancy다. claim된 seatId만 접근 경로와 최종 operator anchor 이동에 허용된다.
- 레이아웃 변경은 semantic `OfficeGrid`를 교체하고 Starter Runtime을 staged rebuild한다. 이전 Actor/path/reservation은 폐기되고 새 Occupancy revision과 레이아웃 해시에 맞춰 다시 바인딩된다.
- `PlacedOfficeFurniture.PlacementAnchor`가 의미·시각·충돌·저장의 공통 좌표다. `OfficeGridFurniturePresenter`는 책상 소켓에 맞추기 위해 VisualRoot만 따로 이동하지 않는다.
- Starter Runtime의 착석 표현은 `OfficeSeatingV1`의 사람 승인 `NorthWest` 14프레임과 `OfficeCharacterSeatPoseCatalog` v5를 사용한다. Work는 pelvis를 cushion에 고정하고 SitDown/StandUp은 서 있는 pelvis와 cushion 사이를 승인 프레임 순서대로 보간한다. 회전·pose 확대·member별 scale은 없으며 렌더 틱당 한 프레임만 전진해 장시간 프레임이나 배속에서도 원화를 건너뛰지 않는다.
- Windows player 빌드 전 OfficeGrid schema/migration, semantic layout hash/save round-trip, 8방향 수학, 방향 승인 32개와 개별 보행 프레임 승인 192개를 검증한다. 숨김 player QA는 4명 각각 SitDown 4 + Work 6 + StandUp 4의 실제 적용, 접점 오차, 정렬과 그래픽 합성을 확인한다.

## Pixel clarity presentation layer

```text
PixelClarityDefault (ScriptableObject policy)
    └─ PixelClarityRuntime (persistent presentation-only coordinator)
        ├─ native buffer / DPI / mip / MSAA policy
        ├─ legacy PixelatedCameraEffect disable
        ├─ aspect-safe OfficeGridCameraFitter reframe
        ├─ camera physical-pixel snap
        └─ OfficeRuntimeAgent.PresentationRenderer pre-cull snap / post-render restore
```

- 이 계층은 Simulation, occupancy, semantic layout, save state를 소유하지 않는다.
- 움직이는 actor의 `PresentationRenderer`만 렌더 callback 안에서 일시적으로 보정하며 actor root는 렌더 뒤
  원래 위치로 돌아간다. 착석 actor는 좌석 pose/anchor 소유권과 충돌하지 않도록 제외한다.
- `RenderClarityValidation`은 프로파일과 importer 범주 경계를 정적 검증하고,
  `RenderClarityRuntimeQa`는 실제 D3D11 player에서 동일 프레임 비교와 이동 grid residual을 검증한다.

## Semantic Office Layout Authoring

- `StarterOfficeLayoutAsset`은 floor, walkability, furniture footprint/anchor/facing/blocking, seat/approach/workstation binding을 직렬화한다.
- `OfficeLayoutEditorWindow`는 0.5셀 snap, 전체 footprint 이동, 회전, 복사, 삭제, Undo/Redo, Workstation Blueprint와 접근 칸 지정을 제공한다.
- `OfficePlaceableDefinition`의 기본값은 `BlocksMovement=true`다. 저장 전 overlap, 경계, 필수 좌석, approach, 출구 연결을 검증한다.
- Runtime Presenter와 Save adapter가 같은 `OfficeGrid.ComputeLayoutHash()`를 사용하며 Scene Transform은 저장 정본이 아니다.

## Perimeter visual and attendance-audio boundary

- `OfficeGridLayouts` owns the 52 semantic perimeter placements: far full wall 26, near cutaway 25, exterior threshold 1. The three source/runtime PNG pairs and their pivots are presentation assets; `OfficeFurnitureAssetBuilder.BuildPerimeterWalls` may update only their catalog definitions.
- `EntranceDoorKind` is a legacy persistence/catalog key for an always-open threshold. Navigation continues to own the canonical interior entrance `(8,1)`; door leaf, jamb, lintel, door state, and animation are not added to `OfficeRuntimeAgent`.
- `OfficeAutonomyCoordinator` observes the shift's first successful attendance release (normally 09:00, including same-state clock/day jumps into a newly observed work shift) and requests one `door_open` SFX. A newly bound save that is already mid-shift consumes the date without replaying the cue. `GameAudioCoordinator` owns playback and QA counters; later staggered entrants and `door_close` are outside this path.
