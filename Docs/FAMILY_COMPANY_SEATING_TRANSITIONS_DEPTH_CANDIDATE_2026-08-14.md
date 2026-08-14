# Seating Transitions and Depth Candidate — 2026-08-14

## Candidate scope

This candidate is based on `24bae1a` and preserves the complete stabilized worktree that followed it. It finalizes four-family `SitDown`/`Work`/`StandUp`, occupied-chair compositing, continuous furniture depth, grounded feet, and collision-safe seat egress. It does not merge, push, or cherry-pick movement candidate `5ba3971`.

The occupied-chair implementation has one source of truth, `OfficeSeatedUpperBodyProtectionRules`:

1. the canonical complete chair foreground stays continuous while the seat is occupied;
2. a narrow lower-body crop redraws only the 1,816 opaque seat-rim pixels that must naturally cover legs;
3. a pose-pelvis upper-body sprite redraw protects torso, head, and typing hands from chair/desk clipping.

The previously competing `OfficeOccupiedChairForegroundRules` type is absent and has no references. This also removes the source of the former validator `CS0103`.

## Acceptance evidence

| Requirement | Result | Primary evidence |
| --- | --- | --- |
| Four family members, `SitDown`/`Work`/`StandUp` | PASS; 4/4, 6/6, 4/4 and 56/56 primary closeups | `Logs/seating-final-player-d3d11-r4.log` |
| Seat residual | PASS; strict `<=0.9px`, generated maximum `0.899px` | same player log |
| Hand-to-keyboard residual | PASS; strict `<=3.5px`, generated budget `3.499px` | same player log |
| Facing lock and older-sister 90-degree pop | PASS; locked `SitDown..LeavingSeat`, mismatch 0, max octant delta 0 | same player log; `Logs/seating-final-facing-lock.log` |
| Mother/others upper body uncut; lower body naturally hidden | PASS; invalid upper overlap 0, typing-hand overlap 0, 56/56 captures | same player log; `Artifacts/SeatingTransitionFinalD3D11/` |
| Leaving-seat depth and safe side/front egress | PASS; foreground retained to safe anchor, 64 static cases, rear candidates 0, overlap 0 | `Logs/seating-final-egress-r2.log`; player log |
| Chair/furniture/agent penetration | PASS; seating penetration 0, main-flow agent penetrations 0 | player log; `Logs/seating-final-main-flow-d3d11.log` |
| Foot grounding | PASS; 24/24 Work frames, maximum shoe gap `0.284px` | `Logs/seating-final-foot-grounding.log` |
| Continuous depth permutations | PASS; 120 permutations, 8 directions, 4 footprints, 30/60/144 fps | `Logs/seating-final-hybrid-depth.log` |
| Windows D3D11 normal main flow | PASS; attendance, meeting seating, four desks, traffic, live furniture, save/load | `Logs/seating-final-main-flow-d3d11.log` |
| Furniture-depth player | PASS | `Logs/seating-final-furniture-depth-d3d11.log`; `Artifacts/FurnitureDepthFinalD3D11/` |
| Windows build | PASS; Unity `6000.3.21f1`, warnings 0 | `Logs/seating-final-windows-build-r4.log` |

Additional static evidence:

- `Logs/seating-final-chair-foreground-r2.log`: 9,881 complete foreground pixels, 1,816 lower-occluder pixels, exact pivot alignment.
- `Logs/seating-final-occlusion-depth-r2.log`: nine phases, eight exit directions, atomic safe-anchor release, seated plane order.
- `Logs/seating-final-typing-contact.log`: 24/24 typing frames and all four family members.
- `Logs/seating-final-seat-profiles.log`: 56 approvals, rotation 0, scale 1.

The final visual review includes `seating-transition-work-overview-1920x1080.png`, each family member's 56 closeups, and `seating-transition-egress-after-overview-1920x1080.png` under `Artifacts/SeatingTransitionFinalD3D11/`. The mother and father closeups show intact heads, torsos, and hands; chair/desk redraw is limited to the intended lower-body depth.

## Movement candidate `5ba3971` integration memo

`5ba3971` must not be cherry-picked as a blind follow-up. Its merge base with this branch is `9ad8eb7b88e85b5f6ff70161a770add48793b84b`, and the three shared runtime files below have semantic overlap:

### `DirectionalSpriteAnimator.cs`

The seating candidate adds the `SitDown` through `LeavingSeat` facing lock and an explicit leaving-seat completion boundary. The movement candidate replaces free-motion frame choice with `OfficeSharedLocomotionRules.ResolveFrame`, actual/requested displacement telemetry, `_tileIsMoving`, shared stride, and interaction-facing readiness.

Manual merge rule: run shared actual-motion resolution only for free locomotion. A live seating lock remains the final facing/frame authority, and shared interaction-facing/pivot logic must not rotate the actor until the leaving-seat lock has been explicitly completed at the safe anchor.

### `OfficeRuntimeAgent.cs`

The seating candidate owns seat pose offsets, per-frame depth, typing contacts, egress selection/reservation, and atomic release. The movement candidate adds shared upcoming-path buffers, path reservation calls, interaction-facing readiness, and stationary-pivot rules.

Manual merge rule: keep the movement candidate's actual-motion path handling for ordinary navigation, but gate its stationary pivot and interaction-ready checks behind `!IsOfficeSeatingFacingLocked`. Preserve reserve-before-stand and do not replace the dedicated safe egress segment with an ordinary two-cell path reservation.

### `OfficeRuntimeOccupancy.cs`

The seating candidate exposes seat-egress reservation ownership (`HasReservation`) used to prove atomic egress. The movement candidate changes contact tolerance metrics and reuses reservation/corridor list buffers.

Manual merge rule: preserve the movement tolerance and allocation reduction only after the egress path's `TryReservePath` plus clearance validation remains an all-or-nothing acquisition. A shared mutable `_reservationRequestBuffer` must not be reused by a nested or re-entrant egress request, and seat/egress reservations must remain owned until safe-anchor release.

`OfficeRuntimeActorRegistry`, `OfficeNavigationMotionRules`, `PrototypeBootstrap`, and `ScenePreviewJump` are direct files in `5ba3971` but do not overlap this candidate's seating edits. They still need regression testing because they feed the same movement/player flow.

Recommended integration order after this candidate is applied: (1) port `OfficeNavigationMotionRules` and shared animator telemetry while preserving the seating override, (2) port ordinary Agent path/pivot changes behind the seating lock, (3) port Occupancy buffer/tolerance changes while retaining atomic egress semantics, then (4) rerun the strict seating player, normal D3D11 main flow, furniture-depth player, and warning-zero Windows build.
