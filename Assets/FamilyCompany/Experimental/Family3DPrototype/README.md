# Family 3D Identity Candidate Lab V1

This folder is a strictly isolated 3D experiment created after the project moved family-character
work to 3D. All new family-character work must follow
`Docs/FAMILY_3D_CHARACTER_CANON_2026-08-24.md`. Existing 2D family sprites, atlases, limb donors,
R-series candidates, and runtime frame slices are forbidden inputs for this 3D pipeline.

The folder does not alter the production family sprite catalog, production scene, default
executable, or Downloads build. The old 2D files remain only for history/migration safety until a
separate, user-approved production migration.

## Locked motion contract

- one complete skinned humanoid per actor;
- one valid Humanoid Avatar contract;
- the same Mixamo `PlayerHumanoidWalk.fbx` clip for Player/Father/Mother/Older Sister;
- `0.99380799s` per full left/right cycle (`120.7477` steps/min);
- controller-owned in-place travel at `1.0 world unit/s`;
- one bottom-centre root owns translation and yaw;
- continuous screen/office `SW -> NW -> NE -> SE` route, three walk cycles per direction;
- exact `clock=0 / SW / P0` first rendered frame with no startup teleport;
- global listener volume `0`, late-frame AudioSource mute/stop, and whole-run violation counters;
- captured SW/NW/NE/SE order, per-direction P0-P5 masks, expected-route/root continuity gates;
- Direct3D11 runtime QA and human visual review required.

## Current identity candidates

The four explicit model paths under `Candidates/PlayerV3`, `Candidates/FatherV1`,
`Candidates/MotherV1`, and `Candidates/OlderSisterV1` are new independent Blender candidates. No
existing 2D asset, Player V1/V2, Styloo content, or other mesh/texture/decal/motion donor or fallback
was used. Each candidate imports as one complete skinned body, one material, one external atlas,
23 skin bones, and an explicit valid Humanoid mapping. All four retarget the same locked walk clip.

The final generic FBX round-trip validator is fail-closed on a single active UV layer as the sole
UV0. All four pass. Active UV0 names are Player `PlayerV3AtlasUV`, Father `IdentityAtlasUV`, Mother
`UVMap`, and Older Sister `OlderSisterV1AtlasUV`. The Father's earlier multi-UV0 export was caught in
the real D3D11 image and replaced with the sole `IdentityAtlasUV` export before final validation.

Canonical FBX/atlas SHA-256 pairs:

- Player: `80CEEC5269D229D213DEBF17B90EB99FDB93B9DB60B8D3416AAB779D1A657EA9` /
  `46DD6CA613465C5E65338701AECB8FF029CB22C0059716CEEC5C9ED7ED6D7C8F`;
- Father: `417D28116037D23895AAA813089BD0EC25E1786370E60FECAE2BAB1B8761591F` /
  `6A271252664216266874DF5FDCD40775DFA3AF2D88747C4664C63E1D4ED334EA`;
- Mother: `59F0FB77C23FD9BD5457E2305E86DAFACD9BB3D62F4BE079ADA8D1CC65F85E01` /
  `4FA4D826132C72787CA740E917BB0B29A958C31D47E062D6B7B2C4705722D9A2`;
- Older Sister: `51EE97D6278038EDA30E24D74E62C75FC4AA00086D0C119BF76F54A2FE0B15D4` /
  `BAC4245933C91D5CDFBEADB9280F670CC7D1F93DA29B52BF9514EAA37B5EF48A`.

## Build

Run Unity `6000.3.21f1` with:

```powershell
& '<UNITY_EDITOR>\Unity.exe' -batchmode -nographics -quit `
  -projectPath '<REPOSITORY>' `
  -executeMethod FamilyCompany.Experimental.Family3D.Editor.Family3DIdentityCandidateBuilder.BuildFromCommandLine `
  -family3d-build-output '<REPOSITORY>\Artifacts\Family3DIdentityCandidateV1\BuildRun3' `
  -logFile '<REPOSITORY>\Artifacts\Family3DIdentityCandidateV1\BuildRun3\unity-build.log'
```

Default isolated output:

`Artifacts/Family3DIdentityCandidateV1/BuildRun3/FamilyCompany3DIdentityCandidateLab.exe`

`BuildRun3/build-receipt.json` records `buildResult: Succeeded`. Unity all-import receipt
`Artifacts/Family3DIdentityCandidates/UnityImport/all-import-receipt.json` records
`PASS_VISUAL_AND_MOTION_REVIEW_REQUIRED` and `productionEligible: false`.

Reproduce the final D3D11 QA in a fresh directory:

```powershell
& 'Artifacts\Family3DIdentityCandidateV1\BuildRun3\FamilyCompany3DIdentityCandidateLab.exe' `
  -force-d3d11 -batchmode `
  -family3d-qa-output '<REPOSITORY>\Artifacts\Family3DIdentityCandidateV1\D3D11Run4Final' `
  -family3d-qa-seconds 13.9
```

The final receipt is `AUTO_PASS_VISUAL_REVIEW_REQUIRED`: 420/420 frames contain visual content over
13.9100847 s, every SW/NW/NE/SE pose mask is `63`, and route-root continuity, audio enforcement,
and P0/P3 anatomical-foot alternation pass. Two earlier `ScreenCapture` attempts produced black
frames and were rejected; the final path uses camera `RenderTexture + ReadPixels` and a per-frame
luma gate.

The builder rejects output paths outside `Artifacts/Family3DIdentityCandidateV1/`, refuses an
interactive build while an open scene is dirty, saves only the generated experimental materials
individually, and does not call global `AssetDatabase.SaveAssets()`.

## StarterOffice QA-only integration

`Runtime/Family3DStarterOfficeCandidateQa.cs` and
`Editor/Family3DStarterOfficeCandidateQaBuilder.cs` connect the four candidates only in the generated
`Scenes/Family3DStarterOfficeCandidateQa.unity`. The builder copies `Prototype01.unity` into this
experimental scene and builds it together with the read-only `OfficeTileMigrationPreview.unity` by
passing explicit `BuildPlayerOptions.scenes`; it does not persist a Build Settings change.

Build the final isolated player with:

```powershell
& '<UNITY_EDITOR>\Unity.exe' -batchmode -nographics -quit `
  -projectPath '<REPOSITORY>' `
  -executeMethod FamilyCompany.Experimental.Family3D.Editor.Family3DStarterOfficeCandidateQaBuilder.BuildFromCommandLine `
  -family3d-starter-office-qa-build-output '<REPOSITORY>\Artifacts\Family3DStarterOfficeCandidateQaV1\BuildRun6SinglePassFinal' `
  -logFile '<REPOSITORY>\Artifacts\Family3DStarterOfficeCandidateQaV1\unity-build-single-pass-final.log'
```

Run the final D3D11 movement/layout pass with a fresh runtime output:

```powershell
& '<REPOSITORY>\Artifacts\Family3DStarterOfficeCandidateQaV1\BuildRun6SinglePassFinal\FamilyCompanyStarterOffice3DCandidateQa.exe' `
  -force-d3d11 -batchmode -familyCompanyMovementLayoutQa `
  -family3d-starter-office-qa-runtime-output '<REPOSITORY>\Artifacts\Family3DStarterOfficeCandidateQaV1\RuntimeRun6SinglePassFinal' `
  -logFile '<REPOSITORY>\Artifacts\Family3DStarterOfficeCandidateQaV1\RuntimeRun6SinglePassFinal\player.log'
```

`BuildRun6SinglePassFinal` is `Succeeded`, `productionMutation: false`, and
`productionEligible: false`. SHA-256 before/after values are identical for `Prototype01.unity`
(`5970EF496ACD81E7A0646A96807448E2283AB96F7D4866C234A09140D5872CD1`),
`OfficeTileMigrationPreview.unity`
(`1EC8C2156D887F083CB5F4EB63BB46D5F9451C3F9CAC8C239688D86F7AD0DA1F`), and
`ProjectSettings/EditorBuildSettings.asset`
(`010B57B9A51DE91C83FC9C7465DECFA0563214C74EA6A7E1DB5A991879890590`).

`RuntimeRun6SinglePassFinal` reaches Starter ready with four bindings. The project's official
MovementLayout QA passes, including eight Player directions (`observedDirectionMask: 255`) with
static/interaction/penetration violations all `0`. The adapter observes 4,165 moving sample frames;
Player contributes 2,651 moving frames with gait phase `0.000248..0.999476`. Three composite frames
pass visual-content checking with luma range `199..216`.

The adapter maps production 2D XY through the production camera viewport and overlay-camera ray to
the `Y=0` ground plane, uses `yaw=(direction-4)*45°`, and scales from live sprite bounds. A real-frame
review exposed duplicate rendering by the base and overlay cameras; during QA, layer 30 is now
excluded from the base camera and its original culling mask is restored on exit, so each final frame
contains exactly four candidate bodies once.

Coverage is deliberately limited: 3D presentation applies only while Standing or Navigating.
Approach, seating, work, and egress restore the original 2D presentation. Only Player was directly
driven through all eight office directions; Older Sister/Father/Mother were verified for binding,
scale, and standing. Their shared walk was separately verified for all four in `D3D11Run4Final`.
Full-3D furniture occlusion and seating remain the next office gate.

Human original-resolution visual review remains required. All candidates and both QA players are
isolated with `productionEligible: false`; production assets, the real office/default executable,
Build Settings, and Downloads builds are unchanged.
