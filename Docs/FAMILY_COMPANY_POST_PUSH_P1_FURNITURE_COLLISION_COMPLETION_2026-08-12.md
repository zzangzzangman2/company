# Family Company post-push P1 furniture collision completion — 2026-08-12

Source task: `FAMILY_COMPANY_POST_PUSH_REVIEW_AND_TASKS_2026-08-12.md`

## Result

Furniture contact motion now preserves the starter office's original full-grid nearest-cell
semantics while refining a blocked move to the collision boundary. Player input and autonomous
agents share the same production collision resolver. Fast local `WorldToCell` and cached-world-
center approximations were rejected because the Windows runtime QA proved that they changed
movement in the translated isometric office.

Production commit: `612b461`

## Furniture collision matrix

Unity method: `FamilyCompany.Editor.OfficeFurnitureCollisionQa.Run`

Result:

`FAMILY_COMPANY_OFFICE_FURNITURE_COLLISION_QA: PASS`

- Targets: 12
- Cases: 10,368
- Failures: 0
- Maximum 30/60/120fps contact-stop variance: 0.00011 world unit
- Acceptance limit: 0.02 world unit
- Generated artifacts: `Artifacts/OfficeFurnitureCollisionQa/`
- Machine-readable result: `collision-results.json`
- Human summary: `collision-summary.md`
- Direction images: 12 PNG files, one eight-direction image per target

The matrix records eight directions, four family member IDs, 30/60/120fps, TimeScale 1/2/4,
low-speed center contact, high-speed corner sliding, deterministic repeats, and NPC path
blocking/detours. Equivalent calculations are evaluated once and copied only when every actual
production input is identical: the isolated fixture has no other actors or member reservations,
all four current family agents use the same radius, and frame rate/time scale are not inputs to A*
path planning. All 10,368 requested result rows remain in the JSON report.

## Covered targets

Static hard collision:

- `coffee_table`
- `desk_with_pc`
- `document_bookcase`
- `fax_copier`
- `filing_cabinet`
- `meeting_table`
- `potted_plant`
- `reception_counter`
- `sofa`
- `water_dispenser`
- unwalkable floor/wall

Interaction collision:

- `swivel_chair`

Every target passed 864/864 rows with zero recorded static, interaction, or agent penetration.

## Windows player regression

Artifact directory: `Artifacts/PostPushFurnitureCollisionQaV5/`

Result: `FAMILY_COMPANY_STARTER_TILE_MAIN_FLOW: PASS`

The final production nearest-cell implementation passed:

- runtime desk placement and removal;
- narrow-corridor routing;
- all eight player movement directions;
- Filing, Printer, Water, Coffee, and OpenArea micro-action destinations;
- direct desk, reception-counter, and NPC collision at TimeScale 4;
- autonomous father/mother meeting seating with visible assigned chairs;
- all four family members seated and working simultaneously;
- continuous SitDown/Work/StandUp presentation;
- contract, save/load, layout hash, and actor ownership checks.

Collision and seating measurements remained within the authored limits:

- Static/interaction/agent penetration: 0
- Seat contact and chair-to-desk anchor error: 0 px
- Maximum transition pelvis step: player 0.117 px; family NPCs 1.900 px
- Transition monotonic violations: 0
- Chair overlay sorting: seated person = rendered chair order + 1

## Scope boundary

This P1 result validates the current authored tile footprints and interaction layers. Per-sprite
subcell or polygon collision profiles remain a separate P2 visual-precision enhancement; they are
not required to prevent walking through the current starter-office furniture.
