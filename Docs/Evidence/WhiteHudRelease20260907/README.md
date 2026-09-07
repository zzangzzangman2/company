# 흰색 통합 HUD 공개 배포 · 2026-09-07

[fc-win-20260907.2](https://github.com/zzangzzangman2/company/releases/tag/fc-win-20260907.2), sequence **5**,
release ID **383977367**, 게임 소스 **c519985513f3953e6612467f48c895ac29100bf6**.
회사에서 만든 실행파일은 Git에 없었으므로 집 정본 main에서 다시 만든 non-Development Release를 사용했다.
회사 원본 QA 기록은 별도 `../UnifiedOfficeHud20260907/`에 있고, 아래는 집에서 새로 실행한 증거다.

## 결과

| 검증 | 실제 결과 |
| --- | --- |
| 빌드 / 순수 코어·save / Editor broad | Unity 6000.3.21f1, 184.07s / 11.035s / 23.851s PASS |
| HUD | 1280×720, 1024×768, 1600×1000, 1920×1080 실제 렌더 PASS, 글자 넘침 0 |
| 전체 메뉴 | 54개 EventSystem 포인터 경로, 33개 캡처, 격리 save roundtrip PASS |
| 정상 이동 | 8,148 표본, 충돌 위반 0, 최대 타일 축 오차 0.0000991875칸 (기존 한계 0.0001 이내) |
| 다음 날 출근 | 09:00/01/02/03 정확히 release; 모두 09:20 전 첫 정상 착석 |
| 정상 착석/업무 | 안정 표본 3,248개, 오류 0; 4명·4방향 전환 확인 |
| 두 몸체 × 네 방향 자세 | 264개 표본, 의자 관통 0, 최대 개별 손 오차 0.0089 |
| 24초 실제 보행 | 391프레임, 네 명 발목 중점·앞발 교대 gate PASS, runtime/충돌 오류 0 |
| 사업 UI/실제 업무 | 첫 4 PH의 248/248분 착석; 개발 마지막 4 PH와 지원 2 PH, 첫 판매 180,000원·주간 60,000원 |
| 음소거 | 8,264 muted samples, 최대 output peak 0 |
| 패치 회귀 | 81/81 PASS, shipping worker bytes 불변 |
| 실제 GitHub v2→v5 | 16개 변경 파일 / 162,939,018 compressed bytes / 175 progress events → 100% |
| 최종 파일 | 153개 재사용, 169개 전체 SHA-256 검증; 공개 자산 16개의 ID/size/digest 재확인 |
| 사용자 파일 | 고정 메인 169개, 사용자 v2 cache/pointer, save/backup 5개 불변 |

신규 PC 설치 ZIP: **273,144,271 bytes**, SHA-256
`b42c4f76f94766c12179d3086ca2df4e4352145e01ba7cfc99450622abdfbd86`.
기존 PC는 같은 메인 EXE를 유지하며 게임 내부에서 패치한다. 캐릭터/가구/게임 로직/세이브는 v4와 같다.

## 증거 범위

- HUD 이미지 네 장은 실제 private-desktop presented frame이다. 정상 이동/착석의 별도 offscreen PNG는
  HUD 전체 검증으로 사용하지 않는다. full navigation의 2560×1440은 1280×720을 2배 캡처한 것이지 native layout이 아니다.
- UI는 EventSystem으로 검사했다. 기존 native 4회 구매/회전/겹침 거부 기록은 변경 없는 상점 코드에만
  해시 연결했다. 새 OS 마우스 입력을 수행하지 않았다. 모든 게임 창은 사용자 desktop과 분리했다.
- 정상 이동은 실제 coordinator/경로/회피/착석이며 가구 setup은 transaction API다. 오후/밤만 checkpoint로
  건너뛰고 다음 날 08:50→09:50은 정상 1×로 관찰했다. 네 명이 동시에 계속 앉아 있었다고 주장하지 않는다.
- 8방향 chair-fit은 통제된 pose injection이고 정상 착석 표본은 별도다. 원본 body/rig/clip/가구를 바꾸지 않았다.
  실제 보행의 00/03/07/11/15/19 시트와 8개 자세 이미지를 직접 검토했다. 391프레임 전부 육안 검토,
  모든 skin 픽셀의 zero foot-slip, 신규 모델 외형 승인을 의미하지 않는다.
- 사업의 초기 하청 4 PH는 정상 새 게임의 실제 작업이다. 제품 통합은 이전 하청/개발 20 PH를 코어로
  준비하고 마지막 개발·지원은 실제 업무로 수행했다. 정산만 시간 이동하며 전체 주간 native 플레이는 아니다.
- 공개 수신은 실제 shipping worker/GitHub를 사용한 별도 설치의 PrepareOnly다. 사용자 메인에서 새 Unity
  활성화·재시작 또는 다운로드 UI를 새로 관측한 것은 아니다. 기존 unchanged-worker 재시작 증거는 v2 기록이다.

## 패키징 경계와 보존

원본 빌드의 170번째 파일은 `FamilyCompanyPrototype_BurstDebugInformation_DoNotShip/.../lib_burst_generated.txt`
(60,653 bytes)였다. 게시 전 publisher만 중단하고 draft가 없음을 확인했다. 해당 비실행 진단 텍스트만 exact-root/hash
기록 뒤 후보 바깥 `non-shipping-debug`에 보관했다. 원본 candidate manifest는 170개를 유지한다.
실제 배포한 169개 runtime 파일은 검사 때와 전부 같은 해시이며 재작성/재빌드하지 않았다. `packaging-exclusion.json` 참조.
receipt 보조 스크립트의 따옴표 파싱 오류는 증거 조건 실행 전에 수정했다. runtime gate 실패나 허용치 완화가 아니다.

gate JSON·release-receipt는 원본 bytes 그대로 보관했다. 내부 Artifacts 경로는 당시 실행 위치이며 다른 PC의
새 후보 승인서가 아니다. 각 basename 파일이 함께 있고 SHA-256으로 확인할 수 있다. `receipt-builder.ps1`은 감사용이다.
`evidence-inventory.json`은 수집 당시 원시 증거 해시 목록이며, 이후 작성한 이 README는 포함하지 않는다.
이후의 `remote-final-inventory.json`은 새 공개 자산을 포함한 branch/tag/release 검사이며 prohibited/unknown 모두 0이다.
원시 Unity 로그의 기존 줄 끝 공백도 해시 증거라 그대로 보존했다. 일반 문서의 공백 검사와 원본 로그 보존을 구분한다.
