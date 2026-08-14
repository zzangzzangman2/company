# SIMUL MARKET PORT

## 소유권과 원칙

`C:/Users/godho/Documents/Codex/simul`은 읽기 전용 설계 참고다. Unity 이식은 Codex 작업 흐름이 맡으며 Claude 역사 데이터 작업과 파일을 겹치지 않는다.

Claude도 역사 조사 전에 `simul/DATA_SOURCES.md`, `simul/flutter_app/lib/game/market_era_events.dart`, `market_corpus_*`, `market_arc_scenarios.dart`, `corporate_disclosure.dart`, `game_engine_corporate_actions.dart`를 읽기 전용으로 검색할 수 있다. 기존 사건 문법, 출처 URL과 사용자가 제공했던 2000~2026 한국 주식시장 자료의 파생 데이터를 참고하되 실제 회사명·날짜의 최종 확정은 원 1차 출처로 교차 검증한다. `simul`은 수정하지 않는다.

기존 Dart 파일의 거대한 구조를 그대로 복사하지는 않지만 기능을 요약하거나 새 감각으로 바꾸지도 않는다. 이미 검증된 규칙, 골든값, 저장 의미, 호가 애니메이션 순서와 테스트 계약을 작은 순수 C# 구성요소로 다시 구현한다. 동일 입력은 Dart와 Unity에서 동일한 체결량·가격·호가 프레임을 내야 한다.

## 유지할 시장 계약

- 캠페인 범위: 2000~2026
- 시장 참조 타임라인: 08:00은 장전 준비 시작이며 가족회사 캠페인 시작 시각은 08:50이다. 정규장 09:00, 종가 단일가 14:50, 정규장 종료 15:00, 하루 종료 20:00을 유지한다.
- 1분 단위 시뮬레이션
- D+2 결제
- 시대별 가격 제한과 호가 단위
- 부분 체결과 잔량 대기
- 내부 10호가와 화면 7매도+7매수
- 분당 평시 1~4회, 급변 5회, 극단 7회의 결정론적 미세구조 프레임
- 체결 batch별 FIFO `도착 → 소진 → 다음 단계`, 일시정지 중 잔량·현재가·테두리 완전 고정
- 전량 소진 가격은 소진 프레임 한 번 동안 숫자 0 없이 체결 위치를 유지
- `worldSeed + companyId + date + minute` 기반 결정론
- 기업행동 이후 주식 수·현금·원가·지분 보존

실제 회사 기준선은 상장·상장폐지·합병 조건을 제공하지만, 회차의 가격은 `CompanyState`, 뉴스, 유동성, 주문과 시장 심리가 계산한다. 플레이어가 역사를 바꾸면 가격 경로도 즉시 달라져야 한다. 현재 기업 정본은 Korea History V1의 82개 국내 회사이며 날짜별 `displayNameKo`, 불변 `companyId`, KOSPI/KOSDAQ 상장 구간과 ticker를 시장 종목 입력으로 사용한다. 2000-01-03에는 이 데이터에서 국내 상장 종목 10개가 해석된다.

## Unity 이식 순서

### S1. 시간과 가격 규칙

- `market_clock.dart` → Calendar, SessionClock, SettlementCalendar
- `market_price_rules.dart` → PricingRules
- `market_cost_rules.dart` → TradingCosts
- 기존 `StableRandom`을 공통 결정론 원천으로 사용

상태: 2026-08-10 완료. 6,545 거래일 코퍼스, 장전 준비/정규장/종가 단일가/장 종료/하루 종료 경계, D+2, 시대별 가격 제한·호가 단위·IPO 첫날 범위·수수료·거래세를 Unity 골든 검증에 넣었다.

### S2. 호가와 체결

- `order_book.dart`를 Book, Matching, Capacity, Replay로 분리
- 시장가·지정가, 부분 체결, 우선순위, 상하한가, 수수료와 세금
- 재고와 현금 보존 테스트부터 작성

상태: 2026-08-10 진행 중. cadence/frame/capacity, 다단계 지정가 체결, 절대 가격 소비 건너뛰기, 체결가 depth walk, 개별 print 분할, 7+7 표시 상수와 FIFO 재생 상태기를 순수 C#으로 옮겼다. 누적 소비 snapshot은 소수 단위 floor, 동일 watermark 멱등성, 누적 delta만 차감, 1~9주 잔량 행 숨김, 구조적 벽 90% 돌파와 회복 상한을 Dart와 동일하게 처리한다. 플레이어 지정가 잔량은 매수 고가·매도 저가 우선, 동일 가격 날짜/분/sequence/ID FIFO, 앞 주문 취소·부분체결에 따른 뒤 queue-ahead 해제, 외부 호가 대기량 선소진, 수수료 포함 매수 현금 예약과 매도 잔량 예약을 동일하게 처리한다. Dart 직접 생성 fixture는 `Assets/FamilyCompany/Tests/Fixtures/simul_order_book_golden_v1.json`이며 SHA-256은 `1A28D79148B24C9311EA19BFD25C2E51691B1E5B52B0E44FFE204F5273497DD9`다. 원본 Unity 6000.3.21f1에서 확장된 소비 snapshot·대기주문 큐, Korea History V1 등록부·2000년 상장 종목·씬 런타임 카탈로그를 함께 검증해 `FAMILY_COMPANY_VALIDATION: PASS`를 확인했다.

### S3. 시세 생성과 정보

- `market_tick.dart` → PricePathGenerator
- 기술적 수준, 유동성 구간, QuoteService
- 뉴스와 기업 상태를 가격 압력으로 변환

### S4. 기업행동과 지배구조

- 배당, 유상·무상증자, 액면분할·병합
- 공개매수, 의결권, 대주주, 합병과 상장폐지
- 실제 회사 대체역사 시스템과 연결

## 이식하지 않을 것

- 거대한 `game_engine.dart`와 `order_book.dart`의 구조 자체
- Flutter Widget 구현체와 모바일 레이아웃 자체. 단, 호가 7+7, 현재가·최근 체결·빨간 테두리 동시 갱신, FIFO 도착/소진, 정지 고정 같은 관찰 가능한 화면 동작은 그대로 이식한다.
- 52,000개 일별 샘플 같은 대용량 fixture 전체
- 기존의 50개 가상기업 이름을 정본으로 사용하는 것

대신 Dart 원본이 직접 생성하는 골든 fixture와 대표 회귀 시나리오를 가져와 C# 결과가 일치하는지 확인한다. 회사 이름과 역사 데이터 교체는 엔진 수치를 변경할 이유가 아니다.
