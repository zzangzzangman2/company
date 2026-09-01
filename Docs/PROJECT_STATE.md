# PROJECT STATE

Last updated: 2026-09-01. This file contains current handoff state only. Superseded Father experiments are not current inputs.

## Production cutover: Player V8 + Father V19 + V31 workstation

- The user's 2026-08-31 instructions promote the approved **Player V8** and **Father V19** packages
  as the only visible production bodies for those two runtime actors. Assets live under
  `Assets/FamilyCompany/Content/Resources/Production3D/{PlayerV8,FatherV19}/`; the authoritative
  adapter is `Assets/FamilyCompany/Runtime/Character3D/Family3DProductionPresenter.cs`.
- Production locks are Player scale/mesh height `1.024378657/1.857258558`, Father
  screen-standardized scale/mesh height `0.950318127/1.769311871` plus horizontal proportion scale
  `0.92`, map stride `0.7950477`, authored walk cycle `1.4 s`, full pose strength and `0.18 s`
  whole-body turns. Father never derives size from a retired sprite; 1280x720 D3D11 moving head
  and torso widths must each match Player within 1px. Each FBX's own
  Humanoid Avatar, skin and named walk clip (`PlayerV6_Casual_Walk_inplace` or
  `FatherV19_Casual_Walk_inplace`) stay together.
- The old selectable Player sprite presentations, contact-frame Resources, PSB/FBX authoring labs,
  bakers, importers and their dedicated QAs were deleted. The simulation still supplies its
  invisible direction/seat clock through a fail-closed SpriteRenderer, but it can never render and
  there is no command-line or missing-asset 2D fallback.
- Purchased seat-bound workstation sets keep the production shop, semantic tile footprint,
  collision, save IDs, seat assignment and four rigid quarter-turns. Their placed presentation is
  now one `Family3DWorkstation` root containing the approved V31 dark-walnut desk, CRT, keyboard and
  graphite open-back chair; the corresponding baked desk/chair SpriteRenderers are hidden. Shop
  thumbnails and placement ghosts continue to use the exact directional V31 sprites so their green
  footprint remains identical to the confirmed object.
- Normal new-game D3D11, Player/Father interaction, and the four-direction purchased-workstation
  route were revalidated in Unity
  `6000.3.21f1`: Player V8 bound at the locked height/stride, `playerPhase=Working`, four workstation
  roots, four desk/chair directions, mesh axes `90 degrees`, maximum tile-corner error `0.0003px`,
  bent knees `107.45 degrees / 113.16 degrees`, seated chair offset `0.13001`, and visible retired
  Player/workstation renderers `0/0`. Tracked screenshot and receipt:
  [Evidence/PlayerV8Production/README.md](Evidence/PlayerV8Production/README.md).
- Player and Father each move through the real `OfficeRuntimeAgent` path and sit at their own
  purchased V31 seat. Peer avoidance uses visible-body radii `0.28/0.30`, while proven static
  furniture/docking clearance remains `0.22`. A head-on D3D11 run recorded blocked agent moves `45`,
  penetrations `0`, rendered silhouette overlap pixels `0`, then `Working/Working` at
  `seat_player/seat_father` with static/interaction/agent violations `0/0/0`. Evidence:
  [Evidence/PlayerFather3DProduction/README.md](Evidence/PlayerFather3DProduction/README.md).
- Mother and Older Sister still await approved one-package 3D replacements; their current production
  representations remain untouched.

## 2026-09-01 company-PC review: legacy-2D screen-size candidate

- A pull/review of home commits `2698a21d..65971ec5` found that the latest production correction
  successfully matches Player/Father to each other at 1280x720: rendered height `74/72px`, head
  width `22/22px`, torso width `30/29px`, and area difference below 10%. It does **not** yet match
  the older 2D screen-height standard.
- All 48 retained HighMotion sprites were measured from their actual alpha bounds. Player height is
  `197/218/222px` min/median/max and Father is `208/229/233px` at 180 PPU. With the locked runtime
  visual scale `1.55` and shipping 16:9 camera, the median on-screen references are approximately
  `89.25px` Player and `93.75px` Father.
- The old 2D head/height ratios themselves are inconsistent (`0.396` Player, `0.272` Father), so
  the candidate copies only each character's total screen height. It deliberately keeps the two
  approved 3D head widths equal (projected `26.53/26.53px`) and torso widths close
  (`36.18/34.98px`) instead of reproducing the old oversized-Player-head mismatch.
- A fail-closed, command-line-only candidate is available through
  `-familyCompanyLegacy2DScaleCandidate`. It uses Player scale/height
  `1.263885643/2.291498763`, Father `1.306909878/2.454888000`, Father horizontal proportion
  `0.806840529`, and dynamic collision radii `0.345465984/0.412570225`. The V31 workstation keeps the
  already approved `1.857258558` reference height and therefore does not grow with the character
  candidate.
- In candidate mode only, Father uses the same neutral, emission/specular-free balanced-albedo
  shader family as Player, with Father-local neutral fill `0.82`. The QA records rendered mean
  luminance and saturation and rejects a luminance ratio outside `0.70..1.30`, either actor below
  luma `45`, or saturation below `0.12`.
- The strengthened D3D11 candidate run records the complete head-on approach rather than judging
  one collision pose. Its 88 ordered frames and 15 evenly spaced silhouette samples measured
  Player height `86/90/97px` min/median/max and Father `91/94/97px`; median head widths `27/28px`,
  torso widths `24/23px`, silhouette pixels `1751/1772`, luma `91.36/69.32`, and saturation
  `0.364/0.210`. The earlier single-frame check varied with gait phase and is no longer the
  acceptance source.
- The same run covered `3.59827/3.60416` map-unit travel, dynamic blocking with pixel overlap `0`
  and penetration `0`, followed by real purchased V31 routing to `seat_player/seat_father`, bent
  knees `83.01/86.79` and `96.24/100.85` degrees, `Working/Working`, and static/interaction/agent
  violations `0/0/0`. Portable evidence is under
  `Docs/Evidence/PlayerFather3DLegacy2DScaleCandidateV9/`; the Git-ignored local artifact directory
  `Artifacts/PlayerFather3DLegacy2DScaleCandidateV9-20260901/` additionally keeps all 88 raw PNGs.
- The generic Render Clarity capture previously rendered only `Camera.main`, so the two health bars
  could appear without the production 3D bodies while the unrelated pixel-clarity gate passed.
  It now composites `Family3DProductionOverlayCamera` into the same target. This was a QA capture
  blind spot, not evidence that the shipping screen omitted the bodies; the dedicated combined
  D3D11 proof rendered both bodies normally.
- Company-PC compile/build validation passed at
  `Artifacts/FastQa/runs/20260901-112736-779`. An ordinary Player with only hidden process style is
  forbidden because it can still display a render surface. The final D3D11 run used standalone
  `-batchmode`, `CreateNoWindow=true`, a continuously checked zero `MainWindowHandle`, and never
  opened a window. The candidate still remains `productionEligible=false` and is not the default
  until the user explicitly approves its GIF.

## Family character completion boundary

- Complete and user-approved: **Father V19/V31** and **Player/protagonist V6/V8** only.
- Not complete: **Older Sister** and **Mother**. Existing legacy candidates are not approved
  one-package successors and must not be described as finished.
- The compact approval matrix, exact V8 receipts and permanent failure-prevention list are in
  [FAMILY_3D_CHARACTER_COMPLETION_AND_FAILURE_GUARD_2026-08-31.md](FAMILY_3D_CHARACTER_COMPLETION_AND_FAILURE_GUARD_2026-08-31.md).
- Player V8 and Father V19 are production/default by the explicit cutover above. Their older isolated
  receipts remain immutable historical facts (`productionEligible=false` at capture time). No
  deployed executable or Downloads copy was changed.

## Player V6 package / V8 approved appearance and production runtime

- The current protagonist candidate was generated from the locked no-hat Player V6 four-view
  identity as one Higgsfield/Meshy package containing mesh, bind skeleton, weights, PBR and action
  `613 Casual_Walk_inplace`. Job `8609013b-996c-439a-97a0-0f3dc8a50cae` cost 38 credits; balance
  after completion was 72.
- Production Unity uses `Content/Resources/Production3D/PlayerV8/player-v8-production.fbx`, its own
  Avatar and `PlayerV6_Casual_Walk_inplace` directly at `poseStrength=1`. No Father/mixed clip,
  procedural gait, limb rewrite or pose weakening is enabled.
- Raw 127-frame/two-view inspection and actual-map 169-frame inspection show exactly two legs and
  shoes, two arms and hands, alternating contacts, small opposite arm swing, upright body, correct
  travel-facing and no tear/third-leg/residue. Numeric support: 42-frame/1.4 s repetition, foot
  correlation `-0.854584` raw / `-0.834884` map, hand correlation `-0.935886` raw / `-0.932847`
  map, runtime torso lean `1.490..3.390 degrees`.
- The user accepted the walking direction/appearance sufficiently to proceed to the real desk.
  The walk remains unchanged. The rejected grey presentation multiplied the complete one-material
  albedo by `0.74` and let the production sky probe vary from `0.61` overhead to `0.047` below; at
  map scale this killed the red/yellow/navy clothing and left silver-looking gaps between hair
  locks. Current V8 preserves the source albedo at white tint and uses the Player-only
  `PlayerV8BalancedAlbedo` shader with neutral fill `0.70` plus soft normal form `0.18`. It has no
  emission, reflection or specular path and does not recolour approved workstation visuals.
- Current seated/appearance build/runtime:
  `Artifacts/Family3DStarterOfficeCandidateQaV1/PlayerV6MeshyOnePackage613MapBuildV8PlayerOnlyBalancedColor`
  and `.../PlayerV6MeshyOnePackage613MapRuntimeV8PlayerOnlyBalancedColor`. The explicit
  `-family3d-player-v6-desk-work-qa` path runs the real `seat_player` route and reuses the approved
  Father StandingHeight-relative cushion, pelvis, wrist, knee and ankle correction on the Player's
  own Avatar.
- The final seated proof has 136 ordered captures, 813 samples, 361 Working observations, knee
  angles `106.3443° / 110.4238°`, 149,395 baked skin vertices with chair-part penetration `0`, four
  expected/created workstation visuals, legacy renderers `0`, and static/interaction/agent
  violations `0/0/0`.
- Review the current V8 candidate in
  [PLAYER_V6_FULL_3D_DESK_WORK_QA_2026-08-31.md](PLAYER_V6_FULL_3D_DESK_WORK_QA_2026-08-31.md),
  especially `player-v6-v6-v8-color-hair-comparison.png`, the full-map GIF and tracked-close GIF.
  Those receipts describe the earlier isolated review faithfully. The later explicit production
  cutover is `USER_VISUAL_APPROVED_PRODUCTION` and does not rewrite the historical receipt JSON.

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
- The isolated V31 receipt still truthfully records the pre-cutover state
  `productionMutation=false`, `productionEligible=false`. The later explicit production cutover at
  the top of this file separately promotes Father V19; the historical receipt itself is not
  rewritten.
- Higgsfield use for V31: `0 credits`.

Final isolated evidence:

- Build: `Artifacts/Family3DStarterOfficeCandidateQaV1/FatherV19MeshyOnePackage613MapBuildV26AtomicOriginalChair`
- Runtime: `Artifacts/Family3DStarterOfficeCandidateQaV1/FatherV19MeshyOnePackage613MapRuntimeV31AtomicOriginalChair-CompanyPullFull`
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

The superseded Family3D candidates and QA outputs were cleaned on 2026-08-31 after current-scene
GUID verification. Do not touch production/default/Downloads/deployed executables. On a company
PC, Unity and Blender must run hidden/background only.

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
