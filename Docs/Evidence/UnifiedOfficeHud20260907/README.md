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

UI 범위만 커밋·push한다. 기존 Father importer와 미완료 OlderSister 파일의 임시 분리·복원 승인 없이
clean Release guard를 우회하지 않는다. 현재는 검증용 빌드까지 완료했으며 정식 Release 후보/공개 패치는 보류다.
기존 Downloads 메인 EXE, AppData 패치 캐시 및 사용자 세이브를 교체하지 않았다.
