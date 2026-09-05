# Older Sister V3 zero-credit SD repair candidate

Status: **`CANDIDATE_USER_APPROVAL_REQUIRED`**, `productionEligible=false`.

V3 fixes the rejected V2 proportions, palette separation and map-scale eyes without another
Higgsfield/Meshy purchase. It uses the already-paid V2 GLB as immutable source, applies a local
continuous bind-space SD transform to its copy, and keeps that package's skin weights, UV topology
and action 613. New provider credit charge: **0**. Production/default resources, the normal game
executable, collision radii and seating were not changed.

## Result

- Canon: Korean adult age 20; long near-black twin tails, large black bows, teal eyes, charcoal
  sleeveless tank, navy dolphin shorts with white piping, barefoot, curvy-athletic SD adult.
- S6/S7 ratios: head/height `0.310`, hip/height `0.090`, shoulder/height `0.036`, leg/height `0.460`.
- Package: one skinned mesh/armature/material/UV; 211,673 vertices, 118,945 polygons, 24 bones;
  unweighted/invalid `0/0`, maximum four influences.
- FBX SHA-256: `6639CB85D79B6385E089D9A3301AB1D2A9D1B20C9D8749E8144C629501846D2E`.
- Albedo SHA-256: `7264BEA780D2B821A4128BBB9B47B83E3CD41DA2A4A7E20B98743E38ECB71473`.
- Same-package clip: `OlderSisterV3_Casual_Walk_inplace`, `1..43`, `1.4 s`, pose strength 1.
  There is no donor, retarget, procedural gait, damping, rigid-arm rewrite or per-contact host move.

## Actual Starter Office proof

Unity `6000.3.21f1` built a copied Experimental scene with `productionMutation=false`. The hidden
Windows D3D11 player moved the real `older_sister` `OfficeRuntimeAgent` around the same clear 3x3
perimeter for two complete circuits. The run contains 337 real 1280x720 frames and 1,344 telemetry
samples across all four diagonal directions; static/interaction/agent occupancy violations are
`0/0/0`.

- height: locked `2.367 = 93.02px`; temporal-background visible silhouette median `86px`, S1 pass
- face/eyes: projected face `28.84px`; both teal eyes at least `3px` high in four frontal frames,
  S8 screen gate pass
- colour: actual-map silhouette luma `91.49`, saturation `0.247`, white clipping `0%`, C3/C4 pass
- foot-centre offset: `(0.034554,0.112794)` from 24 action phases
- ground: one fixed correction `-0.073097`, lowest skinned point `0.210697 -> 0.137600`
- tile centre: median/max `2.715/5.856px`, passing the `4/8px` gate
- individual feet: outside diamond `0/2688`; planted outside `0/1120`; minimum planted margin
  `8.85px`
- stride/phase/cycle: `1.98761598 / 0.40 / 1.4 s`; horizontal reach `0.393736`

The S8 UV-area rule remains mandatory for future provider submissions. Because V3 is the explicit
no-new-charge repair and preserves the V2 UV topology, it can only receive the narrow local-repair
exception after the user judges the real map GIF; this is not a reusable precedent.

## Review files

- `older-sister-v3-actual-map-full.gif`: complete two-circuit overview, SHA-256
  `F9006A975A982E5ADF09850E38D2BBA2B9825FB84049D6447F90AF859435F12E`
- `older-sister-v3-actual-map-tile-center.gif`: tracked nearest-neighbour view with the cyan semantic
  tile and red agent centre, SHA-256
  `C29D52863242DD371E2B637720233BE3B0F12486050AE12538D9C856228F02DC`
- `older-sister-v3-actual-map-direction-contact.png`: eight route legs, SHA-256
  `1342353902E86E27A29834157FD4F4B8CB65BAB0E58B3A37733EEDD0EF6BCE3E`
- `older-sister-v3-walk-three-quarter.gif`: one enlarged source cycle, SHA-256
  `44EEBAD3CE31A1D236BEC125395B2EBE4C1631616832981F568FE5704D77369D`
- `older-sister-v3-walk-side.gif`: one enlarged side cycle, SHA-256
  `E20B3A3BB662BAA5FBC5746606046D76ED16960F71358E358FDDC71796CCAA5D`
- JSON receipts contain the geometry, albedo, FBX, Unity build/runtime, tile-centre and visual-gate
  measurements used above.

Next allowed step: user review of the full and tracked GIFs. Promotion, collision and seating remain
blocked until explicit visual approval.
