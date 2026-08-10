# PROJECT STATE

최종 갱신: 2026-08-10
현재 단계: Art V0.5 / SIMUL v3 통일 화풍과 플레이어·사무실 도트
Unity: 6000.3.21f1

## 현재 목표

2000년의 소형 가족 하청회사에서 시작해 실제 기업과 경쟁하고 2026년까지의 역사를 바꾸는 장기 구조를 세운다. 가까운 재미 검증 질문은 하청 계약과 가족 부탁이 동시에 생겼을 때 누구를 어디로 보내는 선택이 납기·현금·관계에 다른 결과를 만드는가이다.

## 정본 요약

- 플레이어: 14살
- 누나: 20살, 긴 검은 양갈래와 검은 리본·청록색 눈, 나시티·돌핀팬츠·맨발
- 아빠: 46살 임시 확정, 최종 에셋 대기
- 엄마: 44살 임시 확정, 최종 에셋 대기
- 캠페인 시작: 2000-01-03 08:00
- 정본 누나 에셋: Assets/Art/Characters/OlderSister/older_sister_casual_neutral_v2.png
- 런타임 누나 도트: Assets/Art/Characters/OlderSister/Pixel/older_sister_pixel_walk4x2_v2.png
- 런타임 플레이어 도트: Assets/Art/Characters/Player/Pixel/player_pixel_walk4x2_v1.png
- 사무실 도트 모듈: Assets/Art/Office/Pixel/office_module_atlas_4x3_v1.png 및 Modules 12종
- 공식 화풍: SIMUL polished soft-render VN anime v3 / 런타임 도트 번역 v1

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

## 진행 중

- 없음. Frontend V0.4 구현과 자동·시각 검증은 완료 상태다.

## 다음 작업

1. Claude Korea History V1에서 국내 실제 회사 60개 이상, 2000~2003 상세 25개 이상과 2004~2026 진입·퇴출 앵커를 작성하고 검증한다.
2. 저장 슬롯 카드에 회사 대표 썸네일과 회차별 대체역사 요약을 추가한다.
3. 계약의 품질, 고객 만족과 재계약 확률을 가족 능력·피로·검수 행동에 연결한다.
4. 플레이어가 직접 계약 작업 지점에서 상호작용해 인시를 기여하도록 연결한다.
5. 아침 가족회의에서 회사 일과 가족 부탁이 충돌하는 첫 선택 이벤트를 만든다.
6. `simul` 시장 이식 S1인 거래 달력·세션·가격·비용 규칙을 골든 테스트로 구현한다.
7. 부모 최종 에셋은 사용자 제공·확정 시 placeholder와 교체한다.
8. 12개 사무실 도트 모듈을 현재 3D 충돌 가구의 렌더 비주얼에 단계적으로 연결한다.

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
- 2026-08-10: Unity 6000.3.21f1에서 4인 회사 계약 코어 컴파일 및 PrototypeValidation 통과. 32개 결정론적 초기 제안이 4명·80 인시·250만원 상한 안에 있고, 12명·1000 인시 대형 계약은 TeamTooSmall로 거절됨을 확인함.
- 2026-08-10: Claude 역사 데이터 작업과 격리한 별도 Codex QA worktree에서 Unity 6000.3.21f1 PrototypeValidation 통과. 계약 수락 착수비, 가족 4명 기여 20인시, 완료 매출·평판, 중복 차단, 기한초과 실패, 장부 균형, 저장 스키마 v2 왕복을 확인함.
- 2026-08-10: 별도 Codex QA worktree에서 Office V0.3 씬 재생성 및 PrototypeValidation 통과. 누나·아빠·엄마 agent, 직원 A·B 부재, 일반 순환 이동, 누나의 계약 출력 지점 실제 이동, 체류 후 4인시 반영과 계약 완료를 확인함.
- 2026-08-10: 별도 Codex QA worktree의 Unity 6000.3.21f1에서 Frontend V0.4 PrototypeValidation 통과. 3개 슬롯의 서로 다른 seed·시간 왕복, backup 생성, 기존 단일 저장 호환, 시작 화면·새 게임·일시정지·재개 상태 전환, 1920×1080·1280×720·전체화면 설정을 확인함.
- 2026-08-10: imagegen 타이틀 키아트가 포함된 Windows Development QA player 빌드 통과. 1920×1080 실제 플레이어 캡처에서 왼쪽 제목·버튼 안전 영역, 오른쪽 20살 누나와 2000년 CRT·전화·팩스 사무실, 글자 잘림 부재를 눈으로 확인함.
- 2026-08-10: 플레이어·사무실 imagegen 크로마 원본을 하드 키로 투명화. 두 정본 모두 RGBA, 알파 0/255, 네 모서리 투명이며 빨간 모자·피부·민트·복숭아 팔레트 보존을 눈으로 확인함.
- 2026-08-10: Office V0.5 빌더 통과. 플레이어 8개 방향 프레임과 사무실 12개 개별 Sprite를 생성하고 플레이어 캡슐을 DirectionalSpriteAnimator 이동 도트로 교체함.
- 2026-08-10: PrototypeValidation 통과. 플레이어 8프레임, 누나 8프레임, 사무실 12모듈, 플레이어 DirectionalSpriteAnimator와 기존 계약·저장·화면 검증을 함께 확인함.
- 2026-08-10: GPU 사무실 개요 캡처 통과. 기존 3D 충돌 사무실과 누나 도트가 유지되고 씬 재생성 결함이 없음을 확인함.
- 참고: -nographics에서 Camera.Render를 호출하면 Unity 네이티브 렌더러가 충돌하므로 시각 캡처에만 -nographics를 쓰지 않는다. 일반 빌드와 로직 검증에는 -nographics를 계속 사용한다.

## 차단 요소

- 아빠와 엄마의 최종 캐릭터 에셋은 사용자가 나중에 제공한다. 현재는 placeholder를 유지한다.
- 누나 이름은 미정이며 내부 ID older_sister를 사용한다.
