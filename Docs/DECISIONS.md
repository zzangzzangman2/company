# DECISIONS

## 2026-08-17 / 엄마 북쪽 보행은 승인 파일 수가 아니라 반대 접지 포즈로 판정한다

결정: 엄마 북쪽은 기존 `BeforeCoherenceV1` 복원본을 더 이상 충분한 정본으로 보지 않는다. 6프레임 존재·고유
hash·인접 실루엣 상한만으로는 상체와 치마가 멈춘 발 끌림을 잡지 못하므로 0/3 접지 포즈에서 상체·치마·발
실루엣이 각각 최소 `20%/20%/50%` 달라야 한다. 지지발 순서는 `R,R,L,L,L,R`이어야 한다.

결정: 생성 모델에 6칸 시간 순서를 맡기지 않는다. 북쪽 뒷모습의 반 주기 `contact→recoil→passing` 3장만
포즈 가이드로 확정하고 나머지는 픽셀 정확 좌우 반전한다. 생성 원본은 Unity import 밖 `ArtSources/`에 두며,
runtime 256px frame과 4×6 sheet는 `Tools/build_mother_north_walk_v2.py`로 재현한다.

이유: 6칸 일괄 생성 시 지지발이 매 프레임 임의 교대했고, 기존 엄마 0/3은 상체 5.6%·발 27.0% 변화에 그쳤다.
확정한 V2는 상체 30.1%·치마 29.2%·발 78.2%, 인접 median/worst 20.6%/26.1%이며 전용 회귀 5/5와
전체 walk 96/96를 함께 통과한다.

결정: 최종 아트 판정은 source PNG 비교에서 끝내지 않고 clean Release Player의 실제 정북 변위가
`mother_north_walk_0..5`를 모두 렌더한 closeup으로 닫는다. 이 검증에서 QA는 sprite/frame을 직접 지정하지
않고 기존 직접이동·충돌·거리 기반 gait를 사용한다. NPC 직접입력 허용은 `BeginQaControl` 뒤 command-line
opt-in 검사에서만 활성화하며 정상 일정·경로·좌석 observer 증거와 섞지 않는다.

## 2026-08-16 / 일반 새 게임 observer가 출근 계약의 최종 판정자다

결정: seating 전용 QA는 좌석·전환·가림의 국소 계약을 검증하지만 일반 새 게임 출근 회귀의 최종 증거로
사용하지 않는다. 출근 판정은 `actorQaControl=false`, route injection=false, clock jump=false,
docking force=false인 observer-only Windows Player가 08:50→09:50을 실제로 진행하며 actor별 phase,
destination/path, reservation/claim, occupancy, atomic seat, Work 6프레임을 기록해야 한다.

이유: 기존 seating QA의 `BeginQaControl`과 `QaBeginSeatedWork`는 정상 일정·경로·좌석 handoff를 의도적으로
강제해 09:07의 `player-work-controller-reset` 좌석 해제를 가렸다. 빠른 국소 QA 통과와 실제 새 게임 동작은
서로 다른 증거다.

## 2026-08-16 / 보행은 화면축 전신 방향·cardinal 타일 중앙·한 타일 한 주기다

결정: 가족 원화 방향은 실제 화면 투영과 일치하는 `(-world.x, world.y)` facing axes로 고른다. 오른쪽으로
이동할 때 West 전신, 왼쪽으로 이동할 때 East 전신이 보이는 현재 원화 계약을 presenter가 한 곳에서 소유하고,
animator·interaction facing·seat egress trace·gameplay QA가 모두 같은 변환을 사용한다.

결정: 생산 경로와 legacy pathfinder는 대각선·corner easing·중간점 생략 없이 cardinal cell center를 모두
순서대로 지난다. 기본 이동 속도는 `1.00 world unit/s`, 6프레임 2보 주기는 투영 타일 한 칸과 같은
`0.99380799 world unit`으로 둔다. 실제 작은 변위를 무시해 걷기 Sprite가 멈추는 문제를 막기 위한 visual
displacement 제곱 임계값은 `1e-10`이며 path budget·destination tolerance와 분리한다.

이유: 기존 방향 변환은 오른쪽으로 이동하는 아버지에게 North 전신을 선택했고, 대각선 경로와 corner easing은
위쪽 이동도 비스듬하게 보이게 했다. Venture Tycoon처럼 타일 중앙선을 기준으로 코너에서만 90도 전환하면
진행 방향을 예측할 수 있다. 기존 `1.65/0.78` 조합의 약 4.23 steps/s는 발이 떨리고 끌려 보였지만 새 계약은
한 타일에 두 걸음, 약 2.01 steps/s라 발 접지와 이동 거리가 맞는다.

## 2026-08-16 / 가족 보행은 승인된 전신 여섯 포즈와 scaled actor time을 보존한다

결정: 가족 4인의 runtime HighMotion 8개 시트는 `BeforeCoherenceV1`에 보존된 승인 원본으로 복원한다. 머리·몸통·
팔을 frame 0으로 고정하거나 다리를 두 포즈로 축소하지 않는다. 분할기는 8-connected 실루엣을 순수 NumPy로
찾아 256×256 frame의 하단 8px에 발 기준선을 맞추며, strict coherence gate는 RGB 변화량이 아니라 silhouette
인접 변화 `median<=30%`, `worst<=40%`, unique 6, foot drift<=1px, stable root drift<=4px, closure<=2px를 쓴다.

결정: gameplay clock과 actor motion은 모두 scaled time을 사용한다. actor 이동은 `Time.deltaTime`을 최대 0.08초
조각으로 소비하며, logical root·collision·occupancy는 매 조각 동기화한다. `unscaledDeltaTime`으로 몸만 1x에
묶는 것을 금지한다.

이유: 기존 안정화 산출물은 팔을 얼리고 하체를 사실상 두 포즈로 줄여 발 끌림을 만들었다. 승인 원본에는 이미
좌우 발과 반대 팔이 교차하는 여섯 전신 포즈가 있으므로 신규 생성보다 손실 없는 복원이 정확하다. 또한 clock만
2x/4x가 되고 actor가 1x면 출근 시간창을 실제 이동이 따라가지 못해 부모가 의자 앞에서 멈춘 것처럼 보인다.

## 2026-08-15 / Windows 자동 배포는 clean integration HEAD와 검증된 candidate만 승격한다

결정: 자동 watcher는 `codex/integration-p0-qa`의 committed HEAD가 배포 manifest와 다르고 debounce 동안
안정됐을 때만 기존 Release builder를 한 번 호출한다. untracked를 포함한 dirty 상태, merge conflict, 다른
branch는 빌드하지 않는다. Unity project version뿐 아니라 실제 editor binary ProductVersion/revision도
`6000.3.21f1_c02631ffc030`과 같아야 한다.

결정: Unity/build는 사용자별 machine-wide file lock으로 직렬화하고 이미 실행 중인 다른 작업방 Unity가
끝날 때까지 기다린다. candidate의 EXE, Data, UnityPlayer.dll, build/deploy manifest와 runner가 완전할 때만
Downloads target을 같은 볼륨 rename으로 승격한다. 기존 target은 이전 SHA와 UTC가 붙은 LKG 한 개로
보존하며 승격 실패 시 복구한다. target player가 실행 중이면 종료하지 않고 candidate를 유지해 watcher가
종료 뒤 재사용한다. watcher는 서비스나 시작 프로그램으로 등록하지 않는다.

이유: clean commit 단위의 재현성과 실제 실행본 SHA를 일치시키면서도 빌드 실패, 부분 staging, 동시 Unity,
플레이 중 파일 잠금이 현재 정상 실행본이나 AppData 저장 데이터를 손상시키지 않아야 한다. 디렉터리 교체 전
완전 검증과 old-build rollback은 copy-in-place보다 실패 경계가 작고 자동 테스트할 수 있다.

## 2026-08-15 / 착석은 가구를 움직이지 않고 명시적 좌석 계약에 캐릭터만 정렬한다

결정: chair semantic root와 `VisualRoot`의 parent, local/world position·rotation·scale은 가구 배치가
소유하며 착석·업무·기립 코드는 절대 쓰지 않는다. Rigidbody 2D/3D, Collider 2D/3D, Animator도 이 두
Transform의 소유자가 될 수 없다. occupied chair는 전체 사각 front crop 대신 기존 base와 좌판 하단 rim만
사용해 머리·몸·발을 가르는 직선 겹침을 막는다.

결정: 좌석 서비스는 approach, alignment, pelvis, hand, egress anchor와 배타적 reservation claim을 제공한다.
캐릭터 logical root, 점유 셀, 충돌 반경은 좌석 alignment 계약을 유지하고 pose의 연속 이동은 캐릭터 표현에만
적용한다. 상태는 Navigating→ApproachingSeat→AligningSeat→RotatingToSeat→SittingDown→Working→
FinishingWork→StandingUp→LeavingSeat로 분리하며 release는 safe egress 도착 뒤 수행한다.

이유: 기존 `OfficeRuntimeAgent.AdvanceChairPresentation`→`OfficeRuntimeWorkstationService.
AlignChairPresentationToOccupant`→`OfficeGridFurniturePresenter.AlignSeatPresentationToWorld` 경로가 매 프레임
의자 `VisualRoot.position`을 캐릭터 골반으로 당기고 기립 뒤 되돌려 의자 비행을 직접 만들었다. 의자를
불변으로 바꾼 D3D11 4명 동시 검증은 가구 Transform 전 항목과 logical root·pelvis-seat를 실질 0으로 유지했다.
손–키보드 64.989~91.534px는 기존 타이핑 아트의 별도 `KNOWN_FAIL`이며 좌석 안정화 PASS로 완화하지 않는다.
## 2026-08-15 / 횡이동은 같은 frame의 실제 변위와 8방향 Sprite가 함께 증명한다

결정: 실제 root displacement가 있는 frame은 그 heading만 motion facing의 권한으로 사용한다. 4°/0.075초
hysteresis는 최근접 방향과 현재 방향이 인접한 45° 경계에서만 허용하고, 오차가 30.5°를 넘거나 두 octant
이상 바뀌거나 좌우 cardinal로 전환하면 즉시 적용한다. 이는 2026-08-14의 "모든 실제 이동 frame 즉시
전환" 결정을 경계 인접 안정화에 한해 대체하며, 실제 횡이동 중 South/North sprite 유지는 허용하지 않는다.

결정: 최종 `SpriteRenderer` 소비 단계는 actual displacement/speed, resolved motion/display direction,
phase/clip, Sprite asset name, `flipX`를 같은 frame trace로 제공한다. 모든 방향이 독립 원화이므로
`flipX=false`를 매 frame 강제한다. 보행 위상은 실제 이동거리와 속도만 소비하고 정지 시 마지막 자연스러운
방향을 유지한다. legacy mover도 요청 속도가 아니라 controller/transform의 실제 frame displacement를 전달한다.

결정: `OfficeRuntimeOccupancy`는 알려진 가구에 `OfficeFurnitureGeometryQuery.Shared`의 회전된 4×4
subcell ground mask를 사용한다. 이전 저장에서 정본 geometry를 찾지 못하면 부분 legacy profile을
재사용하지 않고 전체 셀 차단으로 fail-closed한다. 출근 입장은 live path 첫 구간을 문 밖으로 연장한 단일
ingress reservation으로 직렬화하며 기존 좌석 claim·Transform·socket 소유권을 변경하지 않는다.

결정: contact refinement와 축 slide를 합친 최종 displacement는 segment query뿐 아니라 같은 endpoint의
zero-length collision query도 통과해야 한다. 두 판정 중 하나가 실패하면 합성 displacement 전체를 다시
보수적으로 refine한다. 4×4 mask 경계의 보간 반올림을 여유 공간으로 간주하거나 QA tolerance로 숨기지 않는다.

이유: 방향 계산과 최종 Sprite 선택이 다른 frame/state를 읽으면 계산 테스트가 통과해도 정면 몸으로
횡이동할 수 있다. 또한 편집 geometry와 runtime collision의 이중 정본, 화면 안 spawn, 공동 입구 무예약은
가구 관통·NPC 중첩을 만든다. 같은 frame trace, exact endpoint 검증, fail-closed 이관, 단일 ingress gate가
각각 이 경계를 관측 가능하고 결정론적으로 만든다.
## 2026-08-15 / 정적 이동 그래프는 layout revision별로 Loading에서 완전 사전 계산한다

결정: `OfficeRuntimePathService`는 정적 통과 가능성을 `OfficeRuntimeOccupancy.Revision`, 허용 좌석 ID,
agent radius로 분리한다. revision은 의미 layout/furniture occupancy를 `Rebuild`할 때만 바뀐다. actor 위치,
동적 충돌 회피, interaction reservation은 정적 그래프 키나 invalidation 원인이 아니며 경로 탐색 시 별도 검사한다.

결정: Starter Office의 현재 runtime key 전체는 플레이 화면 진입 전에 neighbor graph와 connected component를
완전 계산한다. Loading UI는 진행률을 갱신하고 4노드 단위로 프레임을 양보한다. 가구 편집으로 revision이
바뀌면 runtime을 준비 상태로 닫고 같은 prewarm을 다시 끝낸 뒤 연다. 최초 출근·착석·업무·상호작용에서
lazy flood-fill을 허용하지 않는다.

결정: 후보 접근점 검사는 같은 prewarmed graph의 neighbor를 재사용한다. 경로 BFS는 재사용 queue/set/parent를,
연속 depth 정렬은 재사용 workspace를 사용한다. `OfficeGridTilemapPresenter.NearestCell`은 전체 격자 탐색 대신
각 x열의 정확한 투영 후보만 비교하며 dense oracle 검증으로 기존 결과와 같음을 보장한다.

이유: 09:00과 반복 스케줄에서 capability/offer query가 같은 13×13 layout을 90번 동기 flood-fill하고
8,733노드를 다시 방문해 4.7초 main-thread 정지를 만들었다. 분산 lazy 안은 첫 상호작용으로 작업을 옮겨
최대 2.4초 프레임을 남겼다. 완전 prewarm과 명시적 revision 경계는 Loading 2.27초 이하를 쓰는 대신
격리 Release 플레이 정상 구간을 1배속 23.943ms, 4배속 36.965ms wall max로 제한하며 stale graph도 막는다.

## 2026-08-14 / UI Remaster V3와 MapleStory typography를 전체 화면의 공용 정본으로 사용

결정: Title, New Game/Load, Loading, HUD, 회사·인사·사업·연구·투자, People 상세는 프로젝트에 포함된
Maplestory Light/Bold와 공용 V3 크기·weight·layout token을 사용한다. 글자는 런타임에서 렌더하며 이미지에
굽지 않는다. 670자 한국어·영문·숫자 glyph와 1280×720, 1392×768, 1600×900/1000, 1920×1080의
clipping·overflow·icon collision을 정적 검사와 Windows D3D11 캡처로 함께 판정한다.

이유: 화면별 자체 폰트·크기와 단순 파일 생성형 캡처는 실제 검정 프레임, 해상도 강제, baseline 어긋남을
놓칠 수 있다. 공용 typography 계약과 비활성 오프스크린 GPU readback을 함께 사용해야 회사 PC에서 창을
노출하지 않으면서도 실제 픽셀 방향·해상도·내용을 검증할 수 있다.

## 2026-08-14 / 공용 업무 능력은 6종이며 잠재력은 문자 등급만 공개

결정: 가족과 채용 완료 직원은 기술개발·기획·창작·사업·운영·협업 6종의 0~100 능력과 내부
잠재력 0~100을 같은 구조로 사용한다. 별도 Speed는 제거하고 업무별 10,000bp 가중 점수가 속도와
품질을 정한다. 잠재력 UI는 S 90~100, A 80~89, B 65~79, C 50~64, D 35~49, F 0~34의
문자만 표시한다. 색이나 정확한 숫자만으로 잠재력을 표시하지 않는다.

결정: Save v10은 분야별 XP와 fixed-point remainder를 저장한다. 업무 점수는 1인시에 필요한 정수
GameTime 분으로 환산하며 그 시간이 실제로 지난 뒤에만 계약 기여와 XP가 생긴다. UI·이동·E키를 누른
실시간·프레임 시간은 입력이 아니다. legacy Speed와 Stamina는 v1~v9의 operations 초기값,
Mental은 스트레스 저항 초기값을 만드는 일회성 이관에서만 읽는다. 스트레스 저항은 스트레스 증가량만
보정하며 계약 속도·품질을 직접 높이지 않는다.

이유: 하나의 만능 속도나 멘탈 품질 보너스는 역할 선택을 약하게 만들고 체력/감정 상태와 영속 숙련을
혼동한다. 업무 프로필과 상태를 분리하면 가족과 미래 직원이 같은 규칙으로 성장하면서 배속·저장에도
동일한 결과를 유지한다.

## 2026-08-14 / Warm incremental QA와 release build를 분리한다

- 영구 detached QA worktree의 `Library/Bee`와 별도 Fast QA player cache를 반복 검증에 재사용한다.
- 순수 Simulation은 Unity Editor를 시작하지 않고 설치된 Unity의 Roslyn/Mono로 검증하며, Editor gate는 manifest로 선택한다.
- `BuildOptions.BuildScriptsOnly`는 호환되는 normal Fast QA player와 동일한 serialization-layout signature가 있을 때만 허용한다.
- cold import, clean build, 최종 배포는 60초 SLO 통계에서 분리한다.
- 측정 전 asmdef 분할, 매번 Library 삭제, 모든 Unity 프로세스 전역 대기/종료, asset 변경에 scripts-only 강제는 채택하지 않는다.

## 2026-08-14 / 메인 내비게이션은 V2 스킨과 실제 adapter route로 확정

결정: 사무실 상단은 회사명·날짜/시간·1x/2x/4x만, 하단은 회사·인사·사업·연구·투자 5개만 둔다. ImageGen은 cream/coral/teal 9-slice surface와 아이콘만 제공하고 회사명, 날짜, 숫자, 상태, 버튼 글자는 TMP/uGUI가 별도 렌더한다. 검은 상·하단 바, 두꺼운 문서형 테두리, 회색 카드와 V1 자산은 사용하지 않는다.

이유: 이미지에 정보를 구우면 회사명·시간·상태 변화와 다국어가 깨지고, 거절된 V1 문서 허브는 실제 경영게임 화면보다 구식 관리 도구처럼 보였다. 장식과 상태를 분리한 V2는 네 목표 해상도에서 같은 hierarchy와 입력 의미를 유지한다.

결정: 회사의 건축·편집은 `OfficeBuildEditorNavigationAdapter`, 사업의 하청 계약·자체 제품은 `ContractBusinessRuntimeAdapter`, 투자의 주식시장은 기존 `StockMarketFullscreenPanel` 정본만 호출한다. 나머지 카드도 막힌 placeholder로 두지 않고 전용 화면에 들어가 명시적 `준비 중` 상태를 보여 준다.

이유: public adapter를 경계로 사용하면 UI가 건축·계약·시장 상태를 복제하지 않으며, 사용자는 모든 카드에서 클릭 결과와 일관된 ESC/back 스택을 확인할 수 있다.

## 2026-08-14 / 체력은 GameTime 정수 상태와 실제 시설 claim으로 확정

결정: 모든 캐릭터는 공용 기본 profile(10,000/10,000, 회복 threshold 25%, resume floor 35%)로
시작하고 ID별 profile override만 허용한다. Typing은 GameTime 분당 16 unit을 소모해 정상 근무일에
약 75%가 소모된다. Save v9은 이 정수 상태를 별도 `staminaState`로 저장하며 v1-v8은 저장 시점의
legacy energy를 이관한다.

결정: 회복은 build editor의 배치·도달·capacity query가 제공하는 WaterSource, DrinkVending,
RestSeat만 기존 interaction claim으로 실행한다. 화장실은 시설 definition이 생길 때까지 fail closed다.
활성 회복 session은 일반 autonomy refresh만 보류하고 출근/필수 일정과 계약 우선순위는 유지한다.
Performing 성공과 claim release 뒤에만 회복을 반영하고, 정확한 지정 좌석과 남은 업무로 복귀한다.

이유: 순수 상태와 transient scene claim을 분리하면 시간 배속·save/load가 결과를 바꾸지 않으며,
없는 시설 순간이동과 refresh마다 이동을 abort/reclaim하는 보상 루프를 함께 막을 수 있다.

## 2026-08-14 / 자판기 4방향은 additive Resources 정본으로 확정

결정: 실패한 투명/체커 배경 ImageGen 결과는 자산으로 채택하지 않는다. 같은 2000년형 크림·민트 자판기를 SE/SW 조작면과 NW/NE 후면의 실제 4회전으로 다시 만들고, 공식 `remove_chroma_key.py --auto-key border --soft-matte --transparent-threshold 18 --opaque-threshold 210 --despill --edge-contract 1`만 사용해 alpha source를 만든다. `OfficeBuildVendingArtBuilder`는 이를 640×512 hard-alpha, 180 PPU, Point, mipmap 없음, ground pivot `(320,28)`의 방향별 Resources Sprite로 결정론적으로 승격한다.

이유: 생성형 이미지의 불투명 체크무늬나 임의 alpha 추정은 테두리 오염과 흔들리는 충돌 기준을 만든다. 반면 source/chroma/runtime 단계를 분리하고 정확한 방향 Resource ID를 사용하면 공유 furniture catalog를 바꾸지 않으면서도 회전 결과, GUID, pivot, 반복 빌드 SHA를 자동 검증할 수 있다.

## 2026-08-14 / 건축·편집은 의미 layout + 소유 inventory + 복식부기 transaction으로 확정

결정: 사무실 배치는 계속 `GameState.OfficeGrid`가 소유하고, 구매·보관·구매 basis는 새 `OfficeFurnitureInventoryState`가 소유한다. 모든 확정 명령은 먼저 immutable 후보 layout/inventory를 검증한 뒤 ledger를 한 번만 전기하고 두 상태를 함께 교체한다. Save schema v8은 inventory를 저장하며 v1-v7은 기존 grid 가구를 `LegacyIncluded`로 이관한다.

이유: scene Transform이나 편집기 전용 화폐를 저장하면 렌더·충돌·소유권·회계가 서로 다른 사실이 된다. 의미 상태와 idempotent transaction ID를 분리하면 preview 취소와 실패가 0원 변경이고, 같은 가구 종류의 여러 instance와 판매 basis도 저장 후 동일하게 복원된다.

결정: 가격은 2000년 한국 KRW 기준 reference price를 definition에 보존하고 모든 품목에 하나의 명시적 25% gameplay scale만 적용한다. 구매는 office furniture asset, 판매는 원가 제거와 처분손실(legacy included는 별도 sale income)로 기록한다.

이유: 시작 자금 500만원 안에서 배치 실험이 가능해야 하지만 품목별 숨은 보정은 경제 규칙을 설명하거나 검증할 수 없다. 기준 가격과 gameplay 조정을 분리하면 현실성 근거와 게임 밸런스를 독립적으로 바꿀 수 있다.

결정: 회사 UI는 새 하단 탭을 만들지 않고 `company.hub.build_editor` adapter를 호출한다. 가구 geometry/capability는 read-only query로 제공하며 movement·좌석·stamina·interaction lifecycle의 소유권은 기존 시스템에 남긴다.

이유: 병렬 UI와 movement 작업의 파일을 직접 수정하지 않고도 안정적인 통합 경계를 제공해야 한다. 같은 query가 tile footprint, ground mask, access/egress를 공유하면 후속 소비자가 instance ID나 Sprite alpha를 충돌 정본으로 하드코딩하지 않는다.

## 2026-08-14 / Starter Office 이동 방향은 실제 변위만 정본으로 사용

결정: `OfficeSharedLocomotionRules`를 player, NPC, autonomy, contract route가 공유하는 순수 C# 운동·표현 경계로 사용한다. requested direction, actual displacement/speed, display facing, gait phase를 분리하고 수치 오차보다 큰 실제 root 변위가 있는 모든 프레임의 display facing은 실제 변위의 최근접 8방향으로 즉시 결정한다. 4° hysteresis, 0.075초 방향 안정화, 충돌 slide의 0.15초 semantic-facing hold는 폐기한다.

결정: 135° 이상 급반전은 기존 속도를 0 근처까지 감속한 뒤 제자리 pivot을 끝내고 새 방향으로 가속한다. 정지 상태의 입력과 상호작용 목표 방향은 실제 변위 없이 인접 45° 방향을 거쳐 회전할 수 있으며 pivot 중 walk cycle은 진행하지 않는다. 도착 상호작용은 actual stop, Idle, desired facing 완료 전에는 Performing으로 진입하지 않는다. 착석 facing lock과 phase depth는 별도 착석 작업이 소유한다.

결정: 보행 위상은 모든 현재·향후 캐릭터가 공유하는 `OfficeLocomotionGaitRules.DefaultStrideLength`와 누적 실제 이동거리로만 계산한다. member ID별 보폭, 회전시간, 방향 허용치와 Animator별 stride override를 금지한다.

이유: semantic heading이나 presentation timer가 actual motion을 덮으면 벽 slide, 코너, 급반전에서 정면을 보며 옆·뒤로 미끄러지는 프레임이 생긴다. 실제 변위와 화면 방향을 같은 사실로 만들고 정지 회전과 거리 기반 발 위상을 공용 규칙으로 분리해야 가족 4명과 향후 직원 8명이 동일한 결정론·접지 품질을 유지한다.

## 2026-08-14 / 착석 가림은 단일 3-layer 규칙으로 소유한다

결정: `OfficeSeatedUpperBodyProtectionRules`를 점유 의자의 유일한 합성 규칙 소유자로 사용한다. 이 규칙은 (1) 기존 의자 전체 foreground, (2) 좌석 테두리만 포함하는 1,816 opaque-pixel 하체 crop, (3) pose pelvis에서 12px 아래를 경계로 만든 상체 redraw를 함께 정의한다. 별도 `OfficeOccupiedChairForegroundRules` 클래스는 만들지 않으며 validator와 runtime은 모두 같은 정본을 참조한다.

이유: 전체 foreground만 사용하면 의자가 엄마의 상체까지 가리고, 상체 redraw만 남기고 하체 crop을 제거하면 실제 Windows D3D11 아빠 Typing 6프레임에서 하체와 의자 foreground의 겹침이 0이 되어 앉은 깊이가 사라졌다. 세 plane은 서로 대체하는 구현이 아니라 `chair base < actor < lower seat rim < upper-body redraw`를 만드는 한 알고리즘의 구성요소다. 정본을 한 클래스로 모으면 과거 validator `CS0103`처럼 경쟁 타입 중 하나가 누락되는 실패도 제거된다.

## 2026-08-14 / 착석 종료는 safe-anchor 도달 뒤 원자적으로 해제한다

결정: 일어서기 전에 front/left/right 순서로 충돌 없는 egress cell과 구간을 예약한다. `LeavingSeat` 동안 facing/depth/foreground/seat ownership을 유지하고, actor가 safe anchor에 도착했을 때만 seat claim, chair foreground, egress reservation을 한 lifecycle 경계에서 해제한다. rear 후보는 사용하지 않는다.

이유: 애니메이션 진행률만으로 chair depth를 먼저 해제하면 의자·책상 사이를 빠져나오는 동안 상체가 다시 잘리거나 가구 안으로 들어갈 수 있다. 반대로 예약을 먼저 확보하고 safe anchor를 release 조건으로 사용하면 다양한 좌석 방향에서도 기립 전 경로 안정성, 하체 가림, 충돌 무결성을 같은 조건으로 증명할 수 있다.

## 2026-08-14 / 공개 착석 오차 경계보다 생성 예산을 0.001px 작게 둔다

결정: QA 계약은 seat/egress `<=0.9px`, typing hand-to-keyboard `<=3.5px`를 그대로 사용한다. 런타임이 생성하는 최대 step/contact 예산만 각각 `0.899px`, `3.499px`로 둔다.

이유: 카메라 world-to-screen 투영의 부동소수점 반올림으로 정확히 `0.900px`를 목표한 값이 내부적으로 경계를 아주 조금 넘을 수 있다. validator의 허용치를 늘리는 대신 생성값에 0.001px의 결정론적 여유를 두면 사용자 계약을 완화하지 않고 모든 프레임에서 같은 엄격한 비교를 유지할 수 있다.

## 2026-08-13 / compact 타이틀은 세로 확장 배경으로 레터박스를 제거

결정: 1.35 미만 화면에서는 16:9 V2를 scale-to-fit하지 않고, V2의 위아래를 확장한 10:11 세로 V3를 aspect-fill한다. 가로 화면은 기존 V2를 그대로 사용하며 메뉴·돈다발 애니메이션 로직은 공유한다.

이유: 440×481에서 원본 전체를 보존하는 scale-to-fit은 화면 절반 가까이를 검은 띠로 만들었다. 기존 16:9를 단순 확대하면 가족과 CRT가 잘리므로, 중앙 사무실 정본을 유지한 별도 세로 배경이 화면 활용과 캐릭터 보존을 동시에 만족한다.

## 2026-08-13 / 타이틀 메뉴는 왼쪽 세로형 웜 팔레트로 고정

결정: 하단 메뉴는 사용하지 않는다. compact와 가로 화면 모두 제목 아래 왼쪽에 작은 5행 세로 메뉴를 배치하며, 440×481에서 행 높이는 33px로 제한한다. UI 팔레트는 코랄·크림·차콜·웜그레이로 고정하고 민트·청록 버튼과 하단 삼색 리본을 제거한다.

이유: 하단 배치는 오피스 장면과 메뉴를 위아래로 분리해 모바일 런처처럼 보였고, 기존 녹색 카드는 프로젝트 초기 UI의 구식 인상을 계속 남겼다. 원화가 이미 왼쪽 저복잡도 영역과 오른쪽 사무실을 갖고 있으므로 왼쪽 메뉴가 배경 구성을 가장 자연스럽게 활용하며 가족 장면을 가리지 않는다.

## 2026-08-13 / 타이틀 메뉴 장식은 주요 행동 두 개에만 사용

결정: 메인 화면에서 배경을 가리는 외곽 메뉴 패널과 번호·영문 배지·버튼별 설명을 제거한다. `새 회사`와 `이어하기`만 낮은 둥근 버튼으로 강조하고, `불러오기/화면 설정/종료`는 배경 없는 텍스트 메뉴로 표시한다. 이 원칙은 compact와 가로 화면에 동일하게 적용한다.

이유: 모든 항목을 큰 카드로 만들면 중요도 차이가 사라지고 작은 창에서는 설정 목록처럼 보인다. 첫 진입 행동 두 개만 클릭 영역으로 강조하고 나머지를 가벼운 텍스트로 두면 사무실 키아트를 더 많이 보여 주면서 메뉴 크기와 시각적 소음을 함께 줄일 수 있다.

## 2026-08-13 / 세로형 타이틀은 오피스 히어로 + 타일 메뉴로 분리

결정: 화면 가로세로비가 1.35 미만이면 16:9 타이틀 배경을 전체 화면에 aspect-fill하지 않는다. 원본 전체를 상단 16:9 히어로에 `ScaleToFit`으로 표시하고, 하단 메뉴는 `새 회사/이어하기` 2개 큰 타일과 `불러오기/화면/종료` 3개 작은 타일로 렌더한다. 돈다발 12개와 모든 저장·화면·종료 동작은 가로형과 동일하게 유지한다.

이유: 실제 440×481 실행 창에서는 16:9 배경의 좌우가 크게 잘려 가족과 사무실이 오른쪽 일부만 보였고, 5개의 긴 카드는 화면 대부분을 차지해 게임 타이틀보다 설정 목록처럼 보였다. 배경과 조작 영역을 위아래로 분리하면 원화의 가족 4인과 컴퓨터 방향을 보존하면서도 작은 창에서 현대적인 게임 런처형 위계를 유지할 수 있다.

## 2026-08-13 / 메인 타이틀을 등각 도트 경영게임 UI V2로 교체

결정: 기존 Money Rain의 투명 돈다발 3종·12개 인스턴스·2.8초 루프는 유지하되, 활성 배경을 가족 4인이 일하는 2000년 등각 도트 사무실 `money_rain_tycoon_background_v2.png`로 교체한다. 왼쪽 42%는 Unity 제목·메뉴용 저복잡도 짙은 청록 영역, 오른쪽은 주인공·누나·아빠·엄마의 역할이 읽히는 실제 사무실 장면으로 고정한다. 누나는 맨발 정본을 유지하고, 책상에 앉은 주인공과 엄마의 CRT 화면·키보드는 반드시 각 사람을 향한다.

결정: 메인 조작 UI는 이미지에 굽지 않는다. Unity IMGUI가 민트 기본 CTA, 짙은 청록 보조 카드, 코랄 종료 카드, 번호·제목·설명의 3단 위계, 최근 저장 비활성 상태, 시작 상태 스트립과 단축키를 해상도에 맞춰 별도 렌더한다. 둥근 9-slice 텍스처는 런타임 생성하고 저장 상태나 시뮬레이션에는 넣지 않는다.

이유: 밝은 빈 사무실 위에 동일한 민트 사각형을 반복한 화면은 기능은 읽히지만 업무용 템플릿처럼 보였고, 실제 런타임 등각 도트 사무실 및 가족 경영 정체성과 연결되지 않았다. 가족의 실제 역할 장면과 강한 카드 위계를 첫 화면에서 함께 보여 주면 경영게임의 활기와 최신 PC 게임의 가독성을 확보하면서 기존 돈 날림 연출과 저장 동선을 보존할 수 있다.

## 2026-08-13 / Micro Action 목적지는 실제 배치 가구별 Offer로 해석한다

결정: `OfficeInteractionDefinition`을 `OfficeGrid.Furniture`에 투영해 실제 `FurnitureId`마다 Offer를 만들고, 기존 Occupancy/PathService로 열린 접근 칸과 도달 가능성을 검사한다. Offer는 레이아웃에서 매번 다시 만들고 Occupancy revision 변경 시 동일 intent도 재해석한다. Simulation에는 Unity 참조를 넣지 않으며 passability와 reachability를 delegate 경계로 주입한다.

이유: 위치→가구 kind switch와 모든 동종 가구의 접근 칸을 한 목록으로 합치면 가구 삭제·이동·복수 배치에서 대상 소유권과 capacity를 구분할 수 없다. 실제 인스턴스 ID와 접근 칸을 묶은 Offer가 있어야 없는 가구, 막힌 가구, 도달 불가 가구를 선택 전에 제거하고 이후 예약 lifecycle을 인스턴스별로 확장할 수 있다.

결정: 당시 단계에서는 기존 `WeightedPick`을 유지하고 Micro Action 변경만으로 전체 저장 schema를 올리지 않았다. 당시 전체 schema는 v7이었고 office build editor/재고는 v8, semantic stamina는 v9에서 도입되었으며, 현재는 업무 능력을 포함한 v10이다. 물리 회의 테이블은 작성 좌석이 없으므로 직접 플레이어/계약 목적지 예외를 유지하고, NPC Micro Action 회의는 기존 assigned-PC 좌석 계약을 Offer로 해석한다.

이유: 가구 Offer 연결, Utility selector 활성화, 예약·중단 cleanup lifecycle은 서로 다른 회귀 위험을 가진다. 레이아웃 가용성 계층을 먼저 검증해야 행동 분포 변화와 저장 변경을 분리해서 판단할 수 있다.

## 2026-08-10 / Flutter 투자 게임을 Unity 가족회사 게임으로 전환

결정: 기존 Flutter 앱을 직접 덮어쓰지 않고 C:/Users/godho/Documents/Codex/family_company_unity에 새 Unity 프로젝트를 만든다.

이유: 원본의 시장·시간·결정론·저장 설계는 참고하면서, 공간 이동과 가족 관계를 중심으로 게임의 재미 축을 재설계하기 위해서다. 원본 저장소는 이관이 끝날 때까지 읽기 전용 참고로 남긴다.

## 2026-08-10 / 가족 구성을 4명으로 시작

결정: 플레이어 14살, 누나 20살, 아빠 46살, 엄마 44살로 Prototype 0.1을 시작한다. 부모 나이는 최종 서사 확정 전까지 임시 확정이다.

이유: 누나 20살이라는 사용자 정본에 맞추고, 미성년 플레이어의 법률·은행 한계를 가족 협업 게임플레이로 바꾸기 위해서다.

## 2026-08-10 / 누나 의상 정본 교체

결정: 첫 사무실 스웨터·치마 버전은 폐기 후보로 분류하고, 나시티·돌핀팬츠·맨발 버전을 정본으로 사용한다.

이유: 사용자의 직접 수정 요청을 반영했다. 얼굴, 긴 검은 양갈래, 검은 리본, 청록색 눈의 정체성은 경마장 표 판매원 에셋에서 유지한다.

## 2026-08-10 / 결정론적 순수 C# 코어

결정: 시간, RNG, 이벤트, 가족, 회사, 회계를 UnityEngine과 분리된 순수 C# 계층에 둔다. 장기 런타임 상태는 MonoBehaviour나 ScriptableObject가 소유하지 않는다.

이유: 헤드리스 검증, 저장 안정성, 향후 Unity 버전/프레젠테이션 교체에 대한 내구성을 확보하기 위해서다.

## 2026-08-10 / 사무실 아트는 2.5D 도트 방식

결정: 순수 2D 등각 타일맵으로 전환하지 않고, 3D 모듈 사무실·CharacterController 이동·직교 카메라 위에 포인트 필터 도트 렌더와 2D 캐릭터 스프라이트를 결합한다.

이유: 고품질 등각 도트는 가구와 캐릭터의 방향별 제작량이 많다. 2.5D 방식은 사무실 배치 변경, 실제 충돌 이동, 카메라, 가구 상호작용을 빠르게 반복하면서 사용자가 원하는 귀여운 도트 화면을 유지할 수 있다.

## 2026-08-10 / 모든 회사 캐릭터는 실제 위치를 가진다

결정: 배경에 그려진 가짜 직원이나 단순 UI 아이콘만으로 회사 활동을 표현하지 않는다. 플레이어와 NPC는 실제 Transform을 가지며, NPC는 업무·출력·회의·휴식 지점 사이를 이동하고 도착 상태를 가진다.

이유: 사용자가 실제 이동을 핵심 요구로 다시 확인했다. 향후 일정, 업무 배분, 피로, 관계 시스템이 공간 행동과 직접 연결되어야 한다.

## 2026-08-10 / 2000년 3차 웹·전산 하청회사로 시작

결정: 가족회사는 지역 업체의 홈페이지·사내 도구·데이터 QA·전산 유지보수를 맡는 소형 3차 하청회사로 시작한다. 2차 하청, 1차 공급사, 자체 제품, 상장, 인수합병 순으로 성장한다.

이유: 자본과 경력이 없는 14살 플레이어와 가족회사의 출발을 설득력 있게 만들고, 실제 사무실의 업무 배정이 장기적인 시장 경쟁으로 이어지는 성장감을 만들기 위해서다.

## 2026-08-10 / 실제 역사 기준선과 대체 역사 상태를 분리

결정: 검증된 HistoricalBaseline, 회차별 WorldState, 차이를 기록하는 DivergenceLog를 분리한다. 역사 사건은 조건을 가진 후보이며 플레이어 개입 후에는 취소·지연·대체·이전될 수 있다.

이유: 실제 2000~2026년의 중요 역사를 제공하면서도 플레이어가 인수와 경쟁으로 원인을 바꿨을 때 결과가 그대로 강제되는 모순을 막기 위해서다.

## 2026-08-10 / 실제 회사 이름은 데이터 기반으로 교체 가능하게 사용

결정: 개발과 역사 검증 중에는 실제 회사명을 명시해 혼동을 줄인다. 저장과 코드 참조는 불변 companyId를 사용하고, 공개판 이름은 releaseNameMap 한 곳에서 바꿀 수 있게 한다.

이유: 실제 회사 관계를 정확히 연구하면서도 향후 명칭 변경과 법률 검토가 게임 로직이나 기존 저장을 깨뜨리지 않게 하기 위해서다.

## 2026-08-10 / Claude와 Codex 작업 흐름 분리

결정: Claude는 실제 회사 역사 데이터·출처·validator 전용 경로를 맡고, Codex는 Unity 런타임과 `simul` 시장 이식을 맡는다. 병행 중에는 공용 정본 문서를 동시에 고치지 않는다.

이유: 토큰과 작업량을 나누면서도 같은 파일 수정과 서로 다른 가정으로 생기는 병합 충돌을 방지하기 위해서다.

## 2026-08-10 / 국내 실제 회사 우선과 실제 이름 표시

결정: 첫 역사 데이터는 국내 실제 회사 60개 이상을 넓게 등록하고 2000~2003년 국내 회사부터 상세화한다. 현재 개발판 UI에는 가명이 아니라 해당 날짜의 실제 법인명·실제 통용명을 표시한다. Apple, Microsoft, Google 등 해외 회사 상세 구현은 후순위다.

이유: 2000년 한국의 작은 가족회사라는 출발점에서 만날 수 있는 시장 밀도를 높이고, 회사 이름 혼동 없이 실제 산업사를 따라가기 위해서다.

## 2026-08-10 / 4인 회사의 초반 계약 상한

결정: 초반 계약은 최대 2건 동시 진행, 계약당 최대 80 인시·4명·250만원으로 제한한다. 가족 1명당 주 16시간의 계약 업무 용량을 사용한다.

이유: 가족 4명뿐인 창업 회사가 대기업급 계약을 수행하는 부자연스러움을 막고, 각 가족의 실제 이동과 업무 배정이 계약 성패에 중요하게 만들기 위해서다.

## 2026-08-10 / 낮은 금액의 인수는 실제 소형·부실 회사와 자산 인수로 제공

결정: 실제 대기업 가치를 임의로 낮추지 않는다. 작은·부실·청산 기업의 전체 인수와 함께 사업부, 소스코드, 특허, 도메인, 장비, 고객계약, 핵심 인력의 자산 인수를 제공한다.

이유: 초기에도 도달 가능한 인수 선택지를 충분히 만들면서 실제 회사의 역사적 규모를 훼손하지 않기 위해서다.

## 2026-08-10 / simul의 역사 자료를 Claude 조사 입력으로 재사용

결정: Claude는 기존 `simul`의 DATA_SOURCES, 시장 시대 사건, 시장 코퍼스, 기업행동 자료를 읽기 전용으로 먼저 검색한다. 파생 데이터는 참고하되 실제 법인명과 날짜는 원 1차 출처로 다시 확인한다.

이유: 사용자가 이미 제공하고 검증한 2000~2026 한국 시장 자료를 중복 조사하지 않으면서 잘못된 파생 해석이 정본 사실로 굳는 것을 막기 위해서다.

## 2026-08-10 / 계약 진행은 가족별 인시와 실제 재무 결과를 가진다

결정: 계약 수락 시 착수 비용을 지출하고, 가족별 작업 인시가 체력·스트레스를 바꾸며, 납기 내 완료 시 매출·평판을 얻는다. 미완료 상태로 납기를 넘기면 자동 실패한다. 계약과 가족별 기여는 저장 스키마 v2에 보존하고 v1 저장은 빈 계약 목록으로 이관한다.

이유: 계약을 단순 버튼 보상으로 끝내지 않고 이후 NPC의 실제 웨이포인트 이동, 가족 업무 배분, 회계와 직접 연결하기 위해서다.

## 2026-08-10 / 계약 인시는 실제 웨이포인트 작업 완료 후에만 증가

결정: 가족 NPC가 계약에 맞는 회의·업무·출력 지점으로 CharacterController 이동을 끝내고 정해진 작업 체류 시간까지 완료한 경우에만 계약 인시를 반영한다. 시작 시점의 이동 NPC는 누나·아빠·엄마이며 직원 A·B placeholder는 사용하지 않는다.

이유: 사용자가 요구한 실제 이동을 장식이 아니라 회사 성과의 원인으로 만들고, 4명뿐인 시작 회사에 존재하지 않는 고용 직원을 제거하기 위해서다.

## 2026-08-10 / PC 가로형 풀스크린과 3개 저장 슬롯

결정: 시작 화면과 게임 HUD는 1920×1080 PC 가로 화면을 기준으로 만들고 borderless fullscreen, 1600×900 창 모드와 크기 조절을 지원한다. 처음하기·이어하기·불러오기·게임 중 저장은 3개 슬롯을 공유하며 기존 단일 저장은 1번 슬롯으로 읽는다.

이유: 기존 `simul`의 세로 모바일 구성을 그대로 옮기지 않고 넓은 사무실 공간과 실제 캐릭터 이동을 동시에 보여주기 위해서다. 슬롯을 명시해 새 게임과 장기 캠페인을 안전하게 병행한다.

## 2026-08-10 / imagegen 타이틀 키아트와 런타임 UI 분리

결정: 메인 화면은 OpenAI imagegen으로 만든 16:9 가족회사 키아트를 사용하되 글자와 버튼은 이미지에 넣지 않는다. 누나 정본을 오른쪽에, Unity 메뉴 안전 영역을 왼쪽에 두고 aspect-fill로 표시한다.

이유: `simul`처럼 첫 화면에서 게임의 매력을 강하게 전달하면서도 해상도, 언어, 저장 상태, 버튼 활성화가 바뀔 때 이미지를 다시 만들지 않고 UI를 정확하게 유지하기 위해서다.

## 2026-08-10 / SIMUL v3를 가족회사 최상위 화풍으로 고정

결정: 인물 원화와 키아트는 `SIMUL polished soft-render VN anime v3`를 그대로 최상위 화풍으로 사용한다. 런타임 캐릭터와 사무실은 같은 유색선, 색층, 웜 명암과 재질 대비를 선명한 등각 도트로 번역한 `Family Company SIMUL-v3 isometric pixel translation v1`을 사용한다. 승인 화풍 앵커를 프로젝트 안에 복사하고 모든 후속 imagegen의 Image 1 화풍 전용 참조로 고정한다.

이유: 타이틀·캐릭터·사무실이 서로 다른 게임처럼 보이는 문제를 막고, 고급 VN 원화와 실제 이동 도트가 같은 미술 세계에 속한다는 인상을 유지하기 위해서다.

## 2026-08-10 / 플레이어 초기 월드 조작 말 외형 (후속 결정으로 대체)

결정: 기존 `simul` 타이틀의 14살 소년 디자인을 가족회사 플레이어의 초기 런타임 조작 말로 사용했다. 당시 4방향 2프레임은 빨간 뉴스보이캡을 포함했으나, 아래의 최신 `8방향×6프레임, 플레이어 무모자` 결정이 이 모자 규칙을 대체한다. 흰 후드 윈드브레이커와 줄무늬 티셔츠는 유지한다. 이는 별도 VN 초상화나 실존 사용자의 얼굴을 정의하는 결정이 아니다.

이유: 실제 이동과 사무실 상호작용에서 플레이어 위치와 방향을 즉시 읽을 수 있게 하면서, 사용자가 이미 소유한 `simul` 디자인 자산을 새 게임의 주인공 표식으로 일관되게 이어가기 위해서다.

## 2026-08-10 / SIMUL 주식시장은 관찰 가능한 동작까지 정확 이식

결정: Unity 시장 코어의 구조는 순수 C#으로 나누되, SIMUL의 장 시간·가격·비용·체결뿐 아니라 호가 7+7, 분당 pulse 수, 체결 batch FIFO, 도착/소진 단계, 현재가·최근 체결·테두리 동시 갱신, pause 고정까지 Dart golden과 동일하게 유지한다. 회사 데이터 교체를 이유로 호가 움직임을 바꾸지 않는다.

이유: 사용자가 호가창 움직임을 포함한 모든 시장 기능의 정확한 이식을 핵심 요구로 확정했다. 구조적 리팩터링이 플레이 감각 변경으로 이어지지 않게 하기 위해서다.

## 2026-08-10 / Korea History V1을 실제 회사와 시장 종목의 정본으로 사용

결정: 기존 가상 회사 대신 Korea History V1의 국내 실제 회사 82개를 런타임 기준선으로 사용한다. 저장과 시뮬레이션은 불변 `companyId`, 화면은 날짜별 `displayNameKo`, 주식시장은 날짜별 KOSPI/KOSDAQ 상장 구간과 ticker를 사용한다. 2000년 데이터는 시장 엔진을 수정하지 않고 데이터 공급자로 연결한다.

이유: 회사 이름과 역사를 나중에 갈아끼울 수 있으면서도 호가·체결·저장 결정론을 보존하고, 실제 2000년 한국 회사들로 캠페인을 바로 시작하기 위해서다.

## 2026-08-10 / 부모 정본과 SIMUL 8인을 직원 후보 에셋 풀로 확정

결정: 아빠 46살과 엄마 44살의 SIMUL v3 전신 원화 및 4방향 2프레임 도트를 가족 정본으로 사용한다. 기존 `simul`의 김서아·이지안·최이서·정아린·박하은·한수아·오지우·윤채아는 원화 9종씩을 변경 없이 보존하고, 고유 정체성을 유지한 가족회사 도트를 더해 향후 고용 가능한 직원 후보 에셋 풀로 사용한다. 이들은 시작 시점의 4인 가족 창업팀에는 자동 합류하지 않는다.

이유: 부모 placeholder를 정식 연령·역할에 맞는 인물로 교체할 기반을 마련하고, 이미 승인된 8인의 캐릭터성을 훼손하지 않으면서 고용 확장에 필요한 전신 원화와 실제 이동 도트를 미리 일관된 규격으로 준비하기 위해서다.

## 2026-08-10 / 가족별 시간대와 결정론적 사무실 자율 행동

결정: 가족의 회사 업무 가능 여부를 30분 단위 의미 시간표로 계산한다. 평일 낮의 플레이어 학교, 누나 외부 일정, 아빠 대외 영업, 엄마 가사 시간을 회사 인시에서 제외하고 저녁·주말에 다시 참여시킨다. 누나·아빠·엄마 NPC는 체력·스트레스·역할에 따라 책상·접수·프린터·회의실·휴게실을 자율 선택해 실제 이동하며, 계약 업무는 언제나 자율 행동보다 우선한다. 저장에는 Transform이 아니라 현재 행동, 의미 목적지, 처리 시각, 업무·휴식 블록과 번아웃·사건 요약만 보존한다.

이유: 가족을 하루 종일 사용할 수 있는 직원 슬롯으로 만들지 않고, 누구를 언제 투입할지가 계약 선택의 실제 비용이 되게 하기 위해서다. 절대 30분 경계와 seed 기반 안정 키를 사용하면 1일 점프와 1시간 분할 진행의 결과를 같게 유지하면서도 피로·휴식·관계 사건을 공간 행동으로 보여줄 수 있다.

## 2026-08-10 / SIMUL 이관은 의미 규칙·검증·허가 자산으로 제한

결정: 기존 `simul`에서 가져오는 것은 순수 규칙과 데이터 구조, 검증된 동작, 라이선스가 명확한 원본 자산으로 제한한다. 세로 모바일 패널, 좌표, 비율, 행동력 화면은 복사하지 않는다. 가족회사의 계약·R&D·채용·은행·주식·자체 사업 화면은 모두 1920×1080 16:9 PC 가로 풀화면으로 다시 구성하고, 새 시각 자산이 필요한 UI는 밝고 캐주얼한 ImageGen 가로 비주얼을 먼저 만든 뒤 Unity의 실제 글자·버튼과 분리한다.

이유: 모바일 세로 정보 밀도를 그대로 옮기면 넓은 사무실과 네 가족의 동시 행동을 가리고, 이미 확정된 PC 경영 게임의 조작·가독성과 충돌한다. 의미 규칙과 화면 투영을 분리하면 SIMUL의 검증된 재미는 보존하면서도 가족회사에 맞는 풀화면 UI를 만들 수 있다.

## 2026-08-10 / 외부·회복 활동은 ImageGen 장면으로 체험시킨다

결정: 편의점·PC방·비디오/만화 대여점·목욕탕·외식·산책·소풍·문방구 오락기·라디오 야식·노래방·ADSL 게임은 텍스트 버튼만 누르는 목록으로 끝내지 않는다. 각 활동은 가족 정체성과 2000년 한국 장소·소품이 보이는 밝고 캐주얼한 SIMUL-v3 ImageGen 가로 장면을 가지며, 1920×1080 16:9에서 안전하게 크롭한다. 이미지에는 글자·버튼·아이콘을 굽지 않고 활동 선택·비용·효과·확인은 Unity UI로 분리한다.

이유: 회복을 수치 교환 버튼이 아니라 가족이 실제로 함께 시간을 보내는 기억으로 보여 주고, 장기 캠페인에서 관계의 역사를 장소와 장면으로 회상할 수 있게 하기 위해서다.

## 2026-08-10 / 캐릭터 이동은 8방향×6프레임, 플레이어는 무모자로 갱신

결정: 플레이어·누나·아빠·엄마와 직원 후보 8인의 런타임 이동 정본을 8방향, 방향별 걷기 6프레임으로 통일한다. 플레이어의 빨간 뉴스보이캡은 사용자의 명시 요청에 따라 제거하고, 짧고 헝클어진 짙은 갈색 머리가 드러나는 무모자 외형을 새 정본으로 사용한다. 구형 4방향×2프레임 시트는 정체성과 카메라 참조용 레거시로만 보존한다.

이유: 360도 전용 시트의 과도한 제작량 없이도 등각 카메라에서 대각선 이동을 자연스럽게 읽히게 하고, 접지·하강·통과의 6단계 보행으로 2프레임 왕복 특유의 딱딱함을 없애기 위해서다.

## 2026-08-11 / TMP Essential Resources를 저장소에 포함한다

결정: Unity 공식 TextMesh Pro Essential Resources를 패키지 임포트에 의존하지 않고 `Assets/TextMesh Pro/`(37개 파일, `Resources/TMP Settings.asset`과 셰이더 18종 포함)로 저장소에 넣는다.

이유: 관리 UI v2의 한글은 런타임 동적 아틀라스로 만들어지는데, `TMP Settings` 리소스와 TMP 셰이더가 없으면 폰트 생성이 시작조차 하지 못한다. 이 리소스는 각 작업 PC가 에디터에서 수동으로 임포트해야 생기므로, 저장소에 넣지 않으면 클론한 PC마다 한글 UI가 조용히 깨진다. 빈 `TMP Settings`만 자동 생성하는 방식은 셰이더가 빠져 실패했으므로 공식 리소스 전체를 넣는다.

## 2026-08-11 / 이동이 막힌 걸음은 정지가 아니라 미끄러짐으로 강등한다

결정: `OfficeWorkerAgent`가 가구·NPC 충돌로 한 걸음을 거부당하면 속도를 0으로 만들고 끝내지 않는다. 의미 목적지 방향을 기준으로 0~180°를 좌우로 훑어 통과 가능한 걸음을 찾고, 최소 탈출 보폭 0.06m을 보장한다. 어느 방향도 불가능할 때만 정지한다. 도착 판정 거리나 QA 기준을 느슨하게 바꾸는 방식은 쓰지 않는다.

이유: 정지 프레임이 속도를 0으로 만들면 다음 프레임의 변위도 0이 되어 탈출 방향을 시험할 수조차 없고, 재계획도 경로를 만들 수 없는 자리에서 다시 시작되어 영구 교착이 된다. 실제로 엄마와 누나가 책상 이탈 직후 좌표에 고정되어 30초 퇴실 시나리오가 실패했다. 최소 보폭이 없으면 미끄러짐 탐색 자체가 무효라 이 둘은 함께 지켜야 하는 불변조건이다.

## 2026-08-11 / 사무실 타일 전환은 T1~T3 방향 검증 뒤 확정한다

결정: 단일 OfficeVisualV2 PNG와 수동 아트 픽셀 호모그래피를 장기 사무실 정본으로 확장하지 않는다. `Simulation`이 소유하는 13×13 `OfficeGrid`와 저장 스키마 v6, Unity 내장 Isometric Tilemap 바닥, 같은 깊이 축에서 정렬되는 8방향×6프레임 가족 캐릭터로 T1~T3 방향 검증을 먼저 완료한다. 기존 `Prototype01`의 OfficeVisualV2·3D Collider·계약·좌석 연결은 이 검증 중 폴백으로 유지하며 T4 전에 제거하지 않는다. T3 실제 캡처를 사용자가 확인하기 전에는 가구·좌석·계약을 새 격자로 이관하지 않는다.

2026-08-11 폐기 결정: 사용자 확인 결과 OfficeVisualV2 통짜 PNG 화면은 더 이상 폴백이나 참고 렌더로 노출하지 않는다. base/foreground/guide 파일을 삭제하고, 새 게임과 불러오기는 StarterOfficeV1 타일 씬을 기본 렌더로 사용한다. 구형 월드의 비주얼은 차단하되 계약·자율 AI·Collider는 T6 완전 이관 전까지 백그라운드 호환 계층으로만 유지한다.

이유: 회사 성장 단계마다 배경 PNG와 책상 좌표를 다시 그려 맞추는 구조는 가구 배치, 경로 탐색, 사무실 확장을 막는다. 반대로 한 번에 전부 교체하면 검증된 계약·자율행동·좌석 회귀 범위가 지나치게 커진다. 의미 격자와 화면 투영을 먼저 분리하고 실제 이동 화면에서 방향을 승인받는 것이 가장 작은 되돌림 지점이다.

## 2026-08-11 / T4~T5 가구·착석은 격리 프리뷰에서 먼저 완결한다

결정: 타일 사무실의 첫 가구는 잘못 잘린 구형 4×3 아틀라스를 재사용하지 않고, OpenAI 내장 ImageGen으로 한 이미지에 한 소품만 담은 12종을 새로 생성한다. 의미 격자의 footprint와 시각 Sprite의 `ground/sort/seat/work-surface` 앵커를 분리하고, 12종 데이터는 `OfficeFurnitureVisualCatalog`에서 관리한다. 가구 X/Y 비균등 확대와 종류별 코드 위치 보정은 금지한다. 의자·책상 가림은 고정 Y 절단이 아닌 명시적 픽셀 마스크 front Sprite로 만든다. 이 작업은 `OfficeTileMigrationPreview`에 격리하고 기존 `Prototype01`의 계약·자율 AI·A*와 아직 결합하지 않는다.

이유: 손상된 아틀라스 조각을 배치하면 어떤 좌표를 잡아도 인접 물체가 붙거나 잘리고, 의자를 막힌 셀로 만들면 실제로 걸어 들어가 앉을 수 없다. 의미 상태와 렌더 깊이를 분리한 뒤 30초 충돌 감시와 네 가족의 정확한 좌석·방향을 먼저 검증하면 기존 게임 회귀를 최소화하면서 T6 통합의 기준 화면을 고정할 수 있다.

교정 근거: 최초 v2 의자는 좌석 방향과 등받이 방향이 맞지 않았고 전경 조각이 인물 몸을 덮었다. 최초 v2/v3 책상은 넓은 옆판이 바닥선까지 내려와 바닥에 박힌 것처럼 보였다. 임시 방향 대칭 root 오프셋도 의미 좌표와 화면 좌표를 섞으므로 폐기했다. 캐릭터 의미 root/VisualRoot 분리와 pelvis↔seat 앵커 정렬, 네 다리 책상, 명시적 workstation binding을 적용해 60초 동안 가구 Transform 정확히 0 변화, 네 가족 ground/pelvis/centerline 오차 0.000px, 좌석 중복·막힌 칸 침범 0을 통과했다.

## 2026-08-11 / Office Alignment V2는 실제 사무실·QA fixture·시각 캘리브레이션을 분리

결정: `CreateMigrationPreview()`는 T1~T5 회귀 fixture로만 유지하고 새 게임과 구형 저장 이관은 파티션이 없는 `CreateStarterOfficeV1()`을 사용한다. 책상·의자·좌석은 `OfficeWorkstationSlot`으로 명시적으로 묶고 반 셀 `OperatorAnchor`, 책상 operator seat/work socket, 의자 seat anchor를 각각 보존한다. 가구는 의미 root와 균등 scale `VisualRoot`를 분리하며, 실제 타일 footprint는 단일 ground 점이 아니라 독립 저장된 네 꼭짓점으로 검사한다. 가족 착석 pose는 구성원·방향뿐 아니라 SitDown/Work/StandUp의 clip/frame까지 키로 사용한다.

결정: `OfficeFurnitureVisualCatalog.asset`과 `OfficeCharacterSeatPoseCatalog.asset`은 calibration version 2의 유일한 승인 저장 위치다. PNG 재빌드는 이 값을 덮어쓰지 않는다. 현재 값은 실패 진단 candidate이며 수정은 합성 미리보기와 허용 오차를 함께 보여 주는 `OfficeTycoonAlignmentCalibrationWindow`에서 승인한 뒤 저장한다. 의자 NorthWest 정본의 등받이와 좌판은 인물 뒤 base에 둔다. 당시의 넓은 chair front overlay 미사용 결정은 `08d398b`에서 등받이·근접 팔걸이만 남긴 제한 전경으로 대체됐다. 책상 전면의 다리·서랍·앞 모서리도 제한된 front overlay로 인물 하체 앞에 둔다.

이유: V1은 배치에 사용한 ground/pelvis 수치를 같은 식으로 다시 계산해 화면이 어긋나도 통과할 수 있었고, 2×1 책상의 실제 작업자 위치와 프레임별 신체 변화도 표현하지 못했다. 의미 좌표, 실제 아트 캘리브레이션, 합성 QA를 서로 다른 입력으로 만들면 의자 방향·좌판 중심·책상 접지·손 위치의 오류를 수치와 화면 양쪽에서 드러낼 수 있다.

## 2026-08-11 / Starter Office의 유일한 정본은 GameState.OfficeGrid와 Runtime Actor다

결정: `Prototype01`의 실제 게임 상태 위에 Preview Actor를 덧씌우는 이중 월드를 폐기한다. `OfficeTileMigrationPreviewBootstrap`은 단독 QA Scene의 에셋 공급원으로만 남기고, 실제 세션은 `StarterOfficeRuntimeBootstrap`이 정확히 player·older_sister·father·mother 한 명씩 생성한다. 계약과 자율 행동 Coordinator는 구체 Legacy Agent가 아니라 `IOfficeRuntimeAgent`에 바인딩한다.

결정: 가구 Sprite Transform은 배치 데이터가 아니다. `StarterOfficeLayoutAsset`에서 생성한 `OfficeGrid`가 바닥·가구 placement anchor·hard footprint·interaction seat·workstation binding·저장 해시의 단일 정본이다. 가구는 명시적인 예외가 없으면 이동을 막고, 의자는 claim 소유자의 마지막 착석 구간에서만 통과할 수 있다.

결정: 캐릭터의 이동 여부와 보행 속도는 렌더 프레임 전체의 실제 위치 변화량으로 결정한다. 방향은 실제 이동 heading을 기본으로 하되, 충돌 축 투영의 첫 0.15초와 직접 조작 급반전에서는 의미 heading을 짧게 유지한다. 캐릭터별 Runtime 방향 예외를 금지하고, source 방향 차이는 `HighMotionDirectionManifest.asset`의 import 정규화로 해결한다. 수학 테스트와 4명×8방향 사람 승인 contact sheet를 함께 요구한다.

이유: 숨은 Legacy Actor, visual-only 의자 보정, 의도 속도 기반 방향은 화면과 게임 상태를 서로 다른 사실로 만든다. 하나의 의미 레이아웃과 하나의 Actor 집합에서 렌더·충돌·경로·좌석·저장을 파생해야 사용자가 편집한 배치도 즉시 같은 규칙을 따른다.

결정: 워크스테이션 손 정렬은 캐릭터나 좌석의 위치 offset으로 보정하지 않는다. 실제 pelvis/hand anchor를 유지한 `OfficeCharacterSeatPoseCatalog` v3에 골반 기준 균등 scale과 회전 calibration을 저장하고, Starter Runtime은 방향 의미가 승인된 `OfficeSeatingV1` Work 프레임만 사용한다. 책상 operator work socket은 모든 가족이 공유한다.

이유: 위치 offset은 의미 root·좌석 claim·충돌과 화면을 다시 분리하고, 방향이 서로 다른 Legacy micro-action 프레임은 실제 손 anchor 계측을 무효화한다. 골반을 좌판에 고정한 자세 calibration은 동일한 소켓·충돌·저장 규칙을 유지하면서 실제 hand anchor를 정확히 맞춘다.

## 2026-08-11 / Seated Sprite Root Cause V3는 회전·확대를 폐기하고 승인된 정적 자세로 봉합한다

결정: b53c355의 pose v3 골반 기준 scale/rotation 교정을 폐기한다. `VisualRoot.localRotation`은 항상 identity, pose scale은 1.0으로 고정하고 Animator가 Sprite를 적용한 직후 실제 pelvis를 chair seat로 옮기는 translation만 허용한다. 손이 공용 work socket과 맞지 않으면 실제 anchor 또는 원화를 수정하며 회전·확대로 맞추지 않는다.

결정: `OfficeCharacterSeatPoseCatalog` v4는 `HumanApproved`와 source Sprite SHA-256이 일치하는 네 가족의 `NorthWest/Work/0`만 safe mode에서 허용한다. v3의 56개 반복 프로필은 자동 이관·자동 승인하지 않는다. 현재 정렬의 단일 소유자는 `OfficeRuntimeDepthSorter`이며 가구 footprint와 착석 상태를 함께 정렬해 chair base < character < chair front를 보장한다.

이유: b53c355는 player 17.43%, older_sister 27.55%, father 20.84%, mother 10.81% 확대와 최대 13.68° 회전을 자세 전체에 적용해 손 오차를 줄이는 대신 얼굴·몸·다리 비율을 찌그러뜨렸다. source SHA가 없는 반복 프로필은 실제 Sprite 변경도 감지하지 못했다. V4 safe mode는 화면 품질을 승인 가능한 한 장으로 제한하고, 실제 아트가 준비된 프레임만 점진적으로 확장한다.

## 2026-08-12 / 착석은 승인 좌판 등록점·의자 전후 레이어·공통 scale 1.55로 고정한다

결정: 착석 인물은 자기 바닥 접점을 의자 바닥에 세우지 않는다. 프레임별 실제 엉덩이/좌판 접점을
`OfficeCharacterSeatPoseCatalog`에 기록하고 의자의 실제 cushion anchor에 translation으로 고정한다.
착석 인물은 의자 base보다 앞, 등받이·근접 팔걸이 전면 레이어보다 뒤에 그린다. 이 규칙은
SittingDown·Working·FinishingWork·StandingUp 전 구간에 유지한다. 공통 캐릭터 시각 scale은 `1.55`,
회전·pose scale·member별 scale은 계속 금지한다.

결정: 엄마 `Northwest/Work/0..5`는 원본 6장 모두 하단에서 다리가 잘렸으므로 전부 교체한다. 최종
6장은 256×256, visible height 228px, hard alpha, 발바닥 하단 여백 7px로 통일하고 frame 0의 좌판
등록점 `(131,62)`, 손 접점 `(90,120)`, 새 source SHA를 승인 catalog에 기록한다. 자동 해부학 후보
`(149,75)`는 실제 의자 합성에서 좌판을 벗어나므로 폐기한다. SafeStaticWork는 계속 frame 0만 사용하고,
나머지 5장은 프레임별 접점 승인 후에만 애니메이션 런타임에 연다.

이유: 의자 좌판은 바닥 접점보다 108.2px 위인데 기존 가족 엉덩이는 발보다 52~76px 위라 바닥 정렬 시
전원이 좌판 아래로 가라앉는다. 또한 의자 sort anchor가 ground anchor보다 약 20px 낮아 바닥 순서만
사용하면 좌판이 골반을 덮는다. 실제 Windows 플레이어에서 네 가족 seatContact `0.000px`, 의자 base <
인물 < 의자 전면 레이어 순서, 엄마의 무릎·종아리·발 전체 노출을 확인했다.

## 2026-08-12 / 이동 시뮬레이션과 방향·보행 표현의 시간 단위를 분리한다

결정: 한 렌더 프레임이 여러 0.05초 이하 substep으로 나뉘어도 Animator에는 마지막 substep이 아니라
전체 실제 변위, 전체 의미 변위, 전체 경과 시간을 한 번 전달한다. `IsMoving`은 전체 실제 변위로 판정하고
보행 cadence의 속도 단위는 `실제 이동거리 / 초`로 계산한다. 8방향 경계는 4° hysteresis와 0.075초
후보 안정화를 사용하며, 충돌 축 투영 중 의미 heading 보존은 최대 0.15초로 제한한다.

결정: 대각선 이동이 막혔을 때 X축을 무조건 먼저 선택하지 않는다. 통과 가능한 X/Z 후보를 의미 목적지
진행량, 직전 실제 축 연속성, agent ID 기반 안정 tie-break로 비교한다. 직접 조작 플레이어의 정지와 135°
이상 반전만 각각 기본 속도 변화율의 1.7배·1.8배로 응답시키고 NPC의 기존 변화율은 유지한다.

이유: 변위량을 속도로 사용하면 FPS와 substep 수에 따라 발걸음 주기가 달라지고, 마지막 substep만 보면
실제로 이동한 프레임이 idle로 덮인다. 또한 X 우선 투영과 실제 변위만의 즉시 방향 전환은 벽·책상 모서리에서
의도와 반대로 걷는 프레임을 만든다. 시뮬레이션 사실과 표현 heading을 명시적으로 분리하면 충돌 정합성을
유지하면서도 조작 방향과 화면 방향의 역전을 제한할 수 있다.

## 2026-08-12 / 착석 v5는 Northwest 56개가 완전할 때만 실제 애니메이션을 연다

결정: `OfficeCharacterSeatPoseCatalog` v5에 네 가족별 Northwest SitDown 4, Work 6, StandUp 4를
저장한다. 56개 모두 사람 승인, 실제 신체 내부 pelvis/hand, source SHA-256, scale 1, rotation 0을
만족할 때만 Starter Runtime을 `Animated`로 구성한다. 하나라도 빠지면 승인된 Work/0
`SafeStaticWork`로 fail-closed한다. 렌더 틱 하나에서 여러 착석 프레임을 소비하지 않는다.

이유: 4배속 플레이어 QA에서 기존 누적 while이 한 렌더 틱에 SitDown 두 장을 건너뛰는 결함을
재현했다. 틱당 한 장 전진으로 바꾼 뒤 네 가족 모두 SitDown 4/4, Work 6/6, StandUp 4/4,
anchor error 0.000px, rotation 0°, scale deviation 0%, agent penetration 0으로 통과했다.
## 2026-08-12 / 가구 충돌은 시각 footprint와 분리한 4×4 gameplay profile이 소유한다

결정: OfficeFurnitureCollisionCatalog에 가구 종류·방향·의미 footprint 크기별 4×4 서브셀 마스크와 clearance padding을 기록한다. 직접 이동, NPC 경로 탐색, 좁은 통로, Interaction 좌석은 모두 OfficeRuntimeOccupancy의 동일한 반지름 확장 마스크를 사용한다. 프로필이 없거나 배치 크기·방향이 다르면 기존 전체 셀 충돌로 fail-closed 한다. 시각 정렬용 groundFootprintPolygonPx는 충돌 정본으로 사용하지 않는다.

이유: 전체 의미 셀 사각형은 화분·커피 테이블·의자처럼 실제 바닥 실루엣이 작은 물체 주변에서 보이지 않는 벽을 만든다. 반대로 시각 스프라이트의 footprint는 정렬과 가림을 위한 값이라 gameplay clearance와 변경 주기가 다르다. 전용 마스크를 두면 현재 가족 반지름을 보존하면서 모서리 오탐을 줄이고, 미등록 콘텐츠는 안전하게 기존 동작을 유지할 수 있다.
## 2026-08-12 / 이동 전환은 기존 6프레임 보행 루프와 별도 클립으로 유지

결정: 가족 4명의 `turn_in_place`, `walk_start`, `walk_stop`, `short_shuffle`를 각각 8방향×2포즈의 독립 아트로 제작한다. `Walk` 상태만 기존 8방향×6프레임 루프를 사용하고, `Pivot`, `StartStep`, `Stopping/Idle`, `ShortShuffle`은 각 전용 클립을 사용한다. 모든 전환 PNG는 256×256, 180 PPU, Point, 하단 중앙 피벗이며 보이는 발바닥을 캔버스 하단 8px에 정렬한다.

이유: gait 상태만 나누고 같은 걷기 프레임을 출력하면 출발·급정지·짧은 이동·90도 이상 회전이 시각적으로 구분되지 않는다. 반대로 기본 보행을 바로 8프레임으로 늘리는 것보다 짧은 전환에 전용 2포즈를 제공하는 편이 체감 개선이 크고, 기존 검증된 보행 루프를 보존할 수 있다. 발바닥 정규화는 생성 시트마다 다른 여백 때문에 방향 변경 때 캐릭터가 튀는 문제를 방지한다.

## 2026-08-12 / P1.5는 외부 AI 엔진이 아니라 내부 Shadow Smart Interaction으로 시작한다

결정: NPBehave·CrystalAI·TotalAI·GOAP를 설치하거나 소스를 복사하지 않는다. 현재 13종 Micro Action과
기존 `WeightedPick`을 행동 선택의 정본으로 유지한 채, 순수 C# `OfficeInteractionCatalog`에 현재 후보의
action/location/target/weight를 1:1 표현한다. 새 Utility는 정수 score breakdown과 결정론적 top-band
Shadow 선택만 기록하며 GameState·Save DTO·OfficeRuntimeAgent·패키지 manifest를 변경하지 않는다.

이유: 현재 P1은 이미 쿨다운, capacity, 대화 pair, 45분 책상 제한, step/jump·save/load 결정론을 갖고 있다.
새 프레임워크는 이 정본을 중복시키지만, 스마트 오브젝트식 정의와 Utility 점수 분해는 선택 이유와 향후
가구 Offer 연결을 검증하는 데 유용하다. Shadow 모드는 실제 플레이 결과를 보존한 상태에서 점수 품질과
결정론을 측정하고 다음 활성화 여부를 별도로 판단하게 한다.

## 2026-08-14 / 외곽 벽은 바닥 바깥의 한 타일 모듈, 출입구는 상시 열린 gap

결정: Starter Office 외곽은 별도 직선 화면 wall이나 외곽 셀 중심선이 아니라 13×13 바닥 polygon의 실제 외변을 따르는 한 타일 모듈 52개로 닫는다. 의미 좌표는 보존하고 presentation root만 변별 half-cell offset으로 옮긴다. source의 두 실제 alpha endpoint `(316,172)`→`(796,412)`와 runtime `160×80px` span을 함께 검증하며 SouthWest 면은 같은 모듈의 X mirror를 사용한다. far edge는 full height, near edge는 cutaway height를 사용한다.

결정: 저장·layout·GUID 호환을 위해 `entrance_door`/`EntranceDoorKind` 이름은 유지한다. 이 키의 시각 의미는 문이 아니라 외곽선에 놓인 얇은 threshold뿐인 항상 열린 gap이며 door leaf, jamb, lintel, Animator/Animation을 금지한다. family entrance authority `(8,1)`과 이동/좌석/방향 규칙은 바꾸지 않는다.

결정: 벽 기단 픽셀은 floor polygon 내부 침범 0px를 요구하고, 수직 벽면 투영은 별도 occlusion mask로 계측한다. 같은 베이스를 반대쪽 변에 재사용해 내측으로 돌출시키지 않도록 far full-wall source는 edge connection 위로 솟는 face/cap만 유지한다.

결정: 벽 아트 갱신은 `BuildPerimeterWalls`로만 수행한다. 전체 가구 builder를 캡처 경로에서 호출하지 않고 catalog의 세 perimeter definition만 교체한다. 비벽 definition identity와 swivel-chair overlay link/occupied flag를 변경하면 즉시 실패한다.

결정: 출근 표현은 시각적 문 열림 없이 해당 shift의 첫 입장 성공(정상 진행에서는 09:00, 같은 `GameState`의 시간/날짜 점프 시 그 직후)의 `door_open` 한 번만 사용한다. gate는 BeforeWork 또는 관측된 비근무일→다음 근무일 Working 전환에서 날짜 shift를 arm하고 성공 재생 뒤 잠긴다. 이미 근무 중인 저장본을 새 `GameState`로 bind하면 그 날짜는 소리 없이 소비한다. 실제 전체 audio counter로 open 1/close 0 및 same-shift load delta 0을 Windows player QA에서 검증한다.

## 2026-08-14 / 도트 월드는 네이티브 출력과 presentation pixel grid를 사용한다

결정: 1920×1080을 기준으로 월드 카메라는 render scale 1.0을 사용하며 고정 360/540p 중간 버퍼와
전체 화면 sharpening을 사용하지 않는다. Dynamic Resolution은 끄고 DPI factor는 1.0을 유지한다.

결정: 카메라와 움직이는 비착석 캐릭터의 화면 표현만 물리 픽셀 grid에 맞춘다. 시뮬레이션 root,
semantic cell, occupancy, save 좌표는 바꾸지 않으며 렌더 직전 임시 보정은 렌더 직후 복원한다.

결정: 바닥·가구·벽·캐릭터 도트는 Point, mipmap off, Standalone uncompressed, 원본보다 작은 max size
금지, 180 PPU를 사용한다. Painted/고해상도 UI는 Bilinear를 유지한다. Sprite Atlas를 도입하면 Point,
compression, padding을 별도 승인하기 전에는 검증을 실패시킨다.

이유: 기존 540p downsample은 월드만 2×2 출력 블록으로 만들었고 UI와 DPI는 정상이라 sharpening이나
원본 재생성으로 해결할 문제가 아니었다. native sampling과 presentation snap이 같은 프레임의 얇은 경계
에너지를 2.20배 보존하면서 world state와 병렬 이동/착석 코드를 건드리지 않는다.

## 2026-08-16 / 반복 확인은 Fast QA, 릴리스 빌드는 배포 후보에만

결정: 한 곳을 고치고 결과를 확인하는 반복 루프의 정본 명령은 `FAST_QA_WINDOWS.cmd`이며, `BUILD_WINDOWS.cmd`와
`DEPLOY_WINDOWS.cmd`는 배포 후보 HEAD가 확정되고 clean일 때만 실행한다. 변경 종류별 명령과 실측 근거는
`Docs/ITERATION_LOOP.md`가 정본이고 릴리스 절차는 `PLAYTEST_BUILD.md`/`REGRESSION_BUILD_POLICY.md`가 계속 정본이다.

결정: `Library`, `Library/Bee`, `Artifacts/FastQa`의 플레이어 캐시는 일상 실행 사이에 삭제하지 않는다. 반복
작업은 `Library`가 이미 warm인 기존 worktree 한 곳에서 수행하고, 병합이 끝난 worktree는 정리한다. 새 worktree는
그 첫 실행이 100초 이상 걸린다는 비용을 인정하고 시작한다.

이유: 실측에서 병목은 빌드 옵션이 아니라 cold `Library`였다. `build-20260815-035423-unity.log`의 자동화 전체
133.4초 중 103.707초가 `Asset Pipeline Refresh ... InitialRefreshV2(ForceSynchronousImport)`였고 같은 로그는
`Require frontend run. Library/Bee/1900b0aE.dag couldn't be loaded`도 남겼다. 반면 warm `Library/Bee`에서는 normal
incremental player build가 6.93/6.94/7.00초, forced clean release-config build조차 16.00/19.58/19.44초다. 즉 강제
clean 빌드보다 worktree를 새로 만드는 쪽이 5배 이상 비싸다. 이미 존재하던 Fast QA 경로가 `Artifacts/FastQa/runs`
기준 두 worktree에서만 쓰이고 있었으므로, 도구를 새로 만드는 대신 지침을 정본화해 실제 사용을 강제한다.

결정: Enter Play Mode Options와 Unity Accelerator는 후보로만 기록하고 적용하지 않는다. 전자는
`ProjectSettings/EditorSettings.asset`이 `m_EnterPlayModeOptionsEnabled: 1`, `m_EnterPlayModeOptions: 0`이라 현재
이득이 0이지만, `LiveContentPath`의 `_cachedRoot`/`_rootResolved`, `GameAudioCoordinator._instance`,
`OfficeInteractionSelectionTrace.TraceRecorded` 세 지점의 런타임 가변 static이 Play 사이에 초기화되지 않는 문제를
먼저 해결하고 Unity 실행 검증을 통과해야 한다. 후자는 별도 설치와 엔드포인트 취급 규칙이 필요하다. 검증 전까지
둘 다 현재 상태가 아니다.

## 2026-08-16 / 90도 코너는 멈추지 않고 지나간다

결정: `OfficeSharedLocomotionRules.RequiresStationaryPivot`의 정지 회전 임계값을 2옥탄트에서 3옥탄트로
올린다. 90도 사분 회전은 걷는 속도를 유지한 채 지나가고, 135도 이상의 되돌아가기에서만 발을 심고 회전한다.

이유: cell-centre 4방향 routing으로 바뀐 뒤 일반 코너는 예외 없이 정확히 2옥탄트가 되었다. 기존 `>= 2`
임계값은 그래서 모든 코너에서 액터를 완전히 정지시키고 45도씩 두 번 회전시킨 뒤에야 재출발시켰다. 사용자가
보고한 "방향전환할 때 흔들리면서 한 번 되고 다시 전환한다"는 이 정지-회전-회전-재출발 시퀀스이며, 코너에
정지 프레임이 끼는지에 따라 회전이 보이기도 안 보이기도 한 것도 같은 원인이다. 되돌아가기는 사람도 실제로
멈춰서 돌기 때문에 3옥탄트 이상은 그대로 둔다.

검증: `FAST_QA_WINDOWS.cmd -Profile editor-broad` PASS 18.561초, `-Profile player-scripts` PASS 23.565초
(SLO 60초 충족). 기존 안전 지표는 완화하지 않았고 그대로 유지된다. seeds=128, paths=1152,
movingFrames=1970, reverseFacingFrames=0, movingDuringPivot=0, maxFacingError=29.2740도(한계 30.5도),
unnecessaryCornerStops=0.

보류: 이동 중 표시 방향은 여전히 한 프레임에 스냅한다. 옥탄트당 고정 시간으로 몸을 돌리는 blending 후보는
`DisplayDirection == MotionDirection`을 이동 프레임마다 강제하는 런타임 불변식과 QA 단언 7곳을 함께 바꿔야
하고, 그중 `ReverseFacingFrames == 0`과 `MaximumFacingErrorDegrees` 같은 집계 지표를 느슨하게 만들어야 한다.
과거 역방향 버그를 잡던 그물이므로 이번에는 적용하지 않았다.
