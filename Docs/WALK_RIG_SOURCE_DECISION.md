# WALK RIG SOURCE DECISION — 뼈대와 걷기 클립은 만들지 말고 가져온다

> **2026-08-20 폐기 공지:** 아래 결론은 거부된 Humanoid bake 연구 기록이다. 실제 화면에서
> primitive 3D 주인공과 과한 바운스가 2D 사무실에 맞지 않아 production 결정에서 제외했다.
> 현재 정본은 `Docs/CHARACTER_LOCOMOTION_GENERATION_V1.md`이며, 게임 출력은 8방향×6포즈 2D Sprite다.
> Mixamo는 0.8초 timing과 반대 팔·다리 관절 순서를 참고하는 데만 사용한다.

이 문서는 "걷는 모션이 자연스럽지 않다"를 그림 문제가 아니라 **소스 문제**로 다시 정의한다.

## 당시 결론 (현재 폐기)

우리 런타임은 이미 정답 구조다. 바꿀 것은 **베이커에 무엇을 먹이는가**뿐이다.

> **걷기 사이클을 우리가 만들지 않는다.** 무료 휴머노이드 리그와 걷기 클립을 받아
> `PlayerWalkRigV2Baker`에 먹이고, 카메라를 45도씩 돌려 8방향을 뽑는다.
> 우리 원화는 그 모델의 텍스처로 살아남고, 초상 72장은 UI에 그대로 쓴다.

## 왜 지금 방식이 8배로 어려운가

`PlayerBakedWalkCatalogV2`는 이미 이렇게 선언되어 있다.

```csharp
public const int DirectionCount = 8;
public const int PoseCount = 8;
```

즉 런타임이 요구하는 것은 **64장 베이크 PNG**다. 현재 진행 상황은 `south` 1방향뿐이다.

그런데 `PlayerWalkRigV2Baker.RequireAuthoredRig`가 2D 페이퍼돌을 강제한다.

```csharp
if (!hasLimbSolver)
    throw new InvalidOperationException("Player walk rig has no LimbSolver2D authoring component.");
if (renderers.Length < 12)
    throw new InvalidOperationException("Player walk rig must expose at least 12 rigid SpriteRenderer parts.");
```

`rig-contract.json`의 `requiredLayers`는 17개 파트를 이름으로 못 박는다
(`thigh_R_art`, `shin_R_art`, `shoe_R_art`, `upper_arm_R_art`, ... `hat_art`).

**2D 페이퍼돌은 방향마다 새 리그다.** 남향 리그를 동향으로 돌릴 수 없다. 팔다리 겹침 순서,
가려지는 파트, 실루엣이 방향마다 다르기 때문이다. 따라서 이 길의 남은 비용은:

| 항목 | 방향당 | 8방향 총량 |
| --- | --- | --- |
| 손으로 자른 PSB 레이어 | 17개 | 136개 |
| 손으로 키잉한 walk `.anim` | 1개 | 8개 |
| 접지 QA 통과 | 8포즈 | 64포즈 |

걷기 사이클 키잉은 애니메이터의 전문 작업이다. 이걸 8번 하는 것이 지금의 계획이고,
그래서 며칠이 걸려도 진도가 안 난다. **구현 실패가 아니라 견적 실패다.**

## 3D 리그를 소스로 쓰면 무엇이 사라지는가

| 항목 | 2D 페이퍼돌 | 3D 리그 소스 |
| --- | --- | --- |
| 걷기 사이클 제작 | 방향마다 손 키잉 (8회) | **받는다 (0회)** |
| 8방향 확보 | 리그를 8개 authoring | **카메라 yaw 45도씩 (모델 1개)** |
| 팔다리 겹침 순서 | 방향마다 수동 정렬 | 렌더러가 깊이로 자동 |
| 발 접지 | 포즈마다 사람이 보증 | 클립이 이미 보장, 베이커가 측정만 |
| 물건 들기 | 별도 포즈 원화 | IK 손 타겟 |
| 애니메이션 추가(앉기/타이핑) | 방향마다 신규 원화 | 클립 하나 더 받아서 재베이크 |

**런타임은 한 줄도 안 바뀐다.** 결과물은 여전히 PNG 시트이고 `DirectionalSpriteAnimator`가
그대로 읽는다. 도트 룩도 유지된다 — 베이커가 이미 픽셀 보존으로 렌더한다.

```csharp
target.filterMode = FilterMode.Point;
target.antiAliasing = 1;
camera.allowHDR = false;
camera.allowMSAA = false;
```

이것이 디아블로 2·스타크래프트·롤러코스터 타이쿤이 쓴 고전 2.5D다. 3D로 만들고 2D로 굽는다.
게임은 2D로 돌고, 3D는 **작업실에만 존재한다.**

## 뼈대와 클립을 어디서 받는가

### 1순위 — Mixamo (Adobe, 무료, 계정 필요)

<https://www.mixamo.com/>

우리 목적에 가장 정확히 맞는다. 리그가 없는 메시를 올리면 **자동으로 뼈대를 심어주고**
(Auto-Rigger), 걷기·달리기·앉기·타이핑을 FBX로 내려준다. 로열티 프리 상업 이용 허용이다.
사용 전 현재 약관을 직접 확인한다.

받을 클립: `Walking`, `Idle`, `Standing Idle`, `Sitting`, `Typing`.
KShopGo가 클립 7개로 전부 처리한 것과 같은 규모다.

### 2순위 — Quaternius (CC0, 계정 불필요)

- <https://quaternius.com/packs/universalbasecharacters.html> — 베이스 캐릭터
- <https://quaternius.com/packs/universalanimationlibrary.html> — 애니메이션 120개+
- <https://quaternius.com/packs/universalanimationlibrary2.html> — 130개+
- <https://quaternius.com/packs/modularcharacteroutfitsfantasy.html> — 모듈식 의상 62파트

CC0라 출처 표기조차 필요 없다. 휴머노이드 리그이므로 리타게팅된다. 계정이 없어도 받는다.

### 참고 — Synty POLYGON (유료)

<https://syntystore.com/collections/polygon> — KShopGo가 실제로 쓴 팩
(`PolygonCity`, `PolygonShops`). 상업 유료다. 무료 대안이 충분하므로 필수 아니다.

<https://assetstore.unity.com/packages/3d/environments/polygon-starter-pack-art-by-synty-156819>
는 무료지만 카테고리가 3D Environments다. 리그된 캐릭터 포함 여부는 미확인이다.

### 2D 걷기 리그 라이브러리는 사실상 없다

무료로 받을 만한 2D 본 워크사이클 라이브러리는 존재하지 않는다. 2D 걷기는 항상 손 애니메이션이다.
**"뼈대만 어디서 가져올 수 없나"의 답이 3D인 이유가 이것이다.** 뼈대를 받는 시장은 3D에만 있다.

## 우리 에셋은 어디까지 살아남는가

| 우리 자산 | 3D 리그 소스 전환 후 |
| --- | --- |
| 초상 원화 72장 | **그대로 UI 초상으로 사용** — 손실 0 |
| 얼굴·헤어·모자 픽셀 | 모델 텍스처로 이식. 정면 얼굴은 거의 그대로 얹힌다 |
| 의상 색·실루엣 | 팔레트로 이식. `Docs/CANON.md` 색을 아틀라스에 적용 |
| 사무실 타일·가구 도트 | **건드리지 않는다.** 배경은 계속 2D다 |
| 좌석 448장·업무 640장 | 앉기·타이핑 클립 베이크로 대체 가능. 당장은 유지 |
| `south` 페이퍼돌 PSB | 폐기하지 않는다. 정면 실루엣 기준으로 남긴다 |

배경은 3D로 갈 이유가 없다. 문제는 캐릭터 움직임 하나다.

## 베이커에 필요한 변경 — 작다

`PlayerWalkRigV2Baker`의 핵심(클립 샘플 → 루트모션 제거 → 접지 측정 → 렌더 → 영수증)은
그대로 재사용한다. 추가할 것만 적는다.

1. **휴머노이드 계약 분기.** `RequireAuthoredRig`의 `LimbSolver2D` / `SpriteRenderer` 검증은
   2D 전용이다. 휴머노이드 계약에서는 `Animator.avatar.isHuman`과 필수 본 존재를 대신 검증한다.
2. **카메라 각도를 계약으로 올린다.** 현재 `camera.transform.rotation = Quaternion.identity`로
   정면 고정이다. 아이소메트릭에는 피치가 필요하다. 우리 타일이 `320x160`이므로
   `atan(160/320) = 26.565도`가 정본 피치다.
3. **방향은 모델 yaw로 만든다.** 방향 인덱스 `d`에 대해 모델을 `d * 45도` 회전시켜
   같은 클립을 8번 베이크한다. 계약 8개 또는 계약 1개 + 방향 루프.
4. **루트모션 제거를 3D 평면으로.** 지금은 `(x, y)` 평면을 뺀다. 3D는 `(x, z)`가 지면이므로
   카메라 공간으로 투영한 뒤 제거한다.
5. **본 이름을 계약으로.** 이미 `rootMotionTransform` / `leftFootContactTransform` /
   `pelvisTransform`이 문자열이라 그대로 쓸 수 있다. Mixamo는 `mixamorig:Hips`,
   `mixamorig:LeftFoot`이고 Quaternius는 `Root` / `Hips` 계열이다.

`PlayerBakedWalkCatalogV2`, `PlayerBakedWalkPresenterV2`, `DirectionalSpriteAnimator`,
`PlayerWalkPresentationModeResolver`는 **변경 없다.** `-familyCompanyPlayerBakedWalkV2`
플래그로 기존 48프레임과 나란히 비교할 수 있는 구조도 이미 갖춰져 있다.

## 이 문서가 정하지 않는 것

- 어느 팩을 실제로 받을지. 라이선스 동의와 다운로드는 사용자가 직접 한다.
- 기존 `south` 페이퍼돌 작업을 중단할지. 정면 실루엣 기준으로는 여전히 가치가 있다.

## 별건 — 그림과 무관한 자연스러움

프레임이 완벽해도 이동 규칙이 딱딱하면 딱딱해 보인다. `Docs/KSHOPGO_MOVEMENT_TEARDOWN.md`가
측정한 KShopGo 값과 우리 값을 나란히 둔다.

| 항목 | KShopGo | 우리 (`OfficeNavigationMotionRules.cs`) |
| --- | --- | --- |
| 회전 | `m_AngularSpeed = 1200` (사실상 즉시) | 이동 중 정지 gate 없음, 실제 변위 행 즉시 적용 |
| 이동 | speed 1.5 / acceleration 8.0 | speed `1.0` / acceleration `8.0` |
| Walk cadence | 0.8s / 1.2 unit cycle | stride `0.99380799`, 약 `0.9938s/cycle` (`2.0125 steps/s`) |
| 방향 확정 지연 | 없음 | 이동 frame은 stabilization/hysteresis 0 |
| 회피 | `m_ObstacleAvoidanceType = 0` (끔) | 반경·예약 |
| 도착 실패 | `WaitArriveWithTimeout` | **대응물 없음** |

2026-08-20에 `ShortShuffleStrideFraction=0`으로 바꿔 짧은 이동도 전체 gait를 진행하게 했다.
자유 보행 코너·반전의 planted pivot도 제거했고, 제자리 pivot은 상호작용 정렬에만 남겼다.
KShopGo의 world unit은 우리 타일 크기와 다르므로 1.5/1.2를 직접 복사하지 않는다.

이 이동 변경은 `simulation-pure`와 Unity Bee Roslyn 세 어셈블리 컴파일을 통과했다.
