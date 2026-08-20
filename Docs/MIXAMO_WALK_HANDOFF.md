# MIXAMO WALK HANDOFF — 걷기를 받아서 굽는 경로, 이어받는 문서

이 문서만 읽고 이어받을 수 있게 쓴다. 배경은 `Docs/WALK_RIG_SOURCE_DECISION.md`,
측정 근거는 `Docs/KSHOPGO_MOVEMENT_TEARDOWN.md`에 있다.

## 한 줄

걷기 사이클을 우리가 만들지 않는다. Mixamo에서 리그와 걷기 클립을 받아
`PlayerWalkHumanoidBaker`에 넣고, 모델을 45도씩 돌려 **8방향 × 8포즈 = 64장 PNG**를 굽는다.
런타임과 카탈로그는 변경하지 않는다.

## 지금까지 된 것

| 항목 | 상태 |
| --- | --- |
| `Assets/FamilyCompany/Editor/PlayerWalkHumanoidBaker.cs` | 작성 완료, **컴파일 PASS** |
| `Assets/FamilyCompany/Editor/PlayerWalkHumanoidModelImporter.cs` | 작성 완료, **컴파일 PASS** |
| `ArtSources/PlayerWalkHumanoid/humanoid-contract.json` | 작성 완료 |
| `Assets/FamilyCompany/Editor/PlayerWalkHumanoidAuthoring/PlayerHumanoidBase.fbx` | **받아서 넣음** (Mixamo X Bot, T-Pose, FBX for Unity, 1,750,032바이트, SHA-256 `BA1FBC01DF013A102363E88E698719176A4366CE6B3C01AB500319DF55C37BA1`) |
| 걷기 클립 FBX | **아직 없음. 유일한 남은 차단 요인이다.** |
| 실제 베이크 실행 | 클립이 없어 미실행. 알고리즘 미검증. |

`editor-validation` 결과: `FAST QA: PASS profile=editor-validation compileSeconds=104.325 head=5f387bb9`

## 남은 단계 — 클립 하나만 받으면 된다

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

### 3. 베이크 실행

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

### 4. 카탈로그 승격

8방향 영수증이 모두 생기면 Codex가 만든 `PlayerBakedWalkV2CatalogBuilder`가 카탈로그를 만든다.
런타임은 기본값이 `Legacy48`이므로 **저절로 바뀌지 않는다.** 비교는 플래그로 한다.

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

그리고 스케일은 우리가 고르지 않는다. 한 사이클이
`strideWorld / visualScale = 0.99380799 / 1.55 = 0.64117` 월드 유닛을 덮도록
`RequiredUniformScale`이 계산된다. 사람 크기를 우리 보폭에 맞추는 것이지 그 반대가 아니다.

## 카메라 각도 — 추측이 아니다

`IsometricCameraFollow`의 사무실 프레이밍에서 그대로 유도했다.

```csharp
officeOffset     = new Vector3(0f, 13.5f, -13.5f);
officeLookHeight = 0.6f;
```

yaw가 0이므로 **축 정렬 시야**다. 대각선 아이소메트릭이 아니다. 피치는
`atan((13.5 - 0.6) / 13.5) = 43.6975도`다. 캐릭터를 이 각도로 렌더해야 바닥과 단축이 맞는다.

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

두 경로는 같은 PNG 폴더에 쓴다. **동시에 굽지 말 것.** 나중에 쓴 쪽이 이긴다.

## 아직 안 한 것

- **베이커를 한 번도 실행하지 못했다.** 컴파일만 통과했다. 위상 정렬과 루트 이동거리 재보간은
  실제 클립 없이는 검증 불가다.
- 방향별 실루엣 확인. 8방향 중 north(뒷모습)와 대각선이 픽셀 단위로 읽히는지는 눈으로 봐야 한다.
- 나머지 클립(Idle / Run / Sitting / Typing). KShopGo는 7개로 전부 처리했다.

## 그림과 별개인 문제 — 아직 안 고쳤다

프레임이 완벽해도 이동 규칙이 딱딱하면 딱딱하다.
`Assets/FamilyCompany/Simulation/Navigation/OfficeNavigationMotionRules.cs`의 값들이다.

| 항목 | KShopGo | 우리 |
| --- | --- | --- |
| 회전 | `m_AngularSpeed = 1200` (사실상 즉시) | `PivotSeconds = 0.06f` 정지 피벗 |
| 방향 확정 지연 | 없음 | `DefaultFacingStabilizationSeconds = 0.075f` |
| 허용 방향 오차 | 없음 | `MaximumHeldFacingErrorDegrees = 30.5f` |
| 회피 | `m_ObstacleAvoidanceType = 0` (끔) | 반경·예약 |
| 도착 실패 | `WaitArriveWithTimeout` | 대응물 없음 |

그리고 `ShortShuffleStrideFraction = 0.30f` 구간에서 `ShuffleFrame`이 프레임 0과 3만 내보낸다.
타일 단위로 짧게 움직이는 사무실에서는 6프레임 사이클이 아니라 **2프레임 스터터**가 기본 재생이 된다.

이 파일은 순수 C#이라 `simulation-pure`로 검증되고 **플레이어 창을 띄우지 않는다.**
`OfficeMovementFacingNavigationValidation`, `OfficeSharedLocomotionStrictValidation`,
`OfficeNavigationRegressionSuite`(`PivotSeconds` 참조)가 현재 값을 고정하고 있으므로
값 변경은 그 검증들도 같이 갱신해야 한다. `DECISIONS.md`에 planted pivot 규칙이 정본으로 적혀
있으므로 **방향 결정 변경**으로 기록해야 한다.

## 작업 규칙 (이 저장소)

- 정본 브랜치는 `main` 하나. 새 branch·worktree 만들지 않는다.
- 회사 PC다. Unity 에디터 창과 플레이테스트 EXE를 띄우지 않는다. 검증은 `-batchmode`로 한다.
- `Library`, `Library/Bee`, `Artifacts/FastQa` 캐시를 삭제하지 않는다.
- FastQA는 한 번에 하나만 돌린다. `Artifacts/FastQa/locks/fast-qa.lock`이 남아 있으면 pid가 살아
  있는지 먼저 확인한다. pid는 재사용된다 — 2026-08-20에 이 락의 pid 4592가 `whale`로 재사용되어
  있었다.
- Codex의 미커밋 파일을 스테이징하지 않는다. `git status`로 소유를 먼저 확인한다.
