# Player V8 + Father V19 production interaction evidence

Unity `6000.3.21f1` Windows Release Player, Direct3D11, 2026-08-31.

The real empty-office runtime spawned Player V8 and Father V19, drove both toward each other, and
stopped them through authoritative dynamic occupancy. Both then routed through three normally
purchased V31 workstation sets and independently entered `Working` at `seat_player` and
`seat_father`.

- 1280x720 screen-standardized size: Player `34x74 / 1259px`, Father `31x72 / 1161px`; head widths
  are exactly `22/22px`, torso widths `30/29px`, and silhouette area differs by `7.78%`. Father
  scale/mesh height/horizontal proportion is locked to `0.950318127/1.769311871/0.92` and must
  never be recalculated from a retired sprite or raw mesh height alone.
- mutual travel: Player `1.69471`, Father `1.71053` office units
- dynamic contact: `blockedAgentMoves=45`, collision projection observed for both actors
- overlap: agent penetrations `0`, rendered silhouette overlap pixels `0`
- work: `Working/Working`, static/interaction/agent violations `0/0/0`
- knees: Player `106.13/110.22 degrees`, Father `133.92/140.13 degrees`
- visible retired Player/Father/workstation renderers: `0`
- navigation/static radius: `0.22`; production dynamic silhouette radii: Player `0.28`, Father
  `0.30`. Static furniture paths and chair docking retain the proven radius; peer-to-peer avoidance
  uses each visible 3D body's larger silhouette radius.

Files:

- `player-father-avoidance.png` — both actors stopped face-to-face without a shared opaque pixel.
  SHA-256 `D392FD02BB634CF533A09868C1D2A30D45CC2951F83CF8B7C017CAE59A147EF1`.
- `player-father-working.png` — both actors simultaneously Working at their own V31 sets.
  SHA-256 `4A74E7505558CA77021AD5B86CE8BC21E7204398F5D204B2BBF62F8FC8E0C11A`.
- `player-father-3d-interaction-result.txt` — exact player receipt. SHA-256
  `400BCF3163FD89B6B809B3AF490B9337A72A12F640E716AEB7CF29A2E20AE75F`.

The companion four-direction V31 regression also passed: four rigid quarter-turn sets, maximum
tile-corner error `0.0003px`, Player Working knees `107.45/113.16 degrees`, and retired renderers
`0`.
