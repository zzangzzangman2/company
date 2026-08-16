# PLAYTEST BUILD

저장소를 받은 Windows PC에서 Unity 편집기를 직접 열지 않고 플레이테스트 실행본을 만드는 절차다.
빌드 결과물 자체는 매우 크기 때문에 Git에 넣지 않고, 재현 가능한 빌드 명령만 저장소에서 관리한다.

## 현재 최종 build handoff

- R18 arrival `ce9e3ae4d94a7365c0447103d2ad904013ef58a1`는 독립 static과 Unity `6000.3.21f1` capture-free Player exit 0을 통과해 integration에 단일 merge되었다. 가족 4명의 Work 0..5, atomic seat/root/pelvis, microslide 0, exit/turn/first-walk, stationary endpoint, safe egress와 furniture 0이 실제 Player에서 확인되었다.
- 과거·회귀 실행 payload는 evidence 보존 뒤 허용 root에서 제거되었고, 이전 GitHub history·tags·Releases·Actions 감사의 executable payload는 0이다. `da5c6e7f9f9d48f0eada245cff727435536c91dd`의 tracked Player payload CI guard를 build 전 필수 gate로 유지한다.
- 최종 Windows build와 Downloads 배포는 2026-08-16에 수행되었다. 배포본 `%USERPROFILE%\Downloads\FamilyCompany_Playtest`의 `BUILD_INFO.txt`는 `commit=8b9e3313928545f98b4fc60427da76901271fc96`, `unity=6000.3.21f1 c02631ffc030`, `qa=FAMILY_COMPANY_SEATING_TRANSITION_QA PASS exit0`을 기록하며 이 SHA는 `origin/main` HEAD와 같다. 이후 build/deploy도 clean 최종 HEAD의 새 `BUILD_INFO.txt` identity로만 수행한다.
- 이 배포본이 남긴 QA 증거는 seating transition QA 하나다. [REGRESSION_BUILD_POLICY.md](REGRESSION_BUILD_POLICY.md)가 요구하는 네 가족 09:00/09:01/09:02/09:03 독립 oracle 결과는 배포본 옆에 기록되어 있지 않으므로, 다음 배포에서는 그 결과를 비실행 evidence로 함께 남긴다.

## 반복 확인에는 이 문서를 쓰지 않는다

이 문서는 배포 후보를 만드는 절차다. 한 곳을 고치고 결과만 확인할 때는 `BUILD_WINDOWS.cmd` 대신
`FAST_QA_WINDOWS.cmd`를 쓴다. 변경 종류별 명령과 실측 근거는 [ITERATION_LOOP.md](ITERATION_LOOP.md)가 정본이다.

## 가장 간단한 사용법

1. Unity Hub에서 프로젝트 버전과 같은 `6000.3.21f1` 및 Windows Build Support를 설치한다.
2. 저장소 루트의 `BUILD_WINDOWS.cmd`를 더블 클릭한다.
3. 성공 후 `RUN_WINDOWS.cmd`를 더블 클릭한다.

생성되는 실행 파일:

```text
Builds\Windows\FamilyCompany_Playtest\FamilyCompany.exe
```

`Builds/`는 `.gitignore` 대상이므로 빌드 결과가 실수로 커밋되지 않는다.

clean integration HEAD를 Downloads의 자동 플레이테스트 실행본으로 안전하게 교체하는 별도 절차는
[WINDOWS_AUTO_DEPLOY.md](WINDOWS_AUTO_DEPLOY.md)를 따른다. 이 절차는 로컬 `BUILD_WINDOWS.cmd`와 같은
Release 빌더를 재사용하지만, staging 검증·last-known-good·실행 중 배포 대기 계약을 추가한다.

## 다른 PC에서 자동으로 찾는 항목

- 프로젝트 경로: `BUILD_WINDOWS.cmd`가 있는 현재 저장소 루트
- Unity 기본 경로: `C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe`
- Codex 번들 경로: 저장소 부모의 `UnityEditors\6000.3.21f1\Editor\Unity.exe`

Unity를 다른 위치에 설치했다면 환경 변수로 지정한다.

```cmd
set FAMILY_COMPANY_UNITY_EDITOR=D:\Unity\6000.3.21f1\Editor\Unity.exe
BUILD_WINDOWS.cmd
```

## PowerShell에서 직접 실행

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Build-FamilyCompanyWindows.ps1
```

주요 구성:

- `BUILD_WINDOWS.cmd`: 저장소 루트에서 실행하는 원클릭 빌드 진입점
- `RUN_WINDOWS.cmd`: 마지막으로 성공한 로컬 빌드를 실행
- `Tools/Build-FamilyCompanyWindows.ps1`: machine-wide 잠금, 스테이징, 이전 빌드 복구, 상태 기록
- `Tools/FamilyCompanyBuild.Common.ps1`: 프로젝트·Unity 버전 검증과 빌드 지문 계산
- `Assets/FamilyCompany/Editor/WindowsPlayerBuild.cs`: Unity의 Windows x64 실제 빌드 함수

빌드 상태와 로그:

```text
Builds\Windows\Automation\build-status.json
Builds\Windows\Automation\logs\
```

빌드는 임시 staging 폴더에 먼저 생성한다. 성공한 경우에만
`FamilyCompany_Playtest`로 승격하며, 실패하면 기존 성공 빌드를 보존한다.

## 자동 감시 빌드

필요한 경우 아래 스크립트를 사용한다.

```powershell
.\Tools\Start-FamilyCompanyBuildWatch.ps1
.\Tools\Stop-FamilyCompanyBuildWatch.ps1
```

감시기 역시 저장소 상대경로와 같은 Unity 자동 탐색 규칙을 사용한다.

## 검증 원칙

- 빌드 전에 `ProjectSettings/ProjectVersion.txt`가 `6000.3.21f1`인지 검사한다.
- 빌드 입력은 `Assets`, `Packages`, `ProjectSettings`의 Git 상태와 HEAD로 지문화한다.
- 빌드 중 입력이 바뀌면 결과를 승격하지 않는다.
- `BUILD_INFO.txt`에 커밋, 브랜치, dirty 상태, Unity 버전과 지문을 기록한다.
- 실제 배포용 대용량 EXE와 Unity 데이터 파일은 GitHub에 푸시하지 않는다.
