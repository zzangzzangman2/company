# 집 PC에서 가족회사 개발 이어가기

이 문서는 집 PC에 개발 환경을 설치하고, 회사 PC의 작업물을 안전하게 이관하며, `main` 한 곳에서 한 작업씩 순차 진행하는 절차만 다룬다. 기능별 완료·진행·미완료 현황은 이 문서에 복사하지 않는다. 작업을 시작할 때마다 [PROJECT_STATE.md](./PROJECT_STATE.md)와 [CLAUDE_HANDOFF_HISTORY_DATA.md](./CLAUDE_HANDOFF_HISTORY_DATA.md)를 정본으로 다시 읽는다.

현재 회사 PC의 저장소 예시는 `C:\Users\godho\Documents\Codex\family_company_unity`이다. 집 PC의 사용자명, 드라이브, 저장 위치는 달라도 된다. 아래 명령은 저장소 루트에서 실행하는 것을 기본으로 한다.

2026-08-11 기준으로 가족 4인의 좌석 애니메이션 448프레임, 좌석 런타임, 가구 회피 이동, 관리 UI v2, Stock 회사계좌·Save 자동 연결은 공유 `Prototype01`에 통합되어 30초 PlayMode를 통과했다. 세부 미완료와 재개 순서는 [PROJECT_STATE.md](./PROJECT_STATE.md)를 따른다.

## 1. 먼저 한쪽 PC의 작업을 멈추고 상태를 기록한다

집 PC로 옮기기 전에 회사 PC의 Unity를 완전히 종료하고, 파일을 수정 중인 Codex 작업도 모두 멈추거나 결과를 회수한다. 두 PC에서 같은 파일을 동시에 편집하지 않는다. OneDrive 같은 동기화 폴더에서 두 Unity가 같은 프로젝트를 동시에 열게 해서도 안 된다. 특히 씬, `.meta`, `Packages`, `ProjectSettings`, `Library`가 서로 다른 시점으로 섞이면 복구가 어렵다.

이 저장소는 이 문서 작성 시점에 **미커밋·미추적 파일이 매우 많은 dirty worktree**였다. 따라서 먼저 백업하고, 현재 브랜치·커밋·변경 범위를 기록한다. 아래 명령은 읽기 전용이다.

```powershell
$repo = (Get-Location).Path
git status --short --branch
git branch --show-current
git rev-parse HEAD
git diff --stat
git diff --cached --stat
```

출력이 예상과 다르거나 현재 작업 범위 밖의 변경이 섞여 있으면 커밋, pull, 복사를 서두르지 말고 현재 채팅에서 파일별 생성 시각과 소유 범위를 먼저 확인한다. dirty 상태인 동일 작업 폴더에서 브랜치를 바꾸지 않는다. 비밀번호, 접근 토큰, 개인키는 저장소·패치·전송 폴더에 넣지 말고 Git 자격 증명 관리자를 사용한다.

## 2. Unity Hub와 필수 도구를 설치한다

`ProjectSettings/ProjectVersion.txt`에 고정된 정확한 버전은 다음과 같다.

- Unity Editor: **6000.3.21f1**
- Revision: `c02631ffc030`

Unity Hub의 `Installs`에서 이 버전을 설치한다. 목록에 보이지 않으면 Unity Download Archive에서 같은 버전을 Hub로 연다. 다른 6000.x 버전으로 먼저 열어 자동 업그레이드하지 않는다.

Windows 설치 항목은 다음처럼 구분한다.

- 필수: `Unity 6000.3.21f1` Windows Editor 본체. 현재 프로젝트를 열고 Import·C# 컴파일·Editor PlayMode를 실행하는 데 필요하다.
- 권장: Visual Studio 2022 또는 다른 C# 편집기. Unity 코드 탐색과 디버깅용이며, 편집기는 한 종류만 있어도 된다.
- 조건부: `Windows Build Support (IL2CPP)`. 현재 `ProjectSettings`에는 IL2CPP가 명시적으로 고정되어 있지 않으므로 단순 Editor 실행과 Play 점검에는 불필요하다. 이후 Windows 배포 빌드를 IL2CPP로 전환할 때만 추가한다. 그때는 Visual Studio의 **Desktop development with C++**와 Windows SDK도 설치한다.
- 불필요: Android, iOS, WebGL, Dedicated Server용 Build Support. 해당 플랫폼 빌드가 실제 일정에 들어올 때 추가한다.
- 선택: Unity Documentation, 언어 팩. 오프라인 도움말이나 개인 편의가 필요할 때만 설치한다.
- 조건부: .NET 8 SDK. Unity를 여는 데는 필요 없지만, 저장소 밖 독립 C# 검증 하네스나 `work/**`의 `net8.0` 프로젝트를 직접 실행할 때 필요할 수 있다.

Unity Hub는 나중에도 같은 Editor 버전에 모듈을 추가할 수 있다. IL2CPP 선택과 Windows 도구 요구사항은 [Unity Hub 설치 안내](https://docs.unity3d.com/6000.1/Documentation/Manual/GettingStartedInstallingUnity.html)와 [Unity 6 Windows 요구사항](https://docs.unity3d.com/6000.0/Documentation/Manual/system-requirements.html)을 기준으로 확인한다.

`Packages/manifest.json`에는 외부 Registry/Git 패키지가 없고 아래 Unity 내장 모듈만 모두 `1.0.0`으로 선언되어 있다. 첫 항목을 제외한 이름에도 공통 접두사 `com.unity.modules.`가 붙는다.

```text
com.unity.modules.accessibility, adaptiveperformance, ai, androidjni,
animation, assetbundle, audio, cloth, director, imageconversion, imgui,
jsonserialize, particlesystem, physics, physics2d, screencapture, terrain,
terrainphysics, tilemap, ui, uielements, umbra, unityanalytics,
unitywebrequest, unitywebrequestassetbundle, unitywebrequestaudio,
unitywebrequesttexture, unitywebrequestwww, vectorgraphics, vehicles,
video, vr, wind, xr
```

따라서 Package Manager에서 임의로 최신 패키지를 추가하지 않는다. `Packages/manifest.json`과 `Packages/packages-lock.json`을 함께 가져오면 일치하는 Editor가 Import 중 필요한 내장 모듈을 해석한다.

## 3. 회사 PC 작업물을 안전하게 가져온다

### 권장: 의도한 변경만 확인해 Git으로 이동

현재 작업에서 직접 수정한 파일을 검토한 다음, **의도한 경로만** 선택해서 커밋하고 원격에 올리는 방법이 가장 안전하다. `git add .`와 `git commit -a`처럼 범위 밖 파일까지 한꺼번에 포함하는 명령은 사용하지 않는다.

```powershell
git diff -- "수정/경로"
git add -- "수정/경로"
git diff --cached
git commit -m "작업 내용을 설명하는 메시지"
$branch = git branch --show-current
git push -u origin $branch
```

집 PC에는 새 빈 위치에 원격 저장소를 clone한다. 원격 주소에 토큰을 직접 적지 않는다. 정본 개발 브랜치는 `main` 하나이며 기능별 보조 브랜치를 다시 만들지 않는다.

```powershell
$remote = 'https://github.com/zzangzzangman2/company.git'
$homeRepo = Join-Path $HOME 'Documents\Codex\family_company_unity'
git clone $remote $homeRepo
Set-Location $homeRepo
git fetch origin
git switch main
git pull --ff-only origin main
git status --short --branch
git rev-parse HEAD
```

집 PC에 이미 clone이 있다면 dirty 상태에서 바로 pull하지 않는다. 먼저 그 폴더의 `git status`와 백업을 확인하고, 양쪽 변경을 커맨더가 비교한다. 작업 폴더가 clean일 때 `git fetch origin`, `git switch main`, `git pull --ff-only origin main` 순서로 재개한다.

### 미커밋 변경도 보존해야 할 때: 새 폴더에 스냅샷

미커밋·미추적 파일이 많아 안전한 커밋을 만들 수 없다면, Unity와 작성 작업을 모두 닫은 뒤 **기존 clone 위가 아닌 새 빈 폴더**에 스냅샷을 만든다. 아래 예시는 원본을 삭제하거나 이동하지 않으며 `/MIR`, `/PURGE`, `/MOVE`를 사용하지 않는다.

```powershell
$sourceRepo = (Resolve-Path '.').Path
$transferRepo = Join-Path (Split-Path $sourceRepo -Parent) `
    ('family_company_unity_transfer_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
if (Test-Path -LiteralPath $transferRepo) { throw '새 전송 폴더가 이미 존재합니다.' }
New-Item -ItemType Directory -Path $transferRepo | Out-Null
robocopy $sourceRepo $transferRepo /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 `
    /XD Library Temp Logs obj bin Build Builds UserSettings MemoryCaptures `
        Recordings Artifacts .vs .vscode .idea work
if ($LASTEXITCODE -ge 8) { throw "복사 실패: robocopy exit code $LASTEXITCODE" }
```

이 스냅샷에는 `.git`까지 포함되므로 현재 브랜치, staged 변경, unstaged 변경, 미추적 파일을 함께 보존할 수 있다. 만든 새 폴더를 탐색기에서 다시 확인한 뒤 외장 저장장치나 승인된 전송 수단으로 옮기고, 집 PC에서도 **새 위치**에 복사한다. 기존 clone에 덮어쓰지 않는다.

반드시 포함할 항목은 다음과 같다.

- `.git/`(dirty 상태 그대로 이어갈 때), `.gitignore`, `AGENTS.md`, 최상위 안내·설정 파일
- `Assets/**`와 모든 짝 `.meta`
- `Packages/manifest.json`, `Packages/packages-lock.json`
- `ProjectSettings/**`
- `Docs/**`, `Tools/**`, `HistoryTools/**`

Unity가 다시 생성하므로 보통 가져갈 필요가 없는 항목은 `Library/`, `Temp/`, `Logs/`, `obj/`, `bin/`, `Build/`, `Builds/`, `UserSettings/`, `MemoryCaptures/`, `Recordings/`, `Artifacts/`, `.vs/`, `.vscode/`, `.idea/`, 생성된 `.csproj`와 `.sln`이다. `work/`의 격리 Unity 복사본과 `bin/obj`도 제외한다. 단, 커맨더가 `work/` 안의 손으로 작성한 검증 소스나 `.csproj`를 정본으로 지정했다면 그 작은 파일만 별도로 검토해 옮긴다.

패치 파일만으로는 미추적 이미지·오디오 등을 보존할 수 없다. 패치를 보조 수단으로 쓸 때는 `git diff --binary`, `git diff --cached --binary`, `git ls-files --others --exclude-standard` 결과와 미추적 원본 파일을 모두 챙기고, 집 PC의 별도 clone에서 `git apply --check`로 먼저 검사한다.

## 4. 집 PC에서 백업과 저장소 정합성을 확인한다

집 PC에서 처음 열 때도 Unity보다 Git 상태를 먼저 본다.

```powershell
Set-Location $homeRepo
git status --short --branch
git branch --show-current
git rev-parse HEAD
Test-Path -LiteralPath 'Assets\FamilyCompany\Scenes\Prototype01.unity'
Get-Content -LiteralPath 'ProjectSettings\ProjectVersion.txt' -Encoding UTF8
```

회사 PC에서 기록한 `main` 커밋과 비교하고, 예상한 dirty 변경과 미추적 에셋이 모두 있는지 확인한다. 누락이나 뜻밖의 변경이 있으면 Unity로 열지 말고 원본 스냅샷을 보존한 채 다시 비교한다. 어느 PC를 현재 작성 원본으로 쓸지 한 곳만 정하고, 다른 PC는 동기화 완료 전까지 읽기 전용으로 둔다.

## 5. Codex 데스크톱 앱에서 저장소를 연다

Codex 데스크톱 앱에 로그인한 뒤 `폴더 열기/Open folder` 또는 프로젝트 위치 선택 메뉴에서 **`Assets`, `Packages`, `ProjectSettings`가 함께 있는 저장소 루트**를 선택한다. `Assets` 하위 폴더만 열지 않는다. 화면 문구는 앱 버전에 따라 조금 다를 수 있다. 공식 앱 절차도 로그인 후 작업 위치로 채팅·프로젝트·폴더를 선택하도록 안내한다: [ChatGPT 데스크톱 앱 안내](https://learn.chatgpt.com/docs/app).

첫 확인 순서는 다음과 같다.

1. 루트 `AGENTS.md`를 UTF-8로 전부 읽는다.
2. `Docs/PROJECT_STATE.md`, `Docs/CANON.md`, `Docs/DECISIONS.md`, `Docs/ARCHITECTURE.md`를 읽는다.
3. `Docs/CLAUDE_HANDOFF_HISTORY_DATA.md`에서 History 전용 소유권을 확인한다.
4. `git status --short --branch`로 실제 변경을 확인한다.
5. 현재 목표·마지막 검증·이번에 수정할 정확한 파일 범위를 정리한 뒤에만 작업을 시작한다.

Codex 앱의 권한 요청이 뜨면 명령과 대상 경로를 읽고 승인한다. 저장소 밖 삭제, 원본 Unity 프로세스 종료, lockfile 삭제, 다른 담당 경로 덮어쓰기는 승인하지 않는다.

## 6. Unity에서 최초 Import와 기본 Play를 점검한다

Unity Hub에서 `Add/Open project from disk`로 저장소 루트를 등록하고 반드시 `6000.3.21f1`로 연다. 첫 실행은 `Library`를 새로 만들고 모든 에셋을 Import하므로 오래 걸릴 수 있다. 진행 중 강제 종료하거나 회사 PC의 `Library`를 복사해 시간을 줄이려 하지 않는다.

Import가 끝나면 다음 순서로 확인한다.

1. `Window > General > Console`을 열고 C# 컴파일 오류와 Import 오류가 0인지 확인한다. 경고도 새로 생겼다면 원인을 기록한다.
2. Project 창에서 `Assets/FamilyCompany/Scenes/Prototype01.unity`를 더블클릭해 연다.
3. 씬이 완전히 로드된 뒤 저장하지 않은 자동 변경 표시가 생겼는지 확인한다. 예상하지 못한 업그레이드·재직렬화가 보이면 저장하지 말고 현재 채팅에 기록한다.
4. Game 뷰를 16:9로 두고 Play한다. 플레이어 입력, 가족 3 NPC의 이동·상태 표시, 사무실 화면, 기본 UI가 보이고 Console에 예외가 없는지 짧게 확인한다.
5. Play를 종료한 뒤 `git status --short`를 다시 실행해 예상하지 못한 프로젝트 파일 변경이 생기지 않았는지 확인한다.

실패하면 Console의 첫 오류부터 파일·라인·재현 순서와 함께 기록한다. 오류가 난 상태에서 씬이나 ProjectSettings를 무심코 저장해 다른 변경과 섞지 않는다.

## 7. 한 채팅에서 한 작업씩 순차 진행한다

현재 운영 원칙은 **한 채팅·`main` 한 브랜치·한 작업**이다. 작업을 빠르게 하려고 다른 채팅, 하위 에이전트, 새 branch나 worktree를 만들지 않는다. 사용자가 나중에 운영 방식을 명시적으로 바꾼 경우에만 먼저 AGENTS.md와 README.md를 갱신하고 새 방식을 적용한다.

각 작업은 아래 순서로 끝까지 닫는다.

1. `PROJECT_STATE`에서 현재 1순위와 마지막 PASS를 읽는다.
2. `git status --short --branch`로 `main`과 예상하지 못한 변경을 확인한다.
3. 이번 작업의 정확한 파일 범위와 금지 경로를 정한다.
4. 구현한 뒤 범위에 맞는 순수 C#·Unity batchmode·PlayMode·시각 QA를 실행한다.
5. 결과와 남은 문제를 `PROJECT_STATE`에 기록한다.
6. 의도한 파일만 stage·commit하고 `main`에 push한다.
7. 다음 작업을 시작하기 전에 worktree가 깨끗한지 다시 확인한다.

`Prototype01.unity`, `ProjectSettings/**`, `Packages/**`, 공용 asmdef와 정본 문서는 한 작업 안에서도 변경 범위를 특히 좁게 잡는다. History 전용 경로인 `Assets/FamilyCompany/Content/History/**`, `HistoryTools/**`, `Docs/CLAUDE_HISTORY_PROGRESS.md`는 역사 데이터 작업이 명시된 순서에서만 수정한다.

## 8. 집 PC의 단일 작업 채팅을 시작한다

아래 짧은 프롬프트를 집 PC에서 사용할 한 채팅에 그대로 붙여 넣는다.

```text
실제 저장소 루트에서 AGENTS.md, Docs/HOME_PC_CONTINUATION_GUIDE.md,
Docs/CLAUDE_HANDOFF_HISTORY_DATA.md를 UTF-8로 전부 읽어라.
Docs/PROJECT_STATE.md, Docs/CANON.md, Docs/DECISIONS.md,
Docs/ARCHITECTURE.md도 정본으로 확인하라.
현재 git status --short --branch, HEAD 커밋과 마지막 검증 결과를 확인하라.
정본 브랜치는 main 하나다. 새 브랜치·worktree·다른 채팅·하위 에이전트를 만들지 마라.
PROJECT_STATE의 다음 작업을 한 번에 하나씩 순차 구현하고, 매번 정확한 수정 범위를 먼저 정하라.
구현 뒤 검증·PROJECT_STATE 갱신·의도한 파일만 커밋·main push까지 끝낸 다음 다음 작업으로 넘어가라.
예상하지 못한 tracked·untracked 파일은 삭제하거나 포함하지 말고 먼저 보고하라.
```

매 작업일 종료 때는 Unity를 닫고 `git status`, 변경 파일, 검증 결과와 `main` push 여부를 확인한다. 세부 기능 현황은 이 문서에 덧붙이지 않고 [PROJECT_STATE.md](./PROJECT_STATE.md)와 [CLAUDE_HANDOFF_HISTORY_DATA.md](./CLAUDE_HANDOFF_HISTORY_DATA.md)에 각 정본 소유권에 맞춰 반영한다.
