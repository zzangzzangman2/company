# 가족 3D 캐릭터 표준 (FAMILY_3D_CHARACTER_STANDARD)

최종 갱신 2026-09-02. 가족 3D 캐릭터(아들 Player, 아빠 Father, 이후 엄마 Mother, 언니 Older Sister)의
**크기·밝기·바닥 접지·타일 보행·충돌·착석·검증 절차**를 한 문서로 고정한다. 새 캐릭터를 추가할 때는
이 문서의 표에 한 줄을 추가하고 같은 규칙으로 통과시킨다. 이전의 FAMILY_3D_*, FATHER_V19_*,
PLAYER_V6_* 문서들은 2026-09-02에 삭제되어 이 문서로 대체되었다(이력은 §12).

상태: 아래 값은 2026-09-02 사용자가 GIF·크기·밝기를 승인한 값이다. 코드에서는 아직
`-familyCompanyLegacy2DScaleCandidate` 플래그(후보 프로필)로 켜지며 receipt는
`CANDIDATE_USER_APPROVAL_REQUIRED`, `productionEligible=false`다. production 기본 프로필로 승격하는
일은 별도 작업이며, 승격 시 §3 "production 기본" 열을 이 표준 열로 바꾼다.

---

## 1. 절대 규칙

1. **3D 전용.** 2D sprite·atlas·PSB·분리 팔다리·보행 프레임은 mesh, texture, decal, billboard, motion
   donor, fallback 어느 용도로도 쓰지 않는다. 2D 가족 스프라이트는 삭제 예정이며 크기·색 비교 기준도
   아니다(§4·§5는 3D끼리 맞춘다).
2. **한 패키지 원칙.** 캐릭터마다 Meshy `multi_image_to_3d` 한 작업에서 나온 mesh + bind skeleton + skin
   weight + action `613 Casual_Walk_inplace`를 한 덩어리로 유지한다. 다른 생성물의 rig/clip을 섞지 않는다.
3. **같은 보행 계약.** 모든 가족은 같은 clock·cycle·phase·root 소유권·방향 매핑을 공유한다. 캐릭터별로
   달라지는 것은 mesh, Avatar, albedo, StandingHeight, 측정된 stride/forward 값만이다.
4. **승인된 보행 클립을 다른 문제를 고치려고 바꾸지 않는다.** 착석·크기·색·타일 위치 문제는 각각의
   전용 수단(§6~§8)으로만 해결한다.
5. **회사 PC에서 Unity/Blender/플레이어 창을 띄우지 않는다.** 빌드는 `-batchmode -nographics`, 렌더 QA는
   `-batchmode -force-d3d11` + `CreateNoWindow` + `MainWindowHandle==0` 감시.
6. **자동 PASS는 시각 검토를 대체하지 않는다.** 전체 프레임 시트와 GIF를 보고 사용자가 승인해야
   `productionEligible=true`·production 승격이 가능하다.
7. **보존 항목.** production/default 값, Downloads, 배포 exe, 사용자 편집 `.meta`, git stash는 이 작업
   흐름에서 건드리지 않는다. 브랜치·worktree를 만들지 않는다.

---

## 2. 단위와 화면 환산

| 항목 | 값 | 비고 |
| --- | --- | --- |
| 격자 1칸 | `0.994` office world | 기저 두 축 길이 같음, 사이각 `127°`. 칸 모서리에 수직인 폭은 약 `0.79`(반칸 `0.397`) |
| 화면 px / office world | `48.0` (1280x720) · `72.0` (1920x1080) | 바닥 평면 거리 환산 |
| 화면 px / 3D 세로 1유닛 | `39.3` (1280x720) · `59.0` (1920x1080) | 3D 메시 키 환산(카메라 기울기 축소 포함) |
| 타일 마름모 화면 크기 | `85.3 × 42.7 px` (1280x720) | 1920x1080은 `128 × 64` |
| 3D 메시 키 → 화면 | `StandingHeight × 39.3` | 예: `2.2915 → 90px` |
| 3D 바닥 평면 | world `y = 0` (`Plane(Vector3.up, zero)`) | office 좌표 → 3D는 `MapOfficeWorldToProductionGround` |

---

## 3. 캐릭터별 표준 파라미터

| 파라미터 | Player(아들) | Father(아빠) | Older Sister V3 후보 (§4.4) | production 기본(참고) | 상수 위치 |
| --- | --- | --- | --- | --- | --- |
| FBX / albedo / material | `Production3D/PlayerV8/player-v8-production.fbx`, `player-v8-albedo.png`, `PlayerV8ProductionSurface.mat` | `Production3D/FatherV19/father-v19-production.fbx`, `father-v19-albedo.png`, Player 계열 material | Experimental `OlderSisterV3HiggsfieldSdRepair613/*`, `OlderSisterV3CandidateSurface.mat` | 동일 | production은 `Family3DProductionPresenter`; 누나는 Experimental QA builder |
| walk clip | `PlayerV6_Casual_Walk_inplace` (`1..43`, `1.4 s`) | `FatherV19_Casual_Walk_inplace` (`1..43`, `1.4 s`) | `OlderSisterV3_Casual_Walk_inplace` (`1..43`, `1.4 s`) | 동일 | FBX 내 clip |
| model scale / StandingHeight | `1.263885643` / `2.291498763` | `1.306909878` / `2.454888000` | `1.154662251` / `2.367000` | `1.024378657/1.857258558`, `0.950318127/1.769311871` | `*Legacy2DMatchedModelScale/TargetHeight`; 누나 QA 상수 |
| 가로 scale | `1.0` | `0.806840529` | `1.0` | `1.0` / `0.92` | `FatherLegacy2DMatchedHorizontalScale` |
| 화면 키 (1280x720, 보행 median/목표) | `90px` (85–95) | `93.5px` (91–98) | `86px` median / `93.02px` 목표 | `73px` / `69.5px` | 측정값 |
| 발-뿌리 offset (local x, forward) | `(0.050989, 0.214083)` | `(0.037517, 0.138023)` | `(0.034554, 0.112794)` (24위상 측정) | `(0,0)` | `*FootCenterOffsetLocal`; 누나 runtime receipt |
| 바닥 접지 보정 | 기준(0) | `AlignCandidateStandingGround`가 측정: `-0.2910` | `-0.073097`, 최저점 `0.210697→0.137600` | 없음 | 런타임 측정, 상수 아님 |
| 밝기 gain (`_Color`) | `1.26` | `1.28` | `1.0`; 실제 맵 luma `91.49`, clipping `0%` | `1.0` | `*Legacy2DMatchedBrightnessGain` |
| neutral fill (`_AmbientFactor`) | `0.70` (material 기본) | `0.82` | `0.70` | `0.70` / material 기본 | material |
| stride / phase / cycle | `1.98761598` / `0.40` / `1.4 s` (두 발 착지 = 타일 2칸) | 동일 | `1.98761598` / `0.40` / `1.4 s`, 발 중점 오차 `2.715/5.856px`, 접지 발 선 밖 `0/1120` | `0.7950477` / `0` / `1.4 s` | candidate gait constants |
| 사람 충돌 반경 | `0.475` | `0.578` | 미적용; 보행 reach `0.3937` 측정 | `0.28` / `0.30` | 승인 뒤 `StarterOfficeRuntimeBootstrap`에 추가 |
| 가구 정적 반경 + 패딩 | `0.22 + 0.18` | `0.22 + 0.18` | 미적용·승인 뒤 검증 | `0.22 + 0` | `OfficeRuntimeAgent.DefaultRadius`, `*FurnitureClearancePadding` |
| 책상 인접 칸 경로 비용 | `+2.5` (패딩>0인 배우) | 동일 | 미적용·승인 뒤 검증 | 없음 | `OfficeRuntimePathService.DeskProximityStepPenalty` |
| 원본 hash | receipt `player-v8-source-receipt.json` | GLB `210DC2E1…17F9`, FBX `479F883A…AEB5`, albedo `8C1418E1…962C`, Meshy job `865f2115-…-84eb-d38ca106d45d` (38 credits) | paid V2 GLB `62E1366B…3D3DD`; V3 FBX `6639CB85…846D2E`, albedo `7264BEA7…B71473`; new charge `0` | — | `*-source-receipt.json` |

새 캐릭터는 이 표에 열을 추가한다. 값은 복사하지 않고 §9 절차로 **측정**한다(키, stride, forward,
접지 보정, 밝기 gain, 팔 끝 반경).

---

## 4. 크기 기준

### 4.1 측정값 (1280x720)

| 대상 | 화면 키 | 타일 높이 배수 | 아빠/아들 |
| --- | ---: | ---: | --- |
| 아들 | `90px` | `2.11` | 화면 `1.039`, 메시 `1.071` |
| 아빠 | `93.5px` | `2.19` | |
| 누나 V3 후보 | `86px` median / `93.02px` 목표 | `2.02 / 2.18` | SD 비율·얼굴·눈·밝기 자동 gate 통과, GIF 승인 대기 (§4.4) |
| 누나 V2 후보 (불합격) | `93px` | `2.18` | 키는 대역 안이지만 머리:키 `0.16`, 골반 폭 `0.059`로 가족 비율 위반(§4.3) |
| 머리:키 | `0.307` / `0.333` | | |
| 어깨 폭 / 몸통 폭 / 다리 높이 | `27/34/44px` / `27/32/43px` | | |
| 실루엣 픽셀 / 화면 점유 | `1792` `0.194%` / `1906` `0.207%` | | |
| V31 책상 상판 | `35px` (3D `0.90`) | `0.83` | 아들 키의 `39%`, 아빠 `37%` |

### 4.2 판정 규칙

| 규칙 | 기준 | 현재 | 결과 |
| --- | --- | --- | --- |
| S1 가족 화면 키 대역 | 아들 `90px` 기준 `±10%` (81–99px) | 아들 90, 아빠 93.5 | 통과 |
| S2 아빠/아들 키 비 | `1.03 ~ 1.10` | `1.039` | 통과 |
| S3 키 / 타일 높이 | `2.0 ~ 2.3` | `2.11 / 2.19` | 통과 |
| S4 책상 상판 / 키 | `35 ~ 45%` | `39% / 37%` | 통과 |
| S5 머리·몸통 폭 | 1280x720 보행 중 머리 폭·몸통 폭 아들과 `±1px` 내(정본 비율 유지) | `28/28`, `22/20` | 통과 |
| S6 머리:키 (골격) | GLB rest pose `(head_end − neck) / (head_end − toe)` `0.26 ~ 0.36` (가족 SD 비율) | 아들 `0.273`, 아빠 `0.337`, 누나 V3 `0.310` | 통과 · 누나 V2 `0.159` 불합격 |
| S7 몸 폭 (골격) | 골반 폭/키 `0.08 ~ 0.10`, 어깨 폭/키 `0.030 ~ 0.040`, 다리(골반→발끝)/키 `0.40 ~ 0.50` | 아들 `0.093/0.031/0.470`, 아빠 `0.093/0.037/0.413`, 누나 V3 `0.090/0.036/0.460` | 통과 · 누나 V2 `0.059/0.028/0.581` 불합격 |
| S8 얼굴 가독성 | 1280x720에서 얼굴 높이 `≥ 22px`, 눈 높이 `≥ 3px`(아들 머리 `25px`·눈 `4px` 기준); 텍스처에서 얼굴이 UV 면적의 `≥ 8%` | 아들·아빠 통과; 누나 V3 화면 얼굴 `28.84px`, 두 눈 세로 `≥3px` 프레임 `4` | 화면 gate 통과 · 누나 V2 얼굴 `~12px`, 눈 `~1px` 불합격 |

새 캐릭터의 키는 정본 나이·체형(§CANON)을 따르되 S1 대역 안에 둔다. 엄마·언니는 아들보다 크고
아빠보다 작거나 같은 것이 자연스럽다. **나이·체형은 SD(3~3.7등신) 안에서 표현한다**: 아들·아빠가
이미 3등신대이므로, 어른 캐릭터를 실사 6등신으로 만들면 같은 키라도 가족이 아니라 다른 게임의
캐릭터로 보인다(누나 V2 사고). 참조 이미지 단계에서 S6·S7을 먼저 만족시킨다.

### 4.3 누나 V2 후보 판정 (2026-09-02, 사용자 "크기·밝기·선명도·눈 전부 불합격")

| 항목 | 측정 | 기준 | 판정 |
| --- | --- | --- | --- |
| 화면 키 | `93px` (목표 `93.02`) | S1 81–99px | 통과 |
| 머리:키 (골격) | `0.159` | S6 `0.26~0.36` (아들 `0.273`, 아빠 `0.337`) | **불합격** — 실사 6등신 |
| 골반 폭/키 · 어깨/키 · 다리/키 | `0.059 / 0.028 / 0.581` | S7 | **불합격** — 가족의 절반 폭, 다리 과장 |
| 얼굴·눈 | 얼굴 `~12px`, 눈 `~1px`; 텍스처 눈 지름 `~40px/2048`(아들 `~150px`) | S8 | **불합격** — 눈이 보이지 않음 |
| 텍스처 대비 | albedo `57.6%`가 거의 검정 자주(`RGB 49/36/51`, val `0.205`): 머리·나시·반바지 구분 없음, 남색 반바지 `0.3%`, 흰 파이핑 없음 | C6 정본 의상색·구분 | **불합격** — 실루엣이 한 덩어리 |
| 밝기 | 렌더 근사 luma `121`, val `0.55` (피부 `38.8%`가 val `0.98`) | C3 | 평균은 통과지만 원인은 밝기가 아니라 대비. gain으로 고칠 수 없음(피부만 날아감) |
| 보행·타일·접지 | 발 중점 오차 `3.55/7.83px`, 최저 정점 `0.1376`, occupancy `0/0/0` | §6 | 통과 (파이프라인 자체는 정상) |

원인: Higgsfield 참조 4장 자체가 실사 비율의 성인 일러스트였고, Meshy가 그대로 재현했다. 텍스처는
머리·상의·하의를 같은 어두운 자주로 칩했다. 크기·밝기·오프셋 조정으로는 해결되지 않으며 **참조
이미지부터 다시 만들어 재생성**해야 한다(§9.1 요구사항). 기존 V2 자산은 Experimental 후보 폴더에
남기되 승격·착석 작업을 하지 않는다.

### 4.4 누나 V3 로컬 SD 복구 후보 (2026-09-02, 추가 provider 비용 0)

V3는 새 생성물이 아니라 이미 결제된 V2 GLB의 보존 복사본을 로컬에서 교정한 후보이다. 같은 mesh와
bind skeleton에 연속 비율 함수를 적용하고 skin weights, UV topology와 action 613은 유지했다. albedo도
같은 UV atlas의 기존 색/skin-weight category만 결정적으로 재분류했다. 원본 V2는 별도 보존하며 donor,
retarget, 절차 보행, 접지별 host 이동은 없다.

| 항목 | 측정 | 기준 | 판정 |
| --- | ---: | ---: | --- |
| 머리:키 | `0.310` | S6 `0.26~0.36` | 통과 |
| 골반/키 · 어깨/키 · 다리/키 | `0.090 / 0.036 / 0.460` | S7 | 통과 |
| 실제 맵 키 | median `86px`, 목표 `93.02px` | S1 `81~99px` | 통과 |
| 얼굴·눈 | 얼굴 환산 `28.84px`; 두 눈 세로 `>=3px` 정면 프레임 `4` | S8 화면 `22/3px` | 통과 |
| 밝기 | luma `91.49`, sat `0.247`, clipping `0%` | C3 `90~125`, C4 `<=5%` | 통과 |
| 타일·접지 | 발 중점 `2.715/5.856px`; 발 밖 `0/2688`, 접지 발 밖 `0/1120`; 최저점 `0.1376` | §6 | 통과 |

자동 gate는 사용자 시각 승인 대신이 아니다. 전체 실제 맵 GIF 승인 전 상태는
`CANDIDATE_USER_APPROVAL_REQUIRED`, `productionEligible=false`이며 production/충돌/착석을 바꾸지 않는다.
V3는 새 provider 제출물이 아니므로 원본 V2 UV topology를 보존했다. S8의 `UV 8%` 항목은 이후 새 생성
제출의 사전 gate로 계속 유지하며, 이 로컬 복구는 실제 맵의 얼굴/두 눈 픽셀 측정과 사용자 GIF 판정으로만
예외 승인할 수 있다. 이 예외는 Mother나 이후 생성물에 복사하지 않는다.

---

## 5. 밝기·색 기준

측정 정의: 1280x720 같은 타일·같은 카메라·같은 조명에서 배우만 분리 렌더(`ratio-*-isolated.png`)한
실루엣 픽셀(alpha>32) 평균. `luma = 0.299R+0.587G+0.114B`, `sat/val`은 HSV 평균, 클리핑 = RGB 모두 `≥250`인
픽셀 비율.

캐릭터 셰이더 `FamilyCompany/Production/PlayerV8BalancedAlbedo`는
`albedo × _Color × saturate(_AmbientFactor + _KeyFactor(0.18)·form)`의 **고정 중립광**이다. 씬 방향광·
sky probe는 캐릭터 밝기에 영향이 없다. 밝기 조정 수단은 `_Color` gain 또는 albedo 자체뿐이며, 씬
전체 ambient를 바꾸는 것은 금지(승인된 가구 색이 변한다).

### 5.1 측정값

| 대상 | luma | sat | val | 흰색 클리핑 | 비고 |
| --- | ---: | ---: | ---: | ---: | --- |
| 아들 (gain 1.26) | `118.2` | `0.364` | `0.522` | `3.0%` | albedo 자체 `103.8 / 0.456 / 0.475` |
| 아빠 (gain 1.28) | `93.2` | `0.216` | `0.407` | `0%` | albedo 자체 `78.6 / 0.254 / 0.359` |
| 바닥(꿀빛 목재) | `153.5` | `0.626` | `0.795` | | 팔레트 정본 `ART_STYLE.md` |
| 폐기: gain 1.00 | `93.9` / `73.7` | | `0.41` / `0.33` | | 사용자 "어둡다" |
| 폐기: 아빠 1.42 | | | | | 얼굴·손 피부 하얗게 날아감 |
| 폐기: 둘 다 1.32 | `123.2` / `95.8` | | | `11.2%` / 0 | 아들 후드 흰색 평평해짐 |

### 5.2 판정 규칙

| 규칙 | 기준 | 현재 | 결과 |
| --- | --- | --- | --- |
| C1 아빠/아들 luma 비 | `0.70 ~ 1.30` (QA fail-closed) | `0.789` | 통과 |
| C2 최소 luma / sat | `≥ 45` / `≥ 0.12` (QA fail-closed) | `93.2` / `0.216` | 통과 |
| C3 절대 밝기 | 실루엣 luma 아들 `≥ 110`, 아빠 `≥ 90`; 새 캐릭터는 의상 명도에 따라 `90 ~ 125` | `118.2 / 93.2` | 통과 |
| C4 클리핑 | 흰색 클리핑 `≤ 5%`, 피부 하이라이트 날림 없음(얼굴·손 확대 검토) | `3.0% / 0%` | 통과 |
| C5 정본 색 유지 | gain은 RGB 곱셈만(색상·채도 불변); 회색 곱(<1)·emission·specular·전체 ambient 변경 금지 | white tint × gain | 통과 |
| C6 팔레트 계열 | 갈색·남색·청록 외곽, 크림·민트·청록·복숭아 포인트, 정본 의상색 | 후드 흰색, 남색, 청록 셔츠, 차콜 | 통과 |

새 캐릭터 절차: white tint(gain 1.0)·neutral fill 0.70으로 시작 → 분리 렌더 측정 → gain을 0.02 단위로
올려 C3를 만족시키되 C4를 넘기 직전에서 멈춘다. 피부가 먼저 날아가면 gain 대신 albedo 밝기 보정을
검토한다(정본 색상 유지). **gain은 대비를 만들지 못한다**: 누나 V2처럼 머리·상의·하의가 한 어두운
색이면 평균 luma가 기준을 넘어도 실루엣이 한 덩어리로 읽힌다. C6 의상 색 블록 구분은 텍스처(참조
이미지) 단계에서 확보한다.

---

## 6. 바닥 접지·타일 중앙 보행

- **접지**: 프리젠터는 bind pose의 renderer bounds `min.y = 0`으로 모델을 세우지만, 보행 클립이 골반을
  bind pose보다 높게 실어 걷는 동안 최저 skinned 정점이 뜬다(아들 `0.138`, 아빠 `0.429`). 후보 프로필의
  `AlignCandidateStandingGround`가 바인드 시 보행 사이클 24위상을 샘플링해 **아들을 기준**으로 다른
  캐릭터의 standing/walking visual ground를 낮춘다(아빠 `-0.2910`). 상수 보정 1개, 착석 포즈에는 적용
  안 함. 새 캐릭터도 자동으로 같은 처리를 받으며 receipt `walkGroundClearanceMeshY`가 아들과 `≤0.05`
  차이여야 한다.
- **타일 중앙**: 발 중점 타일 오차(뼈 기반) median `≤4px`, max `≤8px`. 현재 아들 `2.23/6.13`, 아빠
  `1.46/4.31`. 접지 중 선 접촉 프레임(뼈+신발 폭 기준) 아들 `2/61`, 아빠 `8/61`.
- **stride**: action 613 한 사이클에 착지 두 번이므로 사이클 이동을 타일 2칸(`1.98761598`)에 결합, phase
  `0.40`. 접지 프레임마다 root를 옮기는 보정(contact inset)은 **금지**(프레임 의존 텔레포트/슬립).
- **판정에 쓰지 않는 지표**: `sameTileShoeCentroidDeltaPx`, `dynamicShoeLaneOffsetMedianPx`(신발 픽셀
  중심). 신발 높이가 섞여 바닥 위치 증거가 아니다. 2026-09-02에 이 지표를 맞추려다 아빠 발을 타일
  모서리로 옮긴 사고(접지 선 접촉 `8→57/61`)가 있었다. 눈 검증은 agent 중심에 타일 마름모를 그려서 한다.
- 발-뿌리 offset(§3)은 FBX 양발 뼈 평균점이 root에서 벗어난 양을 측정해 넣는 상수이며, 진행 방향
  회전 뒤 적용된다. 모델/Animator root를 옮기는 방식은 착석 무릎을 바꾸므로 금지.

---

## 7. 충돌·가구 규칙

| 항목 | 값 | 동작 |
| --- | --- | --- |
| 사람끼리 | 반경 합 `0.475 + 0.578` | 목표점이 두 반경 합보다 가까우면 이동 거부(`BlockedAgentMoveCount`) |
| 경로 예약 | 앞 셀 예약 | 타인이 예약한 셀 진입 불가 |
| 막힘 처리 | 정지 → `0.8 s` 후 옆 칸 비켜서기 → `1.1 s` 후 재계산 → `2 s` 후 예약 해제 | `OfficeNavigationTrafficRules` |
| 벽·바닥 셀 | 정적 반경 `0.22` | 벽 옆 줄 보행 유지(패딩 미적용) |
| 책상(BlocksMovement) | `StaticHard`, 서브셀 마스크, 반경 `0.22 + 패딩 0.18 = 0.40` | 반칸 `0.397` 아래로 두어 책상 옆 칸이 막히지 않게 함(`0.49` 시험 시 700프레임 정체) |
| 의자 | `Interaction`, 좌석 주인만 통과 | 도킹은 `permittedSeatId` 예외; 본인 책상도 예외 |
| 경로 비용 | 책상 footprint 접한 칸 `+2.5` (패딩>0 배우) | 여유가 있으면 한 칸 떨어져 우회, 목표가 책상 옆이면 도달 |
| 팔 끝 반경(측정) | 아들 `0.514`, 아빠 `0.407` world | 가구 여유 설계 근거. 새 캐릭터는 receipt `walkBodyHorizontalReach`로 측정 |

판정: 우회 테스트(아들 `(3,8)→(3,2)`, 아빠 `(7,8)→(11,8)`, 책상 관통 직선)에서 정적/의자 침범 `0/0`,
책상 부품 로컬 박스 안 정점 프레임 `0/0`(월드 AABB는 격자 기울기 때문에 과대 판정이라 쓰지 않음),
마주 걷기 overlap `0px`, penetration `0`, 둘 다 `Working` 착석.

---

## 8. 착석·워크스테이션 계약 (h = WalkActor.StandingHeight)

승인 V31 원형 의자 세트(`V31_AtomicWorkstationSet_OriginalChair_<seat>`)는 불변이다. 캐릭터 fitting 중
의자·책상·CRT 위치와 방향을 바꾸지 않는다. 좌석 한 개 = 책상+CRT+키보드+의자 한 루트.

| 항목 | 규칙 |
| --- | --- |
| 착석 전환 | 로코모션이 seat phase로 끝난 뒤 같은 Avatar의 neutral pose를 `0.42 s` 블렌드 |
| 보이는 골반 | 의미 좌석은 chair socket 유지, 시각 골반 `+bodyForward·0.07h`, hips 중심은 쿠션 위 `0.113h` |
| 손목 중심 | `keyboard + up·0.022h − bodyForward·0.035h`, 좌우 `±bodyRight·0.12h` |
| 타이핑 | 반대 위상, `0.8 s`, 세로 진폭 `≤0.010h` |
| 발 중심 | 시각 root `+bodyForward·0.09h`, 바닥 `+0.158h`, 좌우 `±0.12h`; 발목은 회전축에서 `≥0.19h`, 앞으로 `≥0.14h`; 무릎 앞으로 `≥0.12h` |
| 무릎 각 | 네 방향 모두 `80°~140°` (현재 Player `107/113°`, Father `96/100°`) |
| 메시 여유 | 최종 포즈에서 skinned 정점이 쿠션·등받이·요추 레일·기둥·바닥판 안에 `0`개(네 방향) |
| 네 방향 | 홀수 회전마다 `(right,forward)→(−forward,right)`, footprint 폭/깊이 교환; 두 방향 body yaw 차 `≥45°` |
| 키보드/CRT | 키보드 앞줄 `frontForward + depth/2 + 0.020h`, CRT는 깊이 `0.43` 뒷줄, 정면 오차 `≤0.1°`, 좌석-키보드 `≤0.30h`, 키보드-화면 띠 `≥0.07h`, 좌석 책상 여유 `≥0.14h` |
| 경로 | `Idle > Navigating > ApproachingSeat > AligningSeat > RotatingToSeat > Working`, 정적/의자/사람 침범 `0/0/0`, 좌석 타일 오차 `0` |

---

## 9. 새 캐릭터 추가 절차

1. **참조 4장**(front·three-quarter·side·back, 같은 의상·비율·조명). 확대 검사: 팔다리 중복/누락, 손
   가림, 다리 겹침, 신발 잘림, 뷰 간 정체성 변화 있으면 재작업. **비율 요구(누나 V2 실패 후 추가)**:
   - 아들·아빠와 같은 SD 3~3.7등신(머리:키 `0.26~0.36`), 어깨/키 `0.030~0.040`, 골반/키 `0.08~0.10`,
     다리/키 `0.40~0.50`. 참조 프롬프트에 "same chibi proportions as the approved Player/Father
     models, large head, short legs" 수준으로 명시하고, 아들·아빠 정면 렌더를 비율 참고로 첨부한다.
   - 얼굴이 크고 눈이 큼(아들 기준 머리 높이의 `≥15%`). 정면 참조에서 눈이 또렷해야 한다.
   - 의상 색 블록이 명도로 구분됨: 누나는 머리(거의 검정) < 나시(차콜, 머리보다 밝게) < 반바지(남색)
     + 흰 파이핑이 참조에서 보여야 한다. Meshy는 참조의 대비를 그대로 텍스처에 옮긴다.
   - 참조 생성 뒤 Meshy 제출 전에 이 문서 S6~S8을 참조 이미지에서 먼저 눈으로 판정한다.
2. **생성**: Higgsfield MCP → Meshy `multi_image_to_3d`, rigging/animation/PBR/remesh on, quad, target
   60,000, A-pose 1.65 m, action `613 Casual_Walk_inplace`. 먼저 credit preflight, 사용자 승인 후 제출.
   job ID·옵션·차감 credit·GLB SHA-256 기록. OAuth URL/토큰은 저장소에 넣지 않는다.
3. **변환**: `Tools/Blender/prepare_father_v19_meshy_one_package_unity.py`의 캐릭터별 복사본을 Blender
   숨김 실행. rig helper(`Icosphere`)만 제거. 뼈 좌표로 실제 반복 주기를 찾아 `1..43`(1.4 s)처럼 한
   주기만 import. clip-delta retarget·anatomical sanitation·procedural gait·rigid-arm·pose damping 모두 off.
4. **원본 fail-closed**: 세 번째 다리, 붙은 손, 옷 찢김, 교차 weight, 굽은 몸통, 인형 걷기면 Unity 전에 폐기.
5. **반입**: `Content/Resources/Production3D/<Name>/` (FBX·albedo·material·`*-source-receipt.json`).
   같은 FBX의 Avatar와 clip, `poseStrength=1`. material은 `PlayerV8BalancedAlbedo`, white tint, ambient
   `0.70`, emission/specular 없음(YAML에 `_MainTex/_Color/_AmbientFactor/_KeyFactor`만).
6. **바인딩**: `Family3DProductionPresenter`에 캐릭터 리소스 경로·scale·StandingHeight·offset·gain 상수 추가
   (§3 표와 같은 이름 규칙). `StarterOfficeRuntimeBootstrap`에 충돌 반경·가구 패딩 추가. QA에 host 이름
   `<Name>ProductionHost`와 seat ID 추가. 아빠 전용 ID를 재사용하지 않는다.
7. **측정**(§10 QA 실행): forward는 `0/90/180/270` 네 후보를 같은 프레임에서 비교해 두 구간 이상에서
   진행 방향을 보는 값을 택한다. stride는 두 발 착지 분포로 정한다. 키는 S1 대역, 접지는 §6, 팔 끝
   반경은 receipt로 읽어 §7 패딩이 충분한지 확인, 밝기는 §5 절차로 gain 결정.
8. **착석**: 보행 승인 뒤에만 §8 적용. 네 방향 렌더와 메시 여유 `0` 확인.
9. **문서**: §3 표에 열 추가, §4·§5 표에 행 추가, `PROJECT_STATE`·`DECISIONS` 갱신, 증빙 폴더에
   GIF·시트·receipt 저장. `CANON.md`의 외형·의상 정본과 어긋나지 않는지 확인.
10. **승인**: 사용자가 전체 GIF를 보고 승인하기 전까지 `productionEligible=false`.

---

## 10. QA 실행법과 게이트

1. 빌드(창 없음): `Tools/Invoke-FamilyCompanyFastQa.ps1 -Profile player-scripts -NoPlayerSmoke`
   → `Artifacts/FastQa/cache/WindowsPlayer/FamilyCompany_FastQa.exe` (약 30 s). 잠금 파일
   `Artifacts/FastQa/locks/fast-qa.lock`의 pid가 살아 있으면 대기.
2. 실행(창 없음, 약 60 s):
   ```
   FamilyCompany_FastQa.exe -batchmode -force-d3d11 -screen-fullscreen 0 -screen-width 1280 -screen-height 720
     -familyCompanyPlayerFather3DInteractionQa -familyCompanyLegacy2DScaleCandidate
     -familyCompanyPlayerFather3DInteractionArtifacts <dir> -logFile <dir>\player.log
   ```
   exit `0` = 게이트 통과(`CANDIDATE_USER_APPROVAL_REQUIRED`), `1` = FAIL(로그의 `FAMILY_COMPANY_PLAYER_FATHER_3D_INTERACTION: FAIL | …`에 원인).
3. 산출물: `player-father-3d-interaction-result.txt`(receipt), `father-player-same-tile-pixel-ratio.txt`,
   `ratio-*-isolated.png`(색 측정용), `approach-frames/`(마주 걷기 86장), `route-frames/`(좌석 경로),
   `detour-frames/`(책상 우회), `turn-frames/`(회전 48장), `player-father-*-trace.csv`,
   `office-furniture-footprints.csv`, `office-desk-part-bounds.csv`.
4. 증빙 생성: `python Tools/build_player_father_3d_independent_qa_media.py --artifact <dir> --output Docs/Evidence/PlayerFather3DIndependentQaCurrent`
   (GIF 4종, 전체 프레임 시트, ratio sheet).
5. 자동 게이트(fail-closed): 이동 `≥0.25`, 프레임 `≥24`, 사람 침범 `0`, 시작 타일 오차 `≤0.0001`, 중심선
   오차 `≤0.0005`, 발 중점 오차 아들·아빠 각 `≤4/8px`, 최저 정점 높이 차 `≤0.05`, 접지 샘플 `≥24`,
   신발 픽셀 측정 프레임 = 전체, luma 비 `0.70~1.30`, 최소 luma `45`, 최소 sat `0.12`, 우회 목표 도달,
   착석 `Working/Working`, 정적/의자/사람 위반 `0/0/0`.
6. 눈 검증(필수): 마주 걷기 GIF에서 두 사람 발이 타일 마름모 안 같은 높이인지, 회전 GIF에서 몸 전체가
   도는지, 우회 GIF에서 책상을 돌아가고 팔이 책상에 안 들어가는지, 같은 타일 시트에서 크기·밝기.

---

## 11. 재발 방지 목록

| 사고 | 영구 규칙 |
| --- | --- |
| 세 번째 다리·찢어진 옷·고무 팔 | 패키지 부품을 섞지 않는다. 원본에서 fail-closed |
| 방향 오류 | 뼈 이름·발 축·화살표로 추정 금지. `0/90/180/270`을 실제 화면 이동과 비교 |
| 인형 걷기·프로시저럴 | 생성된 action 613을 그대로 재생, 전체 GIF 검토 |
| 회색 곱셈으로 밝기 조절 | 전체 atlas에 `<1` 곱 금지(아들 V6 `0.74` 실패). 밝기는 gain(`>1`)·albedo만 |
| 씬 ambient로 한 캐릭터 고치기 | 승인된 책상·의자 색이 변함. 캐릭터 material 로컬만 |
| 비활성 material 필드 | 셰이더 교체 후 emission/specular 잔존 확인, property table 재생성 |
| `-nographics` 캡처를 색 증거로 | 렌더 QA는 hidden D3D11만 |
| 네 장 스틸·자동 PASS로 판단 | 전체 프레임 시트 + 추적 GIF + 전체 맵 GIF, 회전·루프 포함 |
| 아빠 ID를 다른 캐릭터에 재사용 | 캐릭터마다 named flag·family ID·seat·receipt |
| agent root = 발 위치로 가정 | 양발 뼈 중점을 전체 보행에서 추적. 상수 offset만 허용, contact-frame 보정 금지 |
| 신발 픽셀 중심으로 타일 정렬 | §6. 높이가 섞여 무효. 2026-09-02 아빠 발 모서리 사고 |
| 뿌리 위치만 맞고 메시가 떠 있음 | 최저 skinned 정점 높이를 측정(`walkGroundClearanceMeshY`) |
| 팔이 책상에 파묻힘 | 팔 끝 반경 측정, 가구 패딩 `0.40`(반칸 미만), 책상 인접 칸 경로 비용 |
| 가구 반경을 반칸 이상으로 | 책상·벽 옆 칸이 막혀 정체(0.49 시험) |
| 월드 AABB로 침투 판정 | 격자가 월드 축에 기울어 과대 판정. 부품 로컬 박스 사용 |
| 한 세트 요청이 의자·CRT 배치 변경으로 | 시각 루트만 묶고 승인된 좌표·방향 유지 |
| 서랍 디테일이 책상 전면에 | `drawerForward − drawerDepth/2`에서 파생 |
| 착석에서 다리 일자·2D 포즈 | 캐릭터별 desk flag로 공용 착석 경로에 태움 |
| 옛 의자 하반신 renderer 잔상 | 좌석 claim 뒤 생성되는 renderer를 매 프레임 마스크 |
| 창 뜬 Unity/플레이어 | 회사 PC 금지. `-batchmode`, `CreateNoWindow`, `MainWindowHandle` 감시 |

---

## 12. 증빙과 이력

- 현재 증빙: `Docs/Evidence/PlayerFather3DIndependentQaCurrent/` (README에 최종 run과 수치),
  근거 실행 `Artifacts/FatherBrightnessFinal-20260902-165000/`.
- 과거 증빙: `Docs/Evidence/Family3DFatherV19V31/`, `Docs/Evidence/Family3DPlayerV6/`,
  `Docs/Evidence/PlayerFather3DProduction/`, `Docs/Evidence/PlayerV8Production/`.
- 결정 이력: `Docs/DECISIONS.md` 2026-08-24 ~ 2026-09-02 항목. 사용자 시각 판정 이력과 폐기된 접근이
  거기에 남아 있다.
- 2026-09-02에 삭제해 이 문서로 대체한 문서(git 이력에서 복구 가능, 삭제 커밋 직전 HEAD `4c1cb829`):
  `FAMILY_3D_CHARACTER_CANON_2026-08-24.md`, `FAMILY_3D_CHARACTER_HANDOFF_2026-08-24.md`,
  `FAMILY_3D_CONTINUATION_GUIDE_2026-08-25.md`, `FAMILY_3D_RUNTIME2D_V2_STYLE_LOCK_2026-08-24.md`,
  `FAMILY_3D_WORKSTATION_CHARACTER_REUSE_CONTRACT_2026-08-28.md`,
  `FAMILY_3D_CHARACTER_COMPLETION_AND_FAILURE_GUARD_2026-08-31.md`,
  `FATHER_V19_MESHY_ONE_PACKAGE_WALK_QA_2026-08-28.md`, `FATHER_V19_FULL_3D_DESK_WORK_QA_2026-08-28.md`,
  `FATHER_V19_INDEPENDENT_SCALE_WALK_QA_2026-09-01.md`, `PLAYER_V6_FULL_3D_DESK_WORK_QA_2026-08-31.md`,
  `FAMILY_CHARACTER_SCALE_COLOR_STANDARD_2026-09-02.md`.
- 캐릭터 외형·의상 정본(얼굴·머리·옷·나이)은 계속 `Docs/CANON.md`가 소유한다. 이 문서는 크기·밝기·
  보행·충돌·착석·절차만 소유한다.
