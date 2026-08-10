import 'dart:convert';
import 'dart:io';

import '../../../simul/flutter_app/lib/game/order_book.dart';

Map<String, Object?> _fillPlan(GameOrderBookFillPlan plan) => <String, Object?>{
  'side': plan.levelSide.name,
  'filledQuantity': plan.filledQuantity,
  'notional': plan.notional,
  'averagePrice': plan.averagePrice,
  'worstPrice': plan.worstPrice,
  'fills': <Map<String, Object?>>[
    for (final fill in plan.fills)
      <String, Object?>{
        'levelIndex': fill.levelIndex,
        'price': fill.price,
        'quantity': fill.quantity,
      },
  ],
};

Map<String, Object?> _transition(GameOrderBookPriceTransition value) =>
    <String, Object?>{
      'price': value.price,
      'consumedUnits': value.consumedUnits,
      'targetReached': value.targetReached,
      'consumedAsks': <Map<String, Object?>>[
        for (final entry in value.consumedAskByPrice.entries)
          <String, Object?>{'price': entry.key, 'quantity': entry.value},
      ],
      'consumedBids': <Map<String, Object?>>[
        for (final entry in value.consumedBidByPrice.entries)
          <String, Object?>{'price': entry.key, 'quantity': entry.value},
      ],
      'fills': <Map<String, Object?>>[
        for (final fill in value.orderedFills)
          <String, Object?>{
            'side': fill.side.name,
            'price': fill.price,
            'quantity': fill.quantity,
            'remainingQuantity': fill.remainingQuantity,
            'structuralBreach': fill.structuralBreach,
            'boundaryCrossed': fill.boundaryCrossed,
          },
      ],
    };

Map<String, Object?> _level(GameOrderBookLevel level) => <String, Object?>{
  'side': level.side.name,
  'price': level.price,
  'quantity': level.quantity,
  'isWall': level.isWall,
  'isStructuralWall': level.isStructuralWall,
  'isStructuralBreached': level.isStructuralBreached,
  'queueRecoveryTargetQuantity': level.queueRecoveryTargetQuantity,
};

Map<String, Object?> _consumptionView(GameOrderBookSnapshot value) =>
    <String, Object?>{
      'asks': <Map<String, Object?>>[
        for (final level in value.asks) _level(level),
      ],
      'bids': <Map<String, Object?>>[
        for (final level in value.bids) _level(level),
      ],
      'totalAskQuantity': value.totalAskQuantity,
      'totalBidQuantity': value.totalBidQuantity,
      'tradeStrength': value.tradeStrength,
      'sourceLastTradePrice': value.sourceLastTradePrice,
      'appliedAskConsumptionByPrice': <String, double>{
        for (final entry in value.appliedAskConsumptionByPrice.entries)
          entry.key.toString(): entry.value,
      },
      'appliedBidConsumptionByPrice': <String, double>{
        for (final entry in value.appliedBidConsumptionByPrice.entries)
          entry.key.toString(): entry.value,
      },
      'appliedCapacityConsumptionUnits':
          value.appliedCapacityConsumptionUnits,
    };

void main(List<String> arguments) {
  const asks = <GameOrderBookLevel>[
    GameOrderBookLevel(
      side: GameOrderBookSide.ask,
      price: 10050,
      quantity: 100,
      isWall: false,
    ),
    GameOrderBookLevel(
      side: GameOrderBookSide.ask,
      price: 10100,
      quantity: 200,
      isWall: false,
    ),
    GameOrderBookLevel(
      side: GameOrderBookSide.ask,
      price: 10150,
      quantity: 300,
      isWall: false,
    ),
  ];
  const bids = <GameOrderBookLevel>[
    GameOrderBookLevel(
      side: GameOrderBookSide.bid,
      price: 10000,
      quantity: 120,
      isWall: false,
    ),
    GameOrderBookLevel(
      side: GameOrderBookSide.bid,
      price: 9950,
      quantity: 240,
      isWall: false,
    ),
    GameOrderBookLevel(
      side: GameOrderBookSide.bid,
      price: 9900,
      quantity: 360,
      isWall: false,
    ),
  ];
  const snapshot = GameOrderBookSnapshot(
    asks: asks,
    bids: bids,
    turnoverEok: 1000,
    fullDayTurnoverEok: 1000,
    executionCapacity: 1000,
    totalAskQuantity: 600,
    totalBidQuantity: 720,
    tradeStrength: 120,
  );
  final fractional = gameOrderBookSnapshotAfterConsumption(
    snapshot: snapshot,
    consumedAskByPrice: <double, double>{10050: 0.5},
    consumedCapacityUnits: 1,
    latestConsumedSide: GameOrderBookSide.ask,
    latestConsumedPrice: 10050,
  );
  final cumulative40 = gameOrderBookSnapshotAfterConsumption(
    snapshot: snapshot,
    consumedAskByPrice: <double, double>{10050: 40},
    consumedCapacityUnits: 40,
    latestConsumedSide: GameOrderBookSide.ask,
    latestConsumedPrice: 10050,
  );
  final repeated40 = gameOrderBookSnapshotAfterConsumption(
    snapshot: cumulative40,
    consumedAskByPrice: <double, double>{10050: 40},
    consumedCapacityUnits: 40,
    latestConsumedSide: GameOrderBookSide.ask,
    latestConsumedPrice: 10050,
  );
  final cumulative70 = gameOrderBookSnapshotAfterConsumption(
    snapshot: repeated40,
    consumedAskByPrice: <double, double>{10050: 70},
    consumedCapacityUnits: 70,
    latestConsumedSide: GameOrderBookSide.ask,
    latestConsumedPrice: 10050,
  );
  final hiddenTinyRemainder = gameOrderBookSnapshotAfterConsumption(
    snapshot: snapshot,
    consumedAskByPrice: <double, double>{10050: 95},
    consumedCapacityUnits: 95,
    latestConsumedSide: GameOrderBookSide.ask,
  );
  const structuralSnapshot = GameOrderBookSnapshot(
    asks: <GameOrderBookLevel>[
      GameOrderBookLevel(
        side: GameOrderBookSide.ask,
        price: 10050,
        quantity: 100,
        isWall: true,
        structuralStrength: 4,
        isStructuralWall: true,
        queueRecoveryTargetQuantity: 100,
      ),
    ],
    bids: bids,
    turnoverEok: 1000,
    fullDayTurnoverEok: 1000,
    executionCapacity: 1000,
    totalAskQuantity: 100,
    totalBidQuantity: 720,
    tradeStrength: 240,
  );
  final structuralBreach = gameOrderBookSnapshotAfterConsumption(
    snapshot: structuralSnapshot,
    consumedAskByPrice: <double, double>{10050: 90},
    consumedCapacityUnits: 90,
    latestConsumedSide: GameOrderBookSide.ask,
    latestConsumedPrice: 10050,
  );

  final ordinaryTurnovers = <double>[
    1,
    50,
    100,
    500,
    2000,
    5000,
    10000,
    15000,
    60000,
  ];
  final golden = <String, Object?>{
    'schema': 'simul-order-book-golden-v1',
    'source': 'simul/flutter_app/lib/game/order_book.dart',
    'cadence': <String, Object?>{
      'ordinaryTurnoversEok': ordinaryTurnovers,
      'ordinaryPulses': <int>[
        for (final turnover in ordinaryTurnovers)
          gameOrderBookPulsesPerMarketMinute(
            fullDayTurnoverEok: turnover,
            currentPrice: 10000,
            previousTradePrice: 10000,
            previousClose: 10000,
          ),
      ],
      'fastThreeTicks': gameOrderBookPulsesPerMarketMinute(
        fullDayTurnoverEok: 1,
        currentPrice: 10150,
        previousTradePrice: 10000,
        previousClose: 10000,
        market: 'main',
      ),
      'extremeSixTicks': gameOrderBookPulsesPerMarketMinute(
        fullDayTurnoverEok: 1,
        currentPrice: 10300,
        previousTradePrice: 10000,
        previousClose: 10000,
        market: 'main',
      ),
      'fivePercentSessionMove': gameOrderBookPulsesPerMarketMinute(
        fullDayTurnoverEok: 100,
        currentPrice: 10500,
        previousTradePrice: 10500,
        previousClose: 10000,
        market: 'main',
      ),
      'aboveSixPercentSessionMove': gameOrderBookPulsesPerMarketMinute(
        fullDayTurnoverEok: 100,
        currentPrice: 10610,
        previousTradePrice: 10610,
        previousClose: 10000,
        market: 'main',
      ),
      'unsupportedSparseImbalance': gameOrderBookPulsesPerMarketMinute(
        fullDayTurnoverEok: 1,
        currentPrice: 10000,
        previousTradePrice: 10000,
        previousClose: 10000,
        executionStrength: 500,
      ),
      'supportedLiquidImbalance': gameOrderBookPulsesPerMarketMinute(
        fullDayTurnoverEok: gameOrderBookSparseFullDayTurnoverEok,
        currentPrice: 10000,
        previousTradePrice: 10000,
        previousClose: 10000,
        executionStrength: 500,
        executionSamplePrints: gameOrderBookMinimumImbalanceSamplePrints,
        executionSampleTurnoverEok:
            gameOrderBookMinimumImbalanceSampleTurnoverEok,
      ),
      'paused': gameOrderBookPulsesPerMarketMinute(
        fullDayTurnoverEok: 60000,
        currentPrice: 10000,
        previousTradePrice: 10000,
        previousClose: 10000,
        playbackActive: false,
      ),
    },
    'frames': <String, Object?>{
      'minute': 617,
      'slotFrames': <int>[
        for (var slot = 0;
            slot <= gameOrderBookMaximumPulsesPerMarketMinute;
            slot += 1)
          gameOrderBookLiquidityPulseFrame(
            marketMinute: 617,
            slotIndex: slot,
          ),
      ],
      'pendingAfterSlotTwoThroughSeven': gameOrderBookPendingPulseFrames(
        marketMinute: 617,
        afterLiquidityPulse: gameOrderBookLiquidityPulseFrame(
          marketMinute: 617,
          slotIndex: 2,
        ),
        throughSlotIndex: 7,
      ),
    },
    'cumulativeCapacity': <String, Object?>{
      'capacity': 1001,
      'fourSlots': <int>[
        for (var slot = 0; slot <= 4; slot += 1)
          gameOrderBookCumulativeSlotCapacity(
            executionCapacity: 1001,
            slotIndex: slot,
            pulsesPerMarketMinute: 4,
          ),
      ],
      'sevenSlots': <int>[
        for (var slot = 0; slot <= 7; slot += 1)
          gameOrderBookCumulativeSlotCapacity(
            executionCapacity: 1001,
            slotIndex: slot,
            pulsesPerMarketMinute: 7,
          ),
      ],
    },
    'marketMath': <String, Object?>{
      'positivePriceChangePercent': gameOrderBookPriceChangePercent(
        price: 81300,
        previousClose: 77800,
      ),
      'negativePriceChangePercent': gameOrderBookPriceChangePercent(
        price: 77500,
        previousClose: 77800,
      ),
      'balancedStrength': gameOrderBookExecutionStrength(
        buyQuantity: 0,
        sellQuantity: 0,
      ),
      'buyOnlyStrength': gameOrderBookExecutionStrength(
        buyQuantity: 50,
        sellQuantity: 0,
      ),
      'sellOnlyStrength': gameOrderBookExecutionStrength(
        buyQuantity: 0,
        sellQuantity: 50,
      ),
      'maxQuoteUnknownShares': gameMaximumQuoteQuantity(null),
      'maxQuoteTenMillionShares': gameMaximumQuoteQuantity(10000000),
    },
    'fills': <String, Object?>{
      'buy450Through10150': _fillPlan(
        gameOrderBookLimitFillPlan(
          snapshot: snapshot,
          isBuy: true,
          requestedQuantity: 450,
          limitPrice: 10150,
          maximumNotional: 2000000000,
        ),
      ),
      'sell250Through9950': _fillPlan(
        gameOrderBookLimitFillPlan(
          snapshot: snapshot,
          isBuy: false,
          requestedQuantity: 250,
          limitPrice: 9950,
          maximumNotional: 2000000000,
        ),
      ),
      'skipConsumedBestAsk': _fillPlan(
        gameOrderBookLimitFillPlan(
          snapshot: snapshot,
          isBuy: true,
          requestedQuantity: 150,
          limitPrice: 10100,
          maximumNotional: 2000000000,
          alreadyConsumedByPrice: <double, double>{10050: 100},
        ),
      ),
    },
    'transitions': <String, Object?>{
      'risePartialSecondAsk': _transition(
        gameOrderBookPriceTransitionTowardTarget(
          snapshot: snapshot,
          previousPrice: 10000,
          targetPrice: 10150,
          availableUnits: 250,
          market: 'main',
        ),
      ),
      'fallThroughSecondBid': _transition(
        gameOrderBookPriceTransitionTowardTarget(
          snapshot: snapshot,
          previousPrice: 10050,
          targetPrice: 9950,
          availableUnits: 360,
          market: 'main',
        ),
      ),
    },
    'consumption': <String, Object?>{
      'fractionalHalfIgnored': _consumptionView(fractional),
      'cumulative40': _consumptionView(cumulative40),
      'repeated40Idempotent': _consumptionView(repeated40),
      'cumulative70AppliesOnlyDelta': _consumptionView(cumulative70),
      'tinyRemainderHidden': _consumptionView(hiddenTinyRemainder),
      'structuralWallBreachAtNinetyPercent':
          _consumptionView(structuralBreach),
    },
    'splitPrints': <String, Object?>{
      'one': gameOrderBookSplitTradeQuantity(
        assetId: 'hanbit_telecom',
        day: 6015,
        minute: 617,
        liquidityPulse: 4929,
        quantity: 1,
      ),
      'oneHundredTwentyThree': gameOrderBookSplitTradeQuantity(
        assetId: 'hanbit_telecom',
        day: 6015,
        minute: 617,
        liquidityPulse: 4929,
        quantity: 123,
      ),
      'tenThousand': gameOrderBookSplitTradeQuantity(
        assetId: 'hanbit_telecom',
        day: 6015,
        minute: 617,
        liquidityPulse: 4929,
        quantity: 10000,
      ),
    },
  };

  final encoded = '${const JsonEncoder.withIndent('  ').convert(golden)}\n';
  if (arguments.isEmpty) {
    stdout.write(encoded);
    return;
  }
  File(arguments.single).writeAsStringSync(encoded);
}
