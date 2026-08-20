# KSHOPGO MOVEMENT TEARDOWN — 우리와 방식이 아예 다르다

`C:\Users\godho\Downloads\com.hclab.kshopgo_1.15\com.hclab.kshopgo.apk` 분해 결과다.
사용자가 "이 캐릭터 움직임이 너무 좋다"고 한 그 게임이다.

## 한 줄 결론

**이 게임은 스프라이트 애니메이션을 쓰지 않는다.** 3D 휴머노이드 리그 + Unity NavMeshAgent +
**애니메이션 클립 7개**로 전부 처리한다. 우리가 며칠째 붙잡고 있는 8방향 × 6프레임 도트 시트와는
같은 문제를 푸는 방식이 아니다.

## 이 분석을 우리 주인공 보행에 적용하는 강제 규칙

KShopGo를 단순 분위기 참고로 쓰지 않는다. 게임 Walk의 0.8초·30fps·24샘플을 정규화하고 Mixamo
`Unarmed Walk Forward` 관절을 확인한 뒤, east 2D 여섯 포즈를 원본 sample
`0/4/8/12/16/20`에서 뽑는다. KShopGo 속도를 그대로 미리 볼 때의 시각 시점은
`0/133.3/266.7/400/533.3/666.7ms`, loop `800ms`다.

| 2D 포즈 | 원본 샘플 | 필수 동작 |
| --- | ---: | --- |
| P0 | 0 | 왼발 새 접촉, 오른 뒤발은 앞코만 마지막 접지. 두 발 동시 평발 금지 |
| P1 | 4 | 왼발 flat support/load, 골반이 지지 발목 위로 이동, 오른 무릎·발 최대 회수 |
| P2 | 8 | 왼발 terminal stance/뒤꿈치 이탈, 오른발이 몸 아래를 낮게 지나 +X 전방 착지 준비 |
| P3 | 12 | 오른발 새 접촉, 왼 뒤발은 앞코만 마지막 접지. support owner 교대 |
| P4 | 16 | 오른발 flat support/load, 왼 무릎·발 최대 회수 |
| P5 | 20 | 오른발 terminal stance/뒤꿈치 이탈, 왼발이 몸 아래를 낮게 지나 +X 전방 착지 준비 |

필수 불변식은 다음과 같다.

- support shoe는 sprite root 기준 -X로 단조 이동하고 logical root와 합친 월드 밑창은 1px 이내로 고정한다.
- swing shoe는 rear→under-hip→front로 +X 단조 이동한다. 어느 포즈에서도 앞코는 east(+X)다.
- 신발만 옮기지 않고 같은 physical owner의 hip→knee→ankle→shoe chain 전체를 추적한다.
- 접촉 포즈의 뒤발은 toe-only이며 heel이 떠야 한다. P1/P4부터 swing foot는 완전히 공중이다.
- 머리·상체 bob 총폭은 1~2px 이내다. 동일 contact를 복제해 한 틱 멈추거나 내부 음영만 바꾸지 않는다.
- 하체 좌우반전, 화면 좌/우를 physical leg ID로 사용, 신발/종아리 조각 합성, ImageGen의 임의 관절 생성은 금지한다.

다운로드 입력은 `C:/Users/godho/Downloads/X Bot.fbx`와
`C:/Users/godho/Downloads/X Bot@Unarmed Walk Forward.fbx`이고, 프로젝트에 byte-identical한
`PlayerHumanoidBase.fbx`, `PlayerHumanoidWalk.fbx`가 이미 들어 있다. 외형은 2D 주인공을 유지하지만
동작은 이 기준에서 벗어나 새로 발명하지 않는다. 세부 제작 절차는 `CHARACTER_LOCOMOTION_GENERATION_V1.md`가 소유한다.

2026-08-20 현행 project 변환은 pose당 root advance `19.234993px`이며
`ArtSources/PlayerEastMixamoTraceV2/phase-contract.md`의 target heel/toe 최대 contact drift는
`0.765007px`다. 이는 관절/접지 계약 PASS이고 완성 raster/GIF 승인은 아니다. 정확한 재개점은
`HOME_PC_WALK_CHECKPOINT_2026-08-20.md`에 있다.

## 엔진과 정체

- Unity IL2CPP, Addressables 사용, `assets/bin/Data/data.unity3d` 48.7MB
- 게임플레이 네임스페이스가 `CryingSnow.FastFoodRush`다. 에셋스토어/템플릿 기반 게임을 한국 상점
  테마로 리스킨한 것이다. 순수 자체 개발이 아니다.
- 자체 이동 스크립트는 **3개뿐**이다: `CustomerController.cs`, `CoffeeCustomerController.cs`,
  `EmployeeController.cs`. 그 외 `PlayerController`, `CarController`가 있다.

## 에셋 구성 — 3D다

| 타입 | 개수 |
| --- | --- |
| Mesh | 2,213 |
| MeshRenderer | 7,684 |
| **SkinnedMeshRenderer** | **698** |
| **Avatar** | **6** (`CharactersAvatar`, `StickmanAvatar`, `NecoMaid1.1Avatar`) |
| Animator | 52 |
| AnimatorController | 7 |
| **AnimationClip** | **7** |
| Texture2D | 259 |
| Sprite | 87 (UI용) |
| NavMeshAgent | 31 |
| NavMeshObstacle | 165 |

Sprite 87장은 전부 UI다. 캐릭터는 `SkinnedMeshRenderer` 698개, 즉 스킨드 메시다.

## 애니메이션 클립이 7개뿐이다

| 클립 | 길이 | 샘플레이트 | 루프 |
| --- | --- | --- | --- |
| Idle | 2.967s | 30 | 예 |
| Walk | **0.800s** | 30 | 예 |
| Run | 0.533s | 30 | 예 |
| Sit | 6.400s | 30 | 예 |
| Eat | 2.367s | 30 | 예 |
| Typing | 16.467s | 30 | 예 |
| NecoMimi | 15.033s | 60 | 예 |

전부 `m_Legacy=False`, `m_Compressed=False`, **휴머노이드 머슬 클립**(`m_MuscleClipSize` 존재)이다.
즉 방향별 클립이 **하나도 없다.** Walk 하나를 만들고 캐릭터를 회전시켜 8방향을 만든다.

Walk 한 주기는 24샘플이다. `CustomerAnimator`와 `StickmanAnimator`의 Idle→이동, 이동→Idle 전이는
모두 exit time 없는 **고정 0.25초 cross-fade**이고 상태 속도는 1.0이다. 1.5 unit/s로 0.8초를 가므로
한 주기 이동거리는 1.2 unit, 두 발 기준 cadence는 2.5 steps/s다.

`CharactersAvatar` 하나를 손님·직원·경비 전부에 리타게팅한다. 우리가 캐릭터마다 48장씩 그리는 것과
정반대다.

## 이동의 정본은 NavMeshAgent다 — 31개 전부 동일 설정

```text
m_Speed                    1.5
m_AngularSpeed             1200.0
m_Acceleration             8.0
m_StoppingDistance         0.0
m_Radius                   0.5
m_Height                   2.0
m_BaseOffset               0.0
m_AutoBraking              True
m_AutoRepath               True
m_AutoTraverseOffMeshLink  True
m_ObstacleAvoidanceType    0        (None)
avoidancePriority          50
```

**여기서 제일 중요한 숫자는 `m_AngularSpeed = 1200`이다.** 초당 1200도, 즉 한 프레임에 20도씩
돈다. 사실상 즉시 회전이다. 그래서 "돌기 위해 멈추는" 느낌이 전혀 없다.

2026-08-20 이전 우리 프로젝트는 반대 방향이었다. 각 cardinal segment 앞에서 planted pivot을 끝낸 뒤
translation했지만, 현재 정본은 이 중간 정지를 폐기하고 실제 변위 방향으로 걸으면서 방향 행을 바꾼다.

그리고 `m_ObstacleAvoidanceType = 0`, 즉 **회피를 끈다.** 회피 계산이 만드는 미세한 흔들림과
서로 밀어내는 지터가 없다. 겹침은 그냥 허용한다. 단순한 쪽이 매끄럽다.

`m_StoppingDistance = 0`이라 목표 지점까지 정확히 간다. 도착 판정은 코드가 `HasArrived`로 따로 한다.

## Animator 설정 — Apply Root Motion 플래그를 이동 원인으로 오해하면 안 된다

- 52개 중 **42개가 `m_ApplyRootMotion = True`**, 10개가 False다.
- `m_UpdateMode = 0` (Normal). culling은 52개 중 51개가 mode 1(CullUpdateTransforms), 1개가 mode 0이다.
- 그러나 Walk/Run을 포함한 7개 클립 모두 `m_AverageSpeed=(0,0,0)`, `m_AverageAngularSpeed=0`이고
  시작·끝 root 위치가 수치 오차 안에서 같다. `m_KeepOriginalPositionXZ=True`이므로 **실제 이동 클립은
  인플레이스**다. Apply Root Motion 플래그만 보고 루트 모션이 이동을 만든다고 결론 내릴 수 없다.
- 52개 모두 `m_StabilizeFeet=False`, `m_LinearVelocityBlending=False`다. Animator state의
  `m_IKOnFeet`도 False다. 자연스러움은 feet stabilization 옵션이 아니라 휴머노이드의 프레임 간 보간,
  0.25초 상태 전이, 연속 agent/controller 이동에서 나온다.
- `CustomerAnimator` layer의 IK pass는 True다. 이는 발이 아니라 `OnAnimatorIK` 손 타겟용이다.

## 코드 쪽 구성

`CustomerController` 멤버:
```text
agent, queuePoint, exitPoint, seat, food
AssignSeat, WalkToSeat, WalkToExit, HasArrived
TriggerEat, FinishEating
OnAnimatorIK, leftHandTarget, rightHandTarget, IK_Weight
```

`EmployeeController` 멤버:
```text
baseSpeed, baseCapacity, currentActivity, interactRadius, Busy
IsNear, WaitCloseTo, SafeSetDestination, WaitArriveWithTimeout, OnStep
HandleActivity, CleanTable, RefillCafe, RefillPizza, RefillPC, RefillFood
```

`PlayerController` 멤버:
```text
moveSpeed, rotateSpeed, movement, isGrounded, gravityValue
footsteps, audioSource, animationEvent
```

읽을 점이 셋 있다.

1. **`OnAnimatorIK` + 손 타겟 + `IK_Weight`** — 쟁반을 드는 손을 IK로 붙인다. 클립을 물건별로 만들지
   않는다.
2. **`OnStep` + `footsteps`** — 발소리를 애니메이션 이벤트로 낸다. 프레임과 소리가 자동으로 맞는다.
3. **`SafeSetDestination` / `WaitArriveWithTimeout`** — 목적지 설정 실패와 도착 실패에 타임아웃을
   둔다. 길이 막혀도 영원히 멈춰 있지 않는다.

속도는 업그레이드 스탯이다. `RestaurantData`에 `EmployeeSpeed`, `PlayerSpeed`, `EmployeeCapacity`,
`PlayerCapacity`가 있다.

## 우리와 비교하면

| | KShopGo | 우리 |
| --- | --- | --- |
| 캐릭터 표현 | 3D 스킨드 메시 + 휴머노이드 리그 | 8방향 × 6프레임 도트 시트 |
| 걷기 에셋 | 클립 1개(Walk)를 회전으로 8방향 | 캐릭터당 48장, 가족 4명 192장 |
| 방향 처리 | Transform 회전, 즉시 (1200°/s) | 화면 변위를 옥탄트로 양자화해 시트 행 선택 |
| 회전 연출 | 걸으면서 돈다 | **현재 동일: 이동 중 즉시 방향 행 변경** |
| 경로 | NavMeshAgent | 자체 타일 A* + coordinator |
| 회피 | 끔 (None) | 충돌 반경·예약 |
| 발 접지 | 인플레이스 휴머노이드 보간 | 프레임별 support foot drift ≤1px QA |
| 물건 들기 | IK 손 타겟 | 별도 포즈 원화 |

**우리가 어려운 이유가 여기서 보인다.** 이 게임은 방향과 접지를 엔진에 넘긴다. 우리는 그 둘을
사람이 그린 픽셀로 매 프레임 보증하려 한다. 후자는 원래 훨씬 어렵다. 며칠 걸리는 게 이상한 일이
아니다.

## 그대로 가져올 수 있는 것

3D로 갈아타지 않아도 옮길 수 있는 것들이다.

1. **회전을 즉시로 만든다.** 1200°/s는 사실상 즉시다. planted pivot을 없애고 걷는 중에 방향 행만
   바꾼다. 2026-08-20에 player/NPC 공용 이동 규칙에 적용했다.
2. **회피를 끈다.** 겹침을 허용하면 지터가 사라진다. 우리도 예약·반경 회피가 흔들림의 원인일 수 있다.
3. **도착 타임아웃을 둔다.** `WaitArriveWithTimeout` / `SafeSetDestination`에 대응하는 것이 우리에게
   없다. 지금 `OFFICE_EMPTY_WANDER_FAIL`이 매 tick 재시도로 반복되는 증상이 정확히 이 부재다.
4. **발소리를 프레임 이벤트로** 낸다. 지금은 별도 타이밍이다.
5. **클립 수를 줄인 설계를 참고한다.** 우리 최종 출력은 2D 8방향 원화라 클립 하나를 회전 베이크하지 않는다.
6. **속도와 cadence의 관계를 참고하되 world unit을 복사하지 않는다.** 우리 한 타일 중심 거리는
   `0.99380799`이므로 speed `1.0`, stride `0.99380799`로 한 타일에 두 걸음을 맞춘다. 짧은 이동의
   2프레임 `ShortShuffle`은 사용하지 않는다.

## 가져올 수 없는 것

- 휴머노이드 보간과 손 IK는 리그가 있어야 한다. 도트 시트에는 직접 대응물이 없다.
- 클립 1개로 8방향을 만드는 것은 3D 회전이라 가능하다. 2D 도트는 방향마다 그림이 필요하다.
  **이 차이는 기법의 한계이고 우리 구현의 결함이 아니다.**

## 현재 선택한 절충

이 분해가 알려주는 진짜 질문은 "우리 도트를 어떻게 더 잘 만들까"가 아니다.

> **게임 출력은 독립 8방향 2D 스프라이트이고, Mixamo/KShopGo는 동작 참고다.**

실시간 3D로 전환하면 KShopGo의 30fps 보간과 자유 회전을 그대로 얻지만 기존 2D 좌석·업무 자산과
카메라 합성이 전면 변경된다. 현재는 그 비용을 피하면서 빨간 캡 주인공의 8방향×6포즈 2D 원화를 쓰고,
연속 회전과 가속 8.0만 구조적으로 참고한다. 속도·stride는 우리 타일 크기에 맞춘다.
