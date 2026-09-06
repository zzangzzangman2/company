# 의자 타일 중앙 / 착석 fitting / 실제 Unity 로컬 패치

검사 소스: `6ce5e0eb3c4e06526ee3c3b5706e5649d552daf7`, Unity `6000.3.21f1`.
작업 위치: `C:/Users/godho/Documents/Codex/fc_agents/integration_p0`, 정본 `main`.
사용자 최신 지시: **백그라운드만, 데스크톱/마우스/키보드 조작 금지**.
이 폴더에는 실행 파일이 없으며 모든 테스트는 `Artifacts`의 격리된 실행본으로 수행했다.

## 결과와 한계

- 의자 기둥/바닥판의 중심은 `seat.Cell`의 바닥 중앙. 높이가 있는 구형 sprite socket을 바닥으로
  투영하지 않는다. 모니터와 키보드는 의자 앞의 책상 타일 중앙축에 맞춘다.
- `geometry.json`: orthogonal bake + mapped production basis 각각 네 회전, 총 8건. 실제 mesh 기둥
  오차 0 world, 화면/키보드의 옆축 오차는 부동소수점 범위, 모니터 법선 오차 0°.
  `earlier-normal-chair-centres.csv`: 실제 production 카메라의 네 방향 최대 오차 0.484345px
  (pixel snapping 포함). 이 이전 실행의 팔 fitting은 실패했으며 현재 포즈의 근거로 재사용하지 않는다.
- 책상은 기존 2칸의 깊이를 온전히 사용하고 키보드를 앞쪽 안으로 둔다. 의자 모양/부품/PNG,
  점유 3칸, 배우 스케일/뼈 길이/골반 높이는 유지한다. 손 도달에 필요한 spine lean은 최대 35°.
  목표 무릎 95°로 실제 다리 길이에 맞춰 발목 높이를 정하되 기존 최소 높이보다 내리지 않는다.
- `chair-fit.csv`: Player/Father 각각 네 방향. 최대 개별 손목 오차 0.0081 world,
  Player 실제 무릎 약 95°, Father 99.18°/99.65°, 의자 각 부품 안의 skinned 정점은 모두 0.
  `chair-fit-*.png` 8장은 게임 엔진의 offscreen camera-stack 원본이다. IMGUI는 포함하지 않는다.
- **착석은 한 시점에 production 포즈 함수를 직접 호출한 검사다.** `poseInjection=true`이며
  자율 접근/연속 타이핑/실제 클릭/사용자 외형 승인/Release를 검증한 것이 아니다.
  `opening-shop-final.txt`, `player.log`는 이 제한을 명시하며 runtime errors=0을 기록한다.
  종료 시 ComputeBuffer 해제 경고 1건은 남아 있다. 무경고 실행이라고 보고하지 않는다.
- `build-result.json`: 최종 scripts-only FAST_QA 20.117초 PASS. cache metadata의 head는 데이터
  빌드 기반 bed9949d를 유지하므로 이를 최신 script HEAD와 혼동하지 않는다.
- 가격 400,000원/초기 자금 5,000,000원 유지. 실제 구매 controller의 네 회전 확정 후 잔액
  3,400,000원, 의자 4개, inventory 8개, 겹치는 구매는 차감/변경 0. 모두 **programmatic confirm**.
- 제목/가격/잔액/방향/배치 오류 텍스트와 버튼 영역을 수정했다. 최신 상세창의 실제 IMGUI 픽셀
  검사는 사용자 화면 조작 금지 때문에 수행하지 않았다. 모든 글씨의 시각 PASS가 아니다.
- `earlier-normal-work-FAIL.txt`는 숨기지 않은 이전 실패다. 정상 coordinator에서 네 명이 동시에
  Working에 도달하는 100초 검사가 시간 초과됐다. 원인 미확정, 최신 소스에서 정상 업무 재검증 필요.

## 실제 Unity 로컬 다운로드/재시작

`patch/identity.json`의 실행 경로는 `Artifacts/UnityPatchRestartTests/6ff58f22bd39406eb9205400aa49d31d`.
원본 실제 Unity seed에서 데이터 파일 하나를 바꾼 패치를 만든 뒤 숨김 Unity 부모가 변경 파일을
받아 검증하고 정상 종료했다. 재시작 helper가 정확한 새 snapshot을 활성화하고 Unity 자식을 시작했다.

- `patch/measured-progress.json`, `parent-progress.txt`: 실제 변경 압축 4,195,602바이트,
  다운로드 131개 샘플 모두 실제 바이트 분모/소수 내림/단조 증가 일치, 최종 100.0%.
- `patch/manifest.json`: 실제 검증 대상 1,036,399,960바이트. 실행 스크립트가 전체 파일 hash를 재검증.
- `patch/restart-observed.json`: 부모 정상 종료 0, 정확한 child path/PID 5780, 원래 main-entry SHA 불변.
- `patch/child-ready.txt`: 새 Unity가 전 파일 검증 후 `IN_GAME_PATCH_READY_CURRENT` 도달.
  `-familyCompanyPatchBackgroundExit`는 이 테스트에만 명시돼 부팅 확인 후 정상 종료한다.
- `patch/updater-regressions.json`: inert-fixture 51개 PASS.
  `patch/restart-guard-regressions.json`: windowless probe 10개 PASS (Unity 검사와 별도).

**로컬 전송만 사용했다. GitHub Release/인터넷 다운로드/현재 문구의 presented frame 검사는 아니다.**
`actualUnityUi=true` 로그도 headless IMGUI 픽셀 증거가 아니다. 원래 사용자 Downloads/main 설치나
실제 AppData 패치 저장소는 바꾸지 않았다. 공개 game Release/productionEligible는 여전히 false.

## 재현 및 다음 작업

정본 저장소에서 먼저 상태/가이드 확인. 반복 컴파일은 warm cache를 유지한다.

```powershell
.\FAST_QA_WINDOWS.cmd -Profile player-scripts -NoPlayerSmoke
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Updater/Test-FamilyCompanyUnityRestart.ps1 -Background
```

착석 전용 EXE 인자(실행은 `ProcessStartInfo`의 hidden/no-window로):
`-batchmode -force-d3d11 -familyCompanyOpeningShopQa -familyCompanyChairFitQa -familyCompanyOpeningShopArtifacts <새 절대 evidence 경로> -logFile <절대 로그>`.
정상 업무 재현은 `-familyCompanyChairFitQa`를 **빼고**
`-familyCompanyBackgroundChairObservation <새 절대 observer 경로>`를 추가한다.
이 정상 관측의 실패 원인을 독립적으로 확인해야 한다. 포즈 주입으로 통과시키거나 observer gate를
낮춰 정상 gameplay라고 보고하면 안 된다.

화면 조작 없이 할 수 있는 진단까지만 진행하며, native 구매/UI·연속 업무·다음날 출근·mute와
사용자 승인이 남은 상태에서 GitHub game Release나 Downloads 승격/PC 종료를 실행하지 않는다.
소스 push는 게임 패치 공개와 별개다.
