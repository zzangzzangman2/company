# Casual office UI and update feedback — 2026-09-07

Status: **published as fc-win-20260907.3 (sequence 6)**; game source
`cb86605dad19a823551498c2d4bb6bd073031864`. Final limited graphics validation and same-path,
backup-first main refresh were explicitly authorized and completed. Future graphics require fresh authorization.

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

## Fixed-entry migration (completed at home; company instructions apply once)

The old v2 Downloads bootstrap could not acquire the new first patch screen merely by launching a new snapshot.
The user explicitly approved **메인도 백업 후 갱신**. All 169 old installation files are retained in a separate backup;
the same fixed entry now contains all 169 verified v6 files. Saves and the user's v5 cache remain unchanged.
The main was not auto-launched. Current workers prefer the existing v5 cache over seed files, so the next home
launch downloads the verified v5→v6 delta and displays the new UI. Later unchanged runs do not repeat downloads.

[Exact path, backup, ZIP identity and company one-time install](MAIN_GAME_ENTRY.md).
Do not replace the fixed entry with a FastQA player or point shortcuts to an AppData version.

## Verification

Final exact non-Development Release passed all hubs/internal details at 1280×720, 1024×768, 1600×1000 and
1920×1080 with TMP overflow/containment checks, plus 54 full-navigation EventSystem routes. The former
Canvas lifecycle exception is resolved. These are not new OS pointer clicks or proof of total resource isolation.

Fresh worker regressions: 81 assertions; overall display model: 18 assertions.
An isolated copy of exact Release executable/DLL bytes, under the existing QA data-folder name, passed
two-folder streaming and real rendered 100%/four-second notice. A separate fixture installed real Unity bytes
and observed the normal-named successor verify its snapshot and unlock. Fixture workers are not production network proof.
The actual unchanged shipping worker separately received the public v5→v6 delta: 12 files / 166,189,664 bytes,
158 download events, all 169 hashes. User main/cache/saves were unchanged during that test; installation refresh followed.

Original failures are retained: an existing total-Editor-heap assertion failed once, followed by two unchanged PASS
iterations; exact cause is unconfirmed. The first progress fixture invocation mistakenly used a normal Release name,
ignored fixture flags and checked public v5; its screenshot gate failed and is not counted as PASS. Corrected artifact
harnesses isolate the fixture by copy/name without changing runtime bytes. Main/cache pointer/save contents survived.

[Final evidence, installation receipt and scope limits](Evidence/CasualUiRelease20260907/README.md).
[Earlier headless-only stage](Evidence/OfficeCasual20260907/BACKGROUND_ONLY_QA_2026-09-07.md) is historical.
