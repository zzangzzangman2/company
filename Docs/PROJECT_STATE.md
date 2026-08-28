# PROJECT STATE

Last updated: 2026-08-28. This file contains current handoff state only. Superseded Father experiments are not current inputs.

## Current handoff: Father V19 walk + V27 workstation

- The user approved the Father V19 one-package actual-map walk and restored colour with `좋아잘된당`.
- Locked locomotion input: the same Higgsfield/Meshy mesh, bind skeleton, skin weights and action 613 from one package.
- Current workstation proof: `FATHER_V19_FULL_3D_DESK_WORK_PROOF_COMPLETE`.
- V27 fixes the last two visible workstation defects: the late-created legacy green chair foreground is masked every QA frame, and the 3D chair uses neutral graphite/charcoal materials with equal RGB channels.
- The route, gait, body, hands, feet, desk alignment and seated typing are unchanged from the result the user called perfect except for those two defects.
- Status is `USER_VISUAL_REVIEW_REQUIRED`, `productionMutation=false`, `productionEligible=false` until the user approves the new GIF.
- Higgsfield use for V27: `0 credits`.

Final isolated evidence:

- Build: `Artifacts/Family3DStarterOfficeCandidateQaV1/FatherV19MeshyOnePackage613MapBuildV22NeutralChairNoLegacyOverlay`
- Runtime: `Artifacts/Family3DStarterOfficeCandidateQaV1/FatherV19MeshyOnePackage613MapRuntimeV27NeutralChairNoLegacyOverlay`
- Full GIF: `father-v19-neutral-chair-no-legacy-overlay-actual-map-final.gif`
- Close GIF: `father-v19-neutral-chair-no-legacy-overlay-closeup-final.gif`
- Full MP4: `father-v19-neutral-chair-no-legacy-overlay-actual-map-final.mp4`
- Close MP4: `father-v19-neutral-chair-no-legacy-overlay-closeup-final.mp4`
- Tracked full GIF for home pull: [father-v19-v27-full.gif](Evidence/Family3DFatherV19V27/father-v19-v27-full.gif)
- Tracked close GIF for home pull: [father-v19-v27-close.gif](Evidence/Family3DFatherV19V27/father-v19-v27-close.gif)

Media SHA-256:

| File | SHA-256 |
| --- | --- |
| full GIF | `536C4605B778C1320AE4EC8E71DC2C4E7AC33B84543E55D293A124AF9D66A804` |
| close GIF | `9F0BB210AA724B46A6B0C78E90D3ACFD6A473618D11DBF2B2C3C6DB01C52B18A` |
| full MP4 | `9E4E9B1EC6C6F838FD400DF8209D940FF9E510946A1F90D5F11A9EDCBEF5A78F` |
| close MP4 | `33A89268999436D42B6A93AA30917EF6C528F1A36D4A05BF743B9579A5F608A0` |

## V27 verification

- Actual runtime phases: `Idle > Navigating > ApproachingSeat > AligningSeat > RotatingToSeat > Working`.
- Samples: `1051`; work observations: `361`; captures: `132` at `7.5 fps`.
- Desk origin/footprint: `(2,8)`, `2x1`; blocked cells `2:8`, `3:8` are non-walkable.
- Chair-to-actor seat error: `0`.
- Seat-to-keyboard: `0.5279419 <= 0.5656523`.
- Keyboard-to-screen: `0.1535015 >= 0.1319855`.
- Keyboard inset from physical desk front: `0.1466583 > 0`.
- Seat/chair clearance outside desk front: `0.3812838 >= 0.2639711`.
- Actor-to-keyboard, actor-to-CRT, chair-to-CRT and screen-front-to-seat facing errors: `0 degrees`.
- All 132 frames were reviewed in six consecutive contact sheets. The green legacy chair crop does not reappear during approach, rotation, sitting or typing.
- Automatic COMPLETE is supporting evidence only; user visual approval remains the release gate.

Production guards are unchanged before/after:

| Guard | SHA-256 |
| --- | --- |
| `Assets/FamilyCompany/Scenes/Prototype01.unity` | `5970EF496ACD81E7A0646A96807448E2283AB96F7D4866C234A09140D5872CD1` |
| `Assets/FamilyCompany/Scenes/OfficeTileMigrationPreview.unity` | `1EC8C2156D887F083CB5F4EB63BB46D5F9451C3F9CAC8C239688D86F7AD0DA1F` |
| `ProjectSettings/EditorBuildSettings.asset` | `010B57B9A51DE91C83FC9C7465DECFA0563214C74EA6A7E1DB5A991879890590` |

## Locked method for every next family character

Use [FAMILY_3D_WORKSTATION_CHARACTER_REUSE_CONTRACT_2026-08-28.md](FAMILY_3D_WORKSTATION_CHARACTER_REUSE_CONTRACT_2026-08-28.md) as the only generation/import/walk/workstation procedure. Do not copy an old Father version paragraph.

The immutable order is:

1. Prepare four consistent clean views of one character.
2. Generate rig, skin and `613 Casual_Walk_inplace` in one Higgsfield/Meshy package.
3. Preserve that package through FBX conversion; remove only known helper geometry.
4. Validate one body, two arms, two legs, no duplicated limb and readable hands before Unity.
5. Test the unmodified package walk at `poseStrength=1` in all real map directions.
6. Measure stride and model-forward; never infer direction from bone names.
7. Bind the real production agent/seat/desk/chair and shared StandingHeight-relative workstation contract.
8. Capture and visually inspect every frame before requesting user approval.

## Home-PC continuation

```powershell
git switch main
git status --short --branch
git pull --ff-only origin main
```

Read in this order:

1. this file;
2. [FAMILY_3D_CONTINUATION_GUIDE_2026-08-25.md](FAMILY_3D_CONTINUATION_GUIDE_2026-08-25.md);
3. [FAMILY_3D_WORKSTATION_CHARACTER_REUSE_CONTRACT_2026-08-28.md](FAMILY_3D_WORKSTATION_CHARACTER_REUSE_CONTRACT_2026-08-28.md);
4. [FATHER_V19_FULL_3D_DESK_WORK_QA_2026-08-28.md](FATHER_V19_FULL_3D_DESK_WORK_QA_2026-08-28.md).

Do not stage or delete the pre-existing untracked `FatherV18CleanBipedRigV3` folder/meta. Do not touch production/default/Downloads/deployed executables. On a company PC, Unity and Blender must run hidden/background only.

## Project runtime baseline

| Area | Current production behaviour |
| --- | --- |
| New game | `2000-01-03 08:50`, family of four, capital KRW 5,000,000 |
| Office | empty editable 13x13 new-game office; furnished `StarterOfficeV1` is migration/QA fixture only |
| Attendance | family arrives 09:00-09:03 and leaves from 18:00 |
| Save | `GameSaveDto v10`; reads/migrates v1-v9 |
| Locomotion | actual displacement direction, distance gait and canonical furniture avoidance |
| Render | 1920x1080 reference, native scale 1, pixel snap, 180 PPU, character scale 1.55 |
| Windows build | repository-relative scripts and `BUILD_INFO.txt`; deployment is outside this task |

Historical reports belong under `History/Reports/` and are not current handoff instructions.
