# DECISIONS

## 2026-08-31 / Player V8과 Father V19를 같은 production runtime에서 함께 사용한다

결정: 승인된 Father V19 one-package FBX/albedo/material을
`Content/Resources/Production3D/FatherV19`로 승격하고 `Family3DProductionPresenter`가 실제
`OfficeRuntimeAgent player/father` 둘의 변위·방향·착석을 각자의 Humanoid에 투영한다. 두 사람과
각자 바인딩된 V31 세트의 구형 SpriteRenderer는 처음부터 끝까지 `forceRenderingOff`이며 fallback은
없다.

결정: 정적 가구 경로·의자 도킹 반경 `0.22`와 사람끼리의 3D 실루엣 반경을 분리한다. 동적 반경은
Player `0.28`, Father `0.46`이며 `OfficeRuntimeOccupancy`의 peer 충돌·예약·침범 측정에만 사용한다.
가구 통과 판정과 seat docking은 기존 검증된 `0.22`를 유지한다.

검증: Unity `6000.3.21f1` D3D11에서 정면 동시 접근 시 두 사람 모두 이동했고 agent block `50`,
penetration `0`, 별도 투명 렌더 실루엣의 공통 픽셀 `0`을 확인했다. 이어 세 개의 실제 구매 V31
세트에서 `seat_player/seat_father`, `Working/Working`, static/interaction/agent `0/0/0`, 구형 visible
renderer `0`을 확인했다. 정본 증거는 `Evidence/PlayerFather3DProduction/`이다.

## 2026-08-31 / 승인 Player V8과 V31 workstation을 production 기본 표시로 승격한다

결정: 사용자의 명시적 교체·구형 삭제 지시에 따라 Player V8 one-package FBX, albedo,
전용 material/shader를 `Content/Resources/Production3D/PlayerV8`로 옮기고
`Family3DProductionPresenter`가 유일한 주인공 표시를 소유한다. scale `1.024378657`, height
`1.857258558`, stride `0.7950477`, cycle `1.4 s`, 전신 회전 `0.18 s`, same-package Humanoid
Avatar/skin/clip을 고정한다. 에셋 누락·Avatar 오류 시 2D로 돌아가지 않고 fail-closed한다.

결정: 구형 Player 선택 모드, contact/natural/baked presenter와 Resources, PSB/FBX 제작실,
importer/baker 및 전용 QA를 삭제한다. 아직 Simulation과 분리되지 않은 기존 SpriteRenderer는
방향·착석 시계 데이터만 공급하고 처음부터 `forceRenderingOff=true`이며 공개 전환 API가 없다.

결정: 상점 구매, 4방향 회전, 초록/빨강 footprint, collision/pathfinding, save ID와 seat binding은
기존 의미 상태가 계속 소유한다. 배치된 bound set은 한 `Family3DWorkstation` 루트가 승인 V31
책상·CRT·키보드·원래 의자를 함께 표시하고 대응 sprite renderer는 숨긴다. 상점/ghost는 정확한
V31 방향 sprite를 계속 써서 확인 전후 tile footprint가 달라지지 않는다.

검증: Unity `6000.3.21f1` Windows Player Direct3D11에서 Player V8 locked height/stride,
Working 착석, 4개 set/4방향, 물리 mesh 축 90도, tile-corner 오차 최대 `0.0003px`, visible retired
Player/workstation renderer `0/0`을 확인했다. 빠른 hidden Player의 중간 자세 오판을 막기 위해
Working 진입 뒤 실제 `0.65 s`를 기다려 production `0.42 s` sit blend 완료를 보장했고, 양쪽
무릎 `107.45/113.16 degrees`, chair offset `0.13001`을 별도로 통과했다. 이전 isolated receipt의
`productionEligible=false`는 당시 사실로 보존하지만 Player에 한해 이 후속 사용자 승격 결정이
우선한다. Father는 위 후속 결정에서 별도로 승격됐고 Mother/Older Sister는 이번 변경 대상이 아니다.

## 2026-08-31 / Player V6 색과 머리 명암은 Player 전용 재질에서만 고정한다

결정: Player V6의 원본 알베도는 흰 tint로 그대로 사용하고,
`PlayerV6BalancedAlbedo.shader`가 중성 fill `0.70`과 완만한 normal form `0.18`만 계산한다.
발광, 반사, specular highlight는 사용하지 않는다. 보행 clip, Avatar, skin, stride, 방향, 착석
보정에는 손대지 않는다.

이유: 거절된 V6 표현은 전체 한 장짜리 알베도에 `0.74` 회색을 곱하고, 위 `0.61`에서 아래
`0.047`까지 방향성이 큰 production sky probe를 다시 적용했다. 그 결과 후드와 빨강/노랑/남색이
회색으로 죽고, 갈색 머리 다발의 밝은 결만 남아 은색 틈·발광처럼 보였다. 씬 ambient를 평탄화한
V7은 Player는 고쳤지만 승인된 책상/의자까지 어둡게 하므로 거절했다. V8은 Player material에서만
표현을 계산해 136프레임 전 방향에서 갈색 머리와 의상색을 유지하고 가구 조명은 그대로 둔다.
`productionMutation=false`, `productionEligible=false`를 유지한다.

## 2026-08-31 / Player V6도 same-package 613과 전체 실제 맵 GIF로 판정한다

결정: 모자 없는 Player V6는 Higgsfield/Meshy 작업
`8609013b-996c-439a-97a0-0f3dc8a50cae`가 함께 만든 메시·bind skeleton·skin weights·albedo·
action 613을 한 패키지로 유지한다. Unity에서는 같은 FBX Avatar와
`PlayerV6_Casual_Walk_inplace`를 `poseStrength=1`로 재생한다. Father 리그/클립, 절차 보행,
limb rewrite, rigid-arm correction과 pose 약화는 쓰지 않는다.

결정: 실제 맵 stride는 같은 build에서 `0.72/0.76/0.7950477/0.84/0.88`을 측정한 뒤 양발
planted-speed median/RMS가 가장 균형적인 `0.7950477`을 유지한다. 발 접지 플래그는 측정한
42-frame action-613 위상을 telemetry로만 기록하며 뼈에는 손대지 않는다.

근거: raw 127 frames x 2 views와 actual-map 169 frames 전체에서 두 다리/신발·두 팔/손,
반대 팔 스윙, 접지와 체중 이동, 네 코너 진행방향, 상체와 루프 경계를 확대 확인했다. 검수 GIF는
실제 연속 두 번째 바퀴 frames `84..167`이며 다음 실측 frame 168과 이어진다. 자동
`PASS_STRUCTURAL_ONLY`는 참고일 뿐 사용자 실제 GIF 승인을 대신하지 않는다. 승인 전 desk/sitting을
추가하지 않고 `productionEligible=false`로 둔다.

## 2026-08-31 / Family3D 구형 구현과 산출물은 현재 두 패키지만 남기고 제거한다

결정: Git의 Family3D 구현 입력은 Father V19 one-package와 Player V6 one-package, 두 현재 QA 장면,
V31/V8 검수 증거만 남긴다. 탈락 Father V14/V18, Player V3/V4, 미승인 Mother/Sister 실험 후보,
구형 Runtime2D 생성물, 구형 장면과 V27~V30/V2 증거는 제거한다. 두 현재 장면에서 삭제 후보 Unity GUID
참조가 0임을 먼저 확인했다.

로컬 `Artifacts/Family3DStarterOfficeCandidateQaV1` 233개 중 현재 승인 확인용 4개만 남기고
229개, 112.89GiB를 정리했다. Git 이력은 강제 재작성하지 않는다. production/default/Downloads/
배포 실행본은 계속 예외이며, 게임/save/schema 버전은 Family3D 실험 버전이 아니므로 대상이 아니다.

## 2026-08-28 / drawer details use the drawer cabinet front, not the desk front

결정: V27 3D 책상의 `Desk_DrawerLine_*`와 `Desk_DrawerHandle_*` forward 좌표는
`drawerForward - drawerDepth / 2`에서 계산한다. 전체 책상의 `frontForward`를 재사용하지 않는다.

이유: 기존 좌표는 세 줄과 손잡이를 서랍장에서 떼어 무릎 공간에 띄웠고, 실제 아이소메트릭 맵에서는
왼쪽 다리 옆의 톱니형 돌출물처럼 보였다. V29 전후 동일 위치 비교에서 이 돌출물이 사라졌고, 네 책상
공통 코드·실제 경로·132 captures·legacy renderer 0을 유지했다. 사용자 승인 전
`productionEligible=false`다.

## 2026-08-28 / furnished map의 네 workstation은 V27 3D 시각물을 공유한다

결정: `seat_player`, `seat_older_sister`, `seat_father`, `seat_mother`에 연결된 구형 갈색 책상·초록 의자
SpriteRenderer는 격리 3D proof에서 모두 숨기고, 각 semantic origin/footprint/socket으로 같은 V27
mapped-grid 책상과 neutral chair를 만든다. 의미 가구, blocking, 좌석 점유, 경로, 저장 데이터는 삭제하거나
이동하지 않는다.

이유: 기존 `office_workstation_v4.png`의 왼쪽 다리 금색 받침발이 작은 실제 맵에서 돌출물처럼 읽혔고,
V27은 Father 자리만 교체해 나머지 세 자리에 서로 다른 세대의 책상·의자가 남았다. V28은 4/4 생성,
visible legacy desk/chair renderer 0, 132 actual-map captures, `productionMutation=false`를 확인했다.
사용자 actual GIF 승인 전 `productionEligible=false`다.

## 2026-08-28 / 가족 3D는 four-view one-package generation에서 시작한다

결정: 다음 가족 캐릭터도 front/three-quarter/side/back 네 장을 정리한 뒤 Higgsfield/Meshy `multi_image_to_3d` 한 작업에서 mesh, bind skeleton, skin weights, PBR과 action `613 Casual_Walk_inplace`를 함께 생성한다. 같은 패키지를 Unity까지 보존하고 `poseStrength=1`로 직접 재생한다. 다른 리그/클립 혼합, 절차 보행, 전신 pose 약화, limb weight 일괄 삭제를 금지한다.

이유: Father V19에서 한 패키지 직접 재생만이 세 번째 다리, 찢어진 옷, 흐물거리는 팔·손과 인형 보행의 반복을 끝냈다. 생성 직후 raw 확대 검수와 전체 actual-map GIF 검수는 수치 PASS보다 우선한다. 정확한 옵션·가변값·gate는 `FAMILY_3D_WORKSTATION_CHARACTER_REUSE_CONTRACT_2026-08-28.md` 하나가 소유한다.

## 2026-08-28 / 3D workstation은 semantic grid와 실제 screen-front가 소유한다

결정: 책상은 실제 integer origin, footprint, blocking, 네 corner와 mapped grid basis를 사용한다. work socket은 keyboard 좌우 seed로만 쓰고, keyboard는 operator-front row, CRT는 별도 back row, chair/actor는 실제 screen-front seat를 공유한다. 모든 수치는 StandingHeight 비례 공통 gate로 판정한다.

결정: production 2D furniture는 삭제하거나 이동하지 않고 QA에서 `forceRenderingOff`만 사용한다. occupied-chair lower-body renderer가 seat claim 뒤 늦게 생성되므로 occupancy 동안 매 frame mask를 갱신한다. 3D chair 색은 equal-channel neutral graphite/charcoal이 소유하며 mug material과 분리한다.

검증: V27은 132 captures 전체에서 초록 chair crop이 사라졌고 actor/keyboard/CRT/chair facing error가 모두 0 degrees다. `productionMutation=false`, `productionEligible=false`; 사용자 actual GIF 승인 전 승격하지 않는다.

## 2026-08-28 / current-state 문서에는 현재 후보와 재사용 계약만 둔다

결정: PROJECT_STATE, continuation guide, experimental README와 current asset/decision sections에는 V29 경로와 공통 재현 계약만 둔다. 폐기 버전별 장문을 새 current section으로 반복하지 않는다. 재발 방지 교훈은 재사용 계약의 compact failure table 한 곳에만 유지한다.
## 2026-08-21 / 6포즈는 key pose로만 유지하고 격리 V3는 24단계를 소비한다

결정: KShopGo/Mixamo의 0/4/8/12/16/20 여섯 자세는 owner/contact 계약의 key pose로 보존한다.
화면 후보는 각 key interval을 4등분한 24포즈, 8방향 192장으로 만들고 distance gait가 전부 소비한다.
production `WalkFrameCount=6`, `RequiredFrameCount=48`, `Legacy48`은 바꾸지 않으며 가변 pose consumer와
192장 주입은 QA control에서만 허용한다.

결정: stable upper 한 장 반복은 상체가 멈춰 보인다는 사용자 판정으로 거부한다. V3는 방향별 여섯 상체
key pose를 같은 하체 phase에 결합해 counter-swing을 복구하고 pelvis와 같은 최대 1px sway/bob을 적용한다.
motion은 pelvis 1.0→knee 0.55→ankle 0.2→foot 0.0으로 감쇠한다. 신발은 앞코·뒤꿈치·갑피를 확대하고
minimum shoe material 208px를 보장한다. 사용자가 actual-office Run8 GIF를 승인하기 전에는 승격·배포를 금지한다.

이유: APK의 실제 Walk는 0.8초 30fps 연속 humanoid clip(dense frame count 26)인데, 여섯 PNG를
133.3ms씩 유지하면 root는 흐르고 관절 그림만 점프한다. 여섯 장의 상체 donor를 그대로 교체하면 얼굴과
체형 pop보다 상체 정지가 더 큰 실제 화면 결함으로 확인됐으므로, upper는 원본 여섯 key만 사용하고 lower의
24단계 연결과 몸 전체 weight shift를 결합한다. Windows D3D11 QA V3는 24 east pose와 실제 좌석
route/Work를 PASS했지만 사람 승인은 별도다.

## 2026-08-20 / 주인공 east는 KShopGo·Mixamo locked whole-chain으로 다시 저작한다

결정: 주인공 east 6포즈의 공개 motion 정본은 `ArtSources/PlayerEastMixamoTraceV2/target-joints.json`,
`phase-contract.md`, skeleton guide다. KShopGo 0.8초 24샘플의 0/4/8/12/16/20 위상과 Mixamo physical
left/right owner를 유지하고 승인된 east 격리 root advance `28.852490px/pose`를 결합한다. heel/toe target
최대 world drift `0.295020px`는 motion 계약 PASS다. raw Unity export는 ignored `Artifacts`에서만 재생성한다.

결정: 기존 v3 상체는 phase별로 보존하지만 lower는 P0~P5 모두 locked
`pelvis→hip→knee→ankle→heel/toe` 체인으로 새로 저작한다. lower mirror, screen-side owner, 신발/종아리 조각
이동, V13/ImageGen/LockedArtV2 raster 재사용을 금지한다. 완성 east GIF 사용자 승인 전에는 Assets와
`Legacy48` 기본값을 바꾸지 않는다.

이유: v10~v13의 문제는 swing 높이만이 아니라 owner 역전, 미러된 골반·무릎, 이중 발목, 신발 용접과
duplicate-contact micro-stutter였다. 기존 donor를 더 잘라 붙여서는 상체와 같은 방향의 연속 physical leg를
만들 수 없다. 당시 집 PC 2D 체크포인트 문서는 2026-08-24에 삭제했다.

## 2026-08-20 / east swing 발 6/10/6px 보정 (거부된 조각 합성 연구 기록)

결정: 주인공 east 6포즈의 팔·몸통·교차 보폭은 alternating v4 그대로 둔다. 지지발 최하단은 y=233,
반대 swing 발은 두 반주기 모두 y=227/223/227로 고정한다. 두 운동화는 반전·변형하지 않고 통째로
평행 이동한다. 다른 7방향은 바꾸지 않는다.

이유: support/swing 실루엣만 분리한 v9도 swing 최하단이 support와 0~2px 차이인 포즈가 남아 화면에서
양발 접지로 보였다. 신발 종류가 아니라 최하단 좌표를 수치로 강제해야 접촉면이 실제로 분리된다.

검증: east 6포즈는 support/swing bottom `233/227, 233/223, 233/227`을 두 번 정확히 반복한다.
actual Windows D3D11에서 8방향 loop, closeup 48장, overview 8장, cadence 1.9819~1.9979 steps/s로
`PASS_NON_SHIPPING`을 기록했다.
이 후보는 이후 발목/owner/하체 방향 결함을 확인해 거부했다. 위 locked whole-chain 결정으로 대체한다.

## 2026-08-20 / 보행 위상은 한 타일마다 오른발·왼발 한 주기를 끝낸다

결정: `DefaultMoveSpeed=1.0`, `DefaultStrideLength=0.99380799`를 사용한다. 이 stride는 320×160,
180 PPU 등각 타일의 인접 중심 간 실제 월드 거리다. 6프레임은 한 타일 안에서 0·1·2 오른발 반주기와
3·4·5 왼발 반주기를 정확히 한 번씩 재생한다. 가속 8.0, 실제 변위 방향, 이동 중 무정지 회전은 유지한다.

이유: KShopGo의 `1.5 unit/s × 0.8s = 1.2 unit`을 우리 월드에 직접 대입하면 한 타일 0.99380799보다
주기가 약 20% 길어 타일 경계마다 발 위상이 밀린다. 화면에서는 오른발 동작이 다음 타일까지 이어진 뒤
왼발 묶음이 나와 `오른발 두 번→왼발 두 번`처럼 읽힌다. 외부 게임의 world unit은 우리 타일 단위와
동일하다고 가정할 수 없다.

검증: alternating v4 조립 뒤 actual Windows D3D11에서 8방향 cycle distance
0.992947~1.008929, cadence 1.9823~2.0142 steps/s, 48 closeup과 8 overview를 기록했다. P0~P2는 승인
그림 그대로이고 P3~P5 하체는 각 대응 프레임의 정확한 골반축 반사다. 사람 화면 승인은 통과했지만
support-foot/contact-step 수치가 아직 미측정이므로 shipping 승격은 보류한다.

## 2026-08-20 / 게임 주인공은 2D 스프라이트로 확정하고 Mixamo는 동작 참고로만 쓴다

결정: 사무실 런타임의 주인공은 8방향×6포즈 단일 2D 스프라이트다. 실시간 3D 모델, Mixamo FBX,
SkinnedMeshRenderer와 Animator를 플레이어 표현에 포함하지 않는다. Mixamo `Unarmed Walk Forward`와
KShopGo는 0.8초 주기, 좌우 팔다리 교차, 낮은 통과발과 연속 방향 전환의 참고 자료로만 사용한다.

이유: primitive volume을 Mixamo 뼈에 붙인 Humanoid bake는 실제 화면에서 주인공처럼 보이지 않았고,
3D 인형 인상과 과한 상하 바운스가 2D 사무실·가족 에셋과 충돌했다. 반면 빨간 캡·흰 후드·줄무늬 셔츠를
직접 그린 2D v3 후보는 동일한 이동값에서 정체성과 평면 합성을 유지한다.

역사 검증 상태: 당시 Windows D3D11 actual player에서 8방향 loop, 48 closeup, 8 overview와
2.4550~2.4879 steps/s를 확인했지만, 이후 project tile stride 결합과 하체 방향 결함 때문에 이 후보를
거부했다. 현행 값은 위 한 타일 결정과 locked trace를 따르며 `Legacy48` 기본값을 바꾸지 않는다.

## 2026-08-19 / 주인공은 8단계 발 교대와 0.18초 planted cardinal turn을 쓴다

결정: 주인공 moving presentation은 source-exact contact A/B 사이를 각 반주기 toe/pass/land로 나눠
거리 phase 0.000/0.125/0.250/0.375/0.500/0.625/0.750/0.875에 배치한다. 각 pose는 거리 12.5%만
소유한다. 하체 좌우를 동/서는 최대 각 18px, 남/북은 최대 각 12px 안쪽으로 평행 이동하고 passing에서
왼발/오른발을 교대로 12px(동/서) 또는 9px(남/북) 든다. scale은 1.0이며
축소·보간·재표본화하지 않는다. 코너에서는 logical root를 0.18초 멈추고 이전/중간 cardinal/목표 contact를
순서대로 표시한다.

이유: 두 contact만 반 타일씩 유지하면 다리는 교대하지만 중간에 굳어 미끄러지는 인상이 남는다. legacy
passing 전신을 그대로 섞으면 얼굴·몸통 폭이 줄어 큰–작은 scale pop이 재발한다. 상체를 contact로 잠그고
두 하체를 원래 굵기 그대로 이동하면 정체성과 크기를 보존하면서도 발이 완전히 합쳐져 가늘어지는 것을
막을 수 있다. V2처럼 양발을 계속 접지하면 스침 자세가 단순한 짧은 보폭으로 읽히고, V3처럼 한 bitmap을
거리 30% 동안 유지하면 발 그림이 고정된 채 root가 0.3타일 흘러 미끄러진다. toe/pass/land 분리는 한
bitmap이 끌리는 최대 거리를 12.5%로 줄인다.
0.06초 즉시 방향 교체도 회전 동작으로 읽히지 않으므로 주인공에게만 짧은 planted turn을 둔다.

검증: ImageGen 후보 3회와 legacy 전신 혼합 1회, 다리가 얇아진 legacy 하체 corridor V1은 화면 검사에서
폐기했다. 양발 접지 wide-passing V2와 거리 30% 고정 readable-transfer V3도 대체했다. eight-pose V4는
Unity 6000.3.21f1 Windows Player D3D11 577 moving frame/69 capture, 실제
인접 타일 8구간 최대 이탈 `0.00000053 world`, endpoint/visual-root/final-center 오차 0,
collision/sprite violation/build warning 0이다.
사람 최종 화면 승인 전에는 다른 가족으로 확대하지 않는다.

## 2026-08-19 / source-exact contact는 불투명 신장으로 legacy 전환 프레임과 정규화한다

결정: 주인공 contact crop의 불투명 발바닥을 bottom-center pivot에 직접 붙이고 bottom padding은 0으로
고정한다. 동/서는 PPU 314, 남/북은 PPU 324를 사용한다. Point, mipmap off, uncompressed와 source-exact
픽셀 계약은 유지한다.

이유: source-exact crop을 프로젝트 기본값 PPU 180으로 가져온 최초 실제 게임 GIF는 legacy 256px 전환
프레임보다 보이는 캐릭터가 약 1.75배 컸다. 4px 아래 여백 때문에 발이 떠 보였고, 코너에서 legacy turn으로
넘어갈 때 작아졌다 다시 커졌다. 경로 좌표 PASS만으로 시각 PASS를 선언한 것도 잘못이었다.

검증: Unity 6000.3.21f1 Windows Player D3D11 실제 사무실 8개 인접 타일 loop 581 moving frame/68 capture,
center-segment 최대 이탈 `0.00000080 world`, endpoint/visual-root/final-center 오차 0,
collision projection/moving sprite violation/build warning 0. 최종 사람 화면 승인은 별도 대기한다.

## 2026-08-19 / 4×2 정본에 없는 대각선은 생성하지 않고 4방향 RPG visual mapping을 쓴다

결정: Player north/west는 정본 시트의 각 열 두 포즈를 source-exact로 게시한다. source에 없는 네 대각선은
새 전신을 만들지 않고 southwest/northwest→west, northeast/southeast→east로 매핑한다. mapping은 moving에만
활성화하며 idle/착석/작업/퇴장은 기존 renderer 소유권을 유지한다.

이유: 대각선 전신을 ImageGen이나 삭제된 legacy pipeline으로 다시 만들면 승인된 캐릭터 비율과 관절 연결을
또 잃는다. 4방향 이동 아트를 8방향 입력에 매핑하는 방식은 source가 실제로 제공하는 네 시점만 사용하면서
주인공의 깨진 legacy diagonal walk를 화면에서 제거한다.

검증: north/west extraction 2회 SHA 일치, generated/interpolated pixel 0, Editor D3D11 36 phase PASS,
Windows Player Direct3D11 36 phase PASS, Intel Graphics, build warning 0. 최종 통합 뒤 cardinal 24 PNG는
직전 승인/후보와 바이트 일치 PASS. 최초 PPU 180 실제 게임 캡처는 시각 실패했고, PPU 314/324 정규화 뒤
실제 새 게임 `OfficeRuntimeAgent`의 인접 타일 8구간 loop는 최대 center-segment 이탈
`0.00000080 world`, endpoint/visual-root/final-center 오차 0, collision/sprite violation 0으로 PASS했다.
north/west 및 diagonal mapping은 사용자 최종 화면 승인 대기다.

## 2026-08-19 / Player south도 승인 east와 같은 source-exact 두 접촉 방식으로만 후보화한다

결정: Player south는 정본 4×2 시트의 south 열 두 포즈를 새 픽셀·보간 없이 crop하고 bottom-center로
정렬한다. 별도 `PlayerSouthContactPresenter`가 south+moving일 때만 기존 `GaitPhase01`에 연결하며 east
승인 파일과 presenter는 수정하지 않는다. 2026-08-19 사용자 화면 승인 뒤 south를 정본으로 승격했다.

이유: 앞 방향 원본 두 포즈가 목·몸통·골반·다리 연결과 주인공 정체성을 이미 보존하고 있다. east와 같은
작은 계약을 방향별로 독립 유지하면 실패한 자동 전신/파츠 생성이 승인본을 다시 오염시키지 않는다.

검증: 추출 2회 frame SHA-256 일치, generated/interpolated pixel 0, Unity 6000.3.21f1 Editor D3D11 6 phase
PASS, Windows Player Direct3D11 6 phase PASS, Intel Graphics, build warning 0. 같은 통합 Player에서 east를
다시 캡처해 승인 당시 6 PNG와 SHA-256 바이트 일치 PASS. south 사용자 화면 승인 PASS.

## 2026-08-19 / 승인 layered art 없이는 cutout을 fail closed하고 Player east 원본 접촉 포즈를 쓴다

결정: 기존 가족 보행 source/writer/gate와 화면 실패한 자동 rigid-part 후보를 제거한다. 주인공 east는
정본 4×2 시트의 두 east 접촉 포즈를 새 픽셀 없이 직접 게시하고 기존 `GaitPhase01`의 0.0/0.5에 연결한다.
다른 방향과 착석/작업은 기존 renderer fallback을 유지한다.

이유: 자동 rigid 파츠 후보는 IK 수치가 정상이어도 팔·다리 비율과 관절 실루엣이 원본과 달라졌다. 반면
정본 접촉 포즈는 목·몸통·다리 연결과 캐릭터 정체성이 이미 승인된 픽셀이다. 부드러운 cutout 확장은
사람이 12~18개 layer PSB를 승인한 뒤에만 다시 시도하며, Player east D3D11 사람 승인 전에는 확장하지 않는다.

검증: Unity 6000.3.21f1 Editor D3D11 6 phase PASS, Windows Player Direct3D11 6 phase PASS,
Intel Graphics에서 Editor/Player 동일 픽셀, build warning 0, 2026-08-19 사용자 화면 승인 PASS.

## 2026-08-19 / 패널을 교체할 때는 detach 후 Destroy한다

결정: `ClearChildren`은 자식을 `Destroy`하기 전에 `SetParent(null, false)`로 먼저 떼어 낸다.

이유: `Destroy`는 프레임 끝에 실행되므로, 같은 프레임에 새 패널을 만들면 나가는 패널이 아직 레이아웃에
남아 두 텍스트가 겹쳐 렌더된다. 인사 화면에서 다른 가족을 고르면 이전 이름과 새 이름이 한 줄에 겹쳐
찍히던 원인이다. detach는 즉시 반영되므로 겹침이 사라진다.

## 2026-08-19 / IMGUI 화면은 offscreen camera 캡처로 검증하지 않는다

결정: 사무실 관리와 주식시장처럼 `OnGUI`로 그리는 화면은 `MainNavigationHudPlayerCapture`의
`RequestFullFrameCapture`(= `ScreenCapture.CaptureScreenshot`)로 캡처한다.

이유: 기존 `CaptureOffscreen`은 카메라를 RenderTexture에 렌더하는데, IMGUI는 그 경로에 들어가지 않는다.
그래서 두 화면의 QA 캡처가 계속 빈 사무실로만 나왔고, 실제로는 아무도 그 화면의 렌더 결과를 확인하지
못한 채 PASS가 기록되고 있었다. 변경 전 빌드로 다시 찍어 같은 빈 화면이 나오는 것을 확인했다.

## 2026-08-19 / 사무실 관리 스킨은 생성 아트에서 뽑은 팔레트를 쓴다

결정: `OfficeLayoutEditModeSkin`의 색은 `MainNavigationV2` 프레임에서 샘플링한 크림 `#FCF0D8`,
골드 `#E49C3C`, 딥틸 `#245454`, 코럴 `#F07854`을 쓰고, 모든 판은 골드 테두리와 그 안쪽 밝은 선을 가진다.

이유: 기존 스킨은 주황 헤더와 민트 버튼이라 같은 게임의 다른 화면과 색·질감이 전혀 맞지 않았다.
IMGUI라 생성 PNG를 9-slice로 그대로 쓸 수는 없으므로(모서리를 대상 크기에 맞게 줄일 수 없다), 팔레트와
테두리 규칙만 아트에서 가져와 절차적 텍스처로 만든다. 텍스처는 border가 기대하는 크기로 정확히 생성해
스케일 문제가 생기지 않게 한다.

## 2026-08-19 / 프레임 아트는 rect에 맞춰 눌러 담지 않고 자기 높이 기준으로 축소해 그린다

결정: sliced `Image`의 9-slice border는 `UiNineSliceFitter`가 `pixelsPerUnitMultiplier`로 스케일한다.
멀티플라이어는 스프라이트 높이를 rect 높이에 맞추는 값을 기본으로 하고, 그 값이 어느 축에서든 늘어나는
중앙부를 10% 미만으로 만들면 그때만 더 키운다. 1 미만으로는 내리지 않는다.

이유: `Image.GetAdjustedBorders`는 양쪽 border 합이 rect보다 크면 border를 rect 크기에 맞춰 줄인다.
중앙 stretch 영역이 0이 되므로 둥근 프레임이 양 끝 cap만 남아 캡슐이나 동그라미로 렌더된다.
MainNavigationV2 프레임은 런타임 크기의 2~8배로 그려져 있어 배지·탭·카드·리본이 모두 이 경로에 걸렸고,
상단 배지는 타원, 하단 탭은 동그라미, 카드 리본은 읽을 수 없는 덩어리가 되어 있었다. 높이 기준 균일
스케일을 쓰면 border가 품고 있는 원형 medallion과 모서리 장식이 원래 비율을 유지하고 평평한 중앙만 늘어난다.

결정: 배지 위에 얹는 아이콘·글자는 절대 픽셀이 아니라 배지 높이의 비율로 배치한다. 아트에서 잰
스프라이트 좌표를 스프라이트 높이로 나눈 값을 쓴다.

이유: 위 규칙에서 스프라이트 1픽셀은 항상 `배지높이 / 스프라이트높이` 캔버스 픽셀로 그려지므로, 비율로
적어 두면 해상도가 바뀌어도 아이콘이 medallion 안에, 글자가 plaque 위에 그대로 정렬된다.

결정: IMGUI 타이틀 화면은 `GUIStyle.border` 9-slice를 쓰지 않는다. 아트의 실제 불투명 영역을 UV로 지정해
`GUI.DrawTextureWithTexCoords`로 그리고, 슬롯 카드처럼 rect와 아트의 종횡비가 크게 다르면
`UiRemasterTitleArt.DrawSliced`의 스케일 9-slice를 쓴다.

이유: IMGUI는 border를 소스와 대상 양쪽에 같은 픽셀값으로 쓰기 때문에 대상 크기에 맞춰 모서리를 줄일 수
없다. 타이틀 시트는 프레임보다 약 2배 큰 투명 여백 위에 그려져 있어서, 100px 버튼에 맞는 작은 border는
여백 안쪽을 잘라 모서리 장식을 중앙으로 늘려 버렸고(불꽃 모양 번짐), 여백째 늘리면 프레임이 절반 높이로
나왔다. 측정한 content window를 직접 지정하면 두 문제가 같이 없어진다.

결정: 아트에서 잰 window·border 값은 픽셀이 아니라 텍스처 대비 비율로 저장한다.

이유: 임포터가 이 시트들을 `maxTextureSize 2048`로 줄이므로 런타임 텍스처 크기가 원본 PNG와 다르다.
픽셀로 저장하면 크기 검사에 걸려 조용히 fallback으로 떨어진다.

## 2026-08-18 / 방향은 row 이름이 아니라 머리부터 양쪽 신발 앞코까지의 해부학 계약이다

결정: `east/west/...` 파일명, catalog index, actor displacement, 얼굴 방향 중 하나만 맞아도 방향 PASS로
보지 않는다. 각 프레임에서 시선/코, 흉곽, 골반, 양 무릎·발목, **두 신발의 뒤축→앞코 축**이 같은 화면
heading이어야 한다. 뒤에 있는 발도 위치만 뒤일 뿐 앞코는 진행 방향을 유지해야 하며, 정면으로 벌어지거나
반대쪽을 향하면 발 excursion과 cadence가 정상이어도 실패다.

결정: P0→P1→P2와 P3→P4→P5는 단지 다른 실루엣이면 되는 것이 아니라 support-leg ownership이 바뀌는
두 반주기다. P3/P4/P5에서 같은 화면측 다리를 다시 swing하거나 P0/P1/P2를 색·팔만 바꿔 반복하면
실패다. contact A/B의 앞/뒤 다리 occlusion과 반대 arm swing도 함께 뒤집혀야 한다.

결정: 생성 원본을 `full-body`라고 부르는 것 자체는 coherence 증거가 아니다. ImageGen/기존 donor 모두
한 프레임 안에서 머리와 발을 다른 카메라 방향으로 그릴 수 있다. 방향별 source manifest에는 사람 눈으로
승인한 `head/torso/pelvis/knees/shoeToes` 의미와 정확 frame hash를 남기고, runtime은 그 hash에서만
결정론적으로 파생한다. 새 시안은 모자·seam·motion gate뿐 아니라 확대된 전신/발 contact sheet를 거친다.

결정: 실제 결함이 확인된 엄마 east/west는 `BeforeCoherenceV1` donor를 재사용하지 않는다.
`ArtSources/MotherSideWalkV3`의 승인 east 전신 6포즈를 정규화하고 west는 그 프레임의 정확한 수평 반전으로
파생한다. raw/frame SHA, 모든 신발 앞코 방향, support alternation manifest가 일치하지 않으면 생성기가
즉시 실패한다. 이 예외는 직원이나 다른 가족 행을 암묵적으로 다시 그릴 권한이 아니다.

결정: QA 캡처 자체도 전신을 잘라서는 안 된다. 이전 1.15 orthographic closeup은 원화가 정상이어도
플레이어 모자와 누나 머리카락을 화면 밖으로 잘랐다. 캡처에는 머리 위 여백과 양발 전체가 포함돼야 하며,
원화 clipping과 카메라 framing clipping을 별도로 판정한다.

이유: commit `befe937`은 허리 절단, 모자 asset clipping, 발 이동량과 Player cadence는 고쳤지만 엄마 east
프레임에서 머리·몸통이 옆을 보는 동안 발이 정면/반대쪽을 향하는 것을 놓쳤다. 방향 semantic을 독립
계약으로 두지 않으면 수치가 모두 PASS해도 사람에게 즉시 틀려 보인다.

## 2026-08-18 / contact pose가 아니라 projected support-foot anchor가 skating 판정을 소유한다

결정: `split_high_motion_sheets.extract_aligned_frames()`처럼 매 프레임 upper-body median을 x=128로
재센터링하는 출력을 보행 정본으로 승인하지 않는다. 이 정렬은 실루엣과 pivot을 안정시키지만 접지발이
root 진행을 상쇄해야 하는 local translation을 지운다. 신규 baker는 pelvis/root와 좌우 foot anchor를
위상별로 명시하고 P0~P2/P3~P5의 같은 support foot projected 위치를 추적해야 한다.

결정: stride `0.99380799`, PPU 180, visual scale 1.55의 현행 결합에서는 source frame당 root가 진행축으로
19.235px, 한 걸음은 57.705px에 해당한다. 지지발 local anchor가 반대로 같은 양을 이동하지 않거나 contact
stride가 이 값과 맞지 않으면 실패다. 임계값 `foot excursion >=1px`, 두 발 cluster 존재, cycle world
distance, cadence, world-step/body-height는 foot lock 증거가 아니다.

이유: push 전 commit `6ae4041`의 실제 Player를 다시 보자 일부 방향이 한 발로 미끄러졌다. 계산 결과 실제
인접 발 excursion은 1.011~6.471px뿐이고, 프레임마다 두 군집 중 가장 유리한 발을 골라도 32/32루프의
반주기 support drift가 26.260~40.138px였다. 방향이 교정된 엄마 east/west도 26.260px라 승인할 수 없다.

## 2026-08-18 / 가족 4명은 full-body authored pose가 소유하고 허리 splice를 금지한다

결정: 현재 shipping/gate 범위는 가족 4명, 32루프, 192 PNG다. 직원 후보 8명은 가족 4명의 실제 D3D11
Player 시각 승인이 끝난 뒤 같은 계약을 확장하는 별도 작업으로 둔다. 아래의 “전체 12명 cutout” 결정은
실제 허리 절단과 머리 clipping을 놓쳤으므로 현재 권한이 없다.

결정: `BeforeCoherenceV1`의 승인 6포즈를 머리부터 발까지 온몸 motion authority로 사용한다. lower-body를
지우고 다시 채우는 cutout, 허리선 합성, 직사각형 pelvis cap을 금지한다. 플레이어와 엄마만 결함 전
revision `9144fa0e`에서 독립 보존한 방향별 identity anchor의 머리 영역을 10px underlap으로 겹친다.
누나와 아빠는 donor 전신 자체가 authority다. 생성된 runtime P0를 다음 생성 입력으로 사용하지 않는다.

결정: 발 아래 분리된 1~3px shadow island는 ground normalize 전에 제거한다. 가짜 최저점을 먼저 맞춰
몸 전체가 위로 이동하고 모자·머리카락이 canvas 밖으로 잘리는 순서 오류를 fail-closed한다.

결정: 발의 contact/excursion/lift/support/cadence gate에 full donor 대비 허리 band mismatch ≤1%, 머리
실루엣 IoU ≥0.78, top margin ≥4px, profile identity-head mismatch 0을 추가한다. 발이 움직여도 상체가
byte-identical인 후보, 허리에 4px 투명 절단이 있는 후보, 모자를 같은 높이로 자른 후보는 음성 대조군에서
반드시 실패해야 한다. 수치 PASS 뒤 실제 D3D11 Player closeup을 사람이 확대 확인해야 승인한다.

이유: 이전 게이트는 발 운동과 identity를 서로 떨어진 구역으로만 검사해 두 영역 사이의 seam을 보지
않았고, detached shadow를 실제 발바닥으로 오인해 정렬하면서 모자까지 잘랐다. 온몸 pose authority와
seam/head 회귀 검사는 사용자가 실제 GIF에서 본 결함을 직접 금지한다.

## 2026-08-18 / 전체 12명 보행은 공용 cutout 규칙, 실제 발 궤적, Player cadence가 소유한다

이 결정은 위 가족 4명 full-body 결정으로 대체된 실패 이력이다. 현재 shipping 권한이 없다.

결정: shipping walk는 `FC-CHARACTER-LOCOMOTION-GENERATION-V1` 하나가 소유한다. 현재 catalog의
가족 4명과 직원 후보 8명 모두 같은 8방향 좌표계와 contact/support/pass/contact/support/pass 위상 곡선을
사용한다. 캐릭터 프로필에는 하체 시작 비율과 발 corridor margin만 두며, 캐릭터·방향·프레임별 수작업
예외를 두지 않는다. 현재 P0는 identity authority, 보존된 BeforeCoherence 접촉 A/B는 하체 기하 donor다.

결정: 얼굴·머리·복장 픽셀은 생성 모델로 다시 그리지 않는다. 다만 정체성 보존을 상체 동작 정지와 같은
뜻으로 취급하지 않는다. P1/P4 support/down에는 승인 상체를 변형 없이 1px 내리는 공용 강체 하중 이동을
넣고, 정렬 뒤 identity 변화 1.5% 이하와 강체 이동 0.5~1.25px를 함께 검사한다. 6위상 상체가
byte-identical이면 발이 움직여도 실패다.

결정: 하체 donor는 발 band까지 연결된 불투명 component만 leg layer가 될 수 있다. hip 전환대에서는
방향별 P0 identity silhouette을 2px 확장한 범위 밖의 donor 픽셀을 contact·passing 모두 거부한다.
늘어진 손·소매·머리카락이 seam 아래에 있어도 보행 limb로 복제하지 않으며 이 조건은 생성 중 단언한다.

결정: PASS는 실제 PNG의 두 발 간격·접촉 교대·dense-flow excursion·인접 실루엣 변화·바닥 이탈·수직
들림·support 바닥 픽셀을 모두 만족해야 한다. 그 다음 Unity 576 Sprite/catalog 순서와 broad 이동을
검사하고, 최종 D3D11 Player에서 12명×8방향의 실제 actor 변위, sprite 이름, 방향, flip, cycle world
distance, cadence와 body-height 대비 world step을 검사한다. 구 `footDrift=0`, loop closure, 상체 identity
하나만으로는 릴리스할 수 없다.

결정: `build_family_walk_half_cycles_v2.py`와 marker 소스는 제작 이력/해부학 증거로만 보존한다. 그
`--write`는 차단하고 `--check`는 V1 gate에 위임한다. 2D Animation 패키지나 런타임 skeletal 시스템을
추가하지 않고, 공용 cutout 생성 결과를 현재 256×256 PNG 소비 형식으로 bake해 기존 런타임·GUID를
보존한다.

이유: 변경 전 strict coherence는 발 excursion 0에 가까운 루프도 통과시켰고, 외부 보행 리뷰가 지적한
상체 exact-byte lock은 정체성을 지키는 대신 체중 이동까지 막았다. Mad Games Tycoon 2의 30fps 실화면과
Big Biz Tycoon의 책상 동선은 작은 화면에서도 전신 방향과 지지/스윙 실루엣이 읽혀야 한다는 판독 기준을
확인시켰다. 공용 규칙→PNG bake는 현재 2D 투자와 runtime을 지키면서 같은 실패를 576장에 반복하지 않는
가장 작은 구조 변경이다.

## 2026-08-18 / 가족 보행은 identity-locked body, 분리 다리, 별도 marker copy가 소유한다

이 결정은 Generation V1 이전 가족 전용 제작 이력이며 shipping writer 권한은 위 결정이 대체한다.

결정: 가족 보행은 방향별 canonical identity/body anchor를 먼저 고정하고, 프레임마다 전신을 새로 생성하지
않는다. 첫 반주기 0·1·2의 해부학적으로 분리된 두 다리를 결정론적으로 움직이고, 3·4·5 하체는 골반축
반사로 만든다. 머리·얼굴·의상·카메라 방향은 반사하지 않는다. 방향쌍까지 파생하므로 생성 예산은 가족당
5방향×3패널=15패널이다.

결정: 청록/자홍 좌우 다리 표식은 출하 픽셀에 칠하지 않는다. 표식 없는 shipping source를 먼저 만들고,
동일 alpha 실루엣의 별도 marker review copy에만 표식을 칠한다. `IdentityModelV1`, shipping source,
`MarkerReviewV1`을 함께 추적하며 0/3 접지발 색과 2/5 통과발 색 교대를 증명한다.

결정: `build_family_walk_half_cycles_v2.py`는 추적 source를 runtime frame/sheet로 publish하는 한 경로만
가진다. 구형 V4/V5/V6/V7/raw import CLI 세대는 제거하고 `--write` 앞에 two-step/marker 32행 gate를
강제한다. `FAIL이면 사람 눈으로 뒤집을 수 없다. PASS해도 필요조건일 뿐 충분조건이 아니다.`

결정: 정상 가족 actor는 별도 `LocomotionTransitionsV1` 캐릭터 그림을 사용하지 않는다. 시작·정지·pivot은
승인 walk/idle 계열만 사용해 다른 세대 모자·바지·몸통이 순간 교체되는 것을 막는다. 최종 승인은 정적
수치가 아니라 실제 normal 새 게임 30fps 영상과 1×/2×/4× 이동 불변조건으로 닫는다.

이유: 독립 192장 생성은 같은 사람·키·팔레트를 유지하지 못했고, 래스터 봉합선·바지 폭·신발 간격·보폭을
따로 늘리는 시도는 변수가 결합돼 수렴하지 않았다. probe 53은 구조 gate는 통과했지만 출하본에 칠한
표식의 어두운 외곽이 남았다. candidate 61의 identity-lock과 marker-copy 분리는 초상화 교체와 표식 잔류
실패가 생기는 제작 경로 자체를 제거한다.

## 2026-08-17 / 빈 사무실 생산 자율과 실제 구매 클릭을 릴리스 gate로 둔다

결정: 빈 사무실 가족의 생산 선택 권한은 `OfficeAutonomyCoordinator` 하나다. `OfficeInteractionScoring`의
Shadow 결과는 비교 진단일 뿐 fallback 목적지나 actor state를 바꾸지 않는다. 가구가 없으면 coordinator가
현재 셀과 다른 도달 가능한 타일 중심을 선택하고, cardinal path의 다음 segment 방향으로 planted pivot을
완료한 뒤 translation한다. 같은 셀 `current-look` 재선택, destination 없는 장기 Idle, 중복 pivot,
direction-displacement 불일치는 릴리스 실패다.

결정: 걷기 여섯 포즈가 이미 발·골반·어깨의 상하 움직임을 포함하므로 `VisualRoot`에 별도 foot-plant
위치 보정을 더하지 않는다. 충돌·점유를 소유하는 논리 root와 표현 root를 항상 같은 위치에 두고, 보행 위상은
실제 이동거리로만 진행한다. 정지/look 입력의 방향 hysteresis는 유지하되 실제 translation 프레임은 그 프레임의
화면 변위와 가장 가까운 방향을 즉시 사용한다.

결정: 사무실 관리 구매는 green preview 표시만으로 완료 판정하지 않는다. 실제 Windows Player의 첫 유효
좌클릭이 `ConfirmPreview()`에 정확히 한 번 도달하고 ledger·inventory·OfficeGrid·runtime apply가 원자로
갱신돼야 한다. preview hover 갱신은 같은 frame의 confirm 입력을 조기 return으로 소비하지 않는다.

이유: `b397af9`의 empty-office observer는 입장/퇴실과 진행 중 전환 정지만 보아 stationary 48 대 walk loop
13의 제자리 행동을 PASS했고, editor/static purchase 검사는 실제 pointer 분기를 지나지 않아 녹색 preview 뒤
클릭 무효를 놓쳤다. 사용자 입력과 생산 coordinator를 직접 지나지 않는 QA는 이 두 회귀의 최종 증거가 아니다.
사용자 녹화의 반복 점프는 foot-plant 보정이 일정한 논리 이동 위에서 표현 root 속도를 주기적으로 0~1.5배로
바꾼 결과였으므로 원화나 좌석을 바꾸지 않고 중복된 전신 보정만 제거한다.

## 2026-08-17 / 기능 작업트리는 warm integration 하나만 유지한다

결정: 필수 Git 기준 작업트리 외 기능 작업은 `fc_agents/integration_p0` 하나에서 순차 진행한다. 옛 작업트리
34개와 파생 `Library/Builds/Artifacts`는 제거하며, 미커밋 변경이 있던 7개만
`cleanup-salvage-20260817-*` stash로 복구 가능하게 보존한다. 새 기능 작업은 이 stash를 자동 참조하지 않는다.

이유: 중복 Unity Library와 실행 증거가 약 85GB를 차지했고, 여러 checkout이 서로 다른 과거 구현을 현재
정본처럼 참조하게 만들었다. 한 warm Library를 유지하면 빌드 시간과 코드 정본을 동시에 안정화한다.

## 2026-08-17 / 새 게임은 빈 타일 사무실이며 모든 가구는 타일 중심에 배치한다

결정: 실제 새 게임은 13×13 바닥과 외곽 bay 52개만 가진 `CreateNewGameEmptyOfficeV1()`로 시작한다.
플레이어 편집 가구·좌석·워크스테이션·가구 재고는 0개다. furnished `CreateStarterOfficeV1()`은 기존 저장
호환과 출근·좌석 회귀 fixture로만 유지하며 FAST_QA는 실제 빈 시작을 먼저 단언한 뒤 fixture를 설치한다.

결정: 회사 허브의 기존 build-editor route는 사용자 이름 `사무실 관리`로 노출하고, 카테고리별 구매·보관·
판매·회전·배치를 그대로 사용한다. 배치 원점은 항상 정수 타일이며 1×1은 한 타일 중심, 다중 타일은 회전된
전체 footprint의 정확한 중심을 의미·시각·충돌·저장의 공통 anchor로 사용한다. 포인터 world 좌표와 Sprite별
숨은 보정을 영속화하지 않는다.

이유: 고전 타이쿤처럼 바닥 타일을 사무실 구성의 유일한 기준으로 삼아야 빈 공간에서 가구를 사서 꾸미는
성장감과 이동·충돌·렌더 정합성이 함께 유지된다. furnished fixture를 일반 새 게임으로 위장하지 않으면
FAST_QA가 일정·route·docking을 강제해 실제 시작 상태의 회귀를 가리는 문제도 막을 수 있다.

## 2026-08-17 / 가족 32개 방향 행은 반대 발·팔 실루엣으로 판정한다

결정: 엄마 북쪽에 적용한 반 주기 제작법을 가족 4명×8방향 전체에 확장한다. contact/recoil/passing 0·1·2를
정본으로 두고 3·4·5는 방향을 보존하는 정확한 좌우 반전으로 만들며, 0↔3 전체 실루엣 변화율 30% 미만,
중복 프레임, 발 하단 절단, runtime sheet/frame 불일치를 release failure로 본다. 엄마는 치마 밑단, 전 캐릭터는
반대 발과 반대 팔이 함께 교차해야 한다.

이유: 파일 6개와 hash 차이만으로는 정지한 상체·끌리는 발·잘린 신발을 잡지 못한다. 32개 행을 동일한
결정론 builder와 정량 gate로 관리하면 출근에 자주 쓰이는 대각선부터 모든 방향이 같은 걷기 계약을 가진다.

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

## 2026-08-18 / 가족 보행은 전신 생성·독립 재센터링 대신 marker-owned 2D part rig로 bake한다

결정: 가족 4명의 출하 보행은 `FC-FAMILY-LOCOMOTION-RIG-V1` 공용 리그가 소유한다. ImageGen은
방향별 좌/우 허벅지·종아리/발 분리 파츠만 제공하고, 6위상의 foot control, anatomical support ownership,
root stride 결합, 반대 방향 mirror와 final PNG bake는 결정론적 코드가 담당한다. 직원 8명은 가족 4명의
실제 D3D11 Player 시각 승인 전에는 변환하지 않는다.

이유: 기존 `extract_aligned_frames()`의 phase별 upper-body median recenter가 접지발의 local counter-motion을
삭제했다. stride 0.99380799, PPU 180, scale 1.55에서는 phase당 19.234993px 역이동이 필요하지만 구 PNG는
1.011~6.471px만 움직였고 가족 32/32루프의 best-case 반주기 drift가 26.260~40.138px였다. 기존 1px
excursion/ground/upper identity gate와 cadence-only Player QA는 이 미끄러짐을 통과시켰다.

결정: P0~P2는 left, P3~P5는 right support로 명시하고 출하와 alpha가 같은 anatomy marker에서 발 anchor를
측정한다. `supportWorldPx[p] = localAnchor[p] + p × 19.234993 × direction`의 진행축 drift를 1px 이하,
P0→P3 contact step을 57.704980±1px, swing world travel을 80px 이상, passing lift를 2.5px 이상으로
검증한다. 런타임 Resources anchor와 실제 Player SpriteRenderer transform 뒤 좌표도 별도 측정한다.

결정: 방향별 canonical identity upper의 얼굴·모자·의상 픽셀은 변형하지 않는다. garment seam에서 기존
standing leg만 corridor clear하고 generated legs를 같은 hip에 결합한다. P1/P4는 upper와 hip을 함께 1px
아래로 움직여 upper byte freeze와 상·하체 독립 이동을 모두 금지한다. 엄마 front/back stance는 18px로
고정해 불투명 치마 아래 한 발 가림과 과도한 X자/팔자 다리를 함께 피한다.

거부한 대안: 완성 전신 6패널 재생성은 exact anchor/시간 순서를 지키지 못했고, whole-body/marker-leg warp는
치마·다리 왜곡과 seam을 만들었다. 이미 중앙 정렬된 PNG의 pivot lock은 몸을 41.5~73.4px 점프시켰고,
평면 fixed-length IK는 front/back 무릎을 옆으로 던졌다. 전역 VisualRoot smoothstep으로 원화를 숨기는
방식도 runtime 속도 변동을 만들므로 다시 사용하지 않는다.

검증 상태: pure candidate/runtime QA는 32/32루프 PASS, 최대 support drift 0.726448px, contact step
57.669070~57.794742px, swing travel 최소 86.316402px, passing lift 최소 3.234033px, 기존 `.meta`/GUID
diff 0건이다. Unity import batch도 192 Sprite, rootStep 19.2350px, cadence 2.0125로 PASS했고
`FAST_QA_WINDOWS.cmd -Profile editor-broad`는 10.725초 PASS했다. clean build와 배포 D3D11 Player 사람
검토가 끝날 때까지 릴리스 결정은 미완료다.

## 캐릭터 애니메이션 소스는 Mixamo다 (2026-08-20, 위 2D 결정으로 대체됨)

이 절의 Humanoid bake/promotion 결론은 같은 날짜의 문서 맨 위 2D 결정으로 대체됐다. 아래 내용은
반입한 FBX와 당시 검증 상태를 보존하는 역사 기록이다.

결정: **월드 캐릭터의 걷기·대기·달리기·앉기 모션은 우리가 제작하지 않고 Mixamo에서 받는다.**
<https://www.mixamo.com/> (Adobe, 무료, 계정 필요). 사용자 계정으로 로그인되어 있으므로
**추가 클립이 필요하면 즉시 받을 수 있다.** 받은 리그와 클립을 `PlayerWalkHumanoidBaker`에 넣어
8방향 × 8포즈 PNG로 굽고, 게임은 계속 2D 스프라이트로 돈다. 3D는 작업실에만 존재한다.

현재 반입된 것:

| 파일 | 출처 | 설정 |
| --- | --- | --- |
| `PlayerHumanoidBase.fbx` | Mixamo `X Bot` | FBX For Unity, **T-Pose**, 1,750,032바이트 |
| `PlayerHumanoidWalk.fbx` | Mixamo `Unarmed Walk Forward` | FBX For Unity, With Skin, 30fps, Keyframe Reduction none, In Place 해제, 417,392바이트 |

즉시 받을 수 있는 다음 클립(KShopGo가 클립 7개로 전부 처리했다): `Idle`, `Standing Idle`,
`Running`, `Sitting`, `Typing`. 다운로드 설정은 위 워크 클립과 동일하게 하고
당시 Mixamo 2D 인계 표를 따랐다. 해당 퇴역 문서는 2026-08-24에 삭제했다.

이유: 런타임이 요구하는 8방향 × 8포즈를 2D 페이퍼돌로 만들면 방향마다 리그 authoring과 걷기
사이클 손 키잉이 필요하다. 8회다. 걷기 사이클 키잉은 애니메이터의 전문 작업이고, 이것이 며칠간
진도가 나지 않은 원인이었다. 휴머노이드 리그는 클립 1개를 카메라 yaw 45도씩으로 8방향에
재사용한다. 당시 2D rig 비용 비교 문서는 2026-08-24에 삭제했다.

거부한 대안: Synty POLYGON은 KShopGo가 실제로 쓴 팩이지만 상업 유료다
(<https://syntystore.com/collections/polygon>). 무료 대안으로 충분하므로 채택하지 않았다.
Quaternius(CC0, <https://quaternius.com/>)는 계정이 필요 없어 2순위 백업으로 남긴다.
무료 2D 본 워크사이클 라이브러리는 사실상 존재하지 않으므로, 2D를 유지하면서 뼈대만 받아오는
길은 없다.

검증 상태: 파이프라인은 실제로 실행됐다. 클립 보폭 1.40847 유닛 측정, 스케일 0.45522 자동 계산,
왼발 접지 0.2680초 검출, 8방향 렌더와 PNG 기록 통과. 아직 미완이다 —
`ValidateVisibleHeight`(투영 pelvis에서 정수리까지 1%)가 페이퍼돌 전용 불변량이라 4.124%로
실패하고, `ValidateReceiptFootLock`의 등방 투영 가정과 정본 카메라 피치가 미확정이다.
당시 Mixamo 2D 인계 문서에 상세가 있었으나 그 퇴역 문서는 2026-08-24에 삭제했다.

## 참고 자료 폴더 (2026-08-20)

읽기 전용 참고물이다. **수정하지 않는다.** 저장소에 포함하지 않는다.

| 대상 | 경로 | 무엇을 참고했나 |
| --- | --- | --- |
| KShopGo (`com.hclab.kshopgo` 1.15) | `C:\Users\godho\Downloads\com.hclab.kshopgo_1.15\com.hclab.kshopgo.apk` | 퇴역 2D 연구 당시 이동 참고. 상세 분해 문서는 2026-08-24에 삭제했다. |
| simul (Flutter/Dart) | `C:\Users\godho\Documents\Codex\simul` | 주식 호가창 체결 구조. sweep 재생, `orderedFills`, 델타 배지 규칙. 이식은 `Docs/ORDER_BOOK_SWEEP_V1.md` |
| Mixamo | <https://www.mixamo.com/> | 휴머노이드 리그와 걷기 클립의 실제 소스. 위 결정 참고 |

KShopGo는 게임플레이 네임스페이스가 `CryingSnow.FastFoodRush`인 에셋스토어 템플릿 리스킨이며,
사용한 아트는 Synty POLYGON 유료 팩이다. **그 아트를 가져오지 않는다.** 참고한 것은
설정값과 구조다.

## Mixamo 보행 크기·외형·승격 계약 수정 (2026-08-20, 거부된 연구 기록)

이 절은 primitive costume의 실제 화면 거부 전에 작성됐다. production pipeline으로 재실행하지 않는다.

결정: Mixamo `Unarmed Walk Forward`의 root travel은 gait phase를 찾는 데만 쓰고 캐릭터 크기를 정하지
않는다. 주인공은 380px 목표 실루엣으로 한 번 uniform scale하고, runtime stride/8에 대한 support-foot
궤도는 프레임 전체 정수 픽셀 정렬로 결합한다. 애니메이션 샘플 뒤 모델 yaw를 적용해 독립 8방향을 만든다.

결정: Mixamo X Bot은 Avatar/리타기팅 probe로만 남긴다. final 후보는 X Bot Renderer를 숨기고 같은 뼈에
`canonical-protagonist-v1` 닫힌 볼륨을 붙인다. CANON의 빨간 뉴스보이 캡, 흰 후드 윈드브레이커,
줄무늬 셔츠, 남색 바지와 운동화가 방향마다 보존돼야 한다. 자동 분리 2D paper-doll은 final art가 아니다.

결정: Humanoid receipt는 paper-doll 1% 높이 규칙과 분리하되 검증을 약화한 warning 모드는 두지 않는다.
hard alpha/canvas/pelvis/material component, foot lock/contact step/passing lift, 8개 고유 direction row와
pose hash를 모두 통과해야 한다. 후보는 별도 root에 쓰고, 전부 PASS한 뒤에만 promotion 명령이 production
64장과 catalog를 갱신한다. actual normal Windows D3D11 8방향 캡처 전에는 기본 런타임은 Legacy48이다.

이유: 최초 X Bot 출력은 root travel에 맞춘 scale 0.45522 때문에 정본 주인공보다 현저히 작았고,
`SampleAnimationClip`이 사전 yaw를 덮어써 방향별 PNG가 중복됐다. paper-doll의 pelvis-to-crown 1% 규칙을
사람형 spine bob에 그대로 적용한 뒤 `continueOnValidationFailure`로 삼킨 것은 production gate가 아니었다.

검증 상태: 당시 관련 Editor 코드와 PowerShell parser는 통과했지만, 실제 Humanoid 결과는 주인공 외형 불일치와
과한 바운스로 전체 거부됐다. 당시 “실제 D3D11 실행이 남은 gate” 기록도 함께 취소한다. 현행
`Tools/Invoke-PlayerWalkHumanoidPipeline.ps1`과 `PlayerWalkHumanoidPromotion`은 실행·승격 불가이며 2D east
제작의 gate가 아니다.

## KShopGo 기준 연속 회전·0.8초 cadence로 자유 보행을 교체한다 (2026-08-20, 위 한 타일 결정으로 수치 대체)

결정: 자유 보행의 segment 시작, 45°/90° 코너, 135°/180° 반전에 `RequiresStationaryPivot` 정지 gate를
사용하지 않는다. `OfficeRuntimeAgent`는 가속 적분을 계속하고, `DirectionalSpriteAnimator`는 같은 frame의
실제 변위를 즉시 8방향으로 양자화한다. 제자리 `PivotSeconds=0.06`은 막힌 actor와 좌석·업무 상호작용의
최종 facing 정렬에만 남긴다.

결정: 공용 이동은 KShopGo의 직렬화 값을 기준으로 `DefaultMoveSpeed=1.5`,
`DefaultAcceleration=8.0`을 사용한다. 실제 APK Walk는 0.800초/30fps이며 인플레이스이므로 한 주기 stride는
`1.5 × 0.8 = 1.2 world unit`, 두 발 cadence는 2.5 steps/s로 둔다. 짧은 이동도 전체 gait를 진행하며
`ShortShuffleStrideFraction=0`으로 0/3 두 프레임 스터터를 폐기한다.

근거 수정: Animator 52개 중 42개의 `ApplyRootMotion=True`만으로 루트 모션 이동이라고 판단했던 이전 설명은
폐기한다. Walk/Run 포함 모든 휴머노이드 클립은 평균 root 속도 0이고 시작·끝 XZ가 동일하며,
`KeepOriginalPositionXZ=True`다. 52개 모두 feet stabilization/linear velocity blending이 꺼져 있고 상태의
feet IK도 꺼져 있다. 자연스러움의 직접 근거는 24샘플 Walk의 프레임 간 보간, Idle↔Walk 0.25초 전이,
연속 agent/controller 이동과 빠른 회전이었다. 당시 상세 수치 문서는 2026-08-24에 삭제했다.

검증: `simulation-pure` PASS, Unity 6000.3.21f1 Bee Roslyn으로 Simulation,
Presentation.Unity, Editor 세 어셈블리 컴파일 PASS. Mixamo 후보의 actual normal D3D11 베이크·플레이어
캡처 gate는 계속 별도이며 통과 전에는 `Legacy48` 기본값을 바꾸지 않는다.

## Player east lower는 잠긴 owner chain에서 격리 2D 후보로 저작한다 (2026-08-20)

결정: `PlayerEastWalkLockedArtAuthoring`은 tracked `target-joints.json`을 직접 읽고 각 P0~P5의 physical
left/right pelvis→hip→knee→ankle→heel/toe chain을 새로 hard-alpha rasterize한다. phase별 V3 입력에서는
`y=0..176` 상체 픽셀만 그대로 잠그며 기존 lower, 반전 lower, 신발/종아리 조각, raster warp를 쓰지 않는다.
출력은 사용자 GIF 승인 전까지 ignored `Artifacts/PlayerEastMixamoLockedArtV3/`에만 둔다.

이유: 추가 ImageGen edit 2회도 exact 256 좌표와 원본 scale/상체 픽셀을 보존하지 못했다. 첫 결과는 전체
인물을 1024로 다시 그렸고, lower-only 결과는 골반·신발 크기와 후행발 owner/각도가 target에서 벗어났다.
두 결과는 shipping frame에 쓰지 않으며 SHA와 exact prompt는 trace README에 남긴다. 잠긴 좌표를 직접
rasterize하면 owner/contact를 임의 추론하지 않고 상체 동일성을 byte 단위로 증명할 수 있다.

검증: Unity authoring PASS. 독립 `Test-PlayerEastWalkLockedArtV3.ps1`에서 6 poses, upper mismatch 0,
soft alpha 0, missing joint 0, material component 6/6, east shoe 12/12, unique hash 6/6 PASS. normal new-game
13×13 타일 맵의 Windows D3D11 격리 Player에서도 player만 표시, editable furniture 0, PPU 180, scale 1.55,
speed 1.0, stride 0.993808, VisualRoot offset 0, P0~P5 캡처를 PASS했다. 사용자 타일 맵 화면 승인과 Assets
승격은 아직 하지 않았으므로 `Legacy48`이 계속 production 기본값이다.

수정: owner별 다리를 각각 완성된 검정 outline으로 그린 뒤 겹치는 방식은 골반 아래에 내부 contour를 남겨
두 다리가 분리된 조각처럼 보였으므로 거부한다. pelvis와 두 다리의 실제 색 면을 하나의 pants core mask로
합친 뒤 그 mask 바깥에만 1px outline을 만든다. 골반을 가로지르던 긴 highlight도 짧은 사선 명암으로 바꾸고,
교차 포즈의 완전히 둘러싸인 1px concavity는 검정이 아니라 남색 깊이로 닫는다. 독립 검사는 불투명 하체 내부
exact black outline을 포즈당 `<=60px`로 제한하며 수정본 최대값은 `8px`, 별도 pelvis→thigh junction
exact-black 값은 6포즈 모두 `0`이다. 앞/뒤 owner 구분은 계속 남색 명암으로만 표현한다.

추가 수정: 짧은 중앙 남색 주름도 확대 GIF에서 접합선처럼 읽혀 제거한다. pelvis에서 두 upper-leg chain의
60% 지점 평균을 향하는 tapered hip bridge를 pants core와 실제 색 면 양쪽에 포함해, 포즈가 앞뒤로 이동해도
고정 플랩이 되지 않으면서 골반과 양 허벅지가 한 덩어리로 이어지게 한다. 독립 raster 검사는 투명한 두 leg
run이 pelvis 아래 최소 `17px` 이후에만 시작하도록 강제하며 현 후보의 최솟값은 `19px`다.

추가 수정: 실제 1.55배 맵 화면에서 기존 full width `14.3/12.5/8.5px`의 허벅지/무릎/발목이 관절선은
정확하지만 가는 막대처럼 읽혔다. 관절 중심·heel/toe 접점은 바꾸지 않고 반경만 조절해 약
`16.8/14.5/10.6px`로 보강하고 신발 collar도 함께 넓힌다. 발목과 신발이 겹친 내부의 완전히 둘러싸인
exact-black만 주변의 지배적인 바지/신발 depth 색으로 닫으며 외부 검정 실루엣은 보존한다. 첫 보강본이
interior outline `63px`로 기존 `<=60px` gate를 실패했을 때 gate를 완화하지 않고 이 내부선 처리를 추가했다.

격리 actual QA는 production catalog를 수정하지 않고 외부 6 PNG를 기존 48-frame pose-major 배열의 east row에만
주입한다. 일반 새 게임 bootstrap과 실제 `OfficeRuntimeAgent`를 사용하지만 맵 화면 판독을 위해 다른 가족 3명은
숨긴다. 이 east 직선 캡처는 아트의 실제 게임 scale/cadence 확인용이며 논리 인접 셀 중심 이동의 증명은 아니다.
인접 셀 규칙은 별도 정본대로 tile basis `(160/180, 80/180)`의 길이 `0.99380799`와 stride가 같으므로 한 타일에
6포즈 한 주기, 즉 physical left/right 두 걸음을 완료한다.

신발 수정: 실제 맵에서 평평한 contact 신발의 색 높이가 최소 `8px`라 바닥에 눌린 흰 띠처럼 읽혔다. heel/toe
접점과 toe-east 방향은 유지하고 shoe polygon의 vamp/heel 높이, 앞·뒤 extension, 빨간 heel panel과 흰 toe cap을
보강한다. 독립 gate는 12개 신발 각각 색 높이 `>=10px`, 신발 material `>=130px`, red `>=38px`, white
`>=55px`를 요구한다. 첫 보강본은 P1/P4가 `9px`로 실패해 gate를 낮추지 않고 heel 높이를 추가 보강했다.
최종 최소값은 높이 `10px`, material `147px`, red `48px`, white `62px`이며 shoe overlap 0이다.

타일당 걸음 재검토: loop GIF에서 P0→P3→다음 P0를 세 접지처럼 셀 수 있지만 첫 P0는 시작 상태라 한 타일
진행 중 새 접지는 두 번이다. 현행 `2 steps/tile`, `120.75 steps/min`은 cadence만 보면 일반 걷기 범위지만,
visible sprite height `1.808 world` 대비 step `0.496904 world`는 `27.5%`라 종종걸음으로 읽힌다. KShopGo는
타일 없는 연속 보행 `150 steps/min`, Pokémon Emerald는 16px tile action마다 step animation 시작을 교대하고,
RPG Maker MZ 기본도 한 tile마다 full cycle을 강제하지 않는다. 따라서 사용자 지적은 계수 오류가 아니라 짧은
보폭의 시각 문제다.

사용자 승인 뒤 east 격리 비교값에 `speed 1.5`, full-cycle stride `1.49071199`를 함께 적용했다.
`1.333 steps/tile`, step/height `41.2%`, cadence `120.75 steps/min`이며 정확히 `1 step/tile`은 같은 cadence에서
`2 tiles/s`와 약 `55%` step/height가 필요해 사무실 걷기로는 여전히 채택하지 않는다. runtime 상수만 바꾸지
않고 target root advance를 `19.234993→28.852490px/pose`로 재생성하고 owner별 pelvis→toe chain을 새로
rasterize했다. target heel/toe drift `0.295020px`, 실제 D3D11 cycle distance `1.498756`, VisualRoot offset 0으로
PASS했다. 이 값은 QA 소유 중에만 활성화되고 `EndQaControl`이 speed/stride를 production 값으로 복구한다.
현행 전역 speed/stride, `Legacy48`, 배포 EXE는 유지한다.

## 네 가족 신규게임 결합 빌드는 동일 Player V6 사람 게이트 뒤에만 만든다 (2026-08-23)

결정: 주인공·아빠·엄마·누나가 한 EXE에서 시작하는 격리 빌드는 네 세트 모두 Player V6의 실제
SW/NW/NE/SE×P0~P5 방향, 접촉/지지/낮은 통과, 반대팔·손 교대, 양발 앞발 교대, cadence, stride,
bottom-center를 사람 확대 검수와 D3D11로 통과한 뒤에만 생성한다. 한 캐릭터라도 cadence만 같거나 팔·다리
동작이 다르면 다른 세 캐릭터와 함께 빌드하지 않는다.

이유: 현 일반 catalog build는 production HighMotion을 소유하고, 최신 격리 입력 중 엄마 R17은 원본 전신에
cadence/stride만 적용해 팔 스윙이 V6와 다르며 누나는 유효 후보가 없다. 실패 누나 R16/R18~R22 V2와 새
폐기 generation/parametric 결과를 빈 슬롯에 대입하는 것은 금지된다.

검증: `Tools/Test-FamilyWalkFourFamilyCleanStartInputsV1.ps1`은 정확히 48장/실제 24슬롯, V6 motion owner,
서명된 24포즈·4주기 visual review, 알려진 실패 누나 패키지 배제를 확인한다. 2026-08-23 실행 결과
`167 checks / 5 failures / buildExecuted=no`로 fail-closed했다. 신규 상태 자체는
`CreateNewGameEmptyOfficeV1()`와 `includeInteriorFurniture:false`로 확인했다.

## Runtime-2D V2 Final3는 외부 게임 asset donor 없이 사용자 판정용으로만 잠근다 (2026-08-24)

결정: 실제 family HighMotion 2D를 P0 identity authority, current high-resolution neutral art를 P1로 둔
독자 3D 네 명만 후보로 사용한다. Final2 24방향 검수에서 발견한 Father detached pocket outline과 Older
Sister detached shorts piping은 각각 torso/shorts와 곡면을 공유하는 filled surface로 교체한 Final3만
현행이다. Player와 Mother는 이미 통과한 결과를 보존했다. 네 명 모두 fresh-FBX/Unity Humanoid/공통 walk/
D3D11 구조 gate와 독립 24방향 사용자 표시 gate를 통과했지만 명시적 사용자 승인 전에는
`productionEligible: false`다.

결정: 공개 Blue Archive preview와 사용자가 제공한 Mika/Yuuka archive는 추상적인 비율·눈·팔다리 taper·
hair-clump/toon 관찰만 허용한다. 사용자의 소유권 주장은 기록하지만 archive 내부에 underlying game-origin
asset의 license/transfer 문서가 없으므로 mesh/texture/UV/material/rig donor, Unity import, commit, shipping은
금지한다. `test3.zst`/Sakurako는 bundled LICENSE가 NAT GAMES/NEXON 소유, 상업 사용 금지, ripped라고
명시했고 사용자 지시에 따라 연구 입력에서 제외했다.

검증: Final3 VisualReview는 개별/통합 GIF 모두 24프레임이며 독립 검수 PASS다. Unity BuildRun2는
`Succeeded`; D3D11Run2는 420/420 visual frames, 13.906058초, 네 방향 pose mask `63`, route/root/audio와
네 역할 P0/P3 lead-foot alternation PASS다. production/default/StarterOffice/Downloads는 변경하지 않았다.

## 사용자 소유 Mika/Yuuka 직접 개조가 절차형 Final3/Final4를 대체한다 (2026-08-24)

결정: 사용자가 절차형 Final3/Final4 외형을 사람답지 않고 기존 2D와 닮지 않았다고 명시적으로 탈락시킨
뒤, 본인 소유라고 확인한 `test.7z`와 `test2.targz`의 기존 3D 캐릭터를 직접 수정하도록 지시했다. 따라서
현행 분기는 Mika/Yuuka의 실제 face/eye/body/hand topology, skin weights, rig를 격리된 로컬 proof에서
유지·수정하고, 가족 HighMotion 2D를 기준으로 머리·의상·색·나이 실루엣을 바꾼다. 이전 no-donor 결정은
절차형 historical branch에만 적용한다.

경계: archive 내부에 독립 검증 가능한 license/transfer 문서는 없으므로 현재 근거는 사용자의 ownership
attestation이다. 이 결정은 개인용 로컬 시각 proof만 허용하며, 사용자 화면 승인과 별도 provenance/shipping
결정 전에는 production/default Unity import, 원본 payload commit, 공개 재배포, 판매를 허용하지 않는다.
`test3.zst`/Sakurako는 bundled LICENSE와 사용자 지시에 따라 계속 완전 제외한다.

## Father 생성 보행은 one-package 경계를 넘지 않는다 (2026-08-28)

결정: Father V19부터 생성 메시, bind skeleton, skin weight, 보행 clip은 하나의 Higgsfield/Meshy 작업
패키지로만 소비한다. 외형이 비슷하다는 이유로 별도 job의 메시/리그/clip을 교차 결합하지 않는다.
무릎의 자연스러운 soft blend를 좌우 이름만 보고 일괄 삭제하지 않으며, 신발 영역 반대편 weight와
arm↔leg 강한 혼합만 fail-closed한다.

이유: 이전 세 번째 다리·옷 찢김·흐물거리는 팔/손은 cross-package bind/skin과 과도한 weight sanitation이
만든 문제였다. 새 원본의 전체 limb sanitation 실험은 edge stretch max를 `3.17→29.29`로 악화시켰다.
반면 변경하지 않은 one-package 원본은 foot-region cross `0`, arm↔leg strong `0`이고 실제 127프레임에서
두 다리와 옷 연결이 유지된다.

결정: source action의 주기는 실제 뼈 좌표 recurrence로 측정한다. Father V19 action 613은 127프레임
전체가 아니라 42프레임/1.4초 한 주기이며, Unity는 `1..43`만 사용한다. 실제 맵 cadence/접지는
GaitDistance/stride로 맞추고, 한 회전 7.950477 units에 10 cycles인 `0.7950477`을 사용한다.
정지 시트나 자동 PASS만으로 승인하지 않고, 확대/전체 실제 맵 GIF의 사용자 판정 전까지
`productionEligible=false`를 유지한다.

## 책상·의자는 한 세트로 묶되 승인된 의자 배치는 바꾸지 않는다 (2026-08-28)

결정: 3D 시각물은 좌석마다 `V31_AtomicWorkstationSet_OriginalChair_<seat>` 루트 하나가 책상,
CRT, 키보드, 사용자가 선택한 V29 의자를 모두 소유한다. 실제 layout에서는 묶인 책상이나
의자 어느 쪽을 선택해도 기존
`MoveWorkstation`/`RotateWorkstation`으로 승격하여 책상, 의자, seat cell, approach cell,
operator anchor를 한 트랜잭션으로 이동·회전한다.

결정: 한 세트라는 요청은 묶음 소유권만 뜻한다. 의자 외형·위치, 착석 캐릭터 위치와 자세,
CRT 방향을 재설계하지 않는다. 책상은 canonical `StaticHard`, 의자는 좌석 소유 `Interaction`
obstacle을 계속 사용한다. 해당 좌석을 claim한 캐릭터만 docking 중 의자 obstacle을 통과하고,
다른 캐릭터는 책상과 의자를 모두 피한다.

이유: V30에서 보이는 의자/캐릭터를 `ChairFloorAnchorWorld`로 옮기고 CRT를 회전한 변경은 사용자
요청 범위를 넘었고 시각적으로 거절됐다. V31은 그 재배치를 제거했다. 실제 Player 132프레임은
승인된 V29 대응 PNG와 132/132 SHA-256이 일치한다. 동시에 네 atomic set, legacy renderer 0,
static/interaction/agent penetration `0/0/0`을 기록했고, 기존 atomic layout 검증
accepted 18/refused 6은 유지된다.

## 생산 상점은 CRT 책상과 승인된 기존 의자를 하나의 착석 가능한 세트로 판매한다 (2026-08-29)

결정: `사무실 -> 회사 -> 사무실 관리` 카탈로그는 `desk_with_pc`를
`CRT 업무 책상·회전의자 세트` 한 행으로 표시하고 `swivel_chair` 단독 구매 행은 숨긴다. 의자 정의와
sprite/collision profile은 삭제하거나 교체하지 않는다. 세트 가격은 두 component gameplay basis의 합인
377,500원이며 한 ledger transaction만 기록한다.

결정: 구매 preview의 pointer cell은 chair/seat pivot이다. `PlaceWorkstation`은 책상 2칸, 원래 의자 1칸,
seat cell, approach cell, half-cell operator anchor를 한 candidate grid에 넣고 기존 bounds/overlap/floor/
entrance/BFS/access/egress 검사를 모두 통과할 때만 성공한다. `R`은 SE/SW/NW/NE 네 방향에서 이 값을 한
rigid transform으로 회전한다. 실패하면 자금·ledger·inventory·grid를 전혀 바꾸지 않는다.

결정: 첫 네 구매는 family order에서 아직 없는 `seat_<memberId>`를 사용한다. runtime rebuild가 이를 기존
workstation assignment로 읽으므로 별도 임시 착석 시스템 없이 실제 업무 이동과 docking이 새 의자 위치,
접근 칸, operator anchor를 사용한다. 책상은 StaticHard, 의자는 owner-only Interaction obstacle이므로 좌석
소유자가 docking할 때만 의자를 통과하고 다른 가족은 둘 다 피해 간다.

검증: `OfficeFurnitureBuildSystemValidation`은 네 방향 exact offset, 4회 회전 hash 왕복, 겹침 무결제,
desk/chair/seat 원자 생성, idempotency와 save round-trip을 PASS했다. `OfficeLayoutEditRulesValidation`은
accepted 18/refused 6 PASS다. actual Windows Player native pointer는 한 클릭에서 cash
`5,000,000->4,622,500`, ledger `1->2`, inventory `0->2`, furniture `52->54`, editable `0->2`,
`seat_player`, runtime hash 일치, desk/chair anchor error `0/0`으로 PASS했다.

## 구형 책상·의자 Sprite를 제거하고 V31 세트를 production에 사용한다 (2026-08-29)

결정: 직전의 "의자 sprite를 교체하지 않는다"는 결정 중 시각 에셋 부분은 사용자의 명시적 최신 지시로
폐기한다. 논리 ID `desk_with_pc` / `swivel_chair`와 저장·충돌·좌석 계약은 유지하지만, 금색 책상과 초록
의자 픽셀은 production, 상점 thumbnail, ghost, fallback 어디에서도 사용하지 않는다.

결정: 승인된 V31 procedural 책상/CRT/키보드/오픈백 의자를 네 방향 640x512 RGBA, PPU 180 Sprite로
bake한다. 각 방향의 ground, seat, operator-seat, work-surface anchor를 실제 V31 mesh projection에서
측정하여 사용하고 mirror는 허용하지 않는다. 책상은 2x1 semantic footprint 안에 들어가며 의자/seat pivot,
approach, collision은 기존 atomic 2x2 set 규칙대로 회전한다.

결정: `office_workstation*` / `office_swivel_chair*` standalone 구형 source/runtime/foreground와 meta 34개를
삭제한다. visual resolver는 V31 방향 파일이 하나라도 없으면 구형 catalog로 되돌아가지 않고 실패한다.
2026-08-31 후속 결정으로 구형 4x3 atlas, chroma source, 남은 열 개 cut module과 atlas cutter/검증 코드도
전부 삭제한다. 비-workstation 현행 소품은 `Tiles/Furniture`의 독립 source/runtime만 사용하고,
책상·의자는 V31 네 방향 Resources만 사용하므로 standalone 구형 Sprite를 다시 만들 수 없다.

검증: editor-broad PASS, V31 chair directional integrity PASS, player-scripts PASS. 실제 Windows Player
시각 proof는 네 세트/네 책상 방향/네 의자 방향/legacyFlip 0을 기록했다. 이 결정은 가구 외형만 승격하며
Father V19 캐릭터의 `productionEligible=false`는 변경하지 않는다.

## V31 네 방향 Sprite는 월드 Y축이 아니라 타일 기저에서 정확히 90도 회전한다 (2026-08-29)

결정: 아래쪽 SE 세트를 정본으로 두고 방향별 bake는 등각 투영 전의 semantic tile basis를
`R'=R,F'=F` → `R'=-F,F'=R` → `R'=-R,F'=-F` → `R'=F,F'=-R` 순서로 회전한다. CRT, 키보드,
책상 2x1 몸체, operator/work socket, 의자와 seat centreline 모두 같은 기저를 사용한다. skewed ground
plane에 Unity world-Y 90도 yaw를 적용하는 방식은 타일축과 어긋나므로 폐기한다.

검증: 실제 Windows Player proof
`Artifacts/FastQa/v31-workstation-tile-quarterturn-ready-20260829/`는 네 방향 exact resource,
`legacyFlip=0`을 확인했다. 책상·의자 8개 rendered ground polygon과 authoritative semantic footprint의
최대 모서리 오차는 `0.0001px`다. 이전
`v31-workstation-four-directions-ready-20260829` 화면은 회전 오류 재현 자료일 뿐 현행 승격 근거가 아니다.

## V31 타일 회전은 직각 mesh와 true-isometric 투영을 분리한다 (2026-08-29)

결정: semantic 방향 전환은 계속 `R→-F,F→R`을 사용하지만 그 non-orthogonal 화면 기저를 3D vertex에
직접 적용하지 않는다. 책상, 다리, CRT, 키보드는 서로 직교하는 world X/Z 축의 직사각형 mesh로 만들고,
30도 true-isometric camera가 두 축을 production 타일의 `(160,80)` / `(-160,80)` 픽셀 벡터로 투영한다.
따라서 네 방향은 같은 직각 형상을 rigid quarter-turn하며 좌우 상판이 평행사변형으로 휘지 않는다.

검증: bake는 `meshAxes=orthogonal-90deg`와
`projectedTileBasisPx=160,80|-160,80`을 계산해 둘 중 하나라도 틀리면 실패한다. 실제 Windows Player
`Artifacts/FastQa/v31-workstation-orthogonal-isometric-verified-20260829/`도 같은 manifest, 네 exact
direction resource, `legacyFlip=0`, tile corner 최대 오차 `0.0001px`를 확인한다. 직전
`v31-workstation-tile-quarterturn-ready-20260829` 결과는 타일 좌표는 맞지만 mesh shear가 남은 반려본이다.

## 앞으로의 모든 가구 배치는 semantic 타일을 유일한 정본으로 강제한다 (2026-08-29)

결정: 신규·교체 가구는 rotated integer footprint, placement anchor, claimed/collision cells,
seat/approach socket, 상점 ghost와 확정 runtime renderer가 동일한 타일을 가리켜야 한다. scene transform
보정이나 sprite 외곽을 별도 배치 정본으로 둘 수 없다. 직사각형 mesh는 물리적으로 90도를 유지하고
true-isometric projection으로 `(160,80)` / `(-160,80)` 타일축에 맞춘다. mesh shear, 임의 flip,
footprint 밖 ground contact는 금지한다.

검증/승격 조건: exact 4-direction resource, bounds/overlap/navigation/collision, preview-confirm parity와
실제 Player rendered ground-footprint corner error `<= 0.01px`를 모두 통과해야 한다. 하나라도 실패한 가구는
상점에 노출하거나 production-ready로 표시하지 않는다. 이 규칙의 정본 문서는
`Docs/OFFICE_BUILD_EDITOR_V1.md`의 `Mandatory production tile-placement rule`이다.

## V31 의자 녹색 칸은 보이는 의자 바퀴 아래의 실제 좌석 칸이다 (2026-08-29)

결정: 상점 워크스테이션의 pointer는 계속 chair/seat pivot `(x,y)`로 유지한다. 기본 SE에서 책상은
`(x-1,y+1)`과 `(x,y+1)`, 의자는 `(x,y)`를 점유하며 과거에 녹색으로 칠해졌던 빈칸 `(x-1,y)`는
점유·충돌·preview 어디에서도 사용하지 않는다. 나머지 세 방향은 의자 칸을 중심으로 이 세 칸과 seat,
approach, operator anchor를 정확히 quarter-turn한다.

이유: 이전 bake는 카메라를 빈 semantic pivot에 두면서 실제 의자는 V31 연속 좌표에 남겨, metadata상
ground error가 0이어도 녹색 의자 diamond가 화면상 왼쪽 빈칸에 놓였다. 방향별 의자 Sprite를 실제
swivel-foot contact 중심으로 다시 bake하고 seat anchor를 함께 재측정해야 visual, preview, collision과
착석 위치가 같은 셀을 가리킨다.

검증: actual Windows D3D11 preview는 marker 3, `previewCellsMatchVisibleFurniture=True`, chair `2:2`,
desk origin `1:3`, desk/chair ground error `0/0`을 기록했다. 네 방향 Player proof는 exact desk/chair
resource, `legacyFlip=0`, 최대 tile corner error `0.0003px`로 PASS했다. 가구 시스템 batch도
`geometry=13x4`로 구매·겹침·경로·4회 회전 왕복·저장을 PASS했다.

## 주인공은 전용 좌석 플래그로 아버지의 검증된 3D 착석 계약을 재사용한다 (2026-08-31)

결정: 주인공 걷기 클립을 수정하지 않는다. `-family3d-player-v6-desk-work-qa`를 추가해 실제
`player` binding과 `seat_player`를 선택하고, locomotion이 끝난 뒤에만 아버지에서 승인된
StandingHeight 상대 중립 착석, 쿠션/골반 보정, 키보드 손목 목표와 무릎/발 endpoint IK를 주인공
Avatar에 적용한다. Father ID나 `seat_father`를 주인공에게 묵시적으로 재사용하지 않는다.

이유: 기존 주인공 빌드는 걷기 플래그만 있어 좌석 phase에서 3D를 숨기고 2D 표현으로 돌아갔다.
그래서 아버지와 달리 다리가 뻗은 것처럼 보였으며, 메시·리그·걷기 생성 문제가 아니었다.

검증: 숨김 D3D11 실제 맵 136프레임을 전부 확대 검수했다. 실제 phase는
`Idle>Navigating>ApproachingSeat>AligningSeat>RotatingToSeat>Working`, 무릎은
`106.3443°/110.4238°`, 149,395개 현재 skin vertex의 cushion/back/lumbar/stem/base 관통은 모두
0, 네 workstation 생성 `4/4`, legacy renderer 0, static/interaction/agent violation `0/0/0`이다.
`productionMutation=false`, `productionEligible=false`를 유지하고 사용자 GIF 승인을 기다린다.
