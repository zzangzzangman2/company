# Family 3D Identity Candidate Lab

> **V1 VISUAL REJECTION:** The user rejected PlayerV3/FatherV1/MotherV1/OlderSisterV1 after viewing
> their GIFs. They are diagnostic history only even where automated Unity/Humanoid/D3D checks pass.
> Do not promote, present as approved, or use their mesh/atlas/renders as V2 donors. Active visual
> authority is `Docs/FAMILY_3D_RUNTIME2D_V2_STYLE_LOCK_2026-08-24.md` and the family's actual 2D art.

This folder is a strictly isolated 3D experiment created after the project moved family-character
work to 3D. All new family-character work must follow
`Docs/FAMILY_3D_CHARACTER_CANON_2026-08-24.md`. Existing 2D family sprites, atlases, limb donors,
R-series candidates, and runtime frame slices are forbidden inputs for this 3D pipeline.

The folder does not alter the production family sprite catalog, production scene, default
executable, or Downloads build. The old 2D files remain only for history/migration safety until a
separate, user-approved production migration.

## Father V18 clean-biped natural walk V66 (current isolated review candidate)

V66 keeps the paid static Father V18 topology, UV, texture, proportions and the static FBX surface
material. Its moving rig is
`Candidates/FatherV18CleanBipedRigV4/father-v18-clean-biped-rig-v4.fbx`, SHA-256
`107DE6C4D2F36C1048746275B4E4E108447094705684D75AECF62CA1220F50B0`. The rig has 28,895
vertices, 49,192 polygons, 24 bones/22 deform bones, no cross-side or arm+leg mixed vertices, and
keeps 38 whole shirt/collar/waist panels on the torso chain so arm motion cannot split them.

No imported/generated motion clip is used. `Family3DWalkActor` drives a compact 0.88-second SD
biped cycle from this rig's own T-pose contract: alternating support/recovery legs, bent recovery
knees, no lateral pelvis sway, a tiny upward rise, and small opposite arms beside the body. The
T-pose arms are first lowered through Humanoid muscles; reapplying their rest transforms is forbidden
because the rejected V64 diagnostic proved that it restores horizontal arms. V66 defaults are torso
lean 0°, arm outward 1°, opposite swing 8°, elbow bend 12°.

The actual Father completed two map circuits with 1,344 telemetry samples, 673 rendered 30 fps
frames, and 169 rendered 2K frames. A separate 2K sweep records exact yaw
0/45/90/135/180/225/270/315°. Review media is under
`outputs/father-v18-clean-biped-natural-walk-v66-review`: a 34-frame close GIF and all-frame sheet,
full-map GIF/30 fps MP4, and paid-static-vs-moving exact 8-yaw comparison. The enlarged evidence does
not show torn clothing, a third leg, lateral crossing, detached shoes, backward torso, horizontal
arms, giant scale, wrong facing, or a loop jump.

This is `USER_VISUAL_REVIEW_REQUIRED`, `productionEligible: false`. No production/default/Downloads/
deployed executable changed, Unity/Blender/Player ran hidden/background only, and Higgsfield usage was
0 credits (balance 68).

## Father V18 clean-biped action-613 walk V61 (user-rejected history)

The user rejected the actual V61 GIF for torn clothing and a backward zombie-like torso. Later
diagnostics found 2,011 arm-contaminated vertices among 3,116 central torso vertices and mean signed
torso lean `-1.4516°`. V61/V62 also used a dark Unlit surface unlike the approved static render.
The former internal/automatic PASS is void.

V61 preserves the paid static Father V18 appearance and replaces only its deformation contract.
`Candidates/FatherV18CleanBipedRigV2/father-v18-clean-biped-rig-v2.fbx` has a horizontal T-pose bind
and Blender bone-heat weights capped at four influences. Opposite-side and arm/leg contamination are
both zero. The FBX SHA-256 is
`F72705D868199B36B40C51762ED8B3525E9CA1B6E09FE4802CAFE3C9C42256BF`.

The only time-varying source is the original `Casual_Walk_inplace` action 613 Humanoid clip at
`poseStrength=1.0`. Its generated moving mesh, skeleton and weights are forbidden. Runtime
sanitation removes invalid lateral/twist channels and keeps the torso upright while preserving the
clip's biped timing, bent-knee recovery, vertical weight shift and small opposite arm swing beside
the body. This is not the rejected V39 procedural gait.

The actual Father agent completed two Office perimeter circuits at 30 fps. Every one of the 673 map
frames and all enlarged tracking sheets was visually reviewed, including an exact 23-frame close
loop. No third-leg silhouette, rubber limb, crossed legs, detached shoe, hunch, raised/static arms,
giant scale, wrong facing or loop-end jump was visible. Evidence is in
`FatherV18CleanBipedCasualWalkMapBuildV61`, `FatherV18CleanBipedCasualWalkRuntimeV61HiFps`, and the
external review folder `outputs/father-v18-clean-biped-casual-walk-v61`.

This is `USER_VISUAL_REJECTED`, `productionEligible: false`. No additional Higgsfield credits were spent, and no
production/default/Downloads/deployed executable was changed.

## Father V18 clean-biped natural walk V39 (user-rejected procedural history)

The user rejected the prior imported Father V18 walk after viewing the actual video: fixing the
measured +90-degree facing offset solved direction only, while the malformed moving skeleton/skin
still produced a third-leg silhouette, rubber motion and nearly static arms. That moving mesh,
skeleton, weights and clip are not reused here.

`Candidates/FatherV18CleanBipedRigV1/father-v18-clean-biped-rig-v1.fbx` keeps the paid static Father
V18 appearance exactly: 28,895 vertices, 49,192 polygons, the same topology/UV/material slots and a
maximum rest-coordinate delta of `6.143906e-8`. Only a clean 24-bone biped armature and deterministic
weights were added. Cross-left/right and arm+leg mixed weight counts are both zero, and no vertex has
more than two influences. FBX SHA-256:
`83C6892C1C0F8BDC6081F3D8086BFCD5D4E4F3008F843F4ED07730FD94AB4F2F`.

V39 embeds no generated/shared motion clip. `Family3DWalkActor` drives a 0.88-second SD biped cycle:
alternating support and bent-knee recovery, a small body-side counter-swing of the arms, a centred
pelvis, and a tiny upward-only body rise. The V35 world-space impact IK was removed after telemetry
showed a one-frame 0.23-unit pull; contact flags remain phase telemetry, not an automatic visual-pass
claim.

The user rejected V36 after seeing hidden-looking arms and a hunched silhouette. V37 then exposed a
separate implementation error: direct arm/posture corrections used host `+Z` even though the measured
model forward is local `-X`. V39 consistently resolves body forward as `-transform.right` and body
side as `transform.forward`, starts the arms from the paid static rest pose, straightens the torso by
5 degrees, and applies only 2-degree outward placement, 6-degree opposite swing and 22-degree elbow
bend. The selected V38 style-B run and V39 defaults produce 169/169 byte-identical map PNGs.

The actual Father OfficeRuntimeAgent completed two same-map perimeter circuits. All 169 rendered
frames were enlarged and reviewed, not just a four-frame sheet. No third leg/cone, detached shoe,
mesh melting, leg crossing, giant scale, wrong direction, or loop jump was visible. Evidence is
`FatherV18CleanBipedNaturalWalkMapBuildV39` and the external workspace runtime
`outputs/father-v18-clean-biped-map-runtime-v39-final`. This is still
`USER_VISUAL_REVIEW_REQUIRED`, `productionEligible: false`; only the user can approve the GIF.
No production/default/Downloads/deployed executable changed, and no Higgsfield credits were spent.

## Father V14 stylized SD walk J (superseded rejected history)

The user-approved static `FatherApprovedV14`/Proof23 appearance remains unchanged. V13 is now
`USER_VISUAL_REJECTED`: enlarged review of both GIFs and all 24 source frames exposed detached shoes,
merged/dragged legs, weak support transfer, asymmetric arms, a layout transition and a loop jump.
Automated PASS and four-frame sheets are not acceptance evidence.

Proof26 fixes the remaining rig defect without changing approved static coordinates: 1,228 shoe
memberships transferred to finger bones are removed, and each shoe is bound only to its anatomical
Foot bone. The current candidate is
`Candidates/FatherApprovedV14NaturalWalkRigV1/father-approved-v14-natural-walk-rig-v1.fbx`, SHA-256
`0A4AE8A1620A9E7F85BF0A072DCB7B5553D2C584DC550A38EAC0DE2349383773`.

The user rejected G as `USER_VISUAL_REJECTED_FLOPPY_RUBBERY_MOTION` and H as
`USER_VISUAL_REJECTED_STIFF_GLIDING_STATIC_ARMS`. J keeps the approved mesh, materials, scale and
Proof26 rig. A frame review of actual Blue Archive SD cafe/select GIFs informed only the motion
principles: a long support-leg sweep, fast lifted recovery, and a visible bent-elbow counter-swing.
No external art, model, rig, texture, or animation asset is copied into this project.
`-family3d-father-map-walk-qa` drives the actual Father agent around one same-map 3x3 perimeter for
two circuits and refuses the Player movement-layout alias mode. J evidence lives under
`FatherApprovedV14StylizedSdWalkV17BuildJ` and `FatherApprovedV14StylizedSdWalkMapRuntimeV17J`; all
110 frames of the first circuit were enlarged and reviewed. It remains `productionEligible: false`
until the user approves the actual GIF.

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
& '<UNITY_EDITOR>\Unity.exe' -batchmode -quit `
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

### FatherApprovedV14 actual-map walk proof

The user-approved static Father v14 is rigged/exported by
`Tools/Blender/rig_export_father_approved_v14.py` and imported from
`Candidates/FatherApprovedV14/father-approved-v14-rigged.fbx`. The QA-only runtime flag
`-family3d-father-map-walk-qa` temporarily binds that visual model to the existing authoritative
Player movement-layout subject, so the exact eight-direction, corridor and collision oracle can be
reused without changing production state or scenes. Other family candidates remain hidden and their
2D presentations remain authoritative during this focused proof.

The full command adds the flag to the D3D11 pass above. The V4 motion candidate used
`poseStrength: 0.32`, blending the shared Humanoid clip toward the approved rest pose after an
earlier full-strength run exposed an unacceptable wide stride. Evidence is under
`Artifacts/Family3DStarterOfficeCandidateQaV1/FatherApprovedV14MapWalkRuntime4`: 3,495 movement
samples, 2,481 Father-visual moving frames, direction mask 255, gait phase 0.000824..0.999747,
24 composite frames with visual-content PASS, and zero replans, blocked static/interaction attempts,
or agent penetrations. The copied-scene build receipt confirms identical before/after hashes for
`Prototype01`, the preview scene and EditorBuildSettings. The user then rejected the GIF for giant
map scale, non-bipedal leg readability, and grotesque arm motion. V4 is sealed as
`USER_VISUAL_REJECTED_GIANT_SCALE_NON_BIPEDAL_ARM_MOTION`; its shared-human-clip damping approach
must not be reused. Static Father v14 and its valid rig remain approved; production remains blocked.

The replacement V13 bypasses the shared human clip and drives only a compact Humanoid muscle
profile: 55% map height, upper-leg ±0.05, swing-knee -0.09, arm-down -0.48 and arm swing ±0.008.
During that work, lowering the arms exposed 1,604 erroneous arm/hand memberships on trouser
vertices from nearest-surface transfer. Proof25 removes them, reassigns 927 orphaned trouser
vertices to pelvis/thigh/calf and verifies zero forbidden trouser influences. The replacement FBX
SHA-256 is `88734B5F16598B2027FC7F54139F27E17C6912755E2907F09EB97CBC72497094`. V13 D3D11 evidence is
under `FatherApprovedV14MapWalkRuntimeV13`: moving 2,502, direction mask 255, gait
0.000826..0.999978, 24 composites, and zero collision/penetration/replan violations. Human visual
review is still required.
