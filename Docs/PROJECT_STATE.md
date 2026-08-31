# PROJECT STATE

Last updated: 2026-08-30. This file contains current handoff state only. Superseded Father experiments are not current inputs.

## Father V19 single-workstation interactive playtest

- The isolated command-line mode `-family3d-father-v19-single-workstation-playtest` now starts from
  the real empty 13x13 office, places exactly one production `PlaceWorkstation` Father set, hides
  the other three actors only at the QA presentation layer, walks the real Father agent around one
  clear 3x3 grid loop, then runs the real `seat_father` route through approach/rotation/Working.
- An interactive run stays open with Father typing until the player window is closed. Supplying an
  explicit runtime-output folder records the deterministic evidence and exits after 360 Working
  frames.
- Build:
  `Artifacts/Family3DStarterOfficeCandidateQaV1/FatherV19SingleWorkstationPlaytestBuild`.
- Runtime evidence:
  `Artifacts/Family3DStarterOfficeCandidateQaV1/FatherV19SingleWorkstationPlaytestRuntime`.
- Actual Windows D3D11 result: `READY`, workstation `1/1`, visible legacy workstation renderers
  `0`, route completed, Working frames `361`, captures `203`, static/interaction/agent penetration
  `0/0/0`, phases
  `Idle>Navigating>ApproachingSeat>AligningSeat>RotatingToSeat>Working`.
- This remains an isolated review build with `productionMutation=false` and
  `productionEligible=false`; it does not modify the production/default/Downloads executable.

## Production office shop: atomic V31 CRT desk + open-back-chair set

- `사무실 -> 회사 -> 사무실 관리` is the existing production shop/build route. A normal new
  game still opens an empty editable 13x13 office.
- The shop now exposes `CRT 업무 책상·회전의자 세트` as one offer. The separate
  `swivel_chair` logical definition remains for saves, collision and seat binding, but it is no
  longer sold as a separate shop row and none of the retired green-chair pixels remain.
- Production visuals now come from the user-selected V31 dark-walnut CRT desk and graphite
  open-back chair. The accepted procedural set was baked into eight exact 640x512 / PPU 180
  directional Sprites (`desk_with_pc_{se,sw,nw,ne}` and
  `swivel_chair_{se,sw,nw,ne}`); no mirror or legacy fallback is allowed. The mesh X/Z axes remain
  physically orthogonal at 90 degrees, while a true-isometric camera projects them to the exact
  tile vectors `(160,80)` / `(-160,80)`. Each quarter-turn rigidly rotates the complete desk, CRT,
  keyboard and chair; no side-view mesh shear is allowed.
- One confirmation atomically creates two owned instances (V31 desk + V31 chair), one bound
  `OfficeSeatSlot`, and one ledger transaction. The gameplay price is KRW 377,500. Invalid
  placement creates no charge and no partial inventory.
- The preview draws both sprites with the same ground-anchor and uniform-scale correction as the
  confirmed runtime object. Green/red diamonds describe physical occupancy only: exactly two desk
  cells plus the visibly rendered chair cell. In base SE orientation, the pointer/chair/seat cell
  is `(x,y)`, the desk cells are `(x-1,y+1)` and `(x,y+1)`, and the old empty `(x-1,y)` cell is not
  claimed or painted. The empty chair approach cell is still mandatory for placement and
  path validation, but is not painted as furniture. Green means the complete rotated set, hidden
  approach reservation and office topology pass; red means out of bounds, existing-object overlap,
  non-floor placement, entrance/path disconnection, blocked workstation access or blocked chair
  egress.
- `R` turns the set through SE -> SW -> NW -> NE. The desk footprint, chair, chair-facing,
  approach cell and half-cell seated-character operator anchor use one rigid 90-degree transform.
- The first four purchased sets receive the first missing `seat_<familyMemberId>` in family order.
  Runtime rebuild therefore assigns actual work routing and docking to the newly purchased chair.
  The desk remains a hard obstacle; the chair is an interaction obstacle that only its seat owner
  may cross while docking, so other family members route around both.
- The transaction/native-pointer proof passed before the visual replacement: one click, cash
  `5,000,000 -> 4,622,500`, ledger `1 -> 2`, inventory `0 -> 2`, editable furniture `0 -> 2`,
  `seat_player`, matching runtime grid hash, and desk/chair render-anchor error `0 / 0`.
- Transaction evidence is local under
  `Artifacts/FastQa/workstation-native-pointer-20260829/` (`office-build-green-preview.png`,
  `office-build-placed.png`, `office-build-native-pointer-result.txt`).
- Current preview-ground proof is local under
  `Artifacts/OfficeBuildPreviewChairCellQa/20260829-101700/`. Actual Windows D3D11 reports
  physical markers `3`, `previewCellsMatchVisibleFurniture=True`, chair cell `2:2`, desk origin
  `1:3`, desk ground-anchor error `0.00000000` and chair ground-anchor error `0.00000000`;
  `office-build-green-preview.png` visibly places the third green diamond below the chair instead
  of the old empty cell.
- Current visual Player proof is local under
  `Artifacts/OfficeV31ChairCellFourDirectionQa/20260829-101900/`. It renders four purchased
  sets, all four desk directions and all four opposite chair directions with `legacyFlip=0`; the map
  screenshot is `v31-workstation-four-directions.png`. Runtime projection of all eight desk/chair
  ground polygons matches the authoritative tile footprint with maximum corner error `0.0003px`.
- Validation: `FAST_QA_WINDOWS.cmd -Profile asset-capture` PASS in 41.12 s,
  `-Profile player-scripts` PASS in 28.998 s, and `OfficeFurnitureBuildSystemValidation` PASS with
  `geometry=13x4`, four-direction placement/rotation, purchase, collision and save checks.
- All 34 standalone legacy workstation/chair source, runtime, foreground and `.meta` files were
  deleted. The remaining legacy 4x3 office atlas, its ten cut modules and the entire atlas cutter/
  validation path were also deleted on 2026-08-31. `OfficeBuildFurnitureVisualLibrary` hard-fails
  instead of returning old desk/chair catalog art, so a project rebuild cannot recreate any retired
  office module Sprite.

Production placement rule for every later furniture asset: the rotated semantic tile footprint is
the only authority for placement, collision, preview, sockets and runtime rendering. Physical mesh
axes must remain 90 degrees and be projected onto the `(160,80)` / `(-160,80)` diamond axes without
shear or mirror substitution. Actual Player ground-footprint corner error must be `<= 0.01px`; an
asset failing this gate must not be exposed by the shop or marked production-ready. The complete
normative contract is in `Docs/OFFICE_BUILD_EDITOR_V1.md` under “Mandatory production
tile-placement rule”.

## Current handoff: Father V19 walk + V31 original-chair atomic workstation

- The user approved the Father V19 one-package actual-map walk and restored colour with `좋아잘된당`.
- Father V1/V2 candidates and their dedicated legacy authoring/Unity labs were removed on
  2026-08-31. Local Father build/runtime/diagnostic outputs whose iteration was V1..V9 were moved
  out of the workspace to avoid accidental reuse. Do not restore them as implementation inputs;
  the current source is Father V19 and the current visual proof is V31.
- Locked locomotion input: the same Higgsfield/Meshy mesh, bind skeleton, skin weights and action 613 from one package.
- Current workstation proof: `FATHER_V19_FULL_3D_ALL_WORKSTATIONS_PROOF_COMPLETE`.
- V31 creates four `V31_AtomicWorkstationSet_OriginalChair_<seat>` roots. Each root owns one complete
  desk, CRT, keyboard and the user-selected V29 chair, so the visible pieces form one placement set.
- Chair appearance/position, seated actor placement/pose and CRT direction are exactly the V29
  composition. All 132 corresponding V31 Player PNG hashes match V29; V30's relocation/swivel is rejected.
- Production already promotes moving/rotating either bound desk or chair to the complete
  workstation: desk, chair, seat, approach and operator anchor move atomically.
- Desk footprints remain hard obstacles. Chairs remain seat-owner interaction obstacles: only the
  assigned/claimed actor can dock through its chair; everyone else routes around it.
- The retired gold foot cap, floating drawer details and all legacy desk/chair renderers stay absent.
- The workstation appearance is now production-selected by the user's explicit instruction. The
  Father character itself remains gated: the isolated V31 receipt still truthfully records
  `productionMutation=false`, `productionEligible=false`; workstation promotion does not promote
  the Father model.
- Higgsfield use for V31: `0 credits`.

Final isolated evidence:

- Build: `Artifacts/Family3DStarterOfficeCandidateQaV1/FatherV19MeshyOnePackage613MapBuildV26AtomicOriginalChair`
- Runtime: `Artifacts/Family3DStarterOfficeCandidateQaV1/FatherV19MeshyOnePackage613MapRuntimeV31AtomicOriginalChair`
- Tracked full GIF: [father-v19-v31-original-chair-atomic-set-full.gif](Evidence/Family3DFatherV19V31/father-v19-v31-original-chair-atomic-set-full.gif)
- Tracked close GIF: [father-v19-v31-original-chair-atomic-set-close.gif](Evidence/Family3DFatherV19V31/father-v19-v31-original-chair-atomic-set-close.gif)
- Tracked equality comparison: [father-v19-v31-v29-visual-equality.png](Evidence/Family3DFatherV19V31/father-v19-v31-v29-visual-equality.png) (left V29, right V31; identical pixels)

Media SHA-256:

| File | SHA-256 |
| --- | --- |
| full GIF | `B759D359DEAB1D99CA46983A18580F1F873E24F4CDD9388A33E99DD9F62A7C60` |
| close GIF | `46F627F87CEFDC42865CDC9C9B8327DE02D382B19F5AB9CE4DAC5AD3C10E7D76` |
| V29/V31 equality PNG | `8A8002BA2FC115EDB16576FBDCF2F62687C6531DD9504CE722BA43429A8F3766` |

## V31 verification

- Actual runtime phases: `Idle > Navigating > ApproachingSeat > AligningSeat > RotatingToSeat > Working`.
- Samples: `1051`; work observations: `361`; captures: `132` at `7.5 fps`.
- Desk origin/footprint: `(2,8)`, `2x1`; blocked cells `2:8`, `3:8` are non-walkable.
- Atomic original-chair workstation sets: `4 expected / 4 created`; visible legacy desk/chair renderers: `0`.
- Visual equality: V31 matches V29 for all `132/132` corresponding PNG SHA-256 hashes.
- Seat-to-keyboard: `0.5279421 <= 0.5656524` (`0.30h`).
- Keyboard-to-screen: `0.1535015 >= 0.1319855`.
- Keyboard inset from physical desk front: `0.1466583 > 0`.
- Seat/chair clearance outside desk front: `0.3812839 >= 0.2639711`.
- Actor-to-keyboard, actor-to-CRT, chair-to-CRT and screen-to-seat facing errors: `0 degrees`.
- Occupancy violations during the actual route: static `0`, interaction `0`, agent penetration `0`.
- Collision profiles: `PASS`, 52 profiles, 1,216 subcells, 628 default-radius clearances,
  416 visual approaches and visible pass-throughs `0`.
- Layout edit rule batch: `PASS`, accepted `18`, refused `6`; moving/rotating either member preserves the complete workstation binding.
- All 132 frames preserve the V29 chair/actor/CRT composition through approach, rotation, sitting and typing.
- Replaced seats: `seat_player`, `seat_older_sister`, `seat_father`, `seat_mother`.
- The V29 drawer-face correction remains active; no detached spike or retired gold foot cap returns. V30 chair relocation/CRT swivel is absent.
- Automatic COMPLETE is supporting evidence only; user visual approval remains the release gate.

Production guards are unchanged before/after:

| Guard | SHA-256 |
| --- | --- |
| `Assets/FamilyCompany/Scenes/Prototype01.unity` | `5970EF496ACD81E7A0646A96807448E2283AB96F7D4866C234A09140D5872CD1` |
| `Assets/FamilyCompany/Scenes/OfficeTileMigrationPreview.unity` | `256683B170CD18B46A0FBAAD1C654BD844586D900F343C0C7EB7F9F7C53B8026` |
| `ProjectSettings/EditorBuildSettings.asset` | `9FDAD82927314397B035ECBD90502A4E567DB85F0703DAC3B27F8966813BCBDC` |

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
| Save | `GameSaveDto v11`; reads/migrates v1-v10 |
| Locomotion | actual displacement direction, distance gait and canonical furniture avoidance |
| Render | 1920x1080 reference, native scale 1, pixel snap, 180 PPU, character scale 1.55 |
| Windows build | repository-relative scripts and `BUILD_INFO.txt`; deployment is outside this task |

Historical reports belong under `History/Reports/` and are not current handoff instructions.
