# 통합 흰색 사무실 HUD · 2026-09-07

사용자의 직전 UI 시안 및 "빌드하고 push해" 요청에 대한 실제 Unity 표시 계층 구현이다.
공개판 `fc-win-20260907.1`의 교체나 새 공개 패치 게시 증거는 아니다.

## 구현

- 회사 아이콘·회사명·실제 회사 자금·날짜·요일·시간·pause·1×/2×/4× 한 흰색 상단 바.
- 회사/인사/사업/연구/투자 한 하단 바, 기존 생성 일러스트 atlas 그대로 재사용.
- 기본 사무실은 패널/dim 없이 보이고 메뉴 재클릭·사무실 복귀·ESC로 닫는다.
- 기존 사업 계약/배정/제품·사무실 관리·인사·주식 경로 및 의미 상태/세이브는 유지한다.
- HTML 화면을 게임에 삽입하거나 사무실·인물·가구·카메라를 이미지로 대체하지 않았다.

## 확인

- Unity 6000.3.21f1, FastQA data build: `20260907-111725-938` PASS (21.762s overall, 20.276s build).
  Development validation player이며 clean Release 후보가 아니다. 실행 파일은 Git에 넣지 않는다.
- Editor `MainNavigationHudValidation.Run`: PASS. 위치 `Artifacts/FastQa/UnifiedOfficeHud/editor-white-hud-validation.log`.
- Native D3D11 HUD: `20260907-112000` PASS. 1280×720, 1024×768, 1600×1000, 1920×1080에서
  상단 한 줄 정렬·회사 자금/날짜 연결·텍스트 넘침 없음·최소 클릭 크기·5탭·Business toggle/ESC·배속·pause/resume.
- 전체 기존 navigation suite `20260907-111810`: PASS, 54 EventSystem 클릭 경로, 33 캡처,
  contract/product/build/stock adapter 및 격리된 save roundtrip. 실제 OS 마우스 입력은 하지 않았다.
  기존 suite의 2560×1440은 1280×720 창을 2× 캡처한 것이며 native 2560 layout 증거가 아니다.
- 첫 실행 `111205`의 빠른 캡처는 로딩 화면을 포함하여 office 증거에서 제외했다. 캡처 전 로딩이 끝났는지
  기다리고 검사하도록 수정한 뒤 새 빌드/새 출력으로 다시 검증했다. 기존 상세 화면의 offscreen capture는
  IMGUI나 3D overlay 전부를 담는 증거가 아니며, 이 폴더의 office PNG는 실제 전체 presented frame이다.
- 별도 비공개 Windows desktop에서만 Player를 실행했다. 업무 desktop 및 사용자 main/save 파일은 불변.
  기존 캐릭터 작업 19개 파일의 SHA256도 이전 보존 기록과 동일했다.

`hud-result.txt`, `navigation-result.txt`, `office-1280x720.png`, `office-1024x768.png`는 위 실행에서 복사했다.
추가 원본 출력은 `Artifacts/FastQa/UnifiedOfficeHud/`에 있으며, 자산 원본 prompt/hash는 `asset-provenance.json`이다.

## 빌드·배포 경계

후속 사용자 요청 "응 백업하고빌드"에 따라 기존 Father importer와 OlderSister 작업 19개를 외부에
백업하고 임시 분리한 뒤 clean main으로 빌드했다. 성공·실패 모두 원본 복원하며 SHA256 19/19 일치했다.
백업은 `C:/Users/godho/Documents/Codex/2026-09-04/older-sister-3d-continuation/backups/character-pre-release-20260907-112928`에
`manifest.json`, `original/`, `build.json`, `restoration.json`과 함께 보존했다. 기존 작업은 커밋하지 않았다.

## 후속 정식 Release 후보 검증

- 빌드 소스: `66cac4e9c68133ca21f87e186d8ee62400d0f3ec`, Unity 6000.3.21f1, non-Development Release,
  clean tree 및 build-input fingerprint 전후 일치, 빌드/해시 기록까지 58.415초. `release-build-info.txt` 참조.
- 후보 위치: `Artifacts/PatchCandidates/66cac4e9-ee41c2270f3f4f2d8d41546e3d8d5cd3/payload`.
  원본 `candidate.json`은 빌드 단계의 기록이고, 이후 gameplay 검증과 공개 승인은 별도다.
- 첫 후보 `56fe0fd4-49c89918539b4a62b7c3e68564cd6501`은 컴파일 후 Unity의 새 HUD texture metadata
  재직렬화(줄 끝 공백 7곳)로 dirty guard가 차단했다. 해당 후보 전체 payload만 휴지통으로 이동했으며
  `failure.json`, `deletion.json`, `unity.log`는 보존했다. `66cac4e9`는 그 공백만 반영한 수정이다.
- 이 정식 후보를 명시적으로 지정한 private-desktop HUD QA `20260907-113734`: PASS, native 4개 해상도,
  오류 0. `release-hud-result.txt`, `release-office-1280x720.png`는 해당 실행의 실제 전체 화면이다.
- 같은 후보의 full navigation QA `20260907-113811`: PASS, 54 EventSystem 경로/33 캡처,
  isolated save roundtrip. `release-navigation-result.txt` 참조. 위에 적은 2560×1440 supersize 한계는 같다.
- 두 UI runner 모두 사용자 main/save 해시와 interactive desktop 불변을 확인했다. 전면 실행/OS 입력 없음.
- 같은 후보의 `Artifacts/NormalAutonomy/UnifiedWhiteHudRelease20260907`: 출근 gate 및 독립 CSV PASS.
  Player/누나/아빠/엄마의 release는 09:00/09:01/09:02/09:03 정확히 일치했고 최초 착석은 각각
  09:09/09:06/09:17/09:19로 모두 09:20 전에 정상 도착했다. 09:04 활성 이동/도착 조건과 오류 0 통과.
  출근 뒤 09:50까지 관찰했으며 '동시 네 명 계속 근무'나 새 캐릭터 미술 승인으로 해석하지 않는다.
- 독립 CSV 분석은 8,128개 표본, 위반 0, 최대 rail fraction error 0.000026063,
  navigating 무진행 최대 1.001초(<8초). `release-normal-autonomy-observed.txt`,
  `release-independent-navigation.json`, `release-process.json` 참조. 창 핸들 0 및 desktop 불변 통과.
  가구 구매는 programmatic setup이고 관찰 구간의 actor/route/pose injection은 없다.
  오후·밤만 시간 이동하고 다음 날 08:50→09:50은 정상 1×로 관찰했다. 실제 업데이트 수신 검증은 아니다.
- 원본 19개 및 후보 169개 파일의 기록된 SHA256을 다시 확인했으며 불일치 0.

Release 후보 빌드는 공개 패치 게시가 아니다. 기존 Downloads 메인 EXE, AppData 패치 캐시 및 사용자
세이브를 교체하지 않았으며, 현재 공개판 `fc-win-20260907.1`은 유지된다.
