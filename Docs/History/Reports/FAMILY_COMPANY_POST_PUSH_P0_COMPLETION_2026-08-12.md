> [!NOTE]
> 역사 구현 보고서입니다. 현재 정본·미완료·최신 검증은 [PROJECT_STATE.md](../../PROJECT_STATE.md)를 따릅니다.

# Post-push P0 seating completion — 2026-08-12

Source task: `FAMILY_COMPANY_POST_PUSH_REVIEW_AND_TASKS_2026-08-12.md`

## Result

- SitDown placement follows elapsed presentation time with `SmoothStep`, not sprite frame index.
- SitDown uses 0.62 seconds and StandUp uses 0.56 seconds.
- Office time at 2x/4x no longer speeds up the human sit/stand gesture.
- Long render frames are step-limited to 1.9 screen pixels relative to the chair cushion.
- SitDown 4, Work 6, and StandUp 4 authored poses are each rendered at least once.
- Repeated frame 0 callbacks no longer recapture the transition start point.
- Chair foreground overlay ON/OFF comparison captures are generated for all four family members.

## Final Windows player QA

Player build: `C:\Users\godho\Downloads\Family\FamilyCompany_Playtest\FamilyCompany.exe`

Artifacts: `Artifacts/PostPushP0SeatingQaV4/`

| Member | Seat contact | Max pelvis step | Reverse steps | Sit/Work/Stand |
|---|---:|---:|---:|---:|
| player | 0.000 px | 0.214 px | 0 | 4 / 6 / 4 |
| older_sister | 0.000 px | 1.900 px | 0 | 4 / 6 / 4 |
| father | 0.000 px | 1.900 px | 0 | 4 / 6 / 4 |
| mother | 0.000 px | 1.900 px | 0 | 4 / 6 / 4 |

All four also passed rotation `0°`, scale deviation `0%`, animated-anchor error `0.000 px`,
unique seat claims, chair/desk sorting, and all occupancy penetration counters at zero.

The final hidden Windows player run exited with code 0 and logged:

```text
STARTER_OFFICE_FOUR_SEAT_WORK_QA_PASS
FAMILY_COMPANY_STARTER_TILE_MAIN_FLOW: PASS
```

Visual review of the ON/OFF captures found that the chair front overlay covers only the intended
near back/seat edge. It does not cover the waist, legs, or feet of father or mother, so the overlay
asset remains enabled.

## Pushed commits

- `ce5069b` — continuous elapsed-time seating and overlay QA
- `4e1ac34` — canonical `Downloads/Family` playtest path
- `56b6e1b` — canonical-repository Git trust scoped inside build automation
- `9d3047c` — real-time seating presentation at accelerated office time
- `0173f7b` — chair-relative motion measurement
- `7224f54` — long-frame transition step limiting
