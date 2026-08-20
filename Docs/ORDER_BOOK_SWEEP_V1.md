# ORDER BOOK SWEEP V1 — 호가창 체결이 실제로 움직인다

`simul/flutter_app/lib/stock_market_order_book.dart`와
`stock_market_order_workspace.dart`의 체결 재생 구조를 Unity 패널로 이식한 문서다.

## 왜 테두리가 안 움직였나

`MarketOrderBookPresentationRules.BuildVisibleLevels`는 **매 프레임 현재가(호가 접점) 기준으로 사다리를
다시 중심 잡는다.** 그리고 테두리를 고르는 `CentralOutlineLevel`도 그 접점을 골랐다.

결과적으로 테두리는 **언제나 사다리 한가운데 같은 줄**에 왔다. 가격이 오르내려도 사다리 전체가 같이
스크롤되니 화면상 테두리는 한 픽셀도 움직이지 않았다. 데이터는 바뀌는데 시각적으로는 정지였다.

`CentralOutlineLevel`의 주석이 이미 이 상태를 적어 두고 있었다.

> Selects the idle current-price outline at the central touch.
> **Active replay targets its exact execution row instead of this fallback.**

즉 fallback만 이식되고 본체인 체결 재생이 연결되지 않은 상태였다. 자료구조
(`MarketOrderBookReplayQueue`, `MarketOrderBookSweepStep`)는 상수까지 정확히 이식돼 있었지만
`PrototypeValidation` 외에는 아무도 쓰지 않았다.

## 재생 구조 — simul과 동일

```text
idle → arriving → draining → (다음 스텝) arriving → … → 최종 홀드 → idle
```

| 단계 | 길이 | 이 동안 보이는 것 |
| --- | --- | --- |
| `arriving` | 144ms/배속 (최소 36ms) + 정착 20ms | 테두리는 아직 **이전** 스텝 행에 있다 |
| 도착 프레임 | — | 테두리·가격축·헤더·체결테이프·플래시가 **같은 프레임에** 이동 |
| `draining` | 480ms/스텝수, 56~96ms로 clamp, 배속 나눔 | 현재 행에서 잔량이 줄어든다 |
| 최종 홀드 | 112ms | 마지막 체결이 화면에 남는다 |

상수 정본은 `Simulation/Market/MarketOrderBookReplay.cs`다. 최소 36ms 하한이 있는 이유는 10배속에서도
행당 60Hz 두 프레임을 남겨야 하기 때문이다. 그보다 빠르면 중간 행을 건너뛰어 무작위 점프처럼 보인다.

## 스텝은 체결이 지나간 호가 레벨 하나다

simul은 `minuteTransition.orderedFills`에서 스텝을 만든다. 체결이 소진한 레벨마다 1스텝이고, 순서와
`sequence`, 소진/잔여 수량, 마지막 스텝의 `boundaryCrossed`를 가진다.

우리 시장은 매 분 시드로 호가를 재생성하므로 읽을 fill 목록이 없다. 그래서 동등한 것을 유도한다.
정본은 `Simulation/Market/MarketOrderBookSweepBuilder.cs`다.

- 가격이 21,100 → 21,250으로 움직였으면 그 사이 매도 호가를 **순서대로 전부 소진한 것**이므로 각
  레벨이 스텝이 된다.
- 가격이 그대로인 분은 **1스텝**을 찍는다. simul이 pulse 합성 체결에 대해 하는 것과 같다.
- 마지막 스텝만 `boundaryCrossed`이고 잔여 수량을 남긴다. 중간 스텝은 전량 소진이다.
- `MaximumSteps = 12`. 상하한가급 갭이 다음 분까지 재생되는 것을 막고, 상한에 걸려도 마지막 스텝은
  **실제 체결가**로 끝낸다. 사다리·헤더·테이프가 같은 가격에 합의해야 한다.

**왜 점프가 아니라 워크인가.** 테두리·가격축·헤더·테이프가 모두 같은 커서를 읽는다. 점프하면 목적지가
즉시 보이고 아무것도 움직이지 않는다.

## 사다리 앵커

스윕이 도는 동안 사다리는 접점이 아니라 **재생 중인 스텝**에 고정한다
(`touchReferencePrice`/`touchReferenceSide`). 그리고 `preserveEmptyMarketLevelPrices`로 방금 비운
레벨도 행으로 남긴다. 그러지 않으면 테두리가 올라앉은 행이 재생 중에 사라진다.

## 행 안에서 움직이는 것

IMGUI는 프레임마다 행을 새로 그려 위젯 상태가 없다. 그래서
`Presentation.Unity/OrderBookRowMotion.cs`가 (side, price)별로 이전 잔량·델타·만료·트윈 깊이를 들고 있다.
사다리가 스크롤해 사라진 레벨은 30초 후 정리한다.

| 요소 | 규칙 |
| --- | --- |
| 체결 플래시 | 420ms, alpha 0.58 → 0, ease-out cubic. 도착 프레임마다 재시작 |
| 깊이 바 | 트윈. **소진 중인 행만 스윕 스텝 시간**, 나머지는 144ms |
| 잔량 증감 배지 | 520ms. 증가 `#16794E`, 체결 소진 `#B42332` |
| 선택 가격 행 | 금 테두리 `#E0A900` 2px + 플레이트 `#FFF2B8` |

### 배지가 일부러 숨기는 케이스

```text
delta == 0                → 표시하지 않음
!isTrade && delta < 0     → 표시하지 않음
그 외                      → +N / -N
```

두 번째 규칙은 simul의 주석 그대로다. **호가가 그냥 줄어든 것에 음수를 찍으면 일어나지 않은 체결처럼
보인다.** 취소된 호가는 잔량만 제자리에서 갱신되고, 실제 체결만 부호 라벨과 테이프 기록을 함께 받는다.

선택 가격 테두리를 따로 두는 이유도 같은 계열이다. 사다리가 아래로 스크롤해도 플레이어가 고른 가격은
계속 보여야 한다. 접점 테두리 하나로는 그것이 표현되지 않는다.

## 검증

`FamilyCompany.Editor.PrototypeValidation.ValidateOrderBookSweep`

```text
ORDER_BOOK_SWEEP_VALIDATION: PASS | steps=4 arrivedRows=4 cap=12 flash=0.52s
```

- 여러 틱 이동이 **2스텝 이상**을 만들고, 상승이면 한 칸씩 올라간다
- `sequence`가 순서대로 붙고 마지막만 `boundaryCrossed`다
- 정지한 분도 정확히 1스텝을 찍는다
- 갭이 상한에 걸려도 마지막 스텝 가격은 실제 체결가다
- **모든 스텝이 각자의 arrived 프레임을 받는다.** 하나라도 건너뛰면 행이 튄다
- 같은 batch identity는 두 번 재생되지 않는다
- 앵커된 사다리에 재생 중인 행이 존재한다
- 배지 3규칙(숨김/체결/증가)과 520ms 만료

## 아직 안 한 것

- 실제 플레이어 렌더로 눈으로 확인하지 않았다. 창을 띄우지 않는 조건으로 작업했으므로, 배포 후보를
  확정하기 전에 실제 화면에서 테두리가 행을 훑는 것을 사람이 봐야 한다.
- simul의 취소 호가 표시(`cancellations`)는 `MarketOrderBookReplayBatch`에 자리는 있으나 우리 시장에
  취소 개념이 없어 항상 비어 있다.
- 대량 체결이 여러 분에 걸쳐 이어지는 경우의 배치 연결(simul의 `carriesSameMinuteBatch`)은 이식하지
  않았다. 우리는 분당 한 배치다.
