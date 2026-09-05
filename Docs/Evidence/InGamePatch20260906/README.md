# In-game patch loading — actual Unity frame

The user rejected the external Windows launcher. This screenshot is the **actual Unity game** drawing
its existing UiRemasterV3 loading background/panel. No external GUI is used in the new startup route.

- `in-game-patch.png`: actual 2560x1440 presented D3D11 frame, visually inspected: **20.3%**, **0.81 / 4.00 MiB**.
- `measured-progress.json`: external check of the actual Unity-received byte events and rounded percentage.
- `player-trace.txt`: progress events and runtime diagnostics, no credentials.
- `updater-regressions.json`: **51 local inert-fixture checks** including preparation without activation.
- `unity-patch-result.json`: prepared immutable snapshot; no current pointer was activated or game payload executed.

Run: `Artifacts/InGamePatchTests/e6e108d7f2444cf595a4d2d8db0e2f60/`.
Player build: FastQA `20260906-001422-391`, base HEAD `115817b4` plus the documented working changes.
This is **not** an independent Release build or a real GitHub game-download/restart test.
`editor-validation-result.json`: final source compile/PrototypeValidation, 17.478 seconds PASS, after a
restart-helper exception guard was added; the screenshot's drawing and byte calculation are unchanged.

Two earlier invalid tests are not reused as approval: a missing FastQA opt-in caused a timeout (the
exact 166-file cache was hashed/recycled before rebuilding); a batch-mode capture wrote a black PNG.
The corrected visual runner requires announced `-ShowWindow` and rejects black frames. No source,
Library/Bee, saves, existing Downloads builds or the user's 13 untracked sister inputs were deleted.
