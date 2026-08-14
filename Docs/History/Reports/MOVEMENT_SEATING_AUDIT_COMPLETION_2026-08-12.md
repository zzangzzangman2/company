> [!NOTE]
> 역사 구현 보고서입니다. 현재 정본·미완료·최신 검증은 [PROJECT_STATE.md](../../PROJECT_STATE.md)를 따릅니다.

# Movement & Seating Audit Completion — 2026-08-12

감사 원본: `Downloads/FAMILY_COMPANY_MOVEMENT_SEATING_AUDIT_2026-08-12.md`

## 완료 범위

- 보행: 실제 이동거리 누적 위상, 30/60/120 FPS 독립성, 속도 1.15/1.65 독립성, 100ms 정지 정착,
  짧은 셔플, 135° 이상 피벗 전환을 순수 규칙과 Windows 플레이어 런타임에 연결했다.
- 경로: 의미 BFS와 예약은 유지하고, 정적 선분 안전성과 동적 인물 여유가 확보된 같은 축 구간만
  표시용으로 줄인다. 코너 방향 선행은 실제 충돌 이동을 바꾸지 않는다.
- 보행 아트: 가족 4명 × 8방향 × 6프레임 = 192장을 256×256/알파/루트·골반·발 proxy와
  가족별 8방향 루프로 검사했다. 실패와 흔들림 경고는 0건이며 manifest v2에 192개 개별 승인을 저장했다.
- 착석: `OfficeCharacterSeatPoseCatalog` v5에 Northwest SitDown 4 + Work 6 + StandUp 4를 가족별로
  저장해 56개 프로필을 승인했다. 각 항목은 실제 신체 안 pelvis/hand, scale 1, rotation 0, source SHA-256을 가진다.
- 런타임: 56개 완전성이 확인된 경우에만 Animated를 사용하며, 실패 시 승인 Work/0 정적으로 닫힌다.
  렌더 틱당 한 장만 전진해 배속이나 긴 프레임에서도 SitDown/StandUp 원화를 건너뛰지 않는다.

## 최종 검증

- Unity `6000.3.21f1` Windows x64 release build: warnings `0`, errors `0`.
- 숨김 `-batchmode -nographics -familyCompanyTileRuntimeQa`: 종료 코드 `0`.
- 숨김 그래픽 렌더 player QA: 종료 코드 `0`.
- 8방향 샘플: `8/8`, 거리 위상과 표시 frame 일치.
- 교차로·런타임 책상 추가/제거·좁은 통로·책상/카운터/NPC 충돌: PASS, agent penetration `0`.
- 착석: 네 가족 각각 SitDown `4/4`, Work `6/6`, StandUp `4/4`; seat contact와 animated anchor
  error `0.000px`; rotation `0°`; scale deviation `0%`; chair base < occupant < chair front.
- 계약·저장/불러오기와 Starter Tile Main Flow: PASS.

## 재현 명령

```text
python Tools/build_high_motion_qa.py --output <artifact-folder>
python Tools/build_office_seating_qa.py --output <artifact-folder>
FamilyCompany.exe -batchmode -nographics -familyCompanyTileRuntimeQa -logFile <log-path>
```

## 2026-08-12 Meeting seating and empty-chair follow-up

- Long NPC `MeetingRoom` actions now resolve to a seated video meeting at each member's assigned
  workstation. Direct player interaction with the physical meeting table is unchanged.
- Releasing a seat no longer hides the chair-front sprite. That layer contains the visible back and
  near edge, so an empty chair remains complete while occupancy only changes depth ordering.
- Final approach uses SmoothStep deceleration inside 0.48 world units and clamps displacement to the
  remaining distance, preventing waypoint overshoot and abrupt stops.
- The real 08:00 schedule reproduced `father,mother` seated with `Meeting` activity,
  `occupiedChairVisible=true`, `emptyChairVisible=true`, and agent penetration `0`.

회사의 작업 규칙에 따라 Unity Editor와 플레이어는 모두 숨김/배경 모드로 실행했다.
