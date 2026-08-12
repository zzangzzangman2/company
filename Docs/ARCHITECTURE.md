# ARCHITECTURE

## 계층

- FamilyCompany.Simulation: Unity 참조가 없는 시간, RNG, 이벤트, 가족, 회사, 회계, 역사 resolver, 시장 코어, 게임 상태
- FamilyCompany.Simulation.Contracts: 실제 고객 회사 ID, 소형 하청 제안, 4인 팀 수락 용량 정책
- FamilyCompany.Simulation.Banking: 연도별 금리, 예금·대출·어음 할인 순수 계산 규칙
- FamilyCompany.Simulation.Organization: 업종별 채용 직군, S~F 후보 제안과 인사 순수 규칙
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
- 계약 저장은 제안 원본, 수락·납기·해결 시각, 상태, 완료 인시와 가족별 기여 인시를 보존한다. 스키마 v2는 스키마 v1을 빈 계약 목록으로 이관한다.
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

## 2.5D 도트 프레젠테이션

- 사무실과 가구는 실제 Collider를 가진 3D 모듈이다.
- Main Camera는 직교 투영으로 플레이어를 추적한다.
- PixelatedCameraEffect가 월드 렌더를 낮은 내부 해상도로 축소하고 Point 필터로 확대한다.
- 플레이어·누나·부모는 카메라 기준 이동 벡터를 45도 옥턴트로 양자화해 8방향 Sprite를 선택하고, 방향별 6프레임 보행을 0.11초 간격으로 순환한다.
- 고동작 시트의 24개 실제 실루엣은 오프라인 분리기가 상체 중심·발 기준선으로 정렬해 256×256 단일 PNG 48개로 만든다. Editor 빌더는 이를 Point·180 PPU·하단 피벗 Sprite로 임포트하며 런타임 코드가 원본 PNG를 자르지 않는다.

## OfficeGrid 타일 이행 경계

- `FamilyCompany.Simulation.OfficeLayout.OfficeGrid`가 폭·높이·행 우선 바닥 종류·통행 가능 배열·배치 가구·좌석 슬롯을 불변 의미 상태로 소유한다. Unity Transform과 화면 픽셀은 들어가지 않는다.
- 저장 스키마 v6은 `officeGrid` 서브 페이로드를 보존한다. v1~v5 저장은 결정론적인 13×13 초기 격자로 이관하고, 저장·복원 뒤 `ComputeLayoutHash()`가 같아야 한다.
- `OfficeGridTilemapPresenter`는 Unity 내장 Isometric Grid/Tilemap에 320×160, 180 PPU 바닥 Tile을 투영할 뿐 격자 상태를 소유하지 않는다.
- 캐릭터 프레젠테이션은 그리드 셀 중심을 발 기준점으로 사용하고, 화면 Y(격자의 x+y에 대응)에 따라 매 프레임 정렬한다. 누적 스케일은 균등이어야 하고 16:9 기본 카메라에서 실제 실루엣 높이는 화면의 14~18%다.
- T1~T3은 `OfficeTileMigrationPreview` 격리 씬에서 검증한다. 현재 `Prototype01`의 OfficeVisualV2·3D Collider·웨이포인트·좌석·계약은 T4/T5 이관 전까지 런타임 폴백이며 삭제하지 않는다.
- 16:9가 아닌 화면에서는 타일이나 캐릭터를 비균등하게 늘리지 않는다. `OfficeGridCameraFitter`가 균등 직교 크기만 늘려 네 격자 모서리를 보존한다.
- `OfficeGridLayouts.CreateMigrationPreview()`는 18개 가구·12종 kind·파티션을 가진 T1~T5 회귀 fixture다. 실제 새 게임과 v1~v5 저장 이관은 같은 13×13 구조에서 불필요한 파티션을 뺀 `CreateStarterOfficeV1()` 17개 가구·11종을 사용한다. Preview fixture를 게임 기본값으로 다시 연결하지 않는다.
- 각 `OfficeSeatSlot`에서 파생한 `OfficeWorkstationSlot`은 desk/chair/seat ID, seat/approach cell, facing, 반 셀 정밀도의 `OperatorAnchor`를 한 의미 단위로 묶는다. 저장 서브스키마 v3은 `operatorX2/operatorY2`를 보존하고 v1은 레거시 좌석, v2는 연결된 work-surface 방향의 반 셀 operator anchor로 안전 이관한다.
- `OfficeFurnitureVisualCatalog` calibration v2는 12종·방향별 base/front Sprite, ground/sort, 네 점 ground footprint, 의미 footprint 크기, chair seat, desk operator seat/work socket, 양의 균등 scale을 명시한다. 네 점은 타일맵이 독립 계산한 footprint 투영과 각 점 2px 이내여야 한다.
- `OfficeGridFurniturePresenter`는 의미 root를 footprint 중심·scale 1에 고정하고, 자식 `VisualRoot`에만 균등 scale과 승인된 socket 정렬을 적용한다. desk를 먼저 배치하고 desk operator seat socket에 chair seat를 맞춘다. chair·desk의 의미 root와 footprint는 이 시각 보정으로 이동하지 않는다.
- `OfficeGridCharacterMover`의 의미 root는 항상 scale 1·좌석 셀 중심이며 SpriteRenderer는 균등 scale 1.69의 자식 `VisualRoot`에 있다. 착석 Sprite가 적용된 직후 `OfficeRuntimeAgent`가 승인된 실제 pelvis를 chair seat로 옮기는 translation만 적용한다. `VisualRoot.localRotation`은 identity, pose scale은 1.0이며 일어서면 위치·회전·scale을 정본 상태로 복원한다.
- `OfficeCharacterSeatPoseCatalog` calibration v4의 키는 member/direction/clip/frame이고 각 승인 항목은 `humanApproved`와 source Sprite SHA-256을 가진다. Starter Runtime safe mode는 네 가족의 `NorthWest/Work/0` 정확히 4개만 허용하며 미승인·SHA 불일치·다른 방향/프레임을 fallback하지 않는다.
- 착석 중 정렬 stack은 `OfficeRuntimeWorkstationService`만 소유한다. character order를 기준으로 desk base `-2`, chair base `-1`, character `0`, desk front `+1`, chair back `+2`다. 책상 front는 고정 Y 절단이 아니라 앞 모서리·다리·서랍만 포함하는 원본 좌표 픽셀 마스크다.
- `OfficeTycoonAlignmentCalibrationWindow`는 가구 100/200/400% 픽셀 보기, 네 점 footprint·socket 드래그, character clip/frame·onion skin, workstation 합성과 실시간 오차를 제공한다. 자세 scale/rotation은 각각 1.000/0.000으로 잠그고 실제 pelvis/hand와 사람 승인·source SHA만 저장한다. PNG 빌더는 기존 calibration asset을 자동 승인하거나 덮어쓰지 않는다.
- `OfficeGridCollisionMonitor`는 실제 Transform을 매 프레임 가장 가까운 셀로 투영해 막힌 셀 침범을 계측하는 QA 전용 경계다. 결과는 저장하지 않는다.
- `OfficeTycoonAlignmentV2Qa`는 Preview와 Starter를 분리 실행하고 1920×1080 캡처, 실제 네 점 footprint, chair↔desk socket, pelvis↔seat, hand↔work, 프레임 안정성, 얼굴/하체 overlay, 60초 Transform 0 변화, 충돌·중복 claim·저장 왕복을 검사한다. 기존 `Prototype01`의 OfficeVisualV2·Collider·계약·자율 AI는 T6 통합 전까지 폴백으로 유지한다.
- 2026-08-11 사용자 폐기 결정으로 OfficeVisualV2 base/foreground/guide PNG는 저장소와 빌드에서 제거했다. `Prototype01`의 계약·자율 AI·Collider는 시뮬레이션 호환용으로 유지하되 Renderer/Camera는 세션 시작 때 차단하고, `OfficeTileMigrationPreview`를 additive로 올린 StarterOfficeV1만 월드로 렌더한다. `F9`는 구형 화면 복귀가 아니라 이 타일 표시를 복구하는 단방향 키다.

## 실제 회사 이동

- 플레이어는 PrototypePlayerController와 CharacterController로 직접 이동한다.
- NPC는 OfficeWorkerAgent와 CharacterController로 웨이포인트 사이를 실제 이동한다.
- OfficeAutonomyCoordinator는 순수 시뮬레이션의 의미 목적지를 실제 웨이포인트로 투영한다. 계약 coordinator가 배정한 업무는 자율 목적지보다 우선하고 완료 후 기존 자율 행동을 재개한다.
- 평일 학교·외부 일정·영업·가사와 수면은 FamilyScheduleRules에서 계산하며 계약 코어와 화면 배정이 같은 가용성 판정을 사용한다.
- 외부 일정은 `Outside` 의미 목적지를 출구 웨이포인트에 투영한다. NPC는 출구 도착 뒤 렌더만 숨기고, 복귀 행동이 정해지면 다시 표시되어 안전 통로로 걸어 들어온다.
- 플레이어 직접 작업은 PlayerOfficeWorkInteractor가 계약 단계의 의미 장소를 요구하고, 근처에서 E를 유지한 경우에만 계약 코어의 `RecordWork`를 호출한다.
- OfficeWaypoint는 위치, 업무 종류, 최소·최대 체류 시간을 가진다.
- 체류 시간은 agentId, 정거장 횟수, waypointId에 StableRandom 키를 적용해 재현된다.
- 현재 경로는 사전 정의된 안전 통로다. 사무실 배치를 플레이어가 자유 편집하게 되면 경로 탐색 계층을 추가한다.

## 오디오 프레젠테이션

- GameAudioCoordinator는 Resources의 라이선스 확인된 BGM·SFX를 지연 로드하고, 타이틀과 실제 회사 세션의 BGM을 교차 전환한다.
- 계약 수락·작업·수익·실패·저장·이동 효과음은 프레젠테이션 결과만 표현하며 시뮬레이션 상태나 RNG를 변경하지 않는다.
- OfficeSoundscapeController는 NPC의 최초 관측을 무음으로 시드하고 Inside/Outside·Walking/도착 전이만 감시한다. 퇴실·복귀 문, 프린터 종이, 계약 업무/회의 환경음은 전역 쿨다운을 거쳐 중복 폭주 없이 재생한다.
- LeisureAudioCueCatalog는 회복 활동 ID와 ImageGen 장면 ID를 1:1로 유지하고 각 장면의 진입 SFX·반복 BGM·완료 SFX를 의미 데이터로 제공한다.
- `simul`의 세로 화면 배치와 좌표는 아키텍처 입력이 아니다. 재사용 대상은 순수 규칙, 검증 교훈, 라이선스가 확인된 원본 자산이며 모든 후속 화면은 1920×1080 16:9에 새로 투영한다.

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
- Actor는 현재 셀과 최대 두 개의 예정 셀을 예약하고, 실제 위치·desired velocity·stuck seconds를 기존 교통 규칙에 제공한다. 0.8초 회피, 1.1초 재탐색, 2초 예약 해제로 교착을 복구한다.
- 좌석 셀은 일반 경로에서 Interaction Occupancy다. claim된 seatId만 접근 경로와 최종 operator anchor 이동에 허용된다.
- 레이아웃 변경은 semantic `OfficeGrid`를 교체하고 Starter Runtime을 staged rebuild한다. 이전 Actor/path/reservation은 폐기되고 새 Occupancy revision과 레이아웃 해시에 맞춰 다시 바인딩된다.
- `PlacedOfficeFurniture.PlacementAnchor`가 의미·시각·충돌·저장의 공통 좌표다. `OfficeGridFurniturePresenter`는 책상 소켓에 맞추기 위해 VisualRoot만 따로 이동하지 않는다.
- Starter Runtime의 착석 표현은 `OfficeSeatingV1`의 사람 승인 `NorthWest/Work/0`과 `OfficeCharacterSeatPoseCatalog` v4만 사용한다. Animator가 Sprite를 적용한 뒤 이벤트로 pelvis→chair seat translation만 수행하며 회전·확대와 member/seat별 위치 offset은 없다. 손 오차는 실제 손 anchor와 아트로 해결한다.
- Windows player 빌드 전 OfficeGrid schema/migration, semantic layout hash/save round-trip, 8방향 수학과 32개 사람 승인 manifest를 검증한다. 그래픽 합성은 player RenderTexture QA 캡처로 별도 확인한다.

## Semantic Office Layout Authoring

- `StarterOfficeLayoutAsset`은 floor, walkability, furniture footprint/anchor/facing/blocking, seat/approach/workstation binding을 직렬화한다.
- `OfficeLayoutEditorWindow`는 0.5셀 snap, 전체 footprint 이동, 회전, 복사, 삭제, Undo/Redo, Workstation Blueprint와 접근 칸 지정을 제공한다.
- `OfficePlaceableDefinition`의 기본값은 `BlocksMovement=true`다. 저장 전 overlap, 경계, 필수 좌석, approach, 출구 연결을 검증한다.
- Runtime Presenter와 Save adapter가 같은 `OfficeGrid.ComputeLayoutHash()`를 사용하며 Scene Transform은 저장 정본이 아니다.
