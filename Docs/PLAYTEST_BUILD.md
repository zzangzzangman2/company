# PLAYTEST BUILD

Unity 없이 바로 실행되는 Windows 플레이테스트 빌드를 만드는 방법이다. 빌드 산출물 자체는 Git에 넣지 않고, 누구나 같은 EXE를 다시 만들 수 있도록 절차와 지문만 저장소에 남긴다.

## 산출물을 Git에 넣지 않는 이유

`FamilyCompany_Playtest`는 147개 파일 201.8MB이며 그중 `sharedassets0.assets.resS` 48.5MB, `resources.assets.resS` 39.9MB, `UnityPlayer.dll` 35.0MB가 대부분이다. 이 용량을 Git 히스토리에 넣으면 되돌릴 수 없고 이후 모든 clone이 계속 내려받는다. 반면 아래 절차는 같은 커밋에서 같은 EXE를 다시 만들어 내므로, 바이너리를 보관하지 않아도 참조 목적이 충족된다.

## 필요한 것

- Unity 6000.3.21f1: `C:\Users\godho\Documents\Codex\UnityEditors\6000.3.21f1\Editor\Unity.exe`
- 정본 작업 폴더: `C:\Users\godho\Documents\Codex\family_company_unity`

빌드 스크립트는 `Assert-CanonicalProjectPath`로 정본 경로를 강제한다. `family_company_worktrees` 아래의 통합 worktree에서는 빌드가 거부되므로, 통합 브랜치를 정본 작업 폴더에 합친 뒤 빌드한다.

## 빌드 명령

```powershell
powershell -ExecutionPolicy Bypass -File C:\Users\godho\Documents\Codex\family_company_unity\Tools\Build-FamilyCompanyWindows.ps1
```

기본값만으로 동작하며 인자는 필요할 때만 덮어쓴다.

| 인자 | 기본값 |
| --- | --- |
| `-CanonicalProjectPath` | `C:\Users\godho\Documents\Codex\family_company_unity` |
| `-UnityEditorPath` | `C:\Users\godho\Documents\Codex\UnityEditors\6000.3.21f1\Editor\Unity.exe` |
| `-FinalOutputPath` | `C:\Users\godho\Downloads\FamilyCompany_Playtest` |
| `-AutomationRoot` | `C:\Users\godho\Downloads\FamilyCompany_BuildAutomation` |
| `-UnityWaitTimeoutMinutes` | `120` |

실행 결과는 `C:\Users\godho\Downloads\FamilyCompany_Playtest\FamilyCompany.exe`다. Unity를 열지 않고 이 파일만 실행하면 된다.

## 구성 요소

- `Tools/Build-FamilyCompanyWindows.ps1`: 배타 잠금, 스테이징, 이전 빌드 백업, 상태 기록까지 담당하는 진입점
- `Tools/FamilyCompanyBuild.Common.ps1`: 정본 경로·Unity 버전 검증과 커밋 지문 계산
- `Assets/FamilyCompany/Editor/WindowsPlayerBuild.cs`: Unity 쪽 실제 플레이어 빌드
- `Tools/Start-FamilyCompanyBuildWatch.ps1`, `Watch-FamilyCompanyBuild.ps1`, `Stop-FamilyCompanyBuildWatch.ps1`: 커밋 변화를 감시해 자동 재빌드

## 상태 확인

`C:\Users\godho\Downloads\FamilyCompany_BuildAutomation\build-status.json`에 `state`, `head`, `branch`, `fingerprint`, `finalPath`, 그리고 automation/unity 로그 경로가 기록된다. `logs/`에는 회차별 로그가 최대 30개 남는다. 이미 성공한 지문과 같으면 재빌드를 건너뛴다.

동시에 두 빌드가 돌지 않도록 `build.lock`으로 배타 잠금을 잡고, 잠겨 있으면 exit 23으로 `SkippedLocked`를 남긴다.

## 현재 보관 중인 빌드의 출처

`C:\Users\godho\Downloads\FamilyCompany_Playtest`에 있는 빌드는 다음 기준이다.

- 커밋 `d07638ad7ac06f0c940a436be3ace8f41b5fe152`, 브랜치 `agent/contract-lifecycle-v0-3`
- 지문 `872070D3F59A1F9C91318F22062F4A67529CD21F90EA7AFA2A5103BE90983469`
- 빌드 시각 2026-08-10T11:46:08Z, `state: Succeeded`

주의: 이 빌드는 2026-08-11 좌석·가구 회피 이동·관리 UI v2·행동/UI 아트 통합 **이전** 커밋에서 만들어졌다. 따라서 오늘 고친 폰트 폴백 테이블 결함과 이동 교착 수정, 새 관리 UI와 행동 아트는 이 EXE에 들어 있지 않다. 오늘 작업을 실제로 확인하려면 통합 커밋을 정본 폴더에 합친 뒤 위 명령으로 다시 빌드한다.
