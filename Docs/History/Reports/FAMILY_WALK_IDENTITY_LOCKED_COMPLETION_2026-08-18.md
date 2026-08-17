# Family Walk Identity-Locked Completion — 2026-08-18

## 범위

가족 4명의 보행을 기존 V4~V10 세대 이름이나 변화율 PASS에서 분리해 처음부터 재진단했다. 사용자 영상과
캡처에서 확인한 실제 결함은 한 발 반복, 큰 런지, 방향/변위 불일치, 방향 전환 때 다른 초상화로 보이는
identity drift, 그리고 다른 세대 start/stop/pivot 그림의 모자·바지·몸통 pop이었다.

작업은 `FC-WALK-GUARDRAIL-V1`과 `FC-WALK-TWOSTEP-GATE-V1`을 최우선 정본으로 진행했다. 기존 dirty
작업은 reset/revert/stash/delete하지 않았다. 실패 후보는 Downloads나 main으로 승격하지 않았다.

## 채택 구조

- candidate 61을 가족 4명×8방향×6프레임의 identity-locked canonical source로 채택했다.
- 방향별 canonical portrait/body anchor 32장을 고정하고, 해부학적으로 분리된 두 다리/발만 결정론적으로
  움직인다. 3·4·5 하체는 0·1·2의 골반축 반사다.
- 표식 없는 shipping source 192장과, 같은 alpha 실루엣에 청록/자홍 좌우 다리 표식만 더한 marker review
  copy 192장을 별도로 추적한다. 표식은 출하 픽셀에 닿지 않는다.
- 제작 예산은 원본 5방향×첫 반주기 3패널=가족당 15패널이다. 동/서, 북동/북서, 남동/남서는 미러 쌍이지만
  north와 south는 서로 다른 원본이 필요하므로 4×3=12가 아니라 5×3=15가 정확하다.
- `build_family_walk_half_cycles_v2.py`는 구형 V4/V5/V6/V7/raw import 모드를 제거한 단일
  source→runtime writer다. source/marker gate PASS 전 `--write`를 거부한다.
- production family actor는 `LocomotionTransitionsV1`의 다른 세대 캐릭터 그림을 사용하지 않는다.

## 실패 후보 요약

- 1–26: 한 방향에서 시작해 실제 네 대각 방향의 구조/정적/사람 검수를 확보하는 과정. gate FAIL은 즉시
  폐기했고, gate PASS라도 치마 seam, X자 다리, 점 신발, 분리 조각, 방향 오류는 사람 검수에서 폐기했다.
- 27–49: 8방향 확장과 cross-direction identity lock. 독립 방향 전신 생성과 잘못된 mirror mapping은 다른
  초상화/몸 비율을 만들었고 제품 후보가 되지 못했다.
- 50–52: pelvis seam, 바지 폭, 신발 간격, 보폭을 래스터 확대/변형으로 각각 보정했다. 한 변수를 고치면
  다른 임계값이 깨져 수렴하지 않았다.
- 53–56: 구조와 marker 교대는 통과했지만 shipping 픽셀에 칠했다 지운 표식의 어두운 외곽이 남았다.
  probe 53의 보행 구조만 연구 기준으로 남기고 출하 PNG는 폐기했다.
- 57–60: unmarked shipping 구조와 가족 통합. flat trousers, mother stride/upper stability,
  older-sister lift, direction tone bias 때문에 폐기했다.
- 61: 구조·표식·정적·identity·사람 검수 통과. 현재 정본.
- 62: raw 재현 시도 자체가 top gate FAIL. 추적 원화에 쓰지 않고 작업 증거로만 보존했다.

## 자동 증거

```text
FAMILY_WALK_TWO_STEP_GATE_SELFTEST: PASS | reflected row accepted, synthetic one-leg row rejected, whole-frame mirror rejected
FAMILY_WALK_ANATOMY_MARKER_GATE: PASS | contract=FC-WALK-TWOSTEP-GATE-V1 source=artsources rows=32
FAMILY_WALK_TWO_STEP_GATE: PASS | contract=FC-WALK-TWOSTEP-GATE-V1 source=artsources rows=32
FAMILY_WALK_RUNTIME_STATIC_ROWS: passed=32 total=32
Ran 10 tests in 155.571s
OK
PASS all family six-pose runtime outputs match identity-locked deterministic sources
```

`FAIL이면 사람 눈으로 뒤집을 수 없다. PASS해도 필요조건일 뿐 충분조건이 아니다.`

## 실제 Player 보행

normal 빈 새 게임 1×에서 08:50→09:50을 실행하고 실제 플레이어 카메라를 30fps로 629장 캡처했다.
20.97초 1280×720 H.264 영상과 네 가족별 8fps review sheet/GIF를 사람 눈으로 확인했다. 네 가족은 방향을
바꿔도 같은 얼굴·머리·몸통·옷 길이·다리/신발 크기를 유지했고, 짧고 낮은 보폭으로 양발을 교대했다.
큰 런지, X자 다리, 크로마, 반투명 잔상, 구형 transition pop을 찾지 못했다.

| 속도 | walk loops | direction mismatch | pre-pivot move | duplicate pivot | non-cardinal | collision/overlap | tile-center/root offset | transition sprite |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1× | 132 | 0 | 0 | 0 | 0 | 0/0 | 0/0 | 0 |
| 2× | 134 | 0 | 0 | 0 | 0 | 0/0 | 0/0 | 0 |
| 4× | 124 | 0 | 0 | 0 | 0 | 0/0 | 0/0 | 0 |

Unity 6000.3.21f1 `editor-broad`와 `player-scripts`도 PASS했다.

## 실제 배치 클릭 회귀

전용 Player gate가 normal 빈 새 게임에서 구매 미리보기만 준비했다. 마지막 확정은 Windows native cursor를
녹색 `(1,1)` 셀 중심에 놓고 실제 왼쪽 클릭 1회를 보냈으며, 제품의 `Input.GetMouseButtonDown(0)`와
`HandlePointer()`를 통과했다.

```text
FAMILY_COMPANY_OFFICE_BUILD_NATIVE_POINTER: PASS
pointerCommitCount=1
stateMutationCount=1
cash=5000000->4986250
ledger=1->2
inventory=0->1
furniture=52->53
editable=0->1
gridHash=104C121BBA787A22->2D928B958610B1BF
runtimeGridHash=2D928B958610B1BF
anchorError=0.00000000
```

녹색 preview와 클릭 후 화분은 같은 타일 중심에 있다.

## Claude 설명에 대한 판정

설명의 핵심은 맞다. 192장을 독립 이미지 생성하면 identity가 드리프트하기 쉽고, seam·바지 폭·신발 간격·
보폭을 완성 래스터에서 늘리는 방식은 변수가 결합돼 수렴하지 않았다. 표식은 출하본이 아니라 별도 검수
사본에만 칠해야 잔류 실패 종류가 구조적으로 사라진다는 설명도 맞다.

두 가지는 보정해야 한다. 생성량은 4방향×3=12가 아니라 north와 south를 각각 유지한 5방향×3=15패널이다.
또 probe 53은 보행 구조 기준으로는 유지할 수 있지만 실제 shipping PNG는 잔류 외곽 때문에 제품 정답이
아니다. 현재 제품 정본은 같은 marker-copy 원칙으로 완성한 candidate 61이다.
