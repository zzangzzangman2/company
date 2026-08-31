# Blender tooling for current Family3D packages

Updated 2026-08-31. This folder intentionally contains only generic inspection tools and the two
current one-package conversion scripts. Rejected manual modelling, donor, retopology, surface-fix,
Father V14/V18 and Player V1-V5 pipelines were removed so they cannot be mistaken for the approved
workflow.

## Current package conversion

- Father V19: `prepare_father_v19_meshy_one_package_unity.py`
- Player/protagonist V6: `prepare_player_v6_meshy_one_package_unity.py`

Both scripts preserve the provider-created mesh, bind skeleton, skin weights, UV/albedo and action
613 as one indivisible package. Run Blender only with `--background`; never mix a donor rig/clip,
procedural gait, limb rewrite or pose weakening into either package.

```powershell
& '<BLENDER>\blender.exe' --background --python `
  'Tools\Blender\prepare_player_v6_meshy_one_package_unity.py' -- `
  <script-specific arguments>
```

The exact inputs, options, hashes and approved state are recorded in `Docs/ASSET_MANIFEST.md` and
`Docs/FAMILY_3D_CHARACTER_COMPLETION_AND_FAILURE_GUARD_2026-08-31.md`.

## Tools retained for the next one-package character

- `analyze_generated_biped_walk.py`: inspect the returned authored walk.
- `audit_generated_biped_skin_overlap.py`: detect cross-limb/garment skin overlap.
- `render_character_turntable_frames.py`: deterministic multi-view still render.
- `render_generated_biped_animation.py`: enlarged animation render.
- `validate_family_humanoid_fbx.py`: validate Unity Humanoid structure after conversion.
- `validate_generated_biped_skin_glb.py`: validate the provider GLB before conversion.

For Mother or Older Sister, start with a new four-view provider `multi_image_to_3d` job and make a
character-specific copy of the current conversion contract. Do not resurrect the deleted Blender
identity builders. Follow
`Docs/FAMILY_3D_WORKSTATION_CHARACTER_REUSE_CONTRACT_2026-08-28.md` and keep the result
`productionEligible=false` until the user approves its full actual-map animation.
