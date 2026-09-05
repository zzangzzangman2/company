# Older Sister V3 Higgsfield SD repair 613

Status: `CANDIDATE_USER_APPROVAL_REQUIRED`, `productionEligible=false`.

This candidate repairs the already-paid, user-rejected Older Sister V2 package locally. It did not
submit another Higgsfield or Meshy job and charged `0` new provider credits. The V2 source remains
preserved in its own candidate/artifact folders.

- FBX: `older-sister-v3-higgsfield-sd-repair-613.fbx`
- albedo: `older-sister-v3-higgsfield-sd-repair-albedo.png`
- Unity material: `OlderSisterV3CandidateSurface.mat`
- clip: `OlderSisterV3_Casual_Walk_inplace`, frames `1..43`, `1.4 s`
- structure: one skinned mesh, armature, material and UV set; original action 613; no donor,
  retarget, procedural gait, damping or contact-frame translation
- ratios: head/height `0.310`, hip/height `0.090`, shoulder/height `0.036`, leg/height `0.460`
- hashes: FBX `6639CB85D79B6385E089D9A3301AB1D2A9D1B20C9D8749E8144C629501846D2E`;
  albedo `7264BEA780D2B821A4128BBB9B47B83E3CD41DA2A4A7E20B98743E38ECB71473`

The full actual-map proof and measurements are under
`Docs/Evidence/OlderSisterV3CandidateCurrent/`. Do not copy this package into production or add
collision/seating defaults before the user approves the complete GIF.
