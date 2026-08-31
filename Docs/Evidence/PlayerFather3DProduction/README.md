# Player V8 + Father V19 production interaction evidence

Unity `6000.3.21f1` Windows Release Player, Direct3D11, 2026-08-31.

The real empty-office runtime spawned Player V8 and Father V19, drove both toward each other, and
stopped them through authoritative dynamic occupancy. Both then routed through three normally
purchased V31 workstation sets and independently entered `Working` at `seat_player` and
`seat_father`.

- screen-standardized size: Player `17x37 / 313px`, Father `16x36 / 322px`; width and height differ
  by only one pixel and silhouette area by `2.88%`. Father scale/mesh height is locked to
  `0.950318127/1.769311871` and must never be recalculated from a retired sprite or raw mesh height.
- mutual travel: Player `1.68766`, Father `1.69756` office units
- dynamic contact: `blockedAgentMoves=52`, collision projection observed for both actors
- overlap: agent penetrations `0`, rendered silhouette overlap pixels `0`
- work: `Working/Working`, static/interaction/agent violations `0/0/0`
- knees: Player `106.13/110.22 degrees`, Father `125.18/130.46 degrees`
- visible retired Player/Father/workstation renderers: `0`
- navigation/static radius: `0.22`; production dynamic silhouette radii: Player `0.28`, Father
  `0.32`. Static furniture paths and chair docking retain the proven radius; peer-to-peer avoidance
  uses each visible 3D body's larger silhouette radius.

Files:

- `player-father-avoidance.png` — both actors stopped face-to-face without a shared opaque pixel.
  SHA-256 `B085B45757B2FE244D4E43818F4883ED9D0A5637093357FE214613FEED45EE82`.
- `player-father-working.png` — both actors simultaneously Working at their own V31 sets.
  SHA-256 `9ECED4FCC5B0D9774E3A9D9D6193D669C14C42A17D4179C829616AE8C90E4735`.
- `player-father-3d-interaction-result.txt` — exact player receipt. SHA-256
  `6B8EC5813301AA67202235B05FB80092F131B9ADDDFDA78341368E9F3D3557E3`.

The companion four-direction V31 regression also passed: four rigid quarter-turn sets, maximum
tile-corner error `0.0003px`, Player Working knees `107.45/113.16 degrees`, and retired renderers
`0`.
