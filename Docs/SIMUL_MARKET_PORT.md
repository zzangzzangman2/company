# SIMUL MARKET PORT

## 소유권과 원칙

`C:/Users/godho/Documents/Codex/simul`은 읽기 전용 설계 참고다. Unity 이식은 Codex 작업 흐름이 맡으며 Claude 역사 데이터 작업과 파일을 겹치지 않는다.

기존 Dart 파일을 줄 단위로 옮기지 않는다. 이미 검증된 규칙, 골든값, 저장 의미와 테스트 계약을 작은 순수 C# 구성요소로 다시 구현한다.

## 유지할 시장 계약

- 캠페인 범위: 2000~2026
- 게임 하루: 08:00 시작, 09:00 정규장 시작, 14:50 종가 단일가 구간, 15:00 정규장 종료, 20:00 하루 종료
- 1분 단위 시뮬레이션
- D+2 결제
- 시대별 가격 제한과 호가 단위
- 부분 체결과 잔량 대기
- `worldSeed + companyId + date + minute` 기반 결정론
- 기업행동 이후 주식 수·현금·원가·지분 보존

실제 회사 기준선은 상장·상장폐지·합병 조건을 제공하지만, 회차의 가격은 `CompanyState`, 뉴스, 유동성, 주문과 시장 심리가 계산한다. 플레이어가 역사를 바꾸면 가격 경로도 즉시 달라져야 한다.

## Unity 이식 순서

### S1. 시간과 가격 규칙

- `market_clock.dart` → Calendar, SessionClock, SettlementCalendar
- `market_price_rules.dart` → PricingRules
- `market_cost_rules.dart` → TradingCosts
- 기존 `StableRandom`을 공통 결정론 원천으로 사용

### S2. 호가와 체결

- `order_book.dart`를 Book, Matching, Capacity, Replay로 분리
- 시장가·지정가, 부분 체결, 우선순위, 상하한가, 수수료와 세금
- 재고와 현금 보존 테스트부터 작성

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
- Flutter UI와 화면 상태
- 52,000개 일별 샘플 같은 대용량 fixture 전체
- 기존의 50개 가상기업 이름을 정본으로 사용하는 것

대신 경계값과 대표 시나리오를 작은 골든 fixture로 가져와 C# 테스트와 Dart 결과가 일치하는지 확인한다.
