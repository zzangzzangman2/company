# CHARACTER LOCOMOTION GENERATION V1

계약 ID: `FC-CHARACTER-LOCOMOTION-GENERATION-V1`

리그 ID: `FC-FAMILY-LOCOMOTION-RIG-V1`

발 좌표 ID: `FC-FAMILY-LOCOMOTION-FOOT-ANCHORS-V1`

이미지 QA ID: `FC-CHARACTER-LOCOMOTION-FOOT-LOCK-QA-V1`

Unity/Player QA ID: `CHARACTER_LOCOMOTION_GENERATION_V1_UNITY`,
`FC-CHARACTER-LOCOMOTION-PLAYER-QA-V1`

이 문서는 가족회사 캐릭터 보행의 원화, 공용 리그, 8방향/6위상, Unity 런타임 결합, 실패 판정과
수정 절차의 정본이다. 2026-08-18 출하 대상은 가족 4명뿐이다. 직원 후보 8명은 가족 4명의 실제 D3D11
Player 보행이 사람 눈으로 승인된 뒤 같은 리그 계약을 retarget한다. 오래된 문서의 full-body donor,
upper-body byte lock, strict coherence PASS와 충돌하면 현재 코드·실제 PNG·Unity import·Player 캡처
순으로 판정한다.

## 1. 게임 정체성과 보행 목표

가족회사는 2000-01-03 한국풍 가상 도시에서 14살 플레이어와 누나·아빠·엄마가 빈 13×13 사무실에서
IT/SI·웹 외주 회사를 시작하는 싱글플레이 생활 경영 RPG/회사 타이쿤이다. 가구와 워크스테이션을 사고,
계약·연구·주식·M&A, 가족 관계·피로·시간을 관리한다. 캐릭터는 전투 유닛이 아니라 문, 책상, 회의,
휴식, 퇴근 동선을 계속 오가는 사람이다.

작은 등각 화면에서도 다음이 동시에 읽혀야 한다.

- identity: 얼굴, 머리/모자, 체형, 의상과 가족 구분이 프레임 사이에서 바뀌지 않는다.
- heading: 실제 화면 변위와 머리·몸통·골반·무릎·양쪽 발끝이 같은 방향을 향한다.
- weight: 좌우 지지발이 한 반주기씩 교대하고 반대 발은 진행축으로 스윙·들림·착지한다.
- coherence: 상체와 하체가 한 골반 결합점을 공유한다. 허리 투명 틈, 폭이 다른 splice, 분리 파츠가
  보이면 실패다.
- foot lock: 월드 root가 이동하는 동안 지지발의 투영 화면 위치는 접지 구간에 남는다.

## 2. 현재 출하 범위와 정본 파일

캐릭터 ID는 `player`, `older_sister`, `father`, `mother`다. 각 캐릭터는 8방향×6위상=48개
256×256 RGBA PNG와 A/B 4×6 시트 두 장을 가진다. 총 192 PNG, 32루프, 8시트다.

- 런타임 프레임:
  `Assets/Art/Characters/<Character>/Pixel/HighMotion/Frames/<id>_<direction>_walk_<phase>.png`
- 런타임 시트: 같은 `HighMotion` 폴더의 `<id>_pixel_walk8dir6_{a,b}_v1.png`
- 방향별 정체성 상체 32장:
  `Tools/CharacterLocomotionIdentityV1/<id>/<id>_<direction>_identity_v1.png`
- 리그 raw/manifest: `ArtSources/FamilyLocomotionRigV1/`
- 공용 리그 writer: `Tools/build_family_locomotion_rig_v1.py`
- 단일 공개 진입점: `Tools/generate_character_locomotion_v1.py`
- 독립 QA/self-test: `Tools/verify_character_locomotion_v1.py`,
  `Tools/test_character_locomotion_v1.py`
- Unity Player용 발 좌표:
  `Assets/FamilyCompany/Content/Resources/HighMotion/FamilyLocomotionFootAnchorsV1.json`

`ArtSources/FamilyLocomotionRigV1/rig_manifest_v1.json`은 source SHA-256, runtime stride/PPU/scale,
phase ownership과 캐릭터 공용 프로필을 고정한다. raw 입력은 다음 다섯 장뿐이다.

| 파일 | SHA-256 |
| --- | --- |
| `player_east_rig_parts_raw_v1.png` | `AB11A69D51911B212D3DE8BB5D787CFB5C6346420DBD4B059F92CBB446F8748B` |
| `player_other_directions_raw_v1.png` | `A4AE8E02A56589AB4AB16E4B8CFB9662DF7F5910CEC69170C8281BD32BA63883` |
| `older_sister_five_directions_raw_v1.png` | `A4EE4FB3C85F247721C2FF116B091D02FE8A603B01D6E706932A3C360FB09DFF` |
| `father_five_directions_raw_v1.png` | `01250857E9C897986DC04EA09E26AA0BD6E21F1D5EDDAFA75980E07881A82673` |
| `mother_five_directions_raw_v1.png` | `B835BF6F9DAF7F5025839D621530A2411C63DD87FF1CA6F325D1FE72FE813C73` |

ImageGen은 이 분리된 좌/우 허벅지·종아리/발 파츠와 방향별 형태를 만드는 데만 사용한다. 최종 6프레임의
발 좌표, 지지발 ownership, 보폭과 접지는 결정론적 코드가 소유한다. 완성 전신 6프레임을 모델에 맡기지
않는다.

파일 계약:

- 256×256 RGBA, alpha `{0,255}`, Point, PPU 180, bottom-center pivot `(0.5,0)`.
- A 시트 행은 `south, southwest, west, northwest`, B 시트 행은
  `north, northeast, east, southeast`다.
- Unity 배열은 phase-major, `index = phase * 8 + direction`이다.
- 기존 PNG `.meta`/GUID는 삭제·재생성하지 않고 바이트 해시를 전후 비교한다.
- 프레임 alpha는 하나의 연결 실루엣이어야 한다. 모자/머리는 위와 좌우에 최소 4px 여백을 둔다.

## 3. 8방향 좌표계

Unity 화면/world 벡터는 y가 위로 증가하지만 PNG row는 y가 아래로 증가한다.

| index | token | Unity 화면 벡터 | PNG 진행 벡터 |
| ---: | --- | --- | --- |
| 0 | south | `(0,-1)` | `(0,+1)` |
| 1 | southwest | `(-1,-1)` | `(-√½,+√½)` |
| 2 | west | `(-1,0)` | `(-1,0)` |
| 3 | northwest | `(-1,+1)` | `(-√½,-√½)` |
| 4 | north | `(0,+1)` | `(0,-1)` |
| 5 | northeast | `(+1,+1)` | `(+√½,-√½)` |
| 6 | east | `(+1,0)` | `(+1,0)` |
| 7 | southeast | `(+1,-1)` | `(+√½,+√½)` |

manifest remap은 가족 네 명 모두 0→0 … 7→7이며 Player에서 `flipX=false`다. 공용 생성의 canonical
방향은 `south, north, east, southeast, northeast`다. `west, southwest, northwest`는 각각
`east, southeast, northeast`의 **프레임 전체** 수평 반전이다. 머리만 또는 발만 반전하지 않는다.

한 프레임의 방향 승인은 코/시선, 머리 회전, 흉곽과 옷의 앞뒤, 골반, 두 무릎·발목, 양쪽 신발의
뒤축→앞코가 모두 같은 진행축을 가리킬 때만 성립한다. 뒤에 있는 발도 위치만 뒤일 뿐 앞코가 반대나
정면으로 돌아가면 실패다. 사용자가 지적한 “고개는 뒤인데 발은 앞”은 파일명이나 얼굴 방향 PASS로
덮을 수 없다.

## 4. 6프레임 위상, 접지와 스윙

해부학적 좌우 ownership을 색 marker 사본에 고정한다.

- left marker RGB: `(0,235,255)`
- right marker RGB: `(255,35,195)`
- marker는 QA 전용이며 출하 PNG에는 색을 넣지 않는다. marker와 출하 PNG의 alpha는 픽셀 단위로 같다.

| phase | ownership | 의미 |
| ---: | --- | --- |
| P0 | left support | 왼발 접촉, 오른발 toe-off 위치 |
| P1 | left support | 왼발 접지 유지, 오른발 passing/air, 상체·골반 공통 +1px down |
| P2 | left support | 왼발 접지 유지, 오른발 전방 swing/착지 준비 |
| P3 | right support | 오른발 접촉, 왼발 toe-off 위치 |
| P4 | right support | 오른발 접지 유지, 왼발 passing/air, 상체·골반 공통 +1px down |
| P5 | right support | 오른발 접지 유지, 왼발 전방 swing/착지 준비 |

P0~P2 지지발은 항상 left, P3~P5는 항상 right다. 검사기가 프레임마다 더 안정적으로 보이는 발을
K-means나 optical flow로 골라 PASS시키는 것을 금지한다. 같은 발을 두 반주기에 재사용하거나, 발끝·바짓단
몇 픽셀만 흔들거나, passing pose에 공중 위상이 없으면 실패다.

P1/P4의 1px 체중 이동은 상체만 또는 하체만 움직이지 않는다. canonical 상체 레이어, hip joint와 두 다리
결합점을 같은 `(0,+1)`로 평행 이동한다. 얼굴·모자·옷 픽셀을 재생성/warp하지 않으면서 upper-body
byte-identical freeze도 피한다. 별도 허리 cap이나 seam 은폐 레이어는 없다.

## 5. 런타임 stride와 foot-lock 수식

런타임은 실제 누적 이동거리로 frame을 선택한다.

```text
OfficeRuntimeAgent actual displacement
→ DirectionalSpriteAnimator.AccumulateTileMotion
→ OfficeSharedLocomotionRules.ResolveFrame
→ OfficeLocomotionGaitRules.DistanceFrame
→ ApplyFrame → SpriteRenderer
```

- `DefaultStrideLength = 0.99380799 world`
- `phasesPerCycle = 6`, `stepsPerCycle = 2`
- `pixelsPerUnit = 180`, `visualScale = 1.55`
- `DefaultMoveSpeed = 1.0 world/s`
- cadence = `1 / 0.99380799 × 2 = 2.0125 steps/s`

한 phase의 root 이동을 source pixel로 환산하면 다음과 같다.

```text
rootStepPixels
= (0.99380799 / 6) / (1.55 / 180)
= 19.234993 px
```

PNG 진행 단위벡터를 `v`, phase를 `p`, 해당 phase 지지발 local anchor를 `a[p]`라 하면 화면 투영
지지발은 다음 값이다.

```text
supportWorldPx[p] = a[p] + p × 19.234993 × v
```

P0~P2의 left `supportWorldPx`, P3~P5의 right `supportWorldPx` 진행축 투영 차이는 각각 최대 1px다.
따라서 local 지지발은 매 phase 진행축 반대로 약 19.235px 이동해야 한다. 좌우 착지 위치 P0→P3은
`3 × 19.234993 = 57.704980px`, world로는 `stride/2`다. 이 수식 없이 cadence만 맞으면 root와 함께
발이 끌려간다.

## 6. 공용 리그 생성 규칙

1. manifest raw SHA가 하나라도 다르면 생성 전에 실패한다.
2. 방향 atlas의 녹색 배경을 제거하고 큰 연결 component를 네 파츠로 분리한다.
3. 방향별 identity 상체에서 실제 옷 seam 아래의 기존 서 있는 다리만 corridor로 제거한다. 낮게 내려온
   손과 소매, 치마/반바지/바지 허리선은 보존한다.
4. 좌/우 허벅지와 종아리/발 파츠의 joint endpoint와 foot anchor를 계산한다.
5. phase profile의 left/right foot control로 hip→knee→foot를 배치한다. 허벅지/종아리는 hard-alpha로
   bake하고 canonical 상체를 마지막에 합성한다.
6. front/back은 화면 평면의 고정 길이 IK가 무릎을 옆으로 던지는 X자/활 모양을 만들므로 투영
   foreshortening midpoint를 쓴다. side/diagonal도 작은 진행축 knee bend만 허용한다.
7. 정면/후면은 두 해부학적 발이 겹쳐 한 발로 보이지 않도록 lateral stance를 둔다. player/sister/father는
   9px, 불투명 치마의 mother는 18px다. 엄마 값을 9px로 낮춰 치마 아래 한 발이 사라지거나, 30px로
   높여 과도한 팔자/X자 다리가 되면 실패다.
8. support marker를 다시 렌더해 진행축 projected drift를 반복 보정한다. 수직/횡 방향의 자연스러운
   원근 변화까지 0으로 만들지 않고 **진행축 미끄러짐만** 최대 1px로 제한한다.
9. canonical 5방향을 완성한 뒤 반대 3방향은 upper/lower/marker를 포함한 프레임 전체를 mirror한다.
10. 192 frame, 8 sheet, marker 사본, anchor catalog, 정량 JSON/CSV, 방향 contact sheet, fixed-grid world
    motion/contact GIF를 한 번에 생성한다.

캐릭터별 허용 프로필은 garment seam, leg corridor, hip 높이, 파츠 길이와 엄마 depth stance뿐이다.
캐릭터×방향×phase별 수작업 좌표는 두지 않는다. 직원 확장은 이 프로필을 retarget하는 방식으로만 한다.

## 7. 재현 절차

정본 `main`에서 다음을 실행한다.

```powershell
git status --short --branch
git rev-parse HEAD
git fetch origin main
git rev-parse origin/main
git pull --ff-only origin main

# 후보 생성. runtime은 아직 쓰지 않는다.
py -3 Tools/generate_character_locomotion_v1.py

# 후보 4×8×6과 음성 회귀 검사.
py -3 Tools/verify_character_locomotion_v1.py `
  --candidate-root Artifacts/CharacterLocomotionGenerationV1/Candidate `
  --output Artifacts/CharacterLocomotionGenerationV1/CandidateQa
py -3 Tools/test_character_locomotion_v1.py

# Evidence contact/GIF를 사람이 확인한 뒤에만 게시한다.
py -3 Tools/generate_character_locomotion_v1.py --publish-existing

# 실제 runtime PNG/시트를 다시 검사한다.
py -3 Tools/verify_character_locomotion_v1.py `
  --output Artifacts/CharacterLocomotionGenerationV1/RuntimeQa
```

게시 전후 네 캐릭터 HighMotion 아래 모든 `.meta` SHA-256 목록을 비교해 0 diff여야 한다.

## 8. 이미지/manifest fail-closed QA

검사는 candidate 또는 실제 runtime PNG를 다시 열고 marker와 anchor catalog를 독립 계산한다.

| 지표 | PASS 기준 | 2026-08-18 가족 4명 실측 |
| --- | ---: | ---: |
| 범위 | 4명×8방향×6 = 192 | 192/192 |
| explicit support ownership | P0~2 left, P3~5 right | 32/32 |
| projected support drift | 각 반주기 ≤ 1.0px | 최대 0.726448px |
| adjacent counter-motion error | ≤ 1.25px | PASS |
| alternating contact step | 57.704980 ± 1px | 57.669070~57.794742px |
| swing world travel | 좌/우 각각 ≥ 80px | 최소 86.316402px |
| passing air lift | 좌/우 각각 ≥ 2.5px | 최소 3.234033px |
| head/hat top margin | ≥ 4px | PASS |
| marker/candidate alpha | exact | 192/192 |
| direction mirrors | 전체 RGBA exact | 72/72 derived frames |
| detached alpha | 0px | 0px |
| sheet tile vs frame | exact | 192/192 |
| raw source SHA, frame RGBA SHA | exact | PASS |

셀프테스트는 최소 다음 음성 대조군을 FAIL시킨다.

- 여섯 프레임의 두 발이 정지: support world drift `38.470px`로 FAIL.
- 후반도 같은 지지발을 재사용: contact step error `32.407px`로 FAIL.
- 발은 이동하지만 passing lift 0: air phase로 FAIL.
- P0~P5가 모두 left support: explicit ownership으로 FAIL.
- 모자 상단 절단: marker/candidate alpha 및 identity로 FAIL.

발 움직임이 거의 0인데 프레임 byte uniqueness, upper-body identity 또는 loop closure만으로 PASS하는 경로는
없다. marker 색을 제거한 출하 PNG만 보고 “가장 그럴듯한 발”을 사후 선택하지 않는다.

## 9. Unity Editor와 Windows D3D11 Player QA

Unity Editor는 창 없이 실행한다.

```powershell
Unity.exe -batchmode -nographics -quit -projectPath <repo> `
  -executeMethod FamilyCompany.Editor.CharacterLocomotionGenerationV1Validation.RunBatch `
  -logFile <artifact-log>

FAST_QA_WINDOWS.cmd -Profile editor-broad
```

Editor gate는 실제 import된 192 Sprite의 이름, phase-major 순서, direction remap, PPU 180, bottom-center
pivot, 32행 foot-anchor Resources 계약, root step 19.234993px와 cadence 2.0125 steps/s를 확인한다.

Player QA는 정상 새 게임의 실제 `OfficeRuntimeAgent`, navigation, `DirectionalSpriteAnimator`, 최종
`SpriteRenderer`를 사용한다. 각 phase의 support pixel anchor를 sprite pivot/PPU/renderer transform으로
world에 투영하고 P0~P2, P3~P5의 진행축 drift를 source pixel로 다시 환산한다. 빌드/런타임 샘플링 오차를
포함한 hard limit은 4px이며, 정지 PNG의 약 38.47px/phase 미끄러짐은 통과할 수 없다. 좌우 contact world
간격은 `stride/2 ± 0.05 world`, cycle은 `0.99380799 ± 0.08`, cadence는 1.85~2.15 steps/s여야 한다.

```powershell
FamilyCompany.exe -force-d3d11 -screen-fullscreen 0 -screen-width 1392 -screen-height 699 `
  -familyCompanyCharacterLocomotionV1Qa `
  -familyCompanyCharacterLocomotionV1QaArtifacts <artifact-directory> `
  -logFile <player-log>
```

필수 증거:

- 가족 4명×8방향 실제 전체 화면 overview와 방향별 closeup.
- 6위상 contact sheet, fixed-grid world-motion GIF, support marker trace GIF.
- Player CSV의 sprite/direction/phase/root displacement/support world 좌표.
- Player 결과의 D3D11, 32 loops, support drift, contact step, cadence.
- 실제 배포 `C:\Users\godho\Downloads\Family\BUILD_INFO.txt`의 commit SHA가 정본 HEAD와 같고
  `WorkingTreeDirty=False`임을 확인한다.

수치 PASS는 시각 승인을 대체하지 않는다. 모자 전체, 허리/치마/반바지 seam, 정면/후면 두 발 가시성,
side/diagonal 발끝 방향과 한 발만 미끄러지는 느낌을 사람이 확대 확인한다.

## 10. 확인된 과거 실패 원인과 금지된 수정

이번에 실제 PNG와 runtime 수식으로 확인한 원인은 다음과 같다.

1. `split_high_motion_sheets.py::extract_aligned_frames`가 매 프레임 상체 median X를 128로 독립 재정렬했다.
   이 과정이 지지발의 root-relative 역이동을 삭제했다. `build_mother_side_walk_v3.py`의 개체별 중앙 정렬도
   같은 결함을 가진다.
2. 기존 gate는 adjacent foot motion 1px, 바닥선과 upper identity를 주로 보았고 월드 투영 지지발을
   계산하지 않았다. 1~6px 흔들림도 PASS했지만 필요한 값은 phase당 19.235px였다.
3. best-case K-means support 선택은 프레임마다 다른 발을 선택해도 된다. 실제 고정된 left/right ownership으로
   재측정하자 구 루프 32개 전부 반주기 drift 26.260~40.138px로 실패했다.
4. 구 Player QA는 cycle world distance와 cadence만 검사했다. root가 정상이어도 발 그림이 같이 끌리는
   오류를 검출하지 못했다.
5. full-body ImageGen 6패널은 정확한 anchor/시간 순서를 반복해서 무시하고 같은 큰 보폭을 복제했다.
6. whole-body warp는 치마/다리를 늘였고, marker full-leg warp와 2-bone 추정은 허리 seam, 블록형 관절과
   다리 교차를 만들었다.
7. 이미 잘못 중앙 정렬된 PNG에서 pivot만 고정하면 발은 붙지만 몸통이 41.5~73.4px 점프했다.
8. 화면 평면 fixed-length IK는 front/back 무릎을 옆으로 던져 X자/활 모양 다리를 만들었다.
9. 얼굴 방향만 맞추고 발 band를 반전한 행은 “고개는 뒤, 발은 앞”이 되었다. 방향은 전신 축으로 본다.
10. 허리에서 상체와 하체를 별도 offset/width로 합성해 투명 틈이나 직사각 cap이 생겼고, closeup framing
    1.15는 실제 PNG가 온전해도 모자 상단을 잘라 보였다.
11. 엄마의 front/back stance가 좁으면 불투명 치마 아래 한 발이 사라져 한 발 보행처럼 보이고, 너무 넓으면
    X자/팔자 다리가 된다. 현재 공용 프로필 18px가 두 조건 사이의 정본이다.
12. upper-body exact-byte lock은 얼굴 흔들림은 막지만 고정 몸통 아래 다리만 흐르는 느낌을 강화했다.
    현재는 정체성 픽셀을 변형하지 않고 P1/P4에 upper+hip을 함께 1px 이동한다.

다음 수정은 금지한다.

- 발 excursion threshold를 후보에 맞춰 낮추기.
- 전역 VisualRoot smoothstep/foot-lock 보정으로 PNG 결함을 숨기기.
- 상체와 하체를 서로 다른 좌표로 이동하거나 허리 cap으로 가리기.
- 192장의 완성 전신 프레임을 ImageGen에 다시 맡기기.
- 한 캐릭터/방향/phase별 수작업 숫자를 추가하기.
- 가족 4명 Player 시각 승인 전에 직원 8명까지 복제하기.
- 이전 EXE나 dirty build의 캡처를 새 결과로 보고하기.

## 11. 실패 시 수정 순서

1. candidate frame/marker → runtime frame/sheet → Unity import → Player SpriteRenderer 순으로 처음 달라지는
   지점을 찾는다.
2. 한 발 미끄러짐은 explicit support ownership과 `a[p] + p×rootStep×v`부터 계산한다. 정지 PNG나
   best-case 발 선택으로 우회하지 않는다.
3. 발 스윙 부족은 swing world travel, P1/P4 passing lift와 P0/P3 contact step을 함께 본다. 바짓단이나
   발끝 몇 픽셀만 흔들지 않는다.
4. 허리 찢어짐은 garment seam corridor, canonical upper, hip/body 공통 offset과 connected alpha를 확인한다.
   별도 cap이나 전역 warp를 추가하지 않는다.
5. 모자/머리 잘림은 실제 PNG alpha margin과 Player 캡처 camera 1.35 framing을 각각 구분한다.
6. 머리/발 방향 불일치는 frame 전체 mirror와 8방향 index를 확인하고, 얼굴만 또는 발 band만 반전하지
   않는다.
7. 엄마 한 발 가림은 depth stance 18px와 marker 양발 픽셀을 확인한다. 무조건 넓히지 않는다.
8. PNG는 정상이지만 Player가 미끄러지면 sprite name/order, pivot/PPU/scale, actual displacement,
   cadence, support world trace, BUILD_INFO SHA를 순서대로 확인한다.
9. 수정 뒤 candidate QA, 음성 self-test, runtime QA, `.meta` hash, Editor batch/broad, clean Release build,
   배포본 D3D11 Player와 사람 시각 확인을 처음부터 다시 실행한다.

QA가 실제로 PASS하고 캡처를 사람이 확인하기 전에는 완료라고 기록하지 않는다.
