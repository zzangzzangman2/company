# PROJECT STATE

최종 갱신: 2026-08-10
현재 단계: Prototype 0.1 기반 완성, 첫 재미 이벤트 대기
Unity: 6000.3.21f1

## 현재 목표

집, 거리, 작은 사무실을 오갈 수 있는 회색상자 씬 위에 결정론적 시간·가족·회사·회계·저장 기반을 올린다. 재미 검증의 첫 질문은 가족과 회사를 동시에 챙기는 선택이 짧은 플레이에서도 감정과 결과를 만드는가이다.

## 정본 요약

- 플레이어: 14살
- 누나: 20살, 긴 검은 양갈래와 검은 리본·청록색 눈, 나시티·돌핀팬츠·맨발
- 아빠: 46살 임시 확정, 최종 에셋 대기
- 엄마: 44살 임시 확정, 최종 에셋 대기
- 캠페인 시작: 2000-01-03 08:00
- 정본 누나 에셋: Assets/Art/Characters/OlderSister/older_sister_casual_neutral_v2.png

## 완료

- Unity 프로젝트 생성
- 협업 문서 뼈대 생성
- 경마장 표 판매원 에셋을 기반으로 20살 누나 정본 원화 생성
- 투명 배경 PNG 검증 완료: 1024x1536 RGBA, 알파 0~255
- FamilyCompany.Simulation 순수 C# 계층 구현: 시간, 안정 RNG, 이벤트 큐, 가족, 회사, 복식부기
- 저장 DTO/매퍼와 Unity JsonUtility 기반 primary/temp/backup 저장소 구현
- Prototype01 씬 생성: 집, 거리, 작은 사무실, 14살 플레이어, 부모 placeholder, 누나 원화
- 등각 카메라, WASD/방향키 이동, 시간 진행, 저장/불러오기 디버그 패널 구현
- 로컬 Git 저장소 main 브랜치 초기화. 아직 첫 커밋은 하지 않음

## 진행 중

- 없음. Prototype 0.1 기술 기반은 검증 완료 상태다.

## 다음 작업

1. Unity에서 Prototype01을 직접 플레이해 이동 속도와 카메라 감각을 조정한다.
2. 아침 가족회의에서 회사 일과 가족 부탁이 충돌하는 첫 선택 이벤트를 만든다.
3. 선택 결과를 신뢰, 체력, 스트레스, 회사 현금에 연결한다.
4. 사용자에게 부모 에셋을 받으면 placeholder 자리만 교체한다.

## 검증 기록

- 2026-08-10: 누나 PNG 파일 크기, RGBA, 알파 범위, 모서리 투명도, 피사체 bbox 검사 통과.
- 2026-08-10: Unity 6000.3.21f1에서 Simulation, Save, Infrastructure.Unity, Presentation.Unity, Editor 5개 어셈블리 컴파일 통과.
- 2026-08-10: PrototypeProjectBuilder 실행 통과. Assets/FamilyCompany/Scenes/Prototype01.unity 생성 및 Build Settings 등록.
- 2026-08-10: PrototypeValidation 통과. 시작 나이 14/20/46/44, Dart 호환 RNG 골든값, 이벤트 순서, 시간 진행, 회계 균형, JSON 저장 왕복, 누나 Sprite와 씬 존재 검증.
- 2026-08-10: GPU 배치 렌더 캡처 통과. 집–거리–사무실과 캐릭터 배치를 눈으로 확인함.
- 참고: -nographics에서 Camera.Render를 호출하면 Unity 네이티브 렌더러가 충돌하므로 시각 캡처에만 -nographics를 쓰지 않는다. 일반 빌드와 로직 검증에는 -nographics를 계속 사용한다.

## 차단 요소

- 아빠와 엄마의 최종 캐릭터 에셋은 사용자가 나중에 제공한다. 현재는 placeholder를 유지한다.
- 누나 이름은 미정이며 내부 ID older_sister를 사용한다.
