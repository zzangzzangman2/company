# P1.5 Native Deterministic Smart Interaction Shadow 완료 보고서

## 범위

- 기존 P1 Micro Action과 `WeightedPick` 선택 결과 유지
- 외부 Behavior Tree, Utility AI, GOAP 패키지 미설치
- 순수 C# Interaction Definition/Catalog 및 Shadow Utility score trace 추가
- GameState, Save DTO, OfficeRuntimeAgent, `Packages/manifest.json` 미변경

## 구현

- 현재 13종 Micro Action을 20개 Interaction Definition으로 표현했다. 회의 Macro의 비디오 회의 후보,
  표준 사무실 후보, 후보가 모두 막힌 경우의 현재 위치 fallback을 구분한다.
- 각 정의는 Interaction ID, Micro Action, semantic location, target template, furniture kind,
  duration, capacity, cooldown, approach/reservation policy와 기존 역할별 weight를 갖는다.
- 기존 후보는 그대로 생성하며 Catalog parity QA를 위해 읽기 전용 snapshot만 노출한다.
- Shadow 점수는 `base weight×20 + macro + need + novelty + availability - repetition`의 정수 합이다.
  후보를 OfferId로 정렬하고 최대점수 120 이내 top band에서 StableRandom으로 선택한다.
- score trace에는 legacy/shadow Offer, 실제 resolved target, duration, partner와 모든 점수 항목을 기록한다.

## GitHub 참고 판정

- NPBehave: 트리 설치가 아니라 중단 시 cleanup을 반드시 완료하는 계약만 향후 lifecycle에 참고한다.
- CrystalAI: Consideration별 점수 분해 개념만 내부 정수 점수로 적용했다.
- TotalAI: 가구가 Interaction을 광고하는 Mapping 개념만 Catalog 경계에 반영했다.
- GOAP: 현재 단일 Micro Action에는 도입하지 않고 다단계 업무가 생길 때만 재검토한다.

## QA

- 기준선 `OfficePresentationMicroActionValidation`: PASS
- `OfficeInteractionCatalogValidation`: PASS, 20 definitions / 13 actions / 80 parity cases
- `OfficeInteractionUtilityShadowValidation`: PASS, 128 seeds / 13,777 traces / 68,807 scores
- 후보 정방향·역방향 Shadow 선택 및 score trace: 동일
- 변경 전후 기존 4시간 행동 signature: 동일
- 저장 schema v7, 1분 step/4시간 jump, save/load, capacity, conversation pair: PASS
- Shadow signature: `363f11108739c53997036681dacd25b25d0f645b586cb269f09a84ddc25cef3b`

## 후속 단계

1. 실제 `PlacedOfficeFurniture`에서 Offer를 생성하는 Runtime resolver
2. Shadow 통계를 검토한 점수 조정과 별도 승인 후 Utility 선택 활성화
3. Selected→Reserving→Navigating→Aligning→Performing→Finishing lifecycle 및 idempotent Abort cleanup

이 후속 단계 전까지 legacy `WeightedPick`이 유일한 authoritative selector다.
