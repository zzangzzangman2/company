# PROJECT STATE

이 문서는 과거 작업 일지가 아니라 **현재 실행 가능한 상태, 아직 통합되지 않은 상태, 정확한 다음 작업**만 기록하는 정본이다. 날짜별 구현 증거는 `History/Reports/`에 보존하며 이 문서보다 우선하지 않는다.

## 2026-08-20 / 현재 작업 정본: KShopGo·Mixamo 추적 east 6포즈 재제작

- 최종 외형은 빨간 캡 주인공의 2D 스프라이트다. **동작 정본은**
  `Docs/KSHOPGO_MOVEMENT_TEARDOWN.md`의 KShopGo Walk와 다운로드된 Mixamo
  `X Bot@Unarmed Walk Forward.fbx`다.
- FBX는 Downloads와 `Assets/FamilyCompany/Editor/PlayerWalkHumanoidAuthoring/`에 모델·걷기 클립 모두
  존재한다. KShopGo Walk는 0.8초, 30fps, 24샘플이다. 새 6포즈는 0/4/8/12/16/20 샘플의
  접지→회수/체중이동→전방 통과/착지 직전→반대 접지 순서를 추적해야 한다.
- 사용자는 기존 원본의 다리 교차와 팔 자세는 승인했지만 v10 이후 후보의 신발 이중화, 접지면 뒤틀림,
  하체 역주행, 상체와 하체 진행 방향 불일치를 반복해서 거부했다. v10~v13은 production 후보가 아니다.
- 하체 좌우반전, 신발/종아리 조각 이동, ImageGen이 관절 타이밍을 발명하는 방식은 금지했다.
  ImageGen은 Mixamo 관절 가이드를 잠근 뒤 2D 외형을 정리할 때만 쓸 수 있다.
- 현재 출하 기본은 계속 `Legacy48`이며 승인된 `Player2DV2` 대체 보행은 없다. 다음 작업 범위는 주인공
  east 6프레임뿐이다. Mixamo 대응 오버레이, 지지발 고정, swing 회수→교차→전방 착지, 상·하체 동방향,
  actual GIF를 모두 통과하고 사용자 승인을 받기 전에는 다른 방향/가족/기본 런타임으로 승격하지 않는다.
- 이동 수치는 speed `1.0`, acceleration `8.0`, cycle distance `0.99380799`로 한 타일에 두 걸음을 유지한다.
  KShopGo의 1.5 unit/s와 1.2 unit stride는 월드 스케일이 달라 직접 복사하지 않는다.
- Unity `PlayerWalkMotionReferenceExporter`는 다운로드된 Mixamo clip을 east `+90°`로 평가해
  `PLAYER_WALK_MOTION_REFERENCE: PASS`를 기록했다. clip 길이는 `1.3666668s`, left-contact phase zero는
  `0.2961111s`다.
- 현행 2D target은 `(0.99380799/6)*180/1.55 = 19.234993px/pose`를 사용하며 계산된 heel/toe world
  contact drift 최대값은 `0.765007px`다. raw Unity export는 ignored `Artifacts`에 재생성하고,
  `ArtSources/PlayerEastMixamoTraceV2/`에는 파생 target, phase contract, guide와 재생성 source를 추적했다.
  이 PASS는 motion/foot-lock 계약에만 해당한다.
- V13 ImageGen whole sheet, 개별 P1/P2 ImageGen, LockedArtV2 raster warp는 모두 거부했다. 기존 lower donor로
  P2/P5를 만들면 다리 방향·연결이 기괴해진다. 다음 단계는 V3 phase별 상체만 보존하고 P0~P5 lower를
  locked `pelvis→hip→knee→ankle→heel/toe` 체인으로 각각 새로 저작하는 것이다.
- 집 PC의 단일 재개 문서는 `Docs/HOME_PC_WALK_CHECKPOINT_2026-08-20.md`다.

## 2026-08-20 / KShopGo 기준 연속 이동 + Mixamo 8방향 후보 (폐기된 역사 수치)

> 아래 절의 speed `1.5`/stride `1.2`는 KShopGo world unit을 직접 복사하던 당시 기록이다. 현행 project
> 정본은 위 절의 speed `1.0`/stride `0.99380799`이며 아래 수치를 runtime에 다시 적용하지 않는다.

- APK 실물 재분석으로 KShopGo Walk가 `0.800s`, 30fps, 24샘플, 인플레이스이며 Idle↔Walk 전이가
  exit time 없는 고정 `0.25s`임을 확인했다. `ApplyRootMotion=True` 42/52라는 플래그와 달리 모든 이동
  클립 평균 root 속도는 0이고 시작·끝 XZ가 같다. feet stabilization/linear velocity blending/feet IK도
  모두 꺼져 있다. `Docs/KSHOPGO_MOVEMENT_TEARDOWN.md`를 이 근거로 정정했다.
- 자유 보행은 이제 segment 시작, 코너, 급반전에서 정지 pivot을 기다리지 않는다. logical root는
  `DefaultMoveSpeed=1.5`, `DefaultAcceleration=8.0`으로 계속 적분하고 Sprite 방향은 같은 frame의 실제
  변위를 즉시 따른다. 한 cycle stride는 `1.5 × 0.8 = 1.2 world unit`, cadence는 2.5 steps/s다.
  `ShortShuffleStrideFraction=0`으로 짧은 입력의 0/3 두 프레임 스터터도 제거했다. 제자리 0.06초 pivot은
  막힘·좌석·업무의 최종 facing 정렬에만 남는다.
- `simulation-pure`와 Unity 6000.3.21f1 Bee Roslyn의 Simulation/Presentation.Unity/Editor 컴파일은
  PASS했다. 기존 정지 반전 Player QA와 전환 QA도 연속 반전/전체 gait 계약으로 갱신했다.
- 당시 Mixamo `canonical-protagonist-v1` 64장 후보를 재베이크 대상으로 기록했으나, 이후 실제 화면에서
  3D primitive 외형과 바운스를 확인해 거부했다. Unity 로그인/라이선스 문제는 현재 경로의 blocker가 아니다.
- 이 절은 아래 2026-08-19의 한 타일 한 주기·0.18초 고정발 회전 후보를 이동 정본으로 대체한다.

## 2026-08-19 / 주인공 자연 보행 V4 — 8단계 발 교대·0.18초 고정발 회전 후보

- 기존 네 자세의 한 bitmap을 이동거리 20~30% 동안 끌던 주인공 보행을 `PlayerNaturalWalkPresenter`가
  `접촉 A→발 떼기 A→통과 A→착지 B→접촉 B→발 떼기 B→통과 B→착지 A` 8단계로 재생한다. 각 자세는
  이동거리의 12.5%만 소유한다. phase는 실제 이동 누적 거리만 읽으므로
  한 타일=한 주기와 tile-center root 계약은 바뀌지 않는다.
- 스침 자세는 새 전신 생성물이 아니다. 각 source-exact contact의 머리·목·얼굴·모자·몸통·재킷·팔·손과
  양쪽 하체 픽셀을 그대로 쓴다. 하체를 중앙에서 좌우 두 덩어리로 나눠 동/서는 각 18px, 남/북은 각
  12px 안쪽으로 평행 이동할 뿐 축소·재표본화·보간하지 않는다. 발 떼기는 최대 이동/들기의 50% 이하,
  통과는 전체 이동과 12px(동/서) 또는 9px(남/북) 들기, 착지는 다음 contact의 50% 안쪽 이동을 쓴다.
  접지발과 이동발이 바뀌는 과정을 세 자세로 나누고 두 다리의 원래 굵기는 유지한다.
  `Tools/extract_player_natural_passes_v1.py`와 `PlayerNaturalWalkV1/source-receipt.json`이 입력 SHA, 경계,
  `legScale=1.0`과 발 들기 값을 고정한다.
- 코너에서는 주인공만 0.18초 동안 logical root를 정지시키고, 이전 방향→중간 cardinal→목표 방향의
  source-exact contact를 30/40/30%로 보여 준 뒤 다음 타일 translation을 시작한다. 좌석·작업·idle은 기존
  renderer 소유권을 유지한다.
- ImageGen 통과 자세 3회는 체크 배경, 무릎 과상승, 다리 겹침 또는 정지 자세 회귀로 모두 거부했고 게임
  자산에 넣지 않았다. legacy 전신 passing 혼합 후보는 체형 축소로, legacy 하체 corridor 합성 V1은 발을
  바꿔 디딜 때 다리가 한 덩어리처럼 얇아져 폐기했다. V2 wide-passing은 굵기는 해결했지만 두 발이 모두
  바닥에 붙어 교대 순간이 약해 V3로 대체했다. V3는 발 들기는 읽혔지만 한 스침 bitmap을 이동거리 30%
  동안 끌어 미끄러져 보여 V4 8단계로 대체했다.
- Unity 6000.3.21f1 Windows Player D3D11 실제 8구간 loop는 577 moving frame/69 capture,
  최대 center-segment 이탈 `0.00000053 world`, endpoint/visual-root/final-center 오차 0,
  collision projection/moving sprite violation/build warning 0으로 PASS했다. 사람 최종 화면 승인 대기이며
  승인 전에는 다른 가족으로 확대하지 않는다.

## 2026-08-19 / 주인공 실제 타일 보행 — 크기·접지 정규화 뒤 최종 화면 승인 대기

- 첫 실제 사무실 GIF는 경로 수치만 PASS했을 뿐 시각적으로 실패했다. source-exact contact를 기존과 같은
  PPU 180으로 가져와 legacy 전환 프레임보다 약 1.75배 커졌고, crop 아래 4px 투명 여백 때문에 발이 떠
  보였으며 코너에서 legacy 프레임으로 전환할 때 캐릭터가 작아졌다 다시 커졌다. 이 GIF와 수치를 승인
  근거로 사용하지 않는다.
- contact crop은 불투명 발바닥이 bottom-center pivot에 직접 닿도록 bottom padding을 0으로 바꿨다.
  동/서는 PPU 314, 남/북은 PPU 324로 방향별 불투명 신장을 기존 256px 전환 프레임과 맞췄다. Point,
  mipmap off, uncompressed 및 source-exact/generated pixel 0 계약은 유지한다.
- 수정한 Windows Player D3D11 실제 사무실에서 grid `(1,1)→(2,1)→(3,1)→(3,2)→(3,3)→(2,3)→
  (1,3)→(1,2)→(1,1)`의 인접 8구간을 다시 돌았다. 581 moving frame/68 capture에서 최대 center-segment
  이탈 `0.00000080 world`, endpoint/visual-root/final-center 오차 0, collision projection과 moving sprite
  violation 0, build warning 0으로 PASS했다. 청록 선은 이 QA 실행에만 있는 정확한 tile-center 경로 표시다.
- 수치 PASS는 사람 화면 승인을 대신하지 않는다. 수정 GIF의 크기·접지·코너 전환은 사용자 최종 화면
  승인 대기이며 승인 전에는 나머지 가족으로 확대하지 않는다.

## 2026-08-19 / 가족 보행 전면 정리 — Player source-exact 방향 후보 실행 검증

- 사용자가 기존 보행 생성 찌꺼기 전체 삭제를 승인했다. `FamilyWalkHalfCyclesV2`,
  `FamilyLocomotionRigV1`, `MotherSideWalkV3`, `MotherNorthWalkV2`,
  `CharacterLocomotionIdentityV1`과 연결된 생성기·검사기·구 계약 문서를 제거했다. 현재
  `HighMotion/Frames`와 이동/충돌/착석/방향 전환 런타임은 다른 가족과 non-walking fallback 때문에 유지한다.
- 자동 생성 rigid-part 후보도 D3D11 화면에서 팔·다리 비율이 원본과 달라져 거부하고 source,
  Resources, 코드, QA Player와 로그를 모두 삭제했다. 승인 layered PSB가 없을 때 자동 파츠 분리를
  production으로 승격하지 않는다.
- 최초 승인 범위는 주인공 동쪽 보행 하나였다. `Tools/extract_player_east_contacts_v1.py`가 정본
  `player_pixel_walk4x2_v1.png`의 east 열 두 칸을 생성/보간 없이 crop하고 bottom-center 정렬한다.
  런타임 `PlayerEastContactPresenter`는 기존 `GaitPhase01`의 0.0/0.5 반주기에 두 접촉 포즈를 배정한다.
  idle, 착석, 작업, 퇴장은 기존 `DirectionalSpriteAnimator`가 그대로 소유한다.
- source SHA-256은 `0C23A5D9594FFED9E8263938A11F6268F133B09ECDFFC90BAD4E2545179BC4EB`,
  현재 출력은 204×389 RGBA 두 장이며 receipt는
  `Assets/Resources/FamilyCompany/PlayerEastContactV1/source-receipt.json`이다.
- Unity `6000.3.21f1`에서 Editor D3D11 6 phase와 실제 Windows Player D3D11 6 phase가 PASS했다.
  Player 로그는 `graphics=Direct3D11`, `device=Intel(R) Graphics`, build warning 0이다.
- `com.unity.2d.animation 13.0.0`, `com.unity.2d.psdimporter 12.0.0`은 향후 사람이 승인한 layered PSB
  실험 기반으로 설치했다. 현재 두 접촉 기준선은 이 패키지에 의존하지 않는다.
- 2026-08-19 사용자 화면 승인을 받았다. Player east source-exact contact V1은 사람 게이트까지 통과한
  현재 정본이다. east 파일은 더 이상 자동 파츠/전신 생성으로
  덮어쓰지 않는다. 더 부드러운 rigid cutout이 필요하면 사람이 원본을 12~18 layer PSB로 나눈 뒤 별도
  후보로만 다시 연다.
- 다음 범위인 Player south도 같은 정본 시트의 south 열 두 칸을 새 픽셀·보간 없이 crop해 연결했다.
  현재 출력은 177×401 RGBA 두 장이고 receipt는
  `Assets/Resources/FamilyCompany/PlayerSouthContactV1/source-receipt.json`이다. Unity Editor D3D11 6 phase와
  실제 Windows Player D3D11 6 phase가 PASS했고 통합 Player build warning은 0이다. 같은 통합 Player에서
  east 6 phase를 재캡처해 승인 당시 PNG와 SHA-256 바이트 일치도 확인했다. 2026-08-19 사용자 화면
  승인을 받아 south도 현재 정본이다.
- 같은 4×2 source의 north/west 열도 각각 2장씩 source-exact로 게시했다. 현재 north는 173×396, west는
  204×389이며 generated/interpolated pixel은 0이다. source에 대각선 아트가 없으므로 주인공 이동 중
  southwest/northwest는 west, northeast/southeast는 east 접촉 포즈를 쓰는 표준 4방향 visual mapping을
  적용했다. Editor D3D11은 north/west 12 phase와 diagonal 24 phase, 실제 Player D3D11도 같은 36 phase를
  PASS했다. 최종 build warning은 0이며 east/south/north/west 회귀 24장은 직전 승인/후보와 SHA-256
  바이트 일치다. north/west 및 대각선 mapping은 사용자 최종 화면 승인 대기 상태다.
- 단독 presenter가 아니라 실제 새 게임 사무실의 `OfficeRuntimeAgent`에도 통합했다. 최초 PPU 180 실행은
  경로 수치와 무관하게 캐릭터 크기·접지·전환이 실패했으며, 위 정규화 재검증만 현재 후보 증거다.

## 2026-08-19 / 문서 정합성 감사 — 코드/실행본 대조 후 정정 (문서만 변경)

- 코드·실행본과 대조해 stale해진 문서 주장만 고쳤다. `Assets`, `Tools`, `ArtSources`는 건드리지 않았고
  Codex가 작업 중인 `Docs/CHARACTER_LOCOMOTION_GENERATION_V1.md`와
  `Tools/build_family_locomotion_rig_v1.py`도 제외했다.
- `b397af9`의 두 차단 회귀는 더 이상 현재 상태가 아니다. 배치 pending 좌클릭은
  `OfficeLayoutEditModeController.HandlePointer()`가 preview 갱신 뒤 같은 frame에 `ConfirmPreview()`를
  호출하고, 빈 사무실 산책은 `OfficeAutonomyCoordinator`의 destination 후보 경로를 쓴다. 이 사실을
  README, AGENTS, OFFICE_BUILD_EDITOR_V1, 이 문서의 검증 표와 backlog에 반영했다.
- 배포 경로가 문서와 달랐다. 실제 배포본은 `%USERPROFILE%\Downloads\Family`(`befe937e`)이고
  `Downloads\FamilyCompany_Playtest`는 존재하지 않는다. 저장소 build는 clean HEAD `8fa5fa74`다.
  PLAYTEST_BUILD, WINDOWS_AUTO_DEPLOY, HOME_PC_CONTINUATION_GUIDE, README를 실제 경로로 정정했다.
- **미해결(코드 쪽)**: `Tools/FamilyCompanyBuild.Common.ps1`의 `Get-FamilyCompanyDeployDefaults`는 아직
  `TargetPath = Downloads\FamilyCompany_Playtest`와 `RequiredBranch = codex/integration-p0-qa`를 기본값으로
  갖는다. 그 branch는 더 이상 없으므로 `DEPLOY_WINDOWS.cmd`를 인수 없이 실행하면 `WRONG_BRANCH`(35)로
  멈춘다. 지금은 문서에 우회 인수를 적어 두었고, 기본값 자체를 `main`/`Family`로 바꿀지는 사용자 결정
  사항으로 남긴다. `Tools/Test-FamilyCompanyDeployPipeline.ps1`도 같은 옛 값을 인수로 넘긴다.
- `Docs/MOVEMENT_NATURALNESS_V1.md`가 제안한 `Editor/OfficeNaturalnessQa.cs`는 만들어진 적이 없어 문서
  상단에 미구현 제안서 표시를 달았다. `Docs/ASSET_MANIFEST.md`가 참조하는 `remove_chroma_key.py`는 이
  저장소와 `simul` 어디에도 없다.
- 대조로 확인한 정확한 값(변경 없음): Unity `6000.3.21f1`, 시작 `2000-01-03 08:50`, 자본금 `5,000,000`,
  13×13/169셀, starter 가구 69, 저장 스키마 `v10`(v1~v9 이관), 출근 09:00~09:03·퇴근 18:00, 허브 5개
  (회사·인사·사업·연구·투자), 계약 T0~T4, 체력 10,000·25% 임계, 가족 4명 보행 PNG 192장
  (4×8방향×6프레임), CANON의 가족 4명 런타임 시트 경로.

## [퇴역 이력] 2026-08-18 / 가족 4명 foot-anchored 공용 리그

> commit `befe937`과 로컬 `6ae4041`의 기존 PASS는 무효다. 전자는 프레임 내부 머리/발 방향을,
> 후자는 실제 projected support foot을 검사하지 않았다. `split_high_motion_sheets.py`가 각 phase의 상체
> median X를 128로 독립 재센터링해 접지발의 root-relative 역이동을 지웠다. 필요한 값은 phase당
> 19.234993px인데 구 PNG는 1.011~6.471px만 움직였고, explicit support가 아닌 best-case 발 선택에서도
> 가족 32/32루프가 26.260~40.138px 미끄러졌다. `6ae4041`은 push·배포하지 않는다.

- 사용자 지시에 따라 출하 범위는 가족 4명×8방향×6위상=192 PNG뿐이다. 직원 후보 8명은 변경하지
  않았고 가족 4명 실제 Player 사람이 승인된 뒤 같은 rig/profile을 retarget한다.
- `Tools/generate_character_locomotion_v1.py`는 이제 `Tools/build_family_locomotion_rig_v1.py`를 호출한다.
  `ArtSources/FamilyLocomotionRigV1`의 SHA 고정 5방향 분리 leg parts와 결함 전 방향별 identity upper를
  사용한다. ImageGen은 파츠까지만 맡고 final foot coordinates/phase/contact는 공용 코드가 소유한다.
- canonical 방향은 south/north/east/southeast/northeast, west 계열 3방향은 frame 전체 mirror다. P0~P2는
  anatomical left support, P3~P5는 right support다. 출하와 alpha가 같은 cyan/magenta marker 사본으로
  두 발을 명시해 검사기가 프레임마다 다른 best-case 발을 고를 수 없다.
- garment seam corridor에서 기존 standing leg만 제거하고 generated leg를 같은 hip에 결합한 뒤 canonical
  upper를 마지막에 덮는다. 별도 pelvis cap과 상/하체 independent offset은 없다. P1/P4에는 upper와 hip을
  함께 1px 아래로 옮겨 얼굴/의상 픽셀을 변형하지 않으면서 frozen torso 느낌과 seam 찢어짐을 막는다.
- front/back은 fixed-length 평면 IK 대신 foreshortening을 사용한다. 엄마의 불투명 치마 아래 한 발이
  사라지는 것과 과도한 X자/팔자 다리를 함께 피하도록 depth-facing stance를 18px로 고정했다.
- 후보와 게시 runtime의 독립 foot-lock QA는 4명/8방향/32루프/192프레임 전부 PASS다. 최대 projected
  support drift `0.726448px`, alternating contact step `57.669070~57.794742px`, swing world travel 최소
  `86.316402px`, passing lift 최소 `3.234033px`, detached alpha `0px`다. 기존 `.meta`/GUID hash diff는 0건이다.
- 음성 회귀는 정지발을 `38.470px` support drift, 같은 지지발 반복을 `32.407px` contact-step error,
  lift 0을 air-phase failure, P0~P5 left를 ownership failure, 모자 절단을 alpha/identity failure로 거부한다.
- Unity 6000.3.21f1 숨김 batch는 실제 import된 `characters=4 directions=8 frames=192`, stride
  `0.99380800`, `rootStepPx=19.2350`, cadence `2.0125 steps/s`로 PASS했고 `FAST_QA_WINDOWS.cmd -Profile
  editor-broad`도 10.725초에 PASS했다. clean Release build는 `Builds/Windows/FamilyCompany_Playtest`에
  `commit=8fa5fa74`, `WorkingTreeDirty=False`로 존재한다. 남은 것은 배포본 D3D11 Player의 renderer
  support-world trace와 실제 캡처 사람 검토이며, 현재 배포본 `Downloads\Family`는 아직 `befe937e`다.
  이 둘이 PASS하고 배포본 `BUILD_INFO` SHA가 HEAD와 같기 전에는 완료/정상 릴리스가 아니다.
- 정확한 source SHA, 좌표계, phase, threshold, 실패 사례와 재현 명령은
  `Docs/CHARACTER_LOCOMOTION_GENERATION_V1.md`가 소유한다.

## [퇴역 이력] 2026-08-18 / 가족 보행 identity-lock 재제작·실제 Player 승인 후보

아래 내용은 Generation V1 이전 제작 이력이다. 현재 shipping writer/gate를 설명하지 않는다.

- `FC-WALK-GUARDRAIL-V1`과 `FC-WALK-TWOSTEP-GATE-V1`에 따라 가족 4명×8방향×6프레임을
  identity-locked candidate 61로 교체했다. 방향마다 canonical portrait/body anchor를 고정하고 두 다리만
  결정론적으로 움직인다. 출하 source 192장에는 표식이 한 번도 닿지 않고, 좌/우 다리 표식은 동일 alpha의
  별도 `MarkerReviewV1` 192장에만 있다.
- `Verify-FamilyWalkTwoStep.ps1 -SelfTest`는 반사 행 승인·합성 한 발 반복 거부·전신 반전 치트 거부를
  통과했다. 추적 source는 해부학 표식 32/32와 two-step 32/32, runtime static 32/32, unit 10/10과
  source/runtime/sheet byte 일치를 통과했다. 필수 마지막 줄은
  `FAMILY_WALK_TWO_STEP_GATE: PASS | contract=FC-WALK-TWOSTEP-GATE-V1 source=artsources rows=32`다.
- `build_family_walk_half_cycles_v2.py`는 구형 V4/V5/V6/V7/raw import 세대를 제거한 단일
  source→runtime writer다. `--write`는 source와 marker gate PASS 전에는 쓰지 않는다. candidate 62
  재생성은 gate FAIL로 폐기했고 추적 원화에 쓰지 않았다.
- production `StarterOfficeRuntimeBootstrap.CreateActor`는 `LocomotionTransitionsV1`의 다른 세대
  시작/정지/pivot 초상화를 더 이상 가족 actor에 구성하지 않는다. 빈 사무실 연속 경로는 승인 walk/idle만
  재생하고, 다음 cardinal segment 방향으로 planted pivot을 끝낸 뒤 타일 중심 translation을 시작한다.
- 실제 Release Player normal 빈 사무실 1× 08:50→09:50은 30fps 629-frame burst와 8fps 가족별 review
  sheet/GIF로 사람 확인했다. 네 가족의 identity, 짧은 보폭, 양발 교대, 방향 전환을 승인했고 런지·X자
  다리·크로마·잔상·초상화 교체를 찾지 못했다. 1×/2×/4×에서 합계 walk loop 132/134/124였고 모든
  direction mismatch, pivot 전 이동, 중복 pivot, non-cardinal segment, 충돌/겹침, 타일 중심 이탈,
  visual-root offset, transition sprite frame은 0이다.
- 실제 Windows native pointer가 빈 새 게임의 녹색 `(1,1)` 프리뷰 셀을 한 번 클릭했다. pointer commit과
  state mutation은 각각 1회, 자금 `5,000,000→4,986,250`, ledger `1→2`, inventory `0→1`, furniture
  `52→53`, editable `0→1`, grid hash `104C121BBA787A22→2D928B958610B1BF`이며 runtime hash도 같다.
  화분 semantic/render 중심의 타일 중심 오차는 `0.00000000`이다.
- Unity `6000.3.21f1` `editor-broad`와 `player-scripts`가 PASS했다. release 승격은 이 문서가 포함된 clean
  main commit을 다시 빌드하고 동일 파일을 Downloads에서 재검증한 뒤에만 완료한다.

## 2026-08-17 / 화면 진행방향 직접 양자화·V5 두발 보행

- 런타임 `world` 변위는 이미 등각 Tilemap 변환이 적용된 실제 화면 진행 벡터다. 이를 다시 grid 축으로
  역투영한 것이 오른쪽 위로 번역하면서 정면/옆모습을 재생한 최초 방향 오류였다.
- `DefaultWorldVectorToVisualFacingAxes`는 실제 변위를 그대로 반환하고 원화의 화면 heading을 직접
  양자화한다. 결과는 grid `+X→northeast`, `−X→southwest`, `+Y→northwest`, `−Y→southeast`다.
  따라서 pivot과 translation 프레임의 머리·허리·다리 방향이 화면 displacement와 동일하다.
- 월드 변위를 옥탄트로 바꾸던 나머지 소비자(`NormalNewGameSeatStallObserver`, `ScenePreviewJump`의
  reversal QA와 8방향 QA, `OfficeSeatDockingR5eTraceWriter`)도 같은 직접 화면축 어댑터를 거치게 했다.
  매우 짧은 진단 프레임은 예외를 던지지 않는 `ResolveDirectionFromAxes`를 쓴다.
- 검증기가 제품과 같은 변환을 공유해 거짓 PASS를 내지 않도록 기대값을 하드코딩에서 어댑터 독립
  불변조건으로 바꿨다. 2:1 스텝 벡터는 리터럴로 적고, 몸의 좌우 기울기가 진행 방향의 반대일 수 없다는
  조건과 8개 서로 다른 헤딩이 8개 서로 다른 원화 행에 도달해야 한다는 조건을 추가했다.
- 정지·회전·출발/정지 전이 프레임 256개 중 64칸이 셀 상단에서 잘려 정지 중인 가족의 머리 윗부분이
  평평하게 사라졌다. 원인은 생성 아트가 4×4 시트의 셀 경계를 넘겨 그려졌는데 분할이 256px 경계에서
  그대로 잘랐기 때문이다. 잘린 픽셀은 한 칸 위 셀에 그대로 남아 있었다.
- `Tools/Repair-LocomotionTransitionFrameHeads.ps1`이 vendored alpha 시트를 상향 overflow 밴드와 함께
  다시 분할하고, 최대 연결 실루엣만 남겨 위 칸의 발을 버린 뒤 기존 planted-foot 정렬을 적용한다. 43개
  프레임이 1~18행을 되찾았고 전체 256개에서 평평한 상단 행은 0이다. 복구 대상이 아닌 프레임은 실루엣이
  픽셀 단위로 동일하며(onlyOld=0, onlyNew=0), 기존 분할이 1:1 blit에서 남긴 최대 5/255 색 잔차만
  사라진다.
- Unity `6000.3.21f1`에서 `OFFICE_MOVEMENT_FACING_NAVIGATION_VALIDATION: PASS`(seeds=128, paths=1152,
  movingFrames=1970, reverseFacingFrames=0, movingDuringPivot=0, unnecessaryCornerStops=0), 실제 Release
  Player의 normal 빈 사무실 observer 1배속 08:50→09:50 `PASS`(4인 보행, directionMismatch=0,
  prePivotTranslation=0, duplicatePivot=0, currentLook=0, nonCardinal=0, collision=0), 확장 tile runtime
  QA `-familyCompanyTileRuntimeQa` exit 0(좌석·도킹·4방향·배치/회수·자율 시계·HUD·로딩 UI)을 통과했다.
- 미해결: 확장 QA 로그에 `OFFICE_EMPTY_WANDER_FAIL | actor=father|mother candidateCount=19 rejected=19`가
  반복된다. QA fixture 배치에서 두 액터의 산책 후보가 전부 거부되어 매 tick 재시도한다. normal 빈
  사무실 관측에서는 `coordinatorCandidateFailures=0`으로 재현되지 않았다. 사용자가 보고한 "누나가
  제자리에서 도는" 증상은 수정본 60분 관측에서 재현되지 않았고(누나 pivot episode 9회, 옥탄트 24회,
  A→B→A 반복 0회) 사용자 확인이 필요하다.
- 이 작업은 커밋하지 않았고 Downloads 배포도 하지 않았다. 실행본은
  `Builds/Windows/FamilyCompany_Playtest/FamilyCompany.exe`에만 있다.

## 2026-08-17 / 타이틀 BGM 1초 후 무음 — 오디오 listener 소유권 이전

- 프로젝트의 `AudioListener`는 `OfficeTileMigrationPreview.unity`의 Main Camera 하나뿐이었고 첫 씬
  `Prototype01.unity`에는 없다. `ScenePreviewJump.Start()`가 타이틀이 떠 있는 동안 그 씬을 additive로
  워밍하고 `ScenePreviewJump.cs:471`이 로드 직후 같은 listener를 끄므로, 실제 출력은 씬 활성화부터
  listener 비활성까지의 짧은 구간에만 존재했다. 타이틀 BGM이 약 1초 들리다 끊기고 그 뒤 BGM과 모든
  SFX가 세션 내내 무음이 된 원인이다.
- 회귀 조합이다. `6e9d32b`가 preview listener 비활성화를 도입했고 `47aefa9`가 타이틀 워밍을 `Start()`로
  옮기면서 짧은 가청 구간이 생겼다. `door_open`·`door_close` SFX QA는 `PlaySfx` 호출 횟수만 세므로
  listener가 꺼진 무음 상태를 검출하지 못했다.
- `GameAudioCoordinator`가 자기 `DontDestroyOnLoad` 오브젝트에 `AudioListener`를 소유한다. scene load
  콜백과 기존 0.2초 poll에서 자기 것 외의 활성 listener만 끄므로 다중 listener 경고 없이 항상 정확히
  하나가 유지된다. 모든 소리는 `spatialBlend=0`인 2D여서 listener 위치는 결과에 영향이 없고, 씬 파일과
  `ScenePreviewJump`는 변경하지 않았다.
- Unity를 기동하지 않는 Roslyn 경로 `Tools/Validate-ManagementUiV2.ps1`에서 `Presentation.Unity` 전체
  런타임 compile을 포함해 `MANAGEMENT_UI_V2_EXTERNAL_COMPILE`, `MANAGEMENT_UI_V2_EDITOR_VALIDATOR_COMPILE`,
  `MANAGEMENT_UI_V2_STATIC_STRUCTURE` PASS다. 신규 경고는 없다.
- 실제 Player 가청 검증은 하지 않았다. 같은 worktree에서 사무실 배치 작업이 진행 중이라 Unity와 build
  lock을 점유하지 않았다. 다음 명령은 사무실 작업이 끝난 뒤 `FAST_QA_WINDOWS.cmd -Profile player-scripts`를
  실행하고 Player에서 타이틀 BGM 지속과 문·발소리 SFX 가청을 확인하는 것이다.
- 이 변경은 아직 커밋하지 않았다. 같은 worktree에 다른 작업의 미커밋 변경이 함께 있으므로 커밋할 때
  `GameAudioCoordinator.cs`와 이 문서만 명시적으로 선택해야 한다.

## 2026-08-17 / 배치 클릭·빈 사무실 산책 수정 후보 — 실제 Downloads 클릭 승격 보류

- 기준 `b397af9400be6f958e2d57162bf35508863a7a58`의 실제 회귀는 그대로 재현했다. 배치 pending 분기는 녹색
  preview 셀을 갱신한 뒤 return해 같은 좌클릭의 `GetMouseButtonDown(0)`/`ConfirmPreview()`를 소비하지
  못했고, 빈 사무실 observer는 stationary direction transition 48 대 valid walk loop 13인데도 같은 셀
  `current-look`와 destinationless Idle을 검사하지 않아 거짓 PASS했다.
- 배치 pending 분기는 preview 갱신 뒤 실제 좌클릭을 같은 프레임에 정확히 한 번 confirm한다. 성공 로그는
  cash·ledger·inventory·furniture·editable count·grid hash의 전후 값과 semantic/render anchor 오차를 함께
  기록한다. FAST_QA와 내부 runtime desk add/remove는 통과했지만, 다른 전체화면 앱이 전면 입력을 점유하고
  있어 실제 Downloads의 녹색 preview 한 번 클릭 증거와 배포는 아직 보류한다.
- 생산 소유자 `OfficeAutonomyCoordinator`는 실제 새 게임이 빈 사무실일 때 네 가족별로 겹치지 않는 도달 가능
  타일 영역에서 현재 셀과 다른 목적지를 결정적으로 고른다. actor는 각 cardinal segment의 실제 방향으로
  planted pivot을 완료한 다음 translation하고, 이동 프레임은 실제 화면 변위 방향을 즉시 사용한다.
  사용자 녹화에서 보인 반복 점프는 6포즈 원화 위에 `VisualRoot` foot-plant 위치 보정을 다시 더해 표현 속도가
  한 보마다 0~1.5배로 출렁인 것이 원인이어서, 원화·좌석을 바꾸지 않고 중복 전신 보정만 제거했다.
- observer-only normal Player 08:50→09:50은 1×·2×·4× 모두 PASS다. 1×는 coordinator 선택 25·후보 실패 0,
  유효 walk loop 136 대 stationary direction transition 131이며 actor별 loop는 39/37/32/28이다. 2×는
  선택 24·실패 0·loop 134 대 transition 114, 4×는 선택 18·실패 0·loop 114 대 transition 87이다. 세 속도
  모두 current-look·destinationless·같은 셀·중복 pivot·pivot 전 이동·direction/displacement mismatch·
  non-cardinal 이동·충돌·겹침·중복 목적지·타일 중심 이탈·표현 root offset·20분 정지가 0이다.
- `FAST_QA_WINDOWS.cmd -Profile player-scripts`와 `editor-broad`, main navigation Player QA가 통과했다. 렌더링
  전체 타일 runtime QA도 빈 새 게임 확인 뒤 4인 착석, 4방향 교차, 책상 추가/제거, 8방향, 반전 pivot,
  충돌, save/load를 끝까지 실행해 exit 0 PASS했다. clean commit·Release build·Downloads 실제 클릭·동일 후보
  normal 재검증 전에는 배포하거나 `origin/main`에 push하지 않는다.

## 2026-08-17 / 오래된 작업트리·증거 정리

- 필수 Git 기준 작업트리와 `fc_agents/integration_p0`만 남기고 옛 작업트리 34개를 제거했다. clean 작업트리
  27개는 즉시 제거했고, 변경이 있던 7개는 `cleanup-salvage-20260817-*` stash로 복구 가능하게 보존한 뒤
  제거했다. 옛 `Library/Builds/Artifacts`, FastQA runs, 이전 작업방 work/outputs, interim 출력, last-known-good
  복사본을 정리해 약 85GB를 회수했다. 반복 빌드 속도를 위해 현재 `integration_p0/Library/Bee`만 유지한다.

## 2026-08-17 / 빈 타일 사무실·타일 중심 배치·가족 전체 보행 V2 구현 상태

- 실제 새 게임은 13×13 외곽 구조물 52개와 비어 있는 11×11 실내 타일만 생성한다. 좌석·업무 가구·구매
  inventory는 모두 0이며, 기존 4인 가구 배치는 save migration과 출근/좌석 QA 전용 fixture로 분리했다. 빈
  사무실의 가족은 09:00에 actor별 결정적 open-area 타일로 출근하므로 책상이 없다는 이유로 문 밖에서 멈추지
  않는다. `FAST_QA`는 빈 실제 상태를 먼저 단언한 뒤에만 furnished fixture를 설치한다.
- 회사 허브의 기존 `건축·편집` 진입은 `사무실 관리`로 명확히 이름을 바꿨다. 업무·좌석·기기·수납·음료 등
  기존 카테고리별 구매/회전/보관/판매/배치 흐름은 유지하며, 배치 origin은 항상 정수 타일이다. 1×1 가구는
  그 타일의 정중앙, 다중 타일 가구는 점유 footprint 전체의 정중앙만 semantic/render anchor로 허용한다.
- 실제 cardinal cell-centre path가 사용하는 SW/SE/NE/NW 4개 화면 행을 V5 두발 주기로 교체했다.
  순서는 A발 착지·하중, B발 낮은 통과·착지·하중, A발 낮은 통과이며 0↔3 착지발과 2↔5 통과발이 모두
  반대다. 전신 높이와 바닥선 y=247은 6프레임 내내 고정되고 신발 아래 8px가 투명하므로 발이 잘리거나
  바닥에 묻히지 않는다. V5 96프레임은 raw key sheet에서 byte-exact 재생성되며 나머지 진단 행과 합친
  런타임 192프레임·8개 sheet도 `ArtSources/FamilyWalkHalfCyclesV2/`에서 결정적으로 재생성된다.
- warm rebind와 cold 준비 모두 실제 `NavigationPrewarmProgress`를 따른다. 표시 진행률은 raw 값으로 우회하지
  않고 30fps 한 프레임당 최대 0.0127만 전진하며, runtime `IsReady` 전에는 로딩을 닫지 않는다. 준비가 30초
  동안 막히면 원인 경고를 남기고 빠져나오는 fail-open 계약은 유지한다.
- Editor/static에서 empty layout, canonical footprint-center, 보행 V2 32/32 행은 통과했지만 이것은 실제
  구매 클릭과 빈 사무실 자율 산책의 정상 증거가 아니다. 위 차단 회귀가 해소된 동일 clean HEAD의 Release
  Player를 다시 검증한 뒤에만 Downloads와 `origin/main` 승격을 논의한다.

## 2026-08-17 / 엄마 북쪽 보행 V2 검증 완료·배포 후보

- 기존 엄마 북쪽 6프레임은 0/3 상체 실루엣 차이 5.6%, 발 영역 중심 `128.17→128.11px`로 팔·치마가 거의
  고정되어 있었다. 파일 수와 기존 coherence gate는 통과했지만 사용자 화면에서는 발을 끄는 동작으로 읽혔다.
- ImageGen과 명시적 포즈 가이드로 반 주기 0·1·2를 제작하고 3·4·5를 픽셀 정확 좌우 반전했다. 지지발은
  `R,R,L,L,L,R`, 바닥선 y=247이며 0/3 변화는 상체 30.1%·치마 29.2%·발 78.2%다.
- 생성 원본은 `ArtSources/MotherNorthWalkV2/`, 재현 도구는 `Tools/build_mother_north_walk_v2.py`, 의미 회귀는
  `Tools/test_mother_north_walk_v2.py`다. 전용 5/5, generic animation 9/9, 전체 walk 96/96, 엄마 sheet/frame
  48/48 일치를 통과했다.
- Unity `6000.3.21f1` clean Release Player가 정북 실제 이동에서
  `mother_north_walk_0..5` 여섯 imported sprite를 모두 정확한 이름으로 렌더했다. 0/3은 지지발·반대 팔이
  좌우 반전되고, 1/4는 회수, 2/5는 반대 통과 포즈이며 치마 밑단 변화와 발 하단 잘림 0을 실제 D3D12
  768px closeup으로 확인했다. QA는 프레임/스프라이트를 강제하지 않고 기존 직접이동·충돌·거리 기반 gait를
  사용하며 command-line opt-in 밖에서는 NPC 제어에 영향이 없다.
- 당시 furnished 기본 레이아웃의 clean Release observer-only 일반 새 게임은 1x·2x·4x 모두
  08:50→09:50 PASS였다. 이 역사 결과는 이후 도입한 빈 새 게임의 자율 산책이나 실제 구매 클릭을 검증하지
  않으므로 현재 회귀의 정상 근거로 사용하지 않는다.
  `actorQaControl=false`, route injection=false, clock jump=false, docking force=false이며 네 가족 모두 seat
  arrival 1회·Work 6/6·20 game-minute stall 0이다. 커밋된 clean HEAD의 `FAST_QA player-scripts`도
  37.966초로 SLO 60초를 충족했다.
- runtime/art는 `5e3cf8efd54cda4223af51bfd682b7690ba3e34f`, 실제 Player 검증기는
  `104547967959b1728973cd9bf53a910fb39cb553`에 기록했다. warm Library를 같은 worktree에서 보존해 최종
  clean Release는 약 26초, 반복 `player-scripts`는 약 25~38초 범위다. Downloads 승격과 `main` push는
  이 문서를 포함한 clean HEAD의 최종 동일성 검증을 통과한 뒤에만 실행한다.

## 2026-08-16 / 일반 새 게임 좌석 정지·타일 보행 수정·배포 완료

- Downloads의 `8b9e3313928545f98b4fc60427da76901271fc96` 배포본은 일반 새 게임 09:07의 첫
  `FinishingWork`에서 플레이어 컨트롤러가 출근 좌석을 해제해 캐릭터가 의자 앞에 서는 회귀를 재현했다.
  기존 seating `FAST_QA`는 `BeginQaControl`과 `QaBeginSeatedWork`로 일반 일정·이동·좌석 handoff를
  우회했으므로 이 회귀의 정상 실게임 증거가 아니었다.
- 일반 경로는 수동 E 업무로 얻은 좌석만 플레이어 컨트롤러가 해제하고, 출근 atomic seat→Work 6프레임
  handoff 동안 autonomy가 좌석 업무를 선점하지 않게 했다. 고주사율의 작은 실제 변위도 이동으로 인정하도록
  visual displacement 임계값만 `1e-10`으로 낮췄으며, path budget·도착 허용치는 바꾸지 않았다.
- 가족 보행 방향은 이미 투영된 실제 `world` displacement를 그대로 화면 heading으로 사용한다. 화면 오른쪽
  위 이동은 Northeast, 왼쪽 아래는 Southwest 원화를 사용한다. 생산 경로와 legacy pathfinder는 대각선·
  corner easing·중간 지름길을 제거하고 모든 cell center를 순서대로 지나는 4방향 cardinal 경로로 통일했다.
- 기본 이동 속도는 `1.00 world unit/s`, 6프레임 2보 주기는 실제 투영 타일 한 칸과 같은
  `0.99380799 world unit`이다. 한 타일에 정확히 한 주기·두 발걸음이므로 약 `2.01 steps/s`이며, translation은
  선형으로 유지하고 누적 실제 이동거리만 보행 위상을 전진시킨다. 상하 root offset은 사용하지 않고 V5 원화의
  두 착지발·두 통과발만 움직이므로 방방 뛰는 이동을 만들지 않는다.
- 게임 시간 배속만 빨라지고 캐릭터는 1x로 움직이던 원인은 actor motion이 `unscaledDeltaTime`을 사용한 것이었다.
  runtime actor는 이제 `Time.deltaTime`을 사용하며 1x·2x·4x 모두 같은 game-time 이동 거리를 유지한다.
  프레임당 visible motion은 `0.08s` 조각으로 제한해 빠른 배속에서도 충돌·점유 동기화를 보존한다.
- Unity `6000.3.21f1` 정적 이동 검증은 128 seeds / 1,152 paths / 1,970 moving frames를 통과했고
  reverse-facing, moving-during-pivot, unnecessary-corner-stop은 모두 0이다. 실제 Windows Player의 observer-only
  일반 새 게임은 route injection·clock jump·docking force 없이 1x·2x·4x 각각 08:50→09:50을 통과했다.
  네 가족 모두 assigned seat arrival 1회와 Work 6/6을 기록했고 20 game-minute stall은 0이다. 1x에서 누나
  09:08, 플레이어 09:10, 아빠 09:22, 엄마 09:24에 atomic seat에 도착했고 모두 다음 1 game-minute 안에
  6프레임 Work loop를 완료했다. 같은 후보의 seating transition FAST_QA도 4인 atomic seat/Work 6/6,
  primary 28/28, safe egress 4/4, penetration 0으로 통과했다. 가족 프레임 복원 뒤 animation asset strict
  gate는 12명·96 walk loops·576 frames 전부 PASS다.
- runtime 수정은 `44702af0d40f255bb598d62c3b09a1e3cd25d752`에 커밋했다. Unity `6000.3.21f1`
  Release candidate와 Downloads 승격본을 각각 별도로 실행해 일반 새 게임 1x·2x·4x를 모두 재검증했고,
  observer-only·route injection false·clock jump false·docking force false 상태에서 네 가족이 모두 seat arrival
  1회·Work 6/6·20 game-minute stall 0을 기록했다. `FAST_QA player-scripts`는 44.647초로 SLO 60초를 지켰다.
  최종 release identity는 Downloads의 `BUILD_INFO.txt`/`DEPLOY_MANIFEST.json`을 정본으로 하며 검증된 clean
  HEAD만 `origin/main`과 `C:\Users\godho\Downloads\FamilyCompany_Playtest`에 함께 승격한다.

## 2026-08-16 / 90도 코너 정지 제거

- `RequiresStationaryPivot` 임계값을 2옥탄트에서 3옥탄트로 올렸다. cell-centre 4방향 routing에서 일반 코너는
  항상 정확히 2옥탄트라 기존 임계값이 모든 코너에서 액터를 정지시키고 45도씩 두 번 회전시킨 뒤 재출발시켰다.
  이제 90도 코너는 걷는 속도를 유지하고 135도 이상만 발을 심고 회전한다. 근거는
  [DECISIONS.md](DECISIONS.md)의 같은 날짜 항목에 있다.
- `editor-broad` PASS 18.561초, `player-scripts` PASS 23.565초(SLO 60초 충족). 기존 안전 지표를 완화하지
  않았다: seeds=128, paths=1152, movingFrames=1970, reverseFacingFrames=0, movingDuringPivot=0,
  maxFacingError=29.2740도, unnecessaryCornerStops=0.
- 이동 중 표시 방향은 여전히 한 프레임에 스냅한다. 옥탄트당 고정 시간 blending은 런타임 불변식과 QA 단언
  7곳, 그리고 `ReverseFacingFrames == 0` 같은 집계 지표를 함께 바꿔야 해서 적용하지 않았다. 미완료 항목이다.

## 2026-08-16 / 반복 개발 루프 정본화

- [ITERATION_LOOP.md](ITERATION_LOOP.md)를 짧은 반복 루프의 정본으로 추가했다. 변경 종류별 명령, warm 캐시
  보존 규칙, `BUILD_WINDOWS.cmd`를 반복 확인에 쓰지 않는 경계를 정한다. 릴리스 절차는 그대로
  [PLAYTEST_BUILD.md](PLAYTEST_BUILD.md)와 [REGRESSION_BUILD_POLICY.md](REGRESSION_BUILD_POLICY.md)가 정본이다.
- 실측 근거: `build-20260815-035423-unity.log`의 자동화 전체 133.4초 중 103.707초가
  `Asset Pipeline Refresh ... InitialRefreshV2(ForceSynchronousImport)`였고 같은 로그 239행에
  `Require frontend run. Library/Bee/1900b0aE.dag couldn't be loaded`가 남았다. warm `Library/Bee`의 normal
  incremental player build는 6.93/6.94/7.00초, forced clean release-config build도 16.00/19.58/19.44초다.
  느린 원인은 빌드 옵션이 아니라 worktree마다 warm `Library`를 버리는 것이다.
- 이 진단 당시 Family Company worktree는 36개였고 13개가 각자 `Library`를 가졌다. 2026-08-17 정리에서
  옛 작업트리 34개와 누적 산출물을 제거했으며, 현재는 필수 Git 기준 작업트리와 warm
  `fc_agents/integration_p0` 두 개만 남는다.
- 문서와 도구가 어긋난 지점을 기록한다. [AGENTS.md](../AGENTS.md)는 정본 브랜치를 `main` 하나로 규정하지만
  `Tools/FamilyCompanyBuild.Common.ps1`의 `Get-FamilyCompanyDeployDefaults`는 `RequiredBranch = 'codex/integration-p0-qa'`를
  요구한다. 로컬 정본 체크아웃에는 `main` 브랜치가 없고 `agent/contract-lifecycle-v0-3`에 머물러 있다.
  둘 중 어느 쪽을 정본으로 삼을지 정하기 전에는 배포 자동화를 `main`에서 그대로 실행할 수 없다.
- 이번 작업은 문서와 계약만 변경했다. Unity, Player, build/deploy는 실행하지 않았다.

## 2026-08-16 / R18 최종 arrival 통합 및 배포 완료

- 최종 arrival descendant `ce9e3ae4d94a7365c0447103d2ad904013ef58a1`를 payload guard·UI·tooling이 포함된 clean integration `d2fa777373e8f0376a5aca4899fdfe0c0fecd43a`에 한 번만 통합했다. merge-base는 `45f22430168cf3b3def1f50b147583a0cc3eb624`, 누적 R18 변경은 `OfficeRuntimeAgent.cs`와 `OfficeSeatingTransitionPlayerQa.cs` 2개이며 overlap/conflict는 0이다.
- R18은 독립 static PASS와 Unity `6000.3.21f1` capture-free Windows Player exit 0 PASS를 받았다. 네 actor 모두 Work 0..5를 관측했고, 같은 좌석의 atomic seat/root/pelvis 정렬과 microslide는 0이며 exit·turn·first-walk·endpoint stationary·safe-egress·furniture drift/penetration도 0이다.
- 통합 tree에서 Roslyn Simulation/Save/Infrastructure/Presentation/Editor compile, production fixture 158 scenarios·15 controls, negative oracle, offline/simulation, Management UI full compile, payload guard 14 fixtures와 tracked-tree 0위반을 다시 통과했다. R18 first-parent 범위 밖 UI·guard·tooling blob은 그대로다.
- 과거·회귀 Windows 실행 payload는 repo 밖 text/hash evidence를 먼저 보존하고 허용된 payload root에서 Recycle Bin 우선 방식으로 제거했으며, 이전 GitHub 감사의 history·tags·Releases·Actions executable payload는 모두 0이다. `da5c6e7f9f9d48f0eada245cff727435536c91dd`의 fail-closed CI guard가 `git add -f`와 이름·확장자 변경 Player bundle의 재유입을 차단한다.
- 최종 Windows build와 Downloads 승격은 2026-08-16에 수행되었다. 배포본
  `C:\Users\godho\Downloads\FamilyCompany_Playtest`의 `BUILD_INFO.txt`는 `commit=8b9e3313928545f98b4fc60427da76901271fc96`,
  `unity=6000.3.21f1 c02631ffc030`, `configuration=Windows Release`,
  `qa=FAMILY_COMPANY_SEATING_TRANSITION_QA PASS exit0`을 기록하며 이 SHA는 `origin/main` HEAD와 일치한다.
  `DEPLOY_MANIFEST.tsv`/`.sha256`도 같은 폴더에 있다.
- 이 배포본의 `BUILD_INFO.txt`가 증명하는 QA는 seating transition QA 하나다.
  [REGRESSION_BUILD_POLICY.md](REGRESSION_BUILD_POLICY.md)가 요구하는 네 가족
  `player` 09:00 / `older_sister` 09:01 / `father` 09:02 / `mother` 09:03 입장·착석 독립 oracle의 통과 여부는
  배포본 옆 evidence에 기록되어 있지 않다. 다음 배포 전에 이 oracle 결과를 `BUILD_INFO.txt` 또는 같은
  폴더의 비실행 evidence로 남겨야 한다.

## 2026-08-16 / Git tracked Windows Player payload 예방 guard 후보

- `.gitignore`가 canonical/nested `Builds/Windows`, known `FamilyCompany`/legacy `Company` Player 이름과 Playtest archive의 일반 accidental add를 막는다. 이는 보안 경계가 아니며 `git add -f`는 아래 CI가 별도로 검사한다.
- `Tools/Verify-NoTrackedPlayerPayload.ps1`은 NUL 종료 `git ls-files -s -z` index와 `git cat-file --batch` blob bytes를 대소문자 무시 경로 검사와 함께 사용한다. 알려진 배포 루트·EXE·Data·archive뿐 아니라 실제 Unity Windows Player PE, `UnityPlayer.dll`, CrashHandler identity와 실제 `*_Data` serialized/boot/Managed topology를 이름·확장자와 무관하게 차단한다. 일반 Unity package/plugin/source/art/font/audio, 임의 DLL·`*_Data`, DGGL은 차단하지 않는다.
- archive는 확장자가 아니라 ZIP/7z/RAR magic을 우선 식별한다. ZIP은 .NET으로, 7z/RAR은 7z 또는 bsdtar로 member bytes까지 검사하며, 확장자를 `.bin`으로 바꾼 Player bundle도 차단한다. archive를 검사할 수 없거나 512 MiB inspection limit을 넘으면 fail-closed한다. 전용 GitHub Actions workflow는 PR, push, 수동 실행에서 self-test와 현재 tracked tree 검사를 모두 수행한다.
- temp Git repo self-test 14건은 모든 target을 `git add -f`하고 각 baseline PASS→target 단독 oracle→index restore PASS를 검증한다. faithful PE/serialized/archive bytes로 standalone Player EXE·DLL·CrashHandler·Data, conventional/surface-renamed unpacked bundle, ZIP/7z와 `.bin` rename, malformed RAR, 공백·한글·대소문자·특수 선행 경로를 다루며 legitimate plugin/DGGL/source/art/font/audio/source archive 오탐은 0이다. 실제 Unity 6000.3.21f1 template 3종과 실제 `globalgamemanagers` identity probe, 현재 tracked tree 6,592개 0위반, workflow negative exit 1 전달도 통과했다. Unity, Player, build/deploy는 실행하지 않았다.
- 이전 remote audit에서 origin/main/history/tags/Releases/Actions executable payload가 모두 0이었으므로 history rewrite/force-push는 계속 불필요하며 이 후보도 remote ref/release/history를 수정하지 않는다. 서버 차단을 완결하려면 merge 후 repository ruleset에서 `No tracked Windows Player payload / Verify tracked Player payload is absent`를 required check로 지정해야 한다.

## 2026-08-16 / 회귀·실패 실행본 영구 삭제 정책

- [REGRESSION_BUILD_POLICY.md](REGRESSION_BUILD_POLICY.md)를 build/deploy의 영구 fail-closed 정본으로 추가했다. user-visible regression, failed gate, stale/unverified provenance, self-PASS-only candidate는 current 또는 Downloads에 존재할 수 없다.
- 판정 시 exact-root fence와 unrelated build 보호를 먼저 확인하고, SHA/log/manifest 같은 비실행 evidence를 payload 밖에 보존한 뒤 EXE, `*_Data`, `UnityPlayer.dll`을 포함한 전체 실행 payload를 즉시 삭제한다. 이름 변경·격리·LKG suffix만으로 회귀 payload를 보존하지 않는다.
- 회귀 payload의 재승격은 금지한다. 수정 뒤 모든 관련 regression oracle, 기존 필수 gate, 독립 gate를 통과하고 새 commit/input fingerprint/build ID를 가진 새 payload만 처음부터 빌드할 수 있다.
- Windows release의 독립 필수 oracle은 fresh 08:50에서 네 가족이 `player` 09:00, `older_sister` 09:01, `father` 09:02, `mother` 09:03에 실제 입장·이동·assigned seat 착석을 증명해야 한다.
- 이번 변경은 문서 계약만 추가했다. 기존 build/deploy 자동화가 regression 발견 시 evidence→전체 payload 삭제→검증된 정상 build rollback/없으면 empty를 실제 구현하고 독립 테스트로 증명하기 전에는 current/Downloads 실제 배포에 사용하지 않는다.
- 최종 push 전 remote zero-inventory gate는 `.gitignore`와 local tracked tree, `origin/main` current tree, 모든 active branch/tag tree, draft/prerelease 포함 release asset을 검사해 회귀·구 executable payload와 unknown identity가 각각 0임을 manifest로 증명해야 한다. tracked build는 exact 일반 cleanup commit으로 제거하며 `.gitignore`만 추가하거나 `git rm --cached`만 하는 것으로 끝내지 않는다.
- 과거 object까지 제거하는 history rewrite/force-push는 일반 payload 삭제가 아니다. exact object/ref/release reachability audit, 검증된 offline backup/restore, collaborator clone·worktree·CI 영향과 re-clone 계획에 대한 별도 승인이 있기 전에는 수행하지 않는다. 이번 문서 follow-up은 remote fetch/삭제/push나 rewrite를 수행하지 않는다.

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
  `downloadsTouched=false`, `unityLaunched=false`였다. 실제 최종 player build와 Downloads 배포는 그 뒤
  2026-08-16에 `8b9e3313`으로 수행되었다.
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

## 2026-08-19 / Player Baked Walk V2 south 승인 후보

- `company_unity_walk_final_analysis_33d9b5a.md`의 구조 변경안에 따라, 기존 납작 이미지의 중앙 하체 분할을
  더 조정하지 않고 Editor 전용 PSB paper-doll + Unity 2D IK + PNG bake 경로를 추가했다.
- 주인공 `south` 한 방향만 384×512/324 PPU의 17개 rigid layer와 8개 포즈로 제작했다. 얼굴·빨간 모자·머리·
  재킷·몸통은 승인된 contact 픽셀을 유지하며, 좌우 팔·골반·허벅지·종아리·신발 ownership은 PSB에서 고정한다.
- 베이크 결과는 런타임에서 단일 `SpriteRenderer`만 사용한다. PSB, rig prefab, `LimbSolver2D`, `IKManager2D`,
  AnimationClip은 `Assets/FamilyCompany/Editor` 아래의 전용 authoring 입력이며 Player payload에 넣지 않는다.
- 기본 런타임은 `Legacy48`이다. `PlayerNaturalWalkV1`은 `-familyCompanyPlayerNaturalWalkV1`, 완성된 V2는
  `-familyCompanyPlayerBakedWalkV2`가 있어야만 활성화된다. 8방향 catalog가 없으면 V2 flag는 fail closed한다.
- D3D11 숨김 Editor bake와 south static QA가 PASS했다. 8 PNG, 고정 canvas/PPU, hard alpha, waist 연결,
  material alpha 단일 연결을 검사했다. 최대 support drift는 projected `0.000042px`/2D `0.000044px`,
  0→4 contact-step 오차는 `0px`, 양쪽 passing lift는 `18px`다.
- 아직 **production 승격 아님**: south 사람 승인, 나머지 7방향 독립 제작, 64프레임 catalog, actual normal
  Windows D3D11 Player trace/캡처가 남았다. 게임/Unity 전면 창, Windows build, Downloads 배포는 실행하지 않았다.
- `FC-WALK-GUARDRAIL-V1 확인: 0/3 해부학적 앞발 교대, 2/5 낮은 통과발 교대, 짧은 보폭, 동일 실루엣, 별도 전환 그림 금지, actual normal EXE 판정 전 미배포.`

## 2026-08-20 / Mixamo Humanoid Walk V2 fail-closed 재구성 (거부된 연구 기록)

- 최초 X Bot bake를 분석해 64장 중 pose 0/4/5/6이 8방향에서 byte-identical이고, 나머지도 2~3개
  방향 변형뿐인 원인을 확인했다. `AnimationMode.SampleAnimationClip`이 미리 넣은 root yaw를 덮어썼고,
  `continueOnValidationFailure=true`가 방향별 4.124~13.043% 높이 실패를 warning으로 삼킨 상태였다.
- 베이커는 sample 뒤 yaw를 적용하고, 8 direction row hash/pose hash를 fail-closed 검사한다. 크기는 clip
  보폭에 맞춘 `0.45522`가 아니라 8포즈 투영 실루엣 중앙값 380px로 정하며, runtime stride/8의 support
  foot 궤도는 전체 RGBA frame 정수 픽셀 평행이동으로 결합한다.
- Mixamo X Bot 표면은 pipeline probe로만 남기고 `canonical-protagonist-v1`이 같은 뼈에 빨간 뉴스보이
  캡·흰 후드 윈드브레이커·줄무늬 셔츠·남색 바지·운동화의 닫힌 볼륨을 붙인다. 자동 분리 PSB/2D IK
  종이인형 후보는 final art가 아니다.
- Humanoid 검증 profile을 paper-doll profile과 분리했지만 우회 모드는 제거했다. hard alpha/canvas,
  pelvis, material component, support drift, contact step, passing lift와 방향/포즈 고유성을 모두 통과해야
  한다. 후보는 `PlayerBakedWalkHumanoidV2Candidate`에 격리되고, 전체 PASS 뒤에만 promotion 명령이
  production `PlayerBakedWalkV2` 64장과 catalog를 갱신한다.
- `PlayerBakedWalkV2PlayerQa`는 당시 D3D11 actual player 검증용으로 작성됐다. 당시 pipeline은 bake→검증→
  promotion→Windows build→player QA를 수행했지만 후보 전체가 거부된 뒤 PowerShell entry point와 C#
  production promotion을 영구 차단했다. 현행 gate나 재실행 명령이 아니다.
- Unity 6000.3.21f1 Roslyn 독립 컴파일은 Editor 변경과 Player QA 모두 PASS했고 PowerShell parser도
  PASS했다. 실제 Editor D3D bake는 현재 Codex sandbox가 Unity Licensing Client의 BIOS/MAC WMI 조회를
  `Access denied`로 막아 미실행이다. 로컬 Personal entitlement와 headless entitlement 파일은
  2026-09-19 갱신 상태라 사용자 로그인/라이선스 부재가 아니라 sandbox 경계다.
- **production 승격 아님**: 생성됐던 candidate/runtime payload는 거부됐고 ignored `Artifacts`로 격리했다.
  따라서 런타임 기본은 `Legacy48`이며 Humanoid production promotion은 실행할 수 없다.

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
| 사무실 | 새 게임은 13×13, 외곽 bay 52, 편집 가능 가구·좌석·재고 0. furnished `StarterOfficeV1`은 migration/QA fixture 전용 |
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

1. 완료된 `ArtSources/PlayerEastMixamoTraceV2/`의 0/4/8/12/16/20 관절·접지·foot-lock 계약 위에
   주인공 east lower 6장을 완전한 physical-owner chain으로 새로 저작한다. v4~v13 lower나 거부된
   ImageGen/raster-warp 결과를 보정해 재사용하지 않는다.
2. east GIF의 회수→교차→전방 착지, 상·하체 동방향과 KShopGo식 연속 체중이동을 사용자가 승인하기 전에는
   `Legacy48` 기본값을 바꾸지 않고 다른 방향·가족으로 확대하지 않는다.
3. 걷기 승인 뒤에만 필요하면 Mixamo `Standing Idle`, `Sitting Idle`/`Sitting`, `Typing`, `Sit To Type`,
   `Type To Sit`을 필요한 순서로 받는다. 걷기 gate에 추가 클립을 선행 조건으로 만들지 않는다.
4. 직원 후보 8인은 고용 시스템이 생긴 뒤에만 출근시킨다. 시작 roster나 09:00~09:03 가족 출근에 섞지 않는다.
5. 소파/다인 좌석은 group atomic claim, 짝 이동, 취소/퇴장 해제, non-NorthWest pose 승인과 idle/emote QA를 추가한다.
6. 오피스 확장은 현재 StarterOffice를 보존하며 단계별 면적/가구 해금으로 구현한다. 과거 요청서의 숫자를 검증 없이 새 정본으로 삼지 않는다.
7. Utility AI의 생산 선택은 `OfficeAutonomyCoordinator`가 소유한다. Shadow scoring 결과를 생산 동작으로 오인하지 않는다.

## 검증 상태

| 범위 | 기준 | 결과 |
| --- | --- | --- |
| Simulation/Editor 전체 회귀 | clean `main` HEAD | FastQA `simulation-pure`, `editor-validation`, `editor-broad` PASS |
| Workforce/Save v10 | clean `main` HEAD | skills=6, grades=S-F, v1~v9 migration, 1x/2x/4x PASS |
| UI V3/Maplestory | clean `main` HEAD | assets=24, characters=670, missingGlyphs=0; 1280×720·1392×768·1600×900/1000·1920×1080 PASS |
| Windows D3D11 UI | `9144fa0e` 실제 native pointer | PASS — 녹색 preview 1클릭이 commit 1회·mutation 1회, 자금 `5,000,000→4,986,250`, furniture `52→53` |
| Windows D3D11 사무실 | `9144fa0e` normal 처음하기 | PASS — 08:50→09:50 4인 보행, `currentLook=0`, `duplicatePivot=0`, `nonCardinal=0`, `collision=0` |
| 가족 4명 보행 리그 | `8fa5fa74` | 진행 중 — foot-lock QA 192/192, Unity batch, `editor-broad` PASS. 배포 Player trace와 캡처 사람 검토 미실시 |
| 주인공 2D Walk V2 / v4~v13 | local rejected research | REJECTED — 다리/팔 일부는 선호됐지만 발목 이중화, 역주행, 하체 방향 불일치와 KShopGo 위상 미추적으로 전체 후보 거부. production 미승격 |
| Player east Mixamo Trace V2 | tracked derived motion contract | PASS — derived owner/phase와 target foot-lock `0.765007px`; raw export는 ignored Artifacts 전용이며 게임 raster/GIF 승인을 뜻하지 않음 |
| Player east ImageGen / LockedArtV2 | tracked rejected evidence | REJECTED — owner/contact 이탈, P2/P5 교차·꺾임, noisy green. shipping frame 0 |
| Mixamo Humanoid Baked V2 | rejected research | REJECTED — 3D primitive 인상, 정체성 불일치, 과한 바운스. production 재실행 금지 |
| 최종 portable Windows build | 최종 `main` | **BLOCKED** — 배포본 `Downloads\Family`가 `befe937e`로 HEAD보다 뒤. 위 보행 gate 통과 후 HEAD로 재배포해야 함 |

과거 개별 PASS는 해당 기능의 회귀 근거다. 최종 결합 SHA의 PASS를 대신하지 않는다.

## 최종 릴리스 체크리스트

1. `git diff --check`, C# compiler, 순수 검증 harness, Unity D3D PlayMode/render QA를 통과한다.
2. 저장 v1~v9→v10, 새 게임 v10, 편집 재고, 계약 성장, 주식 계좌, 출퇴근, 실제 변위 회귀를 확인한다.
3. `BUILD_WINDOWS.cmd`로 새 실행본을 만들고 `BUILD_INFO.txt`와 현재 HEAD가 같은지 확인한다.
4. [REGRESSION_BUILD_POLICY.md](REGRESSION_BUILD_POLICY.md)의 네 가족 09:00/09:01/09:02/09:03 oracle과 독립 gate를 통과시킨다.
5. 검증된 새 identity의 폴더 전체만 현재 배포 경로 `%USERPROFILE%\Downloads\Family`에 배포하고 staging은 같은
   부모의 `.Family.deploy-staging`을 쓴다. `Tools/FamilyCompanyBuild.Common.ps1`의
   `Get-FamilyCompanyDeployDefaults`는 아직 옛 이름 `FamilyCompany_Playtest`와 존재하지 않는 branch
   `codex/integration-p0-qa`를 기본값으로 갖고 있으므로, 배포는 `-TargetPath`/`-RequiredBranch main`을 명시해
   호출한다. FAIL/UNKNOWN이면 evidence 보존 후 해당 payload를 삭제하고 rollback하거나 current를 비워 둔다.

## 다른 PC에서 이어하기

주인공 걷기는 먼저 `Docs/HOME_PC_WALK_CHECKPOINT_2026-08-20.md`의 체크포인트와 재현 명령을 따른다.
일반 저장소/실행 절차는 아래와 같다.

```powershell
git switch main
git status --short --branch
git pull --ff-only origin main
.\BUILD_WINDOWS.cmd
.\RUN_WINDOWS.cmd
```

빌드가 이미 있더라도 `Builds/Windows/FamilyCompany_Playtest/BUILD_INFO.txt`의 commit이 `git rev-parse HEAD`와 다르면 최신 실행본으로 간주하지 않는다. 상세 절차는 [HOME_PC_CONTINUATION_GUIDE.md](HOME_PC_CONTINUATION_GUIDE.md)와 [PLAYTEST_BUILD.md](PLAYTEST_BUILD.md)를 따른다.

위 `BUILD_WINDOWS.cmd`는 그 PC의 실행본을 처음 한 번 만들 때와 배포 후보를 확정할 때만 쓴다. 이후 한 곳을
고치고 확인하는 반복 작업에는 `FAST_QA_WINDOWS.cmd`를 쓰며, 근거와 변경 종류별 명령은
[ITERATION_LOOP.md](ITERATION_LOOP.md)가 정본이다.

회귀·실패·출처 불명·self-PASS-only 실행본의 처리와 재빌드 조건은 [REGRESSION_BUILD_POLICY.md](REGRESSION_BUILD_POLICY.md)를 반드시 따른다.

## 정본 문서 경계

- 인물·출퇴근·사무실 시각: [CANON.md](CANON.md), [ART_STYLE.md](ART_STYLE.md)
- 구조·저장·Unity 경계: [ARCHITECTURE.md](ARCHITECTURE.md)
- 반복 개발 루프와 캐시 규칙: [ITERATION_LOOP.md](ITERATION_LOOP.md), [FAST_QA_WINDOWS.md](FAST_QA_WINDOWS.md)
- build/deploy 회귀 삭제: [REGRESSION_BUILD_POLICY.md](REGRESSION_BUILD_POLICY.md)
- 내비게이션·편집: [MAIN_NAVIGATION_HUD_V2.md](MAIN_NAVIGATION_HUD_V2.md), [OFFICE_BUILD_EDITOR_V1.md](OFFICE_BUILD_EDITOR_V1.md)
- 계약: [CONTRACTS_V0_3.md](CONTRACTS_V0_3.md), [CONTRACT_CLIENT_PROGRESSION_V1.md](CONTRACT_CLIENT_PROGRESSION_V1.md)
- 주식: [SIMUL_MARKET_PORT.md](SIMUL_MARKET_PORT.md), [STOCK_MARKET_LANDSCAPE_V1.md](STOCK_MARKET_LANDSCAPE_V1.md)
- 역사 구현 증거: `History/Reports/` — 현재 상태 정본 아님
