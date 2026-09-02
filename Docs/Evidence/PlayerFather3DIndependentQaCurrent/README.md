# Player/Father 3D independent QA evidence

Status: `CANDIDATE_USER_APPROVAL_REQUIRED`  
Production eligibility: `false`  
Final hidden D3D11 source run:
`Artifacts/FatherBrightnessFinal-20260902-165000/` (tile centring, ground alignment, desk detour and
brightness all from this run; the earlier `FatherStandingGroundAlignedFinal-20260902-141500` and
`FatherDeskClearanceFinal-20260902-160500` runs carried the same locomotion values).

Brightness (user request, 2026-09-02): candidate-only material tint gain Player `1.26` / Father
`1.28` raised the isolated same-tile silhouette luma from `93.9/73.7` to `118.2/93.2` (ratio `0.789`,
white clipping `3.0%/0%`). See `player-father-brightness-before-after.png` and
`Docs/FAMILY_CHARACTER_SCALE_COLOR_STANDARD_2026-09-02.md` (rule C4). `family-size-color-standard-sheet.png`
puts the 3D pair and the 2D family at one screen scale.

## What was wrong (2026-09-02 correction)

The user reported that in the walk GIF the Player walks along the middle band of the floor tiles
while the Father appears to step on the tile lines. Two earlier same-day candidates tried to fix
this by moving the Father's fixed standing offset to `(0.037517, 0.5)` and then `(-0.24, 0.5)`, tuned
until a 2D shoe-pixel centroid matched the Player's. That measurement mixes rendered shoe height with
floor position, so equal centroids were reached by moving the Father's actual feet 0.38 cells forward
and 0.28 cells left, onto the tile corner. Bone-based planted-foot checks recorded the regression:
planted frames touching a tile line went from `8/61` to `57/61`, and the foot-midpoint tile error
from `1.5px` to `19.3px`.

The real cause is vertical. Both production walk clips carry the hips above the bind pose that the
presenter's bounds lift grounds, so the lowest skinned vertex over a full walk cycle floats above the
3D ground plane: Player `0.138` and Father `0.429` office units. In the isometric view the Father's
soles therefore drew about `12-15px` higher on screen than the Player's for the same floor
position, which reads as standing on the far tile line even though his foot bones were exactly on
the tile centre.

## Fix

- Father's fixed standing offset is restored to the measured foot-at-root value
  `(0.037517, 0.138023)`.
- New candidate-only `AlignCandidateStandingGround` in `Family3DProductionPresenter`: at bind time
  each actor's walk cycle is sampled at 24 phases, the lowest baked skinned vertex is found, and the
  Father's standing/walking visual ground is lowered by the difference to the Player
  (`referenceCycleLowestVertex=0.1379 targetCycleLowestVertex=0.4288 targetCorrection=-0.2910`).
  It is one constant offset: no contact-frame root correction, seated presentation untouched,
  production/default untouched.
- Candidate peer radii are Player/Father `0.475/0.578`. The `0.940` value only existed to cover the
  removed `0.36` visible host advance.
- The QA no longer gates on the shoe-pixel lane median. It gates the Father on the same bone-based
  foot-midpoint tile error as the Player and on the new ground clearance (lowest skinned vertex
  over the walk within `0.05` office units of the Player).

## Final receipt

| Metric | Player | Father |
| --- | ---: | ---: |
| foot-midpoint tile error median / max | `2.227 / 6.129px` | `1.464 / 4.306px` |
| foot-midpoint local offset (x / forward) | `-0.0018 / 0.0348` | `-0.0030 / 0.0267` |
| planted frames touching a tile line | `2 / 61` | `8 / 61` |
| minimum planted sole clearance | `1.741px` | `0.316px` |
| walk lowest skinned vertex median / min | `0.1473 / 0.1376` | `0.1502 / 0.1364` |
| same-tile lowest skinned vertex | `0.1493` | `0.1527` |
| same-tile shoe pixel centroid delta (info) | — | `-0.201 / 1.582px` |

Route centre-line error stays `0.000002/0.000157`; head-on rendered overlap `0px`, agent
penetrations `0`, blocked moves `47`, then `Working/Working` at `seat_player/seat_father` with seated
centre error `0/0` and static/interaction/agent violations `0/0/0`.

The strict test that projects every vertical 3D shoe-side pixel into one flat floor diamond still
does not pass and is retained as a failure, not used to negate the visual alignment.

## Desk detour proof (2026-09-02, run `Artifacts/FatherDeskClearanceFinal-20260902-160500/`)

The QA forces both agents across a blocking V31 desk: Player `(3,8)->(3,2)` through the desk at
cells `(3..4,5)`, Father `(7,8)->(11,8)` through the desk at `(9..10,8)`. Both reached their targets
in `138` frames with static/interaction violations `0/0`.

The user then noticed arms sinking into desk tops while walking past. Measured cause: the enlarged
candidate bodies swing their arms out to `0.514` (Player) / `0.407` (Father) world units from the
agent centre, while the furniture clearance radius was `0.22` and one grid cell is only about
`0.79` world units across its edge normal, so a body hugging a desk-adjacent cell put its arm inside
the desk top. Two candidate-only navigation rules fix it:

- furniture-only clearance padding `+0.18` (total `0.40`, below the `0.397` half-cell so
  desk-adjacent cells stay walkable and the own seat desk stays exempt), and
- a desk-proximity step penalty (`+2.5`) in path search for padded actors, so routes keep one cell
  away from desks whenever the layout leaves room.

| | Player | Father |
| --- | --- | --- |
| cells visited | `(3,8) (3,7) (2,7) (1,7) (1,6) (1,5) (1,4) (1,3) (2,3) (3,3) (3,2)` | `(7,8) (7,9) (7,10) (8,10) (9,10) (10,10) (11,10) (11,9) (11,8)` |
| closest agent centre to a desk footprint | `1.39` cells | `0.50` cells (the goal cell is desk-adjacent) |
| frames with any skinned vertex inside desk geometry | `0 / 138` | `0 / 138` |
| closest visible vertex to desk geometry | `0.547` world | `0.199` world |

Vertex penetration is tested in each desk part's own local box (the grid is skewed against the
world axes, so world AABBs over-report). Files: `player-father-desk-detour.gif`,
`player-father-desk-detour-sheet.png`, `player-father-desk-detour-trace.csv` (per-frame position,
penetrating vertices, clearance, reach), `office-furniture-footprints.csv`,
`office-desk-part-bounds.csv`. Desk footprints are `StaticHard` occupancy obstacles with sub-cell
masks; chairs are `Interaction` cells open only to their seat owner. Peer avoidance: a move is
rejected when the target point is closer to another actor than the two radii (`0.475/0.578`
candidate, `0.28/0.30` production); blocked agents stop, yield to a side cell after `0.8 s`, replan
after `1.1 s` and drop path reservations after `2 s`.

## Review files

- `player-father-independent-zoom-walk.gif`: tracked 86-frame Player/Father comparison.
- `player-father-independent-full-map.gif`: all 86 approach frames at map scale.
- `father-whole-body-turn-close.gif` and `father-whole-body-turn-full-map.gif`: 48-frame Father turn.
- `father-player-same-tile-ratio-sheet.png`: same-tile comparison, now listing lowest sole height,
  foot-midpoint tile error and planted line touches ahead of the informational pixel centroid.
- `map-all-*`, `player-close-all-*`, `father-close-all-*`, `shoes-all-*`: all 86 frames.
- `turn-close-all-*`, `turn-map-all-*`: all 48 Father turn frames.
- `player-father-rendered-shoe-pixel-tile-trace.csv`: per-frame shoe/body measurements.
- `player-father-3d-interaction-result.txt`: final runtime receipt.

How to judge the GIF: draw the tile diamond around each actor's agent centre; both actors' soles
must sit inside it at the same height relative to the centre dot. Do not judge from the shoe pixel
centroid or from a lane median of it.

The final run used standalone `-batchmode -force-d3d11`, `CreateNoWindow=true`, hidden process style
and continuous `MainWindowHandle==0` monitoring. Production/default, Downloads, deployed executables
and the user-edited Father albedo `.meta` were not changed.
