# Player/Father legacy-2D screen-size candidate V9

Status: `productionEligible=false`. This is a review candidate, not the default production
presentation. Player V8/Father V19 mesh, Avatar, skin and same-package action 613 clips are
unchanged; this candidate changes only screen scale, Father horizontal proportion, candidate-local
Father material lighting and matching dynamic peer-collision radii.

The proof ran on Unity `6000.3.21f1`, Windows D3D11, 1280x720 capture targets. On the company PC it
used standalone `-batchmode`, `CreateNoWindow=true`, hidden process style and a continuous
`MainWindowHandle == 0` check. No ordinary Player, Unity editor window or Blender window was opened.

## Result

- 88 ordered approach frames at a deterministic 24 fps capture clock;
- 15 evenly spaced actor-only silhouette/colour samples over the walk, rather than one contact
  pose;
- Player/Father height min/median/max: `86/90/97px` and `91/94/97px`;
- median head width `27/28px`, torso width `24/23px`, silhouette area `1751/1772px`;
- median luma `91.36/69.32`, saturation `0.364/0.210`;
- approach travel `3.59827/3.60416`, pixel overlap `0`, physics penetration `0`;
- real purchased V31 route ended `Working/Working` at `seat_player/seat_father`;
- seated knees Player `83.01/86.79 degrees`, Father `96.24/100.85 degrees`;
- working static/interaction/agent violations `0/0/0`, retired visible renderers `0`.

The full machine-readable values are in `player-father-3d-interaction-result.txt`.

## Review files and SHA-256

| File | SHA-256 |
| --- | --- |
| `player-father-full-walk-map.gif` | `652FB14EACA9FB2BFB8C0728EB2FD706E4948101A41ACFD3EBD48FE27D696237` |
| `player-father-full-walk-contact-sheet.png` | `B5F0DF8DA01225FCBF49A839E357B2D07FCBD7FD02D3C5D3123A4ED98393539C` |
| `player-father-avoidance.png` | `D56BA02EDB0C54237739BED40714D5559BF75FBE0C7A10BD6F650F0058099C38` |
| `player-father-working.png` | `AE029A359554AA45CA82B4E24F2B62D9FAAD3078CE32983415F97AF3FEB42BAD` |
| `player-father-3d-interaction-result.txt` | `03A446D3FBD885B66BB452C4E68D3B4BED859651C7EB4EB0A5EC7E9E8989778F` |
| `player-father-3d-interaction-final.txt` | `3C8486481A2274D20313078BB0601E809E3C5F25443E6E309E3A549308A5B488` |

Automatic PASS is supporting evidence only. The candidate must remain fail-closed and cannot become
the production/default scale profile until the user explicitly approves the GIF.
