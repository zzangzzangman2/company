# CHARACTER LOCOMOTION GENERATION V1

계약 ID: `FC-CHARACTER-LOCOMOTION-GENERATION-V1`

정량 게이트 ID: `FC-CHARACTER-LOCOMOTION-QA-V1`

Unity 소비자 게이트 ID: `CHARACTER_LOCOMOTION_GENERATION_V1_UNITY`

Windows Player 게이트 ID: `FC-CHARACTER-LOCOMOTION-PLAYER-QA-V1`

이 문서는 가족회사 캐릭터 보행 PNG의 생성, Unity import, 실제 이동 결합, 검증과 실패 수정 절차의
공용 정본이다. 2026-08-18 현재 출하 범위는 가족 4명뿐이다. 직원 후보 8명은 가족 4명이 실제 게임에서
사람 눈으로 승인된 뒤 같은 규칙으로 확장하며, 현재 writer/gate의 PASS 수에 포함하지 않는다. 오래된
문서의 12명 PASS, upper-body 고정, lower-body cutout 합성 설명과 충돌하면 이 문서와 현재 코드·실제
PNG·Unity import·D3D11 Player 순으로 판정한다.

## 1. 게임 정체성과 보행의 역할

가족회사는 2000-01-03 한국풍 가상 도시에서 시작하는 싱글플레이 생활 경영 RPG/회사 타이쿤이다.
14살 플레이어와 누나·아빠·엄마가 빈 13×13 사무실에서 작은 IT/SI·웹 외주 회사를 시작해 가구와
워크스테이션을 사고, 계약·연구·주식·M&A와 가족 관계·피로·시간을 관리한다. 캐릭터는 전투 유닛이
아니라 문, 책상, 회의, 휴식, 퇴근 동선을 계속 오가는 사람이다.

따라서 작은 화면에서도 다음 세 항목이 동시에 읽혀야 한다.

- identity: 얼굴, 머리/모자, 체형, 의상, 나이와 가족 구분이 프레임 사이에 바뀌지 않는다.
- heading: 실제 화면 변위와 얼굴·몸·발이 가리키는 8방향이 일치한다.
- weight: 좌우 지지발이 교대하고 반대 발이 진행축으로 이동·들림·착지한다. 월드 이동과 cadence가 맞아
  두 발을 끌거나 컨베이어 위에서 미끄러지는 것처럼 보이지 않는다.

## 2. 현재 범위와 파일 정본

현재 캐릭터 ID는 `player`, `older_sister`, `father`, `mother` 네 개다. 각 캐릭터는
8방향×6위상=48개 256×256 RGBA PNG와 A/B 4×6 시트 두 장을 가진다. 총 출하 범위는 192 PNG,
32 보행 루프, 시트 8장이다.

- 프레임: `Assets/Art/Characters/<Character>/Pixel/HighMotion/Frames/<id>_<direction>_walk_<phase>.png`
- 시트: 같은 `HighMotion` 폴더의 `<id>_pixel_walk8dir6_{a,b}_v1.png`
- full-body motion 원본: `Assets/Art/Characters/BeforeCoherenceV1/<id>/`
- 방향별 identity anchor 32장: `Tools/CharacterLocomotionIdentityV1/<id>/<id>_<direction>_identity_v1.png`
- 공용 프로필: `Tools/character_locomotion_profiles_v1.json`
- writer: `Tools/generate_character_locomotion_v1.py`
- fail-closed gate/self-test: `Tools/verify_character_locomotion_v1.py`,
  `Tools/test_character_locomotion_v1.py`

identity anchor는 허리가 이미 잘린 현재 runtime P0를 다시 입력으로 삼지 않는다. 결함 도입 전 승인
revision `9144fa0e`의 4명×8방향 P0에서 한 번 추출한 독립 입력이며 저장소에 추적한다. 생성 결과를 다음
생성 입력으로 재사용하지 않는다.

파일 계약:

- PNG만 게시한다. 기존 `.meta`와 GUID를 보존하며 삭제·재생성하지 않는다.
- 256×256 RGBA, alpha `{0,255}`, Point, PPU 180, bottom-center pivot을 유지한다.
- 실제 몸 실루엣 최저점은 모든 프레임에서 `y=247`이다. `y>=248`은 비운다.
- A 시트 행은 `south, southwest, west, northwest`, B 시트 행은
  `north, northeast, east, southeast`다.
- Unity 배열은 phase-major, `index = phase * 8 + direction`이다.

## 3. 화면 8방향 좌표계

방향 토큰은 이미 등각 투영된 실제 화면 이동 벡터를 뜻한다. `flipX=false`이며 반대 행을 미러링해
대신하지 않는다.

| index | 토큰 | 화면 벡터 `(x,y)` | 표시 방향 |
| ---: | --- | --- | --- |
| 0 | south | `(0,-1)` | 아래 |
| 1 | southwest | `(-1,-1)` 정규화 | 왼쪽 아래 |
| 2 | west | `(-1,0)` | 왼쪽 |
| 3 | northwest | `(-1,1)` 정규화 | 왼쪽 위 |
| 4 | north | `(0,1)` | 위 |
| 5 | northeast | `(1,1)` 정규화 | 오른쪽 위 |
| 6 | east | `(1,0)` | 오른쪽 |
| 7 | southeast | `(1,-1)` 정규화 | 오른쪽 아래 |

`OfficeGridTilemapPresenter.DefaultWorldVectorToVisualFacingAxes`가 화면축 변위를 반환하고
`DirectionalSpriteAnimator.ResolveDirectionFromAxes`가 옥탄트로 양자화한다. 오른쪽 실제 이동은
반드시 `east(6)`, 왼쪽 실제 이동은 `west(2)`를 소비한다. manifest remap은 네 캐릭터 모두 0→0 … 7→7이다.

## 4. 6프레임 보행 위상과 원화 규칙

한 루프는 좌우 접촉이 한 번씩 나타나는 두 걸음이다. 원화는 발끝 몇 픽셀이나 바짓단만 흔드는 방식이
아니라 골반부터 발까지 연결된 온몸 포즈여야 한다.

| 위상 | 의미 | 접지/스윙 규칙 |
| ---: | --- | --- |
| P0 | 접촉 A | A 지지발이 바닥에 닿고 두 발의 진행축 간격이 읽힌다. |
| P1 | A 지지, B 초기 스윙 | A발은 바닥에 남고 B 하퇴·발이 진행축으로 회수된다. 최소 하중 이동을 허용한다. |
| P2 | B 통과/공중 | A발은 접지하고 B발은 실제 픽셀 중심과 실루엣이 이동하며 바닥에서 들린다. |
| P3 | 접촉 B | P0와 반대 발이 접지하며 접촉 서명이 바뀐다. |
| P4 | B 지지, A 초기 스윙 | B발은 바닥에 남고 A 하퇴·발이 진행축으로 회수된다. 최소 하중 이동을 허용한다. |
| P5 | A 통과/공중 | B발은 접지하고 A발은 실제 픽셀 중심과 실루엣이 이동하며 바닥에서 들린다. |

공통 금지 사항:

- P0/P1/P2 또는 P3/P4/P5를 같은 포즈로 복제하지 않는다.
- 발끝, 바짓단, 치맛단만 흔들고 발 excursion이 거의 0인 루프를 허용하지 않는다.
- 지지발과 스윙발이 함께 뜨거나, 같은 화면측 발만 두 번 들거나, 진행축 반대로 스윙하지 않는다.
- 허리선을 지우고 상체와 하체를 서로 다른 폭으로 덮는 cutout/splice를 하지 않는다.
- 접합부를 가리기 위한 직사각형 골반 cap, 별도 바닥 그림자, 분리된 발 픽셀을 추가하지 않는다.
- X자 다리, 과한 런지·행진·달리기 높이, 얼굴 왜곡, 과도한 상체 bob을 만들지 않는다.

## 5. identity와 온몸 coherence

`BeforeCoherenceV1`의 승인 6포즈가 프레임별 **온몸 authority**다. 머리부터 발까지 연결된 donor 포즈를
그대로 사용하므로 허리에서 상·하체를 자르지 않는다. `lowerBodyStart`는 생성 cut 위치가 아니라 QA의
발/상체/허리 참조 구역을 계산하는 프로필 값이다.

플레이어와 엄마는 포즈 donor의 머리 세대 차이 때문에 방향별 안정 anchor의 머리 영역만 겹친다.
플레이어는 전신 높이의 48%, 엄마는 46% 지점까지 사용하고 10px를 아래로 겹쳐 목에 투명 틈을 남기지
않는다. P1/P4에서는 anchor 전체를 변형하지 않고 1px 아래로 옮겨 최소 하중 이동을 허용한다. 누나와
아빠는 full-body donor 자체가 identity authority이므로 별도 layer 합성을 하지 않는다.

플레이어의 빨간 뉴스보이 캡, 누나의 긴 양갈래·리본, 아빠의 머리·안경, 엄마의 머리와 얼굴은 방향과
프레임 사이에서 잘리거나 바뀌면 안 된다. 모든 프레임은 머리 위 투명 여백을 최소 4px 확보한다.

donor의 발 아래에 분리된 1~3px 높이의 오래된 그림자 streak가 있으면 발로 취급하지 않는다. 생성기는
폭 3px 이상, 면적 80px 이하인 바닥 근처 분리 component를 **ground 정렬 전에** 제거한다. 순서를
뒤집으면 가짜 그림자를 y=247로 맞추느라 실제 몸이 위로 밀려 모자·머리가 잘리고 발이 뜨므로 실패다.
그 뒤 면적 6px 미만 고립 component를 제거하고 실제 몸의 최저점을 y=247로 정렬한다.

## 6. 공용 생성과 재현 절차

정본 `main`의 clean 시작점을 확인한 뒤 실행한다.

```powershell
git status --short --branch
git rev-parse HEAD
git rev-parse origin/main
git pull --ff-only origin main

# 비파괴 후보 생성: runtime PNG를 쓰지 않는다.
py -3 Tools/generate_character_locomotion_v1.py

# 후보의 4명×8방향 32루프/192프레임 정량 검증.
py -3 Tools/verify_character_locomotion_v1.py `
  --candidate-root Artifacts/CharacterLocomotionGenerationV1/Candidate `
  --output Artifacts/CharacterLocomotionGenerationV1/CandidateQa

# 양성 1 + 음성 6 회귀 검증.
py -3 Tools/test_character_locomotion_v1.py

# Evidence의 방향별 contact sheet를 눈으로 확인한 후보만 게시한다.
py -3 Tools/generate_character_locomotion_v1.py --publish-existing

# 게시된 실제 runtime PNG를 다시 검증한다.
py -3 Tools/verify_character_locomotion_v1.py `
  --output Artifacts/CharacterLocomotionGenerationV1/RuntimeQa
```

생성기는 네 캐릭터 프로필을 반복하고, 8방향 donor sheet에서 6개의 온몸 프레임을 추출한다. 분리 그림자와
미세 island를 제거하고 ground를 정규화한 뒤, 프로필에 `identityHeadFraction`이 있는 캐릭터만 머리 anchor를
겹친다. 각 출력은 hard alpha, 서로 다른 6포즈, y=247을 생성 중 단언한다. candidate와 게시 경로의 192
PNG byte 일치까지 확인한다. 캐릭터·방향·위상별 수작업 좌표 예외는 두지 않는다.

## 7. 이미지/manifest fail-closed QA

`verify_character_locomotion_v1.py`는 후보나 실제 runtime PNG를 다시 열어 32루프를 독립 측정한다.
기존 strict coherence의 `footDrift=0`은 바닥선만 고정됐다는 뜻이며 발이 움직였다는 증거가 아니다.
현재 PASS는 아래 항목을 **모두** 요구한다.

| 지표 | 허용값 | 실패 의미 |
| --- | ---: | --- |
| P0/P3 두 발 군집 간격 | 각각 ≥ 7.0px | 접촉 포즈에서 두 발이 읽히지 않음 |
| P0↔P3 접촉 교대 optical excursion | ≥ 1.0px | 좌우 접촉 서명이 사실상 같음 |
| P0→P2, P3→P5 반주기 excursion | 각각 ≥ 1.0px | 실제 스윙이 없음 |
| P0→P1→P2, P3→P4→P5 인접 excursion | 모두 ≥ 1.0px | 정지 프레임 뒤 순간이동 |
| 인접 발 영역 RGBA 변화율 | 모두 ≥ 0.50 | 바지/발끝 일부만 흔듦 |
| 인접 발 alpha 실루엣 변화율 | 모두 ≥ 0.05 | 색만 바꾼 가짜 움직임 |
| P2/P5 수직 optical lift | 각각 ≥ 0.15px | 공중 위상 들림이 없음 |
| P2/P5 바닥선 support 픽셀 | 각각 ≥ 1 | 지지발도 함께 뜸 |
| 상체/몸 authored 변화율 | ≥ 0.10 | 하체와 무관하게 상체가 완전히 얼어 있음 |
| 정렬 허용 머리 실루엣 IoU | ≥ 0.78 | 얼굴/머리/모자 형태가 흔들림 |
| 머리 위 투명 여백 | ≥ 4px | 모자·머리카락 잘림 |
| 머리 최상단 excursion | ≤ 6px | 과한 bob 또는 위치 점프 |
| full donor 대비 허리 band mismatch | ≤ 0.01 | 상·하체 seam 변형/투명 절단 |
| profile identity-head mismatch | = 0 | 플레이어/엄마 머리 anchor 손상 |

optical flow와 변화율은 발/하퇴 corridor에서 측정한다. 한 지표, loop closure, upper identity만으로
PASS하지 않는다. 셀프테스트는 승인 후보와 다음 여섯 음성 대조군을 매번 실행한다.

1. 여섯 장 완전 정지
2. 발은 고정하고 바지만 흔듦
3. 접촉 포즈를 공중 위상으로 재사용
4. 발만 움직이고 상체는 byte-identical
5. 허리에 4px 투명 절단
6. 모든 프레임의 모자/머리를 같은 높이로 잘라냄

이 중 하나라도 PASS하면 gate 자체가 실패다. 임계값을 새 후보에 맞춰 낮추지 말고 positive/negative
fixture와 실제 contact sheet를 함께 고친다.

## 8. Unity import와 이동 속도/cadence 결합

런타임 경로는 다음과 같다.

`OfficeRuntimeAgent` 실제 변위 → `DirectionalSpriteAnimator.AccumulateTileMotion` →
`OfficeSharedLocomotionRules.ResolveFrame` → `OfficeLocomotionGaitRules.DistanceFrame` →
`ApplyFrame` → 최종 `SpriteRenderer`.

`OfficeLocomotionGaitRules.DefaultStrideLength = 0.99380799 world`는 2:1 등각 타일 중심 간 거리다.
한 6프레임 루프가 한 stride/두 걸음이고 `DefaultMoveSpeed=1.0 world/s`에서 이론 cadence는
`1 / 0.99380799 * 2 = 2.0125 steps/s`다. 실제 누적 이동거리로 phase를 고르므로 충돌로 멈추면 발도
멈추고 한 타일마다 같은 위상으로 닫힌다. `VisualRoot`에 별도 미끄럼 보정 offset을 더하지 않는다.

Unity 6000.3.21f1은 화면을 띄우지 않는 batchmode로 다음을 검증한다.

```powershell
Unity.exe -batchmode -nographics -quit -projectPath <repo> `
  -executeMethod FamilyCompany.Editor.CharacterLocomotionGenerationV1Validation.RunBatch `
  -logFile <artifact-log>

FAST_QA_WINDOWS.cmd -Profile editor-broad
```

첫 게이트는 가족 4×8×6=192 Sprite, catalog phase-major 순서, direction remap, stride/cadence를 검사한다.
broad 게이트는 실제 방향, pivot, 충돌, gait closure를 검사한다.

## 9. 실제 Windows D3D11 Player QA와 증거

최종 Release Windows x64 빌드를 `-force-d3d11`, 창 모드, 백그라운드로 실행한다.

```powershell
FamilyCompany.exe -force-d3d11 -screen-fullscreen 0 -screen-width 1392 -screen-height 699 `
  -familyCompanyCharacterLocomotionV1Qa `
  -familyCompanyCharacterLocomotionV1QaArtifacts <artifact-directory> `
  -logFile <player-log>
```

필수 PASS:

- `character-locomotion-player-final.txt`가 PASS이고 graphics가 `Direct3D11`이다.
- 정상 새 게임의 실제 `OfficeRuntimeAgent`, navigation, gait, 최종 `SpriteRenderer`를 사용한다.
- 가족 4명×8방향×6위상=192 sprite를 소비하고 32루프의 motion/display/sprite direction이 일치한다.
- `flip_x=false`, 기대 sprite 이름 일치, 5→0 wrap과 한 cycle world distance가
  `0.99380799 ± 0.08`이다.
- actual speed/cycle cadence가 1.85~2.15 steps/s, world step/body height가 0.18~0.70이다.
- 전체 화면 overview, 방향별 closeup, 6위상 contact sheet/GIF를 `Artifacts`에 남긴다.
- 플레이어의 빨간 모자 상단, 네 캐릭터 허리 연결, 엄마의 southwest를 포함해 실제 Player 캡처를 사람이
  확대 확인한다. 수치 PASS는 이 시각 검수를 대체하지 않는다.

릴리스는 `BUILD_WINDOWS.cmd`와 `DEPLOY_WINDOWS.cmd`로 `C:\Users\godho\Downloads\Family`에 배포한다.
배포 `BUILD_INFO.txt`의 commit이 정본 `main` HEAD와 같고 `WorkingTreeDirty=False`인 같은 파일을 다시
D3D11 검증해야 한다. 원화만 바꾸고 이전 EXE를 재사용한 결과는 무효다.

## 10. 실패 시 수정 순서

1. 정본 runtime PNG와 실제 Player 캡처 중 어느 쪽에서 처음 문제가 보이는지 분리한다.
2. 모자/머리가 잘리면 detached ground shadow 제거가 ground 정렬보다 먼저인지, top margin이 4px 이상인지
   확인한다. 캔버스를 임의 확대하거나 머리만 축소하지 않는다.
3. 허리가 찢어지면 waist band를 full donor 같은 위상의 band와 비교한다. lower-body clear/refill, pelvis
   cap, 폭이 다른 layer splice를 제거하고 온몸 donor 포즈로 돌아간다.
4. 발이 안 움직이면 P0↔P3 접촉 교대, P0→P2/P3→P5 excursion, 인접 변화, vertical lift와 support ground를
   실제 PNG에서 확인한다. threshold를 낮추거나 바짓단만 흔들지 않는다.
5. 방향이 틀리면 원화를 미러링하지 말고 manifest 0→0 … 7→7과 실제 화면 displacement를 추적한다.
6. PNG는 정상인데 Player가 미끄러지면 catalog sprite 이름/phase order, 실제 cycle world distance, speed,
   cadence, BUILD_INFO SHA를 확인한다.
7. 수정 뒤 candidate QA, self-test, runtime 재검증, Unity 두 게이트, clean Release build, 배포본 D3D11 Player
   QA와 사람 시각 확인을 처음부터 다시 수행한다.

완료는 가족 4명의 32루프/192 PNG와 Unity, 실제 배포 Player가 모두 PASS하고 모자·머리·허리·발을 눈으로
승인했을 때뿐이다. 직원 8명은 이 완료 뒤 같은 문서와 도구를 확장하는 다음 작업이다.
