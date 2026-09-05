# Main executable patch loading — 2026-09-06

**SUPERSEDED / USER-REJECTED external window. Do not ship or restore this GUI.**
The user explicitly requested patch loading INSIDE the Unity game instead.
See `../InGamePatch20260906/`. The underlying byte measurements remain historical evidence only.

- `updater-regressions.json`: 48 Windows PowerShell 5.1 inert-fixture checks, all passed.
- `measured-progress.json`: external arithmetic check of 135 real paced-stream download events,
  4,195,684 packed bytes, monotonic exact one-decimal floor percentage, separate verification and completion.
- `local-stream-worker.txt`: measured GUI worker events. Not a timer-generated display or a game download.
- Native window was actually inspected through computer-use: `21.8%`, `0.88 / 4.00 MiB`, file name,
  progress bar and cancel button were visible. First-release-unavailable UI was also inspected.
- The native test exposed a worker-only Get-FileHash failure missed by shell-only tests. The tested final
  worker uses .NET SHA-256; launcher compilation explicitly targets x64. No Unity rebuild was performed.

The native bootstrap is generated from `Tools/Updater/FamilyCompany.Bootstrap.cs` and embeds the production
launcher/update workers. Test compilation substitutes **only** the inert test worker and never feeds the
publisher. No EXE/DLL/ZIP is included in this evidence folder.

Still required: clean approved source inputs, independent gameplay release gates, a non-Development Unity
6000.3.21f1 Release identity, GitHub publication, real first install/delta/failure recovery through the main
EXE. Downloads and saves were not replaced. No shutdown was executed.
