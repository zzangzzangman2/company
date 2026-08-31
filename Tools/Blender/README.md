# Blender family-character tooling

Read `Docs/FAMILY_3D_CHARACTER_CANON_2026-08-24.md` before running or modifying these tools. The
the remaining legacy deliverables are isolated identity candidates, not production replacements. Each
was authored from only its committed turnaround: no existing 2D asset, Player V1/V2, Styloo mesh,
or other mesh/texture/decal/motion donor or fallback was used.

## Current builders

- Player: `build_player_v6_blender_identity_v3.py` (`--quality final`)
- Mother: `build_mother_blender_identity_v1.py` (`--final`)
- Older Sister: `build_older_sister_blender_identity_v1.py` (`--quality final`)

Father V1/V2 authoring scripts and candidates were retired and removed. The current Father is the
one-package V19 provider asset; hidden Blender conversion uses
`prepare_father_v19_meshy_one_package_unity.py` and must preserve its mesh, skeleton, weights and
action 613 together.

The remaining legacy final outputs contain one complete skinned mesh object, one atlas material, one atlas,
one 23-bone armature, a bottom-centre `Root`, and the required Unity Humanoid bones. The older
Player V1/V2 scripts and `ArtSources/Family3DBlenderPlayerDiagnosticsV1V2/` remain rejected
diagnostics and are not valid donors or Unity candidates.

Known-good Blender: official portable Blender `5.2.0 LTS`. Invocation pattern:

```powershell
& '<BLENDER>\blender.exe' --background --python '<SCRIPT>' -- `
  --output '<OUTPUT>' --reference '<TURNAROUND>' <FINAL_FLAG>
```

Use these exact script/output/reference/final-flag combinations:

| Role | Script | Output | Turnaround | Final flag |
| --- | --- | --- | --- | --- |
| Player | `Tools/Blender/build_player_v6_blender_identity_v3.py` | `Artifacts/Family3DBlenderPlayerV3` | `Assets/FamilyCompany/Experimental/Family3DPrototype/References/FamilyIdentityTurnaroundsV1/player-v6-3d-identity-turnaround-v1.png` | `--quality final` |
| Mother | `Tools/Blender/build_mother_blender_identity_v1.py` | `Artifacts/Family3DBlenderMotherV1` | `Assets/FamilyCompany/Experimental/Family3DPrototype/References/FamilyIdentityTurnaroundsV1/mother-3d-identity-turnaround-v1.png` | `--final` |
| Older Sister | `Tools/Blender/build_older_sister_blender_identity_v1.py` | `Artifacts/Family3DBlenderOlderSisterV1` | `Assets/FamilyCompany/Experimental/Family3DPrototype/References/FamilyIdentityTurnaroundsV1/older-sister-3d-identity-turnaround-v1.png` | `--quality final` |

## Fail-closed FBX round trip

Run `validate_family_humanoid_fbx.py` for each generated FBX:

```powershell
& '<BLENDER>\blender.exe' --background --python `
  'Tools\Blender\validate_family_humanoid_fbx.py' -- `
  --fbx '<CANDIDATE.fbx>' --receipt '<ROUNDTRIP_RECEIPT.json>'
```

The legacy validator requires exactly one mesh, one armature, one atlas material, one active UV layer as
the sole UV0, all 23 required bones, and valid skin weights. The remaining final revalidation receipts
are `PASS`; their sole active UV0 names are `PlayerV3AtlasUV`, `UVMap`, and
`OlderSisterV1AtlasUV`, respectively.

## Canonical candidate hashes

| Role | FBX SHA-256 | Atlas SHA-256 |
| --- | --- | --- |
| Player | `80CEEC5269D229D213DEBF17B90EB99FDB93B9DB60B8D3416AAB779D1A657EA9` | `46DD6CA613465C5E65338701AECB8FF029CB22C0059716CEEC5C9ED7ED6D7C8F` |
| Mother | `59F0FB77C23FD9BD5457E2305E86DAFACD9BB3D62F4BE079ADA8D1CC65F85E01` | `4FA4D826132C72787CA740E917BB0B29A958C31D47E062D6B7B2C4705722D9A2` |
| Older Sister | `51EE97D6278038EDA30E24D74E62C75FC4AA00086D0C119BF76F54A2FE0B15D4` | `BAC4245933C91D5CDFBEADB9280F670CC7D1F93DA29B52BF9514EAA37B5EF48A` |

All remaining legacy outputs remain `productionEligible: false`. The QA-only StarterOffice run passes Player
eight-direction movement; current Father V19 validation is owned by the dedicated reuse contract and V31
actual-map evidence rather than this legacy identity pipeline.
Human visual review and a separate user-approved production migration remain required.
