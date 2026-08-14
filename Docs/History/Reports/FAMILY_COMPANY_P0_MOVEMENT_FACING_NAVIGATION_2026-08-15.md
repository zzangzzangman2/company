# Family Company P0 movement, facing, and navigation — 2026-08-15

## Scope and ownership

This change addresses free locomotion, same-frame sprite consumption, furniture collision/pathing,
and attendance ingress. It does not change chair transforms, seated sockets, seat pose art, or seat
claim ownership. Attendance reuses the existing workstation reservation interface only.

Unity was run with the exact project version `6000.3.21f1`. The implementation branch is
`codex/movement-facing-navigation`, based on `9109a8c1fb29cf3cdf9f51cfaa3b57483e38eecb`.

## Proven runtime path

1. `OfficeRuntimeAgent.MoveWithCollision` integrates target velocity with
   `OfficeNavigationMotionIntegrator` and asks `OfficeRuntimeCollisionMotion.Resolve` for the actual
   collision-projected displacement.
2. `DirectionalSpriteAnimator.AccumulateTileMotion` aggregates that actual displacement and elapsed
   time for the presentation frame. Legacy CharacterController and grid mover adapters were also
   changed to forward observed transform displacement rather than requested velocity.
3. `OfficeSharedLocomotionRules.ResolveFrame` derives moving state, actual speed, motion direction,
   stabilized display direction, locomotion phase, and distance-based gait. Only adjacent octant
   boundaries can be held for 4 degrees/0.075 seconds; lateral/two-octant changes commit immediately.
4. `DirectionalSpriteAnimator.ApplyFrame` is the sole free-walk SpriteRenderer consumer. It selects
   the transition/walk/idle frame for the resolved direction and reasserts `flipX=false` because all
   eight directions are independently authored.
5. `CaptureLocomotionFrameTrace` observes actual displacement/speed, motion/display direction,
   locomotion phase/clip, final Sprite asset name, flipX, and moving state after that consumer in the
   same frame.

There is no Unity Animator parameter path in Starter Office locomotion. Seating facing remains a
separate final lock while seated and was not modified here.

## Navigation and compatibility changes

- `OfficeRuntimeOccupancy` resolves `OfficeFurnitureGeometryQuery.Shared` as its collision source. A
  canonical footprint mismatch fails explicitly. A legacy/unknown saved kind absent from that query
  blocks its full semantic rectangle; it cannot inherit a partial legacy mask and create a migration
  false negative.
- Known player-editable furniture is checked for all 13 definitions and all four rotations. The
  runtime profile counter must report canonical=1, legacy=0, full-cell=0 for every known fixture.
  An unknown saved fixture must report the full-cell fallback and block all 16 subcells.
- Attendance extends the first live entrance route segment 2.5 cells beyond the outermost floor and
  exposes one reservable ingress segment. Only the owner can move along it; peer bodies and peer
  reservations block the segment. Ownership is released on arrival, absence, unregister, rebuild,
  disable, and destroy.
- The first full collision matrix exposed 80 unsafe endpoints in 10,368 cases: five 2x1 furniture
  kinds, two unique corner coordinates, four equivalent family actors, and four frame/time
  partitions. `OfficeRuntimeCollisionMotion` had refined the diagonal contact and axis slide
  independently but did not exactly revalidate their composed endpoint; interpolation rounding at a
  chamfered subcell edge could therefore put the result on the blocked side. The composed result now
  requires both segment clearance and an exact zero-length endpoint query, with conservative whole
  displacement refinement when either fails.
- Three deterministic furniture layouts each send four simultaneous entrants through the single
  gate and production path/collision services. Every path is bounded by the grid cell count and is
  checked segment-by-segment for static, interaction, and actor penetration.

## Regression evidence

- `OfficeMovementFacingNavigationValidation.RunBatch`: PASS. `seeds=128`, `paths=1152`,
  `movingFrames=1970`, `reverseFacingFrames=0`, `movingDuringPivot=0`,
  `maxFacingError=29.2740deg`, `managedHeapGrowth10k=0`.
- Same-frame lateral trace: 36 West frames and 36 East frames start from an injected stale South
  facing and an injected `flipX=true` before every tick. All frames finish with West/East
  motion/display direction, matching transition/walk Sprite metadata, and `flipX=false`; South/North
  selections are zero.
- Production asset trace: player, sister, father, and mother each run 18 West and 18 East frames
  (`144` frames total). Every frame records actual displacement/velocity, resolved motion/display
  direction, locomotion clip, final Sprite asset name, and final flip state. The selected assets are
  the matching `<member>_west_walk_*` or `<member>_east_walk_*`; front/back walk selections and
  `flipX=true` occurrences are zero.
- Attendance layouts 0/1/2: four entrants each, canonical obstacles `3/3/4`, path queries four per
  layout, replans zero, penetrations zero.
- All editable path obstacles: 12 blocking definitions x four facings = `48` production path/collision
  routes, replans zero and penetrations zero.
- Canonical profile/Sprite matrix: PASS, `profiles=52`, `subcells=1216`, `fallbackSubcells=16`,
  `defaultRadiusClearances=628`, `visualApproaches=416`, `opaqueApproaches=296`,
  `transparentApproaches=120`, `visiblePassThroughs=0`. Transparent paths remain subject to the
  mask, production-radius, path, and access/egress checks; they are not silently accepted as opaque
  collision probes.
- Focused pre-fix boundary reproduction: five affected furniture kinds x four failing partitions =
  20 cases. After exact endpoint revalidation: PASS, endpoint violations zero.
- Full production collision matrix: PASS, `targets=12`, `cases=10368`, `profiles=52`,
  `subcells=1216`, `defaultRadiusClearances=628`, `maxStopVariance=0.00034`.
- Legacy geometry round trips: PASS, all 13 editable definitions x four facings = `52` schema-3
  migrations preserve facing, footprint, default placement anchor, and canonical geometry. Office
  grid T1 and attendance schedule/perimeter regressions also pass.

The pre-fix heatmaps overlay the actual runtime Sprite alpha, canonical 4x4 mask, and the production
actor circle. The 80 matrix rows collapse to two unique local coordinates per affected kind:
`(0.457369,-0.449149)` and `(0.019011,-0.668315)`. Both had zero opaque-Sprite samples but crossed
the canonical collision edge. They were retained as real mask penetrations and fixed; the oracle was
not weakened.

The final Windows build and executable startup evidence is recorded in the task handoff together
with the committed SHA and generated `BUILD_INFO.txt`.
