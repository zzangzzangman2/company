# Player East Mixamo Trace V2

이 폴더는 집 PC에서 주인공 east 6프레임을 그대로 이어 만들기 위한 **추적 가능한 입력 묶음**이다.
런타임 에셋이 아니며, 여기에 있는 생성 이미지와 raster-warp 결과는 전부 거부된 증거다.

## 현재 판정

| 범위 | 판정 | 설명 |
| --- | --- | --- |
| Unity raw export (`Artifacts` only) | PASS / local motion reference | `PlayerWalkMotionReferenceExporter`가 Mixamo clip을 +90° east로 평가해 ignored `Artifacts/PlayerEastMixamoTraceCandidate/mixamo-east-6pose-joints.json`에 재생성한다. 원시 export는 공개 Git에 넣지 않는다. |
| `target-joints.json` | PASS / 2D motion contract | project stride를 적용한 P0~P5 owner별 pelvis→heel/toe 좌표 |
| `phase-contract.md` | PASS / foot-lock math | maximum contact drift `0.765007px`, 기준 `<=1px` |
| `player-east-locked-skeleton-guide.png` | PASS / guide only | 3×2, 768×512 가이드. 게임 아트 아님 |
| `SourceV3Frames/` | upper/style source only | P0~P5 상체를 보존하는 입력. **하체 pose donor로 사용 금지** |
| `RejectedImageGenInputs/` | REJECTED | 가이드를 지키지 못한 image-edit 결과. shipping frame 사용 금지 |
| `RejectedResearch/` | REJECTED_RESEARCH | 기존 하체 raster warp가 P2/P5를 기괴하게 꺾는다는 재현 증거 |

현재 production 기본은 계속 `Legacy48`이다. 이 폴더의 어떤 PNG/GIF도
`Assets/Resources/FamilyCompany/Player2DWalkV2/`로 복사하지 않는다.

## 고정 동작 계약

- KShopGo reference: `0.8s`, 30fps, 24 samples.
- 2D phase: `0/4/8/12/16/20`, 즉 `0/133.3/266.7/400/533.3/666.7ms`.
- 실제 project runtime은 distance-based speed `1.0`/stride `0.99380799`이므로 정속 한 cycle은
  `0.99380799s`, pose 간격은 약 `165.635ms`다. 0.8초는 KShop 비교 timing이고 runtime speed를 몰래
  `1.24226`으로 올리지 않는다.
- P0/P3 contact, P1/P4 load + maximum recovery, P2/P5 terminal stance + low pass.
- physical left/right owner는 화면 좌우가 바뀌어도 유지한다.
- 모든 신발 앞코는 east(+X), support는 sprite-local -X, swing은 +X로 단조 이동한다.
- heel lock은 q0→q1, toe lock은 q1→q2→q3으로 따로 검증한다.
- production root advance는 `0.99380799 / 6 * 180 / 1.55 = 19.234993px/pose`다.
- maximum heel/toe world drift는 `0.765007px`; alternating step은 `57.704979px`다.

## 휴대 가능한 재생성

집에서 하체 저작을 이어갈 때는 tracked `target-joints.json`, `phase-contract.md`, skeleton guide와
`SourceV3Frames/`만 있으면 된다. raw trace까지 다시 감사할 때만 Unity exporter를 먼저 실행한다.
필수 Python 패키지는 `numpy`와 `Pillow`다. `opencv-python`은 거부된
`build_player_east_mixamo_locked_art_v2.py` 실패 증거를 재현할 때만 필요하고 production 작업 의존성이 아니다.

```powershell
$projectRoot = (Get-Location).Path
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe'
& $unityEditor -batchmode -nographics -quit `
  -projectPath $projectRoot `
  -executeMethod FamilyCompany.Editor.PlayerWalkMotionReferenceExporter.RunFromCommandLine `
  -logFile (Join-Path $projectRoot 'Artifacts\PlayerEastMixamoTraceCandidate\unity-export.log')
if ($LASTEXITCODE -ne 0) { throw "Mixamo trace export failed: $LASTEXITCODE" }

python .\Tools\build_player_east_mixamo_trace_v1.py `
  --json .\Artifacts\PlayerEastMixamoTraceCandidate\mixamo-east-6pose-joints.json `
  --source-dir .\ArtSources\PlayerEastMixamoTraceV2\SourceV3Frames `
  --output-dir .\Artifacts\PlayerEastMixamoTraceCandidate
if ($LASTEXITCODE -ne 0) { throw "Trace build failed: $LASTEXITCODE" }
```

성공 출력은 다음과 같다.

```text
PLAYER_EAST_MIXAMO_TRACE: PASS | poses=6 rootAdvance=19.234993px maxContactDrift=0.765007px
```

raw trace를 다시 뽑을 때는 Unity Editor를 닫고 실행한다. Unity 결과는 항상 ignored `Artifacts`에서 검증하며,
Git에는 파생된 owner/foot-lock 계약과 가이드만 갱신한다.

## 다음 제작의 최소 안전선

기존 파일에서 골라 붙일 수 있는 완전한 lower donor는 없다. V3도 P0의 앞발이 뜨고 P2가 low-pass가 아닌
wide-contact 반복형이다. 따라서 다음 작업은 아래만 허용한다.

1. `SourceV3Frames/player_east_walk_0..5_v2.png`의 **상체를 각 phase 그대로 보존**한다.
2. `target-joints.json` 위에 P0~P5 하체를 각각 완전한
   `pelvis→hip→knee→ankle→heel/toe` 체인으로 새로 저작한다.
3. 하체 좌우반전, screen-left/right 기반 owner 교환, 신발·종아리 조각 이동, 기존 lower warp를 금지한다.
4. 숨김 QA 사본에는 physical left=cyan, right=orange를 hip부터 toe까지 유지한다.
5. 검정/투명 배경 close-up, owner 연결, 두 발 분리, foot-lock, KShop 비교용 0.8s GIF와 실제 runtime
   `0.99380799s` GIF를 모두 검사한다.
6. 사용자 east GIF 승인 전에는 Assets 복사, 카탈로그 갱신, 다른 방향/가족 확대를 하지 않는다.

locked envelope는 P0/P3 `x92..164,y174..233`, P1/P4 `x102..145,y175..233`,
P2/P5 `x112..149,y173..233`이다.

`Tools/build_player_east_mixamo_locked_art_v2.py`는 이 접근의 실패를 보존하는 재현 도구다. 기본 실행은
차단되어 있으며 `--allow-rejected-research`를 명시해도 결과는 promotion할 수 없다.

## ImageGen 편집 기록 — 전부 거부

세 결과 모두 `image edit` 모드로 만들었다. exact 결과는 이 폴더에 보존했고, 입력 guide는 tracked
`target-joints.json`·`SourceV3Frames/`와 trace builder로 재생성할 수 있다. ignored `Artifacts`의 임시 입력을
Git에 복사하지 않았으며 어떤 결과도 최종 프레임에는 사용하지 않는다.

### 1. six-frame-attempt.png

결과 SHA-256: `8570EBCDBAB50EFD14283A9569A2FAC82181D64DD27240E81336237D8BE3CC6F`

```text
The FIRST image is the authoritative locked 3x2, six-frame motion guide. The SECOND image is the exact protagonist appearance and pixel-art style reference. Edit the FIRST image into a finished six-frame EAST / screen-right walking sheet.

NON-NEGOTIABLE:
- Keep canvas exactly 768x512 with the same 3 columns x 2 rows and each cell exactly 256x256.
- Keep every head, cap, face, hair, hoodie, torso, arm, hand, waist position, pose order, character scale, and pure #00FF00 background in the FIRST image unchanged.
- Replace ONLY the cyan and orange lower-body skeleton lines, white joint dots, ground markers, and small white arrows with finished anatomy.
- Every hip, knee, ankle, and toe must stay on the locked guide anchors; do not invent a different gait, mirror a lower body, swap leg ownership, widen the stride, raise the knees, or duplicate a pose.
- Cyan always means the same physical LEFT leg; orange always means the same physical RIGHT leg throughout all six cells.
- Render the exact dark navy trousers and exact red/white/navy sneakers shown in the SECOND image, with the same slim child proportions, crisp hard-edged pixel-art outlines, shading, shoe size, and color palette. Do not create purple, beige, oversized, realistic, or smooth-painted legs.

PHASES, left-to-right then top-to-bottom:
P0 left heel contact while right rear toe makes its last contact; right heel visibly raised.
P1 left flat support/load while the right knee and whole right leg recover; right shoe fully airborne.
P2 left terminal stance while the right foot passes low under the body toward screen-right and descends toward landing.
P3 right heel contact while left rear toe makes its last contact; left heel visibly raised.
P4 right flat support/load while the left knee and whole left leg recover; left shoe fully airborne.
P5 right terminal stance while the left foot passes low under the body toward screen-right and descends toward the loop's P0 landing.

Both shoes always belong to their same hip-knee-ankle chain. Both sneaker toes visually face screen-right, including airborne recovery poses. Keep the support sole flat, keep only the trailing toe touching in P0/P3, maintain a transparent gap at crossings, and never weld or overlap the two ankles/shoes. The pelvis, trouser folds, knees, shins, ankles, and sneakers must all read in the same east-facing direction as the upper body. Natural relaxed KShopGo/Mixamo weight transfer, no march, run, kick, hop, split, backward-bent foot, double ankle, ghost pixels, duplicated contact, or west-facing lower body.

Do not add text, labels, arrows, guide colors, shadows, floor, extra characters, panels, borders, or scenery. Return only the complete edited 768x512 six-cell sheet on pure #00FF00 green.
```

거부 이유: P0~P2 owner/contact 순서가 반대이고 P1은 전방 kick, P2는 wide contact이며 P3→P4에
crossover가 몰렸다. 배경도 단색 green이 아니어서 halo가 생긴다.

### 2. p1-attempt.png

결과 SHA-256: `9729145515E660F5E9B3829A709CE07E8DC392758350D17619EECDAF980E8D09`

```text
Edit this exact 512x256 two-cell sprite sheet. Keep the LEFT cell completely unchanged as the exact appearance, palette, proportions, shoe design, and crisp pixel-art reference. In the RIGHT cell keep the entire head, cap, face, hair, hoodie, torso, arms, hands, waist location, scale, and pure #00FF00 background unchanged.

Replace ONLY the colored lower-body guide and its white dots/arrow/ground line in the RIGHT cell with finished legs and shoes. This is locked Mixamo/KShopGo phase P1: LEFT physical leg is cyan and is the planted load-bearing leg; RIGHT physical leg is orange and is in maximum recovery behind/under the body.

Strict anatomy:
- Put the finished left hip, knee, ankle, and sneaker exactly along the cyan chain. The left support sneaker is flat on the y=233 ground and points screen-right.
- Put the finished right hip, knee, ankle, and sneaker exactly along the orange chain. The entire right leg folds naturally backward at the knee; the right sneaker is fully airborne around 10px above the support sole, remains behind the support ankle, and its toe still visually points screen-right.
- This is recovery, NOT a forward kick, high-knee march, contact pose, or wide stride.
- Same slim child's dark navy trousers and same small red/white/navy sneakers as the LEFT cell. Keep leg thickness and shoe size identical to the reference.
- Pelvis, trouser folds, both knees, shins, ankles, and shoes must all belong to the same east-facing torso. No lower-body mirror, west-facing pants, backward-bent ankle, double shoe, overlap, welding, ghost pixels, or beige/purple clothing.
- Preserve a clear transparent gap between the airborne shoe and ground/support shoe.

Do not change the LEFT cell. Do not add text, arrows, markers, floor, shadow, scenery, or any extra element. Return the complete 512x256 two-cell image on pure green.
```

거부 이유: 회수발의 신발/하퇴가 west 방향으로 돌아가 상체·골반의 east와 일치하지 않는다.

### 3. p2-attempt.png

결과 SHA-256: `E2A7971CEF1093D8DD96A65E8DD391EBF14A0B5AF44063DC988F25D70609B17B`

```text
Edit this exact 512x256 two-cell sprite sheet. Keep the LEFT cell completely unchanged as the exact appearance, palette, proportions, and crisp pixel-art reference. In the RIGHT cell keep the head, cap, face, hair, hoodie, torso, arms, hands, waist position, scale, and pure #00FF00 background unchanged.

Replace ONLY the colored lower-body guide, white joint dots, arrow, and ground line in the RIGHT cell with finished anatomy. This is locked Mixamo/KShopGo phase P2: LEFT physical leg is cyan in terminal stance; RIGHT physical leg is orange making a LOW FORWARD PASS under the body and descending toward its next contact.

Follow every guide anchor:
- The left hip-knee-ankle chain follows cyan. The left support foot is behind the body/root, remains the current support, and begins heel release while its toe stays near the y=233 ground.
- The right hip-knee-ankle chain follows orange. The right knee and ankle have crossed in front toward screen-right. The whole right shoe is airborne only about 6px above the ground, low and descending.
- Both finished sneaker toes point screen-right. On both shoes, the red heel/back is on screen-left and the white toe/front is on screen-right.
- This is a low passing/landing-preparation pose, NOT two flat feet, contact, a forward kick, high-knee march, run, or wide split.
- Use the LEFT cell's exact slim dark navy trousers, small red/white/navy sneakers, outline thickness, shading, and child proportions. No purple/beige pants, oversized shoes, smooth realistic painting, or scale change.
- The east-facing torso, pelvis, trouser folds, knees, shins, ankles, and shoes must read as one coherent body. No mirror, backward-bent ankle, double shoe, overlap, welding, ghost pixels, or owner swap. Preserve a visible transparent gap between the legs/shoes at the crossing.

Do not alter the LEFT cell. Do not add text, markers, arrows, floor, shadows, panels, or scenery. Return the complete 512x256 two-cell sheet on pure green.
```

거부 이유: P2 low-pass가 골반 아래로 충분히 들어오지 않고 보폭·발 높이가 커서 locked target과 다르다.
