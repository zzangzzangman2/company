# CHARACTER LOCOMOTION GENERATION V1

계약 ID: `FC-CHARACTER-LOCOMOTION-GENERATION-V1`

정량 게이트 ID: `FC-CHARACTER-LOCOMOTION-QA-V1`

Unity 소비자 게이트 ID: `CHARACTER_LOCOMOTION_GENERATION_V1_UNITY`

Windows Player 게이트 ID: `FC-CHARACTER-LOCOMOTION-PLAYER-QA-V1`

이 문서는 가족회사 전체 캐릭터의 보행 원화 생성, Unity import, 실제 이동 결합, 검증 및 실패 수정의
공용 정본이다. 가족 4명만 다루던 `FAMILY_WALK_ART_GUARDRAILS.md`와
`FamilyWalkHalfCyclesV2`는 제작 역사와 identity/marker 증거로 보존하지만 shipping walk PNG의 쓰기
권한은 이 문서와 `Tools/generate_character_locomotion_v1.py`가 가진다. 오래된 문서와 충돌하면 실제
코드·PNG·Unity import·정상 Windows Player 순으로 판정한다.

## 1. 프로젝트와 화면 역할

가족회사는 2000-01-03 한국풍 가상 도시에서 시작하는 싱글플레이 생활 경영 RPG/회사 타이쿤이다.
14살 플레이어와 누나·아빠·엄마가 빈 13×13 사무실에서 작은 IT/SI·웹 외주 회사를 시작한다. 가구와
워크스테이션을 사고, 계약·연구·주식·M&A와 가족 관계·피로·시간을 관리해 2000~2026 대체 산업사를
통과한다. 직원 8명은 현재 카탈로그에 들어 있는 향후 채용 후보다.

따라서 캐릭터는 전투 유닛이 아니라 타이쿤 보드 위에서 문→책상→회의→휴식→퇴근을 반복하는 사람이다.
보행은 다음 세 가지를 동시에 전달해야 한다.

- 누구인지: 얼굴, 머리, 체형, 의상, 연령과 가족/직원 구분이 한눈에 유지된다.
- 어디로 가는지: 화면 변위와 몸·발의 방향이 같은 8방향을 가리킨다.
- 바닥을 밟는지: 월드 이동량과 두 발의 접지·스윙 cadence가 맞아 컨베이어 벨트처럼 미끄러지지 않는다.

참조 영상은 복제 대상이 아니라 판독 기준이다. Mad Games Tycoon 2의 1659.042578~1659.409241초 실제
30fps 구간을 프레임 단위로 보면 작은 캐릭터도 지지발과 통과발 실루엣이 연속 프레임에서 구분되고,
상체가 안정된 채 진행축으로 다리가 이동한다. Big Biz Tycoon의 299.954658~300.321321초 책상 동선도
전신이 한 방향을 유지한 채 루트와 사지가 함께 진행한다. 이 프로젝트는 그 가독성을 따르되 현재 SIMUL
계열 캐릭터 identity와 픽셀 비율을 그대로 소유한다. 외부 영상 프레임은 분석 증거일 뿐 shipping asset이
아니다.

## 2. 범위와 파일 정본

캐릭터 12명은 아래 ID로 고정한다.

`player`, `older_sister`, `father`, `mother`, `kim_seoa`, `lee_jian`, `choi_iseo`,
`jung_arin`, `park_haeun`, `han_sua`, `oh_jiwoo`, `yoon_chaea`.

각 캐릭터는 8방향×6위상=48개 256×256 RGBA PNG와 A/B 4×6 시트 두 장을 가진다. 프레임 정본 경로는
`Assets/Art/Characters/<Character>/Pixel/HighMotion/Frames/<id>_<direction>_walk_<phase>.png`다.
직원은 `<Character>` 아래 `Employees/<Name>`을 사용한다. 시트 이름은
`<id>_pixel_walk8dir6_{a,b}_v1.png`다.

- PNG만 갱신한다. 기존 `.meta`와 GUID는 절대 재생성하거나 일괄 재직렬화하지 않는다.
- 256×256, hard alpha `{0,255}`, point filter, PPU 180, 하단 중앙 pivot 계약을 유지한다.
- 불투명 주 실루엣의 최저점은 모든 프레임에서 `y=247`이다. `y>=248` 픽셀은 생성 시 제거한다.
- A 시트 행: `south, southwest, west, northwest`.
- B 시트 행: `north, northeast, east, southeast`.
- 런타임 배열은 phase-major다: `index = phase * 8 + direction`.

## 3. 화면 8방향 좌표계

방향은 캐릭터가 실제 화면에서 이동하는 벡터다. `flipX=false`이며 반대 행을 미러링해 대신하지 않는다.

| index | 토큰 | 화면 벡터 `(x,y)` | 보이는 진행 |
| ---: | --- | --- | --- |
| 0 | south | `(0,-1)` | 아래 |
| 1 | southwest | `(-1,-1)` 정규화 | 왼쪽 아래 |
| 2 | west | `(-1,0)` | 왼쪽 |
| 3 | northwest | `(-1,1)` 정규화 | 왼쪽 위 |
| 4 | north | `(0,1)` | 위 |
| 5 | northeast | `(1,1)` 정규화 | 오른쪽 위 |
| 6 | east | `(1,0)` | 오른쪽 |
| 7 | southeast | `(1,-1)` 정규화 | 오른쪽 아래 |

`OfficeGridTilemapPresenter.DefaultWorldVectorToVisualFacingAxes`는 이미 투영된 월드/화면 벡터를 그대로
반환한다. `DirectionalSpriteAnimator.ResolveDirectionFromAxes`가 이를 옥탄트로 양자화한다. 오른쪽 실제
변위는 반드시 `east(6)` 행, 왼쪽은 `west(2)` 행을 소비한다. 방향 manifest의 source remap은 전 캐릭터
0→0 … 7→7이어야 한다.

## 4. 6프레임 두 걸음 위상

한 루프는 접지발이 두 번 바뀌는 두 걸음이다. P0/P3은 반대 접지 포즈이고 P2/P5는 반대 발의 낮은
통과/공중 위상이다.

| 위상 | 의미 | 지지발 | 반대 발 규칙 |
| ---: | --- | --- | --- |
| P0 | 접촉 A | A발 접지 | B발은 진행축 뒤쪽, 두 발 간격이 읽힘 |
| P1 | A 지지·B 초기 스윙 | A발 최저점 `y=247` | B발은 진행축으로 회수, 약 1px 낮은 들림 |
| P2 | B 통과 | A발 최저점 `y=247` | B발 전체 하퇴/발이 진행축으로 이동, 약 4px 들림 |
| P3 | 접촉 B | B발 접지 | A발은 진행축 뒤쪽, P0과 반대 접촉 서명 |
| P4 | B 지지·A 초기 스윙 | B발 최저점 `y=247` | A발은 진행축으로 회수, 약 1px 낮은 들림 |
| P5 | A 통과 | B발 최저점 `y=247` | A발 전체 하퇴/발이 진행축으로 이동, 약 4px 들림 |

금지 사항:

- P0/P1/P2 또는 P3/P4/P5를 같은 포즈로 복제하지 않는다.
- 발끝 몇 픽셀, 바짓단, 치마만 움직이고 발 중심·실루엣이 정지한 후보는 실패다.
- 두 통과 위상에서 같은 화면측 발만 들거나, 지지발이 바닥에서 함께 뜨면 실패다.
- 진행축 대신 무릎을 옆으로 접은 X자 다리, 런지, 행진, 달리기 높이, 과도한 상체 bob은 실패다.
- 접지발을 보이게 하려고 별도 그림자나 바닥 픽셀을 붙이지 않는다.

## 5. identity 보존과 허용 운동

각 방향의 현재 runtime P0가 얼굴·머리·상체 identity authority다. 생성기는 캐릭터 프로필의
`lowerBodyStart` 위쪽, 정확히 `seamY-2` 전 행을 redraw/deform하지 않는다. P0/P2/P3/P5는 승인 좌표,
P1/P4 support/down은 같은 픽셀을 통째로 1px 아래 옮긴 좌표다. 빨간 모자, 안경, 긴 머리, 리본, 상의
문양, 가방과 같은 현재 식별 표지를 다른 세대 그림으로 바꾸지 않는다.

하체 접촉 donor는 저장소에 보존된 `Assets/Art/Characters/BeforeCoherenceV1/<id>/` 4×6 시트다. 이
시트는 현재 identity 전체를 복원하는 용도가 아니라, 실제 보폭을 가진 하체 접촉 A/B의 기하만 제공한다.
상체를 donor로 교체하거나 캐릭터별 수작업 프레임을 만드는 것은 금지한다.

donor가 seam 아래에 있다는 이유만으로 leg가 되지는 않는다. 각 pose에서 불투명 component가 최저
34px의 신발/발 band에 연결돼야 하며, hip 전환대(`seamY-2..seamY+19`)에서는 방향별 P0 silhouette을
2px 확장한 범위와 겹쳐야 한다. 이 두 공용 조건이 늘어진 손·소매·머리카락을 하체로 오인해 중복
appendage를 만드는 것을 막는다. contact와 합성 passing frame 모두 같은 guard를 거치며 생성 중 누출 0을
단언한다.

허용되는 자연 운동:

- seam 아래 좁은 골반 봉합부는 접촉 donor로 닫아 투명 구멍을 막는다.
- 다리 전체가 접촉 A→B anchor로 보간되며 지지발은 바닥선에 고정된다.
- 1~4px의 낮은 통과발 들림과 진행축 이동을 허용한다.
- P1/P4에 공용 1px rigid-body drop을 넣어 접지발에 하중이 실리는 최소 체중 반응을 만든다.
- 얼굴·머리·옷의 픽셀 정체성은 정렬 비교로 고정하지만, 6프레임 상체가 byte-identical이면 실패다.

프로필 `Tools/character_locomotion_profiles_v1.json`에는 캐릭터별 `lowerBodyStart`와 발 corridor margin만
둔다. phase·direction별 수치나 픽셀 좌표를 넣지 않는다. 새로운 캐릭터도 동일 두 값만 정하고 공용 위상
곡선을 사용한다.

## 6. 공용 생성 절차

정본 저장소 main에서 다음 순서로 실행한다.

```powershell
git status --short --branch
git rev-parse HEAD
git rev-parse origin/main

# 1) 비파괴 후보: runtime PNG를 쓰지 않는다.
py -3 Tools/generate_character_locomotion_v1.py

# 2) 후보 12명×8방향 정량 검증.
py -3 Tools/verify_character_locomotion_v1.py `
  --candidate-root Artifacts/CharacterLocomotionGenerationV1/Candidate `
  --output Artifacts/CharacterLocomotionGenerationV1/CandidateQa

# 3) 정지/바지만/접촉반복/상체완전고정 음성 대조군.
py -3 Tools/test_character_locomotion_v1.py

# 4) 사람이 Evidence의 8개 contact sheet를 모두 확인한다.
# 5) PASS 후보만 기존 경로에 게시한다. .meta는 복사하지 않는다.
py -3 Tools/generate_character_locomotion_v1.py --publish-existing

# 6) 게시 경로를 다시 읽어 판정한다.
py -3 Tools/verify_character_locomotion_v1.py
py -3 Tools/measure_animation_coherence.py --motion walk --strict
```

생성기는 donor frame의 6개 포즈 중 P0/P3을 접촉 authority로 사용한다. 두 접촉의 하체를 화면 x 군집으로
두 leg layer로 분리하고, 아래 공용 곡선으로 통과 프레임을 만든다.

- P1/P4: 지지 진행 0.12, 스윙 진행 0.32, 들림 1px.
- P2/P5: 지지 진행 0.32, 스윙 진행 0.70, 들림 4px.
- P1/P4: 승인 상체 rigid body를 1px 아래로 이동. 나머지 위상은 0px.
- 첫 반주기 support index 0, 둘째 반주기 support index 1.
- 각 지지 layer 최저점을 `y=247`로 보정하고, 스윙 layer만 위로 든다.
- donor alpha 중 신발/발 band에 연결된 component만 leg로 인정하고, hip 전환대의 P0 silhouette 2px 밖
  픽셀은 contact와 passing 양쪽에서 거부한다.
- 6픽셀 미만 고립 component를 제거하되 identity 보호 행은 원본으로 되돌린다.
- 모든 출력은 hard alpha, 6개 byte-unique, 동일 바닥선, 위상별 허용 정렬 뒤 상체 byte identity를 생성
  중 단언한다.

## 7. 정량 이미지 QA

`verify_character_locomotion_v1.py`는 실제 후보/런타임 PNG를 다시 열어 각 96루프를 독립 판정한다.
기존 strict coherence의 `footDrift=0`은 실루엣 최저점이 안정됐다는 뜻일 뿐 발이 움직였다는 뜻이 아니다.
V1 게이트는 다음을 모두 요구한다.

| 지표 | 최소/최대 | 뜻 |
| --- | ---: | --- |
| P0/P3 두 발 군집 간격 | ≥ 7.0px | 접촉 포즈가 두 발을 읽을 수 있음 |
| 접촉 A↔B dense-flow P95 | ≥ 1.0px | 반대 접촉 서명이 실제 픽셀에서 다름 |
| P0→P2, P3→P5 발 이동 P95 | 각각 ≥ 2.75px | 각 반주기에 실제 스윙이 있음 |
| P0→P1→P2, P3→P4→P5 인접 이동 P95 | 모두 ≥ 2.5px | 3장 정지 후 점프를 금지 |
| 인접 발 영역 RGB/alpha 변화 | 모두 ≥ 50% | 바짓단 몇 픽셀만 흔드는 후보 금지 |
| 인접 발 alpha 실루엣 변화 | 모두 ≥ 13% | 내부 색만 바꾸는 가짜 움직임 금지 |
| 접촉 바닥 이탈 픽셀 | 각 반주기 ≥ 4 | 공중/스윙 발이 접촉 영역을 떠남 |
| 통과발 상향 dense-flow P95 | P2/P5 각각 ≥ 0.85px | 실제 수직 들림이 있음 |
| 통과 중 바닥선 support 픽셀 | P2/P5 각각 ≥ 1 | 지지발이 바닥에 남음 |
| upper rigid weight excursion | 0.50~1.25px | 완전 정지와 과도한 bob을 모두 금지 |
| 정렬 뒤 upper identity 변화 | ≤ 1.5% | 얼굴·머리·복장 픽셀 안정 |

OpenCV dense optical flow는 발/하퇴 corridor 안에서만 측정하며 투명 외곽과 내부 신발 픽셀을 함께 본다.
한 지표만으로 PASS하지 않는다. 정지 6장, 바지만 흔드는 6장, P0/P3을 P2/P5로 재사용한 루프, 발은
움직이지만 보호 상체가 6장 모두 byte-identical인 루프가 셀프테스트에서 항상 FAIL해야 한다. 임계값을
후보에 맞춰 낮추지 말고 측정이 틀렸다면 음성·양성 대조군을 함께 고친다.

## 8. Unity와 런타임 cadence

런타임 호출 흐름은 다음과 같다.

`OfficeRuntimeAgent` 실제 변위 → `DirectionalSpriteAnimator.AccumulateTileMotion` →
`OfficeSharedLocomotionRules.ResolveFrame` → `OfficeLocomotionGaitRules.DistanceFrame` → `ApplyFrame` →
최종 `SpriteRenderer`.

`OfficeLocomotionGaitRules.DefaultStrideLength = 0.99380799 world`는 2:1 등각 타일의 중심 간 거리다. 한
6프레임 루프가 정확히 한 stride/두 걸음이며, 정상 `DefaultMoveSpeed=1.0 world/s`에서 cadence는
`1 / 0.99380799 * 2 = 2.0125 steps/s`다. 시간 clock이 아니라 실제 누적 이동거리로 phase를 선택하므로
충돌로 멈추면 발도 멈추고, 한 타일마다 같은 접촉 위상으로 닫힌다.

`PlantedFootPresentationOffset` 함수는 수학 검증용으로 남아 있지만 production에서 호출하지 않는다.
논리 root와 `VisualRoot`는 같은 위치여야 하며 원화 자체의 접지/스윙과 거리-owned cadence가 skating을
해결한다. 문서의 과거 설명만 보고 이 함수를 켜지 않는다.

Unity 검증:

```powershell
Unity.exe -batchmode -nographics -quit -projectPath <repo> `
  -executeMethod FamilyCompany.Editor.CharacterLocomotionGenerationV1Validation.RunBatch `
  -logFile <artifact-log>

Unity.exe -batchmode -nographics -quit -projectPath <repo> `
  -executeMethod FamilyCompany.Editor.OfficeMovementFacingNavigationValidation.RunBatch `
  -logFile <artifact-log>
```

첫 게이트는 12×8×6=576개의 Unity Sprite, catalog phase-major 순서, direction remap, stride/cadence를
검사한다. 둘째 게이트는 128 seed/1,152 path, 실제 방향·pivot·충돌·gait closure를 검사한다.

## 9. Windows D3D11 Player QA와 시각 증거

최종 빌드는 Release Windows x64이며 `-force-d3d11`로 실행한다. V1 전용 QA는 정상 새 게임과 실제
`OfficeRuntimeAgent`를 사용한다.

```powershell
FamilyCompany.exe -force-d3d11 -screen-fullscreen 0 -screen-width 1392 -screen-height 699 `
  -familyCompanyCharacterLocomotionV1Qa `
  -familyCompanyCharacterLocomotionV1QaArtifacts <artifact-directory> `
  -logFile <player-log>
```

필수 결과:

- `character-locomotion-player-final.txt` PASS와 graphics `Direct3D11`.
- 실제 actor를 유지하고 catalog walk input만 바꿔 12명×8방향×6위상=576 runtime frame을 소비한다.
- 플레이어는 48위상 closeup, 나머지 캐릭터는 각 방향 P0/P2/P3/P5 closeup을 남긴다.
- 12명×8방향 phase-2 전체 사무실 overview 96장을 남긴다.
- `character-locomotion-player-trace.csv`에서 12명 모두 motion/display/sprite direction 일치,
  `flip_x=false`, 기대 sprite 이름 일치.
- 96루프 각각 5→0 wrap cycle world distance가 `0.99380799 ± 0.08`.
- actual speed/cycle로 구한 cadence가 1.85~2.15 steps/s, world step/body height가 0.18~0.70이다.
- 별도 정상 gameplay recorder에서 lateral front/back, foot-slide, collision, overlap 위반 0.

이미지/manifest, Unity catalog, Player 실제 이동이 모두 12명을 덮는다. Player의 catalog sweep은 별도
모형 animator가 아니라 정상 새 게임의 실제 player actor·navigation·gait·최종 SpriteRenderer를 사용한다.
긴 캡처 도중 빈 사무실 배회 actor가 테스트 lane을 막지 않도록 나머지 가족 3명은 occupancy를 유지하는
QA teleport로 서로 먼 모서리 walkable cell에 고정한다. 테스트 actor의 충돌·이동·gait 경로는 우회하지
않으며, 정지/차단되면 600 rendered sample 안에 fail-closed한다.
contact sheet/GIF는 원화 검수이고 Player capture를 대신하지 않는다.

## 10. 실패 시 수정 순서

1. 실패가 candidate인지 stable runtime인지 경로를 먼저 확인한다. 오래된 Artifacts나 실행 중인 구 EXE를
   증거로 쓰지 않는다.
2. `character-locomotion-qa-v1.csv`에서 실패 캐릭터/방향/지표를 찾고 해당 6프레임 contact sheet와 실제
   PNG를 함께 연다.
3. 0에 가까운 인접 이동/수직 lift면 임계값을 낮추지 않는다. donor 접촉 A/B, leg split, 공용 위상 곡선을
   고친다. 캐릭터·방향·phase별 픽셀 수작업은 금지한다.
4. 상체 identity drift면 rigid alignment 뒤 변화부터 확인하고 `lowerBodyStart`를 프로필에서 보수적으로
   내린다. upper rigid excursion 0이면 공용 P1/P4 load 규칙을 복원한다. 얼굴/머리를 donor로 교체하지
   않는다.
5. 바닥선 실패면 지지 layer의 ground correction과 y=247 clip을 고친다. 발 밑 픽셀을 추가해 속이지 않는다.
6. 방향 실패면 manifest remap이 아니라 실제 행과 화면 벡터를 고친다. `flipX`로 숨기지 않는다.
7. 이미지 PASS 뒤 Unity catalog, broad 이동, D3D11 Player 순서로 재실행한다. Player가 실패하면 PNG
   PASS를 완료로 보고하지 않는다.
8. 실제 캡처를 사람이 보고 달리기/행진/다리 소실/잔상/초상화 교체가 있으면 정량 PASS라도 후보를
   폐기하고 공용 규칙을 다시 수정한다.

## 11. 빌드·배포 정합성

코드·아트·문서를 main에 커밋한 뒤 그 clean HEAD에서만 최종 빌드한다. 배포 경로는
`C:\Users\godho\Downloads\Family`다. `BUILD_INFO.txt`의 `Commit`은 `git rev-parse HEAD`와 같아야
하고 `WorkingTreeDirty=False`여야 한다. Player QA는 Downloads에 승격된 바로 그 EXE에 다시 실행한다.
push 직전 `git fetch origin main`과 `origin/main`을 확인해 동시 변경을 보존한다.

오래된 `build_family_walk_half_cycles_v2.py --write`는 V1 출하 PNG를 되돌릴 수 있으므로 차단돼 있다.
역사 소스/marker 검증은 유지하되 runtime 확인은 새 `FC-CHARACTER-LOCOMOTION-QA-V1`에 위임한다.
