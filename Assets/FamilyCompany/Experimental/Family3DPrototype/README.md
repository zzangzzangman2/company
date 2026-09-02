# Family 3D Experimental Prototype

This folder contains isolated 3D character QA and historical proof scenes. It is not a production
asset source.

Player V8 and Father V19 are no longer sourced from this folder. Their approved production packages
are under `Assets/FamilyCompany/Content/Resources/Production3D/`, and the production runtime is
`Assets/FamilyCompany/Runtime/Character3D/Family3DProductionPresenter.cs`. Candidate files were
moved with their Unity GUIDs; the descriptions below are historical proof instructions only.

Completion boundary: Father V19/V31 and Player V6/V8 are the only user-approved completed family
characters. Older Sister V2 now has a walking-only candidate but remains unapproved/not complete;
Mother has no current candidate. See `Docs/FAMILY_3D_CHARACTER_STANDARD.md` before continuing either.

## Current Older Sister V2 candidate

The V2 package is one indivisible Higgsfield/Meshy result under
`Candidates/OlderSisterV2MeshyOnePackage613/`: one mesh, Humanoid Avatar, skin, UV/albedo and the
same-FBX `OlderSisterV2_Casual_Walk_inplace` action 613 clip (`1..43`, `1.4 s`). The rejected V1
turnaround and legacy 2D were not donors or size inputs.

- isolated scene: `Scenes/Family3DOlderSisterV2MeshyOnePackage613MapQa.unity`
- build: `Artifacts/Family3DStarterOfficeCandidateQaV1/OlderSisterV2MeshyOnePackage613MapBuildV1`
- run: `Artifacts/OlderSister3DGeneration/20260902-102656/actual-map-run-v2`
- measured standing height/foot offset/ground correction:
  `2.367 / (0.041302,0.151164) / -0.062942`
- stride/phase/cycle: `1.98761598 / 0.40 / 1.4 s`
- 1,344-frame foot-midpoint tile error: median/max `3.549/7.829px`; occupancy `0/0/0`
- individual foot bones outside centred diamond `0/2688`; planted outside `0/1120`; minimum planted
  line margin `4.54px`
- evidence: `Docs/Evidence/OlderSisterV2CandidateCurrent/`
- status: `CANDIDATE_USER_APPROVAL_REQUIRED`, `productionEligible=false`; walking only, no seating
  or production/default cutover before full-GIF approval

## Current Player V6 walk + seated-work input

The no-hat Player V6 candidate is one indivisible Higgsfield/Meshy package: mesh, bind skeleton,
skin weights, albedo and action 613. Unity uses its own Avatar and clip at `poseStrength=1` without
Father/mixed retargeting, procedural gait, pose damping or limb correction.

- production package: `../../Content/Resources/Production3D/PlayerV8/`
- FBX: `player-v8-production.fbx`
- albedo: `player-v8-albedo.png`
- surface: `PlayerV8ProductionSurface.mat` with Player-only
  `PlayerV8BalancedAlbedo.shader` (`0.70` neutral fill + `0.18` soft form, exact albedo,
  no emission/specular/reflection)
- walk: `PlayerV6_Casual_Walk_inplace`, frames `1..43`, `1.4 s`
- map stride: `0.7950477`; measured forward offset: `0 degrees`
- QA scene: `Scenes/Family3DPlayerV6MeshyOnePackage613MapQa.unity`
- current walk/seated/appearance build/runtime:
  `Artifacts/Family3DStarterOfficeCandidateQaV1/PlayerV6MeshyOnePackage613MapBuildV8PlayerOnlyBalancedColor`
  and `.../PlayerV6MeshyOnePackage613MapRuntimeV8PlayerOnlyBalancedColor`
- seated flag: `-family3d-player-v6-desk-work-qa`; real `seat_player`, Player binding and unchanged
  action-613 locomotion followed by the shared neutral seated pose and endpoint IK
- current review: `Docs/Evidence/Family3DPlayerV6/player-v6-v8-balanced-color-close.gif`,
  `player-v6-v8-balanced-color-full.gif` and `player-v6-v6-v8-color-hair-comparison.png`; 136
  ordered frames, stable brown hair and restored clothing colours, knees `106.3443° / 110.4238°`,
  chair penetration `0`, route occupancy `0/0/0`
- status: historical isolated receipt unchanged; current Player runtime is
  `USER_VISUAL_APPROVED_PRODUCTION`

Exact generation inputs and hashes are in `Docs/ASSET_MANIFEST.md`; walk, seated and appearance
proof details are consolidated in `Docs/FAMILY_3D_CHARACTER_STANDARD.md`.

## Current Father input

Father V19 is one indivisible Higgsfield/Meshy package: generated mesh, bind skeleton, skin weights and action 613. Unity uses the Avatar and clip from the same FBX at `poseStrength=1` without retarget sanitation, procedural gait or rigid-arm override.

- production package: `../../Content/Resources/Production3D/FatherV19/`
- FBX: `father-v19-production.fbx`
- albedo: `father-v19-albedo.png`
- surface: `FatherV19ProductionSurface.mat`
- walk: `FatherV19_Casual_Walk_inplace`, frames `1..43`, `1.4 s`
- map stride: `0.7950477`
- measured forward offset: `0 degrees`
- walk/colour/runtime status: `USER_VISUAL_APPROVED_PRODUCTION`

Do not use any retired Father rig, mixed donor clip or procedural gait as a base for this candidate.

## Current V31 original-chair atomic workstation proof

- build: `Artifacts/Family3DStarterOfficeCandidateQaV1/FatherV19MeshyOnePackage613MapBuildV26AtomicOriginalChair`
- runtime: `Artifacts/Family3DStarterOfficeCandidateQaV1/FatherV19MeshyOnePackage613MapRuntimeV31AtomicOriginalChair-CompanyPullFull`
- result: `FATHER_V19_FULL_3D_ALL_WORKSTATIONS_PROOF_COMPLETE`
- evidence: 1,051 samples, 361 work observations, 132 captures at 7.5 fps
- status: `USER_VISUAL_APPROVED_ISOLATED`, `productionMutation=false`, `productionEligible=false`

V31 preserves the user-selected V29 desk, CRT, keyboard, chair and seated composition exactly. All
132 V31 Player frames are byte-for-byte identical to the corresponding V29 PNGs. The only visual
structure change is one `V31_AtomicWorkstationSet_OriginalChair_<seat>` root per seat, owning the
desk, CRT, keyboard and original chair together.

Production collision/pathfinding is unchanged: desks are hard obstacles, unowned chairs are
interaction obstacles, and selecting/moving/rotating either bound desk or chair promotes to the
complete workstation binding. The actual entrance-to-seat Player run recorded zero static,
interaction and agent-penetration violations. The V30 chair relocation and CRT swivel were rejected
and are not part of V31. The V29 drawer-face correction remains active.

Review files in `Docs/Evidence/Family3DFatherV19V31/`:

- `father-v19-v31-v29-visual-equality.png` (left V29, right V31; identical pixels)
- `father-v19-v31-original-chair-atomic-set-close.gif`
- `father-v19-v31-original-chair-atomic-set-full.gif`

The full V31 receipt and media hashes are tracked under `Docs/Evidence/Family3DFatherV19V31/`; the current character standard is `Docs/FAMILY_3D_CHARACTER_STANDARD.md`.

## Code ownership

- `Editor/Family3DStarterOfficeCandidateQaBuilder.cs`: isolated copied scene, build and before/after production hash guards.
- `Runtime/Family3DStarterOfficeCandidateQa.cs`: actual runtime agent binding, route/phase capture, renderer masking and receipt.
- `../../Runtime/Character3D/Family3DWalkActor.cs`: production same-package walk playback, seated neutral pose and endpoint IK.
- `../../Runtime/Character3D/Family3DWorkstation.cs`: production semantic footprint mapping, 3D desk/CRT/keyboard/chair and physical gates.

## Next character

Do not duplicate Father version history. Follow `Docs/FAMILY_3D_CHARACTER_STANDARD.md` from four-view provider generation through full actual-map GIF review. The next character gets a new candidate folder, new asset hashes, measured stride/forward and explicit runtime IDs while reusing the shared formulas and gates.

For tile-centred review, do not stop at the semantic root, the two-foot midpoint or an expanded
ankle/toe proxy. The current command-line-only legacy-size candidate renders a shoe-only skinned mesh
from the actual foot/toe-weighted triangles and tests every shoe pixel in every moving frame. The
ankle-only `8.135/7.096px` and later envelope `3.562/3.562px` results were both false PASSes because
Father's visible forefoot still covered a line. Do not move the character host on individual contact
frames to manufacture containment: that is a frame-dependent teleport and hides slip. The current
candidate retains action 613, couples its two alternating steps to two exact tile-centre distances
with candidate stride `1.98761598` and phase `0.40`, and uses no contact/release host correction.
Father keeps the measured foot-at-root standing offset `(0.037517,0.138023)`. The 2026-09-02
`(0.037517,0.5)` / `(-0.24,0.5)` candidates matched a 2D shoe-pixel centroid that mixes shoe height
with floor position and put the Father's planted feet on the tile corner (`57/61` planted frames on a
line); do not tune floor offsets against pixel centroids. The real `12-15px` same-tile difference was
vertical: the walk clips float the lowest skinned vertex `0.138` (Player) versus `0.429` (Father)
above the ground plane, so candidate-only `AlignCandidateStandingGround` lowers the Father's
standing/walking visual ground by the measured difference (`-0.2910`). Planted line touches are
`2/8` of `61/61`, foot-midpoint tile error `1.464/4.306px`, lowest vertex `0.1473/0.1502`. The strict
vertical shoe silhouette still fails, so it remains `productionEligible=false` pending a new
user-approved GIF. Production and
default locomotion remain stride `0.7950477`, phase `0`.

## Safety

- Keep every candidate on the QA layer and under Experimental/Artifacts.
- Do not touch production/default/Downloads/deployed executables.
- Keep only current one-package candidates and their current QA scenes/evidence in Git.
- Run Unity and Blender hidden/background on a company PC.
- Do not mark a result production eligible before the user approves the complete actual animation.
