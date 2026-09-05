# PROJECT STATE

Last updated: 2026-09-05. This file contains current handoff state only. Superseded Father experiments are not current inputs.

## 2026-09-05 tile-centre fixes + hot settings + GitHub patcher: RELEASE BLOCKED

- User approved proceeding with the opening loop, tile-centred walking, numeric live reload and
  **existing GitHub Releases** distribution, then PC shutdown after completion. Work is not complete:
  no game Release/Downloads promotion or shutdown has occurred.
- Normal path replans now first return to their start-cell centre; presentation lookahead cannot
  skip that joining segment. At cardinal corners residual velocity is projected onto the new segment.
  Blocked normal navigation waits/replans instead of sliding sideways off the tile-centre line.
- Approved Player V8 / Father V19 meshes, Avatars, clips, body scales, materials, V31 furniture and
  seating sockets are unchanged. A fixed whole-cycle mean ankle-centre offset aligns each body to
  its semantic root; a fixed Father ground correction (-0.135403 world units) aligns the cycle minimum
  with Player. No planted-frame translation, procedural gait or enlarged legacy profile promotion.
- Latest normal capture: `Artifacts/FastQa/WalkAudit-default-20260905-230848/`.
  **367 consecutive rendered frames / 23.989 s**, all 19 chronological sheets inspected. Four independent
  bodies, forced routes/teleports 0, runtime errors 0, occupancy violations 0/0/0. Maximum cardinal
  root-line error <= **0.000189 px**, including the yielding retreat. Moving ankle midpoint median/max:
  player **1.96/4.14**, sister stand-in **1.95/4.30**, father **1.66/3.32**, mother stand-in **1.58/3.29**.
  This passes root/ankle-centre measurements, NOT independent pixel-sole slip or full visual release
  acceptance. Actual captured evidence averages about 15.3 fps; 30fps MP4 duplicates are not a
  native performance PASS. Clips remain at measured capture timing.
- **Resolved head-on failure:** forced head-on Player/Father test
  `Artifacts/FastQa/PlayerFather-centres-20260905-220419/` found **84 overlapping silhouette pixels**
  at collision stop, despite no collider penetration. It stopped before work/seat checks.
  Source now expands only dynamic body/body clearance by the fixed calibration envelope:
  Player 0.28 -> **0.445**, Father 0.30 -> **0.415**. Static furniture radius stays 0.22.
  Fresh default FastQA contact capture now measures **0 overlapping pixels**. The original 84-pixel
  failure remains historical evidence. No change to model/body scale, gait clip or seat sockets.
- **Resolved furniture intersection:** the initial retest's legacy self-PASS still reported 11/14
  intersecting desk frames. Default actors now use furniture padding **0.18** (static total 0.40)
  and the existing desk-adjacent path cost +2.5. A new retest measured **0/0 mesh-intersection frames**,
  both actors reached their detour goals and worked in their own seats. The external pair runner now
  rejects nonzero/missing mesh intersection counts, not just a legacy PASS string. Final same-build
  pair result: `Artifacts/FastQa/PlayerFather-centres-20260905-230502/`, **PASS**, 134 detour frames,
  zero intersecting sampled vertices, seated centre error 0/0, Working/Working, retired visible 0.
  This is a two-actor fixture, not all four seat directions or independent release approval.
- **Resolved normal wandering deadlock:** increasing body clearance exposed two actors stopped for
  over 50 seconds while reserving one another's next cells. Normal wandering now counts zero-forward
  traffic waits; the lower-priority ID retreats to a reachable centre on its current cardinal rail,
  including a named reservation blocker beyond the physical contact radius, then replans normally.
  No teleport, injected destination or lateral off-grid sliding. A new 8-second no-progress gate
  catches this even when an actor had already travelled enough to satisfy the older weak test.
  `Artifacts/FastQa/OpeningShop-20260905-230327/`: **60-second normal PASS**, travel father 51.3456,
  mother 53.9563, sister stand-in 52.5758, player 50.5686; maximum navigating no-progress respectively
  **1.9206 / 0.0890 / 0.9011 / 0.0889 seconds**, occupancy violations 0/0/0, runtime errors 0.
  Four paid rotations leave 3400000 won; overlapping placement causes no charge. Native pointer is
  still NOT tested by this controller-driven harness.
- **Exact cleanup blocker resolved by user reauthorization (`너가해`).** The original failed cache
  and three subsequently identified failed test identities were each verified against all **166**
  path/size/SHA256 entries and their matching cache/build identity, then moved to Windows Recycle Bin.
  Exact `Artifacts/FastQa/cache/WindowsPlayer` was verified absent before each fresh build.
  Recoverable, but failed payloads must not be restored for play. Source, saves, sibling cache,
  Library/Bee and the pre-existing untracked sister inputs were preserved.
  Non-executable records: `Artifacts/FastQa/FailedPayloadEvidence/` folders
  `20260905-220419-contact-overlap`, `20260905-223352-desk-intersections`,
  `20260905-224844-wander-deadlock`, `20260905-225826-reservation-deadlock`.
  The current cache is a **new development-only identity**, not any of those failed payloads.
  Latest build: `Artifacts/FastQa/runs/20260905-230213-598/`, **22.814 seconds PASS**.
- Development settings: `OfficeDevelopmentTuningLoader` polls an explicitly named JSON every 0.5s,
  validates all fields and atomically swaps an immutable pure-C# snapshot. Editor/Development/FastQA
  only; ordinary Release ignores the flag. Invalid partial edits keep the last good snapshot.
  Live test `Artifacts/FastQa/DevReload-20260905-220249/reload-result.json`: **PASS**, same EXE, 0 builds,
  all four cruising speeds 1 -> 0.5 -> invalid edit remains 0.50001 -> 1; prices 400000 -> 480003 -> 400000.
  Actual transaction test also checks odd total rounding/purchase bases; existing inventory/save costs
  are never repriced. C# changes still need compilation.
- GitHub updater source is in `Tools/Updater/`; detailed operation and remaining gates:
  [GITHUB_PATCHING.md](GITHUB_PATCHING.md). It is **implemented/tested locally, not internet game
  distribution complete**. Per-file gzip, SHA-256, verified reuse, atomic active-version pointer,
  interrupted activation recovery, invalid-path/junction/concurrency checks, verified-only offline
  fallback. Public GitHub repo pinned; no embedded account token. Saves remain outside the patch store.
- Windows PowerShell 5.1 updater: **36 checks PASS** in
  `Artifacts/UpdaterTests/c589cb5346ed40f4b23fff4b94c4d3fa/result.json` (inert non-PE fixtures, no game
  launched). Real GitHub first-start check correctly returns 404/no installation because no Release
  exists; it does not fall back to a random local EXE. Actual internet game download/publish untested.
- Earlier checks (before this final traffic correction): normal opening/shop 60s PASS
  `Artifacts/FastQa/OpeningShop-20260905-215359/`, four paid rotations leave 3400000 won, overlap no
  charge, errors 0. Navigation validation PASS: 1,152 paths / 53,108 oracle segments / 128 replans.
  Furniture validation PASS: `Artifacts/FastQa/dev-pricing-validation.log`.
  Pure simulation PASS 3.978s; earlier editor compile/PrototypeValidation PASS **19.372s**
  (`Artifacts/FastQa/runs/20260905-221154-151/`). Last player build before radius fix PASS **48.655s**.
- Remote source through `e7830bb1` (Older Sister V3 isolated candidate) was merged at `bf0c06c1`.
  Both sides of the documentation were retained. Existing untracked Older Sister V2 inputs remain
  untouched and unstaged. No sister candidate promotion. Merged-source Editor/PrototypeValidation
  PASS **19.505s**, `Artifacts/FastQa/runs/20260905-222410-432/` at that merge checkpoint.
  Final source compile/PrototypeValidation now PASS **14.720s**, `Artifacts/FastQa/runs/20260905-231035-888/`.
  Complete branch/tag/release/LFS inventory must remain prohibited=0, unknown=0 before source push.
- Remaining: complete seated/working four directions, independent native shop pointer and
  next-day 09:00/09:01/09:02/09:03 attendance, foot-slip/grounding/mute and user visual approval,
  then clean committed Release provenance and actual GitHub publish/download test. Only after all
  requested work is genuinely finished should the authorized PC shutdown run.
- Latest portable non-executable evidence, videos and exact remaining steps:
  [Evidence/OpeningCollisionRetest20260905/README.md](Evidence/OpeningCollisionRetest20260905/README.md).
  Older evidence under `OpeningWalkPatch20260905` remains historical, not final source proof.

## 2026-09-05 current opening: four temporary 3D family bodies + buy/place first

- Implemented in canonical `main` at `C:/Users/godho/Documents/Codex/fc_agents/integration_p0`.
  New game begins at **08:50 with all four family actors already walking inside the empty office**.
  No free editable furniture, seats or inventory are supplied. The founding-morning presentation
  exception does not change the pure 09:00 work schedule or later days' staggered arrival/departure.
- The user approved **5,000,000 won opening capital / 400,000 won per complete V31 desk+PC+chair set**.
  The public shop exposes this one atomic set only; four purchases leave **3,400,000 won**. Old generic
  furniture definitions/transactions remain for saves/fixtures, not as other public shop offers.
- Temporary appearance map: `player + older_sister -> Player V8`, `father + mother -> Father V19`.
  Thus the office shows **two sons and two fathers, all 3D**. `OfficeFamily3DVisualRoster` is the one
  appearance authority. Each retains its original family ID, age, role, save state, independent
  runtime/occupancy and `seat_<memberId>`. Mother/Older Sister remain unfinished identities; this is
  NOT a new package generation or an approval of their final bodies. Their 2D body/seat pixels are
  now suppressed with no fallback. Original 3D mesh, Avatar, clip, size, colour and seating are unchanged.
- Purchase preview retains three real occupied tiles (desk 2 + chair 1), four rigid quarter-turns,
  rotated approach/seat sockets, overlap rejection and one confirmation/one charge. Layout rebuilding
  now also releases partially invalid 3D bindings so destroyed semantic actors cannot leave stale
  hosts for the next binding. `OfficeContractTaskCoordinator.ResetAssignments` also checks the Unity
  lifetime of interface-held actors before cancelling tasks; destroyed actors must not resume paths
  against their already-removed Grid/Transform. The existing pair-only ratio QA explicitly hides the
  two parked stand-ins.
- Current verification, Unity **6000.3.21f1**, same warm main checkout:
  - `FAST_QA` simulation-pure: PASS, 4.004 s, `Artifacts/FastQa/runs/20260905-095412-336/`.
  - `FAST_QA` editor-validation (`PrototypeValidation`): PASS, 48.201 s,
    `Artifacts/FastQa/runs/20260905-095632-435/`.
  - `OfficeFurnitureBuildSystemValidation.RunBatch`: PASS, `Artifacts/FastQa/OpeningShopLogic-20260905/editor.log`.
    It checks four semantic/visual bindings, founding-vs-next-day attendance, 5000000/400000/3400000,
    single shop offer, 4-direction footprint/seat/approach, duplicate charge, overlap and save round trips.
  - Final FAST_QA scripts build: PASS, 18.033 s, `Artifacts/FastQa/runs/20260905-100738-406/`.
  - Final hidden D3D11 Player: **PASS**, `Artifacts/FastQa/OpeningShop-20260905-100825/`.
    `MainWindowHandle=0` in 399 checks; runtime errors/exceptions **0** (receipt plus completed-log audit).
    Normal 60-second 08:50→09:48 observation used no teleport,
    injected route or forced clock advance. Travel: father `51.7100`, mother `53.1719`, older_sister
    `53.5624`, player `49.4328` office units; all four used eight directions and independent
    coordinator destinations. Static/interaction/agent penetration counters `0/0/0`.
    Four controller confirmations charged 400000 each; cash `4600000/4200000/3800000/3400000`,
    seats `1/2/3/4`, final inventory `8`, final 3D workstation roots `4`. Overlap confirmation was
    rejected without a charge. Legacy character/workstation renderers visible `0/0`.
    `normal-wander.csv`, `wander-000..029.png`, `shop-preview-0..3.png`, `shop-overlap-rejected.png`,
    `four-family-four-workstations.png`, `opening-shop-final.txt` contain current evidence.
- Capture caveat: the first attempt (`OpeningShop-20260905-100000`) produced black batch swap-chain
  screenshots and asserted before asynchronous layout preparation completed. It is **not** accepted
  visual evidence. Final capture explicitly renders the actual office camera + actual 3D overlay,
  rejecting blank pixels; it does not include IMGUI. Final screenshots were visually inspected.
  Confirmations used the real controller method programmatically, **not native pointer input**.
  IMGUI/full-window visual and real click→confirm retest remain pending; do not relabel old native PASS
  or this programmatic PASS as current native-pointer evidence. Pair-only seating QA was not rerun.
  The intermediate `OpeningShop-20260905-100332` passed functional assertions but its final log audit
  found four caught stale-agent NullReferenceExceptions during unbind. It is **not** the final clean
  runtime PASS. After the lifetime fix, the QA fails any Error/Exception and the runner also checks
  the completed log; the final clean rerun is `OpeningShop-20260905-100825` above. A Unity shutdown
  ComputeBuffer disposal warning remains; it is not counted as a runtime exception or a new visual gate.
- Rerun from repo: `Tools/Invoke-FamilyCompanyFastQa.ps1 -Profile player-scripts -NoPlayerSmoke`, then
  `Tools/Invoke-FamilyCompanyOpeningShopQa.ps1` (hidden player, bounded timeout, window-handle guard).
  Initial four-person layout intentionally stays empty; the QA's four purchased sets are a test state,
  not automatic gameplay furniture.
- No Higgsfield call/charge, production-candidate profile promotion, Downloads/deployed build change,
  save overwrite, commit or push was requested/performed. Existing Older Sister V2 inputs and the
  pre-existing PROJECT_STATE preparation note below are preserved. Unity generated `.meta` files for
  those pre-existing untracked inputs; preserve them too. Next: user review of this opening loop and,
  when a foreground window is allowed, native build-editor click + full IMGUI acceptance. Mother/Sister
  final 3D generation remains deferred.

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
- Historical 2026-09-02 proof (superseded by the current contact failure at the top): Player and Father
  each moved through the real `OfficeRuntimeAgent` path and sat at their own
  purchased V31 seat. Peer avoidance uses visible-body radii `0.28/0.30`, while proven static
  furniture/docking clearance remains `0.22`. A head-on D3D11 run recorded blocked agent moves `45`,
  penetrations `0`, rendered silhouette overlap pixels `0`, then `Working/Working` at
  `seat_player/seat_father` with static/interaction/agent violations `0/0/0`. Evidence:
  [Evidence/PlayerFather3DProduction/README.md](Evidence/PlayerFather3DProduction/README.md).
- Mother and Older Sister still await approved one-package 3D replacements; their current visible
  bodies use the 2026-09-05 temporary mapping above, while their semantic identities stay unchanged.
- Historical Older Sister V2 preparation note (before the subsequent V2/V3 work): a new front/three-quarter/left/back source set was made
  from her canon identity plus the current Player/Father 3D style only. The rejected V1 turnaround was
  not used. Original-resolution inspection found separate arms, hands, shorts openings, legs and two
  complete bare feet. Higgsfield MCP preflight for the locked Meshy one-package contract returned
  exactly `38 credits`; no 3D job was submitted because the selected MCP trial expired on 2026-08-28
  and the workspace requires an upgrade. Source, hashes, exact order and parameters:
  `Assets/FamilyCompany/Experimental/Family3DPrototype/References/OlderSisterIdentityTurnaroundV2/README.md`.
  Status remains `productionEligible=false`.
- The V2 preparation inputs above remain locally untracked and are preserved. Subsequent remote
  commits added the isolated V3 zero-credit SD repair candidate below. It is not the current sister
  appearance: the 2026-09-05 opening still uses the requested Player stand-in until approval.

## 2026-09-02 Older Sister V3 zero-credit SD repair walking candidate

- V2 remains explicitly rejected for its realistic six-head body, tiny face/eyes and merged dark
  clothing. The user required a repair without paying again. V3 therefore derives only from the
  already-paid V2 GLB (`62E1366B...3D3DD`) and charges **0 new provider credits**. No Higgsfield or
  Meshy generation was submitted for V3.
- The repair is deterministic and local: one continuous bind-space proportion map modifies the
  same mesh and bind skeleton while retaining its skin weights, UV topology and original action
  613. No donor, retarget, procedural gait, rigid-arm rewrite, pose damping or contact-frame host
  translation is used. The source V2 package is preserved separately.
- Measured rest ratios now pass the family standard: head/height `0.310`, hip width/height `0.090`,
  shoulder width/height `0.036`, leg/height `0.460` (S6/S7). The same UV atlas is deterministically
  recoloured so near-black hair, lighter charcoal tank, navy shorts, white piping, skin and teal
  irises remain separate at map scale.
- The Unity package still contains exactly one skinned mesh, one Humanoid armature, one material,
  one UV set, 211,673 vertices, 118,945 polygons and 24 bones; unweighted/invalid references are
  `0/0`, with at most four influences. FBX SHA-256 is `6639CB85...846D2E`; albedo SHA-256 is
  `7264BEA7...B71473`. Clip is `OlderSisterV3_Casual_Walk_inplace`, frames `1..43`, `1.4 s`.
- The hidden Windows D3D11 run completed two real 3x3 map circuits: 337 images, 1,344 telemetry
  samples, all four diagonal directions, occupancy `0/0/0`. Locked target is `2.367 = 93.02px`;
  temporal-background silhouette median is `86px`, inside S1 `81..99px`. Face projection is
  `28.84px` and four frontal frames show both eyes at least `3px` high, passing S8.
- Actual-map silhouette luma is `91.49` (C3 `90..125`), saturation `0.247`, and white clipping `0%`.
  The measured foot-centre offset `(0.034554,0.112794)` gives tile error median/max
  `2.715/5.856px` (gate `4/8`). All individual foot-bone points are inside the agent-centred tile
  (`0/2688` outside; planted `0/1120`; minimum planted margin `8.85px`). One fixed ground correction
  `-0.073097` aligns the walk-cycle minimum `0.210697 -> 0.137600` to Player.
- Candidate assets: `Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/OlderSisterV3HiggsfieldSdRepair613/`.
  Evidence: `Docs/Evidence/OlderSisterV3CandidateCurrent/`.
- Status is `CANDIDATE_USER_APPROVAL_REQUIRED`, `productionEligible=false`. Production resources,
  normal executable, collision radii and seating remain unchanged until the user approves the full
  actual-map GIF.

## 2026-09-02 independent Father scale/walk re-QA follow-up

- The command-line-only `-familyCompanyLegacy2DScaleCandidate` remains
  `productionEligible=false`. Its enlarged Player/Father scale values and approved one-package assets
  remain unchanged. Its two alternating action-613 steps now span exactly two tile-centre distances:
  candidate stride `1.98761598`, phase `0.40`, with no planted-contact whole-host translation. The
  former `0.99380799` stride and contact/release translation are superseded because they caused
  frame-dependent visible-root correction. Production/default remains `0.7950477`, phase `0`.
- 2026-09-02 afternoon correction (Claude): the user's "Father steps on the tile lines" report was
  confirmed on the 82-frame GIF with the tile diamond drawn around each agent centre. The
  `(0.037517,0.500000)` and `(-0.24,0.5)` standing offsets tuned against the shoe-pixel centroid had
  moved the Father's feet onto the tile corner (planted line touches `57/61`, foot-midpoint tile error
  `19.3px`). The pixel centroid mixes shoe height with floor position and is now informational only.
  Real cause: both walk clips lift the hips above the bounds-grounded bind pose, so the lowest skinned
  vertex floats `0.138` (Player) versus `0.429` (Father) office units, drawing the Father `12-15px`
  higher on screen. Fix: offset restored to `(0.037517,0.138023)` and a candidate-only
  `AlignCandidateStandingGround` lowers the Father's standing/walking visual ground by the measured
  lowest-vertex difference (`-0.2910`); seated pose and production/default untouched. Final run
  `Artifacts/FatherStandingGroundAlignedFinal-20260902-141500/`: planted line touches `2/8` of `61/61`,
  foot-midpoint tile error `2.227/6.129` and `1.464/4.306px`, lowest skinned vertex `0.1473/0.1502`,
  same-tile shoe centroid delta `-0.201/1.582px`. QA now gates Father on the bone-based tile error and
  on ground clearance within `0.05` of Player. Still `CANDIDATE_USER_APPROVAL_REQUIRED`.
- Desk detour proof (same day, `Artifacts/FatherDeskDetourProof-20260902-145500/`): the QA now routes
  Player `(3,8)->(3,2)` and Father `(7,8)->(11,8)` straight through blocking V31 desk footprints. Both
  detour around the desk (`(3,6)->(2,6)->(2,5)->(2,4)->(3,4)` and `(8,8)->(8,7)->...->(11,7)->(11,8)`),
  closest body edge `+0.17/+0.23` cells, frames inside a footprint `0/0`, static/interaction violations
  `0/0`. Seat-route frames are also captured (`route-frames/`). Evidence:
  `Docs/Evidence/PlayerFather3DIndependentQaCurrent/player-father-desk-detour.gif`.
- Arms in desk tops (user report, same day): the candidate bodies' arm swing reaches `0.514/0.407`
  world units from the agent centre while furniture clearance was `0.22`, so walking the cell beside
  a desk put the arm inside the desk top (measured `36/68` of `113` detour frames with skinned
  vertices inside desk part boxes). Candidate-only fixes: `OfficeRuntimeOccupancy` furniture-only
  clearance padding `+0.18` (total `0.40`, below the `0.397` half-cell edge distance; own seat desk
  exempt) and a `+2.5` desk-proximity step cost in `OfficeRuntimePathService` for padded actors.
  Final run `Artifacts/FatherDeskClearanceFinal-20260902-160500/`: routes keep one cell from desks
  (`(1,x)` column / `(x,10)` row), vertices inside desk geometry `0/0` frames, closest vertex
  `0.547/0.199` world, static/interaction violations `0/0`, seats still reached. Production/default
  padding and path cost are unchanged (`0`).
- Character docs consolidated (2026-09-02, user request): all FAMILY_3D_*, FATHER_V19_*, PLAYER_V6_*
  and the size/colour standard were deleted and replaced by one authority,
  `Docs/FAMILY_3D_CHARACTER_STANDARD.md` (rules, per-character parameter table, size S1-S5, colour
  C1-C6, grounding, collision, seating contract, new-character procedure, QA commands, failure list).
  Old texts remain in git history before `4c1cb829`.
- Size/colour standard (now folded into `FAMILY_3D_CHARACTER_STANDARD.md`) fixed the units
  (`48.0 px` per office world, `39.3 px` per 3D unit, tile `85.3x42.7 px` at 1280x720), the family
  height table (candidate 3D `90/93.5 px` versus visible 2D Mother/Sister `93.0/84.3 px` and legacy 2D
  Player/Father `90.1/94.7 px`; approved production 3D `73/69.5 px` is `20%+` smaller than the 2D
  family) and the colour table. Size rules S1-S7 pass.
- Brightness (user: "dark is unacceptable; only the 3D pair must match each other, 2D will be
  deleted"): candidate-only material tint gain Player `1.26` / Father `1.28`
  (`*Legacy2DMatchedBrightnessGain`, runtime `_Color`). Isolated same-tile silhouette luma rose from
  `93.9/73.7` to `118.2/93.2` (ratio `0.789`, white clipping `3.0%/0%`). Father `1.42` blew out skin
  and `1.32/1.32` clipped `11%` of the Player hoodie, so both were rejected. The neutral shader
  ignores the directional light, so gain (or albedo) is the only brightness control. Final run
  `Artifacts/FatherBrightnessFinal-20260902-165000/`; all tile/ground/desk metrics unchanged. C4 in
  the standard now reads luma `>=110/>=90`, clipping `<=5%`. Production/default gain is `1.0`.
- Exact same-cell `(6,6)`, same-camera/light/tile 1280x720 masks measure total height Player/Father
  `88/92px`, head bounds `27x27/25x31px`, head:height `0.306818/0.336957`, shoulder width `27/27px`,
  torso width `34/32px`, leg height `44/42px`, shoe pixels `233/208`, silhouette area `1792/1900`
  and screen occupation `0.194444/0.206163%`. Total height differs by `4.55%`; the remaining
  difference is Father-internal proportion (head height `+14.81%`, leg height `-4.55%`), not camera
  distance or global scale. No approved face/hair/outfit or mesh proportion was deformed.
- Across all 82 approach frames, rendered height min/median/max is Player `85/90/95px`, Father
  `91/93.5/98px`; head height `24/25/27` versus `30/31/33px`; leg height `41/45/49` versus
  `40/42/48px`; silhouette area `1571/1729/1921` versus `1578/1768.5/1900px`.
- Candidate-only peer radii are `0.475/0.578`; `0.940` only covered the removed `0.36` visible host
  advance. The hidden D3D11 run records maximum centre-line error `0.000002/0.000117`, blocked moves
  `47`, rendered body overlap `0px`, agent penetrations `0`, then `Working/Working` at
  `seat_player/seat_father`, seated centre error `0/0` and static/interaction/agent violations
  `0/0/0`.
- QA now renders a shoe-only skinned mesh from the actual foot/toe-weighted triangles and tests every
  rendered pixel. The strict vertical silhouette still reports actual outside-pixel frames `82/82`,
  minimum margins `-14.022/-16.705px`, and planted outside counts `19/60`. The candidate therefore
  does **not** pass the strict one-tile projected-contour requirement even though Father and Player
  now share the same visual floor-centre band.
- All 82 approach frames and 48 QA-only Father whole-body turn frames were visually inspected. The
  two legs, arms/hands and clothing skin remain intact; expected side-view leg crossing/occlusion is
  present without a third limb or tearing; posture, alternating weight transfer, full-body turn and
  loop remain continuous. Action 613, confirmed direction, own Avatar/skin and full pose strength are
  retained; no procedural gait or framewise teleport is used.
- Final raw run: `Artifacts/FatherFootCenterFixedFinal-20260902-105300/`. Portable GIFs, all-frame
  sheets, ratio sheet and exact receipts are under
  [Evidence/PlayerFather3DIndependentQaCurrent/README.md](Evidence/PlayerFather3DIndependentQaCurrent/README.md).
  Detailed analysis: [FATHER_V19_INDEPENDENT_SCALE_WALK_QA_2026-09-01.md](FATHER_V19_INDEPENDENT_SCALE_WALK_QA_2026-09-01.md).
- The accepted run used standalone `-batchmode -force-d3d11`, `CreateNoWindow=true` and continuously
  verified `MainWindowHandle==0`. One earlier build attempt exposed a nonzero handle and was aborted;
  it is not evidence. No production/default, Downloads copy or deployed executable changed. User GIF
  approval is still required before any PASS or eligibility change.

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
