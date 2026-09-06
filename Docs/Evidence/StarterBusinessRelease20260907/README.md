# 첫 사업 루프 공개 배포 검증 · 2026-09-07

공개: [fc-win-20260907.1](https://github.com/zzangzzangman2/company/releases/tag/fc-win-20260907.1),
sequence 4, release ID **383654897**.
게임 소스 **c0709823c0e45c4152c673ca0b67d7a1e1506bc7**, clean main / Unity 6000.3.21f1 / non-Development.
이후 문서와 제작용 publisher 버전 guard의 commit은 게임 빌드 SHA가 아니다.

## 실제 결과

| 검증 | 결과 / 기록 |
| --- | --- |
| 순수 시뮬레이션·save / Editor 전체 | PASS, 7.304초 / 19.404초. simulation-pure-result.json, editor-broad-result.json, editor-broad.log |
| 동일 Release 빌드 | PASS, 70.442초. BUILD_INFO.txt |
| 정상 보행·회피 | 8,096 분석 표본, 타일 축 위반 0, 최대 오차 0.000022875칸. normal-navigation.json |
| 다음 날 4명 출근 | 09:00~09:03 순차 출근, 모두 09:20 전 정상 착석. normal-observed.txt |
| 정상 착석 전환·업무 | 안정 표본 2,939개, 실패 0, 개별 손 오차 최대 0.008899. normal-seat-blend.json |
| 두 몸체 × 네 방향 착석 | 264 표본, 의자 관통 0, 개별 손 오차 최대 0.0089. chair-fit.csv, chair-fit-*.png |
| 실제 보행 24초 | 393프레임, 4명 앞발 교대 52~55회, 발목 중점 기준 모두 PASS. walk-analysis.json, four-actors-closeup.mp4 |
| 실제 사업 업무 | 입문 4인시(필요/관측 착석 248/248분), 개발 마지막 4인시, 유지보수 2인시. business-result.txt / 두 CSV |
| 판매·주간 요금 | 첫 판매 180,000원 + 첫 주 요금 60,000원, 고객 3곳. business-billing.png |
| 충돌·runtime / mute | 관측 범위 충돌·runtime 오류 0, mute 출력 0. normal-runtime.json, 원시 관측 CSV |
| 패치 worker 회귀 | 81/81, 변경 없는 shipping worker 해시 연결. updater-regressions.json |
| 실제 GitHub v2→v4 | 변경 16개 / 160,602,031 bytes / 92개 실제 다운로드 진행 이벤트 → 100%, 153개 재사용, 169개 해시 검증. patch-public-delta.json |
| 공개 자산 | 18개 asset의 ID/size/SHA-256 검증 후 정식 공개. published-inventory.json |
| 사용자 파일 | 고정 메인 169개·v2 cache pointer/snapshot·save/backup 5개 불변. patch-before.json / patch-after.json |

설치 ZIP: **271,014,149 bytes**, SHA-256
`31318593d41f50bb512d1734f17c571613bc7320d3c9e5e4d35e52c14d26fea0`.
기존 사용자는 ZIP 재설치 없이 같은 메인을 쓴다.
v3에서 바뀐 신규 gzip은 15개이며, 위 16개/153.2 MiB는 사용자의 **v2 출발** 기준이다.

## 증거 범위를 구분한다

- 정상 이동·출근 관측은 새 실행본의 실제 coordinator/경로/충돌/착석 상태다.
  구매는 거래 API이며 오후/야간 준비용 시간 이동을 명시했다. 다음 날 출근은 실제 정상 시간 진행이다.
- native-binding.json은 이전 8ce7d3ed의 실제 native 4회 구매/회전/겹침 거부를 **변경 없는 상점 코드**에만 연결한다.
  공유 occupancy에는 읽기 전용 동적 경로 검사 추가만 허용했다. 새 Windows 마우스 입력을 수행했다고 주장하지 않는다.
- chair-fit은 자세 주입을 사용하는 통제된 8방향 조합이다. 별도의 normal-seat-blend가 실제 업무를 측정한다.
  next-day-normal-seated 이미지도 네 명이 동시에 계속 앉아 있다는 증거로 확대하지 않는다.
- 사업의 첫 4인시는 정상 새 게임/4배속이며 이동·착석 준비 시간은 업무가 아니다.
  개발·지원 통합은 앞선 계약/개발 20인시를 코어 체크포인트로 구성하고 실제 마지막 개발/지원을 수행했다.
  정산 시각만 건너뛰었고 in-memory save 왕복을 확인했다. 중단 없이 전체 주간을 native 플레이한 것은 아니다.
- 시각 검토: 8개 실제 착석 이미지와 정상 장면, 시간대별 보행 시트 00/03/07/11/15/19를 직접 확인했다.
  walk-review-*.png가 이 시트이며 전체 393프레임을 모두 육안 검토했다고 쓰지 않는다.
  영상 타이밍은 실제 캡처 간격이다. 발목 중점/앞발 교대는 픽셀 피부 중심이나 수학적 zero foot-slip 증명이 아니다.
  모델·rig·clip·가구 소스는 기존 승인 기준과 동일하며 새 모델 승인/승격이 없다.
- public worker는 격리 v2 저장소에 실제 공개 최신판을 준비했다. PrepareOnly는 정상적으로 활성화 전 멈춘다.
  이번에 사용자 메인에서 Unity 재시작/UI를 새로 관측한 것은 아니다. 변경 없는 기존 v2 재시작 증거는
  [이전 공개 기록](../FirstPublicRelease20260906/README.md)에 있다. 사용자의 실제 다음 패치 수신을 보존했다.

## 실패를 숨기거나 재승격하지 않았다

- a307c597 후보: 끝점 충돌 투영 때문에 40개 이동 표본이 최대 0.006725칸 이탈.
  끝점에도 같은 축 제약/정지·재경로 규칙을 적용한 뒤 **새 identity** c0709823로 재검증했다.
  red-navigation-a307c597.json, red-candidate-retirement/deletion.json에 실패와 휴지통 이관 기록이 있다.
- e7ed60c9 후보: 정산 후 QA feature 버튼의 raycast가 UI 갱신 전에 발생하여 실패했다.
  실제 클릭 가능한 새 버튼을 최대 3초 기다리도록 QA만 수정한 뒤 새 후보의 전체 gate를 다시 실행했다.
  red-business-ui-*에 원본 실패와 payload 폐기 기록이 있다.
- Windows PowerShell 5.1의 ZIP 생성은 역슬래시 entry names를 만들어 publisher 검사에서 외부 쓰기 전에 실패했다.
  해당 ZIP만 해시 기록 후 휴지통으로 옮기고 PowerShell 7에서 다시 생성/검증/게시했다.
  rejected-ps51-zip.json에 기록했다. 실패 후보를 배포하거나 ZIP 검사 조건을 완화하지 않았다.
- 게시 뒤 제작용 publisher에 PowerShell 7.2 이상 guard를 추가했다. 5.1의 즉시 거부/AST 파싱과
  draft 조회 회귀 7개를 검증했다. publisher-host-guard.json. 게임 worker나 Unity 게임 코드는 바뀌지 않았다.
- receipt 생성 중 PS5.1의 확장 속성 포함 문자열 직렬화가 과도한 메모리를 썼다.
  그 소유 보조 프로세스만 중단하고 일반 파일 문자열/PS7로 기록했다. 게임 gate 실패로 오인하지 않는다.

## 파일 배치

release-receipt.json과 gate JSON은 원래 bytes를 보존했다. 내부 Artifacts 경로는 당시 실행 경로이며
새 checkout에서 재사용 가능한 신규 승인서가 아니다. 해당 basename의 JSON, raw CSV, 이미지, 영상이
이 폴더에 보관되어 있다. 원본 receipt의 evidence SHA-256은 보관된 같은 이름 파일과 대조할 수 있다.
receipt-builder.ps1은 이 기록의 생성 근거인 감사용 코드이며 새 후보를 자동 승인하는 도구가 아니다.
evidence-inventory.json은 보관 파일 해시 목록이다.
