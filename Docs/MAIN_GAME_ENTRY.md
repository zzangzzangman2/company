# 집·회사 공통 메인 게임 실행 파일

## 고정 진입점

**플레이는 항상 같은 메인 EXE로 합니다. Unity 설치, git pull, FAST_QA, 새 빌드는 필요 없습니다.**

- 집: `C:\Users\godho\Downloads\FamilyCompany_Playtest\FamilyCompany.exe`
- 회사: `%USERPROFILE%\Downloads\FamilyCompany_Playtest\FamilyCompany.exe`
- 바로가기와 `RUN_WINDOWS.cmd`는 이 파일만 사용합니다. AppData 버전 폴더나 날짜별 QA 실행본을 메인으로 삼지 않습니다.
- EXE와 `FamilyCompany_Data`, `UnityPlayer.dll`, `FamilyCompanyPatch`는 한 설치 단위입니다. EXE만 옮기지 않습니다.

## 현재 공개판과 집 설치 완료

**2026-09-07: fc-win-20260907.3 / sequence 6**, 캐주얼 통합 메뉴·전체 패치 진행률·재시작 안내.
게임 빌드 SHA는 `cb86605dad19a823551498c2d4bb6bd073031864`이며 후속 문서/도구 commit과 다릅니다.

사용자가 **메인도 백업 후 갱신**을 승인해 집의 기존 v2 메인 전체를 백업한 뒤 **같은 경로에 v6 정식판을 설치했습니다**.
이전 메인의 첫 패치 화면은 snapshot 업데이트만으로 바뀌지 않으므로 이번 한 번의 갱신이 필요했습니다.

- 이전 전체 설치 169개 파일 백업:
  `C:\Users\godho\AppData\Local\FamilyCompany\InstallationBackups\20260907-212438-6fe36f2bde9343c0b9f2e6db9f47ea8c-before-fc-win-20260907.3\FamilyCompany_Playtest`
- 새 메인 169개 파일 모두 공개 manifest와 SHA256 일치. 삭제한 파일 0개.
- 세이브/백업 5개, 저장 폴더 전체 10개 파일, 패치 캐시 344개 파일의 내용 보존.
- 사용자 메인은 자동 실행하지 않았습니다. 사용자 캐시는 v5 그대로입니다.
  다음 실행에는 캐시를 우선 재사용하는 기존 worker가 v5→v6 변경 12개,
  **166,189,664 bytes (158.5 MiB)**를 받아 v6 snapshot을 활성화합니다.
  이 수신량은 현재 집 캐시 기준이며 다른 PC의 설치 상태에 따라 달라집니다.
- [실제 공개 수신·재시작·설치 증거](Evidence/CasualUiRelease20260907/README.md).

## 회사 PC 최초 설치 또는 첫 패치 화면 갱신

1. 게임을 종료한 상태에서 [정식 전체 ZIP](https://github.com/zzangzzangman2/company/releases/download/fc-win-20260907.3/FamilyCompany-Windows.zip)을 받습니다.
2. 기존 `FamilyCompany_Playtest`가 있으면 **전체를 별도 폴더로 먼저 백업**합니다. 개인 파일이 있으면 버리지 않습니다.
3. ZIP 전체 내용을 같은 `%USERPROFILE%\Downloads\FamilyCompany_Playtest`에 풉니다.
4. 그 안의 `FamilyCompany.exe`를 엽니다. ZIP 안에서 실행하거나 EXE 하나만 복사하지 않습니다.

회사에 이미 구형 bootstrap이 있으면 첫 화면의 새 진행률/안내를 적용하는 이 작업은 최초 1회 필요합니다.
이후 콘텐츠 패치마다 전체 ZIP을 다시 설치하거나 플레이할 때 빌드하지 않습니다.
신규 PC용 [최신 설치 ZIP](https://github.com/zzangzzangman2/company/releases/latest/download/FamilyCompany-Windows.zip)도 제공합니다.

v6 ZIP: **276,668,119 bytes**, SHA256
`1f856cb9985adae3ad767d959a4ca76634998411058a951b89c6bc9f5ea6dc66`.

## 매번 실행할 때

1. 고정 메인 EXE를 엽니다.
2. 게임 내부에서 GitHub 최신 정식 Release를 확인합니다.
3. 전체 진행률 하나를 표시합니다: 압축 수신량 0–85% → 전체 파일 검증 85–99% → 성공 결과 100%.
   파일·폴더가 바뀌어도 되돌아가지 않습니다. 단계 가중치이며 남은 시간 추정은 아닙니다.
4. 완료 화면에 **게임을 다시 시작합니다**와 4초 안내를 실제로 보여준 후, 정상 종료·새 snapshot 재시작을 합니다.
5. 변경이 없으면 가짜 다운로드 없이 최신 확인·무결성 검사 후 시작합니다.

패치 저장소는 `%LOCALAPPDATA%\FamilyCompany\PatchedGame`입니다. 실행 중인 EXE를 제자리에서 덮어쓰지 않습니다.
최신 확인/다운로드/해시 검증이 실패하면 재시도 또는 종료만 가능하고 구버전·오프라인 우회 실행은 하지 않습니다.
여기서 최신은 GitHub main 소스가 아니라 **검증 후 공개된 정식 게임 Release**입니다.

세이브는 `%USERPROFILE%\AppData\LocalLow\FamilyCompany\FamilyCompanyPrototype`에 별도 보존합니다.
집/회사 세이브 자동 동기화는 구현하지 않았습니다. 패치로 세이브를 덮어쓰지 않습니다.

## 개발 인수인계

정본 작업 폴더는 `C:\Users\godho\Documents\Codex\fc_agents\integration_p0`, 브랜치는 `main`입니다.
회사 체크아웃 위치는 달라도 됩니다. 예전 2026-08-25 작업 폴더에서 이어서 수정하지 않습니다.
C# 변경은 개발자가 컴파일하고 검증 후 배포해야 하지만, 플레이하는 PC는 빌드하지 않습니다.
소스 push만으로 게임 패치 배포를 완료했다고 보고하지 않습니다.

[회사 인수인계](COMPANY_HANDOFF_2026-09-07.md) · [현재 상태](PROJECT_STATE.md) · [개발/배포 계약](GITHUB_PATCHING.md).
