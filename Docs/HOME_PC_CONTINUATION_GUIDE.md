# 다른 PC에서 이어서 개발·실행하기

이 안내는 특정 날짜의 작업 폴더를 복제하는 문서가 아니라, 어느 Windows PC에서든 현재 `main`을 안전하게 받아 실행하는 절차다.

## 인계 상태

- R18 arrival `ce9e3ae4d94a7365c0447103d2ad904013ef58a1`는 독립 static과 Unity `6000.3.21f1` capture-free Player exit 0을 통과해 integration에 단일 merge되었다. 네 actor Work 0..5, 동일 좌석 atomic 정렬, exit/turn/first-walk, stationary endpoint, safe egress, furniture 0 오차를 확인했다.
- 로컬 과거·회귀 실행 payload는 repo 밖 evidence를 보존한 뒤 허용 root에서 Recycle Bin 우선으로 제거했다. 이전 GitHub history·tags·Releases·Actions 감사의 executable payload는 0이며, `da5c6e7f9f9d48f0eada245cff727435536c91dd`의 CI guard가 강제 add와 renamed Player bundle을 차단한다.
- 이 PC에는 이미 실행 payload가 두 개 있다. 저장소 `Builds/Windows/FamilyCompany_Playtest`는 clean HEAD `8fa5fa74`의 Release build이고, 배포본 `%USERPROFILE%\Downloads\Family`는 한 커밋 앞선 `befe937e`다. 두 SHA가 다르므로 배포본 실행 결과를 HEAD의 증거로 쓰지 않는다.
- 다른 PC에서는 아래 절차로 clean 최종 HEAD를 받은 뒤 새 build identity를 만들며, 기존 실행본을 복사해 재사용하지 않는다.

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

빌드·실행 전에 [REGRESSION_BUILD_POLICY.md](REGRESSION_BUILD_POLICY.md)를 읽고 현재 local/Downloads payload의 `BUILD_INFO.txt`, manifest, 독립 gate evidence를 확인한다. user-visible regression, failed gate, stale/unverified provenance, self-PASS-only candidate는 실행하지 않는다. exact-root fence로 해당 payload만 확정하고 SHA/log/manifest 같은 비실행 evidence를 먼저 보존한 뒤 EXE, `*_Data`, `UnityPlayer.dll`을 포함한 전체 실행 payload를 즉시 삭제한다. 이름 변경이나 quarantine만으로 current에 남기지 않는다.

```powershell
.\BUILD_WINDOWS.cmd
.\RUN_WINDOWS.cmd
```

- 실행 파일: `Builds/Windows/FamilyCompany_Playtest/FamilyCompany.exe`
- 출처 파일: `Builds/Windows/FamilyCompany_Playtest/BUILD_INFO.txt`
- `BUILD_INFO.txt`의 commit이 `git rev-parse HEAD`와 같은지 확인한다.
- `Builds/`는 Git에 포함되지 않으므로 다른 PC의 오래된 EXE가 자동 갱신되지 않는다.
- provenance나 gate 결과가 없거나 `PENDING`이면 실행하지 말고 정책에 따라 evidence 보존 후 해당 payload만 삭제한다. unrelated build, source, AppData save는 삭제하지 않는다.

상세 옵션과 오류 해결은 [PLAYTEST_BUILD.md](PLAYTEST_BUILD.md)를 따른다.

새 build는 관련 regression oracle 전부, 기존 필수 gate, 독립 gate를 통과하고 새 build identity를 발급받아야 한다. 필수 출근 oracle은 fresh 08:50 state에서 `player` 09:00, `older_sister` 09:01, `father` 09:02, `mother` 09:03의 실제 release·이동·assigned seat 착석을 확인한다. 실패한 payload를 재승격하거나 staging/cache에서 재사용하지 않는다.

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
5. build·실행·배포 작업이면 [REGRESSION_BUILD_POLICY.md](REGRESSION_BUILD_POLICY.md)

현재 통합 완료와 대기 상태는 `PROJECT_STATE.md`만 따른다. `History/Reports/`의 완료 보고서는 당시 증거이며 최신 완료 선언이 아니다.

실제 회사 History 데이터는 사용자가 명시적으로 요청한 경우에만 [CLAUDE_HANDOFF_HISTORY_DATA.md](CLAUDE_HANDOFF_HISTORY_DATA.md)의 전용 경계를 따른다.

## 6. 최종 push 전 remote zero-inventory

다른 PC의 checkout/pull/tag/release 다운로드로 회귀·구 실행본이 다시 들어오지 않도록 [REGRESSION_BUILD_POLICY.md](REGRESSION_BUILD_POLICY.md)의 remote zero-inventory gate를 먼저 수행한다.

- repository root `.gitignore`가 실제 build/deploy output root와 representative EXE/`*_Data`/`UnityPlayer.dll`/archive 경로를 막는지 확인한다.
- `.gitignore`와 별개로 local candidate tree와 tracked file에 build payload가 없는지 확인한다. 이미 tracked된 payload는 exact 일반 cleanup commit으로 삭제한다.
- fresh remote observation으로 `origin/main`, 모든 active remote branch, 모든 tag tree, draft/prerelease를 포함한 모든 release asset을 끝까지 inventory한다.
- prohibited payload와 unknown identity가 각각 0이고 ref/tree/release asset 목록이 비실행 manifest에 기록되어야 PASS다.
- remote current ref에 payload가 있으면 feature/release push를 중단하고 별도 승인된 cleanup 작업 후 remote를 다시 검사한다.

history rewrite나 force-push는 이 정리의 단축 경로가 아니다. exact object/ref reachability audit, 검증된 offline backup과 restore rehearsal, collaborator의 clone/worktree/CI 및 미push 변경 조사, re-clone 영향·중단 시간·rollback 계획을 승인받기 전에는 수행하지 않는다.

## 7. 작업 종료

```powershell
git status --short
git diff --check
git add <이번 작업 파일만>
git commit -m "<작업 요약>"
git push origin main
```

push 전 검증 결과와 남은 작업을 `PROJECT_STATE.md`의 현재 항목에 반영한다. 다른 작업자의 파일이나 `Library`, `Temp`, `Logs`, `Builds`를 commit하지 않는다.

build/deploy 자동화는 regression을 발견하면 fail closed하고, evidence 보존 뒤 해당 실행 payload 삭제와 검증된 정상 build rollback(없으면 current empty)을 완료해야 한다. 이 계약의 구현·독립 테스트가 확인되지 않은 자동화로 current/Downloads를 갱신하지 않는다.

zero-inventory manifest가 없거나 remote branch/tag/release pagination이 불완전하면 최종 push를 진행하지 않는다. history rewrite/force-push 승인은 일반 cleanup commit이나 일반 push 승인으로 갈음할 수 없다.
