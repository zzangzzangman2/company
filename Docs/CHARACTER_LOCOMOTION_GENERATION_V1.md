# CHARACTER LOCOMOTION GENERATION V1

이 문서는 주인공 보행을 다시 만들거나 수정할 때의 현재 재현 절차다. 게임 출력은 2D 스프라이트이며
Mixamo/3D는 동작 참고에만 사용한다. Humanoid bake와 primitive costume promotion 절차는 거부된 연구 기록이다.

## 최우선 동작 정본 — 임의 보행 생성 금지

주인공 보행의 **동작 정본은 KShopGo와 다운로드된 Mixamo 걷기**다. 외형만 2D이며, 걷는 관절 순서와
시간은 임의로 새로 만들지 않는다. 작업자는 다른 보행 문서나 과거 후보보다 먼저 아래 세 입력을 확인한다.

1. `C:/Users/godho/Downloads/com.hclab.kshopgo_1.15/com.hclab.kshopgo.apk`
2. `Docs/KSHOPGO_MOVEMENT_TEARDOWN.md`
3. `Assets/FamilyCompany/Editor/PlayerWalkHumanoidAuthoring/PlayerHumanoidWalk.fbx`
   (`C:/Users/godho/Downloads/X Bot@Unarmed Walk Forward.fbx`와 같은 파일)

KShopGo Walk의 기준은 0.8초, 30fps, 24샘플, 인플레이스 휴머노이드 보간이다. 2D 여섯 포즈는 이 한 주기를
정규화한 `0/24, 4/24, 8/24, 12/24, 16/24, 20/24` 시점에서 뽑는다. 의미는 다음과 같다.

0.8초는 동작 reference timing이다. 실제 project runtime은 speed `1.0`, stride `0.99380799`의 거리 위상을
사용하므로 정속 cycle은 `0.99380799s`이고 pose 간격은 약 `165.635ms`다. KShop 0.8초를 그대로 강제하려고
runtime speed나 stride를 따로 바꾸지 않는다.

`contact A → B 회수/체중이동 → B 전방 통과·착지 직전 → contact B → A 회수/체중이동 → A 전방 통과·착지 직전`

다음은 금지한다.

- 그림만 보고 발을 번갈아 붙이거나 텍스트 지시만으로 새 보행 타이밍을 발명하는 것
- 하체 전체 좌우반전으로 반대 반주기를 만드는 것. 골반·무릎 음영이 상체와 반대 방향으로 읽히므로 실패다.
- 신발/종아리 조각만 이동하는 것. 발목 이중화, 뒤로 꺾임, 접지면 용접을 만든다.
- ImageGen이 무릎·발목·접지 시점을 결정하게 하는 것. ImageGen은 잠근 관절 가이드 위에 외형을 정리할 때만 쓴다.
- 동일 contact를 두 프레임 연속 복제해 6장을 채우는 것. KShopGo식 연속 체중이동이 사라진다.

각 후보는 `골반→무릎→발목→앞코` 6프레임 오버레이와 Mixamo 기준 시점 대응표를 QA 산출물에 남겨야 한다.
상체와 하체 진행 방향, 지지발 고정, 스윙발 회수→교차→전방 착지, 반대 팔·다리, 낮은 통과발을 모두 사람이
GIF로 확인하기 전에는 Assets에 복사하지 않는다. 우선 범위는 **주인공 east 6프레임 하나뿐**이다.

## 시작 전 고정 문구

작업 로그에 아래 문장을 그대로 남긴다.

`FC-WALK-GUARDRAIL-V2 확인: KShopGo 0.8초/24샘플과 Mixamo FBX를 0·4·8·12·16·20 샘플로 추적, 임의 보행·하체 반전·신발 조각 이동 금지, east 6프레임 GIF 사람 판정 전 미배포.`

세부 시각 규칙은 `FAMILY_WALK_ART_GUARDRAILS.md`, 외형은 `CANON.md`, KShopGo 수치는
`KSHOPGO_MOVEMENT_TEARDOWN.md`가 소유한다.

## 현재 입력과 출력

| 구분 | 값 |
| --- | --- |
| 표현 방식 | 단일 2D SpriteRenderer |
| 동작 정본 | KShopGo Walk 0.8초/24샘플 + Mixamo `PlayerHumanoidWalk.fbx` |
| 외형 참고 A | `ArtSources/PlayerWalk2DGenerated/player_walk8dir6_a_chroma_v3.png` |
| 외형 참고 B | `ArtSources/PlayerWalk2DGenerated/player_walk8dir6_b_chroma_v3.png` |
| 추적 가능한 motion 계약 | `ArtSources/PlayerEastMixamoTraceV2/` (PASS, 게임 아트 아님) |
| 재생성 출력 | `Artifacts/PlayerEastMixamoTraceCandidate/` (gitignore, 필요 시 재생성) |
| 기존 Unity 후보 | `Assets/Resources/FamilyCompany/Player2DWalkV2/` (v10, 거부됨) |
| 런타임 카탈로그 | `Player2DWalkCatalogV2` |
| 명시 실행 플래그 | `-familyCompanyPlayer2DWalkV2` |

카탈로그의 `V2`는 런타임 계약 버전이다. 기존 v3 이미지와 v4~v13 조립물은 외형·실패 회귀 참고이고 새
보행의 시간 정본이 아니다. 다음 후보는 다른 방향을 건드리지 않고 east 6장만 격리 생성한다.

## 2026-08-20 체크포인트

- `PlayerWalkMotionReferenceExporter`의 Unity narrow export는
  `PLAYER_WALK_MOTION_REFERENCE: PASS`를 기록했다.
- Mixamo source clip은 `1.3666668s`, 검출된 left-contact phase zero는 `0.2961111s`, east yaw는 `+90°`다.
- project stride를 결합한 root advance는
  `(0.99380799 / 6) * 180 / 1.55 = 19.234993px/pose`다.
- `ArtSources/PlayerEastMixamoTraceV2/phase-contract.md`의 target heel/toe 최대 world drift는
  `0.765007px`로 `<=1px` 계약을 통과했다. 이는 **관절 target PASS**이지 완성 raster/GIF PASS가 아니다.
- V13 whole-sheet ImageGen, 개별 P1/P2 ImageGen, LockedArtV2 raster warp는 모두 거부됐다. P2/P5 하체가
  교차·꺾이거나 owner/contact를 어겼으며 production PNG로 사용하지 않는다.
- 기존 donor를 다시 자르지 않는다. V3 P0~P5의 상체만 보존하고, lower 6장은 locked joint 위에 각 phase별
  완전한 `pelvis→hip→knee→ankle→heel/toe` 체인으로 새로 저작한다.
- 집 PC 상세 재개점은 `HOME_PC_WALK_CHECKPOINT_2026-08-20.md`다.

## 시각 불변식

1. 빨간 뉴스보이 캡, 짙은 갈색 머리와 눈, 흰 후드 윈드브레이커, 남색·노랑 계열 줄무늬 셔츠,
   짙은 남색 바지와 운동화가 8방향에서 같은 인물로 읽혀야 한다.
2. 방향별 프레임 의미는 contact A → support A → low pass B → contact B → support B → low pass A다.
3. 팔은 다리와 반대로 흔들리고, 손은 몸통에서 분리돼 읽히되 과장하지 않는다.
4. 6프레임의 모자 꼭대기와 골반 높이는 안정적이어야 한다. 뛰기·행진·널뛰는 수직 bob은 실패다.
5. 0/3 접지 보폭은 짧고 2/5 통과발은 바닥 가까이 지나야 한다. 무릎이 옆으로 꺾이거나 다리가 X자로
   교차하면 실패다.
6. 프레임은 256×256, hard alpha, bottom-center pivot, 180 PPU, Point, mipmap 없음, 무압축이다.
7. 크로마 조각, 가짜 체크무늬, 반투명 잔상, 끊긴 외곽선, 프레임별 scale pop은 0이어야 한다.
8. 정지·출발·pivot 전용의 다른 인물 그림을 섞지 않는다. 이동 중 실제 변위에 맞는 8방향 행을 즉시 고른다.

## 생성과 변환

1. tracked `ArtSources/PlayerEastMixamoTraceV2/target-joints.json`과 `phase-contract.md`를 motion 정본으로
   사용한다. raw joint export는 공개 Git에 넣지 않고 FBX가 바뀌었거나 수치를 재감사할 때만 ignored
   `Artifacts/PlayerEastMixamoTraceCandidate/`에 Unity exporter로 다시 만든다.
2. `Tools/build_player_east_mixamo_trace_v1.py`로 0/4/8/12/16/20 가이드를 재생성하고
   `PLAYER_EAST_MIXAMO_TRACE: PASS`와 최대 drift `<=1px`를 확인한다.
3. 승인된 빨간 캡 주인공의 phase별 상체를 보존하고 lower 6장을 가이드 위에 완전한 두 leg chain으로 새로
   그린다. 원본 팔 자세·체형·바지·운동화 정체성을 유지한다.
   ImageGen을 쓰더라도 관절 좌표와 접지 이벤트는 바꿀 수 없다.
4. `Artifacts/PlayerEastMixamoTraceCandidate/`에서 오버레이, 검정/녹색 GIF, 발 확대, 위상 대응표를 검증한다.
5. 사용자 east GIF 승인 뒤에만 6 PNG를 기존 `.meta`를 보존해 Unity 후보로 복사한다.

`Tools/Build-Player2DWalkV2Candidate.ps1`과 v4~v13 조립 스크립트는 기존 실패 재현용이다. 다음 후보의
보행 저작 경로로 사용하지 않는다.

## 실행 검증

Unity Editor가 닫힌 상태에서 저장소 루트에서 실행한다.

tracked target과 guide만 확인하는 데는 Unity가 필요 없다. raw trace부터 동일하게 재생성하려면 아래 exporter와
Python 검증을 순서대로 실행한다.

```powershell
$projectRoot = (Get-Location).Path
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe'
& $unityEditor -batchmode -nographics -quit `
  -projectPath $projectRoot `
  -executeMethod FamilyCompany.Editor.PlayerWalkMotionReferenceExporter.RunFromCommandLine `
  -logFile (Join-Path $projectRoot 'Artifacts\PlayerEastMixamoTraceCandidate\unity-export.log')
if ($LASTEXITCODE -ne 0) { throw "Mixamo trace export failed: $LASTEXITCODE" }

python .\Tools\build_player_east_mixamo_trace_v1.py `
  --json .\Artifacts\PlayerEastMixamoTraceCandidate\mixamo-east-6pose-joints.json `
  --source-dir .\ArtSources\PlayerEastMixamoTraceV2\SourceV3Frames `
  --output-dir .\Artifacts\PlayerEastMixamoTraceCandidate
if ($LASTEXITCODE -ne 0) { throw "Trace build failed: $LASTEXITCODE" }
```

예상 결과:

```text
PLAYER_EAST_MIXAMO_TRACE: PASS | poses=6 rootAdvance=19.234993px maxContactDrift=0.765007px
```

`Invoke-PlayerHighMotionWalkPipeline.ps1`은 승인된 8방향×6포즈 PNG 48장이 runtime root에 모두 존재한 뒤에만
사용하는 후속 visual QA다. 현재 fresh clone에는 거부 후보를 의도적으로 넣지 않았으므로 실행 대상이 아니며,
스크립트는 Unity build 전에 `PLAYER_2D_WALK_V2_NOT_READY`로 종료한다. east 승인 뒤 완성 48장이 생기면 normal
Windows x64/D3D11에서 48 closeup과 8 overview를 캡처하는 용도로만 사용한다.

과거 source v3 결과는 방향·프레임 수·주기·cadence·렌더가 통과했지만 아래 두 필드는 의도적으로 미측정이다.

- `screenSupportFootDriftSourcePxMinMax=NOT_MEASURED_VISUAL_CANDIDATE`
- `alternatingContactStepWorldMinMax=NOT_MEASURED_VISUAL_CANDIDATE`

따라서 성공 문구도 `PLAYER_2D_WALK_V2_VISUAL_PIPELINE: PASS_NON_SHIPPING`이다. 이 상태를 shipping PASS로
고쳐 쓰거나 support-foot 검사를 건너뛴 채 기본 모드로 승격하지 않는다.

## 이동 결합값

- speed: 1.0 world unit/s
- acceleration: 8.0
- walk cycle distance: 0.99380799 world unit = 등각 타일 중심 한 칸
- stride: 0.99380799 world unit/cycle
- cadence: 약 2.0125 steps/s
- 방향: 실제 frame 변위를 즉시 8방향으로 양자화
- 자유 보행: stationary pivot과 ShortShuffle 없음

KShopGo의 걷기 클립도 인플레이스이며 logical root 이동은 NavMeshAgent가 소유했다. 우리도 PNG가 이동량을
소유하지 않고 `OfficeRuntimeAgent`가 pathfinding, collision, root 위치와 실제 이동 위상을 소유한다.

## Mixamo의 현재 역할

보유한 `X Bot`과 `Unarmed Walk Forward` FBX는 Editor-only 참고 자료다. 팔꿈치·무릎 순서, 반대 팔·다리,
보행 cycle과 관절 순서를 관찰하는 데 쓸 수 있지만 주인공 외형을 만들거나 production PNG를 자동 promotion하지 않는다.
추가 Mixamo 클립은 걷기 후보 승인 뒤 idle/sitting/typing 동작을 연구할 때만 받는다.

`Tools/Invoke-PlayerWalkHumanoidPipeline.ps1`은 폐기된 3D 경로의 tombstone이다. 항상 Unity 실행·Artifacts 생성·
production 복사 전에 실패하며 우회 스위치는 없다. `PlayerWalkHumanoidPromotion` 직접 호출도 같은 이유로 차단한다.
