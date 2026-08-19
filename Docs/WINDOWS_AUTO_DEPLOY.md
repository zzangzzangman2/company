# WINDOWS AUTO DEPLOY

clean integration HEAD를 Windows Release player로 만든 뒤 사용자의 Downloads에 안전하게 승격하는 절차다.
자동화와 격리 dry-run은 검증되어 있고 실제 1회 배포도 수행된 적이 있다. watcher 상시 구동은 최종 QA를
통과한 clean HEAD에서만 시작한다.

## 최종 대상

현재 실제로 사용하는 배포 경로는 다음이다.

```text
C:\Users\godho\Downloads\Family\FamilyCompany.exe
```

`Tools/FamilyCompanyBuild.Common.ps1`의 `Get-FamilyCompanyDeployDefaults`는 아직 옛 이름
`Downloads\FamilyCompany_Playtest`를 기본값으로 갖고 있다. 기본값을 그대로 쓰면 지금 배포본과 다른 폴더에
쓰게 되므로 배포는 `-TargetPath "C:\Users\godho\Downloads\Family"`를 명시해 호출한다.

배포 폴더에는 다음 파일이 함께 있어야 한다.

- `FamilyCompany.exe`
- `FamilyCompany_Data\`
- `UnityPlayer.dll`
- `BUILD_INFO.txt`
- `DEPLOY_MANIFEST.json`
- `DEPLOY_MANIFEST.txt`
- `RUN_WINDOWS.cmd`

게임의 저장 데이터는 이 폴더가 아니라 Windows `AppData`에 있다. 배포기는 사용자 저장 데이터를 읽거나
삭제하거나 초기화하지 않는다.

## 작동 계약

1. watcher는 `-RequiredBranch`로 지정한 branch의 clean committed HEAD만 관찰한다. 스크립트 기본값은 지금은
   존재하지 않는 `codex/integration-p0-qa`이므로, 정본 branch인 `main`을 쓰려면 `-RequiredBranch main`을
   명시해야 한다. 기본값 그대로 실행하면 `HeldWrongBranch`(35)로 멈춘다.
2. 배포 manifest의 commit과 HEAD가 같으면 아무것도 만들지 않는다.
3. 새 HEAD가 debounce 시간 동안 그대로이고 working tree가 clean일 때만 한 번 빌드한다.
4. untracked 파일을 포함한 미커밋 변경은 `HeldDirtyWorktree`, 미해결 merge는
   `HeldMergeConflict`, 다른 branch는 `HeldWrongBranch`로 기록하고 빌드하지 않는다.
5. `ProjectVersion.txt`와 실제 `Unity.exe`의 ProductVersion/revision이 모두
   `6000.3.21f1 (c02631ffc030)`인지 검사한다.
6. machine-wide Unity/build lock을 획득한 뒤 모든 기존 `Unity.exe`와 다른 QA player가 끝날 때까지 기다린다.
   현재 Downloads target player만 candidate 빌드 중 허용하고 승격 단계에서 대기한다. 어떤 외부 프로세스도
   종료하지 않는다.
7. 기존 `BUILD_WINDOWS.cmd`가 사용하는 `Build-FamilyCompanyWindows.ps1`과
   `WindowsPlayerBuild.BuildWindowsX64`가 비-Development Windows x64 candidate를 staging에 만든다.
8. EXE, Data, `UnityPlayer.dll`, build info, deploy manifest와 runner를 모두 검사한 뒤에만 같은 볼륨의
   디렉터리 rename으로 Downloads target을 교체한다.
9. 기존 target은 `<target-이름>.last-known-good.<UTC>.<old-commit>` 한 개로 보존한다. 현재 배포 경로에서는
   `Family.last-known-good.<UTC>.<old-commit>`이며, candidate 승격이 실패하면 기존 target을 즉시 복구한다.
10. target의 `FamilyCompany.exe`가 실행 중이면 종료하지 않는다. candidate와
    `AwaitingPlayerExit` 상태를 남기며 watcher가 종료 후 같은 candidate를 재사용해 승격한다.

## 명령

Downloads를 쓰지 않는 사전 점검:

```cmd
DEPLOY_WINDOWS.cmd --dry-run --no-pause
```

최종 QA가 끝난 뒤 명시적으로 watcher 시작/중지:

```cmd
START_WINDOWS_DEPLOY_WATCH.cmd
STOP_WINDOWS_DEPLOY_WATCH.cmd
```

한 번만 실제 빌드·배포하려면 다음을 사용한다.

```cmd
DEPLOY_WINDOWS.cmd --no-pause
```

watcher는 Windows 서비스나 시작 프로그램으로 등록되지 않는다. 시작 CMD를 실행한 현재 로그인 세션에서만
동작한다. 시작 명령은 PID, target, watcher lock, machine-wide Unity/build lock, status와 log 경로를 출력한다.

## 상태·로그

```text
Builds\Windows\Automation\Deploy\watch-status.json
Builds\Windows\Automation\Deploy\deploy-status.json
Builds\Windows\Automation\Deploy\logs\
%LOCALAPPDATA%\FamilyCompany\BuildAutomation\unity-build.lock
```

주요 종료 코드는 다음과 같다.

| 코드 | 의미 |
|---:|---|
| 0 | 성공, 이미 최신, 또는 dry-run PASS |
| 24 | 중복 watcher lock 거부 |
| 31 | dirty working tree 보류 |
| 32 | merge conflict 보류 |
| 33 | 중복 deploy lock 거부 |
| 34 | 게임 실행 중이라 candidate 승격 대기 |
| 35 | 허용되지 않은 branch |
| 36 | 빌드 도중 HEAD 변경 |

## 자동화 자체 dry-run 회귀

다음 검증은 실제 Unity를 실행하거나 Downloads를 쓰지 않는다.

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Test-FamilyCompanyDeployPipeline.ps1
```

공백·한글 경로, exact Unity binary metadata, clean/dirty/conflict, 변경 없는 HEAD skip, debounce,
중복 watcher, CMD 인수와 오류 코드, 불완전 candidate 차단, 승격 전·후 실패 rollback, LKG 1개, 실행 중 EXE 감지를
격리된 `Builds\Windows\Automation\DeployDryRunTest` 아래에서 검사한다.

## 잔여 위험과 최종 배포 전 조건

- dry-run은 실제 player build 시간, Windows Build Support 설치 상태와 실행 smoke를 대체하지 않는다.
- 후속 통합 뒤 chair/movement/performance/animation QA와 Windows D3D11 smoke를 통과시킨 clean HEAD에서만
  watcher 또는 실제 1회 배포를 시작한다.
- `Tools/Test-FamilyCompanyDeployPipeline.ps1`은 아직 옛 target 이름과 `codex/integration-p0-qa`를 인수로
  넘긴다. dry-run 회귀는 격리 폴더에서만 돌기 때문에 통과하지만 실제 배포 경로·branch의 검증은 아니다.
- 최종 실행본은 `DEPLOY_MANIFEST.json`의 SHA가 당시 integration HEAD와 같은지 다시 확인한다.
