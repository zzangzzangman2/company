# PLAYTEST BUILD

저장소를 받은 Windows PC에서 Unity 편집기를 직접 열지 않고 플레이테스트 실행본을 만드는 절차다.
빌드 결과물 자체는 매우 크기 때문에 Git에 넣지 않고, 재현 가능한 빌드 명령만 저장소에서 관리한다.

## 가장 간단한 사용법

1. Unity Hub에서 프로젝트 버전과 같은 `6000.3.21f1` 및 Windows Build Support를 설치한다.
2. 저장소 루트의 `BUILD_WINDOWS.cmd`를 더블 클릭한다.
3. 성공 후 `RUN_WINDOWS.cmd`를 더블 클릭한다.

생성되는 실행 파일:

```text
Builds\Windows\FamilyCompany_Playtest\FamilyCompany.exe
```

`Builds/`는 `.gitignore` 대상이므로 빌드 결과가 실수로 커밋되지 않는다.

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
- `Tools/Build-FamilyCompanyWindows.ps1`: 잠금, 스테이징, 이전 빌드 복구, 상태 기록
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
