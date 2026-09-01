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

That midpoint correction did not prove each foot was safe. Re-reading all 89 frames with the
walk actor's actual left/right action-613 contact flags found that the former coupling put planted
ankles almost exactly on half-cell lines: Player/Father minimum `0.527 / 0.024px`, with `16 / 17`
moving contact frames below 6px. A ten-value phase-only sweep also failed (best minimum only
`2.13px`), proving that this was the incommensurate `0.7950477` cycle stride versus tile period,
not a one-frame start-pose issue.

Candidate mode now couples one unmodified action-613 cycle to the exact isometric tile-centre
distance `0.99380799` and uses measured phase offset `0.64 cycles`. This changes no mesh, Avatar,
skin, pose strength, direction, limb curve or seating. Production/default stays at `0.7950477 / 0`.
QA grounds and projects each ankle separately every moving frame and fails if either actor has
fewer than 24 planted samples, minimum line clearance below 6px, or any under-6px contact frame.

Candidate-only dynamic peer radii are `0.380465984 / 0.447570225`; the proven furniture and docking
radius remains `0.22`. This prevents the newly centred visible silhouettes from overlapping while
leaving the semantic path, grid, seat and workstation geometry unchanged.

## Final hidden D3D11 result

- Unity `6000.3.21f1`, D3D11, 1280x720, deterministic 24 fps;
- all `89` approach frames captured and enlarged into six chronological sheets;
- start tile-centre error: `0.000000 / 0.000000` world units;
- maximum straight centre-line deviation: `0.000002 / 0.000212` world units;
- foot-midpoint tile-centre error median/max: Player `1.833 / 3.866px`, Father
  `1.141 / 2.754px`;
- actual planted-contact samples: Player/Father `65 / 66`;
- X-axis minimum planted-foot line clearance: `8.135 / 7.096px`, under-6px frames `0 / 0`;
- separate Y-axis minimum clearance: `8.767 / 6.453px`, under-6px frames `0 / 0`;
- the two actors walk opposite ways in each run, so these cover `+X/-X/+Y/-Y`;
- visual overlap pixels `0`, agent penetration `0`;
- real purchased V31 routes ended `Working/Working` at `seat_player/seat_father`;
- seated semantic tile-centre error: `0.000000 / 0.000000`;
- seated knees: Player `83.78 / 95.48 degrees`, Father `94.60 / 107.66 degrees`;
- working static/interaction/agent violations `0/0/0`, retired visible renderers `0`.

Every ordered frame was visually reviewed, not just the automatic PASS. Both characters retain
exactly two legs/shoes and two arms/hands, alternating contacts and small opposite arm swing; the
body remains upright and travel-facing through the collision stop, with no third leg, garment tear,
rubber limb, planted foot on a tile line, silhouette overlap or pose discontinuity before the
collision stop. At the locked map speed the clip now reads at about two natural steps per second
rather than the former hurried 2.5-step cadence. The review GIF is a one-shot approach, so its
viewer loop intentionally
returns
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
- `player-father-3d-interaction-result.txt`: complete machine-readable measurements;
- `player-father-foot-tile-trace.csv`: all X-axis per-frame foot/grid/contact measurements;
- `player-father-foot-tile-trace-y-axis.csv` and
  `player-father-foot-tile-sweep-y-axis-result.txt`: opposite-axis proof.

## SHA-256

| File | SHA-256 |
| --- | --- |
| `player-father-tile-center-map-walk.gif` | `CE01C68A54F35C04045A6174508A37B37DCD0E293C0B24D3E3DB341B98C698C3` |
| `player-father-tile-center-walk-zoom.gif` | `37F4A751DDFA443D332FC83F3FC5705BD4C4A9E653AA4B64A1B3EF4B3247D0D5` |
| `player-father-avoidance.png` | `698D3C4CB034A8C7FD83D812672913CC79A44B2007A14C482BFB1A46248A94AC` |
| `player-father-working.png` | `FFC4BEBB7AB2D3EBA2D3ECA41F829C9F759CCD965A55C970CE3177FBD12273F0` |
| `player-father-3d-interaction-result.txt` | `437E16B64FD801D1A980F70D6E1B9D863799EFA54817E2113B2ACD6DF1379B0E` |
| `player-father-3d-interaction-final.txt` | `D126FD8872E88D72674DE719869A47EEA54EE298E0BF168B6C97997C4C6537E8` |
| `player-father-foot-tile-trace.csv` | `8386033C6A72612A5C82298EE4A6AB1190B8E5C0AC53639D28BABCD85F199CC7` |
| `player-father-foot-tile-trace-y-axis.csv` | `9232BE90C97C4F8C007801F88C84A1AE2B0862B16D8ED267FEF030CCB4B73760` |
| `player-father-foot-tile-sweep-y-axis-result.txt` | `FDE2CFBC40D9A7AD3FFB9A785BE32675A1444FD0EBE53D5A99747171435D7F8F` |

Automatic PASS is supporting evidence only. User review of the actual GIF remains the promotion
authority.
