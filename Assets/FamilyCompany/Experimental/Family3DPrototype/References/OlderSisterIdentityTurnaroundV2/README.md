# Older Sister 3D Identity Turnaround V2

Status: `REFERENCE_CANDIDATE_READY / 3D_JOB_NOT_SUBMITTED / productionEligible=false`

This is the current four-view source candidate for the Older Sister's first one-package 3D build.
It was generated on 2026-09-02 after comparing the approved Player V8 and Father V19 3D bodies.
The rejected `FamilyIdentityTurnaroundsV1` sheet was not supplied as an input.

## Source ownership

- identity only: `Assets/Art/Characters/OlderSister/older_sister_casual_neutral_v2.png`
- 3D family style, readable brightness and relative scale only:
  `Docs/Evidence/PlayerFather3DIndependentQaCurrent/family-size-color-standard-sheet.png`
- locked identity: 20-year-old Korean older sister, long near-black twin ponytails, matte black bows,
  teal eyes, charcoal sleeveless tank, navy dolphin shorts with white piping, barefoot
- geometry guard: arms and hands clear of the torso, separate leg openings, visible gap between both
  legs, exactly two complete feet, no skirt-like bridge

## Ordered provider inputs

Use exactly this order with Meshy `multi_image_to_3d`:

1. `older-sister-v2-front.png`
2. `older-sister-v2-three-quarter.png`
3. `older-sister-v2-left.png`
4. `older-sister-v2-back.png`

The wide `older-sister-3d-identity-turnaround-v2.png` is the review sheet, not an additional fifth
provider input.

## Locked 3D preflight

Higgsfield MCP returned exactly `38 credits` on 2026-09-02 with no job submitted:

```json
{
  "model": "multi_image_to_3d",
  "count": 1,
  "should_texture": true,
  "enable_rigging": true,
  "enable_animation": true,
  "should_remesh": true,
  "target_polycount": 60000,
  "topology": "quad",
  "symmetry_mode": "auto",
  "enable_pbr": true,
  "pose_mode": "a-pose",
  "rigging_height_meters": 1.65,
  "animation_action_id": 613,
  "enable_safety_checker": true
}
```

Action `613` was independently resolved as `Casual_Walk_inplace`. The selected Higgsfield MCP trial
expired on 2026-08-28 and the workspace currently requires an upgrade, so generation remains at zero
submissions and zero new credit charge. Do not downgrade this to Tripo, standard-quality geometry,
an unrigged mesh, a separate animation job, or a second seed.

## SHA-256

- `older-sister-3d-identity-turnaround-v2.png` —
  `3A01D2161AE1CEFA2685F55396E3B166086C17513C343BCD119E368AA62CE5E0`
- `older-sister-v2-front.png` —
  `A35DD4D17D94437899D59F5319E9C4B5BE95D56DC3BA1D7157601DB5BDA2F1DB`
- `older-sister-v2-three-quarter.png` —
  `CBFADE7752071226A980FA16E85EC3EEC6E7A3677BADB7F9F27C4265EC5ADCCD`
- `older-sister-v2-left.png` —
  `F5DB3D1AA1A2ECE16EE3BDB5E1D7B75620722E6275BCB3DE94AF1DAABC25603B`
- `older-sister-v2-back.png` —
  `048A6ACA994697A1BB14F3644C8D7115EAC0844E0E54F9CD7A49CEB034E729CD`

After a successful one-time submission, record the job ID, charged credit count, source GLB SHA-256,
Blender conversion receipt and actual D3D11 office evidence here and in `Docs/ASSET_MANIFEST.md`.
User approval of the full walk and four-direction seat GIF is still required before production use.
