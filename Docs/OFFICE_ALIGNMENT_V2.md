# Office Tycoon Alignment V2

최종 갱신: 2026-08-11

## 목적

책상·의자·캐릭터를 같은 임시 숫자로 배치하고 다시 그 숫자로 검사하던 순환 검증을 제거한다. 실제 타일 footprint, 승인된 가구 픽셀 좌표, workstation socket, 실제 clip/frame 신체 anchor를 서로 독립된 입력으로 사용한다.

## 런타임 구조

1. `OfficeGridLayouts.CreateStarterOfficeV1()`이 실제 초기 13×13 사무실을 만든다. `CreateMigrationPreview()`는 파티션을 포함한 QA fixture로만 남는다.
2. `OfficeWorkstationSlot`이 desk/chair/seat, seat/approach cell, facing, 반 셀 `OperatorAnchor`를 묶는다.
3. `OfficeGridFurniturePresenter`는 desk 의미 root를 footprint 중심에 놓고 desk operator seat socket에 chair seat anchor를 맞춘다. 의미 root는 이동하거나 확대하지 않는다.
4. `OfficeGridSeatedWorker`는 현재 member/direction/clip/frame profile의 실제 pelvis를 desk operator seat socket에 맞춘다. 실제 hand와 desk work socket은 별도로 검사한다.
5. NorthWest chair는 base만 캐릭터 뒤에 둔다. desk의 제한된 front overlay만 캐릭터 하체 앞에 둔다.

## 저장

- 게임 save version은 기존 v6을 유지한다.
- `officeGrid.schemaVersion`은 3이다.
- schema 3은 좌석마다 `operatorX2/operatorY2`를 저장한다.
- schema 2는 저장값이 없으므로 좌석과 연결된 work-surface의 가장 가까운 셀 사이 반 칸을 계산해 복원한다.
- schema 1은 기존 workstation 없는 좌석 이관을 유지한다.
- v1~v5 게임 저장은 Migration Preview가 아니라 Starter Office로 이관한다.

## calibration asset 후보

- `Assets/FamilyCompany/Presentation.Unity/OfficeGrid/Authoring/OfficeFurnitureVisualCatalog.asset`
  - calibration version 2
  - 12종 정의
  - 네 점 ground footprint
  - semantic footprint width/height
  - desk operator seat/work socket
  - chair seat anchor
- `Assets/FamilyCompany/Presentation.Unity/OfficeGrid/Authoring/OfficeCharacterSeatPoseCatalog.asset`
  - calibration version 2
  - 4명 × 14프레임 = 56 profile
  - key: member/direction/clip/frame
  - pelvis/hand 후보와 uniform scale. 각 프레임의 실제 신체 지점은 최종 수동 승인이 남아 있다.

`OfficeFurnitureAssetBuilder`는 runtime PNG를 반복 생성해 해시 결정론을 검사하지만 이미 version 2인 catalog는 덮어쓰지 않는다.

## 수동 교정

Unity 메뉴 `Family Company/Office/Office Tycoon Alignment Calibration`을 연다.

- Furniture: 100/200/400% 픽셀 보기, four-corner footprint, ground/sort/seat/operator socket 드래그, front overlay 확인
- Character: member/facing/clip/frame, 이전·다음 onion skin, pelvis/hand 드래그, frame drift
- Workstation composite: floor/desk/chair/character/front overlay 합성과 pelvis-seat, chair-desk, hand-work, footprint residual

Workstation 합성을 사람이 확인하고 승인하기 전에는 Furniture·Character 값을 저장할 수 없다.

## 독립 QA

Unity execute method:

```text
FamilyCompany.Editor.OfficeGridQa.OfficeTycoonAlignmentV2Qa.StartBatch
```

검사는 Migration Preview 45초와 Starter Office 60초를 순서대로 실행한다. Preview의 플레이어는 기존 회귀 동선을 보존하기 위해 30초 뒤 착석 이동을 시작한다. Starter에서는 45초에 네 가족에게 stand/reseat를 요청하고 60초에 다시 검사한다.

| 항목 | 허용 오차 |
|---|---:|
| 1×1·2×1 footprint 각 점 | <= 2px |
| chair seat ↔ desk operator seat | <= 2px |
| pelvis ↔ desk operator seat | <= 2px |
| hand ↔ desk work | <= 4px |
| pelvis→hand / desk seat→work 벡터 방향 차이 | <= 2° |
| pelvis→hand / desk seat→work 벡터 길이 차이 | <= 4% |
| frame correction jump | <= 1px |
| Work pelvis/hand profile drift | <= 1px / 2px |
| semantic root ↔ seat cell | <= 0.001 world |
| 가구 의미/시각 Transform 60초 변화 | 정확히 0 |
| blocking footprint 침범·좌석 중복·지원하지 않는 facing fallback | 0 |
| desk/chair overlay의 얼굴 가림 | 0 opaque sample |

산출물은 `Artifacts/OfficeTycoonAlignmentV2/`에 저장한다.

## 현재 검증 상태

- 기존 화면에서 hand-to-work 검사를 실제 실패 조건으로 바꾼 baseline은 father 9.059px에서 실패했다. 로그는 `unity-v2-baseline-fail.log`다.
- Simulation/Save/Presentation/Editor 전체 Roslyn 컴파일은 오류 0이다.
- 가구·pose catalog v2 구조와 오프라인 4인 workstation 합성은 확인했다. 의자 좌판 중심을 오른쪽 18.55px·위쪽 10.82px로 교정하고 넓은 chair front overlay를 제거했다. 다만 12종의 four-corner 값은 Unity Calibration Window에서 사람의 최종 승인을 아직 받지 않은 candidate다.
- 2026-08-11 오후 Unity 라이선스가 다시 인식돼 실제 그래픽 QA가 scene/PlayMode까지 진입했다. `OfficeGridValidation`, 타일 빌드, 가구 V2 결정론 빌드와 calibration asset 비파괴 검사는 PASS했다.
- Preview 45초에서 18개 가구의 persisted footprint와 semantic footprint 비교는 모두 0.000px였다. 이 값만으로 ImageGen 물체의 실제 다리·바퀴 접점이 맞다고 승인하지 않으며 debug 합성 캡처 육안 확인이 별도로 필요하다.
- 실제 1920×1080 PlayMode의 hand↔work 오차는 player `8.032px`, older_sister `11.906px`, father `12.336px`, mother `13.322px`다. 방향/길이 차이는 각각 player `0.349° / 14.842%`, older_sister `2.754° / 21.601%`, father `9.414° / 17.247%`, mother `13.676° / 9.757%`여서 손 4px, 방향 2°, 길이 4% 기준을 통과하지 않는다.
- 가족별 팔 방향이 서로 달라 translation이나 좌석별 offset으로 동시에 해결할 수 없다. NorthWest work Sprite의 팔·손 자세를 공용 작업 벡터에 맞춰 다시 제작하고 실제 손 좌표를 재측정해야 한다.
- 최종 45+60초 Unity 그래픽 QA는 끝까지 실행됐다. 가구 의미/시각 Transform 60초 변화 0, blocking 침범 0, 좌석 claim 4개 고유, stand/reseat 완료, 저장 왕복 해시 일치를 통과했고 6장 캡처를 생성했다. 수치 보고서는 `Artifacts/OfficeTycoonAlignmentV2/alignment-v2-report.txt`다.
- 최종 결과는 손 pose 때문에 의도적으로 FAIL이며 6장도 시각 승인본이 아니라 실패 진단본이다. work Sprite 수정 후 위 execute method의 모든 표와 얼굴/하체 alpha mask 검사가 PASS한 뒤에만 `Tools/BuildPrototype.ps1`, `Tools/ValidatePrototype.ps1`을 실행한다.

수치 통과를 위해 실제 신체 밖에 가짜 hand/pelvis anchor를 두지 않는다. 실제 손 pose가 4px을 넘으면 해당 프레임 아트를 재교정한 뒤 다시 승인한다.
