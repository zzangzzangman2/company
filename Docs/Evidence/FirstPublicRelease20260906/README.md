# 승인 공개판 v2 · 실제 GitHub 패치 · 고정 메인 설치 증거

2026-09-06 사용자 "ㅇㅇ 승인배포" 이후 실제 공개/설치/재시작까지 완료한 기록이다.

## 정본 identity와 실행

- [공개 Release fc-win-20260906.2](https://github.com/zzangzzangman2/company/releases/tag/fc-win-20260906.2)
- commit `ee48a72c8e9979a605a64c59820af8d23fdbcf4c`, sequence 2, Unity 6000.3.21f1
  (`c02631ffc030`), non-Development. [BUILD_INFO](BUILD_INFO.txt).
- 검증 후보: `Artifacts/PatchCandidates/ee48a72c-1e35194f450d41fc809718562fd3b24c/payload`.
- 집 메인: `C:\Users\godho\Downloads\FamilyCompany_Playtest\FamilyCompany.exe`.
  회사 최초 ZIP 설치와 이후 동일 EXE 계약: [MAIN_GAME_ENTRY](../../MAIN_GAME_ENTRY.md).
- 167 public assets, 169 game files. ZIP 271,062,921 bytes, SHA-256
  `8808d81987cdd997f5b70bdd7151304b798c9f6b12cbd71e5f32b019f456c303`.
- manifest SHA-256 `91134aeba7d32f91a6c5e25fea06a7c59ae693d852ba3153c045ac7a61e42612`.
  활성 snapshot `%LOCALAPPDATA%\FamilyCompany\PatchedGame\versions\2-91134aeba7d3`.
- [불변 배포 receipt](release-receipt.json): productionEligible/userVisualApproval true.
  receipt SHA-256 `0e69519f2f1ffb90150f30b68a5faaa1acf37b5378800740f72ddca91833b575`.
  문서 후속 commit을 이 빌드 SHA로 가장하지 않는다.

## 실제 공개 서버·설치 결과

| 검사 | 실제 결과 | 증거 |
| --- | --- | --- |
| production Unity public repair | 536,348 bytes 수신, 6 progress events, 실제 100%, 검증, 부모 정상 exit 0, 최신 child READY_CURRENT | [repair-result](repair-result.json), [parent](repair-parent.log), [child](repair-child.log), repair-runs/ |
| 공식 ZIP 설치 | public ZIP digest + 169개 파일 검증, 고정 경로 설치, 구 파일 157개 휴지통 이동 | [installation](installation.json) |
| 실제 Downloads 메인 | public latest 검증, 수신 0, 부모 정상 exit 0, 최신 child READY_CURRENT | [main-result](main-result.json), [parent](main-parent.log), [child](main-child.log), main-runs/ |
| 사용자 저장 보존 | 세이브/백업 5개 전후 해시 동일 | repair-result/main-result/installation의 savesUnchanged |

repair seed에는 optional UnityCrashHandler64.exe만 빠져 있었다. 실제 다운로드지만 새 gameplay revision
사이의 업그레이드라고 부르지 않는다. main run은 이미 최신인 경우이므로 가짜 다운로드 %를 만들지 않았다.
두 검사 모두 production worker, offlineQaBypass=false, public GitHub를 사용했다. private desktop은
전환하지 않았고 사용자 입력/화면 캡처는 하지 않았다. child 준비 후 이 작업 소유 job만 종료했다.
부모 정상 종료는 실제 확인했지만 child가 자동 정상 종료했다고 주장하지 않는다.

installation.json은 설치 직후 작성되어 postInstallBoot=PENDING을 그대로 보존한다. 뒤이어 실행한
**main-result.json의 passed=true가 설치 후 부팅 검사 완료**다. 원본 기록을 소급 수정하지 않았다.
구 메인 9144fa0e 폴더는 휴지통에서 복구 가능하나 업데이트 실패 때 실행할 fallback은 아니다.

## gameplay·UI·승인 결합

- [normal-runtime](normal-runtime.json): fresh ee Release 정상 자율 이동 8,112 samples,
  tile-rail 오차 최대 0.0000245625, 충돌/interaction/runtime 오류 0. Settled Working 3,096 samples,
  개별 손 오차 최대 0.008899. 전환 구간 outlier는 삭제하지 않았다.
- 다음날 실제 입장 09:00/09:01/09:02/09:03, 첫 Working 09:09/09:06/09:17/09:19.
  09:04 progress 및 before-09:20 기준을 완화하지 않았다.
- [seated-fit](seated-fit.json), [chair-fit CSV](chair-fit.csv): Player/Father × 4방향 ×33 typing
  poses =264, 의자/skin penetration 0, 무릎 80~140도. 제어 pose fixture이지 자율 이동 증거는 아니다.
- [walk-acceptance](walk-acceptance.json): fresh normal 434 frames/23.966초, 네 가족 foot-midpoint gates
  median ≤4px/max ≤8px PASS. 수치가 수학적으로 완벽한 skin 무미끄러짐을 증명하지는 않는다.
- 실제 native 구매/회전/겹침/UI와 사용자가 승인한 영상·착석 sheet는
  [원본 8ce 증거](../ReleaseCandidate8ce7d3ed20260906/README.md)의 identity를 유지한다.
  새 클릭 검사를 한 것처럼 쓰지 않는다. 승인 이후 변경은 updater 도구와 문서뿐이다.
- [native-binding](native-binding.json), [game-content-binding](game-content-binding.json),
  [이전 단계 binding](prior-gameplay-content-binding.json): 1,751 resource objects와 gameplay
  assembly/scene/texture 동등성을 독립 검증했다. 허용한 정규화는 metadata object 순서/참조와 build GUID뿐이다.
  교정된 새 worker는 이전 실패 PASS를 재사용하지 않고 아래 회귀와 실제 GitHub run으로 검증했다.
- 일반 mute 출력 0과 원본 native unmuted 비영을 확인했다. native 설정 영속성 검사는 아니다.
  가족은 Player V8 ×2 / Father V19 ×2이며 고유 엄마/누나 모델이 완성된 것은 아니다.

## updater 회귀와 철회

[updater-regressions](updater-regressions.json): core 51/51, latest-only 6/6, 실제 restart worker 10/10.
[manifest-before-fix](manifest-before-fix.json)→[manifest-after-fix](manifest-after-fix.json): 실제 production
block의 정상 single/page-2 응답 실패를 재현한 뒤 7/7. [draft-lookup](draft-lookup.json) 7/7.
총 81 local checks와 위 실제 public GitHub 검사는 서로 다른 증거다.

첫 fc-win-20260906.1은 실제 production digest lookup에서 자동 `$Matches`가 `$matches` 배열을 덮어써
실패했다. 공개를 취소하고 정확한 owned release/tag를 제거했다. [실패](failed-release1.json)와
[회수](failed-release1-retirement.json)를 보존하며 관련 whole payload/package/seed는 휴지통으로 보냈다.
v2는 `$manifestAssets`로 분리했다. draft는 numeric ID로 정확히 검증한다.
[GitHub 공식 API](https://docs.github.com/en/rest/releases/releases#get-a-release-by-tag-name)의 tag 조회는
published release용이므로 draft 확인에 쓰지 않는다. digest/승인/identity gate를 우회하지 않았다.

실제 public N→N+1 gameplay revision과 실제 네트워크 장애 복구는 이번에 실행하지 않았다.
파일 단위 delta와 실패 복구는 local fixture 범위다. 다음 공개 변경판에서 실제 버전 간 경로를 추가 검증한다.
최신 확인 실패 시 구버전/오프라인 실행은 금지한다.

## 증거 보존·회사 재개

[evidence-inventory](evidence-inventory.json)는 수집 시점 원본 파일 크기/SHA-256 목록이다(나중에 추가한
이 README와 remote-inventory.json은 제외). receipt의 Artifacts evidencePath는 당시 원본 경로를 유지하며 이 폴더에 복사한
동일 basename의 bytes/hash가 일치한다. `.gitattributes`의 -text로 회사 checkout의 줄바꿈 변환을 막는다.
[검토한 release inventory](../VerifiedReleaseInventory.json)는 이 폴더의 repo-relative receipt와
정확한 public asset ID/size/digest를 연결한다. 새 asset을 blanket allow하지 않는다.

[최종 원격 감사](remote-inventory.json)는 문서 push 직전 origin/main ee48a72c와 active branch/tag,
모든 public release asset을 재조회한 결과다. prohibited=0, unknown=0, paginationComplete=true.
공개 Release는 ID 383548158 / fc-win-20260906.2 하나이며 검토 자산 167개다. 실패 `.1`은 없다.

현재 작업 소스는 `C:\Users\godho\Documents\Codex\fc_agents\integration_p0`, main이다.
회사에서 개발을 계속할 때만 clean main에서 pull하고 AGENTS/PROJECT_STATE부터 읽는다.
플레이는 고정 메인만 사용한다. 소스 push는 공개 게임을 바꾸지 않고, 문서만 바뀌었다고 다시 빌드하지 않는다.
세이브 자동 동기화, 추가 native 입력, Higgsfield 사용, PC 종료는 이 배포에서 하지 않았다.
