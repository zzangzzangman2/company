# Player V8 + Father V19 production interaction evidence

Unity `6000.3.21f1` Windows Release Player, Direct3D11, 2026-08-31.

The real empty-office runtime spawned Player V8 and Father V19, drove both toward each other, and
stopped them through authoritative dynamic occupancy. Both then routed through three normally
purchased V31 workstation sets and independently entered `Working` at `seat_player` and
`seat_father`.

- approved map height: Player `1.857258558`, Father `1.885507822` office units; Father is only
  `1.52%` taller. Father model scale is locked to the V31-approved `1.012728333` and must never be
  recalculated from a retired sprite.
- mutual travel: Player `1.68003`, Father `1.69520` office units
- dynamic contact: `blockedAgentMoves=47`, collision projection observed for both actors
- overlap: agent penetrations `0`, rendered silhouette overlap pixels `0`
- work: `Working/Working`, static/interaction/agent violations `0/0/0`
- knees: Player `106.13/110.22 degrees`, Father `112.92/117.27 degrees`
- visible retired Player/Father/workstation renderers: `0`
- navigation/static radius: `0.22`; production dynamic silhouette radii: Player `0.28`, Father
  `0.33`. Static furniture paths and chair docking retain the proven radius; peer-to-peer avoidance
  uses each visible 3D body's larger silhouette radius.

Files:

- `player-father-avoidance.png` — both actors stopped face-to-face without a shared opaque pixel.
  SHA-256 `447A0AACFBE7FDAE581BCF2F53D1D97E52888F31A7F96F988E379151A5473709`.
- `player-father-working.png` — both actors simultaneously Working at their own V31 sets.
  SHA-256 `FC7DB1DA9B9A5B7AD2CEB2D1B2073D8680DF1A9A67028FC4D97B5FB8E93970A6`.
- `player-father-3d-interaction-result.txt` — exact player receipt. SHA-256
  `275E0EACEBA87272F54B7CE87D7E1684E7FC33C1BEAE6F3A37A388E54FB3809A`.

The companion four-direction V31 regression also passed: four rigid quarter-turn sets, maximum
tile-corner error `0.0003px`, Player Working knees `107.45/113.16 degrees`, and retired renderers
`0`.
