# Claude handoff — Father V18 Higgsfield movement is still unresolved

Date: 2026-08-25 (Asia/Seoul)
Repository: `C:\Users\godho\Documents\Codex\fc_agents\integration_p0`
Branch/base before this handoff: `main` / `340e45a651561709bb1a717c668b077582daa0f7`
Current status: `USER_VISUAL_REJECTED_MOVEMENT_NOT_PROPERLY_AUDITED`
Production: `productionMutation=false`, `productionEligible=false`

> **Superseded in two places — read
> [FATHER_V18_MOVEMENT_ROOT_CAUSE_2026-08-26.md](FATHER_V18_MOVEMENT_ROOT_CAUSE_2026-08-26.md) first.**
> Measurement of the existing V22 telemetry on 2026-08-26 inverted the cause ranking below and found
> `Recommended next sequence` step 5 to point the wrong way. Cause 6 (`poseStrength = 0.45`) is the
> primary defect; cause 1 (cadence) is secondary. Everything else in this document still stands.
>
> **Causes 6 and 7 were fixed on 2026-08-26 (`0be347b8`, `26425e9e`), along with a defect neither
> document had caught: both paid 4096×4096 albedos were importing at `maxTextureSize 2048` with lossy
> `Compressed`, and now measure `4096x4096 BC7`.** Causes 2, 3, 4 and 5 are still open, movement
> quality is still unverified, and `productionEligible=false` still stands.

## What the user actually wants

Father V18 must move in the real game with convincing temporal motion comparable to
`C:\Users\godho\Downloads\캐릭.mp4`. Different leg poses in still frames are not enough. The character
must stay at the correct Starter Office map scale. The user rejected V22 because the movement itself
was not watched or matched properly.

Do not call V22 approved. Route completion, foot-lead sign changes, static contact sheets, or a 10 fps
replay made from intermittent screenshots do not prove natural movement.

## Non-negotiable constraints

- Never use the 9-credit standard-quality Higgsfield option.
- Do not submit another paid job without explicit user authorization and a fresh cost preflight.
- Known Higgsfield balance after the existing jobs: 76 credits.
- Keep `productionEligible=false` until the user visually approves the result.
- Do not modify the production scene/default build; use isolated QA scenes and builds.
- Preserve complete skinned-body and valid Humanoid Avatar gates.
- Do not discard the preserved stash `codex-pre-pull-340e45a6-2026-08-25`, commit
  `9d367c74ed337b05bdfd1ab0ce35c9bbf734681f` (2026-08-25 20:17:20 +0900), currently `stash@{0}`.
  Resolve it by that commit SHA, not by the `stash@{N}` index: any new stash shifts every index.

## Source video audit

- File: `C:\Users\godho\Downloads\캐릭.mp4`
- SHA-256: `39DB58386FC8FFF7CF6D173A5552538C6D01F64959AEDC60495A8DC3E263843E`
- Duration/resolution/rate: 639.418 seconds, 1280×720, 30 fps
- Detailed ranges, re-measured by shot-boundary detection on 2026-08-25. Earlier drafts of this
  document listed `304–368` and `368–384`; both were wrong at the edges and are corrected here:
  - Higgsfield file listing and import: 286–294 seconds
  - Blender viewport verification of the imported GLB: 294–298 seconds
  - key art / trailer menu stills: 298–305.93 seconds
  - **in-game movement footage: 305.93–354.90 seconds** — the only usable locomotion reference
  - Day-4 3D showroom, a turntable in the creator's own asset viewer: 354.90 seconds onward.
    This is not Blender and not in-game movement. The old `304–368` range ran 13 seconds into it, so
    anyone who trusted that range could calibrate cadence against a character rotating in place.
- Exact Father actions already generated:
  - action 0: `Idle`
  - action 644: `Lean_Forward_Sprint_inplace`

The video demonstrates continuous temporal movement and a multi-state character: autonomous
navigation, stopping, turning, attacks, roll/dodge-like motion, and transitions. The current Father QA
only forces an eight-direction square route with idle/run and instant discrete yaw.

## Paid generation already completed

| Purpose | Job ID | Cost | Output SHA-256 |
|---|---|---:|---|
| detailed Tripo H3.1 source | `7373376e-2b09-4b1a-ba1c-15847c6d626f` | 18 | `90789F4F3C0411DA526B8FD8EEBA734E8E77062AA9793EE0D8142160B92E8DFD` |
| idle-0 | `f63f18c3-f5f9-4d6c-9e1a-4f762d20a25f` | 8 | `179A9F9B60FF2E8829A3E9D0E60A0D03349235F8830163104ACC6FAB300A42AC` |
| run-644 | `83c62f11-ea31-4888-9844-0c8f3a8970f3` | 8 | `AA8BF9A8043A6A4E26F141AA1D8F581B9C81BF854518A999D8496CD3446FC01F` |

No additional credits were used in the V19–V22 Unity experiments. Local raw generated outputs and
receipts are preserved under the projectless output directories
`father-v18-higgsfield-motion-v19/` and `father-v18-higgsfield-v18/`.

Do not read the table as 92 − 34. `generation-receipt.json` covers only the two 8-credit animation jobs
(`balance_before` 92 → `balance_after` 76); the 18-credit Tripo source job was already paid for before
that receipt was written. 76 credits is the current balance.

## Asset facts

Both animated GLBs contain one 24-bone armature, one fully weighted 28,924-vertex main mesh, 48,733
main polygons, one action, and one 4096 texture. They share topology, UVs, and texture, but independent
generation produced different rest geometry, skin weights, and bind skeletons. Blindly retargeting one
job's animation onto the other job's body is unsafe.

Current Unity motion source assets are under
`Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/FatherV18HiggsfieldMotionV19/`:

- idle FBX: `3DB1D5414174F07F9395CE030047D98860A27F448C099AE757AD72E2922CB47B`
- run FBX: `8CD7A186BB3600FC92F27F29E7CBF9941516211C3E71D85DBC1DC1D428A1924E`
- albedo: `F9A09FE0B1901125A00C8D0C87FF585E1D86E2A927949672BC44C68776DCCE7C`

The directory retains the V19 generation name because V22 reuses those paid source files; it is not a
V19 runtime approval.

## What was tried and rejected

### V18 — static root translation

An unrigged detailed model moved by root translation/yaw only. It skied because no limbs animated.

### V19 — idle body + run motion retarget

The run motion from an independently generated body was retargeted onto the idle body. The user
rejected stretched legs and washed colour:
`USER_VISUAL_REJECTED_STRETCHED_LEGS_WASHED_COLOR`.

### V20 — native run body at full strength

Moving mesh/Avatar/skin/clip came from the same run FBX. Cross-job skin mismatch was removed, but the
644 sprint still produced exaggerated split-kick poses on Father's longer legs.

### V21 — pose-strength A/B

D3D11 comparisons at 0.45, 0.62, and 0.78 found 0.45 had the smallest split-kick silhouette. This was
only a spatial still-pose comparison, not temporal movement approval.

### V22 — native run body + 0.45 + exact albedo

- moving body/Avatar/skin/clip: native run-644 FBX
- idle motion: idle-0 while stationary
- pose strength: 0.45
- rendering: exact sRGB albedo through `Unlit/Texture`
- root motion: disabled; actual Father `OfficeRuntimeAgent` owns map translation
- scale: one locked projected-height calibration to the live Father sprite

V22 completed two map circuits, but the user rejected the process and result because real movement
timing was not watched. V22 is diagnostic evidence only.

## Highest-priority unresolved movement causes

1. **The 0.6-second sprint is stretched to 0.99380799 seconds — a 1.6563466× slowdown.**
   `Family3DWalkActor.LockedCycleSeconds` is `0.99380799f` (`Family3DWalkActor.cs:14`); the imported run
   clip is `0.6000000238` seconds. Both values are recorded together in the V22 build receipt as
   `walkClipLength` and `lockedCycleSeconds`, so the mismatch is measured, not inferred. The legs
   therefore cycle at 0.6037× authored cadence while translation stays independent. ~~Start here.~~
   **Demoted on 2026-08-26: real but secondary.** Measurement showed the stride-matched cycle at full
   pose strength is 1.15–1.33 s, i.e. *longer* than the current 0.99380799 s, so this is not where the
   slip comes from. See the root-cause document.
2. **No stride-to-speed calibration.**
   Imported root motion is discarded and no measurement matches planted-foot travel to map distance.
3. **The prior 10 fps comparison was not a real-time recording.**
   QA captured every sixth moving render frame and replayed sparse screenshots. Capture continuous
   30/60 fps output or timestamped frames using exact simulation time.
4. **Yaw snaps to eight discrete directions.**
   There is no angular blend or turn animation.
5. **Only idle/run exist.**
   The source video's quality also comes from start/stop, turn, attack, dodge/roll, hit, jump, and death
   transitions.
6. **Pose strength 0.45 may hide motion.**
   It was chosen from silhouettes and may worsen stride mismatch. Re-evaluate only after measuring
   real-time cadence and speed.
   **Promoted on 2026-08-26 to the primary defect — confirmed, not suspected.**
   `ApplyPoseStrength()` slerps every bone 45 % of the way from rest to the animated pose, so the leg
   swing that produces the stride is cut with it. The legs deliver 0.29–0.34 body heights per cycle
   while the body travels 0.56, so the body outruns the feet by 1.66×–1.92×. ~~Fix this first.~~
   **Fixed 2026-08-26: `ResolveFatherMotionPoseStrength()` returns `1f`. Stride not yet re-measured.**
7. **Foot planting never runs on this code path — added 2026-08-26.**
   `Family3DWalkActor.cs:293` gates `ApplyNaturalSdFootPlants` behind `dedicatedNaturalSdWalk`, which is
   `false` for Father V18. The contact latch and the `SolveTwoBonePlant` world-space plant never
   execute, so there is no ground constraint at all, and `leftFootPlanted`/`rightFootPlanted` are
   `false` in 180 of 180 samples. Any foot-contact evidence from V22 is void.
   **Fixed 2026-08-26: the gate is gone and the solver, now `ApplyFootPlants`, runs on both branches.
   Whether its contact window suits a sprint clip is unverified until a real capture exists.**

## Current code touchpoints

- `Assets/FamilyCompany/Experimental/Family3DPrototype/Runtime/Family3DWalkActor.cs`
  - manual PlayableGraph, idle/run blend, locked cycle, imported-root reset
- `Assets/FamilyCompany/Experimental/Family3DPrototype/Runtime/Family3DStarterOfficeCandidateQa.cs`
  - real Father binding, gait-phase clock, discrete yaw, projected scale, frame capture
- `Assets/FamilyCompany/Experimental/Family3DPrototype/Editor/Family3DStarterOfficeCandidateQaBuilder.cs`
  - isolated scene/player build, V22 path, native run model selection
- `Assets/FamilyCompany/Experimental/Family3DPrototype/Editor/Family3DPrototypeModelImporter.cs`
  - explicit 24-bone Humanoid mapping and loop/root-lock import settings
- `Tools/Blender/prepare_father_v18_higgsfield_motion_unity.py`
  - GLB validation and Unity FBX export

Current isolated scene:
`Assets/FamilyCompany/Experimental/Family3DPrototype/Scenes/Family3DFatherV18HiggsfieldNativeRunMapQaV22.unity`.

The V22 build/runtime evidence remains locally under
`Artifacts/Family3DStarterOfficeCandidateQaV1/FatherV18HiggsfieldNativeRunMapBuildV22/` and
`FatherV18HiggsfieldNativeRunMapRuntimeV22R1/`. Older V18–V21 builds, runtime captures, QA scenes,
logs, duplicate output presentations, and temporary audit sheets were removed after this handoff was
written. Paid/raw source assets and receipts were preserved.

## Recommended next sequence

1. Inspect the five code touchpoints before editing.
2. Extract one uninterrupted run/turn/stop source segment at native 30 fps. Use **305.93–318.33
   seconds**: it is the longest clean shot that actually contains locomotion — walk, roll/dodge, the
   three-hit attack combo, and a jump — with no burned-in title card. 12.40 seconds, about 372 frames,
   roughly 20 authored 0.6-second run cycles.

   Do not extract across a shot boundary. Within the in-game band the cuts are at 318.33, 321.77,
   323.07, 326.70, 337.20, 349.73, and 354.90 seconds, which leaves only two windows longer than five
   seconds: 305.93–318.33 (12.40 s) and 337.20–349.73 (12.53 s). The second is marginally longer but is
   jump-debugging footage and carries an overlay title card near its end, so prefer the first.
3. Record Unity continuously in real time.
4. Put source cadence, Unity cadence, root speed, ankle world velocity, and foot-contact duration on one
   time axis.
5. ~~First test native 0.6-second cadence and calculate stride-matched translation~~ — **wrong
   direction, corrected 2026-08-26.** Forcing the native 0.6 s cadence requires the Father to walk at
   1.63 u/s, about 1.9× his current office speed. Instead: restore `poseStrength` to 1.0, give this path
   a ground constraint, replace the capture, and only then solve cycle time from measured stride and
   measured speed (expected 1.15–1.33 s). Full order in
   [FATHER_V18_MOVEMENT_ROOT_CAUSE_2026-08-26.md](FATHER_V18_MOVEMENT_ROOT_CAUSE_2026-08-26.md).
   Still true: do not select another pose-strength value from stills.
6. Add turn/start/stop blending only after forward locomotion no longer skates.
7. Show the user a direct source-vs-game movement comparison before claiming a pass.

## Verification already passed — not visual approval

- Unity `6000.3.21f1` isolated Windows build: PASS
- D3D11 actual Starter Office route execution: PASS
- repository `FAST_QA_WINDOWS.cmd -Profile editor-broad`: PASS
- human-authored C#/MD/PY/JSON `git diff --check`: PASS; generated Unity scene/meta retain the
  serializer's blank-value whitespace
- production scene, preview scene, and `EditorBuildSettings.asset` before/after hashes unchanged

These checks prove isolation and execution only. They do not prove movement quality.

## Handoff facts re-verified against the repository — 2026-08-25

Every factual claim above was re-checked against the working tree before this document was pushed.
Every hash, credit figure, and code value matched. The two source-video sub-ranges did not, and were
corrected in place — see `Source video audit`.

| Claim | Verified against | Result |
|---|---|---|
| source video SHA-256, 639.418 s, 1280×720, 30 fps | `sha256sum`, `ffprobe` | match |
| game-movement range `304–368 s` | shot-boundary detection | **corrected to 305.93–354.90 s** |
| `Blender motion previews: 368–384 s` | frame inspection | **corrected: that band is the 3D showroom** |
| `LockedCycleSeconds = 0.99380799f` | `Family3DWalkActor.cs:14` | match |
| run clip `0.6000000238` s, idle clip `3.2333336` s | V22 `build-receipt.json` | match |
| idle/run FBX + albedo SHA-256 (3 hashes) | files under `Candidates/FatherV18HiggsfieldMotionV19/` | match |
| job IDs, 8+8 credits, balance 92 → 76, `productionEligible=false` | `generation-receipt.json` | match |
| GLB output SHA-256 (idle, run) | preserved raw outputs | match |
| `productionMutation=false`; production/preview scene and `EditorBuildSettings` hashes unchanged | V22 `build-receipt.json` before/after fields | match |
| five code touchpoints + V22 scene present | working tree | all present |
| preserved stash intact | `git stash list` | present as `stash@{0}` |
| V22 build/runtime evidence retained, V18–V21 removed | `Artifacts/Family3DStarterOfficeCandidateQaV1/` | only V22 remains |
| human-authored C#/MD/PY/JSON whitespace clean | scoped `git diff --check` | PASS; Unity-generated YAML excluded |

Repository state at verification: `main` level with `origin/main` after `git fetch` (0 ahead, 0 behind),
base `340e45a6`. `Artifacts/` is covered by `.gitignore:1`, so the 1.04 GB V22 player build is local
evidence only and is not part of any commit.

Two facts a future agent should know about the committed handoff: the 180 files in
`FatherV18HiggsfieldNativeRunMapRuntimeV22R1/frames/` are the sparse every-sixth-frame capture that
unresolved cause 3 rejects, not a real-time recording; and the handoff commit adds 21 new files plus
7 modified files, totalling 32.0 MB, of which the 10.7 MB motion albedo PNG is the repository's second-largest
blob after the existing 16.0 MB `ThirdParty/StylooChibi/allinone.fbx`. That is within existing practice
for this repository, which commits binaries directly and has no Git LFS tracking rules.

`Candidates/FatherV18HiggsfieldStatic/` is intentionally committed alongside the code even though V18 static
was rejected: `Family3DStarterOfficeCandidateQaBuilder.cs`, `Family3DStarterOfficeCandidateQa.cs`, and
`Tools/Blender/prepare_father_v18_higgsfield_static_unity.py` all still reference it. Omitting those assets
would leave a broken asset reference.
