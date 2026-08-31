# Player V8 + Father V19 production interaction evidence

Unity `6000.3.21f1` Windows Release Player, Direct3D11, 2026-08-31.

The real empty-office runtime spawned Player V8 and Father V19, drove both toward each other, and
stopped them through authoritative dynamic occupancy. Both then routed through three normally
purchased V31 workstation sets and independently entered `Working` at `seat_player` and
`seat_father`.

- mutual travel: Player `1.62084`, Father `1.62439` office units
- dynamic contact: `blockedAgentMoves=50`, collision projection observed for both actors
- overlap: agent penetrations `0`, rendered silhouette overlap pixels `0`
- work: `Working/Working`, static/interaction/agent violations `0/0/0`
- knees: Player `106.13/110.22 degrees`, Father `75.61/78.89 degrees`
- visible retired Player/Father/workstation renderers: `0`
- navigation/static radius: `0.22`; production dynamic silhouette radii: Player `0.28`, Father
  `0.46`. Static furniture paths and chair docking retain the proven radius; peer-to-peer avoidance
  uses each visible 3D body's larger silhouette radius.

Files:

- `player-father-avoidance.png` — both actors stopped face-to-face without a shared opaque pixel.
  SHA-256 `9D1D4E16F57E31C6C9DABB601463504CFCD81CBD83229EBA09D7849B85E32BAC`.
- `player-father-working.png` — both actors simultaneously Working at their own V31 sets.
  SHA-256 `1DCEE92652BD6503819165EB906B23911443FC3727413AF90316D10F9A375747`.
- `player-father-3d-interaction-result.txt` — exact player receipt. SHA-256
  `DC401D0F3F26215B11B489949EA6DAD3736499BB26BE7E7E68710C78F4980D3E`.

The companion four-direction V31 regression also passed: four rigid quarter-turn sets, maximum
tile-corner error `0.0003px`, Player Working knees `107.45/113.16 degrees`, and retired renderers
`0`.
