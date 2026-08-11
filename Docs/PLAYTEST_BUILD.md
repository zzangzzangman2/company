# PLAYTEST BUILD

Unity 없이 바로 실행되는 Windows 플레이테스트 빌드를 만드는 방법이다. 빌드 산출물 자체는 Git에 넣지 않고, 누구나 같은 EXE를 다시 만들 수 있도록 절차와 지문만 저장소에 남긴다.

## 산출물을 Git에 넣지 않는 이유

현재 `FamilyCompany_Playtest`는 151개 파일, 총 588,535,534 bytes이며 그중 `sharedassets0.assets.resS` 336,068,608 bytes, `resources.assets.resS` 127,765,088 bytes, `UnityPlayer.dll` 36,734,888 bytes가 대부분이다. 이 용량을 Git 히스토리에 넣으면 되돌릴 수 없고 이후 모든 clone이 계속 내려받는다. 빌드 절차와 출처 지문을 문서로 보존하고 실행본은 Downloads에 두는 방식을 유지한다.

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

- 실행 파일: `C:\Users\godho\Downloads\FamilyCompany_Playtest\FamilyCompany.exe`
- 빌드 시각: 2026-08-11 09:58:39 KST
- Unity: `6000.3.21f1 (c02631ffc030)`
- 기록된 HEAD: `a18d3fc2b060dd4b686b7a2008076e6f8f102de6`, 브랜치 `main`
- 빌드 상태: `Succeeded`, 파일 151개, 총 588,535,534 bytes
- 빌드 지문: `4D92E3966EF2408F3F03CDC3564F1ABCFEA624F9532E9F261CDB5B618A138451`
- EXE SHA-256: `48EFAB523AA684C653BD1254A6962D3410127B5C02DC1310F6F16F4810666556`

이 빌드는 상태 기록상 `dirty=True`인 정본 작업 폴더에서 생성됐다. `a18d3fc`의 좌석·가구 회피 이동·관리 UI v2·폰트/이동 결함 수정뿐 아니라, 당시 작업 폴더에 있던 Claude의 LiveContent 계층 A 미커밋 변경도 포함한다. 따라서 해당 변경을 검토·커밋하기 전에는 HEAD만으로 이 EXE를 완전히 재현할 수 있다고 간주하지 않는다. `build-stamp.json`은 아직 없다.

## 회사 PC에서 백그라운드 검증

사용자가 회사에서 일하는 동안에는 Unity Editor 창이나 `FamilyCompany.exe`를 전면에 띄우지 않는다.

- 컴파일·로직 검증은 Unity `-batchmode -nographics -quit`로 실행한다.
- 실제 렌더·PlayMode 캡처는 `-batchmode`를 사용하되 `Camera.Render`가 필요하면 `-nographics`를 쓰지 않는다.
- 검증 coroutine이 끝나기 전에 종료될 수 있는 작업은 `-quit`를 쓰지 않고, 완료 로그를 감시한 뒤 Unity가 스스로 종료하게 한다.
- 명령 반환만 보고 성공 처리하지 않는다. 자동화 로그와 Unity 로그에서 명시적 PASS/FAIL, 컴파일 오류, 예외와 최종 종료 코드를 확인한다.
- 사용자 조작이 필요한 EXE 육안 검증은 자동 실행하지 않고 먼저 사용자에게 알린다.
