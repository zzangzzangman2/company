# Codex 작업지시서 — clean biped 리그를 임포트 클립이 통하는 스키닝으로 재작성

작성: 2026-08-26 (Asia/Seoul) / 작성자: Claude
대상: `Tools/Blender/build_father_v18_static_clean_biped_rig.py`

## 목표 (변경 불가)

**Codex의 clean biped 외형 + Claude의 `Casual_Walk_inplace`(action 613) 클립.**
사용자가 승인한 걸음걸이는 그 클립이며, **걸음걸이가 절대 우선이다.** 절차적 보행으로 대체하지 않는다.

## 왜 지금은 안 되는가

`Casual_Walk_inplace` 클립을 clean biped 바디에 얹으면 상체가 굽고 팔이 어긋난다. 두 가지를 시도해
둘 다 실패했다.

1. Unity Humanoid 직접 리타겟 → 상체 기울기 `8.42도`, 자세 붕괴
2. 근육값 차분 리타겟(클립 자체 평균을 빼서 절대 기준을 제거) → 기울기 `10.68도`로 **더 나빠짐**
   (`-family3d-father-v18-motion-clip-delta-retarget`로 켤 수 있게 남겨둠)

2번이 실패한 것이 진단이다. 기준 포즈(T-pose) 문제라면 2번이 고쳤어야 한다. 고쳐지지 않았으므로
원인은 기준이 아니라 **스키닝 가중치**다.

현재 `classify_vertex()`는 정점의 **높이(z)와 좌우 거리로 뼈를 배정**한다.

```python
if z < 0.28:   arm_vertex = False
elif z < 0.53: arm_vertex = lateral > 0.116
...
if z < 0.425:  return {side + "Hand": 1.0}
if z < 0.475:  return smooth_pair(side + "Hand", side + "ForeArm", z, 0.425, 0.475)
```

이 방식은 뼈가 **rest pose 근처에서 작게** 움직일 때만 성립한다. 절차적 보행은 각도가 작아서 멀쩡하지만
실제 클립의 큰 관절 회전이 오면 메시가 무너진다. 기준 포즈를 고쳐도 가중치는 고쳐지지 않는다.

## 요청 사항

### 1. 스키닝을 해부학 기반으로 교체

높이 구간 배정을 버리고 **뼈 거리 기반 자동 가중치**로 바꾼다. Blender의
`bpy.ops.object.parent_set(type='ARMATURE_AUTO')`(heat map) 또는 뼈 envelope 기반 가중치를 쓴다.
정점이 실제로 어느 뼈에 가까운지로 결정되어야 하며, 팔과 다리, 좌우 다리가 섞이지 않는 기존 안전장치는
유지한다.

### 2. bind pose를 T-pose로

원본이 팔을 몸 옆에 내린 자세이므로, 리깅 후 **팔 뼈를 수평으로 벌린 자세를 bind pose로 만든다.**
Unity Humanoid는 T-pose를 기준으로 근육값을 해석한다. 팔을 벌린 뒤 `Armature Apply as Rest Pose`로
확정하고, 메시도 그 상태로 재바인딩한다.

### 3. 검증은 클립으로 한다

리그 자체만 보지 말고 **`Casual_Walk_inplace` 클립을 얹은 상태로** 검증한다. 절차적 보행이 잘 도는
것은 이 문제에 대한 증거가 아니다.

## 합격 기준 (숫자로)

빌드 후 아래 명령으로 영수증을 만들고 수치를 확인한다.

```
FamilyCompanyFatherV18CasualWalkMapQa.exe
  -family3d-father-v18-motion-map-qa
  -family3d-starter-office-qa-runtime-output <경로>
  -family3d-starter-office-qa-auto-quit-seconds 60
```

| 항목 | 계산 | 합격선 | 현재(clean biped + 클립) |
|---|---|---|---|
| 상체 기울기 | `acos(torsoUpLocal.y)` 평균 | **3도 이하** | 8.42도 |
| 발 스윙 정렬 | 발가락축 진폭 ÷ 수직축 진폭 (**골반 기준**) | **20:1 이상** | — |
| 보폭 | 골반 기준 발 진폭 × 2 ÷ 신장 | **0.35~0.55 신장** | — |
| `toeForwardLocal` 집중도 | 원형 집중도 | **0.95 이상** | 0.995 |

**팔다리 진폭은 반드시 `footLocal - hipsLocal`로 계산한다.** 호스트 로컬 그대로 쓰면 캐릭터가 맵을 도는
이동이 섞여 들어와 결론이 뒤집힌다(실측: 골반이 호스트 기준 `2.485` 이동). 자세한 내용은
[FATHER_V18_FACING_OFFSET_METHOD.md](FATHER_V18_FACING_OFFSET_METHOD.md) 4절.

## 리깅 후 Claude가 할 일

리그가 합격선을 넘으면 Claude가 이어서 처리한다. 미리 하지 말 것.

1. 방향 오프셋 `K = -atan2(F.x, F.z)` 재산출 (`toeForwardLocal` 기준, 집중도 0.95 이상일 때만)
2. `fatherMotionStrideOfficeUnits` 재산출 (실측 보폭 ÷ office→QA 배율 `0.9082`)
3. 두 값을 `Family3DStarterOfficeCandidateQaBuilder`의 후보별 상수에 반영

## 하지 말 것

- **힉스필드 재생성 금지.** clean biped 메시와 Claude의 메시는 정점 `28,895` 대 `28,924`, 알베도 픽셀
  평균 차이 `7.76/255`(3%)로 사실상 같다. 같은 메시를 다시 리깅해도 지금 있는 에셋과 같은 결과가 나오며
  8 credits만 소모된다. 잔액 `68`을 유지한다.
- **절차적 보행으로 대체 금지.** 사용자 승인 대상은 클립이다.
- **`poseStrength`를 1.0 미만으로 낮춰 자세를 가리지 말 것.** 2026-08-26에 같은 시도가 보폭을 반토막
  내고 미끄러짐을 만들었다.
- production 씬, default 빌드, `Downloads/`, 배포본은 건드리지 않는다. `productionEligible=false` 유지.
