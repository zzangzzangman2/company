# 개발 설정 즉시 반영과 GitHub 자동패치

현재 상태: **로컬 구현/부분 검증 완료, 실제 게임 Release 미공개**. 최종 판정은 PROJECT_STATE.md.
정본 저장소는 `main`, `https://github.com/zzangzzangman2/company`다. 개발 소스 push와 게임 배포는 다르다.
Release 업로드나 PC 종료를 이미 완료했다고 설명하지 않는다.

고정 메인 경로와 회사 PC 사용법은 **[MAIN_GAME_ENTRY.md](MAIN_GAME_ENTRY.md)**가 소유한다.
2026-09-06 사용자 추가 계약: **최신 정식 게임 Release 확인에 실패하면 시작하지 않는다.** 이전 버전
실행/오프라인 우회는 폐기했다. 플레이 PC는 매번 빌드하지 않으며 최초 패치 지원 설치만 한 번 필요하다.

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

## 게임 내부 패치 로딩

사용자가 별도 Windows 창을 거부했다. **외부 로딩창은 사용하지 않는다.** 검증된 첫 Release 공개 후
`FamilyCompany-Windows.zip`을 풀고 그 안의 **실제 Unity `FamilyCompany.exe`**를 실행한다.
기존 UiRemasterV3 게임 로딩 화면에서 패치를 확인한다. 아직 Release/Downloads 교체는 하지 않았다.

2026-09-06 구현:

- `업데이트 확인 → 변경 파일 확인 → 패치 다운로드 → 압축 해제 → 파일 무결성 검증 → 게임 시작`.
- 다운로드는 실제 수신 압축 바이트 / 변경 파일 압축 바이트 합계로 계산한다. 소수 1자리 내림과 MiB를
  함께 표시한다. 재사용 파일은 분모에서 빼며, 변경 없음은 가짜 다운로드 없이 검증 후 시작한다.
- 검증 진행률은 해시 검증을 완료한 원본 파일의 바이트 합계다. 확인·복사·해제처럼 전체량이 정해지지
  않은 단계에는 퍼센트를 만들지 않고 진행 중 표시를 쓴다. 다운로드 100%와 설치 완료를 구분한다.
- 취소/실패 때 불완전 payload는 실행하지 않는다. 서버에 연결할 수 없거나 최신판을 확인하지 못하면
  기존 설치가 온전해도 시작하지 않는다. 재시도/종료만 제공한다. 취소 응답은 진행 중 네트워크 I/O
  때문에 최대 30초 걸릴 수 있다.
- `GamePatchBootstrap`이 worker의 `FC_PROGRESS` JSON을 메인 thread에서 읽고 기존
  `ScenePreviewJump.DrawPatchLoading`에 전달한다. 타이틀 조작과 사무실 warmup은 확인 후 허용한다.
- Release 빌더는 `FamilyCompanyPatch/`에 Update/InGame/Restart 세 worker만 넣는다. 세 worker는
  창 없이 실행한다. 진단은 `%LOCALAPPDATA%/FamilyCompany/InGamePatchRuns/<GUID>/worker.txt`에 기록한다.
- 실행 중에는 `PrepareOnly`로 새 snapshot을 준비만 한다. 재시작 helper가 정확한 부모 PID/시작시각/경로를
  확인하고 준비 신호를 보낸 뒤 부모의 정상 종료를 기다린다. 그 뒤 재검증/원자 활성화/새 게임 실행을 한다.
  부모가 60초 안에 종료하지 않으면 강제 종료하거나 활성화하지 않는다.
- 최초 설치 파일도 서버 manifest와 일치하는 파일만 seed로 재사용한다. 최초 한 번은 검증된 AppData
  snapshot을 만들기 때문에 추가 로컬 저장 공간과 재시작이 필요하다. 옛 최초 설치 EXE를 다시 열면
  게임 로딩 화면을 거쳐 검증된 최신 snapshot으로 다시 이동한다. 무중단 실행 파일 교체는 아니다.
- 일반 개발 캐시는 worker가 없으면 패치 모드가 꺼지지만, 배포용 main은 worker 누락 시 시작을 차단한다. 명시적 QA root는 Editor가 아닌
  Development/FastQA에서만 허용하며, test worker는 배포에 포함하지 않는다.

- 설치 루트: `%LOCALAPPDATA%/FamilyCompany/PatchedGame` 전용 디렉터리.
- API: 공개 GitHub `releases/latest`; draft/prerelease 또는 다른 태그 형식은 거부.
- 태그: `fc-win-YYYYMMDD.N`; 양의 단조 증가 `sequence`, 전체 commit SHA.
- manifest는 GitHub API가 반환한 SHA-256 digest와 TLS로 검증한다. 별도 코드서명은 아니다.
- 각 원본 파일과 gzip 자산의 SHA-256/크기를 모두 검증한다. 토큰/쿠키/계정 비밀을 런처에 넣지 않는다.
- 그대로인 설치 파일은 해시 검사 후 새 snapshot에 복사, 바뀐 파일만 다운로드·해제한다.
- 모두 검증된 뒤 `current.json`만 원자 교체한다. 저장 데이터는 설치 루트 밖에 그대로 둔다.
- 불완전 파일은 실행하지 않는다. 다운로드/최신 확인 실패 시 구버전 설치를 실행하지 않는다.
- 경로 탈출/ADS/장치명/대소문자 충돌/junction/동일 버전 변조/다운그레이드/동시 실행은 거부한다.
- 실행 중인 게임을 강제 종료하지 않는다. 완성 폴더 이동 직후 중단된 경우 재시작은 검증 후 활성화만 한다.
- 실패한 staging은 외부 evidence에 해시/오류를 남긴 뒤 자기 GUID staging만 정리한다. 설치된 이전 버전의
  자동 GC/복귀 UI, 전용 코드서명, 단일 gzip 파일 내부 이어받기는 아직 구현하지 않았다.

## 로컬 패치 회귀검사

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Updater/Test-FamilyCompanyUpdater.ps1
```

실제 실행 파일이 아닌 inert 텍스트 fixture로 51개 검사를 수행한다. 최초 설치/변경·추가·삭제/재사용,
손상·누락 다운로드/재시도/중단 활성화 복구/오프라인 무결성/경로·접합점·동시 잠금/세이브 보존 포함.
추가로 실제 바이트 분모·소수 내림·단조 증가·변경 없음·전송 중 취소·과다/부족 수신을 검사한다.
결과는 `Artifacts/UpdaterTests/<GUID>/result.json`. GitHub 실배포/실게임 patch 다운로드 검증은 별도다.

`Test-FamilyCompanyLatestOnly.ps1`은 실제 production worker에 API 오류만 주입하여, 검증된 이전
설치본이 있어도 네트워크 실패/잘못된 Release/초안 Release에서 시작하지 않는지 검사한다. 게임 내부
worker와 유지된 개발용 CLI 모두 6개 실패 차단 사례를 통과해야 한다. 이전 설치와 pointer는 보존한다.

`Test-FamilyCompanyInGamePatch.ps1 -ShowWindow`는 실제 Unity 게임에 inert 로컬 전송을 연결한다.
IMGUI는 presented frame이 필요해서 검증 전에 사용자에게 실제 게임 창이 열린다고 알린다.
batch 모드의 검은 PNG는 시각 PASS가 아니다. 최종 화면 **20.3% / 0.81 of 4.00 MiB**와
4,195,675바이트 prepare-only 완료 증거는 `Docs/Evidence/InGamePatch20260906/`에 있다.
이 검증은 실제 GitHub 게임 다운로드/부모 종료/자동 재시작까지 검증한 것이 아니다.

### 후속 실제 Unity 재시작 검사 — 백그라운드 전용 (2026-09-06)

사용자의 최신 지시가 전면 실행/데스크톱 입력을 금지한다. 위 `-ShowWindow` 검사는 역사적 증거이며
현재는 실행하지 않는다. 다음 명령은 별도 게임 창/입력 없이 실제 Unity 부모와 자식을 실행한다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Updater/Test-FamilyCompanyUnityRestart.ps1 -Background
```

6ce5e0eb의 실제 FAST_QA Unity payload를 `Artifacts/UnityPatchRestartTests/<GUID>`에만 복제한다.
같은 payload의 테스트 데이터 파일 하나를 바꿔 원래 파일 재사용 + 실제 변경 파일 전송을 확인한다.
6ff58f22bd39406eb9205400aa49d31d 실행: 변경 압축 4,195,602바이트, 실제 Unity 수신 progress 131건의
분모/소수 내림/단조 증가 모두 일치, 검증 대상 1,036,399,960바이트. 부모 정상 종료 0 후 정확한
새 snapshot 자식(PID 5780)이 `IN_GAME_PATCH_READY_CURRENT`까지 부팅했다. 원래 진입 EXE SHA 불변.
근거: `Evidence/ChairTileCentre20260906/patch/` (실행 identity, manifest, 부모/자식 trace, 재시작 receipt).

이것은 **로컬 파일 전송**, 독립 gameplay 승인이 아닌 패치 기능 검사다. IMGUI가 실제로 그려진
화면/현재 문구의 글자 배치, GitHub 다운로드, 외부 인터넷 장애 복구를 PASS로 보고하지 않는다.
`패치 중입니다 · 다운로드` 문구와 실제 바이트 퍼센트를 보내는 게임 내 로딩 경로는 유지한다.
원래 메인 파일은 계속 진입점이고 업데이트 실행본은 별도 검증 snapshot에서 재시작한다. 실행 중
바이너리를 제자리에서 수정하는 방식이 아니다. 기존 사용자 Downloads 실행본은 이번에 교체하지
않았고 첫 검증 Release가 아직 없으므로, 그 옛 파일도 이미 새 패치를 받는다고 설명하면 안 된다.

## 실제 배포 전 필수 조건

1. 2026-09-05 exact cleanup 차단은 해결됐다. 실패한 네 QA identity의 각 166개 파일을 해시 검증 후
   휴지통으로 옮기고 target=0을 확인했다. 새 검사에서도 실패하면 같은 evidence-before-delete 원칙을
   적용한다. source/Library/Bee/다른 실행본/저장 파일은 제외한다. 상세 기록은 PROJECT_STATE.md.
2. 보행·충돌 보정, 4방향 착석/업무, native shop 클릭, 다음 날 가족별 출근, 음소거를 독립 runner로 검증한다.
3. 사용자 실제 보행 화면 승인은 2026-09-06 받았다. clean committed main, 정확한 Unity 6000.3.21f1
   Release 빌드가 아직 필요하다. 기존 미추적 누나 입력 13개는 이후 사용자 continuation으로 원본 그대로
   3b47605e에 보존했다. 이는 누나 모델 생성/게임 승격이 아니다.
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
