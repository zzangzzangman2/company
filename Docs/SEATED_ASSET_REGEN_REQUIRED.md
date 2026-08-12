# SEATED ASSET REGEN REQUIRED

Office Visual Coherence V4 · P0 독립 진단 결과
진단 기준 HEAD `46c20b7` · 적용 기준 HEAD `08d398b` + 엄마 Work 6장 교체 · Unity `6000.3.21f1`
측정 도구: `Tools/office_visual_coherence_v4_probe.py` (Unity 없이 런타임 합성을 그대로 재현)
산출물: `Artifacts/OfficeVisualCoherenceV4/` (git 제외 경로)

이 문서는 **어떤 문제가 코드·데이터로 해결되고 어떤 것이 원본 PNG 재생성인지**만 판정한다.
자동 추정값은 정본이 아니며, 사용자 승인 전까지 catalog에 기록하지 않는다.

> **채택된 해법 (2026-08-12, `08d398b`)**: 바닥 접점 배치를 폐기하고 승인된 인물 좌판 등록점을
> 실제 의자 cushion anchor에 고정한다. 착석 중에는 의자 base < 인물 < 의자 등받이/근접 팔걸이
> 전면 레이어 순서로 그리며, 공통 시각 scale은 `1.55`다. `OfficeRuntimeDepthSorter`가 footprint와
> 착석 상태를 함께 사용해 이 깊이 규칙을 소유한다.
> 아래 §1.2·§2.2는 실패 원인 기록으로만 남긴다. §2.4의 엄마 `Northwest/Work/0..5`는 전부
> 하체가 잘린 원본이었으므로 6장 모두 재생성·교체했다.

---

## 0. 측정 환경

최초 진단 PC에는 Unity `6000.3.21f1`이 없어 오프라인 프로브로 판정했다. 최종 적용 PC에서는 같은
버전으로 화면 없는 batchmode 컴파일·`PrototypeValidation`, Windows x64 빌드, 숨김 창 플레이어
`-familyCompanyTileRuntimeQa`를 실행했다. 최종 플레이어 캡처와 로그는
`Artifacts/MotherSeatedRegenQa/`에 남긴다.

프로브가 재현하는 규칙

| 런타임 | 프로브 |
| --- | --- |
| `OfficeGridTilemapPresenter` 등각 basis, cell 320×160, PPU 180 | 동일 |
| `OfficeGridFurniturePresenter` pivot = `groundAnchorPx` | 동일 |
| `OfficeRuntimeAgent.ApplySeatedPose` pelvis→chair seat 고정 | 동일 |
| `OfficeRuntimeDepthSorter` footprint sort: chair base < occupant < chair front | 동일 |
| `ResolveDynamicSortingOrder = 5000 − round(worldY×100)` | 동일 |

**동치 확인**: 프로브 출력과 마지막 실제 Unity 캡처
`Artifacts/SeatedSpriteRootCauseV3/father-work-closeup.png`가 동일한 배치(캐릭터가 의자 좌측 앞,
책상 앞다리가 몸통을 가로지름)를 보여준다. 프로브는 Unity 실행 없이 기하 판정에만 사용하며,
최종 육안 승인은 실제 빌드에서 다시 받아야 한다.

---

## 1. 코드·데이터로 해결 (원본 PNG 문제 아님)

### 1.1 desk `workSurfaceAnchorPx`가 키보드가 아니라 바닥에 있다 — **데이터 수정**

```text
현재  workSurfaceAnchorPx = (271.63, 91.73)   desk ground anchor 대비 +30.6px
실제  키보드 중심          ≈ (355, 233)        desk ground anchor 대비 +172px
```

`Artifacts/OfficeVisualCoherenceV4/03-current-main-anchor-debug.png`에서 이 앵커는 책상 앞다리 밑동
근처 바닥을 가리킨다. `operatorSeatSocketPx (390.45, 49.33)`도 마찬가지로 책상 스프라이트에서 독립
측정한 점이 아니라 의자 seat에 맞춰 역산된 바닥 점이다.

### 1.2 네 인물의 hand−pelvis 벡터가 전부 동일하다 — **데이터 수정(순환 검증)**

```text
authored  player / older_sister / father / mother = 모두 (-70, +25)
(-70,+25) × 1.69 = (-118.30, +42.25)
desk workSocket − chair seat (월드)      = (-118.82, +42.40)
차이 0.538px  ← QA가 통과시키던 수치
```

즉 네 profile은 해부학 측정값이 아니라 **책상 벡터 ÷ 전역 scale**이다.
PNG 실루엣에서 독립 측정한 값(승인 대기):

| member | seatContact(제안) | primaryHandContact(제안) | seat→hand |
|---|---|---|---|
| player | (148, 62) | (80, 101) | (−68, +39) |
| older_sister | (148, 76) | (75, 108) | (−73, +32) |
| father | (146, 63) | (80, 104) | (−66, +41) |
| mother | (143, 52) | (72, 88) | (−71, +36) |

x 성분은 실제로 −70 부근이 맞지만 y는 +25가 아니라 **+32~+41**이다. 현재 pelvis 앵커는 대체로
엉덩이보다 20px 정도 높고 오른쪽이라, 런타임에서 몸이 좌·하로 밀려 의자 앞에 걸터앉은 것처럼 보인다.
제안 앵커로 다시 합성한 결과가 `30-proposed-anatomy-four-workstations-scale*.png`이며, 네 명 모두
좌판 위에 앉고 등받이가 등 뒤로 온다.

### 1.3 책상 전경 overlay가 한 셀 떨어져 앉은 사람을 덮는다 — **데이터·규칙 수정**

`office_workstation_front_v4.png`는 앞다리 2개와 서랍 블록이다. 사람이 책상 밑으로 다리를 넣고 앉는
배치에서만 의미가 있는데, Starter Office의 의자는 책상보다 **정확히 한 셀 남쪽**이라 어떤 부분도
사람 앞에 올 수 없다. 현재는 앞다리가 상체를 가로지른다. 또한 이 마스크에는
`older_sister`를 피하려는 exclusion polygon이 들어가 있어 가구 마스크가 인물 ID에 의존한다.

### 1.4 `chairSeatError = 0f` / `pelvisSeatError = 0f` — **코드 수정**

`OfficeTycoonAlignmentCalibrationWindow`의 하드코딩 0. 지시서 §3.5와 동일하게 확인했다.

### 1.5 SafeStaticWork의 `frame = 0` 하드코딩 — **코드 수정**

`DirectionalSpriteAnimator.ApplyFrame`이 `appliedFrame = 0`을 강제한다. profile의 승인 프레임을 쓰도록
바꿔야 한다(§9).

---

## 2. 코드로 해결하면 안 되는 것 — 원본 판정

### 2.1 네 인물의 착석 포즈 자체는 **정상. 재생성 대상 아님**

`10~13-work-frame-contact-sheet-*.png` 육안 확인 결과 네 명 모두 허벅지 수평·무릎 굽힘·손 전방의
실제 착석 자세다. 지시서 §1.1의 "서 있는 자세" 추정은 원본 아트가 아니라 **합성 결과**의 문제였다.

### 2.2 손이 키보드에 닿는 것은 현재 워크스테이션 기하에서 **불가능** — 판정 필요

강체 스프라이트는 평행이동 + 균일 scale만 허용하므로, `seatContact→chair seat`와
`hand→keyboard`를 동시에 만족하려면 두 벡터의 **방향이 같아야** 한다.

```text
chair seat → 실제 키보드 (월드)  = (-35.4, +183.7)  len 187.1  angle 100.9°
캐릭터 seat → hand (스프라이트)  = (-66..-73, +32..+41)  angle 148~156°
```

| member | x를 맞추는 scale | y를 맞추는 scale | 판정 |
|---|---:|---:|---|
| player | 0.52 | 4.71 | IMPOSSIBLE |
| older_sister | 0.49 | 5.74 | IMPOSSIBLE |
| father | 0.54 | 4.48 | IMPOSSIBLE |
| mother | 0.50 | 5.10 | IMPOSSIBLE |

원인은 앵커가 아니라 **배치 기하**다. 의자가 책상보다 한 셀 남쪽이라 등각 깊이만으로 +80px이
올라가고, 거기에 책상 상판 높이(바닥 대비 약 161px)가 더해져 키보드가 좌판보다 약 184px 위에 있다.
사람이 앉은 채 손을 올릴 수 있는 높이는 좌판 대비 약 50~60px(scale 1.5 기준)이다.

따라서 `Docs`의 완료 수치 표에 있는 `primary hand ↔ keyboard/work socket ≤ 4px`는 다음 중 하나를
선택하기 전에는 달성할 수 없다.

- **A안 (아트 최소 변경)**: 판정 기준을 "손이 책상 근접 가장자리 높이에 온다"로 바꾸고 hand↔keyboard
  수치를 폐기한다. 코드·데이터만으로 끝난다. 현재 제안 앵커 + scale 1.5 합성이 이 상태다.
- **B안 (책상 1장 재생성)**: 키보드를 상판의 **남쪽(가까운) 가장자리**로 내려 그린 desk 스프라이트를
  만든다. 캐릭터 4명은 그대로 둔다. 재생성 1장으로 손↔키보드 오차가 크게 줄어든다.
- **C안 (캐릭터 4장 재생성)**: 착석 프레임의 전완을 책상 높이까지 올린 포즈로 다시 생성한다.
  좌판 대비 손 높이가 스프라이트 기준 +100px 이상 필요하며, 이는 상체 비율까지 바꾸는 큰 변경이다.

**권장: B안.** 재생성 1장이고 정체성 아트(인물)를 건드리지 않는다.

### 2.3 네 캐릭터의 물리 크기 체계가 서로 다르다 — **정규화 또는 재생성 필요**

| member | 착석 실루엣 높이(px) | 좌판 대비 앉은키(px) |
|---|---:|---:|
| player (14세) | 209 | 163 |
| older_sister (20세) | 222 | 169 |
| father (46세) | 218 | 163 |
| mother (44세) | 217 (frame 0만 228) | 206 |

네 사람이 사실상 같은 앉은키다. 하나의 uniform scale을 쓰면 14살 플레이어가 46살 아빠와 같은 크기로
보인다. 지시서가 허용한 해결은 **Import/정규화 단계에서 공통 물리 규격으로 스프라이트를 맞추는 것**이며
런타임 per-member scale은 금지다. 정규화 비율(제안, 가구 자 2.2px/cm 기준):

```text
player       ×0.86   (앉은키 80cm)
older_sister ×0.91   (85cm)
father       ×1.00   (92cm)
mother       ×0.93   (86cm)
```

### 2.4 `mother_northwest_sit_work_0..5.png` — **REGENERATED 6/6**

```text
판정: REGENERATED (frame 0..5 전부)
원본 결함:
  - 6장 모두 256px 하단에서 청록 스커트가 직선으로 잘려 무릎·종아리·발이 없었다.
  - frame 0만 visible height 228px, frame 1~5는 217px로 약 5% 크게 시작했다.
최종 규격:
  - 6장 모두 256×256 RGBA, visible height 228px, hard alpha 0/255, 하단 여백 7px.
  - 무릎·종아리·갈색 사무화·발바닥 전체가 canvas 안에 있고 frame 0 과대 크기는 제거됐다.
  - 피치 카디건·크림 블라우스·청록 스커트, 얼굴·머리와 Northwest 작업 자세를 유지했다.
  - Unity import는 기존 meta의 180 PPU·Point·mipmap 없음·bottom-center pivot을 그대로 사용한다.
재측정:
  - frame 0 승인 seat registration = (131,62), handContact = (90,120), source SHA-256 =
    1F8D8A299555DD50A8ACE551B8627141CFD1C017DFD0B01FE01D57B559E54FF7.
  - 자동 해부학 후보 `(149,75)`는 실제 의자 합성에서 몸을 좌판 밖으로 밀어내므로 폐기했다.
    `(131,62)`는 desk 벡터 역산값이 아니라 실제 chair sprite의 좌판과 등받이에 맞춘 승인 등록점이다.
  - 6장 자동 seat candidate 편차는 x 1px·y 6px 이내다.
  - 실제 플레이어에서 승인 등록점과 의자 cushion anchor의 seatContact 오차를 0.000px로 검증했다.
```

다른 세 인물의 `*_northwest_sit_work_0..5.png`는 교체하지 않았다.

---

## 3. 요약 판정표

| 원인 | 코드 | 데이터 | 원본 PNG | 판정 |
|---|---|---|---|---|
| global scale 1.69 (화면 점유율 기준) | ○ | ○ | 정규화 필요 | 캐릭터 4종 공통 규격 정규화 후 단일 scale |
| seat contact 앵커 | | ○ | | 완료 · frame 0 승인 등록점 `(131,62)` |
| hand contact 앵커 | | ○ | | 완료 · frame 0 승인 접점 `(90,120)` |
| desk work socket | | ○ | B안 시 desk 1장 | 데이터 수정, 필요 시 책상 재생성 |
| desk front mask | ○ | ○ | | 완료 · 얼굴 미침범/하체 전경 overlap 검증 |
| chair front layer | ○ | ○ | chair front 1장 | 완료 · 착석 전 구간에서 인물 위 전경으로 사용 |
| mother work frame 0..5 | | ○ | ○ | REGENERATED 6/6 · 좌판 접점/SHA 갱신 |
| 착석 포즈 자체 | | | 유지 | 재생성 불필요 |

---

## 4. 다음 순서

1. 완료: 좌판 등록점 배치, chair base < occupant < chair front, 공통 scale `1.55` 적용.
2. 완료: 엄마 Northwest Work 0..5 하체 복원, frame 0 앵커·SHA 갱신.
3. 완료: Unity 6000.3.21f1 전체 검증 및 Windows 플레이어 Main Flow QA.
4. 완료: v5 catalog에 네 가족 `Northwest` SitDown 4 + Work 6 + StandUp 4, 총 56개 pelvis/hand/SHA를
   승인하고 Starter Runtime Animated를 열었다. 렌더 틱당 한 프레임만 적용해 배속에서도 프레임을
   건너뛰지 않으며, 숨김 Windows 플레이어에서 전원 4/4·6/6·4/4와 anchor error `0.000px`를 통과했다.
