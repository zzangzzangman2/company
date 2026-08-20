# KSHOPGO MOVEMENT TEARDOWN — 우리와 방식이 아예 다르다

`C:\Users\godho\Downloads\com.hclab.kshopgo_1.15\com.hclab.kshopgo.apk` 분해 결과다.
사용자가 "이 캐릭터 움직임이 너무 좋다"고 한 그 게임이다.

## 한 줄 결론

**이 게임은 스프라이트 애니메이션을 쓰지 않는다.** 3D 휴머노이드 리그 + Unity NavMeshAgent +
**애니메이션 클립 7개**로 전부 처리한다. 우리가 며칠째 붙잡고 있는 8방향 × 6프레임 도트 시트와는
같은 문제를 푸는 방식이 아니다.

## 엔진과 정체

- Unity IL2CPP, Addressables 사용, `assets/bin/Data/data.unity3d` 48.7MB
- 게임플레이 네임스페이스가 `CryingSnow.FastFoodRush`다. 에셋스토어/템플릿 기반 게임을 한국 상점
  테마로 리스킨한 것이다. 순수 자체 개발이 아니다.
- 자체 이동 스크립트는 **3개뿐**이다: `CustomerController.cs`, `CoffeeCustomerController.cs`,
  `EmployeeController.cs`. 그 외 `PlayerController`, `CarController`가 있다.

## 에셋 구성 — 3D다

| 타입 | 개수 |
| --- | --- |
| Mesh | 2,200 |
| MeshRenderer | 7,684 |
| **SkinnedMeshRenderer** | **698** |
| **Avatar** | **6** (`CharactersAvatar`, `StickmanAvatar`, `NecoMaid1.1Avatar`) |
| Animator | 52 |
| AnimatorController | 7 |
| **AnimationClip** | **7** |
| Texture2D | 222 |
| Sprite | 87 (UI용) |
| NavMeshAgent | 31 |
| NavMeshObstacle | 165 |

Sprite 87장은 전부 UI다. 캐릭터는 `SkinnedMeshRenderer` 698개, 즉 스킨드 메시다.

## 애니메이션 클립이 7개뿐이다

| 클립 | 길이 | 샘플레이트 | 루프 |
| --- | --- | --- | --- |
| Idle | 2.967s | 30 | 예 |
| Walk | — | 30 | 예 |
| Run | 0.533s | 30 | 예 |
| Sit | 6.400s | 30 | 예 |
| Eat | 2.367s | 30 | 예 |
| Typing | — | 30 | 예 |
| NecoMimi | — | 30 | 예 |

전부 `m_Legacy=False`, `m_Compressed=False`, **휴머노이드 머슬 클립**(`m_MuscleClipSize` 존재)이다.
즉 방향별 클립이 **하나도 없다.** Walk 하나를 만들고 캐릭터를 회전시켜 8방향을 만든다.

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

우리 프로젝트는 반대 방향으로 가 있다. `Docs/PROJECT_STATE.md`와 `DECISIONS.md`에 남아 있는
"각 cardinal segment 방향으로 **planted pivot을 끝낸 뒤** translation" 규칙이 바로 이 게임이 하지
않는 것이다. 이 게임은 걸으면서 돈다.

그리고 `m_ObstacleAvoidanceType = 0`, 즉 **회피를 끈다.** 회피 계산이 만드는 미세한 흔들림과
서로 밀어내는 지터가 없다. 겹침은 그냥 허용한다. 단순한 쪽이 매끄럽다.

`m_StoppingDistance = 0`이라 목표 지점까지 정확히 간다. 도착 판정은 코드가 `HasArrived`로 따로 한다.

## Animator 설정

- 52개 중 **42개가 `m_ApplyRootMotion = True`**다. 루트 모션이 이동을 만든다.
- `m_UpdateMode = 0` (Normal), `m_CullingMode = 1` (CullUpdateTransforms)
- 나머지 9개는 루트 모션 없음 — NavMeshAgent가 위치를 몰고 애니메이션은 보여주기만 한다.

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
| 회전 연출 | 걸으면서 돈다 | planted pivot 끝낸 뒤 translation |
| 경로 | NavMeshAgent | 자체 타일 A* + coordinator |
| 회피 | 끔 (None) | 충돌 반경·예약 |
| 발 접지 | 루트 모션 | 프레임별 support foot drift ≤1px QA |
| 물건 들기 | IK 손 타겟 | 별도 포즈 원화 |

**우리가 어려운 이유가 여기서 보인다.** 이 게임은 방향과 접지를 엔진에 넘긴다. 우리는 그 둘을
사람이 그린 픽셀로 매 프레임 보증하려 한다. 후자는 원래 훨씬 어렵다. 며칠 걸리는 게 이상한 일이
아니다.

## 그대로 가져올 수 있는 것

3D로 갈아타지 않아도 옮길 수 있는 것들이다.

1. **회전을 즉시로 만든다.** 1200°/s는 사실상 즉시다. planted pivot을 없애고 걷는 중에 방향 행만
   바꾸면 "돌려고 멈추는" 느낌이 사라진다. 지금 우리 규칙이 정반대로 못 박혀 있으므로 이건 결정 변경이다.
2. **회피를 끈다.** 겹침을 허용하면 지터가 사라진다. 우리도 예약·반경 회피가 흔들림의 원인일 수 있다.
3. **도착 타임아웃을 둔다.** `WaitArriveWithTimeout` / `SafeSetDestination`에 대응하는 것이 우리에게
   없다. 지금 `OFFICE_EMPTY_WANDER_FAIL`이 매 tick 재시도로 반복되는 증상이 정확히 이 부재다.
4. **발소리를 프레임 이벤트로** 낸다. 지금은 별도 타이밍이다.
5. **클립 수를 줄인다.** 이 게임은 7개로 전부 한다. 우리가 방향마다 원화를 늘리는 방향은 유지 비용이
   계속 커진다.

## 가져올 수 없는 것

- 루트 모션과 IK는 리그가 있어야 한다. 도트 시트에는 대응물이 없다.
- 클립 1개로 8방향을 만드는 것은 3D 회전이라 가능하다. 2D 도트는 방향마다 그림이 필요하다.
  **이 차이는 기법의 한계이고 우리 구현의 결함이 아니다.**

## 결정이 필요한 갈림길

이 분해가 알려주는 진짜 질문은 "우리 도트를 어떻게 더 잘 만들까"가 아니다.

> **캐릭터를 3D 로우폴리 + 휴머노이드 리그로 바꿀 것인가?**

바꾸면 방향·접지·물건 들기가 전부 엔진 몫이 되고 클립 7개로 끝난다. 지금까지의 도트 작업
(가족 192장, 좌석 448장, 업무 640장)은 버려진다. 유지하면 방향별 원화를 계속 그려야 하고 접지 QA도
계속 사람이 보증해야 한다.

이건 기술 선택이 아니라 아트 방향 결정이라 사용자가 정해야 한다.
