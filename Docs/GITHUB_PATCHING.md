# 개발 설정 즉시 반영과 GitHub 자동패치

현재 상태: **로컬 구현/부분 검증 완료, 실제 게임 Release 미공개**. 최종 판정은 PROJECT_STATE.md.
정본 저장소는 `main`, `https://github.com/zzangzzangman2/company`다. 개발 소스 push와 게임 배포는 다르다.
Release 업로드나 PC 종료를 이미 완료했다고 설명하지 않는다.

## 무엇이 빨라지는가

- 이동 속도, 보폭, phase, 아들/아빠의 발 중심 미세 오프셋, 테스트 상점 가격은 JSON 저장 후 0.5초 이내
  반영한다. 잘못된/부분 저장은 마지막 정상 값 전체를 유지한다. 저장 게임의 원가나 돈을 다시 쓰지 않는다.
- C# 변경은 컴파일이 필요하다. 반복 확인은 FAST_QA warm cache를 사용하고 매번 Release를 만들지 않는다.
- 플레이어는 시작할 때 검증된 새 버전이 있으면 **변경된 빌드 파일만** 내려받는다. 설치되지 않은 PC는
  최초 전체 파일을 받아야 한다. Resources 패킹 상태라 `resources.assets.resS` 한 파일이 바뀌면
  그 파일 전체를 받는다. Addressables/모델별 streaming이나 실행 중 코드 교체는 이번 구현 범위가 아니다.

## 개발자 설정

샘플은 `Tools/FastQa/development-settings.example.json`. 원본 샘플을 직접 고치기보다 별도 개발용 사본을
사용한다. 명시적 인자 `-familyCompanyDevSettings "절대경로/settings.json"`로 켠다.
Editor/Development/FastQA만 허용하며 일반 `FamilyCompany_Data` Release는 이 인자를 무시한다.
이는 개발자 사용 구분이지 안티치트 보안 경계가 아니다.

| 키 | 범위 / 기본값 |
| --- | --- |
| schemaVersion | 1 |
| moveSpeed | 0.25..2 / 1 |
| strideOfficeUnits | 0.4..2.5 / 0.7950477 |
| phaseOffsetCycles | 0..1 / 0 |
| playerFootOffsetX, playerFootOffsetZ | -0.25..0.25 / 0, 고정 캘리브레이션에 더함 |
| fatherFootOffsetX, fatherFootOffsetZ | -0.25..0.25 / 0, 복제 외형도 동일 적용 |
| workstationPriceWon | 10000..5000000 / 400000 |

금액은 정수 원. 의자는 총액의 정수 1/8, 책상·PC는 나머지로 나눠 반올림 잔액을 없앤다.
한 세트 실제 결제액, UI 견적, 재판매용 구매 원가가 같은 금액을 사용한다. 보폭 변경 시 누적거리 때문에
보행 phase가 갑자기 뛰지 않도록 연속성 bias를 유지한다. 명시적 phase 변경 자체는 개발자 조정이다.

회귀 테스트(현재 개발용 FastQA identity에서만 실행; 실패 payload 재사용 금지):

```powershell
./Tools/Test-FamilyCompanyDevelopmentReload.ps1
./Tools/Invoke-FamilyCompanyOpeningWalkAudit.ps1 -DeveloperSettings 'C:/exact/development/settings.json'
```

첫 명령은 24초 정상 이동에서 1→0.5→잘못된 JSON→1을 확인한다. 재빌드는 0회, 같은 EXE 해시를 확인한다.
초기 캡처 준비 및 자연스러운 양보 때문에 모든 frame의 속도가 같지는 않다. 안정 구간의 cruising p90과
완전한 snapshot 적용 로그를 함께 검사한다. 이 테스트가 화면 보행 전체 승인이나 독립 release gate는 아니다.

## 플레이어 런처

검증된 첫 Release가 공개된 뒤 그 Release의 `FamilyCompany-Launcher.zip`을 한 번 풀고
`PLAY_FAMILY.cmd`로 시작한다. **현재는 Release가 없으므로 처음 실행은 안전하게 실패한다.**
main 브랜치의 임의 EXE, Downloads의 옛 실행본이나 개발 캐시를 찾아 실행하지 않는다.

- 설치 루트: `%LOCALAPPDATA%/FamilyCompany/PatchedGame` 전용 디렉터리.
- API: 공개 GitHub `releases/latest`; draft/prerelease 또는 다른 태그 형식은 거부.
- 태그: `fc-win-YYYYMMDD.N`; 양의 단조 증가 `sequence`, 전체 commit SHA.
- manifest는 GitHub API가 반환한 SHA-256 digest와 TLS로 검증한다. 별도 코드서명은 아니다.
- 각 원본 파일과 gzip 자산의 SHA-256/크기를 모두 검증한다. 토큰/쿠키/계정 비밀을 런처에 넣지 않는다.
- 그대로인 설치 파일은 해시 검사 후 새 snapshot에 복사, 바뀐 파일만 다운로드·해제한다.
- 모두 검증된 뒤 `current.json`만 원자 교체한다. 저장 데이터는 설치 루트 밖에 그대로 둔다.
- 불완전 파일은 실행하지 않는다. 다운로드 실패 시 기존 설치도 다시 해시 검증된 경우에만 실행한다.
- 경로 탈출/ADS/장치명/대소문자 충돌/junction/동일 버전 변조/다운그레이드/동시 실행은 거부한다.
- 실행 중인 게임을 강제 종료하지 않는다. 완성 폴더 이동 직후 중단된 경우 재시작은 검증 후 활성화만 한다.
- 실패한 staging은 외부 evidence에 해시/오류를 남긴 뒤 자기 GUID staging만 정리한다. 설치된 이전 버전의
  자동 GC/복귀 UI, 전용 코드서명, 단일 gzip 파일 내부 이어받기는 아직 구현하지 않았다.

## 로컬 패치 회귀검사

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Updater/Test-FamilyCompanyUpdater.ps1
```

실제 실행 파일이 아닌 inert 텍스트 fixture로 36개 검사를 수행한다. 최초 설치/변경·추가·삭제/재사용,
손상·누락 다운로드/재시도/중단 활성화 복구/오프라인 무결성/경로·접합점·동시 잠금/세이브 보존 포함.
결과는 `Artifacts/UpdaterTests/<GUID>/result.json`. GitHub 실배포/실게임 patch 다운로드 검증은 별도다.

## 실제 배포 전 필수 조건

1. 2026-09-05 exact cleanup 차단은 해결됐다. 실패한 네 QA identity의 각 166개 파일을 해시 검증 후
   휴지통으로 옮기고 target=0을 확인했다. 새 검사에서도 실패하면 같은 evidence-before-delete 원칙을
   적용한다. source/Library/Bee/다른 실행본/저장 파일은 제외한다. 상세 기록은 PROJECT_STATE.md.
2. 보행·충돌 보정, 4방향 착석/업무, native shop 클릭, 다음 날 가족별 출근, 음소거를 독립 runner로 검증한다.
3. 사용자 실제 보행 화면 승인, clean committed main, 정확한 Unity 6000.3.21f1 Release 빌드가 필요하다.
4. `REGRESSION_BUILD_POLICY.md`의 독립 gate 및 실패 후 삭제/복귀 계약을 만족하는 빌드 경로를 사용한다.
   현재 기존 BUILD/DEPLOY 스크립트는 완전한 독립 gate/실패 cleanup 구현이 확인되지 않았으므로 자동 실행하지 않는다.
5. 소스 push 전 fresh remote branch/tag/release/LFS 전체 검사:

```powershell
./Tools/Updater/Test-FamilyCompanyRemoteInventory.ps1
```

`prohibited=0`, `unknown=0`이어야 한다. 첫 Release 이후에는 검토된 정확한 release ID/asset ID/size/digest와
독립 receipt 해시를 `Docs/Evidence/VerifiedReleaseInventory.json`에 기록해야 다음 정상 배포를 허용한다.
이 목록에 없는 assets를 임의 허용하지 말고 조사한다. 예전 archive를 정상 source dependency로 오분류하지 않는다.

## 배포 도구 (아직 live publication 미검증)

`Tools/Updater/Publish-FamilyCompanyPatch.ps1`은 clean main, `BUILD_INFO.txt`의 commit/Release/Unity,
독립 receipt와 현재 사용자 승인을 확인하기 전에는 gzip 패키지조차 만들지 않는다.
`-Publish` 없이는 로컬 패키지만 만들며 외부 쓰기는 없다. 준비된 동일 패키지는 재검증 후 재사용한다.
`-Publish` 때만 draft 생성→asset 업로드→GitHub digest 대조→공개한다. 업로드 실패 시 공개하지 않으며,
실패 draft/asset 정리는 정책에 맞는 exact inventory·evidence와 별도 확인이 필요하다.

receipt 필수 필드는 `schemaVersion:1`, `commit`, `productionEligible:true`, `userVisualApproval:true`,
`approvalReference`, `playerSha256`, `buildInfoSha256`, `gates`다. gate마다 `name`, `passed:true`,
`independent:true`, 동일 `commit`, `evidencePath`, `evidenceSha256`을 실제 증거로 채워야 한다.
필수 이름은 소스의 `$required` 목록이 정본이다. PASS/승인 값을 추측해서 작성하지 않는다.
release receipt는 공개되므로 계정 정보·토큰·개인 로그를 포함하지 않는다.

최초 패키지에는 모든 파일의 gzip, 이후 패키지는 `-PreviousManifest`의 unchanged assetTag를 그대로
참조하여 이전 Release 자산을 재사용한다. 참조 중인 검증된 이전 Release assets를 삭제하면 신규 PC 설치가
깨진다. 출시 뒤 생성되는 `Artifacts/UpdaterRemoteInventory/published-<tag>.json`은 다음 inventory의
검토용 자료이지 자동 승인서가 아니다.

출시된 실제 게임의 최초 설치→새 버전 일부 변경→네트워크 실패 복구까지 확인하기 전에는
“GitHub 자동패치 배포 완료”라고 기록하지 않는다. 작업 미완료 상태에서 완료 후 PC 종료도 실행하지 않는다.
