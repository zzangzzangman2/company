# PROJECT STATE

최종 갱신: 2026-08-10
현재 단계: Art V0.6 + Market Port S2 / 캐릭터 에셋과 SIMUL 호가·체결 정확 이식
Unity: 6000.3.21f1

## 현재 목표

2000년의 소형 가족 하청회사에서 시작해 실제 기업과 경쟁하고 2026년까지의 역사를 바꾸는 장기 구조를 세운다. 가까운 재미 검증 질문은 하청 계약과 가족 부탁이 동시에 생겼을 때 누구를 어디로 보내는 선택이 납기·현금·관계에 다른 결과를 만드는가이다.

## 정본 요약

- 플레이어: 14살
- 누나: 20살, 긴 검은 양갈래와 검은 리본·청록색 눈, 나시티·돌핀팬츠·맨발
- 아빠: 46살, 청록 셔츠·차콜 슬랙스·은색 사각 안경 정본
- 엄마: 44살, 피치 카디건·크림 블라우스·청록 스커트 정본
- 캠페인 시작: 2000-01-03 08:00
- 정본 누나 에셋: Assets/Art/Characters/OlderSister/older_sister_casual_neutral_v2.png
- 런타임 누나 도트: Assets/Art/Characters/OlderSister/Pixel/older_sister_pixel_walk4x2_v2.png
- 런타임 플레이어 도트: Assets/Art/Characters/Player/Pixel/player_pixel_walk4x2_v1.png
- 정본 아빠 원화·도트: Assets/Art/Characters/Father/father_office_neutral_v1.png / Pixel/father_pixel_walk4x2_v1.png
- 정본 엄마 원화·도트: Assets/Art/Characters/Mother/mother_office_neutral_v1.png / Pixel/mother_pixel_walk4x2_v1.png
- 향후 직원 후보 8인: Assets/Art/Characters/Employees/ 아래 전신 원화 72종·정체성 앵커 11종·도트 시트 8종
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
- 7매도+7매수와 batch FIFO `도착→소진→다음 단계`, 10배속 최소 이동 시간, pause 완전 고정, 완료 identity 중복 차단 상태기 구현

## 진행 중

- `order_book.dart`의 생성 snapshot, 구조적 벽 실제 회복·취소, 플레이어 주문 우선순위와 재고/현금 보존을 나머지 S2로 이식
- Unity 호가창 7+7 프레젠테이션과 주문 작업창을 PC 가로 화면에 연결

## 다음 작업

1. 생성 호가 snapshot과 구조적 벽 실제 회복·취소를 Dart golden으로 고정하고 Unity에 이식한다.
2. 시장가·지정가·부분 체결·잔량 대기·플레이어 주문 FIFO와 재고/현금 보존을 이식한다.
3. Unity PC 가로형 주식 화면에 Korea History V1 종목 목록과 실제 7+7 호가창을 연결한다.
4. 시세 경로·뉴스·기술 수준·유동성 구간 S3를 이식한다.
5. 배당·증자·분할·공개매수·합병·상장폐지 S4를 History V1 조건부 사건과 연결한다.
6. 아빠·엄마 정본 도트를 현재 씬의 부모 placeholder 렌더에 연결한다.
7. 직원 후보 8인을 실제 고용·능력치·업무 배치 시스템과 연결한다.
8. 저장 슬롯 카드에 회사 대표 썸네일과 회차별 대체역사 요약을 추가한다.

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
- 2026-08-10: 가족회사 관리 루프 V0.5 구현. 처음하기는 스토리 없이 임차 오피스텔의 엄마·아빠·나·누나 4인 회사로 시작하며, ImageGen 캐주얼 대시보드 위에 요구 개발/속도·마감·보상·위약금 계약 게시판, 자본 투자형 R&D, 시장 조사·자체 제품 출시를 연결함. 저장 스키마 v3는 시장 보고서·제품 프로젝트 존재 플래그와 구형 V3 유효 데이터 판별을 함께 사용해 빈 객체를 복원하지 않으며 독립 로직 검증을 통과함.
- 2026-08-10: 격리 Unity 6000.3.21f1 프로젝트에서 PrototypeProjectBuilder, PrototypeValidation, ManagementLoopValidation 모두 PASS. History 등록부 83행·국내 82개·2000-01-03 상장 10종목, SIMUL 시장 골든값, 씬의 런타임 카탈로그, 저장 V3 왕복을 한 빌드에서 확인함.
- 2026-08-10: 가족회사 관리 루프 V0.6 확장. 시작 자본을 정확히 500만 원으로 고정하고 웹·소프트웨어, 피처폰·모바일, 하드웨어·PC, 패션·유통·오프라인의 4개 업종과 21개 2000년대형 하청 의뢰를 추가함. 분야별 시장 조사 후 첫 사업 설립, 최대 4개 업종 다각화, 자체 제품 출시, 업종별 채용 직군, 가족 공동 경력 기억·관계 변화, 글로벌 진출 준비도를 연결했으며 저장 스키마를 v4로 올림. ImageGen으로 밝고 캐주얼한 사업 확장 대시보드를 제작해 제품 메뉴에 적용했고, 전체 C# 컴파일(경고·오류 0)과 독립 경제·저장 흐름 검증을 통과함.
- 참고: -nographics에서 Camera.Render를 호출하면 Unity 네이티브 렌더러가 충돌하므로 시각 캡처에만 -nographics를 쓰지 않는다. 일반 빌드와 로직 검증에는 -nographics를 계속 사용한다.

## 차단 요소

- 아빠·엄마 정본 에셋은 완성됐으나 현재 씬의 부모 placeholder 렌더 교체는 아직 연결하지 않았다.
- 누나 이름은 미정이며 내부 ID older_sister를 사용한다.
