# Family 3D Motion Lab V3

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

The Styloo all-in-one model instances are motion proxies. Their clothing/hair toggles make family
roles readable enough for synchronized gait review, but the pack cannot reproduce the approved
family identities exactly. No proxy is eligible for production promotion.

The locked identity inputs for replacement meshes are under
`References/FamilyIdentityTurnaroundsV1/`. They are reference images, not final meshes.

## Build

Run Unity 6000.3.21f1 with:

`-executeMethod FamilyCompany.Experimental.Family3D.Editor.Family3DPrototypeBuilder.BuildFromCommandLine`

Default isolated output:

`Artifacts/Family3DPrototypeV3/BuildRun3/FamilyCompany3DMotionLab.exe`

The builder rejects output paths outside `Artifacts/Family3DPrototypeV3/`, refuses an interactive
build while an open scene is dirty, saves only the generated experimental materials individually,
and does not call global `AssetDatabase.SaveAssets()`.
