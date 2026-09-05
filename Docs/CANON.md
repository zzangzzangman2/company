# CANON

이 문서는 현재 콘텐츠 정본이다. 충돌하는 옛 문서나 에셋보다 이 문서가 우선한다.

## 게임 전제

- 장르: 싱글플레이 생활 경영 RPG
- 배경: 2000년대 초반 한국풍 가상 도시
- 시작 시각: 2000-01-03 월요일 08:50. 창업 첫날만 가족 4명이 빈 사무실 안에서 먼저 산책한다. 다음 날부터는 09:00~09:03에 1분 간격으로 입장하고 18:00부터 퇴근한다. 직원 8인은 향후 채용 후보이며 고용되기 전에는 출근하지 않는다.
- 초기 자금은 아빠 퇴직금 출자 **5,000,000원**. 무료 업무 가구 없이 시작하고 사무실 관리 상점에서 승인 V31 **책상·PC·의자 한 세트 400,000원**만 판매한다. 4세트 구매 후 잔액은 **3,400,000원**. 구매 확정 전에는 차감하지 않는다. 네 세트를 강제 자동 구매하지 않으며, 구매·배치한 자리부터 기존 업무 동선을 사용할 수 있다.
- 플레이어는 14살이므로 법률 계약과 은행 업무는 성인 가족의 도움을 받는다.
- 가족은 단순한 직원 슬롯이 아니라 관계, 피로, 시간, 회사 역할을 동시에 가진다.

## 가족

> **2026-09-05 임시 4인 3D 표시:** 엄마·누나의 정식 모델 완성 전에는 사용자의 요청대로
> `player/older_sister`에 승인 Player V8을 각각 한 몸, `father/mother`에 승인 Father V19를 각각
> 한 몸 표시한다(아들 2명 + 아빠 2명). 이름·나이·성별·역할·save ID·개별 이동·좌석 ID는 원래
> 가족대로 유지한다. 이 임시 외형은 엄마·누나의 정식 디자인이나 제작 완료가 아니다.
> 매핑 정본은 `OfficeFamily3DVisualRoster`; 4명 모두 구형 스프라이트 표시/폴백은 끈다.
> 원본 3D 에셋·크기·밝기·보행·착석은 바꾸지 않고 해당 외형의 충돌 반경도 그대로 공유한다.

> **2026-09-05 이동 후속:** 기본 프로필 사람 반경은 아들 0.445/아빠 0.415, 가구 패딩은 둘 다 0.18이다.
> 두 임시 복제 외형에도 같은 값이 적용된다. 원본 모델/가구 크기나 확대 후보 승격은 바꾸지 않는다.
> 정면 겹침/가구 관통과 타일 예약 교착은 별도 실제 실행 계측으로 검사한다. PROJECT_STATE가 최신 결과다.

> **2026-09-02 표시 표준:** 크기·밝기·바닥 접지·타일 보행·충돌·착석의 승인 수치와 새 캐릭터 절차는
> `FAMILY_3D_CHARACTER_STANDARD.md`가 소유한다(사용자 승인 후보 프로필: 아들 키 `90px`/아빠 `93.5px`
> @1280x720, 밝기 gain `1.26/1.28`, 가구 여유 `0.40`, 반경 `0.475/0.578`). 아래 2026-08-31 값은 아직
> 코드의 production 기본 프로필이며 승격 시 표준 값으로 교체한다.
>
> **2026-08-31 production 정본:** 주인공은 승인된 Player V8, 아빠는 승인된 Father V19 3D
> 한 몸만 표시한다. 프로덕션 FBX·albedo·material은
> `Assets/FamilyCompany/Content/Resources/Production3D/{PlayerV8,FatherV19}/`, 런타임 소유자는
> `Family3DProductionPresenter`다. Player scale `1.024378657`, height
> `1.857258558`; Father screen-standardized scale `0.950318127`, horizontal scale `0.92`, mesh
> height `1.769311871`; 공통
> stride `0.7950477`,
> cycle `1.4 s`를 바꾸지 않는다. 구형 Player 2D 표시
> 모드와 폴백은 삭제됐고 Player/Father의 숨은 시뮬레이션용 renderer도 `forceRenderingOff`라
> 화면에 나올 수 없다. 사람끼리 동적 반경은 Player `0.28`, Father `0.30`, 가구 경로·도킹
> 반경은 `0.22`다. 설치된 책상·의자는 타일/상점/충돌/save 의미 상태를 그대로 쓰되 승인 V31
> `Family3DWorkstation` 한 세트로 표시한다. Mother/Older Sister의 정식 3D 교체는 아직 없으며,
> 현재 표시만 위 2026-09-05 임시 매핑을 따른다.

> **2026-08-24 최우선 정본:** 아래에 남은 2D 보행 수치와 R-series 기록은 migration/rollback 분석용
> 퇴역 기록이다. 모든 신규 가족 캐릭터는
> `FAMILY_3D_CHARACTER_STANDARD.md`(크기·밝기·접지·보행·충돌·착석·절차)를 따른다. 기존 2D sprite·atlas·PSB·분리 팔다리·보행
> 프레임은 최종 표시, mesh/texture/decal/billboard, motion donor, silent fallback으로 사용할 수 없다.
> 신규 정체성 입력은 `Assets/FamilyCompany/Experimental/Family3DPrototype/References/FamilyIdentityTurnaroundsV1/`
> 의 네 turnaround뿐이며, 네 역할은 같은 Humanoid walk/clock/cadence/phase/root와 실제 SW/NW/NE/SE
> 방향 계약을 공유한다.

### 플레이어

- 내부 ID: player
- 시작 나이: 14
- 임시 생일: 1985-08-10
- 역할: 창업 아이디어, 제품 방향, 시장 조사, 현장 행동
- 신규 3D 외형 정본: 사용자 승인 V6에 맞춘 무모자, 정수리의 뾰족한 짙은 갈색 머리, 갈색 눈
- 런타임 의상 정본: 흰색 후드 윈드브레이커와 남색 트리밍, 남색·노랑·빨강 줄무늬 티셔츠, 짙은 남색 바지, 흰색·남색 운동화
- 정본 범위: 플레이어를 월드에서 식별하는 조작 말의 외형이다. 별도 VN 초상화나 실존 사용자 얼굴을 의미하지 않는다.
- 기반 디자인: 기존 `simul` 타이틀의 14살 플레이어 디자인
- 현재 production 구현은 한 개의 완전한 Player V8 3D skinned body와 유효한 Humanoid Avatar를
  사용한다. 구형 2D 주인공 표시/폴백/전용 제작 에셋은 production 전환 시 제거됐고 되살리지 않는다.
- 사용자 최종 승인 보행 정본은 `FC-PLAYER-TRIAL18-V2-CROWN-SHAPE-REPAIR-V6-USER-APPROVED`다.
  `Artifacts/PlayerWalkTrial18V2CrownShapeRepairV6/Frames`의 8방향×6포즈 48장과
  `crown-shape-repair-receipt.json`을 읽기 전용 기준으로 잠근다. 이후 가족·직원 제작이 이 픽셀이나
  방향·위상·좌우 발 교대·팔 스윙·허리·청바지·신발·stride·cadence를 바꾸면 회귀다.
- 기존 출하 `Legacy48`, Player2DV2 east v10, v11~v13 및 V5 이전 연구 후보는 주인공 정본으로 되돌리지 않는다.
  Player V8 production 승격은 가족 4인 통합 검수와 별도로 2026-08-31 완료됐다.
- 2026-08-24에 삭제된 퇴역 2D guardrail을 따랐던 주인공 east 6프레임
  long-stride 격리 후보는 static/actual D3D11 Player gate를 통과했다. 새 화면 사람 판정과 Assets 승격 전에는
  다른 방향·가족이나 기본 런타임으로 확대하지 않는다.
- Unity가 ignored `Artifacts`에 만든 Mixamo raw trace와, `ArtSources/PlayerEastMixamoTraceV2/`에 추적한
  파생 2D target-joint foot-lock은 PASS했다. 승인된 east 격리값의 root advance는 `28.852490px/pose`, target
  최대 contact drift는 `0.295020px`다. 완성 하체 격리 raster와 actual D3D11 QA도 PASS했지만 production
  `Legacy48`과 배포 EXE는 바꾸지 않았다.

### 누나

- 내부 ID: older_sister
- 이름: 미정
- 시작 나이: 20 (고정)
- 임시 생일: 1979-11-20
- 외형 정본: 긴 검은 양갈래, 검은 리본, 청록색 눈, 성인 20살
- 의상 정본: 몸에 무리 없이 맞는 어두운 민소매 나시티, 흰 파이핑이 있는 남색 돌핀팬츠
- 신발 정본: 맨발. 두 발이 보이는 전신 원화를 기본으로 한다.
- 초기 회사 역할: 운영, 고객 응대, 사무 지원. 세부 성격과 직책은 임시다.
- 기반 에셋: 기존 경마장 표 판매원
- 런타임 이동 정본: `Assets/Art/Characters/OlderSister/Pixel/HighMotion/older_sister_pixel_walk8dir6_{a,b}_v1.png`
- 실제 사무실 이동 방향: SW·NW·NE·SE의 4방향, 방향별 걷기 6포즈. 남·서·북·동을 포함한 8방향
  파일 표는 `DirectionalSpriteAnimator` 저장 호환 슬롯이며 실제 사무실 이동 방향 수가 아니다.

### 아빠

- 내부 ID: father
- 시작 나이: 46 (가족 연령 관계를 맞춘 임시 확정)
- 임시 생일: 1953-06-15
- 역할: 법정대리, 대외 계약, 은행, 영업
- 외형 정본: 짧고 단정한 숯검정 가르마 머리와 관자놀이의 옅은 새치, 짙은 갈색 눈, 가는 은색 사각 안경, 넓은 어깨의 46살 성인
- 의상 정본: 소매를 걷은 탁한 청록 셔츠, 차콜 슬랙스, 갈색 벨트·구두, 아날로그 손목시계
- 정본 원화: Assets/Art/Characters/Father/father_office_neutral_v1.png
- 런타임 이동 정본: `Assets/Art/Characters/Father/Pixel/HighMotion/father_pixel_walk8dir6_{a,b}_v1.png`
- 실제 사무실 이동 방향: SW·NW·NE·SE의 4방향, 방향별 걷기 6포즈. 남·서·북·동을 포함한 8방향
  파일 표는 `DirectionalSpriteAnimator` 저장 호환 슬롯이며 실제 사무실 이동 방향 수가 아니다.

### 엄마

- 내부 ID: mother
- 시작 나이: 44 (가족 연령 관계를 맞춘 임시 확정)
- 임시 생일: 1955-09-02
- 역할: 재무, 회계, 급여, 가계 조율
- 외형 정본: 부드러운 성인형 얼굴, 어깨 길이의 짙은 밤색 머리와 낮은 하프업 트위스트, 갈색 눈, 단단하고 부드러운 체형의 44살 성인
- 의상 정본: 더스티 피치 카디건, 크림 블라우스, 짙은 청록 A라인 스커트, 짙은 갈색 로퍼, 진주 귀걸이, 아날로그 손목시계
- 정본 원화: Assets/Art/Characters/Mother/mother_office_neutral_v1.png
- 런타임 이동 정본: `Assets/Art/Characters/Mother/Pixel/HighMotion/mother_pixel_walk8dir6_{a,b}_v1.png`
- 실제 사무실 이동 방향: SW·NW·NE·SE의 4방향, 방향별 걷기 6포즈. 남·서·북·동을 포함한 8방향
  파일 표는 `DirectionalSpriteAnimator` 저장 호환 슬롯이며 실제 사무실 이동 방향 수가 아니다.

### [퇴역 기록] 가족 2D 보행 동작과 격리 검수 상태

- `FC-PLAYER-TRIAL18-V2-CROWN-SHAPE-REPAIR-V6-USER-APPROVED`가 가족의 실제 이동 4방향 매핑,
  P0~P5 슬롯 순서, cadence, stride, 연속 경로를 소유한다.
- visible 팔·손·허리·다리·발·신발의 픽셀 형상은 캐릭터 정본 체형에 따라 결정한다. 현재 HighMotion에
  한 몸으로 연결된 6포즈 전신이 있는 엄마·누나는 같은 슬롯의 원본 전신을 사용한다. Player V6의
  청바지형 팔다리를 무조건 이식하지 않는다.
- R1·R2·R3은 폐기됐고, 가족 상체 동작을 섞는 R4는 생성 전에 폐기됐다.
- R5 `Artifacts/FamilyWalkPlayerV6FullMotionIdentityR5`는 아빠 실제 화면에서 donor 머리 마스크의
  어깨·윗옷과 V6 상체가 겹쳐 상체가 두 개로 보인다는 사용자 판정으로 폐기됐다. 같은 구조를 사용한
  누나·엄마도 함께 폐기하며 R5를 다시 실행·승격하지 않는다.
- 단일 연결 성분과 방향 불일치 0은 이중 상체의 성공 근거가 아니다. 다음 후보는 얼굴·머리 donor에
  의상·어깨·팔 픽셀 0을 증명하고, V6의 움직이는 몸 한 벌만 남겨야 한다.
- R6 `Artifacts/FamilyWalkPlayerV6HeadOnlySingleBodyR6`는 얼굴·머리만 남겨 이중 상체는 없앴지만,
  아빠 실제 화면에서 머리가 몸에 비해 지나치게 크다는 사용자 판정으로 전 가족 폐기됐다. 실패 증거는
  `C:/Users/godho/AppData/Local/Temp/codex-clipboard-2ff60496-c21c-4efe-8d83-49478e2a1603.png`다.
  얼굴 1개와 방향 불일치 0은 머리 비율 성공 근거가 아니다.
- R7 `Artifacts/FamilyWalkPlayerV6ProportionalHeadSingleBodyR7`은 머리만 줄였지만 14세 주인공 몸에
  아빠 얼굴을 얹은 비정상·아동 체형이라는 사용자 판정으로 전 가족 폐기됐다. 실패 증거는
  `C:/Users/godho/AppData/Local/Temp/codex-clipboard-e081b5d5-11c3-41b2-9da6-76d08878a75d.png`다.
- R8은 주인공 몸을 늘려 성인 비율을 만들었지만 후드 실루엣과 큰 운동화가 남아 정적 확대 단계에서
  자체 폐기했다. R9는 ImageGen이 새 보행을 그렸기 때문에 주인공 V6 동작과 같지 않았고 실제 실행에서
  오른발이 사라진다는 사용자 판정으로 폐기했다. R10은 V6 팔다리를 사용했지만 몸통 교체 경계가 회색
  가로 막대처럼 갈라져 자체 폐기했다. R11은 한 몸통으로 정리됐지만 SW P3·P5에서 뒤 신발이 가려져
  오른발이 없는 것처럼 보여 실행 후보에서 제외했다.
- Player V6 visible 팔다리를 합성 retarget하던 퇴역 후보의 동일 동작 판정은
  `Artifacts/FamilyWalkPlayerV6MotionContractV1/player-v6-family-motion-contract-v1.json`이 소유한다.
  같은 방향·같은 포즈 V6 source, 24장 공통 고정 retarget, motion witness, 방향별 단일 P0 identity core,
  V6 몸통 root offset, 최종 RGBA 독립 재조립을 모두 통과해야 한다. unique raster 수는 성공 기준이 아니다.
- R12는 이 계약에서 **FAIL**이다. SW P3·P5에 다른 포즈 P4 신발 끝을 추가한 cross-pose/pose-specific
  보정이기 때문이다. source/package 해시 0과 런타임 방향 불일치 0은 정확 동작의 성공 근거가 아니다.
- R13은 팔다리 위상 계약은 통과했지만 identity의 갈색 벨트 아래 V6 motion witness의 회색 허리띠가
  다시 보여 상체·하체 연결이 두 개라는 사용자 판정으로 폐기한다. 실패 증거는
  `C:/Users/godho/AppData/Local/Temp/codex-clipboard-3716e55d-dbff-418d-a9dc-69a3072f8c5a.png`다.
  방향 불일치 0과 motion witness 일치는 단일 허리의 성공 근거가 아니다.
- 최신 아빠 후보 R14는 `Artifacts/FamilyWalkFatherPlayerV6ExactMotionSingleWaistR14`다. R13의 같은 V6
  motion witness는 그대로 보존하고 `waistOwner=identityCore` 하나가 셔츠·갈색 벨트·좁은 골반 연결을
  소유한다. identity core가 `y>=170` 다리 영역에 들어가면 validator가 실패한다.
- R14 자동 계약은 실제 네 방향 24프레임 `637/637`, failure `0`으로 PASS했다. 격리 패키지
  `Artifacts/FamilyWalkFatherPlayerV6ExactMotionSingleWaistDemoR14`은 source/package `48`장 SHA256
  차이 `0`이다. 실제 D3D11 `SmokeEvidenceR14Run1`에서 visible actor `1`, 이동 검사 `1,457`프레임,
  방향 불일치 `0`, transitional `0`, 연속 정지 최대 `1`, waypoint reset `0`, 최소 가구 간격
  `29.267px`, BGM 0을 통과했다.
- Player V6 SW P3은 원본 자체가 뒤발 자연 가림으로 한 신발처럼 보인다. R14는 이를 정확히 보존한다.
  다른 포즈 신발을 덧붙여 매 프레임 두 신발을 강제 분리하지 않는다.
- 엄마 R15와 누나 R16은 **FAIL이며 폐기**한다. 자동 계약과 런타임 방향 검사는 통과했지만 실제 캐릭터
  실루엣이 틀렸다. 엄마 원본 폭 `68px`, 누나 원본 폭 `76~78px`에 Player V6 계열 팔다리를 합성해 두
  후보 모두 최대 폭 `112px`이 되었고, 엄마는 coral 소매가 머리 마스크로 오인되어 원본 팔과 V6 팔이
  겹쳐 네 팔처럼 보였다. 엄마의 청록 치마와 V6 골반·하체도 겹쳐 허리 접합이 이중으로 보였다. 누나는
  원래 남색 돌핀팬츠·맨다리 대신 V6 청바지형 하체가 보여 캐릭터 정본을 위반했다. `637/637`, 단일 연결
  성분, direction mismatch `0`은 이 실패의 성공 근거가 아니다.
- 엄마·누나는 visible body를 부위 합성하지 않는다. 같은 방향·같은 P0~P5의 검증된 전신 원본 한 장이
  머리·몸통·양팔·양손·허리·원래 다리·발·신발을 모두 소유한다. Player V6가 소유하는 것은 실제 이동
  4방향 매핑, P0~P5 슬롯 순서와 cadence, stride, 연속 경로뿐이다. scale, 부위 마스크 합성, 생성 팔다리,
  V6 청바지 실루엣을 금지한다. 크로마 원본은 배경 제거와 색 번짐 제거만 허용하며 원본 내부 신체 형상을
  다시 그리지 않는다.
- 엄마 R17 `Artifacts/FamilyWalkMotherOriginalBodyPlayerCadenceR17`은 최신 엄마 격리 후보다. stored
  `48`, actual `24`, 자동 검사 `729/729`, failure `0`; 실제 D3D11 이동 `1,454`프레임, direction mismatch
  `0`, 연속 정지 최대 `1`, 최소 가구 간격 `29.267px`다.
- 누나 R18 `Artifacts/FamilyWalkOlderSisterOriginalBodyPlayerCadenceR18`은 **FAIL/폐기**다. 복사한 기존
  HighMotion alpha export 자체가 크로마 제거 과정에서 종아리와 발 피부 픽셀을 지운 손상본이었다. 방향
  mismatch `0`, 단일 연결 actor, `729/729`은 정상 다리의 근거가 아니다.
- 누나 R19 V2 `Artifacts/FamilyWalkOlderSisterChromaRecoveredLegsDemoR19V2`는 **FAIL/폐기**다. 사용자 실제
  실행 증거 `C:/Users/godho/AppData/Local/Temp/codex-clipboard-61ae1be4-b0a0-440d-bf22-b969e8fcad7e.png`에서
  NW 머리 윗부분이 셀 경계에서 수평으로 잘리고 한 발 보행처럼 보였다. 크로마 sheet를 256px 셀로 자를 때
  원본 actor가 셀 위를 넘은 상태였고, R19 자체 보행은 승인된 Player V6 포즈가 아니었다. 따라서
  `1381/1381`, direction mismatch `0`, unique hash 등은 성공 근거가 아니다. 첫 R19와 V2 모두 다시
  실행·승격하지 않는다.
- 누나 R20 `Artifacts/FamilyWalkOlderSisterPlayerV6SlimBarefootDemoR20`은 **FAIL/폐기**다. Player V6 하체
  alpha 외곽을 행별로 침식해 피부색으로 다시 칠한 방식 때문에 종아리가 막대처럼 가늘고 발이 뭉개진
  덩어리로 변했다. 사용자 실제 판정이 자동 `409/409`, direction mismatch `0`, foot row span `2`보다
  우선한다. span 개수는 정상 무릎·종아리·발 형태를 증명하지 못하므로 해당 validator PASS도 무효다.
- 누나 R21 V3는 **FAIL/폐기**다. 원본 하체를 행별로 Player 발 간격에 가로 scale해 교차 포즈의 뒤
  종아리·발이 반쪽처럼 눌렸다. 누나 R22도 다리 폭은 복구했지만 옛 identity core가 원래 팔을 복사해
  Player 팔과 겹친 실패본이므로 다시 실행하지 않는다.
- 누나 R22 V2 `Artifacts/FamilyWalkOlderSisterPlayerV6IntactLegPartsDemoR22V2`도 **FAIL/폐기**다. 최신
  사용자 실제 실행 증거
  `C:/Users/godho/AppData/Local/Temp/codex-clipboard-0be38274-3d83-4e50-8fa3-f98637bf8bd2.png`에서 화면 왼쪽
  다리가 과도하게 바깥으로 꺾이고 맨발이 크고 납작하게 퍼지는 반면, 반대쪽 다리·발은 가늘고 수직으로
  눌려 한 쌍의 같은 V6 포즈로 읽히지 않았다. 팔·손도 두 개씩 또렷하게 분리되지 않는다. `627/627`,
  두 발 row 수, source/package hash, direction mismatch `0`, BGM `0`은 올바른 다리 체적·발끝/착지·팔/손·
  보행 위상의 성공 근거가 아니다. R22 V2를 다시 실행하거나 조금 고쳐 후속 후보로 사용하지 않는다.
- 누나는 현재 격리 사용자 확인 후보가 없다. R18·R19·R20·R21·R22·R22 V2 및 그 실패 패키지를 입력이나
  성공 출발점으로 재사용하지 않는다. 새 누나 보행은 먼저 승인 잠금된 Player V6의 SW·NW·NE·SE×P0~P5
  같은 슬롯을 기준으로 방향, 포즈 위상, 팔 스윙, 손, 단일 허리, 양다리 형상, 좌우 발 교대, 발끝/착지,
  stride, bottom-center pivot, 연속 경로를 검증하는 공용 기준표/검수기를 만든 뒤 시작한다.
- 새 누나 후보는 별도 확대 시트에서 얼굴/머리·몸통·팔/손·반바지·허벅지·종아리·맨발이 온전한 정체성
  원본만 선별한다. Player 청바지 피부색 recolor, scanline 침식/팽창/가로 warp, cross-pose 다리 donor,
  원래 팔과 Player 팔 이중 합성, 서로 다른 사람의 상·하체 조립, 잘린 크로마 셀, 억지 연결선을 금지한다.
- 당시 공용 2D 기준은 2026-08-24에 삭제된 visual-standard 문서와
  `Artifacts/PlayerV6WalkVisualStandardV1`이다. 정체성 감사는 `Artifacts/OlderSisterIdentityAuditV1`이며
  `walk4x2_v2`는 정체성 주 참고, HighMotion은 체적 참고 전용, 착석·업무 프레임은 보행 입력 금지다.
- 새 image generation 6포즈 스트립은 사용자 증거
  `C:/Users/godho/AppData/Local/Temp/codex-clipboard-540884e6-bf32-4c3e-bf62-326df9f3307c.png`에서 여섯 장 모두
  같은 화면 왼쪽 큰 앞발만 전방 접촉하고 반대발은 뒤에 접힌 `한쪽 발 보행`으로 판정되어 폐기됐다.
  단일 접촉쌍 생성은 P0/P3의 몸 전체 방향이 반대로 뒤집혀 폐기됐고, parametric full-body V1도 막대/교차
  다리·불명확한 발끝/뒤꿈치·팔/손 겹침으로 폐기됐다. 세 결과를 보정·분할하거나 donor로 재사용하지 않는다.
- 모든 후속 검수는 방향별 P0의 해부학적 왼발 전방 접촉, P3의 오른발 전방 접촉, P0↔P3 앞발 주체의
  명시적 교대, 양발이 주기당 한 번씩 앞발이 됨, 같은 큰 앞발이 P0~P5 내내 남지 않음을 별도 사람
  판정한다. 하나라도 실패하면 프레임 수·hash·정적 검사와 무관하게 `한쪽 발 보행`으로 즉시 FAIL이다.
- 2026-08-23 추가 폐기: `FamilyWalkSkeletalIdentityRigV1`, `OlderSisterWalkPlayerV6PerPoseV2`,
  `MotherWalkPlayerV6PerPoseV2`. 분리 손/막대 다리, 동일 큰 앞발 체인, 포즈별 얼굴·몸 폭 흔들림,
  불투명 checker/검은 배경 중 하나 이상이 확대에서 확인됐다. 좌표나 슬롯 수만 맞는 결과는 성공 후보가
  아니며 이 세 결과와 연결된 생성 이미지를 donor 또는 보정 입력으로 재사용하지 않는다.
- 2026-08-23 추가 감사: 정본 HighMotion 누나 SW P0~P5에는 두 다리·두 맨발과 좌우 접촉 교대가 실제로
  존재한다. R22 계열은 이 원본 교대를 조립 과정에서 훼손했다. 다만 정본 양팔은 거의 정지해 Player V6
  팔 스윙 기준을 통과하지 않으므로 정본 세트도 승인/빌드 입력은 아니다.
- 추가 폐기: 좌우 pose-conditioning/direct Player-slot 생성, 고정 P0→P3 편집, 녹색 per-slot 생성,
  `OlderSisterAnatomicalWalkRigV2`, `OlderSisterWholeBodyWalkV3`, 정본 다리 보존 arm-only P0/P3 편집.
  동일 큰 앞발, 정체성 흔들림, 불투명 배경, 돌출 허리선, 막대 팔·다리, 원형 손, 팔 위상 미교대 중 하나
  이상이 확대에서 확인됐다. 어느 결과도 donor·부분 보정·실행본 입력으로 재사용하지 않는다.
- 추가 폐기: 대형 `walk4x2_v2`+Player 기준표 SW 3×2 생성, Player-first P0/P3 역순 reskin,
  `OlderSisterSingleSourceRigidRigV4`, `OlderSisterSingleSourceArticulatedRigV5`,
  `OlderSisterSingleSourceArticulatedRigV6`. 앞발 소유권 미교대, 몸통/허리 손상, 팔 소실, 발 겹침,
  뒤쪽 다리·발 체적 부족, 두 팔·두 손 가독성 실패 중 하나 이상이 SW 확대에서 확인됐다. 24포즈와
  D3D11로 확장하지 않았으며 각 결과를 후보·donor·보정 입력으로 재사용하지 않는다.
- R14·R17만 각각 **아빠·엄마의 격리 사용자 확인 후보**이며 사용자 승격 승인을 받지 않았다. 누나는
  새 격리 후보가 아직 없다. R7·R15·R16·R18·R19·R20·R21·R22·R22 V2를 승인된 것으로 기록하지 않는다.
  사용자 승인 없이 가족 production 에셋에 복사하거나 기본 실행본으로 연결하지 않는다.
- production/Downloads EXE/commit/push는 변경하지 않는다.

## [퇴역 기록] 주인공 2D 보행 제작 정본

- **동작 정본은 KShopGo Walk와 Mixamo `Unarmed Walk Forward`다.** KShopGo의 0.8초·30fps·24샘플을
  정규화한 0/4/8/12/16/20 샘플을 2D 여섯 포즈의 관절 기준으로 사용한다. 2D라는 이유로 보행을 새로
  발명하거나 기존 PNG 하체를 좌우반전해서 채우지 않는다.
- 다운로드/프로젝트 입력은 `C:/Users/godho/Downloads/X Bot.fbx`,
  `C:/Users/godho/Downloads/X Bot@Unarmed Walk Forward.fbx`,
  `Assets/FamilyCompany/Editor/PlayerWalkHumanoidAuthoring/PlayerHumanoidBase.fbx`,
  `Assets/FamilyCompany/Editor/PlayerWalkHumanoidAuthoring/PlayerHumanoidWalk.fbx`다.
- Mixamo/3D는 관절·위상 참고이고 런타임 외형은 2D다. ImageGen은 잠근 골반·무릎·발목·앞코 가이드의
  외형 정리에만 사용할 수 있으며 포즈와 접지 타이밍을 결정하지 않는다.
- 하체 반전, 신발/종아리 조각 이동, 동일 contact 중복, 상체와 하체 방향 불일치는 모두 fail-closed다.
  대응 오버레이와 east GIF를 사용자가 승인하기 전에는 `Assets`로 승격하지 않는다.

- 최종 게임 출력은 256×256, bottom-center pivot, 180 PPU, Point, mipmap 없음, 무압축인 단일 2D Sprite
  48장이다. 방향 순서는 남·남서·서·북서·북·북동·동·남동이고 방향마다 6포즈다.
- Mixamo `Unarmed Walk Forward`와 KShopGo 분석값은 동작 참고다. 좌우 팔다리 교차,
  낮은 passing foot, 연속 방향 전환을 관찰하되 Mixamo 캐릭터 표면이나 3D primitive 외형을 게임에 넣지 않는다.
- 외형은 빨간 뉴스보이 캡, 짙은 갈색 머리와 눈, 흰 후드 윈드브레이커, 줄무늬 셔츠, 남색 바지와 운동화가
  전 방향에서 읽혀야 한다. 머리 꼭대기와 bottom-center 바닥선을 정규화하고 상하 널뛰기는 허용하지 않는다.
- 현행 motion/owner 정본은 `ArtSources/PlayerEastMixamoTraceV2/target-joints.json`과
  `phase-contract.md`다. `SourceV3Frames/`는 phase별 상체·외형 참고이며 lower pose donor가 아니다.
  `Tools/Build-Player2DWalkV2Candidate.ps1`와 기존 `Assets/Resources/FamilyCompany/Player2DWalkV2/Frames/`는
  거부 후보 재현/회귀 기록으로만 남는다.
- `PlayerBakedWalkHumanoidV2Candidate`와 `PlayerBakedWalkV2`의 primitive 휴머노이드 결과는 2026-08-20
  화면 검토에서 **외형 불일치·3D 인형 인상·과한 바운스로 거부**됐다. 연구/회귀 기록일 뿐 production 후보가 아니다.
- logical root, pathfinding, collision, arrival, distance-based gait phase와 actual-displacement 방향은
  기존 `OfficeRuntimeAgent`가 계속 소유한다. east review 후보는 Sprite와 QA 소유 중 speed/stride만 교체하며
  종료 시 production gait로 복구한다.
- 공용 이동 정본은 `1.0 world unit/s`, 가속 `8.0`, 한 보행 주기와 stride가 실제 등각 타일 중심 간 거리인
  `0.99380799 world unit`이다. 즉 한 타일에서 오른발·왼발 한 번씩 정확히 두 걸음을 끝내며 cadence는
  약 `2.0125 steps/s`다. KShopGo의 speed `1.5`/stride `1.2`는 다른 월드 스케일이라 직접 대입하지 않는다.
  KShopGo의 `0.8s`는 pose timing reference이며 실제 project 정속 cycle은 `0.99380799s`다.
  180 PPU와 visual scale 1.55에서 6포즈당 root advance는 `19.234993 source px/pose`다.
  이동 중에는 45°/90°/180° 방향 변경을 위해 logical root를 멈추지 않으며, 매 frame 실제 변위가 가리키는
  8방향 행으로 즉시 바꾼다. 짧은 이동도 2프레임 `ShortShuffle` 대신 전체 보행 위상을 쓴다.
- 사용자 승인 east 격리 비교는 공용 값을 바꾸지 않고 QA 소유 중에만 speed `1.5`, stride `1.49071199`,
  root advance `28.852490px/pose`를 사용한다. 이는 `1.333 steps/tile`, `120.75 steps/min`, visible-height 대비
  step `41.2%`이며 QA 종료 시 공용 speed/stride로 복구한다.
- 제자리 `PivotSeconds=0.06`은 막힌 상태나 좌석·업무 상호작용의 최종 정렬에만 남긴다. 자유 보행 경로의
  segment 시작·급반전 앞에 강제 정지 gate로 사용하지 않는다.
- 과거 source-exact contact, NaturalV1, HighMotion, layered PSB/2D IK와 Humanoid bake는 회귀·정체성
  참고물이다. 새 production 입력으로 되돌리지 않는다.

## 향후 직원 후보 8인

- `simul`의 김서아·이지안·최이서·정아린·박하은·한수아·오지우·윤채아를 향후 고용 가능한 직원 후보 에셋 풀로 사용한다.
- 각 인물의 정본 전신 원화 9종과 정체성 앵커는 외형·표정·복장을 바꾸지 않고 그대로 보존한다.
- 에셋 루트: Assets/Art/Characters/Employees/
- 인물별 런타임 도트는 8방향과 방향별 걷기 6프레임을 가진다. 정본 루트는 각 인물의 `Pixel/HighMotion/`이다.
- 현재 직원 보행 PNG는 이번 가족 4명 seam/모자 수정의 출하 승인이나 QA 범위에 포함되지 않는다.
- 이 8인은 시작 시점의 4인 가족 창업팀에 자동 합류하지 않으며, 이후 고용 시스템에서 해금·채용한다.

## 에셋 권리

사용자는 프로젝트의 기존 생성 에셋이 모두 GPT로 생성되었고 본인이 사용 권리를 보유한다고 명시했다. 외부 에셋을 새로 추가할 때는 별도 라이선스를 ASSET_MANIFEST에 기록한다.

## 초기 타일 사무실 정본

- 실제 새 게임은 `OfficeGridLayouts.CreateNewGameEmptyOfficeV1()`을 사용한다. 13×13 바닥과 외곽 52 bay만 있고 플레이어 배치 가구·좌석·워크스테이션은 0개다. 회사 허브의 `사무실 관리`에서 가구를 구매해 배치한다.
- 모든 구매 가구의 저장 원점은 정수 타일이며 1×1은 그 타일 중심, 다중 타일은 전체 footprint 중심을 의미·시각·충돌 공통 anchor로 사용한다. 포인터의 임의 world 좌표를 저장하지 않는다.
- `CreateStarterOfficeV1()`의 실내 가구 17개 + 외곽 52 bay(총 69), 가족 workstation 4개 구성은 기존 저장 호환과 출근·좌석 QA용 furnished fixture로 유지한다. 현재 전체 저장 스키마는 v10이며 기존 저장의 의미 `OfficeGrid`를 그대로 이관한다.
- 플레이테스트 런타임은 새 게임/불러오기 직후 해당 `GameState.OfficeGrid`를 타일 씬으로 렌더한다. 폐기된 OfficeVisualV2 통짜 PNG는 저장소와 빌드에 존재하지 않으며 `F9`로도 되돌리지 않는다.
- `CreateMigrationPreview()`의 가구 18개·12종·파티션 구성은 T1~T5 회귀 fixture 전용이다. 실제 게임 기본 사무실로 사용하지 않는다.
- workstation은 desk/chair/seat binding, seat/approach cell, NorthWest facing, 반 셀 operator anchor를 가진다. 네 가족의 의미 root는 좌석 셀 중심·scale 1이다.
- 시각 calibration의 유일한 저장 위치는 calibration version 3의 `OfficeFurnitureVisualCatalog.asset`과 version 5의 `OfficeCharacterSeatPoseCatalog.asset`이다. 의자 좌판 중심·책상 operator socket·가구 footprint·clip/frame별 실제 pelvis/hand를 수동 교정하고 전체 QA를 통과한 값만 승인 데이터로 유지한다.
- NorthWest 회전의자의 좌판과 등받이 대부분은 인물 뒤 base로 그린다. chair front overlay는 등받이의 제한된 전면 가장자리와 근접 팔걸이만 인물 위에 그리고 좌판·몸통을 덮지 않는다. 책상 front overlay는 하체 앞의 다리·서랍·앞 모서리만 담당한다.
- 외곽은 13×13 바닥 polygon의 네 외변을 따라 far full wall 26 + near cutaway 25 + `(8,0)` exterior threshold 1의 정확한 한 타일 bay 52개다. 벽 inner edge는 바닥 outer edge와 일치하고 모든 기단 픽셀은 바닥 밖에 있어야 한다. `entrance_door`는 저장 호환 ID일 뿐 door leaf/jamb/lintel/열림 애니메이션이 아니며, 가족은 기존 `(8,1)` entrance를 통해 09:00~09:03 순차 입장한다.

## [퇴역 기록] 2026-08-23 누나 2D 재시작 V7/V8 판정

- `OlderSisterSingleSourceArticulatedRigV7`은 P3 반대 접촉발이 작은 뒤발 형태라 양발 접촉 교대를 사람 눈으로 통과하지 못했다. 한 포즈의 다리를 회전해 주기를 만드는 방식은 폐기했다.
- `OlderSisterSameSlotBodyArmPhaseV8`은 같은 슬롯 HighMotion 전신·다리를 보존하려 했으나 팔 폴리곤이 상의·반바지까지 잘라 P0/P1/P3/P5의 가슴~허리에 흰 대각선 구멍을 만들었다. 몸통 하나·허리 접합 하나 조건 실패이므로 SW 단계에서 폐기했다.
- V7/V8 프레임·마스크·review는 후보·donor·부분 보정·실행본 입력으로 재사용하지 않으며 24포즈와 D3D11로 확장하지 않는다. production/기본 EXE/Downloads EXE는 변경하지 않았다.
