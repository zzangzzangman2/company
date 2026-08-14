# 다른 PC에서 이어서 개발·실행하기

이 안내는 특정 날짜의 작업 폴더를 복제하는 문서가 아니라, 어느 Windows PC에서든 현재 `main`을 안전하게 받아 실행하는 절차다.

## 1. 준비

- Git
- Unity Hub와 Unity Editor `6000.3.21f1`
- Windows Standalone Build Support
- 저장소 접근 권한

처음 받는 PC에서는 원하는 작업 폴더에서 clone한다.

```powershell
$userProfilePath = [Environment]::GetFolderPath('UserProfile')
$projectRoot = Join-Path $userProfilePath 'Documents\Codex\family_company_unity'
git clone https://github.com/zzangzzangman2/company.git $projectRoot
Set-Location $projectRoot
```

이미 clone이 있다면 해당 저장소 루트에서 시작한다. 한 번에 한 PC/작업자만 `main`을 수정하고, 다른 PC로 옮기기 전에 이전 PC의 변경을 commit/push하거나 안전하게 보관한다.

## 2. 현재 main 받기

```powershell
git switch main
git status --short --branch
git pull --ff-only origin main
git rev-parse HEAD
```

`git status --short`에 예상하지 못한 파일이 보이면 pull, 삭제, restore, 일괄 stage를 진행하지 말고 먼저 소유권을 확인한다.

## 3. 가장 빠른 Windows 플레이테스트

저장소 루트에서 실행한다.

```powershell
.\BUILD_WINDOWS.cmd
.\RUN_WINDOWS.cmd
```

- 실행 파일: `Builds/Windows/FamilyCompany_Playtest/FamilyCompany.exe`
- 출처 파일: `Builds/Windows/FamilyCompany_Playtest/BUILD_INFO.txt`
- `BUILD_INFO.txt`의 commit이 `git rev-parse HEAD`와 같은지 확인한다.
- `Builds/`는 Git에 포함되지 않으므로 다른 PC의 오래된 EXE가 자동 갱신되지 않는다.

상세 옵션과 오류 해결은 [PLAYTEST_BUILD.md](PLAYTEST_BUILD.md)를 따른다.

## 4. Unity Editor로 열기

1. Unity Hub에서 저장소 폴더를 Add한다.
2. 반드시 `6000.3.21f1`로 연다.
3. 최초 import가 끝날 때까지 기다리고 Console compile error가 없는지 확인한다.
4. `Assets/FamilyCompany/Scenes/Prototype01.unity`를 연다.
5. Play 후 타이틀→사무실 흐름을 확인한다.

회사 PC에서는 Editor/EXE를 전면 실행하지 않는다. compile/순수 검증은 `-batchmode -nographics`, 실제 렌더 검증은 graphics가 활성화된 `-batchmode`로 실행한다.

## 5. 작업 전 읽을 문서

1. [AGENTS.md](../AGENTS.md)
2. [PROJECT_STATE.md](PROJECT_STATE.md)
3. [CANON.md](CANON.md)
4. 작업 분야의 정본 문서

현재 통합 완료와 대기 상태는 `PROJECT_STATE.md`만 따른다. `History/Reports/`의 완료 보고서는 당시 증거이며 최신 완료 선언이 아니다.

실제 회사 History 데이터는 사용자가 명시적으로 요청한 경우에만 [CLAUDE_HANDOFF_HISTORY_DATA.md](CLAUDE_HANDOFF_HISTORY_DATA.md)의 전용 경계를 따른다.

## 6. 작업 종료

```powershell
git status --short
git diff --check
git add <이번 작업 파일만>
git commit -m "<작업 요약>"
git push origin main
```

push 전 검증 결과와 남은 작업을 `PROJECT_STATE.md`의 현재 항목에 반영한다. 다른 작업자의 파일이나 `Library`, `Temp`, `Logs`, `Builds`를 commit하지 않는다.
