# 집·회사 공통 메인 게임 실행 파일

## 고정 진입점

사용자 계약(2026-09-06): **항상 같은 메인 EXE를 열고, 게임 내부에서 최신 공개 패치를 받은 뒤 실행한다.**
플레이하기 위해 Unity, git pull, FAST_QA, BUILD_WINDOWS를 실행하지 않는다.

- 공통 경로: `%USERPROFILE%\Downloads\FamilyCompany_Playtest\FamilyCompany.exe`
- 현재 집 PC의 실제 파일: `C:\Users\godho\Downloads\FamilyCompany_Playtest\FamilyCompany.exe`
- 회사 PC도 사용자 계정의 Downloads 아래 같은 폴더명을 사용한다. 계정명이 다르면 `%USERPROFILE%`만 달라진다.
- 바로가기는 이 파일만 가리킨다. `Artifacts/FastQa`, 날짜·커밋별 테스트 EXE, AppData의 특정 버전 EXE를
  메인으로 삼지 않는다. 이 폴더의 `FamilyCompany_Data`, `UnityPlayer.dll`, `FamilyCompanyPatch`는 한 설치
  단위이므로 EXE 하나만 옮기지 않는다.

## 지금 상태와 최초 한 번의 설치

**아직 자동 최신 실행이 배포된 상태는 아니다.** 2026-09-06 읽기 전용 확인에서 위 집 PC 파일의
`BUILD_INFO.txt`는 commit `9144fa0ef3904c267d043ad65af44b817a4f3bea`, 2026-08-18 구버전이다.
패치 worker 폴더도 없다. 이 옛 바이너리는 새 소스를 push하는 것만으로 자동패치 기능을 얻지 못한다.
사용자의 기존 폴더/EXE/저장 데이터는 아직 변경하지 않았다. 이 파일을 최신판이라고 실행 안내하지 않는다.

검증된 첫 게임 Release가 공개되면 개발자가 **최초 한 번** `FamilyCompany-Windows.zip` 전체를 위 고정
폴더에 설치한다. 회사 PC도 같은 공개 패키지를 한 번 받으면 된다. 사용자가 빌드할 필요는 없다.
아직 Release가 없으므로 설치/다운로드 완료로 보고하지 않는다. 실패하거나 검증되지 않은 QA payload는
이 경로에 복사하지 않는다. 기존 파일의 교체는 정확한 identity·증거·저장 보존과 배포 게이트를 따른다.

## 매번 실행할 때

1. 같은 `FamilyCompany.exe`를 연다.
2. 게임 내부 로딩 화면에서 GitHub `zzangzzangman2/company`의 최신 정식 게임 Release를 확인한다.
3. 바뀐 파일만 내려받고 실제 수신 바이트 기준 `패치 중입니다 · 다운로드 / xx.x% / MiB`를 표시한다.
4. 파일 해시 검증이 끝난 새 snapshot을 활성화하고 정상 종료·자동 재시작으로 최신 게임에 들어간다.
5. 변경이 없으면 최신 버전 확인과 무결성 검증 후 시작한다. 다운로드를 한 척하는 퍼센트는 없다.

최신 파일 저장소는 `%LOCALAPPDATA%\FamilyCompany\PatchedGame`다. 기존 메인 EXE는 고정 진입점이고
이 내부 snapshot으로 연결된다. 실행 중인 EXE를 제자리에서 덮어쓰지 않으며 저장 데이터는 별도 보존한다.
게임 종료 후에도 다음번에 같은 메인 파일을 사용한다. AppData 버전 폴더를 직접 실행하지 않는다.

**최신 확인 실패 = 시작 차단.** 네트워크 실패, 미공개 Release, 손상 파일이면 재시도/종료만 허용한다.
`이전 버전으로 시작`, 오프라인 우회, 업데이트 실패 후 옛 게임 자동 실행은 허용하지 않는다.
이 정책 때문에 인터넷/패치 서버 확인이 불가능하면 게임도 시작하지 못한다.
`최신`은 GitHub main 소스가 아니라 검증 후 공개된 정식 게임 Release를 뜻한다.

## 개발하는 PC와 플레이하는 PC 구분

- C# 코드 변경은 개발자가 컴파일해야 한다. 반복 개발은 warm FAST_QA를 이용한다.
- 검증된 코드·콘텐츠를 패치로 공개하는 제작 단계에는 새 빌드가 필요할 수 있다.
- 집/회사에서 **플레이할 때마다 빌드하지 않는다**. git pull은 개발 소스를 받는 명령이며 게임 업데이트가 아니다.
- 회사 Codex는 이 문서와 `PROJECT_STATE.md`, `GITHUB_PATCHING.md`부터 읽고, 고정 메인 경로를 날짜별
  실행본으로 바꾸거나 로컬 소스 push만으로 게임 패치 배포를 완료했다고 보고하지 않는다.

배포 검증·현재 차단 항목은 [PROJECT_STATE.md](PROJECT_STATE.md), 구현 계약은
[GITHUB_PATCHING.md](GITHUB_PATCHING.md)가 소유한다.
