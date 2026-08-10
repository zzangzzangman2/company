# ARCHITECTURE

## 계층

- FamilyCompany.Simulation: Unity 참조가 없는 시간, RNG, 이벤트, 가족, 회사, 회계, 역사 resolver, 시장 코어, 게임 상태
- FamilyCompany.Simulation.Contracts: 실제 고객 회사 ID, 소형 하청 제안, 4인 팀 수락 용량 정책
- FamilyCompany.Simulation.Banking: 연도별 금리, 예금·대출·어음 할인 순수 계산 규칙
- FamilyCompany.Simulation.Organization: 업종별 채용 직군, S~F 후보 제안과 인사 순수 규칙
- FamilyCompany.Simulation.Progression: 은행·R&D·채용·법인계좌·인수합병 의미 접근 조건
- FamilyCompany.Simulation.Leisure: 시대·요일·참가자 조건을 가진 회복 활동 카탈로그, 결정론적 추천·실행·오디오 큐와 완료 활동 추억 의미 데이터
- FamilyCompany.Save: 저장 DTO와 저장소 인터페이스
- FamilyCompany.Infrastructure.Unity: JsonUtility 기반 Korea History V1 로더와 persistentDataPath 저장 어댑터
- FamilyCompany.Presentation.Unity: 입력, 카메라, 화면 표시, 씬 오브젝트 연결
- FamilyCompany.Editor: 프로토타입 씬 생성과 헤드리스 검증
- FamilyCompany.Content.History: 실제 회사·사건·출처의 읽기 전용 JSON 데이터

## 핵심 불변식

- 시간의 원천은 캠페인 시작 이후 흐른 정수 분 하나다.
- 게임 플레이 RNG는 seed와 안정 키로부터 재현된다. UnityEngine.Random을 사용하지 않는다.
- 같은 시각의 예약 이벤트는 dueMinute, priority, eventId 순서로 처리한다.
- 돈은 long 원 단위다. 소수 부동소수점으로 돈을 보관하지 않는다.
- 모든 회계 거래는 차변 합계와 대변 합계가 같다.
- 저장 대상은 의미 상태다. Transform, 렌더러 캐시, UI 선택 상태는 저장하지 않는다.
- 가족 자율 행동은 30분 절대 경계에서 진행하며 현재 행동·의미 목적지·처리 시각·누적 업무/휴식·사건만 저장한다. 같은 seed와 목표 시각이면 시간 진행 호출을 나눠도 결과가 같다.
- 계약 저장은 제안 원본, 수락·납기·해결 시각, 상태, 완료 인시와 가족별 기여 인시를 보존한다. 스키마 v2는 스키마 v1을 빈 계약 목록으로 이관한다.
- 주식시장 session·호가·체결 계산은 순수 C#이며 `companyId + date + minute + pulse`를 안정 키로 사용한다.
- 플레이어 지정가 대기주문은 가격우선·시간우선 FIFO와 queue-ahead를 순수 C#으로 유지하고, Unity UI·저장·원장은 이 코어의 결과만 투영한다.
- 호가 프레젠테이션은 내부 10단계를 유지하고 화면에 최우선 7매도+7매수를 표시한다.
- 체결 replay는 batch identity 중복을 막는 FIFO이며 한 단계마다 Arriving과 Draining을 각각 한 렌더 프레임 이상 공개한다. pause 중 cursor는 변하지 않는다.

## 실제 역사와 회차 상태

- HistoricalBaseline은 검증된 읽기 전용 입력이며 저장 게임이 수정하지 않는다.
- WorldState는 현재 회차의 회사, 소유관계, 제품, 기술, 재무, 주식과 지급능력 상태를 가진다.
- DivergenceLog는 기준 역사에서 취소·지연·대체·이전된 사건과 원인을 기록한다.
- 실제 회사명은 날짜별 데이터이고 영구 참조와 저장은 불변 companyId를 사용한다.
- 역사 사건은 조건부 후보이며, 선행 조건이 깨지면 원래 결과를 강제로 발생시키지 않는다.
- Korea History V1 JSON은 JsonUtility DTO로 읽은 뒤 불변 `HistoricalCompanyRegistry`로 투영한다.
- `ListedSecuritiesAt(date)`는 실제 이름과 KOSPI/KOSDAQ listing 구간을 시장 종목으로 만들고, KOSDAQ은 SIMUL의 `도전시장` 호가 규칙에 대응한다.

상세 규칙은 Docs/REAL_COMPANY_ALT_HISTORY.md, 시장 이식 경계는 Docs/SIMUL_MARKET_PORT.md를 따른다.

## 씬 경계

Prototype01은 집, 거리, 작은 사무실을 한 씬의 구역으로 보여 준다. 구역 이동은 현재 우선순위가 공간 감각 검증이므로 포털 없이 직접 걸어서 가능하게 시작한다. 향후 맵이 커지면 의미 위치 ID와 전환 시스템을 추가한다.

## 2.5D 도트 프레젠테이션

- 사무실과 가구는 실제 Collider를 가진 3D 모듈이다.
- Main Camera는 직교 투영으로 플레이어를 추적한다.
- PixelatedCameraEffect가 월드 렌더를 낮은 내부 해상도로 축소하고 Point 필터로 확대한다.
- 플레이어·누나·부모는 카메라 기준 이동 벡터를 45도 옥턴트로 양자화해 8방향 Sprite를 선택하고, 방향별 6프레임 보행을 0.11초 간격으로 순환한다.
- 고동작 시트의 24개 실제 실루엣은 오프라인 분리기가 상체 중심·발 기준선으로 정렬해 256×256 단일 PNG 48개로 만든다. Editor 빌더는 이를 Point·180 PPU·하단 피벗 Sprite로 임포트하며 런타임 코드가 원본 PNG를 자르지 않는다.

## 실제 회사 이동

- 플레이어는 PrototypePlayerController와 CharacterController로 직접 이동한다.
- NPC는 OfficeWorkerAgent와 CharacterController로 웨이포인트 사이를 실제 이동한다.
- OfficeAutonomyCoordinator는 순수 시뮬레이션의 의미 목적지를 실제 웨이포인트로 투영한다. 계약 coordinator가 배정한 업무는 자율 목적지보다 우선하고 완료 후 기존 자율 행동을 재개한다.
- 평일 학교·외부 일정·영업·가사와 수면은 FamilyScheduleRules에서 계산하며 계약 코어와 화면 배정이 같은 가용성 판정을 사용한다.
- 외부 일정은 `Outside` 의미 목적지를 출구 웨이포인트에 투영한다. NPC는 출구 도착 뒤 렌더만 숨기고, 복귀 행동이 정해지면 다시 표시되어 안전 통로로 걸어 들어온다.
- 플레이어 직접 작업은 PlayerOfficeWorkInteractor가 계약 단계의 의미 장소를 요구하고, 근처에서 E를 유지한 경우에만 계약 코어의 `RecordWork`를 호출한다.
- OfficeWaypoint는 위치, 업무 종류, 최소·최대 체류 시간을 가진다.
- 체류 시간은 agentId, 정거장 횟수, waypointId에 StableRandom 키를 적용해 재현된다.
- 현재 경로는 사전 정의된 안전 통로다. 사무실 배치를 플레이어가 자유 편집하게 되면 경로 탐색 계층을 추가한다.

## 오디오 프레젠테이션

- GameAudioCoordinator는 Resources의 라이선스 확인된 BGM·SFX를 지연 로드하고, 타이틀과 실제 회사 세션의 BGM을 교차 전환한다.
- 계약 수락·작업·수익·실패·저장·이동 효과음은 프레젠테이션 결과만 표현하며 시뮬레이션 상태나 RNG를 변경하지 않는다.
- OfficeSoundscapeController는 NPC의 최초 관측을 무음으로 시드하고 Inside/Outside·Walking/도착 전이만 감시한다. 퇴실·복귀 문, 프린터 종이, 계약 업무/회의 환경음은 전역 쿨다운을 거쳐 중복 폭주 없이 재생한다.
- LeisureAudioCueCatalog는 회복 활동 ID와 ImageGen 장면 ID를 1:1로 유지하고 각 장면의 진입 SFX·반복 BGM·완료 SFX를 의미 데이터로 제공한다.
- `simul`의 세로 화면 배치와 좌표는 아키텍처 입력이 아니다. 재사용 대상은 순수 규칙, 검증 교훈, 라이선스가 확인된 원본 자산이며 모든 후속 화면은 1920×1080 16:9에 새로 투영한다.
