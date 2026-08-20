# MIXAMO WALK HANDOFF — 걷기를 받아서 굽는 경로, 이어받는 문서

> **2026-08-20 최종 방향:** 게임 주인공은 2D 스프라이트다. 이 문서의 휴머노이드 bake/promotion 절차는
> 거부된 연구 기록이고 production에서 실행하지 않는다. Mixamo는 0.8초 타이밍과 관절 순서 참고에만 쓴다.
> 현재 실행 정본은 `Docs/CHARACTER_LOCOMOTION_GENERATION_V1.md`다.

> **2026-08-20 정정:** 이 문서 아래의 `scale=0.45522`, 380px Humanoid 후보,
> `PlayerBakedWalkHumanoidV1/V2` bake/promotion 설명은 X Bot 연구 기록이다. 현재 production 실행 정본은
> `Docs/CHARACTER_LOCOMOTION_GENERATION_V1.md`이며 X Bot 표면이나 Humanoid PNG를 승격하지 않는다.
> 현재 runtime 값은 speed `1.0`, acceleration `8.0`, stride/cycle distance `0.99380799`다. KShopGo의
> 0.8초/24샘플은 2D pose timing 참고이고 KShopGo world의 speed `1.5`/stride `1.2`는 직접 복사하지 않는다.
> 현행 파생 trace 체크포인트는 `ArtSources/PlayerEastMixamoTraceV2/target-joints.json`,
> `phase-contract.md`, skeleton guide와
> `Docs/HOME_PC_WALK_CHECKPOINT_2026-08-20.md`가 소유한다.

이 문서만 읽고 이어받을 수 있게 쓴다. 배경은 `Docs/WALK_RIG_SOURCE_DECISION.md`,
측정 근거는 `Docs/KSHOPGO_MOVEMENT_TEARDOWN.md`에 있다.

## 동작 참고 소스는 Mixamo다 — 추가 다운로드는 걷기 승인 뒤

<https://www.mixamo.com/> (Adobe, 무료, 계정 필요). **사용자 계정으로 로그인되어 있다.**
현재 Walk에 필요한 X Bot과 `Unarmed Walk Forward`는 이미 프로젝트에 있다. 추가 클립은 east 걷기
승인 뒤 idle/sitting/typing 연구를 시작할 때만 받는다. Mixamo는 관절 동작 정본이고 최종 게임 표면은 2D다.

다음에 받을 후보(KShopGo는 클립 7개로 전부 처리했다):

| 클립 | 용도 | 우리 현재 대응물 |
| --- | --- | --- |
| `Idle` / `Standing Idle` | 서 있기 | 정지 프레임 |
| `Running` | 급할 때 이동 | 없음 |
| `Sitting` | 좌석 착석 | 좌석 원화 448장 |
| `Typing` | 업무 중 | 업무 원화 640장 |

추가 클립이 통과하더라도 기존 2D 좌석·업무 원화를 자동 대체하지 않는다. 별도 2D 출력·사용자 승인·runtime
검증이 있어야 한다.

## 참고한 게임과 폴더

| 대상 | 경로 | 참고한 것 |
| --- | --- | --- |
| KShopGo APK | `C:\Users\godho\Downloads\com.hclab.kshopgo_1.15\com.hclab.kshopgo.apk` | 이동 설정값. `Docs/KSHOPGO_MOVEMENT_TEARDOWN.md` |
| simul (Flutter) | `C:\Users\godho\Documents\Codex\simul` | 호가창 체결 구조. `Docs/ORDER_BOOK_SWEEP_V1.md` |

둘 다 읽기 전용이다. 수정하지 않고 저장소에도 넣지 않는다.

## 역사 기록의 한 줄

당시에는 걷기 사이클을 직접 만들지 않고 Mixamo에서 리그와 걷기 클립을 받아
`PlayerWalkHumanoidBaker`에 넣고, 모델을 45도씩 돌려 **8방향 × 8포즈 = 64장 PNG**를 굽는다.
이 경로는 화면 검토 뒤 거부됐으며 런타임과 카탈로그를 더 이상 변경하지 않는다.

## 지금까지 된 것

| 항목 | 상태 |
| --- | --- |
| `Assets/FamilyCompany/Editor/PlayerWalkHumanoidBaker.cs` | 작성 완료, **컴파일 PASS** |
| `Assets/FamilyCompany/Editor/PlayerWalkHumanoidModelImporter.cs` | 작성 완료, **컴파일 PASS** |
| `ArtSources/PlayerWalkHumanoid/humanoid-contract.json` | 작성 완료 |
| `Assets/FamilyCompany/Editor/PlayerWalkHumanoidAuthoring/PlayerHumanoidBase.fbx` | **받아서 넣음** (Mixamo X Bot, T-Pose, FBX for Unity, 1,750,032바이트, SHA-256 `BA1FBC01DF013A102363E88E698719176A4366CE6B3C01AB500319DF55C37BA1`) |
| 걷기 클립 FBX | **받아서 넣음** (`X Bot@Unarmed Walk Forward.fbx` -> `PlayerHumanoidWalk.fbx`, 417,392바이트) |
| 실제 베이크 실행 | **실행됨.** 렌더까지 통과, 검증에서 막힘 (아래 "실행 결과") |

`editor-validation` 결과: `FAST QA: PASS profile=editor-validation compileSeconds=104.325 head=5f387bb9`

## 실행 결과 — 2026-08-20

계획 단계는 전부 의도대로 동작했다.

```text
PLAYER_WALK_HUMANOID_BAKER: plan | cycleSeconds=1.3667 clipCycleDistance=1.40847
requiredDistance=0.64117 scale=0.45522 leftPlantTime=0.2680 pitch=43.6981
```

- 클립 길이 1.3667초, 한 사이클 루트 이동 **1.40847 유닛**. 실제 사람 보폭과 맞다.
- 필요 거리 0.64117에서 **스케일 0.45522**를 스스로 계산했다.
- **왼발 접지를 0.2680초에서 찾았다.** 위상 정렬이 동작한다.
- 8방향 × 8포즈 렌더와 PNG 기록까지 통과했다.

### 막힌 곳 1 — 투영 몸높이 검증 (확정)

```text
InvalidOperationException: Player baked walk visible-height delta 4.124% exceeds 1.000%.
```

`PlayerBakedWalkV2Validation.ValidateVisibleHeight`는 pelvis에서 정수리까지의 **투영 높이**가
8포즈에서 1% 이내여야 한다고 요구한다. 그 코드의 주석은 이렇게 말한다.

> Pelvis-to-crown is the invariant authored height

**페이퍼돌에서는 참이다.** 몸통이 강체 스프라이트라 늘어나지 않는다. **휴머노이드에서는 거짓이다.**
척추가 굽고 상체가 앞뒤로 기울며, 피치가 걸린 카메라는 그 전후 이동을 화면 세로로 바꾼다.
측정값 4.124%는 결함이 아니라 걷기 클립의 성질이다.

즉 이 검증은 2D 전용 불변량이다. 휴머노이드 경로의 등가 불변량은 다르다 — **3D에서 hips와 head
사이의 뼈 거리**는 절대 변하지 않으므로, 그것을 베이크 시점에 직접 단정하는 편이 더 강한 검증이다.

이건 Codex가 의도를 갖고 넣은 공유 검증이므로 **혼자 완화하지 않았다.** 결정이 필요하다.

- (A) 휴머노이드 경로는 자체 검증 세트를 갖고 `ValidateReceiptAndPngs`를 호출하지 않는다.
      투영 독립적인 항목(하드 알파, 연결 성분, 캔버스 클리핑, 포즈/지지발 순서)은 그대로 쓰고,
      투영 몸높이만 3D 뼈 거리 불변량으로 대체한다.
- (B) `MaximumVisibleHeightDeltaPercent`를 휴머노이드용으로 분기한다. 공유 파일 수정이 필요하다.

(A)를 권한다. Codex 파일을 건드리지 않고, 더 강한 불변량을 쓴다.

### 막힌 곳 2 — 투영 등방성 (미확인, 더 근본적)

`ValidateReceiptFootLock`은 방향 벡터를 **정규화된 2D 단위벡터**로 쓰고
(`Vector2.down`, `(-1,-1).normalized`, ...), 포즈당 전진량을 **모든 방향에 같은 스칼라**로 둔다.

```csharp
float rootStepPx = receipt.strideWorld / PoseCount * receipt.pixelsPerUnit / receipt.visualScale;
```

즉 **화면 투영이 등방적이라고 가정한다.** 그런데 피치 43.698도 카메라는 월드 Z를
`sin(43.698) = 0.6909`로 압축하고 월드 X는 1.0으로 둔다. 그래서 south/north 보폭은
east/west 보폭의 약 69%로 투영된다. 한 스칼라로는 8방향을 동시에 만족시킬 수 없다.

그리고 정본 피치 자체가 불확실하다. 두 근거가 어긋난다.

| 근거 | 값 |
| --- | --- |
| `IsometricCameraFollow` officeOffset `(0, 13.5, -13.5)`, lookHeight `0.6` | `atan(12.9/13.5) = 43.698도` |
| 타일 320x160 (`OfficeLocomotionGaitRules` 주석의 보폭 유도) | `atan(160/320) = 26.565도` |

`IsometricCameraFollow`가 구 프로토타입 전용이고 사무실 타일은 2D 스프라이트 격자라면 26.565도가
정본일 수 있다. **확인 전에 굽는 것은 의미가 없다.** `OfficeGridCharacterMover`와
`OfficeGridAlignmentMetrics`가 스프라이트를 실제로 어떤 변환으로 배치하는지 읽어야 한다.

막힌 곳 1을 풀면 곧바로 이 검증에 닿으므로, 순서는 **2를 먼저 확정**하는 것이 맞다.

### 사고 기록 — Codex 출력을 덮었다

첫 실행이 `Assets/Resources/FamilyCompany/PlayerBakedWalkV2/Frames/south/`에 써서 Codex의
페이퍼돌 8장과 `source-receipt-south.json`을 덮었다. 그 파일들은 미커밋이라 git으로 복구할 수 없었다.

복구했다. Codex의 베이커는 SHA 검증된 결정적 파이프라인이라
`PlayerWalkRigV2Baker.RunFromCommandLine`을 다시 돌려 재생성했다
(`PLAYER_WALK_RIG_V2_BAKER: PASS | direction=south poses=8`, 영수증 SHA
`9AB19E2949DFCFF4700548367DA2F549CFA4DB95E6854B1F9D4F038A657B15F7` 원본과 일치).

재발 방지로 휴머노이드 베이커는 이제 자기 폴더에만 쓴다.

```text
Assets/Resources/FamilyCompany/PlayerBakedWalkHumanoidV1/Frames/<direction>/
Assets/Resources/FamilyCompany/PlayerBakedWalkHumanoidV1/source-receipt-<direction>.json
```

계약의 `outputRoot`로 바꿀 수 있다. **두 경로는 이제 절대 겹치지 않는다.**

## 받은 클립과 그때 쓴 설정 — 기록용, 다시 받을 필요는 없다

### 1. Mixamo에서 걷기 클립 다운로드

<https://www.mixamo.com/#/?page=1&query=unarmed+walk&type=Motion%2CMotionPack>

`Unarmed Walk Forward` (Description: Walking Forward)를 고른다. 무기·비틀거림·좀비가 섞이지 않은
중립 루프이고, 사무실 직원 걷기에 그대로 쓸 수 있다.

| 다운로드 옵션 | 값 | 이유 |
| --- | --- | --- |
| Format | **FBX For Unity(.Fbx)** | Unity 축·스케일 변환 적용 |
| Skin | **With Skin** | 렌더해서 PNG로 구우려면 메시가 필요하다 |
| Frames per Second | **30** | KShopGo도 30이다 |
| Keyframe Reduction | **none** | 베이커가 루트 이동거리로 재보간한다. 키 축약은 타이밍을 망친다 |
| **In Place** | **체크 해제** | 루트 이동이 있어야 보폭을 측정한다 |

In Place를 켜면 베이커가 이렇게 던진다.

```text
Walk clip has no root travel. Export it with root motion, or set
cycleDistanceOverride in the contract so the stride can be reconstructed.
```

그 경우 `humanoid-contract.json`의 `cycleDistanceOverride`에 한 사이클 이동거리(미터)를 직접 넣는다.

### 2. 파일 배치

받은 파일을 이 이름으로 넣는다. 계약이 이 경로를 가리킨다.

```text
Assets/FamilyCompany/Editor/PlayerWalkHumanoidAuthoring/PlayerHumanoidWalk.fbx
```

`PlayerWalkHumanoidModelImporter`가 이 폴더의 모델을 자동으로 Humanoid로 임포트하고,
Avatar를 `PlayerHumanoidBase.fbx`에서 복사하며, 텍스처를 Point 필터로 바꾼다. 손으로 만질 것이 없다.
확인된 결과: 베이스 FBX의 meta가 `animationType: 3`(Humanoid), `avatarSetup: 1`이 되었다.

**두 FBX의 역할이 다르다.** 받은 애니메이션 파일에는 메시가 없다(`Geometry: 0`, `Deformer: 0`).
베이스에만 있다(`Geometry: 7`, `Deformer: 263`, `Material: 11`). 그래서 계약의 `rigPrefabPath`는
**베이스**를, `walkClipPath`는 **워크**를 가리켜야 한다. 둘을 같은 파일로 두면
`SkinnedMeshRenderer`가 없다며 실패한다. 메시 1개에 클립 N개 — KShopGo와 같은 구조다.

### 3. 베이크 실행 — HISTORICAL / DO NOT RUN FOR PRODUCTION

아래 명령은 거부된 Humanoid 연구를 재현하는 기록이다. 현재 walk art 제작이나 promotion에 실행하지 않는다.

에디터 메뉴 `Family Company / Art / Bake Player Walk From Humanoid Rig`,
또는 배치모드로:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\godho\Documents\Codex\family_company_unity" -executeMethod FamilyCompany.Editor.PlayerWalkHumanoidBaker.RunFromCommandLine -logFile -
```

`PLAYER_WALK_HUMANOID_BAKER: PASS | directions=8 poses=64` 가 나오면 성공이다.
실패하면 그 전에 찍히는 `plan` 줄을 먼저 읽는다.

```text
PLAYER_WALK_HUMANOID_BAKER: plan | cycleSeconds=... clipCycleDistance=...
requiredDistance=0.64117 scale=... leftPlantTime=... pitch=43.6975
```

### 4. 카탈로그 승격 — HISTORICAL / 현재 금지

과거에는 영수증 뒤 `PlayerBakedWalkV2CatalogBuilder`로 카탈로그를 만들 계획이었다. 실제 화면에서 3D
primitive 인상과 바운스를 거부했으므로 지금은 이 승격을 실행하지 않는다. 런타임 기본은 `Legacy48`이다.

```text
-familyCompanyPlayerBakedWalkV2
```

## 베이커가 지키는 제약 — 왜 이렇게 짰는지

`PlayerBakedWalkV2Validation`이 우리 출력을 검사한다. 통과해야 하는 것 중 까다로운 셋이다.

1. **`supportLeg = pose < 4 ? "left" : "right"`**
   받은 클립은 임의 위상에서 시작한다. 그래서 `FindLeftPlantStart`가 720개 샘플에서 두 발 높이를
   비교해 왼발 접지 구간의 시작을 찾고, 거기서부터 8포즈를 뽑는다.

2. **지지발 고정 오차 ≤ 1.0px (투영) / 1.5px (2D)**
   검증이 `heading * (strideWorld/8 * ppu/visualScale) * pose`를 되더해서 발이 제자리인지 본다.
   즉 루트가 포즈당 정확히 `strideWorld/8`씩 전진해야 한다. 실제 클립의 루트는 시간에 대해
   균등하지 않으므로, **시간 균등이 아니라 루트 이동거리 균등으로 샘플링한다**
   (`ResolveTimeAtTravel`).

3. **하드 알파만 (0 또는 255), 연결 성분 1개, pelvis→정수리 높이 변화 ≤ 1%**
   `ForceHardAlpha`가 반투명 픽셀을 커버리지 과반으로 정리한다. 높이 1%는 walk 클립의 척추
   기울기 때문에 초과할 수 있다. 초과하면 `cycleSeconds`를 줄여 상체 흔들림이 작은 구간만 쓰거나,
   클립을 다른 것으로 바꾼다.

최초 probe는 한 사이클이 `strideWorld / visualScale`을 덮도록 사람 크기까지 root travel에 결합했지만
Humanoid 연구 경로는 이를 폐기했지만 그 경로 자체도 production에서 거부됐다. 현행 2D trace는 runtime
stride `0.99380799`, 180 PPU, visual scale 1.55에서 `19.234993px/pose`를 사용하고 heel/toe roll을
나눠 support-foot drift를 검증한다.

## 카메라 각도 — 추측이 아니다

`IsometricCameraFollow`의 사무실 프레이밍에서 그대로 유도했다.

```csharp
officeOffset     = new Vector3(0f, 13.5f, -13.5f);
officeLookHeight = 0.6f;
```

yaw가 0이므로 **축 정렬 시야**다. 대각선 아이소메트릭이 아니다. 피치는
`atan((13.5 - 0.6) / 13.5) = 43.6975도`다. 캐릭터를 이 각도로 렌더해야 바닥과 단축이 맞는다.

**다만 이 값이 정본인지는 아직 확정되지 않았다.** 타일 320x160에서 유도되는 26.565도와 어긋난다.
위 "막힌 곳 2 — 투영 등방성"을 먼저 읽는다. 그 절이 이 절보다 우선한다.

방향 0(south)은 `-Z`이고 카메라 정면이라 앞모습이다. `DirectionVector(d) = (-sin(45d), -cos(45d))`
이므로 `+Z`를 보는 휴머노이드의 yaw는 `180 - 45d`다 (`ModelYawDegrees`).

**주의:** 누가 `IsometricCameraFollow`의 저 두 값을 바꾸면 `PlayerWalkHumanoidBaker`의
`OfficeCameraHeight` / `OfficeCameraDepth` / `OfficeCameraLookHeight` 상수도 같이 바꿔야 한다.
private 필드라 코드로 읽을 수 없어 상수로 복제해 두었다.

## Codex 작업과 겹치지 않는 부분

Codex는 2D 페이퍼돌 경로(`PlayerWalkRigV2Baker`, `LimbSolver2D` 요구)로 `south` 1방향을 이미 구웠다.
`Assets/Resources/FamilyCompany/PlayerBakedWalkV2/Frames/south/`에 8장이 있다.

이 문서의 경로는 **그 파일들을 건드리지 않는다.** 새 파일만 추가했다.

| 건드리지 않음 (Codex) | 추가함 (이 작업) |
| --- | --- |
| `PlayerWalkRigV2Baker.cs` | `PlayerWalkHumanoidBaker.cs` |
| `PlayerWalkRigV2Contracts.cs` | 계약 클래스는 베이커 파일 안에 둠 |
| `ArtSources/PlayerWalkRigV2/` | `ArtSources/PlayerWalkHumanoid/` |
| `PlayerBakedWalkCatalogV2.cs` | 변경 없음. 출력 형식을 맞출 뿐 |
| `PlayerBakedWalkV2Validation.cs` | 변경 없음. 우리 출력을 검사하게 재사용 |

역사적으로 출력 충돌 위험이 있었으나 현재 Humanoid bake/promotion은 금지되어 있다. production PNG 폴더에
이 경로를 실행하지 않는다.

## 당시 남았던 것 (현재는 중단)

- **64장 재베이크·승격은 하지 않는다.** 최초 south 후보의 clipping은 연구 기록이며 2D v3 경로가 대체한다.
- 방향별 실루엣 확인. 8방향 중 north(뒷모습)와 대각선이 픽셀 단위로 읽히는지는 눈으로 봐야 한다.
- X Bot Renderer를 숨기고 Mixamo 뼈에 `canonical-protagonist-v1` 닫힌 볼륨을 붙이는 구현은 끝났다.
  빨간 뉴스보이 캡·흰 후드·줄무늬 셔츠·남색 바지·운동화가 실제 8방향 캡처에서 읽히는지 확인해야 한다.
- 나머지 클립(Idle / Run / Sitting / Typing). KShopGo는 7개로 전부 처리했다.

## 그림과 별개인 이동 문제 — 2026-08-20 수정 완료

프레임이 완벽해도 이동 규칙이 딱딱하면 딱딱하다.
`Assets/FamilyCompany/Simulation/Navigation/OfficeNavigationMotionRules.cs`의 값들이다.

| 항목 | KShopGo | 우리 |
| --- | --- | --- |
| 회전 | `m_AngularSpeed = 1200` (사실상 즉시) | 이동 중 정지 gate 없음, 실제 변위 행 즉시 적용 |
| 이동 | speed 1.5 / acceleration 8.0 | speed `1.0` / acceleration `8.0` |
| Walk cadence | 0.8s, 1.2 unit/cycle | stride `0.99380799`, 약 `0.9938s/cycle` (`2.0125 steps/s`) |
| 방향 확정 지연 | 없음 | 이동 frame은 stabilization/hysteresis 0 |
| 회피 | `m_ObstacleAvoidanceType = 0` (끔) | 반경·예약 |
| 도착 실패 | `WaitArriveWithTimeout` | 대응물 없음 |

`ShortShuffleStrideFraction=0`으로 짧은 이동도 전체 gait를 진행한다. 제자리 `PivotSeconds=0.06`은
막힘·좌석·업무 상호작용의 최종 facing 정렬에만 남고 자유 보행을 멈추지 않는다.
KShopGo의 1.5/1.2는 다른 world scale의 참고값이며 우리 runtime에 직접 대입하지 않는다.

이 파일은 순수 C#이라 `simulation-pure`로 검증되고 **플레이어 창을 띄우지 않는다.**
`OfficeMovementFacingNavigationValidation`, `OfficeSharedLocomotionStrictValidation`,
`OfficeMovementFacingNavigationValidation`, `OfficeSharedLocomotionStrictValidation`,
`OfficeNavigationRegressionSuite`, actual Player reversal QA를 새 계약으로 갱신했다. `simulation-pure`와
Simulation/Presentation.Unity/Editor Bee Roslyn 컴파일이 PASS했다.

## 작업 규칙 (이 저장소)

- 정본 브랜치는 `main` 하나. 새 branch·worktree 만들지 않는다.
- 회사 PC다. Unity 에디터 창과 플레이테스트 EXE를 띄우지 않는다. 검증은 `-batchmode`로 한다.
- `Library`, `Library/Bee`, `Artifacts/FastQa` 캐시를 삭제하지 않는다.
- FastQA는 한 번에 하나만 돌린다. `Artifacts/FastQa/locks/fast-qa.lock`이 남아 있으면 pid가 살아
  있는지 먼저 확인한다. pid는 재사용된다 — 2026-08-20에 이 락의 pid 4592가 `whale`로 재사용되어
  있었다.
- Codex의 미커밋 파일을 스테이징하지 않는다. `git status`로 소유를 먼저 확인한다.
