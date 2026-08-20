# HOME PC WALK CHECKPOINT — 2026-08-20

이 문서는 회사 PC에서 멈춘 주인공 걷기 작업을 집 PC에서 **같은 판단 기준과 같은 입력으로** 이어가기 위한
단일 체크포인트다. 충돌하는 과거 v4~v13 메모나 3D bake 문서보다 이 문서와
`CHARACTER_LOCOMOTION_GENERATION_V1.md`가 우선한다.

## 한 줄 상태

Mixamo/KShopGo 동작 추적과 2D foot-lock 가이드는 PASS했지만 완성 하체 아트는 아직 없다. 따라서 게임은
계속 `Legacy48`을 쓰며, 오늘 만든 ImageGen/raster-warp 결과는 전부 거부 상태로 보존했다.

## 완료된 것

- KShopGo APK를 다시 확인했다: Walk `0.80000007s`, 30fps, 24샘플, loop, in-place.
- 6포즈 timing을 `0/4/8/12/16/20`으로 고정했다.
- KShop 비교 GIF는 133.3ms/pose지만 실제 project는 speed `1.0`/stride `0.99380799`의 distance phase라
  정속 약 `165.635ms/pose`, `0.99380799s/cycle`이다. 두 값을 섞어 foot slide를 만들지 않는다.
- Mixamo `Unarmed Walk Forward`에서 east +90° raw 관절을 Unity로 추출했다.
- source clip `1.3666668s`, left-contact phase zero `0.2961111s`를 확인했다.
- physical left/right owner, contact 교대, 반대 팔·다리, swing 진행 방향은 raw trace에서 PASS했다.
- 우리 runtime stride `0.99380799`, 180 PPU, visual scale `1.55`를 적용해 pose당 root advance를
  `19.234993px`로 고정했다.
- heel/toe roll을 분리한 world contact drift 최대값은 `0.765007px`로 허용값 `<=1px`를 통과했다.
- 자유 보행의 연속 방향 전환·짧은 이동 규칙은 `OfficeRuntimeAgent` 공용 경로라 가족/NPC에도 적용된다.
  이번 **아트 제작 범위만** 사용자 지시대로 주인공 east 6장에 제한하며 `simulation-pure` 회귀는 PASS했다.
- 파생된 target JSON, phase contract, guide, source upper 6장, 실패 이미지를
  `ArtSources/PlayerEastMixamoTraceV2/`에 추적했다. raw joint export는 공개 Git에 넣지 않으며 exporter가
  ignored `Artifacts/PlayerEastMixamoTraceCandidate/`에 동일하게 재생성한다.
- 회사 PC의 V10/Humanoid generated `Assets/Resources` 4개 폴더는 production에 섞이지 않도록 삭제하지 않고
  ignored `Artifacts/RejectedRuntimeCandidates20260820/`로 옮겼다. Git에는 재현 source·도구·대표 실패
  증거만 넣으며 집 PC에서 이 거부 runtime payload를 복원할 필요가 없다.

## 완료되지 않은 것

- P0~P5 완성 lower-body PNG 6장은 아직 없다.
- Unity 후보/카탈로그/기본 presentation mode는 갱신하지 않았다.
- KShop 비교용 0.8s GIF, 실제 runtime `0.99380799s` GIF 사용자 승인과 Windows Player 캡처는 아직 없다.
- 다른 7방향과 가족 3명 확대는 시작하지 않았다.

이 네 항목이 남아 있으므로 “새 보행 완료” 또는 “shipping PASS”라고 기록하면 안 된다.

## 왜 기존 후보를 더 고치지 않는가

사용자 지적대로 v10 이후 실패는 접지 높이 하나가 아니었다.

- 화면 좌/우를 physical leg ID로 써서 crossover에서 owner가 뒤집혔다.
- `FootCutY` 아래 신발/종아리 조각을 옮겨 기존 발목 위에 덮어 이중 발목과 꺾인 실루엣이 생겼다.
- P4에서는 양 신발 픽셀이 실제 한 connected component로 용접됐다.
- 미러된 골반·무릎 위에 east 신발만 붙여 상체와 하체 방향이 달랐다.
- P2/P3, P5/P0 contact 복제로 움직임 없이 내부 색만 바뀌는 micro-stutter가 생겼다.

V3도 lower donor로는 불충분하다. P0 앞발이 바닥에서 뜨고, P2 lower는 P0과 IoU `0.862`인 wide-contact
반복형이며 shoe collar 간격이 약 `59.6px`라 locked P2 low-pass와 맞지 않는다. 기존 V2, NaturalWalk,
HighMotion, transition, V13, ImageGen draft에도 온전한 `pelvis→toe` donor가 없다.

## 집 PC 첫 명령

```powershell
$projectRoot = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Documents\Codex\family_company_unity'
Set-Location $projectRoot
git switch main
git status --short --branch
git pull --ff-only origin main
git rev-parse HEAD
```

그 다음 아래 추적 파일이 있는지 확인한다.

```powershell
Get-ChildItem .\ArtSources\PlayerEastMixamoTraceV2 -Recurse
```

## raw trace와 가이드 재생성

하체 저작은 tracked target/guide로 바로 이어갈 수 있다. 아래는 FBX부터 raw trace와 가이드를 다시 감사할 때만
실행한다. Unity Editor를 먼저 닫는다.

```powershell
python --version
python -c "import numpy, PIL; print('walk trace deps: PASS')"
```

두 번째 명령이 실패하면 해당 Python 환경에 `numpy`와 `Pillow`를 설치한 뒤 진행한다.

```powershell
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

반드시 아래 PASS가 나와야 한다.

```text
PLAYER_EAST_MIXAMO_TRACE: PASS | poses=6 rootAdvance=19.234993px maxContactDrift=0.765007px
```

## 다음 구현 순서

1. `ArtSources/PlayerEastMixamoTraceV2/player-east-locked-skeleton-guide.png`와
   `target-joints.json`을 연다.
2. `SourceV3Frames/`의 각 P0~P5 상체는 phase별로 그대로 유지한다. 손/소매가 y171 아래로 내려오는
   프레임이 있으므로 단순 수평 cut으로 잘라내지 않는다.
3. 하체 6장을 각각 새로 저작한다. 한 다리는 항상 같은 physical owner의
   `hip→knee→ankle→heel/toe` 연결로 그린다.
4. P0/P3은 lead heel + trailing toe, P1/P4는 flat support + opposite recovery,
   P2/P5는 terminal toe + opposite low pass다.
5. debug 사본에는 left cyan/right orange를 유지하고, final 사본에서만 실제 바지/신발 색으로 flatten한다.
6. 검정 배경 close-up에서 orphan pixel, double ankle, shoe welding, 반대 방향 sole이 0인지 검사한다.
7. root advance를 합친 support heel/toe drift `<=1px`, swing +X 단조, support -X 단조를 자동 검사한다.
8. KShop 비교용 133.3ms×6 GIF와 실제 project 정속 165.635ms×6 GIF를 모두 만든다. 최종 승인은 실제
   runtime timing/이동과 결합한 east GIF·Player 캡처로 받는다.
9. 승인 뒤에만 격리된 Unity candidate에 복사하고 actual normal D3D11 Player를 캡처한다.
10. east가 통과한 뒤에만 다른 방향과 가족을 진행한다.

## 절대 하지 않을 것

- `Build-PlayerEastFootForwardV5.ps1`, V10~V13 조립물을 새 동작 donor로 사용
- lower 전체 mirror 또는 screen-left/right로 owner 결정
- 신발·밑창·종아리 조각만 이동
- `build_player_east_mixamo_locked_art_v2.py` 결과를 candidate로 승격
- ImageGen 결과를 skeleton 검사 없이 PNG로 채택
- 새 east 승인 전에 `Legacy48` 기본값 변경
- `Invoke-PlayerWalkHumanoidPipeline.ps1` 실행 또는 `PlayerWalkHumanoidPromotion` 직접 호출. 폐기된 3D
  promotion 경로는 우회 없이 차단되어 있다.

## 파일 지도

| 파일 | 용도 |
| --- | --- |
| `ArtSources/PlayerEastMixamoTraceV2/README.md` | 입력 provenance, 정확한 ImageGen 편집 prompt, 거부 사유 |
| `ArtSources/PlayerEastMixamoTraceV2/target-joints.json` | 최종 6포즈 2D 관절 좌표 |
| `ArtSources/PlayerEastMixamoTraceV2/phase-contract.md` | timing·root·foot-lock 수치 |
| `Tools/build_player_east_mixamo_trace_v1.py` | 가이드 생성 + fail-closed 검증 |
| `Assets/FamilyCompany/Editor/PlayerWalkMotionReferenceExporter.cs` | Mixamo raw trace Unity exporter |
| `Docs/KSHOPGO_MOVEMENT_TEARDOWN.md` | APK 분석과 2D에 가져올 동작 원칙 |
| `Docs/CHARACTER_LOCOMOTION_GENERATION_V1.md` | production 제작/승격 gate |

## 현재 검증 증거

- `FC-WALK-GUARDRAIL-V1 확인: 0/3 해부학적 앞발 교대, 2/5 낮은 통과발 교대, 짧은 보폭, 동일 실루엣, 별도 전환 그림 금지, actual normal EXE 판정 전 미배포.`
- Unity narrow exporter: `PLAYER_WALK_MOTION_REFERENCE: PASS`.
- Python trace contract: `PLAYER_EAST_MIXAMO_TRACE: PASS`, poses=6, max drift `0.765007px`.
- shared movement rules: FastQA `simulation-pure` PASS.
- 세 ImageGen 편집과 raster-warp V2: 시각 감사 FAIL, 저장만 하고 미승격.
- full Unity build/Windows Player: 이번 체크포인트에서는 실행하지 않았고 PASS를 주장하지 않는다.
