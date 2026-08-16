# PROJECT STATE

이 문서는 과거 작업 일지가 아니라 **현재 실행 가능한 상태, 아직 통합되지 않은 상태, 정확한 다음 작업**만 기록하는 정본이다. 날짜별 구현 증거는 `History/Reports/`에 보존하며 이 문서보다 우선하지 않는다.

## 2026-08-16 / 회귀·실패 실행본 영구 삭제 정책

- [REGRESSION_BUILD_POLICY.md](REGRESSION_BUILD_POLICY.md)를 build/deploy의 영구 fail-closed 정본으로 추가했다. user-visible regression, failed gate, stale/unverified provenance, self-PASS-only candidate는 current 또는 Downloads에 존재할 수 없다.
- 판정 시 exact-root fence와 unrelated build 보호를 먼저 확인하고, SHA/log/manifest 같은 비실행 evidence를 payload 밖에 보존한 뒤 EXE, `*_Data`, `UnityPlayer.dll`을 포함한 전체 실행 payload를 즉시 삭제한다. 이름 변경·격리·LKG suffix만으로 회귀 payload를 보존하지 않는다.
- 회귀 payload의 재승격은 금지한다. 수정 뒤 모든 관련 regression oracle, 기존 필수 gate, 독립 gate를 통과하고 새 commit/input fingerprint/build ID를 가진 새 payload만 처음부터 빌드할 수 있다.
- Windows release의 독립 필수 oracle은 fresh 08:50에서 네 가족이 `player` 09:00, `older_sister` 09:01, `father` 09:02, `mother` 09:03에 실제 입장·이동·assigned seat 착석을 증명해야 한다.
- 이번 변경은 문서 계약만 추가했다. 기존 build/deploy 자동화가 regression 발견 시 evidence→전체 payload 삭제→검증된 정상 build rollback/없으면 empty를 실제 구현하고 독립 테스트로 증명하기 전에는 current/Downloads 실제 배포에 사용하지 않는다.

## 2026-08-15 / Management UI validator Windows argv P2 후보

- `Validate-ManagementUiV2.ps1`의 full-runtime Roslyn compile은 source 파일 234개를 Windows argv로 직접 펼치지 않고, compiler identity·reference/options·source-root/발견 순서를 보존한 UTF-8 no-BOM response file을 사용한다.
- response와 validator output은 각각 GUID 기반 시스템 temp fence를 사용하며, path escaping, missing input, compiler failure/launch exception, stale response, 동시 실행에서도 자기 temp만 `finally` 정리한다. Assets·UI runtime/layout·font·art는 변경하지 않았다.
- 실제 입력 파일은 response serialization 전에 절대 경로로 정규화한다. 정규화 뒤에도 첫 문자가 `@`인 raw argument는 Roslyn nested response directive이므로 temp 생성·compiler 실행 전에 결정론적으로 거부한다. 실제 Roslyn의 quoted `@...` CS2011 재현과 상대 `@` 파일의 안전한 절대 경로화 compile을 fixture로 고정했다.
- 수정 전 89,515자 direct argv는 Win32 error 206으로 재현됐고 같은 425개 argument의 91,434-byte response compile은 통과했다. exact-base contract, 공백·한글·`@` path component·quote/backslash, raw leading-`@` fail-close, deterministic bytes, deliberate CS1513, missing compiler/reference, launch exception, stale response, concurrent 2-run, cleanup을 포함한 15개 외부 fixture가 통과했다.
- non-Unity layout/contrast harness, full runtime compile, editor-validator compile, static structure를 통과했다. `ManagementUiV2Validation.Run` 안의 AssetDatabase/font rasterization/GUID/meta/Sprite header 검사는 이번 작업의 Unity 실행 금지 때문에 실행하지 않았으며, 향후 승인된 단일 Unity slot에서 별도 실행해야 한다.

## 2026-08-15 / clean integration HEAD Windows 자동 배포 준비

- 기존 `BUILD_WINDOWS.cmd` → `Build-FamilyCompanyWindows.ps1` → `WindowsPlayerBuild.BuildWindowsX64`의
  비-Development Windows x64 경로를 재사용하고, clean `codex/integration-p0-qa` HEAD 변경만 debounce해
  Downloads로 승격하는 명시적 start/stop watcher를 추가했다. 서비스·재부팅 자동 시작은 등록하지 않는다.
- 실제 `Unity.exe` ProductVersion과 project revision을 `6000.3.21f1_c02631ffc030`으로 함께 검사한다.
  machine-wide build lock을 소유한 뒤 다른 작업방 Unity/QA player가 유휴가 될 때까지 기다리며 강제 종료하지 않는다.
- candidate의 EXE/Data/UnityPlayer.dll/BUILD_INFO/deploy manifest/runner를 먼저 검증한다. Downloads target은
  같은 볼륨 rename으로만 교체하고 기존 실행본은 timestamp와 이전 commit이 붙은 LKG 한 개로 남긴다.
  실행 중 player는 종료하지 않고 `AwaitingPlayerExit` candidate를 재사용한다. AppData 저장 데이터는 범위 밖이다.
- 격리 dry-run 15건은 공백·한글, dirty exit 31, conflict exit 32, unchanged skip, debounce, duplicate watcher
  exit 24, CMD exit code, 불완전 candidate, 승격 전·후 실패 복구, LKG, 실행 중 EXE 감지를 통과했다.
  `downloadsTouched=false`, `unityLaunched=false`이며 실제 최종 player build/Downloads 배포는 아직 수행하지 않았다.
- 후속 애니메이션·이동 hitch 커밋을 통합하고 최종 QA를 통과한 clean HEAD에서
  `START_WINDOWS_DEPLOY_WATCH.cmd` 또는 `DEPLOY_WINDOWS.cmd`를 명시적으로 실행한다.

## 2026-08-15 / P0 의자·이동·성능 1단계 통합 후보

- 기준 `9109a8c1`에서 전용 `codex/integration-p0-qa`를 만들고 의자 `0c6e8983`, 이동 `b692d24a`,
  성능 `9ea1312e`, 그 직계 문서 자식 `8ee72cd4`를 이 순서로 중복 없이 통합했다. 원본 브랜치와 `main`은
  변경하지 않았다.
- `OfficeRuntimeOccupancy` 충돌은 출근 ingress 상태, canonical 4×4 회전 ground mask, 미등록 콘텐츠의
  full-cell fallback, cached continuous-grid transform/AABB broad-phase, layout `Revision`을 모두 보존했다.
  actor 이동·reservation은 revision을 바꾸지 않는다.
- `OfficeRuntimeWorkstationService`에서는 의자 `VisualRoot`를 움직이는 옛 정렬 API를 되살리지 않고 명시적
  `OfficeSeatInteractionAnchors`만 유지했다. 자유 보행은 실제 변위 기반 8방향/`flipX=false`, 착석은
  LeavingSeat safe anchor까지 좌석 claim·facing·depth를 유지한다.
- Unity `6000.3.21f1` 격리 Editor QA는 chair foreground, seat egress 64건, seat occlusion 8방향,
  movement 128 seeds/1,152 paths/1,970 moving frames, path cache revision/invalidation, furniture collision
  10,368건/52 profiles, hybrid depth 120 permutations와 warmed 100회 allocation 0B를 모두 통과했다.
  채택 로그의 동시 루트 Unity는 각 1개, 다른 작업방 루트는 0개였고 종료 뒤 integration 소유 프로세스는 0개다.
- 후속 타이핑·마우스·물마시기 애니메이션 커밋은 아직 포함하지 않았다. 최종 Windows build와 D3D11 결합
  플레이 검증은 그 커밋들을 추가 통합한 뒤 수행한다.

## 2026-08-15 / 의자·좌석 상태 안정화 후보 (`codex/chair-seat-stability`)

- 착석은 의자 Transform을 쓰지 않는다. 가구 semantic root와 `VisualRoot`는 배치 때 기록한 parent,
  local/world position·rotation·scale을 유지하며 런타임 불변식 감시가 무단 변경을 검출·복구한다.
- 좌석은 approach, alignment, pelvis, hand, egress anchor를 명시적으로 제공하고 예약 claim을 통해 한 좌석의
  동시 점유를 거부한다. 진입은 Approach→Align→Rotate→Sit, 퇴장은 Finish→Stand→Leave로 분리한다.
- Windows D3D11 4명 동시 검증에서 furniture world position `0.000000px`, rotation `0.000000°`, scale
  `0.000000000`, character logical root `0.000000px`, pelvis-seat `0.000px`, seat-cell mismatch `0`, chair step
  `0.000px`, foreground penetration `0`을 기록했다. `(2,4)/(2,3)` 워크스테이션을 `(4,4)/(4,3)`으로 옮긴
  뒤 anchor와 occupancy 재구축도 통과했다.
- 기존 Northwest 타이핑 아트의 hand-keyboard 간격은 player `91.534px`, older_sister `78.188px`, father
  `79.401px`, mother `64.989px`로 3.5px 계약을 통과하지 못한다. 좌석 안정화와 분리된 필수 후속이며 현재
  전환 QA는 이를 `KNOWN_FAIL`/exit 97로 보존한다.
## 2026-08-15 / 실제 변위 방향·가구 geometry·출근 ingress

- 자유 보행의 최종 표현 방향은 실제 frame displacement의 8방향 양자화 결과다. 경계 인접 방향에만
  4°/0.075초 hysteresis를 허용하며, 좌우 cardinal 이동과 두 octant 이상 전환은 즉시 반영한다.
- `DirectionalSpriteAnimator`가 실제 변위·속도, motion/display direction, phase/clip, 최종 Sprite 이름과
  `flipX`를 같은 frame trace로 공개한다. 8방향 원화는 mirroring하지 않으며 최종 소비 단계에서 `flipX=false`를 강제한다.
- `OfficeRuntimeOccupancy`는 모든 알려진 편집 가구의 `OfficeFurnitureGeometryQuery.Shared` 4방향 ground
  mask를 path와 collision에 직접 사용한다. 정본 query에 없는 과거/미등록 저장 콘텐츠는 부분 legacy mask를
  재사용하지 않고 전체 셀 차단으로 fail-closed한다.
- 출근자는 첫 live route segment의 문 밖 2.5배 지점에서 나타나 단일 ingress gate를 예약한 뒤 입장한다.
  4인이 동시에 요청해도 한 명씩 진입하며, 기존 workstation seat claim/socket 소유권은 변경하지 않는다.
## 2026-08-15 / 플레이 중 내비게이션 정지 제거 (통합 대기 브랜치)

- `codex/perf-lag-rootcause`는 `OfficeRuntimeOccupancy.Revision`에 종속된 정적 이동 그래프를
  `(permittedSeatId, agentRadius)`별로 소유한다. 이 revision은 가구 배치·회전·회수처럼 정적 layout을
  다시 구성할 때만 바뀌며 actor 이동·reservation 같은 동적 점유는 캐시를 폐기하지 않는다.
- Starter Office의 빈 seat 권한과 가족 seat 4개, 169셀/키, 4방향 간선은 플레이 진입 전 Loading에서
  coroutine으로 전부 사전 계산한다. 진행률을 `ScenePreviewJump` Loading UI에 반영하고 매 4노드마다
  프레임을 양보한다. layout rebuild도 같은 준비가 끝나기 전에는 runtime ready를 열지 않는다.
- 동일 1배속 Development 시나리오에서 정적 reachability flood는 `90회/8,733 방문 노드`에서
  `0회/0 방문 노드`로, main-thread p99는 `184.631ms`에서 `22.468ms`로 줄었다. 최종 격리
  Release/D3D11 정상 구간 wall max는 1배속 `23.943ms`, 4배속 `36.965ms`이며, 두 배속 모두 플레이 중
  50ms 이상 프레임은 없다. 빌드→1배속→4배속을 직렬 실행했고 각 플레이 측정 중 루트 플레이어는
  최대 1개, Unity/임포트/빌드 background worker와 다른 작업방 루트는 0개였다.
- `OfficeHybridContinuousDepth`는 재사용 workspace를 사용하며 warmed 100회 정렬의 managed allocation은
  0B다. 의자 presentation 정렬은 수정하지 않았고 계측상 최대 `0.166ms`로 정지 원인이 아니다.
- 정적 캐시 invalidation, first/warm call, 이동·충돌·상호작용·depth, Windows Release build 검증을
  통과했다. 기존 `StaminaRuntimeIntegrationValidation` 전체 시나리오는 exact baseline에서도
  `Actor departed before the 25% threshold`로 실패한다. 현재 브랜치는 병목 제거 뒤 저장 단계까지
  진행하지만 다른 가족의 transient Performing 상태를 byte-for-byte 비교하는 기존 가정으로 실패한다.
  순수 stamina runtime-loss normalization과 GameState save 검증은 각각 통과한다.

## 2026-08-14 / 공용 업무 능력과 인사 roster

- 가족과 향후 채용 완료 직원은 기술개발·기획·창작·사업·운영·협업 6능력과 잠재력을 같은
  `WorkforceCapabilityState`로 사용한다. 별도 Speed는 없으며 업무별 10,000bp 프로필이 진행·품질·학습을 정한다.
- MainNavigationV2 인사 탭은 현재 고용된 가족 4명만 데이터 기반으로 표시한다. 잠재력은 숫자가 아니라
  S/A/B/C/D/F 등급만, 현재 스트레스와 스트레스 저항은 서로 다른 현재 상태로 표시한다.
- Save schema v10은 능력·분야별 XP·fixed-point remainder·스트레스 증가 배율을 저장한다. v1~v9의
  Speed/Stamina/Mental은 이관에서만 운영/스트레스 저항 초기값으로 읽으며 신규 권위 계산에서는 사용하지 않는다.
- 세부 수치와 XP/이관 경계는 `Docs/WORKFORCE_CAPABILITIES_V1.md`가 정본이다.

## 2026-08-14 / Windows Fast QA candidate

- `FAST_QA_WINDOWS.cmd`는 변경 분류, project-local lock, validation manifest, cache fingerprint, 순수 Simulation harness, Editor validation, scripts-only/normal Fast QA player build, D3D11 capture 재사용을 제공한다.
- 출력은 `Artifacts/FastQa`로 격리되며 release build/deploy 경로는 변경하지 않는다.
- 60초는 기능 PASS와 분리된 SLO다. cold import와 clean release는 별도 측정/최종 gate다.
- gameplay, seating, UI, stamina, stats, Save 구현 파일 변경은 0이다.

## 2026-08-14 / Shared stamina + placed-facility recovery

- All four family members start from one 10,000-unit profile. Integer GameTime drains 75% across a
  normal typing workday and creates recovery intent only at the 25% remaining threshold; profile
  overrides remain data-driven for later character differentiation.
- Recovery consumes the build editor's live capability query and existing runtime claim lifecycle.
  Only placed, reachable, available water/vending/lounge offers are selectable. Restroom remains
  fail closed because no definition/facility exists.
- A sticky stamina session gates routine autonomy refreshes while preserving attendance/mandatory
  and contract priority. Successful Performing completion releases the facility, returns to the
  exact assigned seat, and resumes the exact task/remaining minutes. Save schema v10 stores semantic
  stamina and workforce capability state and migrates earlier legacy state at the saved integer minute.
- Unity 6000.3.21f1 pure, 1x/2x/4x, save/load, prototype/micro-action, build capability, and PlayMode
  integration QA pass. `Artifacts/StaminaRuntimeQa/four-family-overhead-bars.png` captures all four
  bars and `summary.txt` records the completed runtime round trip.

## 기준선과 릴리스 판정

| 항목 | 현재 값 |
| --- | --- |
| 문서 정리 기준 코드 | clean local `main` HEAD |
| 원격 기준 | push 후 `origin/main == main`; 그 전에는 `git status --short --branch`로 차이를 확인 |
| Unity | `6000.3.21f1` |
| 시작 씬 | `Assets/FamilyCompany/Scenes/Prototype01.unity` |
| 최종 통합 main SHA | 이 문서를 포함한 clean `main` HEAD; 배포본의 `BUILD_INFO.txt`와 대조 |
| 최종 Windows build SHA | 최종 빌드의 `BUILD_INFO.txt`가 유일한 정본 |
| 최종 통합 QA | compiler, Unity static, D3D PlayMode PASS; Windows player는 배포본 `BUILD_INFO.txt`와 실행 로그로 판정 |

실제 배포 SHA는 실행본 옆 `BUILD_INFO.txt`와 `git rev-parse HEAD`가 같아야 한다. 과거 기능별 SHA는 역사 증거일 뿐 현재 릴리스 식별자로 사용하지 않는다.

## 현재 런타임 정본

| 영역 | 현재 동작 |
| --- | --- |
| 새 게임 | `2000-01-03 08:50`, 가족 4인, 자본금 5,000,000원 |
| 출퇴근 | 가족만 `09:00`~`09:03` 1분 간격, 문 밖 spawn과 단일 ingress 예약으로 입장, `18:00`부터 퇴근 |
| 직원 8인 | 시작 인원이 아닌 향후 채용 후보. 고용 전 런타임 출근 금지 |
| 사무실 | `StarterOfficeV1`, 13×13, 실내 가구 17 + 외곽 bay 52, 가족 workstation 4 |
| 외곽 출입구 | `(8,0)` threshold 1칸. `entrance_door`는 호환 ID이며 door leaf/jamb/lintel 애니메이션이 아님 |
| 메인 UI | `MainNavigationV2`: 회사·인사·사업·연구·투자 5개 허브 |
| 계약 | 고객 등급 `T0 → T1 → T2 → T3 → T4`, 순차 해금과 하락/회복 |
| 사무실 편집 | 배치·회전·이동·회수·재고·저장. 회사 허브에서 진입 |
| 저장 | 전체 `GameSaveDto v10`, `v1`~`v9` 읽기/이관; OfficeGrid 하위 스키마 `v4`, 가구 재고 하위 스키마 `v1` |
| 이동·애니메이션 | 실제 frame displacement 기반 8방향, 인접 경계 hysteresis, 거리 기반 걷기, canonical 가구 회피 |
| 렌더 | 1920×1080 reference, native scale 1, pixel snap, 180 PPU, 캐릭터 scale 1.55 |
| Windows 실행 | 저장소 상대 경로 `BUILD_WINDOWS.cmd` / `RUN_WINDOWS.cmd`; `BUILD_INFO.txt`로 SHA 확인 |

근거 구현은 `GameTime`, `PrototypeStateFactory`, `OfficeAttendanceRules`, `StarterOfficeRuntimeBootstrap`, `MainNavigationHudPresenter`, `ContractGrowthValidation`, `GameSaveDto`, `OfficeNavigationMotionRules`, `DirectionalSpriteAnimator`, `PixelClarityProfile`에 있다.

## 현재 통합됨 (clean `main` HEAD 기준)

- `MainNavigationV2`가 런타임에 연결되었고 거부된 V1 경로는 제거되었다. 회사 허브는 사무실 편집기, 사업은 계약/제품, 투자는 주식으로 연결된다.
- 계약 고객 성장은 day-one T0, T1~T4 순차 해금, 평판/실패 기반 하락과 T0 회복을 순수 시뮬레이션 규칙으로 처리한다.
- 사무실 편집기와 재고 저장은 v8에서 도입되었고, 현재 전체 저장 스키마 v10에 그대로 통합되어 있다. 별도 여섯 번째 하단 탭은 만들지 않는다.
- 플레이어, 가족 출퇴근, 계약 이동이 공유 office locomotion 규칙과 실제 변위를 사용한다.
- 배치 가구의 canonical 4방향 geometry가 occupancy/path의 정본이며 legacy/unknown 저장은 전체 셀 fallback한다.
- 출근 입구는 문 밖 비가시 spawn과 단일 ingress reservation으로 직렬화되어 같은 진입점 중첩을 막는다.
- native render/pixel snap/viewport clarity 기준과 캐릭터 scale 1.55가 적용되었다.
- 외곽 bay 52개, 단일 threshold, 가족 09:00~09:03/18:00 출퇴근 규칙이 적용되었다.
- 주식 코어의 시장 시간, 7+7 호가, FIFO, 수수료·세금, 저장 결정론과 투자 허브 진입점이 유지된다.
- 저장소 상대 경로 Windows 빌드/실행 스크립트와 `BUILD_INFO.txt` 생성 절차가 있다.
- 네 가족의 착석 방향 잠금, 키보드 손 접촉, 의자 하체 가림, 안전한 이석 경로가 통합되었다. 자유 보행만 실제 변위 방향을 사용하며 착석 중에는 좌석 방향 잠금이 최종 권한이다.
- 네 가족의 체력과 머리 위 바가 통합되었다. 25% 임계치 전에는 체력 때문에 일어나지 않고, 임계치 이후 실제 배치·접근·capacity가 유효한 회복 시설만 claim해 수행·해제·원래 업무 복귀한다.
- 가족과 향후 고용 직원은 기술개발·기획·창작·사업·운영·협업 6능력의 공용 모델을 사용한다. 인사 UI는 가족 4명의 실제 상태와 잠재력 문자 등급만 표시한다.
- UI Remaster V3는 프로젝트 내 Maplestory Light/Bold와 670자 glyph 검증을 공용으로 사용한다. Title→New Game, Loading, 5개 허브, People, 계약·건축·주식 경로는 같은 Windows D3D11 런타임에서 검증한다.
- `FAST_QA_WINDOWS.cmd`는 simulation-pure, editor-validation/broad, scripts-only cache, player-startup, D3D capture 프로필과 SLO 수치를 기록한다.

## 열린 기술 부채와 제품 backlog

1. 직원 후보 8인은 고용 시스템이 생긴 뒤에만 출근시킨다. 시작 roster나 09:00~09:03 가족 출근에 섞지 않는다.
2. 소파/다인 좌석은 group atomic claim, 짝 이동, 취소/퇴장 해제, non-NorthWest pose 승인과 idle/emote QA를 추가한다.
3. 오피스 확장은 현재 StarterOffice를 보존하며 단계별 면적/가구 해금으로 구현한다. 과거 요청서의 숫자를 검증 없이 새 정본으로 삼지 않는다.
4. 60일 외상 매출/지급, 경쟁 견적, 뉴스 조합은 `GAMEPLAY_FUN_V1.md`와 `DO_NOTS.md`의 재미·미래 누설 제한을 지키며 별도 설계/검증한다.
5. 주식은 전체 계좌/주문/체결/원장 persistence, S3/S4 시나리오, 외부 tape/호가 연결을 확장하되 기존 결정론을 보존한다.
6. Utility AI의 선택 규칙은 현재 `WeightedPick`이 정본이다. `ArgMax` 변경은 제안만으로 적용하지 않는다.

## 검증 상태

| 범위 | 기준 | 결과 |
| --- | --- | --- |
| Simulation/Editor 전체 회귀 | clean `main` HEAD | FastQA `simulation-pure`, `editor-validation`, `editor-broad` PASS |
| Workforce/Save v10 | clean `main` HEAD | skills=6, grades=S-F, v1~v9 migration, 1x/2x/4x PASS |
| UI V3/Maplestory | clean `main` HEAD | assets=24, characters=670, missingGlyphs=0; 1280×720·1392×768·1600×900/1000·1920×1080 PASS |
| Windows D3D11 UI | clean `main` HEAD | Title→New Game, Loading, 5 tabs, People, 계약·건축·주식, ESC, 1x/2x/4x PASS |
| Windows D3D11 사무실 | clean `main` HEAD | 가족4 출근·착석·타이핑·이석·이동·충돌·상호작용·스태미나·저장/불러오기 PASS |
| 최종 portable Windows build | 최종 `main` | clean HEAD에서만 생성하고 `BUILD_INFO.txt` SHA 일치, Release watermark 0, 배포 EXE smoke PASS로 판정 |

과거 개별 PASS는 해당 기능의 회귀 근거다. 최종 결합 SHA의 PASS를 대신하지 않는다.

## 최종 릴리스 체크리스트

1. `git diff --check`, C# compiler, 순수 검증 harness, Unity D3D PlayMode/render QA를 통과한다.
2. 저장 v1~v9→v10, 새 게임 v10, 편집 재고, 계약 성장, 주식 계좌, 출퇴근, 실제 변위 회귀를 확인한다.
3. `BUILD_WINDOWS.cmd`로 새 실행본을 만들고 `BUILD_INFO.txt`와 현재 HEAD가 같은지 확인한다.
4. [REGRESSION_BUILD_POLICY.md](REGRESSION_BUILD_POLICY.md)의 네 가족 09:00/09:01/09:02/09:03 oracle과 독립 gate를 통과시킨다.
5. 검증된 새 identity의 폴더 전체만 `C:\Users\godho\Downloads\Family\FamilyCompany_Playtest`에 배포한다. FAIL/UNKNOWN이면 evidence 보존 후 해당 payload를 삭제하고 rollback하거나 current를 비워 둔다.

## 다른 PC에서 이어하기

```powershell
git switch main
git status --short --branch
git pull --ff-only origin main
.\BUILD_WINDOWS.cmd
.\RUN_WINDOWS.cmd
```

빌드가 이미 있더라도 `Builds/Windows/FamilyCompany_Playtest/BUILD_INFO.txt`의 commit이 `git rev-parse HEAD`와 다르면 최신 실행본으로 간주하지 않는다. 상세 절차는 [HOME_PC_CONTINUATION_GUIDE.md](HOME_PC_CONTINUATION_GUIDE.md)와 [PLAYTEST_BUILD.md](PLAYTEST_BUILD.md)를 따른다.

회귀·실패·출처 불명·self-PASS-only 실행본의 처리와 재빌드 조건은 [REGRESSION_BUILD_POLICY.md](REGRESSION_BUILD_POLICY.md)를 반드시 따른다.

## 정본 문서 경계

- 인물·출퇴근·사무실 시각: [CANON.md](CANON.md), [ART_STYLE.md](ART_STYLE.md)
- 구조·저장·Unity 경계: [ARCHITECTURE.md](ARCHITECTURE.md)
- build/deploy 회귀 삭제: [REGRESSION_BUILD_POLICY.md](REGRESSION_BUILD_POLICY.md)
- 내비게이션·편집: [MAIN_NAVIGATION_HUD_V2.md](MAIN_NAVIGATION_HUD_V2.md), [OFFICE_BUILD_EDITOR_V1.md](OFFICE_BUILD_EDITOR_V1.md)
- 계약: [CONTRACTS_V0_3.md](CONTRACTS_V0_3.md), [CONTRACT_CLIENT_PROGRESSION_V1.md](CONTRACT_CLIENT_PROGRESSION_V1.md)
- 주식: [SIMUL_MARKET_PORT.md](SIMUL_MARKET_PORT.md), [STOCK_MARKET_LANDSCAPE_V1.md](STOCK_MARKET_LANDSCAPE_V1.md)
- 역사 구현 증거: `History/Reports/` — 현재 상태 정본 아님
