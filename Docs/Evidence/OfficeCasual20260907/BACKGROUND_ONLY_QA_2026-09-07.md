# Background-only continuation — 2026-09-07

The user requested continued work without interfering with their other applications. This continuation
did not start Unity or a Player, use a desktop/input API, control a browser, or modify Downloads, user
patch cache or saves. No commit/push/publication occurred during that test phase. The user subsequently
requested source commit/push; this evidence accompanies that source handoff, not a public game patch.
The candidate is based on `b07e24d1` in canonical `fc_agents/integration_p0`.

## Checks actually performed

| Check | Result | Scope |
| --- | --- | --- |
| Offline Presentation C# compile | PASS, 146 source files | Uses warm compiler response/dependencies; no Unity import or Player |
| Pure Simulation/save smoke | PASS, 11.272 s | Compiler + managed harness, no Unity/Player |
| Overall patch display model | PASS, 18 assertions | Stage/file transitions, lower counts, completion gate, retry |
| Shared desktop-launch policy | PASS, 19 assertions in PS7 and PS5.1 | Policy methods and denied script entry points only; no desktop created |
| FastQA default graphics denial | Expected exit 1 / FAIL | Correctly denied D3D capture before compilation/build/Player; NOT a graphics PASS |
| PowerShell syntax | PASS, 11 changed/new scripts | Parser only |
| Final process inventory | Zero Unity / FamilyCompany processes | Read-only, user applications not terminated |

Local artifact paths (ignored and not available merely by pulling Git):

- `Artifacts/FastQa/OfflineUiCompile/20260907-201223-976`: DLL, reference DLL, PDB, response file,
  empty error log, source hashes and scope in `result.json`.
- `Artifacts/FastQa/runs/20260907-201250-632`: pure smoke result; compile 2.828 s, build/player/capture 0.
- `Artifacts/FastQa/runs/20260907-201637-129`: deliberate graphics denial; all execution timings 0.

The offline compiler used BelowNormal priority, serial compilation and at most two processor cores.
The pure smoke shell and its child processes used BelowNormal priority and two processor cores.
No graphical or audio runtime was started to obtain these results.

## Source changes and safety boundary

- Retained the latest Canvas-null guards and deactivate-before-detach fix for the observed
  `CasualResponsiveGrid.LateUpdate` failure. Added safe column clamping/constraint updates and
  viewport width updates when the Canvas scale changes without a height change.
- `CompanyQaDesktop` now checks launch policy before any Win32 operation. Headless batch/no-graphics
  arguments can continue; graphics require explicit authorization. Known stock/native-pointer QA
  flags are blocked independently. This is a workflow guard, not an OS security sandbox.
- Seven graphics wrapper scripts reject default invocations before creating test fixtures. FastQA
  rejects selected graphical profiles before any build and rechecks Unity/Player launch arguments.
- `-AllowGraphicsQa` is not currently authorized. Do not supply it, switch to another launcher, or
  reinterpret `Hidden`, `-batchmode` with D3D11, or private desktop as headless permission.

## Safe next steps

While the user is working, source review and the following screen-free checks remain available:

```powershell
& ./Tools/Background/Test-CompanyQaLaunchPolicy.ps1
& ./Tools/Updater/Test-FamilyCompanyDisplayProgress.ps1
& ./Tools/FastQa/Compile-CasualUiWithoutUnity.ps1
```

The last command verifies the warm source list still matches current source before compiling and
writes only under Artifacts. Missing/stale references fail; it never launches Unity as fallback.

Actual lifecycle/layout revalidation, all current-resolution menu/adapter checks, final Release,
public patch delivery, and the approved one-time fixed-entry bootstrap update remain outstanding.
Old captures and pure compile PASS must not be substituted for those gates.
