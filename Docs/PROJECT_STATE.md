# PROJECT STATE

## 2026-08-13 / Starter Office furniture tile snap complete

- Unified all 17 starter furniture placement anchors at the exact half-cell center of each hard footprint. Render root, collision footprint, and interaction reference now share one pivot, so moving or mirroring a prop cannot accumulate an unrelated visual offset.
- Removed the swivel chair's exceptional `(40px, 120px)` presentation offset. Occupied-chair contact correction remains inside the existing seating lifecycle; an empty chair always restores to its tile-aligned origin.
- StarterOfficeLayoutAsset and save restore normalize legacy placement values to the canonical footprint center. The editor's former free anchor nudge now performs a canonical snap instead of separating art from collision.
- Unity 6000.3.21f1: `FAMILY_COMPANY_OFFICE_GRID_T1_VALIDATION: PASS`, `OFFICE_FURNITURE_TILE_SNAP_VALIDATION: PASS` (17 furniture/12 definitions), and Starter preview visual residual `maxCorner=0.0001px`, `maxCenter=0.0001px` PASS.
- The rebuilt Windows/D3D player passed loading, 08:50-to-09:00 attendance, modern HUD, autonomous meeting seating, four-seat work contact, four-way traffic, live desk placement/removal, and narrow-corridor QA. All four family workstations reported `seatContact=0px`; the maximum typing hand residual was `3.481px` (limit `4px`).

## 2026-08-13 / GitHub Main README 동기화 완료

- GitHub 메인 README를 현재 정본과 비교해 16:9 전용으로 보이던 화면 설명, 구형 `OFFICE_V0_2` 문서 안내, 잘못된 플레이테스트 경로와 모호한 Windows 빌드 설명을 수정했다.
- 왼쪽 메뉴까지 포함한 440×481 V6 정적 QA 캡처를 `Docs/Images/`에 보존해 README 상단 미리보기로 추가하고, StarterOfficeV1·반응형 타이틀·기존 SIMUL 모바일 UI 복제 금지 규칙을 현재 `CANON`·`ARCHITECTURE`·`FRONTEND`·`DO_NOTS`와 맞췄다.
- README의 Markdown 링크, 이미지 경로, 자동화 파일, Unity 버전과 시작 씬이 모두 존재하는지 확인했다. `FRONTEND_V0_4`의 폐기된 중간안과 현재 V6 규칙을 구분하고 문서 갱신일도 2026-08-13으로 맞췄다.

## 2026-08-13 / Compact Title No-Letterbox V6 완료

- 16:9 V2 배경을 440×481 중앙에 scale-to-fit해 생기던 큰 위아래 검은 띠를 제거했다. 기존 사무실을 기준으로 위쪽 벽·창문과 아래쪽 바닥·식물을 확장한 1195×1316 compact 전용 `money_rain_tycoon_background_portrait_v3.png`를 추가했다.
- compact 런타임은 세로 V3를 화면 전체 aspect-fill하고, 가로 화면은 기존 16:9 V2를 계속 사용한다. 왼쪽 33px 세로 메뉴, 코랄·크림·차콜 UI와 돈다발 12개/2.8초 루프는 유지했다.
- 440×481 정적 QA 프리뷰에서 검은 여백 0, 가족 4인 노출, 왼쪽 메뉴 가독성을 확인했다. Simulation/Presentation/Editor Bee 컴파일은 모두 오류 0이다.

## 2026-08-13 / Main Title Left Menu V5 완료

- 하단 메뉴 배치를 폐기하고 compact·가로 화면 모두 왼쪽 세로 메뉴로 통일했다. 440×481 기준 각 행은 높이 33px, 폭 190px이며 `새 회사/이어하기/불러오기/화면 설정/종료`가 한 열로 정렬된다.
- 기존 민트·청록 UI 팔레트와 하단 삼색 리본을 제거했다. 새 UI는 코랄 기본 선택, 크림 텍스트, 차콜 보조 버튼, 웜그레이 테두리만 사용하며 제목 위의 작은 SEOUL 2000 라벨과 왼쪽 상태선으로 위계를 만든다.
- compact 배경은 상단 고정이 아니라 화면 중앙에 16:9 원본 전체를 표시한다. 메뉴는 원화의 왼쪽 저복잡도 영역에 겹치고 오른쪽 가족 사무실, 누나 맨발, 주인공·엄마의 사람 방향 CRT/키보드는 잘리지 않는다. 돈다발 12개/2.8초 루프는 유지했다.
- 새 Simulation 파일을 명시적으로 포함해 Simulation/Presentation/Editor Bee 컴파일을 다시 실행했고 모두 오류 0이었다. 실제 사용자 캡처와 같은 440×481 정적 QA 프리뷰에서 메뉴 크기·오피스 노출·녹색 UI 제거를 확인했다.

## 2026-08-13 / Main Title Minimal UI V4 완료

- 사용자 피드백에 따라 compact V3의 큰 외곽 패널, NEW/CONTINUE 배지, 버튼별 설명과 3개의 보조 카드 테두리를 제거했다. `새 회사/이어하기`는 높이 42px 전후의 얇은 버튼 2개만 유지하고 `불러오기/화면 설정/종료`는 구분선이 있는 텍스트 메뉴 한 줄로 축소했다.
- 같은 축소 규칙을 가로 화면에도 적용해 5개의 72px 설명 카드를 더 이상 사용하지 않는다. 배경·가족·사람을 향하는 컴퓨터·돈다발 12개/2.8초 루프와 모든 메뉴 동작은 그대로다.
- 440×481 정적 QA 프리뷰에서 메뉴가 차지하는 면적과 텍스트 잘림을 확인했다. Bee 응답 파일이 새 Simulation 소스를 아직 포함하지 않아 해당 파일을 명시적으로 포함한 뒤 Simulation/Presentation/Editor를 재컴파일했고 모두 오류 0이었다.

## 2026-08-13 / Main Title Compact UI V3 완료

- 실제 440×481 창에서 16:9 배경을 화면 전체 aspect-fill해 가족 사무실이 오른쪽 조각만 보이던 문제를 수정했다. 가로세로비 1.35 미만에서는 배경 전체를 상단 16:9 히어로 영역에 `ScaleToFit`으로 표시하고 아래는 짙은 청록 메뉴 영역으로 분리한다.
- 세로형 메인 메뉴는 긴 카드 5개를 쌓지 않는다. `새 회사/이어하기` 2개 큰 타일과 `불러오기/화면/종료` 3개 작은 타일, 한 줄 상태 정보로 재구성해 440×481에서도 오피스와 가족 4인이 화면 절반 이상 보인다.
- 기존 돈다발 3종·12개 인스턴스·2.8초 `Time.unscaledTime` 루프와 모든 버튼 동작은 유지했다. 상단 히어로에 원본 배경 전체가 들어가므로 누나의 맨발과 사람을 향하는 주인공·엄마의 CRT/키보드 방향도 세로 창에서 잘리지 않는다.
- 440×481 및 768×1024 compact layout 계약을 `TitleMoneyRainValidation`에 추가했다. Unity Bee Presentation/Editor 응답 파일 재컴파일은 오류 0이며, Unity 에디터 실행은 기존과 같이 로컬 라이선스 부재 때문에 보류했다. 동일 비율 정적 QA 프리뷰에서 히어로/메뉴 경계와 텍스트 잘림을 육안 확인했다.

## 2026-08-13 / Main Title Tycoon UI V2 완료

- 메인 화면의 단순한 밝은 사무실·민트 사각 버튼 구성을 등각 도트 경영게임 키아트와 전용 타이틀 UI로 교체했다. 새 활성 배경은 `money_rain_tycoon_background_v2.png`이며 왼쪽 42% 메뉴 안전 영역과 오른쪽 가족 4인의 실제 역할 장면을 분리한다.
- 배경은 프로젝트의 SIMUL v3 화풍 앵커, 등각 사무실 도트 타깃, 가족 4인 승인 이동 Sprite를 참조한 OpenAI 내장 ImageGen 생성물이다. 누나는 정본대로 맨발이며, 사용자 피드백에 따라 주인공·엄마의 CRT 화면과 키보드가 각각 앉은 사람을 향하도록 다시 편집했다.
- Unity IMGUI는 새 회사/이어하기/불러오기/화면 설정/종료를 번호·제목·설명 3단 위계의 9-slice 카드로 표시한다. 최근 저장이 없을 때의 비활성 상태, 시작 날짜·가족 수·창업 자금 상태 스트립, 단축키 줄과 민트·코랄 하단 리본을 추가했다.
- 기존 돈다발 3종, 12개 인스턴스, `Time.unscaledTime`, 2.8초 폐루프 경로는 그대로 유지했다. GIF는 여전히 QA 전용이고 런타임은 배경과 투명 돈다발 PNG만 그린다.
- Unity 라이선스 부재로 에디터 실행 검증은 `No valid Unity Editor license found`에서 차단됐다. 대신 Unity 6000.3.21f1 Bee의 실제 Presentation/Editor Roslyn 응답 파일로 두 어셈블리를 재컴파일해 오류 0을 확인했고, 동일 좌표·팔레트·돈다발 궤적의 1920×1080 정적 QA 프리뷰에서 메뉴 안전 영역, 글자 잘림, 네 가족, 누나 맨발, 주인공·엄마 CRT 방향을 육안 확인했다. 라이선스 복구 후 `TitleMoneyRainValidation.Run`과 Windows QA player 캡처를 다시 실행한다.

## 2026-08-13 / P1.5 Placed Furniture Interaction Offer Resolver 완료

- 순수 C# `OfficeInteractionOffer`와 `OfficeInteractionOfferFactory`를 추가했다. 한 Definition이 실제 배치된 가구 인스턴스마다 `interactionId@furnitureId` Offer 하나를 만들며 location, furniture kind, capacity, approach policy를 Definition에서 그대로 가져온다.
- `OfficeRuntimeInteractionOfferResolver`가 현재 Occupancy와 기존 cardinal PathService를 사용해 열린 접근 칸과 실제 도달 가능한 접근 칸만 광고한다. 가구가 없거나 모든 접근 칸이 막혔거나 경로가 없으면 Offer는 0개다.
- Runtime Coordinator가 활성 Micro Action을 Catalog의 Interaction ID로 해석해 Agent까지 전달한다. Agent 목적지는 선택된 OfferId/FurnitureId를 보존하며 Occupancy revision 변경 시 동일 intent도 재해석해 이동 전 접근 칸을 재사용하지 않는다.
- `OfficeRuntimeWorkstationService`의 Micro Action 가구 종류·capacity·접근 정책은 Catalog Definition에서 파생된다. 작성 좌석이 없는 물리 회의 테이블의 직접 플레이어/계약 경로만 기존 예외를 유지한다.
- 레이아웃 변화 QA를 추가했다: 정수기/복사기 삭제, 정수기/소파 이동, 서류장 접근점 전부 차단, 가구까지 경로 단절, 커피 테이블 2개와 인스턴스별 capacity. 엔진 독립 harness는 `OFFICE_INTERACTION_OFFER_EXTERNAL: PASS`; Simulation/Presentation/Editor 외부 컴파일은 오류 0이다.
- Unity 6000.3.21f1 백그라운드 QA 메서드도 추가했지만, 이 세션에서는 로컬 Unity 라이선스가 `No valid Unity Editor license found`로 종료되어 실행되지 못했다. 라이선스 복구 후 `OfficeRuntimeInteractionOfferValidation.RunBatch`와 전체 P1/Player 회귀를 재실행해야 한다.
- 기존 `WeightedPick`, Shadow Utility 결과, GameState, Save schema v7은 변경하지 않았다. 다음 단계는 Offer availability/distance를 Shadow Utility 점수에 관찰 항목으로 연결한 뒤 별도 승인으로 selector를 활성화하는 것이다.

## 2026-08-12 / P1.5 Native Deterministic Smart Interaction Shadow 기반 완료

- 외부 NPC AI 패키지 없이 순수 C# `OfficeInteractionDefinition`, `OfficeInteractionCatalog`,
  `OfficeInteractionScoreBreakdown`, `OfficeInteractionSelectionTrace`를 추가했다.
- 현재 13종 Micro Action을 표준·회의·fallback Interaction 정의 20개로 표현하고 역할별 기존 weight,
  target template, 위치, 가구 kind, 지속 시간, capacity, cooldown, 접근·예약 정책을 기록했다.
- 실제 선택은 기존 `WeightedPick`이 계속 담당한다. Shadow Utility는 정수 점수와
  `office-interaction-pick-v1:worldSeed:memberId:macroStart:sequence` StableRandom 키로 비교 선택만 남긴다.
- 변경 전후 4시간 P1 행동 서명이 정확히 같고 저장 스키마 v7, 1분 step/4시간 jump,
  save/load, capacity, 대화 pair, 45분 책상 제한을 유지한다.
- Catalog parity QA는 20 definitions, 13 actions, 80 role/macro/previous-location cases를 검사한다.
  Shadow QA는 128 seeds×4 hours에서 13,777 traces와 68,807 candidate scores를 2회 재생하고 후보 역순
  불변성을 확인했다. 선택 차이 8,804건은 실제 행동에 적용하지 않은 분석 자료다.
- 산출물은 `Artifacts/OfficeInteractionUtilityShadow/`의 `summary.md`, `score-traces.json`,
  `selection-comparison.json`, `divergent-selections.md`, `determinism-signature.txt`이며 signature는
  `363f11108739c53997036681dacd25b25d0f645b586cb269f09a84ddc25cef3b`이다.
- 다음 단계는 별도 요청 후 진행한다: 실제 배치 가구 Offer Resolver, Shadow 결과 조정·Utility 활성화,
  선택/예약/이동/수행/중단 cleanup lifecycle. 현재는 Runtime·Save·GameState를 변경하지 않는다.

## 2026-08-12 / Mother seated-work stabilization and gentler seat transition

- Rebuilt `mother_northwest_sit_work_0..5.png` from the existing approved art with frame 0 as the
  canonical body. Head, hair, torso, cardigan, skirt, knees, legs, and shoes now remain pixel-identical;
  only the near forearm/wrist/hand region changes across the six-frame loop.
- Added `Tools/stabilize_mother_work_frames.py` so stabilization is deterministic and repeatable.
  A second pass reports registration `0,0`, score `0.00` for every frame and makes no further change.
- Unified the mother's six Work pelvis anchors at `(126,62)`, regenerated the v5 pose catalog, and
  refreshed all six source SHA-256 approvals.
- Seat/stand presentation now uses smoothstep progress rather than equal linear drops, and the
  transition cadence is 0.15 seconds per frame so the first and final beats ease in/out naturally.
- Background-only Unity 6000.3.21f1 validation passed: `SEATED_SPRITE_ROOT_CAUSE_V5_PASS` with
  56 approved profiles, source QA `frames=56`, Windows build exit `0`, and actual player QA exit `0`.
  The player observed all four family members' SitDown `4/4`, Work `6/6`, StandUp `4/4`; the mother's
  seat contact and animated anchor error were both `0.000px`, with rotation/scale deviation `0` and
  agent penetrations `0`. Autonomous father/mother meeting seating and visible chairs also passed.

최종 갱신: 2026-08-13
현재 단계: Main Title V6 / Office Runtime V1 / Management V0.8 + Market Port S2
Unity: 6000.3.21f1

## 현재 목표

2000년의 소형 가족 하청회사에서 시작해 실제 기업과 경쟁하고 2026년까지의 역사를 바꾸는 장기 구조를 세운다. 가까운 재미 검증 질문은 하청 계약과 가족 부탁이 동시에 생겼을 때 누구를 어디로 보내는 선택이 납기·현금·관계에 다른 결과를 만드는가이다.

## 정본 요약

- 플레이어: 14살
- 누나: 20살, 긴 검은 양갈래와 검은 리본·청록색 눈, 나시티·돌핀팬츠·맨발
- 아빠: 46살, 청록 셔츠·차콜 슬랙스·은색 사각 안경 정본
- 엄마: 44살, 피치 카디건·크림 블라우스·청록 스커트 정본
- 캠페인 시작: 2000-01-03 08:50. 09:00~09:11 가족 4인·직원 8인 문 입장, 18:00 퇴근
- 정본 누나 에셋: Assets/Art/Characters/OlderSister/older_sister_casual_neutral_v2.png
- 런타임 누나 도트: Assets/Art/Characters/OlderSister/Pixel/HighMotion/ (8방향×6프레임)
- 런타임 플레이어 도트: Assets/Art/Characters/Player/Pixel/HighMotion/ (무모자, 8방향×6프레임)
- 정본 아빠 원화·도트: Assets/Art/Characters/Father/father_office_neutral_v1.png / Pixel/HighMotion/
- 정본 엄마 원화·도트: Assets/Art/Characters/Mother/mother_office_neutral_v1.png / Pixel/HighMotion/
- 향후 직원 후보 8인: Assets/Art/Characters/Employees/ 아래 전신 원화 72종·정체성 앵커 11종·도트 시트 8종
- 가족 4인·직원 8인 고동작 이동 정본: 인물별 `Pixel/HighMotion/`의 A/B 2장, 총 24장·8방향×6프레임
- 사무실 도트 모듈: Assets/Art/Office/Pixel/office_module_atlas_4x3_v1.png 및 Modules 12종
- 공식 화풍: SIMUL polished soft-render VN anime v3 / 런타임 도트 번역 v1
- 실제 회사 정본: Korea History V1 국내 82개, 등록부 83행, 2000-01-03 국내 상장 종목 10개

## 완료

- Unity 프로젝트 생성
- 협업 문서 뼈대 생성
- 경마장 표 판매원 에셋을 기반으로 20살 누나 정본 원화 생성
- 투명 배경 PNG 검증 완료: 1024x1536 RGBA, 알파 0~255
- FamilyCompany.Simulation 순수 C# 계층 구현: 시간, 안정 RNG, 이벤트 큐, 가족, 회사, 복식부기
- 저장 DTO/매퍼와 Unity JsonUtility 기반 primary/temp/backup 저장소 구현
- Prototype01 씬 생성: 집, 거리, 작은 사무실, 14살 플레이어, 부모 placeholder, 누나 원화
- 등각 카메라, WASD/방향키 이동, 시간 진행, 저장/불러오기 디버그 패널 구현
- 로컬 Git 저장소 main 브랜치와 Prototype 0.1 첫 커밋 생성
- Office V0.2 사무실: 접수, 업무 책상 4개, 회의실, 휴게실, 프린터, 서류장, 식물
- 누나 4방향 2프레임 도트와 잔상 제거 v2 등록
- 누나와 부모 placeholder 2명 CharacterController 실제 이동
- 웨이포인트별 고객 응대, 업무, 출력, 회의, 휴식 상태와 결정론적 체류 시간
- 직교 카메라, 카메라 기준 플레이어 이동, 마우스 휠 줌, 저해상도 Point 렌더
- 궁극 목표 확정: 2000년 하청회사에서 시작해 실제 기업과 경쟁·인수합병하며 2026년까지 대체 역사를 만든다.
- 실제 역사 구조 확정: HistoricalBaseline, WorldState, DivergenceLog와 조건부 사건 해결
- Claude 역사 데이터와 Codex Unity/시장 엔진의 전용 작업 경로 분리
- 기존 `simul` 시장 시스템의 계약 기반 Unity 이식 순서 문서화
- 국내 실제 회사 60개 이상 우선, 실제 이름 UI 표시, 해외 기업 후순위 원칙 확정
- Claude가 `simul`의 기존 한국 시장 타임라인·시대 사건·출처를 읽기 전용으로 재사용하도록 지시문 개편
- 4인 창업팀용 소형 하청 계약 코어 구현: 실제 고객 ID·이름, 결정론적 5종 카탈로그, 인원·인시·동시 계약·현금·평판·보상 상한
- 계약 생명주기 구현: 수락 착수비, 가족별 인시·체력·스트레스, 납기 완료 매출·평판, 기한초과 실패, 중복 수락 차단
- 저장 스키마 v2 구현: 계약 제안·상태·인시·가족별 기여를 보존하고 v1 저장은 빈 계약 목록으로 이관
- 계약 작업 coordinator 구현: 회의·업무·출력 지점 실제 이동과 체류가 끝난 뒤에만 계약 인시 반영
- 시작 이동 NPC를 누나·아빠·엄마로 확정하고 고용 전 직원 A·B placeholder 제거
- 1920×1080 PC 가로형 시작 화면 구현: 처음하기, 이어하기, 불러오기, 화면 설정, 종료
- 게임 중 가로형 HUD와 ESC 메뉴 구현: 계속하기, 저장, 불러오기, 메인 화면
- 3개 저장 슬롯 구현: 최신 슬롯 자동 이어하기, 슬롯 덮어쓰기 확인, primary/temp/backup, 기존 단일 저장의 1번 슬롯 호환
- F11 borderless fullscreen 전환, 1600×900 창 모드, 창 크기 조절, Web 1280×720 기본값 구현
- OpenAI imagegen으로 2000년 가족 사무실과 20살 누나를 담은 16:9 타이틀 키아트 생성 및 Unity Resources 연결
- Windows QA player가 메인 화면을 실제 캡처하는 Frontend V0.4 시각 검증 도구 구현
- SIMUL 공식 v3 화풍 문서와 승인 앵커를 분석하고 프로젝트 내부에 영구 앵커 복사
- 원화·키아트는 SIMUL polished soft-render VN anime v3, 런타임은 Family Company SIMUL-v3 isometric pixel translation v1로 통일
- 14살 플레이어 4방향 2프레임 도트 생성, 투명화, 8개 Sprite 분리 및 파란 캡슐 placeholder 교체
- 2000년 한국풍 사무실 도트 12종 아틀라스 생성, 투명화 및 개별 Sprite 자동 분리
- `simul`의 김서아·이지안·최이서·정아린·박하은·한수아·오지우·윤채아 전신 원화 72종과 정체성 앵커 11종을 무변형 직원 후보 에셋으로 이관
- 향후 직원 8인의 고유 외형·복장·소지품을 유지한 4방향 2프레임 도트 시트 8종과 개별 프레임 64종 제작
- 46살 아빠와 44살 엄마의 SIMUL v3 정본 전신 원화 2종, 4방향 2프레임 도트 시트 2종과 개별 프레임 16종 제작
- 부모와 직원 후보를 함께 임포트·분할·검증하는 `EmployeeArtAssetBuilder` 추가
- Korea History V1 완료: 국내 실제 회사 82개, 2000~2003 상세 25개, 사건 42개, 2004~2026 진입·퇴출 앵커 42개, 인수 후보 20개, 출처 100개, validator 오류 0
- Korea History V1 JsonUtility 로더와 불변 companyId·날짜별 실제 displayNameKo·KOSPI/KOSDAQ 상장 종목 resolver 구현, 프로토타입 씬에 원본 JSON TextAsset 런타임 카탈로그 연결
- SIMUL 시장 S1 구현: 6,545 거래일, 장 세션, D+2, 가격 제한, 호가 단위, IPO 첫날 범위, 수수료·거래세
- Dart 직접 생성 order-book golden v1 추가: cadence, frame, capacity, 다단계 체결, 가격 이동, 개별 print 분할
- SIMUL 시장 S2 1차 구현: 평시 1~4/급변 5/극단 7 pulse, 다단계 지정가 fill, 절대 가격 누적 소비 watermark, depth 기반 체결가 이동
- 누적 소비 snapshot 구현: 소수 단위 floor, 동일 watermark 멱등성, 누적 delta만 차감, 1~9주 잔량 행 숨김, 구조적 벽 90% 돌파와 회복 상한
- 플레이어 대기주문 순수 C# 코어 구현: 매수 고가·매도 저가 우선, 동일 가격 날짜/분/sequence/ID FIFO, 취소·부분체결 queue-ahead 해제, 외부 호가 대기수량 선소진, 수수료 포함 매수 예약금과 매도 잔량 예약
- 7매도+7매수와 batch FIFO `도착→소진→다음 단계`, 10배속 최소 이동 시간, pause 완전 고정, 완료 identity 중복 차단 상태기 구현
- Stock 실제 회사계좌 런타임 통합 완료: 50,000원 격리 fixture를 제거하고 회사 현금↔증권 예수금 UI, 균형분개, 매수 예약금 보호, `GameState` 자동 flush/load와 Save V5 optional 왕복을 연결했다. 날짜 전환 시 현재 비거래 종목의 보유·체결·일지·관심 상태를 승계하고 미체결만 결정적으로 취소하며, 미지 자산 ID는 거절한다. 실시간 잔여초와 FIFO 연속성도 닫기/저장/재개 전후 동일하다.
- 가족 자율 AI 확장: 학교·영업·가사·수면 일정이면 NPC가 출구까지 실제 이동해 퇴실하고, 복귀 시간에는 다시 사무실로 걸어 들어옴
- 플레이어 직접 작업 구현: 계약 단계에 따라 회의실·책상·프린터 가까이에서 E를 유지해야 1인시가 반영되며 학교·체력·마감 규칙을 동일하게 적용
- SIMUL 오디오 50종 이관: BGM 11종·SFX 39종 원본 해시 일치, 타이틀/사무실 BGM 전환과 계약·작업·수익·오류·저장·NPC 발걸음 사건 연결
- 사무실 사운드스케이프 추가: NPC 퇴실·복귀 문, 프린터 도착 종이, 계약 업무/회의 도착 환경음과 전역 쿨다운·객체 교체 무음 재시드
- 외부활동 오디오 큐 36개 추가: 12개 ImageGen 장면별 진입 SFX·반복 BGM·완료 SFX, 조용한/활발한 활동 볼륨·페이드 구분
- 순수 장기 경영 규칙 추가: 2000~2026 금리·예금·대출·어음 할인, 업종별 직원 S~F 등급·월급·계약금·사기·충성도·잠재력, 은행·R&D·채용·법인계좌·인수합병 의미 해금 조건
- 월 급여 묶음 정산 규칙 추가: 외부 직원 우선·집단 비례 배분·안정 키 잔여 1원 배분, 연체·크런치에 따른 사기·충성도·스트레스와 퇴사/스카우트 위험 등급
- 2000년대 회복 활동 12종 추가: 편의점·PC방·비디오/만화 대여·목욕탕·외식·산책·소풍·문방구 오락기·라디오 야식·노래방·ADSL 게임의 비용·시간·체력·스트레스·유대와 연도/요일 조건
- 회복 활동 실행 판정 추가: 연도·요일·인원·중복 참가자·자금 조건, 가계 우선/회사 보충 비용 분담, 종료 시각과 사람별 체력·스트레스·유대 실제 적용량
- 회복 활동 추억 규칙 추가: 완료 활동·시각·참가자·실제 유대 변화·seed에서 불변 MemoryId, 한국어 회상 문구, 관계 태그와 중요도 생성
- SIMUL 한글 폰트 3종과 라이선스 2종 무변형 이관: Maplestory Bold/Light와 Pretendard Variable 원본 해시 일치, 16:9 가로 UI의 제목·본문·표 역할 기준 기록
- 16개 seed×30일 전체 Save v5 JSON 시간 분할 결정론과 27년 캠페인용 다중 seed 자율 행동 검증 도구 추가
- 타이틀 MoneyRain 비주얼·렌더러·검증 완료. 전용 검증과 육안 QA는 PASS이며 기존 타이틀 자산을 덮어쓰지 않는다.
- 좌석 배치 순수 규칙, 의미 상태 전용 Save DTO/adapter, seat authoring/registry, 클릭 배치 UI와 ImageGen 패널·상태 마커 3종 완료. 좌석 런타임과 기존 자율행동의 통합은 별도다.
- AutonomyNeeds 순수 모듈 완료. 1/3/10분 진행 동등성, 휴식·크런치·쓰러짐·결근, Save snapshot/transient 분리와 100회 결정론 검증을 통과했으며 기존 `ApplyPulse`와 이중 차감하지 않는다.
- 회복·외부 활동 12종의 SIMUL-v3 ImageGen 16:9 장면과 메타·자산 등록 완료.
- 좌석 애니메이터가 유일한 Sprite writer로 남는 pull-only `OfficeSeatedWorkMicroActionAdapter`와 결정적 프레임셋 주입 경계를 구현했다. 아트가 없거나 일부 동작만 있으면 기존 Work 6프레임으로 복귀하며, micro-action 시간 분할·Drink 하한/간격·기립 handoff·writer rollback 회귀를 고정했다.
- partial 좌석 topology에서 구성원별 실제 claim 가능 여부만 seating gate를 켜고, transition 중 Animator disable/destroy/frame 소실과 GameState session 교체 시 claim·movement writer를 복원하도록 보강했다. 동일 token은 단일 공유 wrapper를 반환하고 NPC approach는 오차 없는 precision path로 도착한다.
- 좌석 배정·앉기·좌석 미세행동·가구 회피 이동·관찰 중심 관리 UI v2·행동/UI 아트를 단일 통합 worktree(`codex/today-integration`)에 결합하고 공유 `Prototype01`에서 실제 PlayMode 30초 전 구간을 통과했다. 계약 작업 → 저에너지 휴게실 회복 → 3인 동시 퇴실 → 복귀 → 전원 사무실 재진입을 1920×1080 캡처 5장으로 확인했다.
- Unity 공식 TMP Essential Resources를 저장소에 포함해 동적 한글 폰트 설정·셰이더가 빌드 PC와 무관하게 준비되도록 했다.
- 관리 UI v2 런타임 폰트 생성 결함을 수정했다. `TMP_FontAsset.CreateFontAsset`가 `fallbackFontAssetTable`을 null로 남기는데 그 위에 `Add`를 호출해 `PrototypeBootstrap.Awake`가 매 프레임 중단되고 있었다(30초 실행에서 NullReference 4,001회). 이 예외가 `ConfigureDisplayDefaults`·`ShowMainMenuNow`·`TryStartFrontendQaCapture`를 통째로 건너뛰게 만들었고, 수정 후 같은 실행에서 0회다.
- 이동이 막힌 프레임에서 속도를 0으로 만들고 영구 정지하던 결함을 수정했다. 막힌 걸음은 의미 목적지 방향을 기준으로 0~180° 미끄러짐 탐색(최소 탈출 보폭 0.06m)으로 강등된다. 정지 프레임이 속도를 0으로 만들면 다음 프레임 변위도 0이 되어 탐색 자체가 불가능해지므로 최소 보폭이 필수다. 이 결함 때문에 엄마·누나가 책상 이탈 직후 좌표에 고정되어 퇴실 시나리오가 실패하고 있었다.
- `CharacterOfficeRuntimeQa`의 PLAY_SNAPSHOT이 좌석 단계·claim·seating clip·work hook·safe-stand·목표 활동을 함께 기록하도록 보강했다. 정지 원인이 좌석 생명주기인지 이동 차단인지 로그만으로 판별된다.
- 기존 `agent/contract-lifecycle-v0-3`의 전체 작업을 `main`에 fast-forward 통합하고 로컬·GitHub 원격 보조 브랜치를 모두 제거했다. 이후 정본 개발 브랜치는 `main` 하나만 사용하며 새 branch나 worktree를 만들지 않는다.

- 콘텐츠 핫리로드 계층 A 구현: 플레이테스트 exe가 빌드 폴더 옆 `FamilyCompany_LiveData` 정션을 통해 프로젝트의 Content JSON을 직접 읽고, 게임 안에서 F5로 다시 읽는다. 외부 폴더가 없으면 빌드 내장 TextAsset으로 되돌아간다. 설계와 남은 계층 B·C는 `Docs/LIVE_PATCH_V1.md`에 있다.

## 진행 중

- Stock 실제 회사/증권계좌 UI와 Save 연결은 완료했다. 남은 범위는 체결별 회사 총계정원장 전기, 외부 생성 tape/orderbook 전체 상태 영속화, 시세·뉴스·유동성 S3와 기업행동 S4의 역사 연결이다.
- OfficeVisualV2는 scale 1.00 정적 교정에서 4명 고유 bbox, 가구 교차 0, 발점 오차 0, IoU 0과 왼쪽 블록 제거를 통과했다. CharacterController 실제 이동과 30초 퇴실/복귀는 2026-08-11 공유 `Prototype01`에서 검증했다. 남은 범위는 1280 정규화 최대 3px를 ≤1px로 낮추는 것과 공유 씬 scale 1.35를 1.00으로 재생성하는 것이다.
- 네 가족 좌석 애니메이션 4명×112=448프레임과 contact sheet 8장·GIF 4개는 검증 완료했고, 기존 Office autonomy·씬의 런타임 좌석 연결도 2026-08-11 완료했다.
- 회복·외부 활동 12장과 순수 규칙을 GameState·Save·실제 선택 UI에 연결하는 통합이 남았다.
- 가족 4인 `OfficeWorkActionFrameSet` 에셋은 `Assets/FamilyCompany/Content/OfficeWorkActions/`에 추가했으나 런타임 훅은 아직 살아 있지 않다. 빌더 검증은 `hook=fallback`, PlayMode 스냅샷은 전원 `hook=False`로 좌석 Work 6프레임 fallback만 재생 중이다. 프레임셋을 실제 세션에 연결하고 8방향 micro-action을 눈으로 확인하는 작업이 남았다.
- 관리 UI v2가 `-batchmode` PlayMode에서 `MANAGEMENT_UI_MISSING_GLYPH: 우리 가족회사`를 1회 남긴다. 같은 저장소에서 `ManagementUiV2Validation`의 한글 글리프 검사는 통과하고 카탈로그도 실제 한글 폰트(Maplestory Bold/Light, Pretendard Variable)를 정확히 참조하므로 배치모드 동적 아틀라스 한정 현상으로 보이나, 실제 Windows player 빌드에서 한글 표시를 확인해야 확정된다.

## 다음 작업

1. `Assets/FamilyCompany/Content/OfficeWorkActions/`의 가족 4인 프레임셋을 좌석 애니메이터 세션에 실제로 연결하고 8방향 micro-action과 안전 기립 handoff를 PlayMode에서 눈으로 검증한다. 현재는 `hook=fallback`/`hook=False`로 좌석 Work 6프레임만 재생된다.
2. 실제 Windows player 빌드에서 관리 UI v2의 한글 표시를 확인해 `MANAGEMENT_UI_MISSING_GLYPH`가 `-batchmode` 한정 현상인지 확정한다.
3. OfficeVisualV2의 1280 정규화 오차를 ≤1px로 낮추고 공유 씬을 scale 1.00으로 재생성한다.
4. Stock 외부 생성 tape/orderbook 전체 상태를 Save 경계에 영속화하고 역사 코퍼스/골든 패리티·다음 거래일 이벤트를 추가한다.
5. 완성된 회복 활동 12장·순수 규칙을 GameState·Save·16:9 선택 UI에 연결한다.
6. 생성 호가 snapshot과 구조적 벽 실제 회복·취소를 Dart golden으로 고정하고 Unity에 이식한다.
7. 시장가/지정가 실제 체결마다 투자자산·수수료·거래세·실현손익을 회사 총계정원장에 전기하고 재고/현금 보존을 검증한다.
8. 시세 경로·뉴스·기술 수준·유동성 구간 S3와 기업행동 S4를 History V1 조건부 사건에 연결한다.
9. 직원 후보 8인을 고용 뒤에만 48프레임 이동 NPC로 생성하고 실제 능력치·업무 배치에 연결한다.

## 검증 기록

- 2026-08-10: 누나 PNG 파일 크기, RGBA, 알파 범위, 모서리 투명도, 피사체 bbox 검사 통과.
- 2026-08-10: Unity 6000.3.21f1에서 Simulation, Save, Infrastructure.Unity, Presentation.Unity, Editor 5개 어셈블리 컴파일 통과.
- 2026-08-10: PrototypeProjectBuilder 실행 통과. Assets/FamilyCompany/Scenes/Prototype01.unity 생성 및 Build Settings 등록.
- 2026-08-10: PrototypeValidation 통과. 시작 나이 14/20/46/44, Dart 호환 RNG 골든값, 이벤트 순서, 시간 진행, 회계 균형, JSON 저장 왕복, 누나 Sprite와 씬 존재 검증.
- 2026-08-10: GPU 배치 렌더 캡처 통과. 집–거리–사무실과 캐릭터 배치를 눈으로 확인함.
- 2026-08-10: Office V0.2 Unity 빌드 통과. 누나 정본 시트를 8개 단일 Sprite로 생성하고 씬에 실제 이동 agent 3명을 연결함.
- 2026-08-10: Office V0.2 헤드리스 검증 통과. 직교 카메라, 픽셀 효과, 8개 프레임, agent별 4개 이상 경로, 누나·직원 A·직원 B 각각 30초 실제 좌표 이동과 정거장 도착을 확인함.
- 2026-08-10: GPU 시각 QA 통과. 접수·업무·회의·휴게·출력 구역, 중앙 통로, 누나 도트, 직원 placeholder 배치를 확인함.
- 2026-08-10: 장기 캠페인, 실제 회사 대체역사, Claude/Codex 전용 경로, `simul` 시장 이식 계획 문서 상호 참조 점검 완료.
- 2026-08-10: 국내 회사 우선 Claude 지시문 작성. `simul` DATA_SOURCES와 시장 사건·코퍼스·기업행동 파일을 읽기 전용 선행 자료로 지정함.
- 2026-08-10: Unity 6000.3.21f1에서 4인 회사 계약 코어 컴파일 및 PrototypeValidation 통과. 모든 결정론적 제안이 4명·80 인시·250만원 상한 안에 있고, 초반 3종은 즉시 수락 가능하며 고급 계약은 평판·연구로 잠김을 확인함. 12명·1000 인시 대형 계약은 TeamTooSmall로 거절됨.
- 2026-08-10: Claude 역사 데이터 작업과 격리한 별도 Codex QA worktree에서 Unity 6000.3.21f1 PrototypeValidation 통과. 계약 수락 착수비, 가족 4명 기여 20인시, 완료 매출·평판, 중복 차단, 기한초과 실패, 장부 균형, 저장 스키마 v2 왕복을 확인함.
- 2026-08-10: 별도 Codex QA worktree에서 Office V0.3 씬 재생성 및 PrototypeValidation 통과. 누나·아빠·엄마 agent, 직원 A·B 부재, 일반 순환 이동, 누나의 계약 출력 지점 실제 이동, 체류 후 4인시 반영과 계약 완료를 확인함.
- 2026-08-10: 별도 Codex QA worktree의 Unity 6000.3.21f1에서 Frontend V0.4 PrototypeValidation 통과. 3개 슬롯의 서로 다른 seed·시간 왕복, backup 생성, 기존 단일 저장 호환, 시작 화면·새 게임·일시정지·재개 상태 전환, 1920×1080·1280×720·전체화면 설정을 확인함.
- 2026-08-10: imagegen 타이틀 키아트가 포함된 Windows Development QA player 빌드 통과. 1920×1080 실제 플레이어 캡처에서 왼쪽 제목·버튼 안전 영역, 오른쪽 20살 누나와 2000년 CRT·전화·팩스 사무실, 글자 잘림 부재를 눈으로 확인함.
- 2026-08-10: 플레이어·사무실 imagegen 크로마 원본을 하드 키로 투명화. 두 정본 모두 RGBA, 알파 0/255, 네 모서리 투명이며 빨간 모자·피부·민트·복숭아 팔레트 보존을 눈으로 확인함.
- 2026-08-10: Office V0.5 빌더 통과. 플레이어 8개 방향 프레임과 사무실 12개 개별 Sprite를 생성하고 플레이어 캡슐을 DirectionalSpriteAnimator 이동 도트로 교체함.
- 2026-08-10: PrototypeValidation 통과. 플레이어 8프레임, 누나 8프레임, 사무실 12모듈, 플레이어 DirectionalSpriteAnimator와 기존 계약·저장·화면 검증을 함께 확인함.
- 2026-08-10: GPU 사무실 개요 캡처 통과. 기존 3D 충돌 사무실과 누나 도트가 유지되고 씬 재생성 결함이 없음을 확인함.
- 2026-08-10: 직원 후보 전신 원화 72종과 정체성 앵커 11종을 `simul` 정본과 SHA-256 비교해 각각 72/72, 11/11 바이트 동일함을 확인.
- 2026-08-10: 직원 8인과 부모 2인의 도트 시트 10종이 1536×1024 RGBA·알파 0/255이고, 80개 모든 셀에 피사체가 존재함을 확인. 부모 전신 원화 2종은 1024×1536 RGBA·투명 모서리 검사 통과.
- 2026-08-10: Korea History V1 validator 통과. 국내 회사 82개, 등록부 83행, 상세 25개, 사건 42개, 후기 앵커 42개, 거시 앵커 14개, 인수 후보 20개, 출처 100개, 오류·경고 0을 확인함.
- 2026-08-10: Dart `order_book.dart` 직접 실행으로 누적 소비 snapshot 사례까지 포함한 `simul_order_book_golden_v1.json` 생성. SHA-256 `1A28D79148B24C9311EA19BFD25C2E51691B1E5B52B0E44FFE204F5273497DD9`.
- 2026-08-10: Unity 시장 S1과 S2 1차 C# 구현. 호가 cadence/frame/capacity, 다단계 fill, 가격 depth walk, split print, 7+7 FIFO/pause 규칙과 Korea History V1 종목 resolver를 PrototypeValidation에서 함께 통과시킴.
- 2026-08-10: SIMUL 누적 소비 snapshot Dart 골든과 Unity 검증 통과. 0.5주 소비 무시, 40주 watermark 재적용 멱등성, 70주 누적 시 30주 delta만 추가 차감, 5주 잔량 행 숨김, 구조적 벽 90% 소비 시 breached 전환·25주 회복 상한을 확인함.
- 2026-08-10: 플레이어 대기주문 순수 C# 코어 Unity 검증 통과. 매수 가격우선, 양방향 시간순 병합, 앞 주문 취소 시 뒤 FIFO `120→100`, 새 주문 queue-ahead `120+20=140`, 5주 부분체결 시 뒤 주문 `120→115`, 2000년 0.5% 수수료 포함 9만원 주문 예약금 90,450원을 확인함.
- 2026-08-10: 가족회사 관리 루프 V0.5 구현. 처음하기는 스토리 없이 임차 오피스텔의 엄마·아빠·나·누나 4인 회사로 시작하며, ImageGen 캐주얼 대시보드 위에 요구 개발/속도·마감·보상·위약금 계약 게시판, 자본 투자형 R&D, 시장 조사·자체 제품 출시를 연결함. 저장 스키마 v3는 시장 보고서·제품 프로젝트 존재 플래그와 구형 V3 유효 데이터 판별을 함께 사용해 빈 객체를 복원하지 않으며 독립 로직 검증을 통과함.
- 2026-08-10: 격리 Unity 6000.3.21f1 프로젝트에서 PrototypeProjectBuilder, PrototypeValidation, ManagementLoopValidation 모두 PASS. History 등록부 83행·국내 82개·2000-01-03 상장 10종목, SIMUL 시장 골든값, 씬의 런타임 카탈로그, 저장 V3 왕복을 한 빌드에서 확인함.
- 2026-08-10: 가족회사 관리 루프 V0.6 확장. 시작 자본을 정확히 500만 원으로 고정하고 웹·소프트웨어, 피처폰·모바일, 하드웨어·PC, 패션·유통·오프라인의 4개 업종과 21개 2000년대형 하청 의뢰를 추가함. 분야별 시장 조사 후 첫 사업 설립, 최대 4개 업종 다각화, 자체 제품 출시, 업종별 채용 직군, 가족 공동 경력 기억·관계 변화, 글로벌 진출 준비도를 연결했으며 저장 스키마를 v4로 올림. ImageGen으로 밝고 캐주얼한 사업 확장 대시보드를 제작해 제품 메뉴에 적용했고, 전체 C# 컴파일(경고·오류 0)과 독립 경제·저장 흐름 검증을 통과함.
- 2026-08-10: 가족 4인과 직원 후보 8인의 고동작 이동 시트 완성. 인물별 A/B 2장에 8방향×6프레임을 배치해 총 24장·576셀을 확보했고, 전부 1536×1024 RGBA·하드 알파 0/255이며 모든 셀에 피사체가 있음을 확인함. 마지막 직원 한수아·오지우·윤채아까지 Unity 메타를 생성함.
- 2026-08-10: 사무실 자율 AI V0.7 구현. 30분 결정론적 행동에서 역할 업무·접수·출력·회의, 휴식·커피·가족 수다, 소파 수면·번아웃 회복을 선택하고 체력·스트레스·관계 기억을 갱신함. 플레이어 학교, 누나 외부 일정, 아빠 대외 영업, 엄마 가사의 가족별 시간대를 계약 가용성과 연결했으며, NPC 3명은 의미 목적지까지 안전 통로를 경유해 실제 이동함. 계약 이동 우선권, 업무 지점 충돌 회피, 색상 상태 라벨, 기간별 가용 인시·최단 완료 플래너를 추가하고 저장 스키마를 v5로 올림. 전체 C# 컴파일 경고·오류 0, 1일 단일/24회 분할 결정론, 7일 장기 범위·휴식, 강제 번아웃 회복, Save v5 왕복 검증을 통과함.
- 2026-08-10: 고동작 시트 24장에서 실제 실루엣 24개씩을 검출해 12명×8방향×6프레임의 256×256 단일 Sprite PNG 576개로 분리. 상체 중심·발 기준선을 정규화해 생성 시트의 비균일 열 간격으로 인한 좌우 흔들림을 제거했고, 재실행 바이트 대조와 전체 C# 컴파일 경고·오류 0을 확인함. 플레이어는 사용자 요청에 따라 모자를 제거한 외형으로 정본 변경.
- 2026-08-10: 자율행동 16시드×30일 장기 검증 통과. 총 업무 블록 38,292개·휴식 1,196회·시드별 이용률 경로 16종을 확인했고, 1일 점프/시간 분할·Save v5·가족 인시 계산도 함께 통과함.
- 2026-08-10: 은행·시설 해금·조직/채용 순수 규칙 코어와 검증 추가. 2000~2024 금리 환경, 이자소득세 15.4%, 법인 신용·DSR·어음할인, 가족 동행/성인 승인 시설 조건, S~F 직원 등급·업종별 직군·인재 네트워크 할인 경계값 검증을 모두 통과함.
- 2026-08-10: `simul`에서 라이선스가 명확한 오디오 50개와 폰트 3개·라이선스 2개를 바이트 무변형 이관. 오디오 집합 SHA-256 `1521D5FD63C7114C6E05368B67AC832EC0E5765525339705198AAD5F4E4CAE3C`, 폰트 집합 SHA-256 `1017B5F9AF25595E322EDA85827BEBF32FCA7D20899AA32D2B9B08D06C76D22A`를 원본과 대조했고, 오디오 런타임 코디네이터를 전체 C# 컴파일에서 확인함. 폰트의 UI 연결은 후속 1920×1080 가로 화면 작업으로 남김.
- 2026-08-10: 자율 AI 후속 검증에서 구성원별 장기간 선처리 때문에 사회 행동 결과가 시간 분할 방식에 따라 달라지는 결함을 발견하고, 모든 가족이 각 30분 경계를 함께 처리하도록 수정함. 16개 seed×30일 단일 점프/시간별 분할의 전체 Save v5 JSON이 일치했고 총 업무 38,292블록·휴식 1,196블록·서로 다른 활용 경로 16종을 확인함.
- 2026-08-10: NPC 외부 일정의 실제 출구 이동·퇴실·복귀, 플레이어의 위치 기반 E 직접 작업, 타이틀/사무실 BGM과 계약·수익·오류·저장·발걸음 SFX를 연결함. SIMUL 오디오 50/50 SHA-256 일치, 전체 C# 컴파일 경고·오류 0, 관리 로직 하네스 PASS를 확인함. 열린 사용자 Unity 때문에 격리 AudioClip 임포트 QA는 환경상 미확인으로 남김.
- 2026-08-10: 사무실 행동 사운드스케이프 추가. 최초 관측/객체 교체는 무음으로 시드하고, 실제 퇴실·복귀·프린터·계약 업무/회의 도착 전이에만 문·종이·환경음을 재생하며 0.55~1.25초 전역 쿨다운으로 동시 폭주를 막음. 순수 전이 9개 시나리오와 전체 5개 C# 어셈블리 컴파일 통과, 실제 플레이 청취 QA는 미실시.
- 2026-08-10: 외부활동 12종과 ImageGen SceneId를 1:1로 묶는 오디오 큐 카탈로그 추가. 각 장면에 진입 SFX·반복 BGM·완료 SFX를 지정하고 조용한 6종/활발한 6종의 볼륨·페이드를 구분함. 12종·36큐·실제 OGG 36/36 존재, ID·반복·볼륨·페이드 경계 검증 통과.
- 2026-08-10: 금융·조직·시설 해금 순수 규칙과 Editor 검증 추가. 2000년 예금 5.5%·무담보 대출 기준 8.5%, 초기 신용점수 650의 12.5% 대출, DSR 40%·어음 할인 골든; S~F 직원 제안과 talent_network 계약금 10% 절감; 은행·R&D·채용·법인계좌·인수합병의 독립 의미 게이트를 검증함. 아직 GameState·Save·UI에는 연결하지 않음.
- 2026-08-10: 급여·인사 순수 규칙 추가. 월 급여를 입력 순서와 무관하게 외부 직원 우선으로 정산하고 비례 배분 뒤 안정 키로 잔여 1원을 나누며, 미지급·연체·크런치에 따른 가족/외부 직원의 사기·충성도·스트레스와 이탈/스카우트 위험을 계산함. 합계·현금 보존, 입력 순서 독립성, 동일 입력 결정론, 위험 경계 독립 하네스 PASS.
- 2026-08-10: 회복·주말 활동 12종과 결정론적 추천 규칙 추가. 동일 seed·분 시각·정렬된 참가자 집합의 추천 재현, 32개 seed 경로 분산, 2001년 노래방·2002년 ADSL 활동의 미래 누설 방지, 효과 0~100 경계를 독립 하네스로 통과함. GameState·Save·UI에는 아직 연결하지 않음.
- 2026-08-10: 회복 활동 순수 실행 판정 추가. 경과 분을 연도·요일로 변환하고 중복/잘못된 참가자·미래 활동·요일·인원·자금 부족을 명시적으로 거절하며, 성공 시 가계 우선/회사 보충 비용·종료 분·참가자별 체력/스트레스·유대 실제 적용량을 반환함. 합계 보존·1원 경계·오버플로·입력 순서 독립·결정론 하네스 PASS.
- 2026-08-10: 회복 활동 추억 순수 규칙 추가. 완료된 12종의 활동·발생 분·정렬 참가자·실제 유대 변화·세계 seed로 불변 MemoryId, 과거형 한국어 회상 3종, 관계 태그와 중요도를 생성함. 동일 입력 100회 일치, 64개 seed 문구 3/3 분산·ID 64/64 고유, 참가자 순서/중복 독립, 미래 ADSL 활동 거절 검증 통과.
- 2026-08-10: Maplestory Bold/Light·Pretendard Variable과 라이선스 원본 2개를 SHA-256 5/5 일치로 이관하고 GUID 중복 0을 확인함. 세로형 SIMUL 줄바꿈을 재사용하지 않고 1080p 본문 최소 18px·일반 20~24px의 가로 UI 역할을 기록함.
- 2026-08-10: 격리 Unity 6000.3.21f1에서 HighMotion ConfigureAll과 Office 빌더로 실제 `Prototype01.unity`를 재생성하고 공유 씬에 반영함. 플레이어·누나·아빠·엄마 각각 48개 non-null/unique 정본 Sprite, 부모 `DirectionalSpriteAnimator` nonzero 연결, placeholder·고용 전 후보 0개, Desk C/D 북측 안전 접근점 z=-1.25, 단일 의미 출구 존재를 직렬화 파일에서 재확인함. 공유 씬 SHA-256은 `B5819E0FFDC0571AAB803FAF980F588FCF32AFB2F6DE38D46E9A6AF323CE0CA2`.
- 2026-08-10: 1920×1080 격리 PlayMode를 벽시계 30초 동안 실행해 계약 업무 3명 우선 배정·서로 다른 업무점·완료 뒤 자율 목적지 재개, 강제 저체력/고스트레스 휴게실 회복, 23시 NPC 3명 동시 퇴실과 출구 슬롯 `(z=0/-0.65/-1.30)` 숨김, 다음날 08시 동시 복귀를 좌표·방향·6프레임 로그와 PNG 5장으로 확인함. 전 목적지 도착 QA와 기존 전체 `PrototypeValidation`, 외부 전체 C# 컴파일도 PASS(경고·오류 0).
- 2026-08-10: OfficeVisualV2 1920×1080 base/foreground/guide를 실제 사무실에 통합함. base GUID `f8060cfe4ff136b33846978ff37a0d0c`, foreground GUID `b3354fa83236b9cafedd5746b2ed793f`, guide GUID `cf191dbb4781f4383e9eca7ffea4f5ac`를 유지하고 base/foreground를 Sprite·Point·mipmap 없음·무압축으로 임포트함. base가 있을 때만 기존 Primitive Renderer를 숨기고 Collider 20개와 waypoint는 유지하며, 에셋이 없거나 사무실 밖이면 블록아웃을 다시 표시하는 폴백을 연결함.
- 2026-08-10: OfficeVisualV2 카메라를 16:9 전용 정면 사무실 투영, orthographic size 6.6, 720p 내부 360/1080p 내부 540의 정수배 Point 렌더로 조정하고 가족 도트 시각 스케일을 1.35로 고정함. 1280×720·1920×1080 실제 캡처에서 비청록 점유 99.9%, 원본 office 실내 폭 약 86%, Primitive 비노출, foreground occlusion, 캐릭터 크기를 직접 확인함. 공유 씬 SHA-256은 `1477A8D5BD3F2D12807E67DF347D17605ADEB7A994C8439094BFFDBE1A2FFA56`.
- 2026-08-10: OfficeVisualV2 포함 격리 PlayMode 30초 회귀와 전체 `PrototypeValidation` PASS. 계약 우선/자율 재개, 회복, NPC 3명 동시 퇴실·숨김·복귀가 유지됐고 1280×720·1920×1080 최종 캡처를 남김. Office 범위 분리 컴파일은 경고·오류 0이며, 현재 공유 저장소 전체 외부 컴파일의 오류 6개는 동시 작업 중인 `StockMarketFullscreenPanel`의 `_orders`/`UiOrderRecord` 미완성 참조라 Market 담당 완료 후 재검증 대상으로 분리하고 해당 파일은 수정하지 않음.
- 2026-08-10: 위 OfficeVisualV2 `1.35` 시각 판정을 실제 캡처 교차 계측 결과에 따라 **시각 FAIL로 폐기**함. 실패 캡처 1047×537에서 어머니·누나가 Desk C/D와 각각 100%·97.3%, 아버지가 회의 가구와 100% 교차했고 플레이어 고유 bbox가 없었음. 4개 책상 중심 호모그래피, 1920 기준 안전 발점, 16:9 내부 crop `(254,79,1666,937)`, 스케일 1.00 기준 투영 코드를 추가했으며 Office 범위 분리 C# 컴파일은 경고·오류 0. 공유 씬/waypoint는 아직 재생성하지 않았고 0.95/1.00/1.05 contact sheet, 1280·1920 실제 캡처, 발점·bbox·가구 교차, 30초 퇴실 회귀는 미실시이므로 시각 PASS가 아님.
- 2026-08-10: 좌석 배치 Rules/Save DTO/Authoring/클릭 UI/ImageGen 패널·마커 정적 검증 완료. 좌석 ID 중복·빈 ID·예약 충돌·결정론·Save v5 missing field·실제 투명 RGBA와 GUID를 확인했으며 캐릭터 착석/기립·씬 통합은 아직 PASS가 아님.
- 2026-08-10: AutonomyNeeds 순수 모듈 netstandard2.1 컴파일 경고 0·오류 0, 순수 하네스 PASS. 1/3/10분 동등성, 휴식·크런치·쓰러짐·결근, Save snapshot/transient 분리, 100회 결정론을 확인했으며 기존 FamilyMemberState·AutonomousOfficeSimulation·Save·씬은 수정하지 않음.
- 2026-08-10: MoneyRain 전용 검증과 시각 QA PASS.
- 2026-08-10: Stock 가로 화면 diff check, C# 경고 0·오류 0, 순수 회귀와 Unity QA build PASS. Maplestory Light/Bold 실제 로드, Pretendard 폴백, ImageGen skin, 한글 11,172 glyph, 실제 종목·버튼·호가·체결 clipping 0을 확인했고 1280×720·1920×1080·3440×1080과 최소 568×843 안내 캡처를 통과함.
- 2026-08-10: Stock 12.016초 실측에서 5분 모드 +55분(허용 프레임 1회 오차), 15분 +180분, 50분 +600분과 잔여 0.4초 보존을 확인함. 08:59:59 주문접수 무체결, 09:00 단일 개장 1회·2체결·21,100원, 09:00:01 중복 0, 09:01 정규장 및 5/15/50 경계 동일 시초가를 확인함.
- 2026-08-10: 네 가족 OfficeSeatingV1 4명×112=448프레임 완료. sit_down 128·work 192·승인된 역순 stand_up 128, contact sheet 8장, GIF 4개, frame meta 448, source PNG/meta 32/32를 확인함. 256 RGBA hard alpha·빈 프레임/잘림 없음·발 y248·180 PPU bottom-center·Point/no mip/uncompressed·GUID 유일·work A/B 높이차 ≤0.5px·재현 해시와 육안 QA PASS. 런타임 좌석 연결은 미완료.
- 2026-08-10: Stock 회사계좌·Save V5 optional 코어 완료. 신규 회사 현금 500만원·증권 미개설/0원, 양방향 균형분개, 장중 입출금과 예약금 보호, 잘못된 금액·중복 ID 차단, 현금/원장/예수금/포지션/평균원가/미체결/체결/일지/관심/FIFO/세션·개장 상태의 왕복 및 구형 safe restore를 검증함. 전체 C# 경고 0·오류 0, 회귀·Unity build·1280 runtime PASS.
- 2026-08-10: Stock 회사계좌 canonical 통합 완료. 50,000원 fixture 제거, 실제 회사↔증권계좌 입출금·균형분개·예약금 보호, `GameState` flush/load·Save V5 optional, 날짜별 상장집합 변경 시 비거래 종목 승계와 미지 ID 거절, 0.4초 residual 및 FIFO 재개 결정성을 Unity `STOCK_MARKET_*_VALIDATION` 4종과 전체 `PrototypeValidation`에서 함께 통과함. 외부 생성 tape/orderbook 전체 영속화와 S3/S4는 미완료로 유지함.
- 2026-08-10: seating 6개 선형 커밋과 micro-action 런타임을 전용 통합 브랜치에 결합하고 pull-only adapter를 추가함. 아트 없음·부분 아트 fallback, 8방향 첫/끝 프레임, 구성원 불일치, safe-stop/disable/destroy 멱등성, push Presenter 시작/프레임 소실 예외 rollback을 독립 하네스로 통과함. 30분 단일/1초 분할 타임라인 일치, 첫 Drink 300초 이후, 모든 `int` seed에 대해 구조적으로 30분 5회 이하인 간격과 범위 분산 4,096 seed·지정 가족 4명 표본, huge delta guard를 확인했으며 전체 5개 어셈블리 외부 컴파일은 경고·오류 0. Unity와 실제 아트는 실행하지 않음.
- 2026-08-10: seating 독립 리뷰 MAJOR 5 회귀를 보강함. Desk C/D 누락 시 father/player gate 해제와 기존 생산성 진행, transition 중 Animator lifecycle/frame-loss 취소, 동일 topology의 새 게임/불러오기 GameState identity rebind, 동일 seat/member/token 단일 wrapper 공유, NPC approach precision exact settle을 standalone 하네스와 통합 source guard로 확인함. 전체 5개 어셈블리 외부 컴파일은 `58/6/3/40/36` 소스, 경고·오류 0이며 Unity/PlayMode는 실행하지 않음.
- 2026-08-11: 통합 worktree `codex/today-integration`에서 Unity 6000.3.21f1 검증 4종 모두 exit 0. `PrototypeValidation.Run` → `FAMILY_COMPANY_VALIDATION: PASS`, `OFFICE_SEATING_BUILDER_VALIDATION: PASS components=4 seats=4 frames=448 hook=fallback`, `SCENE_LINKAGE_PASS family=4 npcAgents=3 framesPerFamily=48 candidates=0`, `OFFICE_VISUAL_V2_ASSET_READY_PASS colliders=24 occupancy=90.4%`. `ManagementUiV2Validation.RunFromCommandLine` → `MANAGEMENT_UI_V2_VALIDATION: PASS`(TMP Settings 존재와 한글 글리프 포함 검사 포함). `ContractBoardUiArtValidation.ValidateOrThrow` → PASS.
- 2026-08-11: `CharacterOfficeRuntimeQa.StartThirtySecondPlayModeBatch` 실제 PlayMode 30초 `PLAYMODE_PASS`(exit 0, timeScale 4, 1920×1080). 정적 구간은 가족 4인 각 48프레임 전부 고유·bottom-center pivot, 카메라 기준 8옥탄트, 6단계 애니메이션, 통로 3·approach 6·목적지 9, NPC 3인 전원 desk/reception/printer/meeting/lounge/exit/return 도달 가능, 자율 분기 recovery=lounge·schedule=exit·return=in-office를 통과했다. 실행 구간은 `PLAYMODE_CONTRACT_PRIORITY_PASS assigned=3 distinctWorkpoints=3` → `PLAYMODE_RECOVERY_ROUTE_PASS` → `PLAYMODE_DEPARTURE_ROUTE_PASS minute=900` → `PLAYMODE_RETURN_PASS minute=1440` → `PLAYMODE_ALL_RETURNED_PASS positions=inside-office` → `PLAYMODE_FINAL_PASS contractResume=3 departureReturn=observed`이며 캡처 5장은 `Artifacts/CharacterOfficeRuntimeQa/`에 있다.
- 2026-08-11: 위 PASS 이전 두 차례 실패의 실제 원인을 고쳤다. 첫 실패는 `ManagementUiV2Presenter.LoadFonts`가 null `fallbackFontAssetTable`에 `Add`를 호출해 NullReference 4,001회로 `PrototypeBootstrap.Awake`를 중단시킨 것이고, 두 번째 실패는 이동 차단 프레임이 속도를 0으로 만들어 엄마·누나가 책상 이탈 직후 `(14.59,3.46)`·`(11.42,3.48)`에 영구 고정된 것이다. 도착 판정이나 검사 기준을 느슨하게 바꾸지 않고 폰트 폴백 테이블 초기화와 미끄러짐 강등으로 해결했다.
- 2026-08-11: 재현 명령. 검증 3종은 `Unity.exe -batchmode -nographics -quit -projectPath <worktree> -executeMethod <Method> -logFile Logs\<name>.log`. PlayMode QA는 캡처 때문에 `-nographics`와 `-quit` 없이 `Unity.exe -batchmode -projectPath <worktree> -executeMethod FamilyCompany.Editor.CharacterOfficeRuntimeQa.StartThirtySecondPlayModeBatch -logFile Logs\character-office-playmode.log`로 실행한다.
- 2026-08-11: Unity 없이 실행되는 Windows 플레이테스트 빌드 절차를 `Docs/PLAYTEST_BUILD.md`로 문서화했다. 빌드 산출물 201.8MB는 되돌릴 수 없는 히스토리 비대화를 피하려고 Git에 넣지 않는다. 현재 `Downloads/FamilyCompany_Playtest`의 EXE는 커밋 `d07638a` 기준이라 오늘 통합과 두 결함 수정이 들어 있지 않으므로, 오늘 작업을 실제 EXE로 확인하려면 정본 폴더에서 재빌드가 필요하다.
- 2026-08-11: 미해결 관찰. PASS한 PlayMode 로그에도 `MANAGEMENT_UI_MISSING_GLYPH: 우리 가족회사`가 1회 남는다. 별도로 `UnityEditor.Search.SearchDatabase.GetDefaultSearchDatabase`의 `ArgumentOutOfRangeException`이 1회 나오지만 이는 에디터 검색 인덱서 내부 문제로 프로젝트 코드와 무관하다.
- 2026-08-11: 정본 작업 폴더에서 원격 최신 `d74f29e`를 받은 뒤 `main`과 `origin/main`을 같은 커밋으로 맞췄다. Korea History V1 validator를 다시 실행해 국내 회사 82개·등록부 83행·2000~2003 상세 25개·사건 42개·진입/퇴출 앵커 42개·인수 후보 20개·출처 100개와 오류 0을 확인했다.
- 2026-08-11: `main` 정본 작업 폴더에서 Unity 6000.3.21f1 `PrototypeValidation.Run`을 재실행해 `OFFICE_VISUAL_V2_ASSET_READY_PASS`(colliders=24, occupancy=90.4%), `SCENE_LINKAGE_PASS`(family=4, npcAgents=3, framesPerFamily=48), `OFFICE_SEATING_BUILDER_VALIDATION: PASS`(components=4, seats=4, frames=448, `hook=fallback`), `FAMILY_COMPANY_VALIDATION: PASS`와 종료 코드 0을 확인했다.
- 2026-08-11: 콘텐츠 핫리로드 계층 A 실제 exe 검증 통과. Windows 플레이테스트 빌드에서 `[LiveContent] 등록부 83행 · 외부 파일`을 확인했고, 환경 변수 `FAMILYCOMPANY_LIVE_CONTENT`로 회사 1개를 뺀 사본을 지정하자 같은 exe가 `82행 · 외부 파일`로 바뀌어 외부 JSON을 실제로 읽는 것을 증명함. 정션을 치우면 로그가 사라지고 예외 0으로 내장본을 쓰는 폴백도 확인함. 정션은 빌드 출력 폴더 밖이라 재빌드 후에도 생존함. PrototypeValidation과 전체 컴파일은 오류·경고 0.
- 참고: 플레이테스트 빌드는 `WindowsPlayerBuild.cs`가 비-Development로 강제하므로 `DEVELOPMENT_BUILD` 심볼로 감싼 코드는 exe에서 컴파일되지 않는다. 개발용 기능은 컴파일 심볼 대신 외부 폴더 존재 같은 런타임 opt-in으로 게이트한다.
- 참고: -nographics에서 Camera.Render를 호출하면 Unity 네이티브 렌더러가 충돌하므로 시각 캡처에만 -nographics를 쓰지 않는다. 일반 빌드와 로직 검증에는 -nographics를 계속 사용한다.

### 2026-08-10 OfficeVisualV2 calibration handoff

- Isolated Unity captured 1280x720 and 1920x1080 at character scales 0.95/1.00/1.05. Scale 1.00 is the selected candidate.
- Calibrated art feet are Desk A `(814,500)`, Desk B `(1103,500)`, Desk C `(650,820)`, and revised safe Desk D `(1105,890)`; the capture log reports zero transform-anchor error at both resolutions.
- The OfficeVisualV2 camera now renders only the base/foreground and character visual children on presentation layer 31. Physics roots and colliders remain on their original layer. This removes the left Street block without changing Collider20 navigation.
- Legacy management HUD drawing is suppressed while enhanced OfficeVisualV2 presentation is active. Blockout renderers remain available as a fallback when the art is unavailable or the camera leaves the office.
- Desk C/D use staged side approaches, and shared Meeting/Lounge slots fan out only along their measured safe axis. Printer, reception, and exclusive desks preserve their exact calibrated foot point.
- Final isolated artifacts: `work/office-visual-v2-isolated/Artifacts/OfficeVisualV2Qa/` and `work/office-visual-v2-isolated/Artifacts/OfficeVisualV2CalibrationQa/calibration-qa.txt`.
- Independent visual QA approval and the 30-second simultaneous departure/return regression are still pending. `Prototype01.unity` has intentionally not been regenerated or copied to the shared scene.

## 차단 요소

- 누나 이름은 미정이며 내부 ID older_sister를 사용한다.

## 2026-08-11 Office Tile Migration T1~T3

- T1 완료: 순수 C# `OfficeGrid`에 13×13 바닥·통행 배열, 배치 가구·좌석 의미 스키마, 결정론적 레이아웃 해시를 추가했다. GameState/Save를 v6으로 올리고 v1~v5는 초기 13×13 격자로 복원한다. Unity `OfficeGridValidation`에서 169셀 무결성, void/walkable 모순 거절, 가구·좌석 왕복, v5 이관, 저장 전후 해시 일치를 통과했다.
- T2 완료: OpenAI 내장 ImageGen으로 밝은 2000년대 우드 바닥 3종 원본을 만들고 320×160 RGBA 하드 알파·180 PPU·Point·mipmap 없음·무압축 Tile 자산으로 정제했다. Unity 내장 Isometric Grid/Tilemap이 13×13 169셀을 렌더하며 16:9와 4:3에서 네 모서리를 보존함을 통과했다.
- T3 완료: 격리 `OfficeTileMigrationPreview` 씬에서 플레이어·누나·아빠·엄마가 서로 다른 walkable 경로를 실제 PlayMode Update로 4초간 각 6.965 units 이동했다. `(6,6)` 막힌 칸 거절, 8방향×6프레임 애니메이션, x+y 동적 정렬, 누적 균등 스케일 1.690을 확인했다. 렌더 bounds 비율은 전원 0.1775, 실제 알파 실루엣 비율은 플레이어 0.1477·누나 0.1511·아빠 0.1581·엄마 0.1726이다.
- 캡처: `Artifacts/OfficeTileMigrationQa/office-tile-t3-1920x1080.png`, SHA-256 `906A5A830198F647F8EFED2376309711664A68DFB33F5E959B9C8A8D083C39B8`.
- 회귀: Unity 6000.3.21f1 `PrototypeValidation.Run` 종료 코드 0, `FAMILY_COMPANY_VALIDATION: PASS`. 기존 OfficeVisualV2·3D Collider·좌석·계약은 수정·제거하지 않고 폴백으로 유지한다. 다음은 사용자 캡처 승인 뒤 T4 가구 12종 이관이며, 승인 전에는 진행하지 않는다.

## 2026-08-11 Office Tile Migration T4~T5

- T4 완료: OpenAI 내장 ImageGen으로 한 이미지에 한 소품만 담은 2000년대 등각 도트 가구 12종을 새로 만들었다. 투명 원본 12/12는 잘림·이웃 물체·불투명 마젠타 테두리 0이며, Unity 런타임은 640×512 하드 알파·180 PPU·Point·mipmap 없음·무압축이다. 12종 모두 visible bounds에 X/Y 동일 배율만 적용하며 종류별 실제 ground anchor를 pivot으로 사용한다.
- 13×13 프리뷰에 가구 18개·12종을 배치했다. 책상+CRT 4개는 2×1 막힘, 회전의자 4개는 1×1 통행 가능 좌석이며 접수대·회의 탁자·문서 책장·팩스/복사기·정수기·소파·커피 테이블·화분·파티션·서류 캐비닛을 함께 배치했다.
- Office Tycoon Alignment V1 완료: `OfficeFurnitureVisualCatalog`가 12종의 ground/sort와 의자 seat·책상 work-surface 앵커를 소유한다. 가구 의미 root는 footprint 중심·scale 1에 고정되고 `BaseVisual`/`FrontOverlay`만 균등 scale로 렌더한다. v3 의자와 v4 책상의 앞면은 고정 Y 절단 대신 명시적 픽셀 마스크 Sprite다.
- T5 완료: `OfficeSeatSlot` 저장 서브스키마 v2에 chair/work-surface/seat/approach/facing 관계를 명시하고 v1 이관을 유지한다. 캐릭터는 approach cell까지 이동한 뒤 좌석 셀로 정밀 이동하며, 의미 root는 셀 중심·scale 1에 고정한다. 자식 `VisualRoot`만 가족·방향별 pelvis↔seat 앵커로 보정하고 일어서면 0으로 복원한다. 기존 `OfficeSeatRuntimeClaim`으로 예약·점유·해제를 수행한다.
- QA: Unity 6000.3.21f1 `OfficeGridValidation.RunBatch`와 그래픽 `OfficeTileMigrationQa.StartT4T5Batch` 종료 코드 0. 45초에 네 가족 전원 일어서기→approach 이탈→claim 해제→재착석을 수행하고, 60초에 가구 18개의 position/rotation/scale/parent 정확히 0 변화, 12종 ground error 0.000px, 네 가족 pelvis↔seat 0.000px, desk-chair centerline 0.000px, VisualRoot 원복 0.000000, 막힌 칸 침범·좌석 중복 0을 확인했다. Unity SearchDatabase의 기존 `ArgumentOutOfRangeException` 1회는 프로젝트 코드와 무관한 에디터 인덱서 문제로 유지된다.
- 캡처: 기존 T4/T5 3장과 함께 `Artifacts/OfficeTileMigrationQa/after-office-tile-tycoon-overview-1920x1080.png`, `after-office-tile-tycoon-seated-1920x1080.png`, `after-office-tile-tycoon-anchors-1920x1080.png`, `after-office-tile-tycoon-occlusion-1920x1080.png`, 수치 보고서 `office-tile-tycoon-alignment-report.txt`를 남겼다.
- 현재 경계: T4~T5는 `OfficeTileMigrationPreview` 격리 씬에만 있다. 현재 플레이테스트 EXE와 `Prototype01`은 여전히 OfficeVisualV2 폴백을 사용한다. 다음 작업은 사용자 캡처 확인 후 T6에서 이 레이어를 메인 사무실에 연결하고 계약·자율 AI 회귀를 다시 실행하는 것이다. A*와 자유 배치 UI는 이번 범위가 아니다.

## 2026-08-11 Office Tycoon Alignment V2 진행 상태

- V1 순환 검증을 교체했다. 기존 화면에서 새 hand-to-work 실패 조건을 먼저 실행해 player 1.745px·older_sister 2.703px·father 9.059px·mother 10.128px을 계측했고 father에서 의도대로 FAIL했다. 기준 로그는 `Artifacts/OfficeTycoonAlignmentV2/unity-v2-baseline-fail.log`다.
- 실제 초기 레이아웃 `CreateStarterOfficeV1()`을 추가하고 GameState 기본값과 v1~v5 save 이관을 Starter로 바꿨다. Migration Preview는 파티션을 포함한 T1~T5 fixture로만 유지한다.
- `OfficeWorkstationSlot`, 반 셀 `OperatorAnchor`, officeGrid schema 3의 `operatorX2/Y2`, 가구 네 점 footprint, desk operator seat/work socket, member/direction/clip/frame pose 56개를 추가했다. 두 calibration catalog는 version 2다.
- 의자 좌판 중심을 runtime `(313.007,153.549)`, desk operator seat를 `(390.445,49.329)`로 다시 승인했다. 당시의 넓은 chair front overlay는 몸을 과도하게 덮어 제거했다. `08d398b`에서는 등받이·근접 팔걸이만 남긴 제한 전경을 다시 연결해 의자 관통을 막는다.
- `OfficeTycoonAlignmentCalibrationWindow`와 전용 `OfficeTycoonAlignmentV2Qa`를 추가했다. V2 QA는 Preview 45초 + Starter 60초, stand/reseat, 1920×1080 6장, footprint/socket/pelvis/hand/frame/mask/Transform/collision/claim/save 표를 검사한다.
- Simulation/Save/Presentation/Editor Roslyn 컴파일은 오류 0이다. 오프라인 4인 합성에서 엉덩이-좌판 중심과 등받이 뒤 배치를 육안 확인했다.
- 오후 Unity 라이선스가 다시 인식돼 실제 PlayMode까지 진입했다. `OfficeGridValidation`, 타일 빌드, 가구 V2 결정론 빌드, calibration asset 비파괴 검사는 PASS했고 Preview 18개 가구의 persisted four-corner residual은 모두 0.000px였다. 이 수치만으로 실제 다리·바퀴 접점을 육안 승인하지 않는다.
- 실제 45+60초 Unity 그래픽 QA를 끝까지 실행했다. 가구 의미/시각 Transform 변화 0, blocking 침범 0, 좌석 claim 4개 고유, 네 명 stand/reseat, 저장 왕복 해시를 통과했고 1920×1080 진단 캡처 6장을 생성했다. 최종 결과는 아래 손 정렬 때문에 의도적으로 FAIL이다.
- **당시 미완료/차단:** 실제 NorthWest work pose의 공용 desk 대비 1920×1080 손 오차는 player 8.032px, older_sister 11.906px, father 12.336px, mother 13.322px이다. 가족별 pelvis→hand 방향도 0.349°/2.754°/9.414°/13.676°로 달라 좌석별 offset 없이 한 소켓을 동시에 맞출 수 없었다. 이 과거 차단은 아래 Seated Sprite Root Cause V3와 2026-08-12 재검증에서 해소됐으며 현재 차단이 아니다. 상세는 `Docs/OFFICE_ALIGNMENT_V2.md`다.

## 2026-08-11 / Starter Office Runtime V1 통합

- `StarterOfficeRuntimeBootstrap`이 `GameState.OfficeGrid`를 직접 렌더링하고 가족 네 명의 유일한 화면 Actor를 생성한다. Starter Office는 더 이상 하드코딩 Preview 캐릭터 월드가 아니다.
- 실제 계약·자율 행동·플레이어 입력은 `IOfficeRuntimeAgent`를 통해 같은 네 Actor에 연결된다. Legacy `OfficeWorkerAgent`, `PrototypePlayerController`, `OfficeNavigationWorld`, Preview mover는 Starter Runtime 활성 중 Update하지 않는다.
- `OfficeRuntimeOccupancy`는 Static Hard, Interaction Seat, Dynamic Actor/Reservation을 분리한다. 반경·연속 구간 검사·고배속 substep·결정론적 교통 양보·재탐색을 적용한다.
- 이동 Sprite 방향은 의도 속도가 아니라 실제 tile displacement로만 바뀐다. 정지 프레임은 마지막 방향을 유지한다.
- `StarterOfficeV1.asset`과 `OfficeLayoutEditorWindow`가 의미 배치의 정본이다. 렌더·충돌·좌석·저장은 같은 `OfficeGrid` 해시를 사용하며 Presenter의 visual-only 의자 이동은 제거했다.
- Save OfficeGrid schema는 placement subcell anchor를 보존하는 v4다. v1~v3은 footprint 중심에서 anchor를 이관한다.
- 자동 실행 QA 진입점 `-familyCompanyTileRuntimeQa`는 단일 Actor 소유권, 4인 십자 이동, 좁은 통로, 실행 중 책상 추가/제거, 4배속 플레이어 충돌, 실제 8방향, 네 좌석, 계약, 슬롯 저장/불러오기를 `Prototype01`에서 연속 검증한다.
- Unity 6000.3.21f1의 실제 Simulation/Save/Infrastructure/Presentation/Qa.Core/Editor compiler response로 전체 컴파일 오류 0을 확인했다.
- Downloads의 `FamilyCompany_BuildAutomation`이 `FamilyCompany_Playtest`를 최신 fingerprint로 빌드했고, 실제 Windows 실행본 `-familyCompanyTileRuntimeQa` Main Flow가 PASS했다. 십자 교차·좁은 통로·런타임 책상 추가/제거·4× 플레이어 책상/카운터/NPC 충돌·8방향·네 좌석·계약·저장/불러오기에서 hard/interaction 무단 통과와 agent penetration은 모두 0이다.
- 네 워크스테이션의 배치 오차는 chair↔desk `0.000px`, pelvis↔seat `0.000px`다. b53c355 당시 사용한 pose v3 scale/rotation은 아래 Seated Sprite Root Cause V3에서 폐기하고 v4 safe profile로 교체했다.

## 2026-08-11 / Seated Sprite Root Cause V3 완료

- b53c355의 pose v3는 56개 반복 프로필에 10.81~27.55% 확대와 최대 13.68° 회전을 저장해 착석 얼굴·몸·다리를 왜곡했다. 실패 기준은 `Artifacts/SeatedSpriteRootCauseV3/b53c355-baseline-fail.txt`에 고정했다.
- `DirectionalSpriteAnimator`가 Sprite를 적용한 뒤 pose 이벤트를 발행하고, `OfficeRuntimeAgent`는 승인된 pelvis→chair seat translation만 적용한다. `VisualRoot.localRotation=identity`, pose scale `1.0`이며 앉기/일하기 내부 상태와 무관하게 safe 화면은 `NorthWest/Work/0`을 유지한다.
- `OfficeCharacterSeatPoseCatalog`를 v4로 올리고 네 가족 safe profile 4개만 `HumanApproved + source Sprite SHA-256`으로 승인했다. 구형 v3는 자동 승인·자동 덮어쓰지 않는다.
- 엄마 safe 원화는 정체성·의상·포즈를 유지한 최근접 이웃 정규화로 visible height 차이를 9.58%에서 5.00%로 줄였고 SHA와 실제 pelvis/hand anchor를 다시 승인했다.
- `OfficeRuntimeDepthSorter`가 전체 가구 footprint와 Actor를 한 번에 정렬하고, 착석 시 chair base < character < chair front 순서를 보장한다. tile runtime의 legacy order `100` 사용은 0이다.
- Unity 6000.3.21f1 V3 정적 QA는 전원 rotation `0°`, scale `1.000`, pelvis↔seat `0px`, hand↔work `0.538px`, visible height 차이 `1.37~5.00%`, 승인 SHA 일치로 PASS했다.
- 최신 Downloads Windows 빌드의 `-familyCompanyTileRuntimeQa` Main Flow가 PASS했다. 단일 Actor·가구 revision·충돌·8방향·네 좌석·계약·저장 회귀와 가족별 1024×1024 클로즈업은 `Artifacts/SeatedSpriteRootCauseV3/`에 남겼다.
- 기존 desk front 마스크가 older_sister의 upper-body 18샘플을 덮는 실패를 재현한 뒤 재생성 exclusion으로 제거했다. 최종 그래픽 45+60초 QA는 얼굴 overlap 전원 0, 하체 overlap 전원 양수, 가구 Transform 변화 0, 전원 stand/reseat와 저장 왕복까지 PASS했다.

## 2026-08-12 / Mother Northwest Work 하체 복원 및 실제 플레이어 재검증

- `mother_northwest_sit_work_0..5.png` 6장을 내장 ImageGen 편집으로 재생성했다. 원본에서 잘렸던
  무릎·종아리·양발·갈색 사무화를 전부 복원했고 256×256, visible height 228px, alpha 0/255,
  발바닥 하단 여백 7px로 통일했다. frame 0의 5% 과대 크기도 제거했다.
- 엄마 frame 0의 승인 좌판 등록점/손 접점을 `(131,62)` / `(90,120)`으로 확정하고 source SHA를
  `1F8D8A29...E54FF7`로 갱신했다. 자동 해부학 후보 `(149,75)`는 실제 chair sprite 합성에서 몸을
  좌판 밖으로 밀어내므로 폐기했다.
- Unity `6000.3.21f1` 백그라운드 컴파일에서 `6299cd9`에 남아 있던 폐기 메서드 호출 1곳,
  Editor QA의 `OfficeGrid` 타입/네임스페이스 충돌 7곳, edit mode의 `DontDestroyOnLoad`/비동기 scene
  load를 수정했다. 최종 `PrototypeValidation`은 컴파일 오류·예외 없이 `FAMILY_COMPANY_VALIDATION: PASS`다.
- 정식 Windows x64 빌드는 warning 0으로 성공했고 `Downloads/Family/FamilyCompany_Playtest`에 승격했다.
  이전 실행본은 `FamilyCompany_Playtest.previous.20260812-033231`에 복구용으로 보존했다. 숨김 창 `-batchmode
  -familyCompanyTileRuntimeQa`는 8방향 이동, 사거리 교차, 좁은 통로, 런타임 책상 추가/제거,
  책상·카운터·NPC 충돌, 네 좌석, 계약, 저장/불러오기를 종료 코드 0으로 통과했다.
- 실제 플레이어 착석 수치: 네 가족 seatContact `0.000px`, rotation `0°`, scale deviation `0%`.
  엄마 character sorting `1008`, chair base `1007`, desk `1005`이며 의자 전면 레이어는 인물 위다.
  캡처·로그는 `Artifacts/MotherSeatedRegenQa/`에 남긴다.
- 2026-08-12 후속 감사에서 이 경계를 해제했다. `OfficeCharacterSeatPoseCatalog` v5는 네 가족의
  `Northwest` SitDown 4 + Work 6 + StandUp 4, 총 56개를 실제 pelvis/hand와 source SHA로 승인한다.
  Starter Runtime은 완전한 56개일 때 Animated를 사용하고, 불완전하면 Work/0 정적으로 fail-closed한다.
- 숨김 Windows 플레이어에서 전원 4/4·6/6·4/4 실제 프레임 적용, seat/animated anchor error `0.000px`,
  rotation `0°`, scale deviation `0%`, agent penetration `0`, Main Flow 종료 코드 `0`을 확인했다.

## 2026-08-12 / Movement & Seating Audit 1차 방향·충돌 수정

- Downloads의 `FAMILY_COMPANY_MOVEMENT_SEATING_AUDIT_2026-08-12.md`를 현재 `main`과 대조했다.
  감사 기준 `f8d7d82` 이후의 레이아웃 편집기 변경은 이동 핵심 코드를 바꾸지 않았으므로 P0 원인이
  현재 런타임에도 남아 있음을 확인했다.
- `OfficeRuntimeWorld`가 렌더 프레임 시작에 네 Actor의 표현 누적을 열고 모든 0.05초 이하 이동 substep의
  의미 변위·실제 변위·시간·충돌 투영 여부를 합산한 뒤 한 번만 Animator에 적용한다. 보행 속도는 더 이상
  한 substep의 변위량이 아니라 `렌더 프레임 실제 이동거리 / 누적 시간`이다.
- 방향 표현은 4° hysteresis와 0.075초 후보 안정화를 사용한다. 충돌로 축 투영된 첫 0.15초와 직접 조작의
  급반전에서는 의미 heading을 표시하되, 위치·이동 여부·보행 속도는 계속 실제 변위만 사용한다.
- 충돌 시 무조건 X축부터 시도하던 순서를 제거했다. 의미 목적지 진행량, 직전 실제 이동 축 연속성,
  agent ID 기반 결정론 tie-break로 X/Z slide 중 하나를 고르며 둘 다 막히면 정지한다. 직접 조작 플레이어의
  정지/135° 이상 반전 감속률만 높이고 NPC 적분률은 유지했다.
- Unity 6000.3.21f1 백그라운드 `PrototypeValidation`과 `OfficeNavigationValidation`이 PASS했다.
  회귀 수치는 128 seeds, 1,152 paths, facing 9, collision slide 5, motion partition 8이며 정식 Windows x64
  build warning은 0이다. 숨김 `-familyCompanyTileRuntimeQa`에서 8방향 모두 semantic/motion/visual 방향 일치,
  실제 속도 1.650, 충돌 3종 reverse-facing 0, 최대 방향 불일치 0초, 네 좌석 seatContact 1px 이하를 통과했다.
- 착석 애니메이션은 이번 단계에서 열지 않았다. SafeStaticWork frame 0, 공통 scale 1.55, 승인 좌판 접점,
  chair base < character < limited chair front 정본을 유지한다. 다음 감사 단계는 거리 기반 gait phase,
  start/stop/idle 표현, 경로 smoothing, 전체 보행 frame 육안 QA, 승인된 착석 다중 frame 순이다.
## 2026-08-12 / Post-push P2 가구 4×4 서브셀 충돌

- 가구 충돌을 의미 셀 전체 사각형에서 gameplay 전용 OfficeFurnitureCollisionCatalog로 교체했다. 12종 가구가 4×4-per-cell 마스크와 clearance padding을 가지며 미등록·크기/방향 불일치는 전체 셀 fallback으로 막힌다.
- 레이아웃에서 가구 때문에 walkable=false인 셀과 실제 비보행 바닥을 분리했다. 직접 이동, NPC A*, 좁은 통로, 회전의자 Interaction, 책상 좌석 예외가 모두 같은 OfficeRuntimeOccupancy 마스크를 사용한다.
- 프로필 전용 Unity QA가 profiles 12, authored subcells 288, fallback subcells 16, production-radius full-cell false positives removed 78로 PASS했다.
- 전체 충돌 매트릭스는 스타터 가구 10종 + 회전의자 + 벽, 8방향, 가족 4명, 직접/NPC, 30/60/120fps, TimeScale 1/2/4, 저속/고속 10,368건을 검증한다. 상세 산출물은 Artifacts/OfficeFurnitureCollisionQa에 기록한다.
- 완료 기록: Docs/FAMILY_COMPANY_POST_PUSH_P2_SUBCELL_COLLISION_COMPLETION_2026-08-12.md
- 다음 P2: 이동 시작/정지/idle/방향 전환 표현 보강.
## 2026-08-12 / Post-push P2 이동 전환 완료

- 가족 4명에게 `turn_in_place`, `walk_start`, `walk_stop`, `short_shuffle` 4클립을 추가했다. 각 클립은 8방향×2포즈이며 전체 256개 PNG가 서로 독립된 아트다.
- `DirectionalSpriteAnimator`가 거리 기반 `StartStep/Walk/Stopping/Idle/ShortShuffle/Pivot` 상태에 맞춰 전용 클립을 선택한다. `Walk`만 기존 6프레임 루프를 유지한다.
- `OfficeLocomotionTransitionCatalog.asset`은 256개 슬롯, 가족별 원본 시트 결합 SHA-256, 256×256/180 PPU/하단 중앙 피벗 규격을 검증한다.
- 모든 프레임의 발바닥은 X=128, 하단 여백 8px로 정규화했고 하드 알파를 적용했다.
- Unity 6000.3.21f1 백그라운드 QA: 전환 asset/runtime PASS, `OfficeNavigationValidation` PASS, `PrototypeValidation` PASS.
- 완료 보고서: `Docs/FAMILY_COMPANY_POST_PUSH_P2_LOCOMOTION_TRANSITIONS_COMPLETION_2026-08-12.md`
- 원문 P2 필수 항목은 모두 완료했다. 6→8 기본 보행 확대는 선택 후속으로 남긴다.

## 2026-08-13 / Starter Office 자연스러운 이동·상호작용 보강 완료

- 걷기 원화는 `6b7b020`에서 12명×8방향×6프레임을 동일 상체 기준으로 안정화했다. 자동발견 strict 결과는 걷기 96/96, 전체 `walk/typing/mouse/drink` 계약 192/192 PASS다. 업무 동작은 의도적인 손·컵 변화를 걷기 전체 프레임 변화율로 오판하지 않고 정확한 프레임 구조·고유성·RGBA/hard alpha·세로 좌석 접점으로 검사한다.
- 이동 중 표시 방향은 의미 입력이 아니라 렌더 프레임의 실제 변위를 즉시 따른다. 실제 이동 중 3옥탄트 이상 역방향은 첫 프레임에 실패하며, 급반전은 감속→정지→제자리 Pivot→새 방향 가속 순서다. 정지 상태에서도 의미 입력으로 90°/180° 제자리 회전이 가능하다.
- 긴 직선의 표현용 6셀 lookahead와 실제 경로 진행 커서를 분리했다. 통과한 셀을 계속 예약하거나 동적 장애물 뒤에 이미 지난 셀로 되돌아가는 stale cursor를 제거했다. 코너 anticipation은 Animator용 의미 방향만 바꾸지 않고 실제 root 궤적을 안전 clearance 안에서 둥글게 만든다.
- 비좌석 시설은 실제 `furnitureId` 단위 예약을 사용한다. 복사기·정수기·서류함은 exclusive, 커피는 capacity 2이며 접근 칸은 사람마다 고유하다. 이동 전 claim, 도착 시 live layout/접근 칸 재검증, 가구 방향 제자리 정렬, Performing, intent 변경·계약 override·경로 실패·reset/disable/destroy 시 complete/abort/release를 한 수명주기로 연결했다. `filing-read`와 `filing-document`는 같은 책장 자원을 공유한다.
- 좌석은 같은 책상에서 행동만 바뀌면 현재 claim을 재사용해 불필요하게 일어나지 않는다. 앉기 전에 좌석 방향으로 제자리 회전하고, 일어나는 동안에는 chair depth/occlusion과 claim을 유지하며 실제 exit step 이후 해제한다. Starter가 기존 typing/mouse/drink frame set을 새 Actor에도 다시 연결한다.
- 레이아웃 적용 전 Actor의 셀·바라보는 방향·외출 상태·남은 계약 분·최신 자율 의도를 transient snapshot으로 잡고 재생성 뒤 복구한다. 외출 Actor는 occupancy presence에서 즉시 빠지고 복귀 때 다시 등록되어 보이지 않는 출구 ghost collision을 만들지 않는다.
- 계약 생산은 `Time.deltaTime`이 아니라 `GameTime.ElapsedMinutes` 변화만 소비한다. 게임 분이 멈춘 동안 실제 몇 초가 흘러도 person-hour가 생산되지 않는다.
- 외부 순수 회귀는 Roslyn Simulation/Presentation/Editor compile 오류 0, navigation 128 seeds·1,152 paths, stale path cursor, animation strict 192/192 PASS다. 실제 Starter Player QA에는 stop-pivot-resume 급반전과 실제 변위↔표시 방향 즉시 일치 검사를 추가했다.
- Unity 6000.3.21f1 로그인·라이선스를 정상 확인하고 Windows x64 비개발 빌드를 `C:/Users/godho/Downloads/Family/FamilyCompany_Playtest/FamilyCompany.exe`로 새로 승격했다. 실제 빌드 EXE의 headless Starter 전체 QA는 급반전 stop-pivot-resume, 8방향, 비좌석 상호작용 목적지, 충돌 3종, 네 좌석 work action hook/stand-up, 계약·저장·불러오기를 모두 통과했고 `reverseFacingFrames=0`, agent penetration 0을 기록했다. 빌드 fingerprint는 `A0F5135C4DCE9159D6298998FE77DF913E2C6D93D8ED513F12DE621100F53073`이며 최종 로그는 `Artifacts/MovementRuntimeQaLatest/family-company-player-qa-final-clean.log`다.
- fail-closed 잔여: 소파 `PairedConversation`/`GroupMeeting`의 원자적 두 사람 claim, NW 외 좌석의 사람별·방향별 pelvis/hand 수동 승인, 서서 사용하는 시설 전용 원화, 호흡 Idle/이모트/떠오르는 수치의 생동감 레이어. 가족 4명은 새 contact sheet 육안 확인 뒤 `Human visual approval`을 다시 기록했고, 직원 후보 8명은 별도 육안 승인 전까지 manifest에서 미승인 상태를 유지한다.
