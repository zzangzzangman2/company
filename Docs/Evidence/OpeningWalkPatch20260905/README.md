# 2026-09-05 보행/상점/개발 설정/패치 증거

**부분 PASS, 전체 게임 Release는 BLOCKED.** `PROJECT_STATE.md`를 먼저 읽는다.
이 폴더에는 실행 가능한 게임이 없고 실제 촬영 영상/CSV/판정 기록만 있다.

- `tile-centres-overview.mp4`: 네 명의 정상 새 게임 24초 연속 촬영. 노랑=타일 경계,
  청록 십자=실제 이동 root, 분홍 십자=바닥에 투영한 양발 뼈 중점.
- `four-actors-closeup.mp4`: 같은 모든 프레임과 실측 시간으로 만든 4인 추적 확대 영상.
  433개의 실제 렌더 프레임이며 MP4 30fps 보간은 중복 프레임이다. 원래 캡처는 약 18fps다.
  route/clock/teleport 주입 없이 정상 coordinator가 이동했다. 전체 22개 연속 sheet를 검사했다.
- `analysis.json`, `walk-trace.csv`, `projection.csv`: root 중심선 최대 오차 0.000076px,
  네 명 모두 ankle midpoint median<=4 / max<=8px. 이것은 신발 바닥 픽셀 미끄러짐 승인과 다르다.
- `opening-shop-final.txt`: 정상 60초/4개 독립 body/4방향 세트 구매/340만 원 잔액/겹침 미차감.
  controller 호출 검사이며 native pointer나 IMGUI 화면 증거가 아니다.
- `reload-result.json`: 한 EXE에서 settings 1→0.5→잘못된 JSON→1, 정상 snapshot만 세 번 적용,
  잘못된 저장 뒤 실제 속도 약0.50001 유지, 복원1.0. 재빌드0/창 전면화0.
- `updater-tests.json`: Windows PowerShell 5.1의 36개 inert fixture 테스트 PASS.
  실제 게임/네트워크 패치 다운로드 PASS가 아니다.
- **`player-father-3d-interaction-final.txt`: FAIL, 정면 충돌 중 보이는 외곽 84픽셀 겹침.**
  이를 숨기거나 위 산책 결과로 대체하지 않는다. 좌석/업무 단계 전에 중단했다.

촬영 빌드: dirty base `7c9fa606`, FastQA run `20260905-214017-576` (48.655초).
촬영 디렉터리: `Artifacts/FastQa/WalkAudit-default-20260905-214939/`.
그 뒤 변경된 0.445/0.415 동적 충돌 반경과 traffic recovery의 semantic 축 보호는 아직
새 Player 영상에 포함되지 않았다. 따라서 이 영상을 최신 모든 소스 변경의 검증이라고 부르지 않는다.

실패 확인용 payload의 정리 명령이 도구 안전정책에 차단됐다. 정확한 로컬 대상은
`C:/Users/godho/Documents/Codex/fc_agents/integration_p0/Artifacts/FastQa/cache/WindowsPlayer`이며
**삭제되지 않았다**. 해시/실패 기록은 같은 프로젝트의
`Artifacts/FastQa/FailedPayloadEvidence/20260905-220419-contact-overlap/`에 보존돼 있다.
이 대상 정리 문제를 해결하기 전 실패 EXE를 실행/배포하지 않는다. source, Library/Bee, 다른 캐시,
사용자 저장 데이터, 누나 입력 파일은 삭제 대상이 아니다. 상위 폴더를 통째로 지우지 않는다.

다음 담당자는 정리 완료 후 새로운 QA identity로 충돌→정상4인→가구 회피→4방향 착석/업무를 재검사하고,
독립 native shop/다음날 출근/mute/발바닥 미끄러짐과 사용자 화면 승인까지 통과해야 한다.
그 뒤에만 `GITHUB_PATCHING.md`의 공개 Release 단계를 진행한다. 종료 요청은 전부 완료된 뒤에만 실행한다.
