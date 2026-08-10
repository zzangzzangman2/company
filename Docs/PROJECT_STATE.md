# PROJECT STATE

최종 갱신: 2026-08-10
현재 단계: Office V0.2 실제 이동·2.5D 도트 전환
Unity: 6000.3.21f1

## 현재 목표

집, 거리, 귀여운 2.5D 도트 사무실을 실제로 오가며 가족과 직원이 업무 지점 사이를 이동하게 한다. 다음 재미 검증 질문은 회사 업무와 가족 부탁이 동시에 생겼을 때 누구를 어디로 보낼지 선택하는 과정이 감정과 결과를 만드는가이다.

## 정본 요약

- 플레이어: 14살
- 누나: 20살, 긴 검은 양갈래와 검은 리본·청록색 눈, 나시티·돌핀팬츠·맨발
- 아빠: 46살 임시 확정, 최종 에셋 대기
- 엄마: 44살 임시 확정, 최종 에셋 대기
- 캠페인 시작: 2000-01-03 08:00
- 정본 누나 에셋: Assets/Art/Characters/OlderSister/older_sister_casual_neutral_v2.png
- 런타임 누나 도트: Assets/Art/Characters/OlderSister/Pixel/older_sister_pixel_walk4x2_v2.png

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
- 누나와 직원 placeholder 2명 CharacterController 실제 이동
- 웨이포인트별 고객 응대, 업무, 출력, 회의, 휴식 상태와 결정론적 체류 시간
- 직교 카메라, 카메라 기준 플레이어 이동, 마우스 휠 줌, 저해상도 Point 렌더

## 진행 중

- 없음. Office V0.2 구현과 자동 검증은 완료 상태다.

## 다음 작업

1. Unity에서 Prototype01을 직접 플레이해 이동 속도, 카메라 크기, 도트 내부 해상도를 체감 조정한다.
2. 플레이어 14살의 외형 정본을 확정하고 4방향 도트 이동 시트를 만든다.
3. 업무 큐가 NPC의 다음 웨이포인트를 선택하도록 연결한다.
4. 아침 가족회의에서 회사 일과 가족 부탁이 충돌하는 첫 선택 이벤트를 만든다.
5. 사용자에게 부모 에셋을 받으면 placeholder 자리만 교체한다.

## 검증 기록

- 2026-08-10: 누나 PNG 파일 크기, RGBA, 알파 범위, 모서리 투명도, 피사체 bbox 검사 통과.
- 2026-08-10: Unity 6000.3.21f1에서 Simulation, Save, Infrastructure.Unity, Presentation.Unity, Editor 5개 어셈블리 컴파일 통과.
- 2026-08-10: PrototypeProjectBuilder 실행 통과. Assets/FamilyCompany/Scenes/Prototype01.unity 생성 및 Build Settings 등록.
- 2026-08-10: PrototypeValidation 통과. 시작 나이 14/20/46/44, Dart 호환 RNG 골든값, 이벤트 순서, 시간 진행, 회계 균형, JSON 저장 왕복, 누나 Sprite와 씬 존재 검증.
- 2026-08-10: GPU 배치 렌더 캡처 통과. 집–거리–사무실과 캐릭터 배치를 눈으로 확인함.
- 2026-08-10: Office V0.2 Unity 빌드 통과. 누나 정본 시트를 8개 단일 Sprite로 생성하고 씬에 실제 이동 agent 3명을 연결함.
- 2026-08-10: Office V0.2 헤드리스 검증 통과. 직교 카메라, 픽셀 효과, 8개 프레임, agent별 4개 이상 경로, 누나·직원 A·직원 B 각각 30초 실제 좌표 이동과 정거장 도착을 확인함.
- 2026-08-10: GPU 시각 QA 통과. 접수·업무·회의·휴게·출력 구역, 중앙 통로, 누나 도트, 직원 placeholder 배치를 확인함.
- 참고: -nographics에서 Camera.Render를 호출하면 Unity 네이티브 렌더러가 충돌하므로 시각 캡처에만 -nographics를 쓰지 않는다. 일반 빌드와 로직 검증에는 -nographics를 계속 사용한다.

## 차단 요소

- 아빠와 엄마의 최종 캐릭터 에셋은 사용자가 나중에 제공한다. 현재는 placeholder를 유지한다.
- 누나 이름은 미정이며 내부 ID older_sister를 사용한다.
