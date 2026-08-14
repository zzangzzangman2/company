> [!NOTE]
> 역사 구현 보고서입니다. 현재 정본·미완료·최신 검증은 [PROJECT_STATE.md](../../PROJECT_STATE.md)를 따릅니다.

# P1.5 실제 가구 Interaction Offer Resolver 완료 보고서

## 완료 범위

- 실제 `PlacedOfficeFurniture` 인스턴스별 `OfficeInteractionOffer` 생성
- Definition의 furniture kind, semantic location, capacity, approach policy를 런타임 목적지까지 전달
- 기존 Occupancy와 cardinal PathService를 이용한 접근 칸·도달 가능성 Hard Gate
- 활성 Micro Action의 Interaction ID를 Coordinator → Runtime Agent → Workstation Service로 전달
- 선택된 OfferId와 FurnitureId를 `OfficeRuntimeDestination`에 보존
- Occupancy revision 변경 시 동일 intent 재해석
- 기존 `WeightedPick`, Shadow Utility, GameState, Save schema v7 유지

## 동작 계약

```text
OfficeInteractionDefinition
    ↓ OfficeInteractionOfferFactory
현재 OfficeGrid.Furniture의 실제 FurnitureId별 Offer
    ↓ OfficeRuntimeInteractionOfferResolver
열린 접근 칸 + 기존 PathService로 도달 가능한 접근 칸만 유지
    ↓ StableRandom
Offer 선택 → 접근 칸 선택 → OfficeRuntimeDestination
```

- 가구가 삭제되면 해당 Offer는 0개다.
- 가구가 이동하면 새 footprint에서 접근 칸을 다시 계산한다.
- 모든 접근 칸이 막히거나 현재 위치에서 경로가 없으면 해당 Offer는 0개다.
- 같은 kind의 가구가 2개면 서로 다른 `interactionId@furnitureId` Offer 2개다.
- capacity는 각 Offer에 Definition 값으로 복사된다. 실제 인스턴스별 reservation state는 후속 lifecycle 단계에서 활성화한다.
- 캐시된 Transform, Renderer, GameObject 이름 검색은 사용하지 않는다.

## 검증

- `OFFICE_INTERACTION_OFFER_EXTERNAL: PASS`
  - 정수기 삭제
  - 정수기 이동과 이전 접근 칸 미사용
  - 복사기 삭제
  - 서류장 모든 cardinal 접근점 가구 차단
  - 세로 장벽으로 정수기 경로 단절
  - 소파 이동
  - 커피 테이블 2개 → 별도 Offer 2개
  - 커피 테이블 Offer별 capacity 2 유지
  - assigned father desk → `desk-typing@desk_father`
- Unity 6000.3.21f1 Roslyn response를 사용한 Simulation, Presentation.Unity, Editor 전체 소스 컴파일: 오류 0
- Unity Editor QA `OfficeRuntimeInteractionOfferValidation.RunBatch` 추가
- Unity 실행 시도는 로컬 라이선스 오류 `No valid Unity Editor license found` / return code 198로 중단됐다. 코드나 QA assertion 실패가 아니라 Editor가 프로젝트 로드 전에 종료된 상태다.

## 후속 단계

1. Unity 라이선스 복구 후 Offer Editor QA, 기존 Catalog/Shadow QA, Prototype/Navigation, 숨김 Player Main Flow 회귀 실행
2. 실제 Offer availability와 path cost를 Shadow score trace에 추가
3. Shadow 비교 결과 검토 후 Utility selector 활성화를 별도 커밋으로 결정
4. 인스턴스별 reservation과 Selected → Reserving → Navigating → Performing → Finishing/Abort cleanup lifecycle 구현
