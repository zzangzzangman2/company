> [!NOTE]
> 역사 구현 보고서입니다. 현재 정본·미완료·최신 검증은 [PROJECT_STATE.md](../../PROJECT_STATE.md)를 따릅니다.

# Post-push P2: 4×4 가구 충돌 프로필 완료

기준 문서: FAMILY_COMPANY_POST_PUSH_REVIEW_AND_TASKS_2026-08-12.md

## 완료 범위

- OfficeFurnitureCollisionCatalog과 가구별 OfficeFurnitureCollisionProfile을 추가했다.
- 각 의미 셀을 4×4 서브셀로 나누며, 2×1 가구는 8×4, 1×2 파티션은 4×8 마스크를 사용한다.
- 스타터 사무실 가구와 파티션까지 총 12종의 전용 마스크를 등록했다.
- 프로필이 없거나 크기·방향이 일치하지 않는 가구는 기존 전체 셀 충돌로 fail-closed 처리한다.
- 레이아웃의 walkable=false 가운데 가구가 차지한 셀과 실제 벽·비보행 바닥을 분리했다. 따라서 가구 셀은 전용 마스크가, 벽은 전체 셀 충돌이 담당한다.
- 직접 이동, NPC 경로 탐색, 좁은 통로 판정이 모두 같은 반지름 확장 마스크를 사용한다.
- 의자 Interaction과 책상 WorkSurface 좌석 예외도 같은 프로필 판정에 연결했다.
- 런타임 변환 뒤에도 맞도록 매 충돌 질의에서 현재 Tilemap 원점과 두 isometric basis를 읽어 연속 grid 좌표를 계산한다.

## 프로필 전용 QA

Unity 실행 메서드:

FamilyCompany.Editor.OfficeFurnitureCollisionQa.RunProfilesOnly

검증 결과:

- authored profiles: 12
- authored subcells: 288
- unregistered furniture full-cell fallback subcells: 16
- 기존 전체 셀 충돌 오탐 가운데 실제 가족 반지름으로 통과 가능해진 표본: 78
- 의자 소유자는 자기 Interaction 프로필 중심에 진입 가능
- 오류: 0

## 전체 충돌 회귀

Unity 실행 메서드:

FamilyCompany.Editor.OfficeFurnitureCollisionQa.Run

검증 범위:

- 스타터 가구 10종 + 회전의자 Interaction + 비보행 벽
- 8방향
- 가족 4명
- 직접 입력 / NPC 경로
- 30/60/120fps
- TimeScale 1/2/4
- 저속 / 고속

산출물:

- Artifacts/OfficeFurnitureCollisionQa/collision-results.json
- Artifacts/OfficeFurnitureCollisionQa/collision-summary.md
- 실제 4×4 마스크와 8방향 접촉점을 함께 그린 대상별 PNG 12장

## 안전 규칙

- 시각용 groundFootprintPolygonPx를 충돌에 그대로 재사용하지 않는다.
- 충돌은 게임플레이 전용 profile asset이 소유한다.
- 미등록·불일치 가구는 통과시키지 않고 기존 전체 셀 fallback을 사용한다.
- 벽과 void는 가구 마스크의 빈 서브셀 때문에 열리지 않는다.
- 사용자 작업 중이던 무관한 .meta, Live Patch, 역사 카탈로그, Preview Scene 변경은 스테이지하지 않는다.

## 다음 항목

원문 우선순위표에서 남은 P2는 이동 시작/정지/idle/방향 전환 표현 보강이다. 충돌 프로필 완료 후 별도 커밋으로 진행한다.
