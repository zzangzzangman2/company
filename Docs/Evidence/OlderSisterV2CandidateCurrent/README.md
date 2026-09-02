# Older Sister V2 one-package 3D candidate

Status: **`REJECTED_BY_USER_2026-09-02`** (size, brightness, sharpness, eyes). `productionEligible=false`.
Measured causes and the regeneration requirements are in `Docs/FAMILY_3D_CHARACTER_STANDARD.md`
§4.3 and §9.1: realistic 6-head proportions (skeleton head:height `0.159` vs family `0.27-0.34`),
one near-black texture block for hair/tank/shorts (`57.6%` of texels), and a face UV too small for
eyes to survive at `93px`. Locomotion metrics below still pass and the pipeline is reusable. The FBX,
albedo, reference PNGs and the 13 MB tile-centre GIF are kept locally only (not committed).

This is the first current Older Sister candidate after the rejected V1 turnaround. It is an
isolated walking-only QA package. Production resources, the normal game executable, the approved
Player/Father presentation and every workstation remain unchanged. Seating is intentionally not
implemented or tested until the user approves the full actual-map walk GIF.

## Canon and source

- Identity: Korean adult woman, age 20; long near-black twin tails, large black bows, teal eyes,
  dark sleeveless tank, navy dolphin shorts with white piping, barefoot, curvy-athletic adult
  proportions.
- Canon source:
  `Assets/Art/Characters/OlderSister/older_sister_casual_neutral_v2.png`, SHA-256
  `4335F2025D6FA3AC7145FBA93B4447CC934D85ABFD38452A2A8F3E977A7EA0B5`.
- Higgsfield 4K 16:9 turnaround job: `cb63c171-c910-4d46-817a-3e1820c268f9`, charged 4 credits.
  The clean front/three-quarter/left-side/back panels are under
  `Docs/ReferenceImages/OlderSisterV2Higgsfield/` and their hashes/media IDs are in
  `generation-receipt.json`.
- Higgsfield/Meshy model job: `495165b9-e47c-47e5-9836-8a8725ced20a`, charged 38 credits. It used
  one `multi_image_to_3d` call with rigging, animation, texture, PBR and remesh enabled; quad target
  60,000; symmetry on; A-pose at 1.65 m; action 613 `Casual_Walk_inplace`; safety checker on.
  Total approved charge: 42 credits (`72 -> 30`).
- Source GLB SHA-256:
  `62E1366BCB804E572565B2C3E57CDCE8810271647B61C729AE3AC61F9FA3D3DD`.

## Package validation

- Unity candidate FBX/albedo:
  `Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/OlderSisterV2MeshyOnePackage613/`.
  FBX SHA-256 `910F85E51B6B22524735E00867ABFC18CC4E02F31FC46357EB846EDF2F2B28DB`;
  albedo SHA-256 `C2A6F83FCF03AB6218EC130281353C727EB5B6478C6E1B02DDE111B8C6CAFDBA`.
- Converted package: one skinned mesh, one armature, one material, one UV set, 211,673 vertices,
  118,945 polygons, 24 Meshy bones, no unweighted vertices, no invalid bone references, maximum
  four influences. Only the known auxiliary `Icosphere` was removed.
- Same-FBX clip: `OlderSisterV2_Casual_Walk_inplace`, frames `1..43`, `1.4 s`, pose strength 1.
  No donor, retarget, anatomical sanitation, procedural gait, rigid-arm rewrite or damping.
- `validate_generated_biped_skin_glb.py` reports `head-leg-mixed:1666`, but all 1,666 vertices are
  actually the high-waisted hip/shorts seam at normalized height `0.6000..0.6263`, dominated by
  `Hips` (`>=0.83`) with only `0.10..0.219` upper-leg weights. The geometry overlap audit reports
  zero flagged vertices and zero clusters. This is recorded as a validator cutoff false positive,
  not hidden as a pass.
- Blender animation inspection found one 42-frame/1.4 s recurrence, foot-forward correlation
  `-0.8310`, hand-forward correlation `-0.9378`, and torso lean `1.07..6.17 degrees`. The side and
  three-quarter source GIFs show no extra/missing limbs, attached hands, torn clothing, cross-weight
  collapse, doll walk or hair detachment.

## Actual Starter Office D3D11 proof

- Unity `6000.3.21f1`; isolated copied QA scene and Windows D3D11 player; build succeeded.
- Route: the real `older_sister` `OfficeRuntimeAgent` follows a clear 3x3 perimeter for two
  continuous circuits. 337 visual frames and 1,344 telemetry frames cover 22.4 seconds and all four
  diagonal travel directions. Static/interaction/agent penetrations are `0/0/0`; blocked static and
  interaction moves are `0/0`.
- Height is not read from the rejected 2D Sister. Locked target `2.367` gives `93.02px` at 1280x720:
  taller than the 90px Player and no taller than the 93.5px Father, inside S1 `81..99px`.
- Material starts at the required `PlayerV8BalancedAlbedo`, white tint, ambient `0.70`, key `0.18`,
  emission/specular/reflection off. No scene-wide lighting change.
- Measured 24-phase foot-centre offset: `(0.041302, 0.151164)` local X/forward. This is applied after
  whole-body travel rotation; model/Animator root is not moved.
- Lowest skinned walk vertex before/after one constant standing correction:
  `0.200542 -> 0.137600`; correction `-0.062942`. The result exactly matches the approved Player
  reference `0.1376`. There is no planted-frame/contact-dependent host translation.
- Foot-midpoint tile error over all 1,344 telemetry frames: median `3.549px`, maximum `7.829px`;
  passes the standard `<=4/<=8px` gate. Each left/right foot bone was also tested separately:
  outside the moving agent-centred tile diamond `0/2,688`, planted outside `0/1,120`, minimum
  planted bone-to-line margin `4.54px`. Stride/phase/cycle are
  `1.98761598 / 0.40 / 1.4 s`; the two action-613 landings span two tile-centre distances.
- Measured walking body horizontal reach: `0.4423` world. This is recorded for later collision and
  furniture-padding QA; no production radius is changed before approval.

## Review files

- `older-sister-v2-actual-map-full.gif`: all 337 frames, complete two-circuit actual-map proof.
  SHA-256 `E564877E95017062246E35D58D2162853DE2E84429907CF3C0E3354A833970E2`.
- `older-sister-v2-actual-map-tile-center.gif`: tracked enlargement of the same full proof. Cyan is
  the tile diamond centred on the semantic agent; the red dot is the agent centre. SHA-256
  `4F882ED0030C41CBD0CDFC884C9C5051A8AB93ACAB2A2DD84781CB2973C10EE0`.
- `older-sister-v2-actual-map-direction-contact.png`: eight route legs with the same centre overlay.
  SHA-256 `6239F6856CE94D8D19902BEA766E1BA83459E13DCAC51F779B5187A6AA72596B`.
- `older-sister-v2-walk-three-quarter.gif` / `older-sister-v2-walk-side.gif`: source-package
  one-cycle inspection before Unity.
- `older-sister-v2-actual-map-metrics.json`, `older-sister-v2-runtime-receipt.json`,
  `older-sister-v2-build-receipt.json` and the four validation JSON files contain reproducible raw
  measurements.

The next allowed step is user visual approval of the complete actual-map GIF. Only after approval
may the candidate be promoted and receive collision, avoidance and four-direction seating QA.
