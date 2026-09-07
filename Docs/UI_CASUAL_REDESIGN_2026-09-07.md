# Casual office UI and update feedback — 2026-09-07

Status: source implementation / headless checks; graphics testing paused by user instruction;
**not a public patch**. Public latest remains
`fc-win-20260907.2` (v5). Source starts at clean `b07e24d1` on the canonical `main` checkout.

## Requested scope

The user rejected the mismatched ornamental Company / People / Business / Research / Investment
screens and explicitly requested a new, genuinely casual design, including image generation.
The prior request for one overall patch percentage and a visible restart notice remains in scope.
No map, camera, character, furniture geometry, balance, save schema, or paid Higgsfield work is included.

## Visual contract

- Native rounded paper-white surfaces; quiet sage/denim/apricot accents; ink `#303D42`.
- One shared `OfficeCasualPalette`, used by the native menu, shop and stock-market presentation.
- Bundled OFL Pretendard in the HUD and all revised detail views; real Unity text, never baked labels.
- One new transparent illustrated office icon family. The old gold/coral ornamental modal, ribbon,
  card and full-screen market overlay are no longer selected by the revised routes.
- Header and cards share an aligned content boundary. Grid width comes from the actual viewport,
  not a fixed desktop cell width. Body copy remains readable; long content scrolls inside a masked
  viewport. Research uses two columns in compact windows, three in larger windows.
- Existing contracts, products, personnel, company cash, market state and navigation adapters remain
  authoritative. Unimplemented features remain explicitly labelled `준비 중`; no invented metrics.
- The roster uses a neutral personnel icon rather than obsolete hidden 2D character sprites.
  This is not a new family portrait or a change to the approved world models.

## Image provenance

Built-in `image_gen`, **not API/CLI fallback**. One atlas generation:
`exec-f35e4954-8db1-4bd2-84d0-ddabd8f967e3`.

- Workspace asset: `Assets/Art/UI/Resources/OfficeCasual/office-icons-v1.png`.
- Original: `C:/Users/godho/.codex/generated_images/01a06ef4-ef32-75c1-a0cc-f0b0b79fe33e/exec-f35e4954-8db1-4bd2-84d0-ddabd8f967e3.png`.
- 1536×1024 RGBA; copied byte-for-byte. SHA256
  `288df4da6f0346b8217622249547d1746e4f20dd1d8c294bca9ba1b832d0aba8`.
- Native runtime crops six non-overlapping 512×512 cells; alpha preserved, no pixel repaint,
  chroma key, Python image editing, text rasterisation or map screenshot imported.
- Order: Company, People, Business / Research, Investment, workstation navigation symbol.
  The last symbol is not a replacement shop model or furniture preview.
- Full generation prompt: [imagegen-prompt.txt](Evidence/OfficeCasual20260907/imagegen-prompt.txt).

## Overall patch percentage

`PatchDisplayProgress` owns a single monotonic value per attempt:

1. Aggregate compressed download bytes map to 0–85%.
2. Aggregate final verified file bytes map to 85–99%.
3. A successful worker result earns 100%.

These are **stage weights**, not estimated remaining time or a single compressed/uncompressed
byte fraction. `expand`, `reuse`, directory changes and lower/out-of-order observations do not
reset the displayed value. Raw per-stage totals remain in the diagnostic log. An explicit Retry
begins a new attempt at zero. The displayed whole number is floored, never rounded to 100 early.

After preparation, the real loading UI must repaint `업데이트 완료 · 게임을 다시 시작합니다`.
A four-second countdown begins only after that repaint. Failure to render is fail-closed.
Only then does the existing verified-parent / normal-exit / atomic-snapshot restart handshake run.
No forced process kill or incomplete installation fallback is added.

## Fixed-entry migration caveat

The currently installed Downloads entry contains the **v2 bootstrap assembly and worker**.
It checks/downloads the latest immutable snapshot before launching that snapshot. Merely publishing
a new snapshot changes the in-game menus, but **cannot retroactively change that old entry's first
patch screen**. Source push or the new snapshot's startup is not proof that the fixed v2 bootstrap
has received the new progress UI.

The fixed path remains `%USERPROFILE%/Downloads/FamilyCompany_Playtest/FamilyCompany.exe`.
To fix its first screen, a one-time, approved, backed-up installation/bootstrap refresh at that same
path must be planned and verified against the final Release; do not silently replace Downloads,
run a FastQA player there, repoint it to an AppData version, or consume the user's cache to fake proof.
The current user cache is v5 (`versions/5-9664e3ec8254`), observed read-only after their update.

## Verification

Pending final results are recorded in PROJECT_STATE. The expanded native UI test visits all five
hubs and reachable internal detail routes at 1280×720, 1024×768, 1600×1000 and 1920×1080,
checks real TMP overflow and horizontal containment, and captures scroll endpoints.
The separate existing full-navigation test covers shop / stock adapters, route back stacks and saves.
These prior captures are not input/resource isolation proof. Following the user's click-interference
report, graphics tests are stopped, including private-desktop rendering. The exact interference cause
is unconfirmed. The latest expanded capture run failed on a layout-component lifecycle exception;
the subsequent source fix compiles but has not been rerun in a Player. Do not label it visual PASS.
Only source/logic work resumed after the user's background-only instruction. See
[headless evidence](Evidence/OfficeCasual20260907/BACKGROUND_ONLY_QA_2026-09-07.md).

Progress tests use two large files in different directories and actual paced streaming, not a lone
percent animation. Restart UI-only preparation, real Unity parent/child local restart and public
GitHub deployment are separate claims. No new public Release or bootstrap installation is implied.
