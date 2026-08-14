# FAST_QA_WINDOWS pipeline measurement — 2026-08-14

Audited measurement base: `4b51e2c1a818b4ce1ea841571e2d6b6c349e4b3a`

Latest integration base observed at cutoff: `279027d935362f2d111a84fde39b3fc22bb67de2`

Unity: `6000.3.21f1`

Target: `StandaloneWindows64`, D3D11

Fast output: `Artifacts/FastQa` (never release/deploy output)

## Audited baseline

The prior `BUILD_WINDOWS.cmd` always ran `WindowsPlayerBuild.BuildWindowsX64`, whose pre-build path invokes
asset/catalog builders plus broad validators and `BuildOptions.None`. Its watcher waits for every Unity
process globally and creates a fresh staging folder. It did retain the canonical Library/Bee, so the latest
three historical successful incremental automation logs were 17.218, 19.221, and 18.881 seconds. Those were
warm incremental release-config builds, not cold imports or forced clean builds.

No single complete Editor-validation command existed. The new `editor-broad` measurement is the manifest's
10 central gates; it does not claim to execute every builder/menu item in `Assets/FamilyCompany/Editor`.

## Candidate measurements

Each cell is one process/scenario, in seconds. Repeated-profile SLO is evaluated per iteration (maximum), not
against the three-run aggregate.

| Scenario | Run 1 | Run 2 | Run 3 | Result / evidence run |
| --- | ---: | ---: | ---: | --- |
| Cold Editor import + compile + one validation | 93.223 | 88.333 | 89.684 | PASS, `cold-import-summary.json` |
| Pure Simulation Roslyn + deterministic harness | 0.81 | 0.74 | 0.73 | PASS, `20260814-191408-369` |
| Warm one Editor validation | 8.47 | 8.91 | 8.79 | PASS, `20260814-191714-126` |
| Warm broader 10-gate Editor suite | 13.22 | 10.00 | 10.15 | PASS, `20260814-191948-131` |
| Existing Fast QA player startup probe | 4.19 | 4.16 | 4.14 | PASS, `20260814-190923-852` |
| Normal incremental player build, no smoke | 6.93 | 6.94 | 7.00 | PASS, `20260814-191759-485` |
| Scripts-only build + D3D11 smoke/capture, including base-data hash gate | 22.65 | 15.14 | 15.02 | PASS, cache hit, `20260814-193526-493` |
| Existing candidate D3D11 1920x1080 scenario | 8.25 | 8.18 | 8.19 | PASS, `20260814-191119-905` |
| Forced clean release-config player build | 16.00 | 19.58 | 19.44 | PASS, `20260814-191837-033` |

The cold import stage itself was 85.005, 80.421, and 81.487 seconds; license startup was 2.68, 2.71, and
2.77 seconds. Cold import therefore honestly misses 60 seconds. An earlier concurrent-Unity attempt spent
repeated 60-second licensing IPC timeouts and failed after 755.018 seconds; it is excluded from the valid
cold samples and retained as environmental failure evidence (`20260814-182938-098`).

The final root-CMD auto run detected the new runtime QA type as a possible serialization-layout change,
refused scripts-only, ran broader validation + data build + D3D11 capture, and passed in 26.499 seconds
(`20260814-191528-166`). A deliberately corrupted player-cache fingerprint produced
`mismatch-clean-seed`, forced `BuildOptions.CleanBuildCache`, and rebuilt data successfully
(`20260814-192956-258`). A one-byte corruption of cached `globalgamemanagers` was also detected by the
base-data SHA-256 manifest and cleanly rebuilt (`20260814-193419-860`); stale or damaged cache never produced
a scripts-only PASS.

An intentional Editor timeout-0 fixture terminated only the runner-owned Unity PID tree and left zero new
Unity processes and no project `Temp/UnityLockfile` (`20260814-193657-445`). Exclusive lock contention also
failed before process startup. No unrelated Unity process was stopped.

An actual changed PNG/prefab/scene payload was not introduced merely for benchmarking. Therefore the table's
normal incremental row is pipeline overhead with unchanged content, not an invented asset-import target.
Real content changes use `asset-capture`, preserve the measured duration, and report their own actual import.

## Decision from measurement

- Keep the existing asmdef boundaries. Warm compile/validation is already under 14 seconds; splitting
  assemblies without a measured need would add dependency and serialization risk.
- Do not add a persistent Editor daemon yet. Hidden process startup plus one validation is under 9 seconds in
  the stable-license warm path, so daemon lifecycle/license/lock complexity has no current payoff.
- Do not use no-domain-reload claims for batch startup. No such option was applied or credited.
- Keep cold import and final release/deploy outside the warm SLO. Final release remains the central main
  integrator's responsibility.
