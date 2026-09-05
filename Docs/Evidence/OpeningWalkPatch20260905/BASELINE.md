# Superseded baseline diagnostic (before the current fixes)

This is historical evidence, not the current acceptance state. See PROJECT_STATE.md first.

## 2026-09-05 continuous walking audit: current default is NOT visually accepted

- The user's follow-up asks for real walking and tile-centred feet. Added opt-in diagnostic
  `Family3DOpeningWalkAudit`, hidden runner `Tools/Invoke-FamilyCompanyOpeningWalkAudit.ps1`, and
  `Tools/analyse_opening_walk_audit.py`. No production gait, scale, mesh, Avatar, clip, path logic or
  default/candidate selection was changed by this audit. Prior opening/shop work below is preserved.
- One warm scripts-only build: PASS, total **19.894 s**, build **17.658 s**, cache `hit-scripts-only`,
  `Artifacts/FastQa/runs/20260905-102936-335/`. Initial Asset Pipeline Refresh **6.992 s**; no
  `Require frontend run ... dag couldn't be loaded` cache-miss line. Both following observations
  reuse this exact binary; comparison does not cause another build.
- Normal opening coordinator, four actors, no injected routes/teleports/fixed capture clock:
  - Default: `Artifacts/FastQa/WalkAudit-default-20260905-103049/`, **241** consecutive rendered frames,
    approximately 24 real seconds, runtime errors **0**, static/interaction/agent penetration **0/0/0**.
  - Existing comparison flag only: `Artifacts/FastQa/WalkAudit-candidate-20260905-103351/`, **243** frames,
    approximately 24 real seconds, errors **0**, penetration **0/0/0**. Hidden window checks 148/153,
    always `MainWindowHandle=0`. `CAPTURED` is capture success, NOT visual approval.
- Moving ankle midpoint projected to ground vs semantic movement root, 1280x720 pixels:

  | Actor (appearance) | Default median / max | Candidate median / max |
  | --- | ---: | ---: |
  | player (Player V8) | 8.37 / 11.54 | 2.18 / 6.38 |
  | older_sister (Player V8 stand-in) | 8.42 / 11.47 | 2.18 / 6.37 |
  | father (Father V19) | 5.65 / 8.22 | 1.43 / 4.44 |
  | mother (Father V19 stand-in) | 5.91 / 8.17 | 1.35 / 4.34 |

  Default fails the existing median <=4px / max <=8px foot-midpoint gate for all four. The comparison
  passes ONLY this gate; it also changes scale, stride/phase, clearance and colour, so it is not a
  safe isolated fix or an approved default. Feet alternate in both captures (default 38-44, comparison
  16-17 lead changes per actor); this alone does not prove planted-foot slip or grounding is correct.
- There is a separate root-path defect: default `older_sister` pauses near grid `(3.971646,4.522904)`
  and replans at frame 96. Frames 96-107 move directly toward `(5,5)`; at frame **100 / 10.839 s**,
  root `(4.328140,4.688296)` is **11.79px** from the nearest cardinal tile-centre line. This is not
  merely a foot-offset issue. `OfficeRuntimeAgent.RebuildPath` starts from NearestCell but initializes
  `_pathIndex=1` on a multi-node route; skipping that centre when resuming off-centre is the likely
  cause to test next. Do not claim all turns/replans stay centred because straight-line medians are near 0.
  The raw `pathErrorPx` measures retained path segments, which may omit the current-root joining leg;
  its 22px maxima are NOT independently proven lane errors. Analysis names this `retainedPathPolylinePx`
  and separately reports `nearestCardinalCellCentreLinePx`.
- Both runs contain measured-time `review/tile-centres-overview.mp4`, `four-actors-closeup.mp4`, CSV,
  `analysis.json` and 13 chronological all-frame sheets. All sheets were visually inspected in order.
  Yellow = actual cell boundary, cyan = movement root, pink = ground-projected ankle midpoint.
  Capture overhead yields about 10 rendered frames/s; MP4 30fps duplicates do not create extra motion
  evidence. This is not native 30/60fps smoothness approval, a pixel-sole slip test, or seated/work QA.
  Lowest-skin-Y samples (every six frames) are diagnostics, not a completed grounding acceptance gate.
- Next: fix/test the off-centre replan joining segment in isolation, then calibrate feet/stride/grounding
  under the locked body-size contract and obtain user visual approval. No automatic candidate promotion,
  release/Downloads deployment, save overwrite, commit or push occurred.
- User also asked whether startup patch downloads can reduce iteration time. Inspection found no
  Addressables dependency; current 3D loading uses Resources. Existing FAST_QA is already incremental.
  Proposed, NOT implemented: validated local development settings for frequent numeric adjustments,
  followed separately by a startup updater (version check, changed-file download, integrity verification,
  atomic activation, saves preserved). Content bundles can update assets; changed C# still requires a
  compiled player payload. No hosting account, network updater or new dependency was created.
  Reference: [Unity content update limits](https://docs.unity3d.com/Packages/com.unity.addressables@2.9/manual/content-update-builds-overview.html).
