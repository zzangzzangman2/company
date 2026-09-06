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
- 저장소의 `RUN_WINDOWS.cmd`도 이 고정 경로만 연다. 로컬 Builds/QA를 실행하거나 자동으로 빌드하지
  않으며, 설치가 없거나 패치 worker가 없는 구버전이면 안내 후 차단한다.

## 지금 상태와 최초 한 번의 설치

**2026-09-07: 첫 하청·자체 제품·주간 유지보수 `fc-win-20260907.1` 공개.**
최신 게임 빌드 commit은 `c0709823c0e45c4152c673ca0b67d7a1e1506bc7`다. 이후 문서/배포 host guard push SHA와 구분한다.
집의 고정 메인은 패치 지원 v2(`ee48a72c`) 그대로이며 정상이다. **같은 EXE를 열면 v4 패치
160,602,031 bytes(153.2 MiB)를 받고 최신판으로 진입한다.** 이 수치는 집의 v2 출발 기준이며 설치 버전에 따라 다르다. 캐시를 미리
업데이트하지 않았다. 이미 설치한 회사 PC도 같은 메인을 사용한다. 재설치나 Unity 빌드는 필요 없다.
[모니터 수정 기록](MONITOR_ALIGNMENT_PATCH.md) · [v2 최초 설치/재시작 검증](Evidence/FirstPublicRelease20260906/README.md).
[이번 공개 전송·게임 검증](Evidence/StarterBusinessRelease20260907/README.md) · [회사 인수인계](COMPANY_HANDOFF_2026-09-07.md).

회사 PC 최초 설치는 다음 한 번만 필요하다.

1. [공개 설치 ZIP 받기](https://github.com/zzangzzangman2/company/releases/download/fc-win-20260907.1/FamilyCompany-Windows.zip).
2. `%USERPROFILE%\Downloads\FamilyCompany_Playtest` 폴더를 만들고 ZIP **전체 내용**을 그 안에 푼다.
   EXE 바로 옆에 `FamilyCompany_Data`, `UnityPlayer.dll`, `FamilyCompanyPatch`가 있어야 한다.
   ZIP 안에서 실행하거나 EXE 하나만 복사하지 않는다. 기존 설치/개인 파일이 있으면 무작정 덮어쓰지 않는다.
3. 그 폴더의 `FamilyCompany.exe`로 실행하거나 그 파일의 바로가기를 만든다. Unity/git/빌드는 필요 없다.

이후 신규 PC용 [최신 공개 설치 ZIP](https://github.com/zzangzzangman2/company/releases/latest/download/FamilyCompany-Windows.zip)도
제공한다. 이미 설치된 PC는 ZIP을 매번 받을 필요 없이 같은 메인 EXE가 패치를 확인한다.
이번 v4 ZIP은 271,014,149 bytes이며 SHA-256은
`31318593d41f50bb512d1734f17c571613bc7320d3c9e5e4d35e52c14d26fea0`이다.

집 설치에서는 169개 게임 파일을 검증했고 세이브/백업 5개를 보존했다. 구형 `9144fa0e` 폴더의
157개 파일은 해시 기록 후 **휴지통으로 이동**했으므로 복구 가능하다. 최신 확인 실패 시 사용하는
fallback은 아니다. 철회된 `fc-win-20260906.1` 또는 실패 QA 실행본을 설치하지 않는다.

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
- 현재 소스 작업 위치는 `C:\Users\godho\Documents\Codex\fc_agents\integration_p0`, 브랜치 `main`이다.
  회사의 소스 체크아웃 경로는 달라도 된다. 집의 예전 2026-08-25 작업 폴더에서 이어서 수정하지 않는다.
- 집/회사 게임 저장 파일의 자동 동기화는 이번 기능에 포함되지 않는다. 패치가 최신이어도 각 PC의
  세이브는 따로 유지되며, 기존 세이브를 패치 파일로 덮어쓰지 않는다.

배포 검증·현재 차단 항목은 [PROJECT_STATE.md](PROJECT_STATE.md), 구현 계약은
[GITHUB_PATCHING.md](GITHUB_PATCHING.md)가 소유한다.
