# Windows Fast QA

`FAST_QA_WINDOWS.cmd` is the short feedback loop for ordinary changes. It is deliberately separate from
`BUILD_WINDOWS.cmd`: Fast QA writes only to `Artifacts/FastQa`, while the release command owns
`Builds/Windows/FamilyCompany_Playtest` and the deployed Downloads folder.

## Quick start

```bat
FAST_QA_WINDOWS.cmd
FAST_QA_WINDOWS.cmd -Profile simulation-pure -Repeat 3
FAST_QA_WINDOWS.cmd -Profile editor-validation
FAST_QA_WINDOWS.cmd -Profile d3d-capture
FAST_QA_WINDOWS.cmd -Profile d3d-capture -PrebuiltPlayer D:\qa\FamilyCompany.exe
FAST_QA_WINDOWS.cmd -Profile player-startup
FAST_QA_WINDOWS.cmd -BaseRef origin/main
```

The default `-Profile auto` compares the worktree with `HEAD` plus untracked files. Use `-BaseRef` when
validating a committed candidate. A successful functional test can still print `slo=MISS`; exceeding the
target is performance evidence, not a false functional failure.

Editor discovery precedence is `-UnityEditor`, `UNITY_EDITOR`, `FAMILY_COMPANY_UNITY_EDITOR`, an ancestor
`UnityEditors/<ProjectVersion>/Editor/Unity.exe`, then the standard Unity Hub directory. No personal absolute
editor path is stored in the repository.

## Selection and safety

The file-to-gate manifest is `Tools/FastQa/fast-qa-manifest.json`.

| Change | Selected path |
| --- | --- |
| Simulation `.cs` only | external Roslyn compile plus deterministic pure/stamina harness; Unity is not started |
| Editor validation `.cs` | one hidden warm Unity startup and the mapped methods |
| Runtime `.cs`, unchanged serialization layout | scripts-only player build when a compatible player cache exists, then D3D11 smoke/capture |
| Asset, prefab, scene or UI content | normal incremental data build plus D3D11 capture |
| asmdef/asmref, packages, ProjectSettings or serialized layout | broader Editor gates plus forced clean data-build fallback |
| Unknown file | broader Editor suite; never a fail-open scripts-only pass |

The scripts-only cache is rejected when Unity version, target, packages, enabled scenes/build settings,
PlayerSettings, assembly definitions, or the runtime serialization-layout signature changes. A cache miss
seeds a normal Fast QA player; an existing but incompatible cache is replaced with a forced clean data build.
Four immutable base-data files are SHA-256 checked before every scripts-only build, so a present but damaged
player directory also falls back cleanly. This is stricter than checking only the Git SHA or EXE existence.

Unity documents two important constraints:

- [`BuildOptions.BuildScriptsOnly`](https://docs.unity3d.com/jp/current/ScriptReference/BuildOptions.BuildScriptsOnly.html)
  needs an existing full build at the same output and skips player-data rebuilding.
- [Scripts-only build manual](https://docs.unity3d.com/ja/current/Manual/build-scripts-only.html) warns that a
  serialization-layout change, such as adding a `MonoBehaviour` field, requires a regular or clean build.

The runner therefore treats possible serialized-field/type changes as a clean data-build fallback. It does not
claim that `BuildScriptsOnly` proves asset compatibility.

## Cache, lock and cleanup

- Keep the detached QA worktree path stable. Its `Library`, `Library/Bee`, and Fast QA player are warm caches.
  This is the single highest-impact rule: a new worktree pays an 80-104 s initial asset import before any
  profile can hit its target. See [ITERATION_LOOP.md](ITERATION_LOOP.md) for the measured breakdown.
- Never delete `Library`/`Temp` between ordinary runs.
- A project-local exclusive lock prevents two Fast QA commands from sharing a project path. An existing
  `Temp/UnityLockfile` fails closed; it is never deleted.
- Unity/player windows are hidden. On failure, timeout, or interruption, cleanup targets only the PID tree
  started by the runner. Other Unity sessions are not killed or globally blocked.
- JSON and text results are written under `Artifacts/FastQa/runs/<timestamp>/result.json` and `summary.txt`.
  Stage fields include import, script compilation, domain reload, Editor method, player build and capture.

## Profiles and SLOs

The runner's `ValidateSet` is the authority for accepted names:
`auto`, `diagnose`, `simulation-pure`, `editor-validation`, `editor-broad`, `player-scripts`,
`player-startup`, `asset-capture`, `full-fallback`, `d3d-capture`, `clean-build`.

| Profile | Warm target | Notes |
| --- | ---: | --- |
| `auto` | selected profile's target | default; classifies the diff and picks the cheapest safe path |
| `simulation-pure` | 15 s | no Unity/license/import |
| `player-startup` | 15 s | existing Fast QA player launch probe only |
| `d3d-capture` | 30 s | reuses the prebuilt player |
| `editor-validation` | 45 s | persistent Library required |
| `player-scripts` | 60 s | compatible prebuilt Fast QA player required |
| `diagnose` | 60 s | stage/cache diagnostics when a run is unexpectedly slow |
| `editor-broad` | no target | the manifest's 10 central gates |
| `asset-capture` | measured, no 60 s promise | import/data build/capture required |
| `full-fallback` | no target | broad Editor suite plus forced clean data build |
| `clean-build` | no 60 s promise | measurement/diagnostics only; never deployment |

Cold import and clean release are reported separately from warm Fast QA statistics. Final release still uses
`BUILD_WINDOWS.cmd`, full validation, the canonical main SHA, and the release deployment process.

## Measurement record

The exact three-run cold/warm table, stage breakdown, exclusions, and artifact run IDs are recorded in
`Docs/History/FAST_QA_WINDOWS_PIPELINE_2026-08-14.md`. Machine results live in ignored
`Artifacts/FastQa/runs`; cold import has a separate `Artifacts/FastQa/cold-import-summary.json`.

An actual changed PNG/prefab/scene is not manufactured for a faster-looking benchmark. Such changes always
take `asset-capture`; their real import time is recorded on the run that changes the content.
