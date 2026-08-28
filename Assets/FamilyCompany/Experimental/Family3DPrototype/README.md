# Family 3D Experimental Prototype

This folder contains isolated 3D character candidates and actual Starter Office QA. It is not a production asset source.

## Current Father input

Father V19 is one indivisible Higgsfield/Meshy package: generated mesh, bind skeleton, skin weights and action 613. Unity uses the Avatar and clip from the same FBX at `poseStrength=1` without retarget sanitation, procedural gait or rigid-arm override.

- candidate: `Candidates/FatherV19MeshyOnePackage613/`
- FBX: `father-v19-meshy-one-package-613.fbx`
- albedo: `father-v19-meshy-one-package-albedo.png`
- surface: `Materials/FatherV19MeshyOnePackageSurface.mat`
- walk: `FatherV19_Casual_Walk_inplace`, frames `1..43`, `1.4 s`
- map stride: `0.7950477`
- measured forward offset: `0 degrees`
- walk/colour status: user approved for the next isolated phase

Do not use any retired Father rig, mixed donor clip or procedural gait as a base for this candidate.

## Current V27 workstation proof

- build: `Artifacts/Family3DStarterOfficeCandidateQaV1/FatherV19MeshyOnePackage613MapBuildV22NeutralChairNoLegacyOverlay`
- runtime: `Artifacts/Family3DStarterOfficeCandidateQaV1/FatherV19MeshyOnePackage613MapRuntimeV27NeutralChairNoLegacyOverlay`
- result: `FATHER_V19_FULL_3D_DESK_WORK_PROOF_COMPLETE`
- evidence: 1,051 samples, 361 work observations, 132 captures at 7.5 fps
- status: `USER_VISUAL_REVIEW_REQUIRED`, `productionMutation=false`, `productionEligible=false`

V27 uses the real production Father route, `seat_father`, `desk_father`, `chair_father`, the actual `(2,8)` / `2x1` semantic footprint and blocked cells. It adds QA-layer-only mapped-grid 3D furniture. The late occupied-chair foreground renderer is masked every frame, and the 3D chair uses neutral graphite/charcoal materials. Production furniture data and transforms are untouched.

Review files in the V27 runtime directory:

- `father-v19-neutral-chair-no-legacy-overlay-closeup-final.gif`
- `father-v19-neutral-chair-no-legacy-overlay-actual-map-final.gif`

The same final GIFs are tracked for home pull in `Docs/Evidence/Family3DFatherV19V27/`.

The full receipt and hashes are in `Docs/FATHER_V19_FULL_3D_DESK_WORK_QA_2026-08-28.md`.

## Code ownership

- `Editor/Family3DStarterOfficeCandidateQaBuilder.cs`: isolated copied scene, build and before/after production hash guards.
- `Runtime/Family3DStarterOfficeCandidateQa.cs`: actual runtime agent binding, route/phase capture, renderer masking and receipt.
- `Runtime/Family3DWalkActor.cs`: same-package walk playback, seated neutral pose and endpoint IK.
- `Runtime/Family3DWorkstationQa.cs`: semantic footprint mapping, 3D desk/CRT/keyboard/chair and physical gates.

## Next character

Do not duplicate Father version history. Follow `Docs/FAMILY_3D_WORKSTATION_CHARACTER_REUSE_CONTRACT_2026-08-28.md` from four-view provider generation through full actual-map GIF review. The next character gets a new candidate folder, new asset hashes, measured stride/forward and explicit runtime IDs while reusing the shared formulas and gates.

## Safety

- Keep every candidate on the QA layer and under Experimental/Artifacts.
- Do not touch production/default/Downloads/deployed executables.
- Do not stage unrelated untracked candidate files.
- Run Unity and Blender hidden/background on a company PC.
- Do not mark a result production eligible before the user approves the complete actual animation.
