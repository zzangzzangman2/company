# Player/Father tile-centred screen-size candidate

Status: `productionEligible=false`. This remains a command-line review candidate and does not
replace the approved production/default profile until the user explicitly accepts the GIF.
Player V8 and Father V19 keep their own same-package mesh, Avatar, skin and action-613 clip at full
pose strength.

## What changed

The semantic `OfficeRuntimeAgent` path already crossed the exact tile centres, but enlarged-frame
measurement showed that the two animated foot bones had a stable midpoint bias relative to that
path. Candidate locomotion now offsets only the visible production host by the measured amount:

- Player local X/Z correction: `+0.050989 / +0.214083`;
- Father local X/Z correction: `+0.037517 / +0.138023`.

The correction is applied after the whole-body travel yaw, so it follows every direction. It is
not applied during seat alignment, sitting, working, finishing or standing up. Moving the imported
model/Animator root was explicitly rejected because it changed the approved seated knee solution.
The normal production/default profile continues to pass zero correction.

Candidate-only dynamic peer radii are `0.380465984 / 0.447570225`; the proven furniture and docking
radius remains `0.22`. This prevents the newly centred visible silhouettes from overlapping while
leaving the semantic path, grid, seat and workstation geometry unchanged.

## Final hidden D3D11 result

- Unity `6000.3.21f1`, D3D11, 1280x720, deterministic 24 fps;
- all `89` approach frames captured and enlarged into six chronological sheets;
- start tile-centre error: `0.000000 / 0.000000` world units;
- maximum straight centre-line deviation: `0.000002 / 0.000212` world units;
- foot-midpoint tile-centre error median/max: Player `2.118 / 5.921px`, Father
  `1.286 / 4.170px`;
- signed foot-midpoint local bias median: `0/0` for both characters;
- visual overlap pixels `0`, agent penetration `0`;
- real purchased V31 routes ended `Working/Working` at `seat_player/seat_father`;
- seated semantic tile-centre error: `0.000000 / 0.000000`;
- seated knees: Player `83.01 / 86.79 degrees`, Father `96.24 / 100.85 degrees`;
- working static/interaction/agent violations `0/0/0`, retired visible renderers `0`.

Every ordered frame was visually reviewed, not just the automatic PASS. Both characters retain
exactly two legs/shoes and two arms/hands, alternating contacts and small opposite arm swing; the
body remains upright and travel-facing through the collision stop, with no third leg, garment tear,
rubber limb, foot slide across the path, silhouette overlap or pose discontinuity before the
collision stop. The review GIF is a one-shot approach, so its viewer loop intentionally returns
from the stopped collision frame to the starting positions.

Company-PC execution used standalone `-batchmode`, `CreateNoWindow=true`, hidden process style and
continuous `MainWindowHandle == 0` monitoring. No Unity editor, ordinary Player or Blender window
was opened.

## Review media

- `player-father-tile-center-map-walk.gif`: full map, all 89 frames;
- `player-father-tile-center-walk-zoom.gif`: enlarged moving corridor, all 89 frames;
- `zoom-all-frames-000-014.png` through `zoom-all-frames-075-088.png`: all frames in chronological
  order;
- `player-father-avoidance.png`: final non-overlapping collision stop;
- `player-father-working.png`: real V31 simultaneous work result;
- `player-father-3d-interaction-result.txt`: complete machine-readable measurements.

## SHA-256

| File | SHA-256 |
| --- | --- |
| `player-father-tile-center-map-walk.gif` | `E1A6D03A7C0AA98BCD7A072D6FD90C9177685292AB9F47D26C49895A6F3B1879` |
| `player-father-tile-center-walk-zoom.gif` | `8841C4D0770A988D48DCE92B9E18B9427EA7197DE9CC8B6BC433262F5FA17F73` |
| `player-father-avoidance.png` | `FF41C9A7DEED6BD5F12B0259627750B5F2030930EFA2F498F69EDF8EAFDCF219` |
| `player-father-working.png` | `19AB2BA0B3C69539DB545EC95A9A46C088DDA730194D71CBFDD616281D2DFF3F` |
| `player-father-3d-interaction-result.txt` | `BEE81A82DF2D58CB63172940E528037D2F23E1B3AFE9ED51DB3B8AE2B460BA24` |
| `player-father-3d-interaction-final.txt` | `D126FD8872E88D72674DE719869A47EEA54EE298E0BF168B6C97997C4C6537E8` |

Automatic PASS is supporting evidence only. User review of the actual GIF remains the promotion
authority.
