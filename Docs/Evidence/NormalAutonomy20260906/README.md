# Background normal navigation / shop text evidence

Canonical workspace: `C:/Users/godho/Documents/Codex/fc_agents/integration_p0`, branch `main`.
Exact editor: Unity `6000.3.21f1 (c02631ffc030)`.
User constraint: background only; no desktop, mouse, keyboard, foreground game/editor or browser control.
No file in this evidence directory is an executable payload. Source push is not a game Release.

## Production changes

- `511321db`: count real reservation/zero-speed traffic waits for furnished routes, use reserved cardinal
  tile-centre retreat/replan, and allow a moving actor to yield around a stationary/manual/seated peer.
  No diagonal displacement, radius reduction, character scale change or pose injection.
- `30640539`: a seat claim no longer opens its own furniture to the route toward the approach tile.
  Both path planners and actual navigation endpoint/recovery collision use the same permission boundary.
  Atomic seating and egress retain their existing specific permission.
- Existing chair-centre/geometry/pose corrections are in
  [ChairTileCentre20260906](../ChairTileCentre20260906/README.md), not duplicated here.

## Evidence scopes

| Evidence | Result and limitation |
| --- | --- |
| `baseline-8346` | Independent CSV parser: Father 12.8896s and older-sister 13.8225s no-progress; FAIL. |
| `first-recovery-5113` | First normal-navigation sample PASS, but not all NPC work observed. A later repeat failed occupancy, so this is not final gameplay acceptance. |
| `failed-repeat-0dcb` | Occupancy assertion failed before next-day setup. All 166 payload files hash-fenced and recycled; source/saves/Library/Bee untouched. |
| `reserved-seat-routes` | Four rotations, 472 open-floor approach routes: 137 furniture crossings before, zero after. Uses real path service and independent segment collision checks with no seat permission. |
| `shop-text` | Production font/styles: 75 text rectangle checks across 720/900/1080/1440/2160 heights PASS. Not IMGUI pixels, native pointer purchase or whole-game UI approval. |
| `candidate-3064` | Build PASS only. Normal movement and departure had zero occupancy violations, then strict 09:04 presence failed for Father. That executable was recycled, not promoted. |
| `final-c264` | Full 229.54-second hidden process, 867 no-window checks. Independent navigation PASS: 6,964 rows, max no-progress 1.0169s, rail error 0.000031125, zero collision/runtime errors. All four had Working samples across the run. Strict attendance gate FAIL, exit 1; all 166 payload files recycled. |

Final exact source is `c2644f618994968f4fd0b906491078a8231a97cb`; build took 21.630s total / 19.549s.
The immediately preceding diagnostic-only 6bc25275 build failed CS0136; c2644f61 fixes that local variable
name. This compile failure and the actual attendance failure are separate, neither was shipped.

Actual next-day release/seat observations:

| Actor | Due | Visible release | At 09:20 |
| --- | --- | --- | --- |
| Player | 09:00 | 09:00 | Working |
| Older sister stand-in | 09:01 | 09:03 | Working |
| Father | 09:02 | 09:07 | Navigating |
| Mother stand-in | 09:03 | 09:11 | Navigating |

`next-day-normal-seated.png` is the historical capture filename, **not a claim that all four are seated**;
the frame visibly contains two workers and two walkers. `normal-autonomy-end.png` and four shop previews
are engine camera-stack renders, not native input or IMGUI screenshots.
Chair/stem centre error is at most 0.484345 screen pixels (pixel snapping); monitor/keyboard lateral-axis
errors are zero in all four directions. The raw `chair-geometry.csv` also retains three Working-transition
hand-midpoint outliers: Father 0.647744 at 140.0686s, Father 0.395937 at 188.2296s, Player 0.536368 at
212.6886s. These are not discarded. Continuous pose/foot-slip approval remains pending even though later
steady-state samples return near the keyboard. Working state counts alone cannot certify full work animation.

The CSV parser excludes the actual paused shop/rebuild boundary, using `finalTime` plus `ready` and
`shopOpen` flags where present. Applying the same exclusion to red and green keeps the baseline red;
raw CSV files are not rewritten. A paused cached Navigating label does not count as live deadlock.
The two raw Unity failure stack traces retain their original trailing spaces; only those two exact
evidence files are excluded from the source/document whitespace check.

Manual Player behavior is unchanged: founding-day furniture placement hands control back to the user.
Without user input, demanding four simultaneous autonomous Working states would be an invalid oracle.
Normal NPC routines also include unavailable amenities; no intention weights were changed to force work.
During one run, a stationary Player blocked an older-sister egress candidate; she exited after Player's
normal departure. This dynamic-clearance wait is separate from the fixed path reservation deadlock.

Next-day observation keeps the strict 09:00/09:01/09:02/09:03 release and 09:04 presence checks explicit.
The current entrance serializes the entire 2.5-cell ingress segment, so later appearances can be delayed.
The extended observer collects through 09:20 even after timing failures, then still exits 1 and records
`nextDayAttendanceGatePassed=False`. Later seated frames do not turn a failed timing gate into PASS.
Only unobserved afternoon/night clock jumps are used; no actor routes, poses or controls are injected.

`editor-validations.txt` contains final exact-source PASS lines for the 472 routes, 128-seed / 1,152-path /
53,108-segment navigation regression and atomic docking's 20 negative fixtures. These static/regression
checks do not override the real Player attendance failure or replace normal continuous animation evidence.

## Reproduce without touching the user's screen

From the canonical workspace:

```powershell
.\FAST_QA_WINDOWS.cmd -Profile player-scripts -NoPlayerSmoke
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Invoke-FamilyCompanyNormalAutonomyQa.ps1 -EvidenceDirectory '<new absolute Artifacts/NormalAutonomy subdirectory>' -NextDay
```

The runner launches only hidden `-batchmode -force-d3d11`, checks its owned process for any window,
logs actual normal gameplay, and leaves no save changes. `-AnalyzeOnly` reads existing raw CSV evidence;
its navigation result is separate from the attendance gate and `productionEligible` remains false.

Editor entrypoints are `FamilyCompany.Editor.OfficeGrid.OfficeReservedSeatRouteValidation.RunBatch`,
`FamilyCompany.Editor.OfficeGrid.OfficeShopTextLayoutValidation.RunBatch` and
`FamilyCompany.Editor.OfficeNavigationValidation.RunBatch`. Launch the exact editor through a hidden
`ProcessStartInfo` with `-batchmode -nographics`; do not open a visible Editor.

If any mandatory gate fails, use the exact identity-fenced `Tools/Retire-FamilyCompanyFailedFastQa.ps1`
before another Player build. It preserves non-executable evidence, verifies hashes and a non-running
exact cache root, sends only that payload to Recycle Bin, and verifies siblings plus zero remaining files.
The wrong-source identity guard was tested: rejection occurred before mutation, with live candidate intact.

## Patch and release boundary

The previous real Unity local patch/restart receipt remains at
[ChairTileCentre20260906/patch](../ChairTileCentre20260906/patch/).
Its executable fixture was recycled after normal gameplay regressions were found; the scoped functional
download/restart evidence remains valid. The new inert updater regression again passed all 51 checks.
No public GitHub game Release, Downloads upgrade or PC shutdown was performed. Do not tell the user
their existing main EXE has already received this source patch. Native shop/UI acceptance, remaining
normal-gameplay gates and visual approval are still required before publishing a playable Release.
