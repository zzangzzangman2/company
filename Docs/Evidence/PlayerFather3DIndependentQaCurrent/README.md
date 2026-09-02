# Player/Father 3D independent QA evidence

Status: `CANDIDATE_USER_APPROVAL_REQUIRED`  
Production eligibility: `false`  
Final hidden D3D11 source run:
`Artifacts/FatherStandingGroundAlignedFinal-20260902-141500/`

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
