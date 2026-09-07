# PROJECT STATE

Last updated: 2026-09-07. Current handoff only. Game build SHA and later documentation/tool SHA are distinct.

## Latest delivered: casual UI + overall patch feedback (v6)

- Public release: [fc-win-20260907.3](https://github.com/zzangzzangman2/company/releases/tag/fc-win-20260907.3),
  sequence **6**, release ID **384067644**, game source **cb86605dad19a823551498c2d4bb6bd073031864**.
- Company / People / Business / Research / Investment hubs and details now share paper-white/sage surfaces,
  Pretendard text and a six-cell generated office icon family. Shop/market presentation shares the palette.
  World, family bodies, furniture geometry, Simulation/Save and shipping updater workers are unchanged from v5.
- One overall progress value: aggregate compressed download 0–85%, final verification 85–99%, accepted result 100%.
  File/folder/expand/reuse events do not reset it. These are stage weights, not ETA or a combined byte ratio.
  Actual loading-screen Repaint starts a four-second restart notice, then normal exit and verified successor launch.
- User explicitly authorized final deployment graphics checks, without OS mouse/keyboard or foreground switching,
  then **메인도 백업 후 갱신**. That one-time installation refresh is complete, not pending.
- Same home entry: `C:\Users\godho\Downloads\FamilyCompany_Playtest\FamilyCompany.exe`.
  Its entire 169-file v2 installation was retained first at:
  `C:\Users\godho\AppData\Local\FamilyCompany\InstallationBackups\20260907-212438-6fe36f2bde9343c0b9f2e6db9f47ea8c-before-fc-win-20260907.3\FamilyCompany_Playtest`.
  The same main folder now has all 169 public-verified v6 files. No files were deleted.
- Five save/backup files, all 10 files under the save directory and all 344 patch-cache files preserved by hashes.
  The user's cache remains v5. The refreshed main was **not auto-launched**. On the next user launch the production
  worker prioritizes that v5 cache, downloads the verified v5→v6 delta, shows the new overall UI and activates v6.
  Later unchanged runs verify the latest snapshot without repeating the download.
- Public delta: **12 changed files / 166,189,664 compressed bytes (158.5 MiB) / 158 download events / 157 reused**;
  every one of 169 files verified. This was a fresh isolated public shipping-worker transfer, not a native user-main run.
- Full ZIP: **276,668,119 bytes**, SHA256 `1f856cb9985adae3ad767d959a4ca76634998411058a951b89c6bc9f5ea6dc66`.
  Fifteen uploaded assets were verified by GitHub IDs, sizes and digests before publication.
- [Delivery evidence and limits](Evidence/CasualUiRelease20260907/README.md) ·
  [Main entry / company install](MAIN_GAME_ENTRY.md) · [Company handoff](COMPANY_HANDOFF_2026-09-07.md) ·
  [Design/progress contract](UI_CASUAL_REDESIGN_2026-09-07.md).
- Fresh post-publication remote refs/tags/releases inventory PASS: prohibited=0, unknown=0, complete pagination.
  Post-release fixture entry guards passed four expected early refusals in PS5.1/PS7 without game/fixture creation.
  Only handoff/evidence and safety-test entry scripts changed after the game build; shipping Assets/workers did not.

## Fresh verification and known test limitations

- Clean exact Unity 6000.3.21f1 non-Development Release PASS, 83.01s. Warm Library/Bee retained.
  Pure Simulation/save PASS (13.654s). Existing broad Editor heap oracle failed once; no code/oracle/tolerance
  changes were made and both bounded repeat iterations passed (58.052s total). Exact initial cause is unconfirmed.
- Final Release UI: 1280×720, 1024×768, 1600×1000, 1920×1080; all hubs/internal details, no measured overflow,
  zero runtime exceptions. Full navigation: 54 EventSystem routes, not new Windows pointer clicks.
- Normal navigation: 8,100 samples, zero collision/interaction/error samples; rail error at most 0.0000256875 cells.
  All four next-day 09:00/01/02/03 arrivals and subsequent real seated work observed.
  3,727 settled seat samples, zero failures; controlled two-body/four-direction fit: 264 samples, zero chair penetration.
- Actual 23.975-second walking capture: 337 frames; four foot-midpoint/lead-alternation gates passed.
  Eight seated captures and six chronological walking sheets were inspected; not every frame or zero skin-slip proof.
  No new body/rig/clip promotion or new 360-degree model approval.
- Starter business integration PASS, including actual first lesson work, final development 4 PH and support 2 PH.
  Earlier history/billing use explicit checkpoints, not uninterrupted whole-week play.
- Updater: 81 fresh worker assertions + 18 display-model assertions. Exact Release executable/DLL bytes in an
  isolated QA-named copy passed multi-folder rendered progress/notice and real normal-named Unity successor tests.
  Local fixture transport and public GitHub transfer are separate evidence.
- Initial patch fixture invocation used the normal Release name, so fixture flags were ignored (`qa=False`).
  It checked existing public v5 and the screenshot gate rejected it. Main/cache pointer/save contents unchanged;
  updater run logs/locks were touched. Original failed log retained; not a PASS. Corrected harness uses only an
  isolated renamed copy with unchanged compiled bytes. Shipping payload was not modified.
- Earlier failing FastQA cache (166 generated files) was retired after preserving identity/hashes/failure logs.
  No failed runtime payload was promoted. Old native shop evidence binds only unchanged transaction/grid/controller
  code; skin colors/font changed, but scale and complete layout-metrics tail are byte-identical and current renders
  plus managed four-rotation purchases/rejection passed. Do not describe this as new native input.

## Current gameplay and next work

- New game: 2000-01-03 08:50, empty 13×13 office, four family identities, KRW 5,000,000.
  Player V8 × 2 / Father V19 × 2 are the approved temporary bodies; unique Mother/Older Sister 3D models are deferred.
- Shop: desk + PC + original chair as one KRW 400,000 set, three occupied cells, exact tile-centre placement,
  four rotations, occupancy rejection, direction-bound seat/monitor axes. Do not change approved dimensions/poses casually.
- First subcontract → acquired know-how → own store-management product → trial sales → weekly support loop remains.
  See [STARTER_BUSINESS_LOOP.md](STARTER_BUSINESS_LOOP.md). Save v12 reads v1–v11; no cross-PC save sync.
- Real external live stock API, product performance→stock valuation and additional research/product families remain future work.
  User play feedback and money/time balance come next. No paid generation or shutdown was performed for this delivery.
- Before any new family asset work read [FAMILY_3D_CHARACTER_STANDARD.md](FAMILY_3D_CHARACTER_STANDARD.md).
  No legacy 2D sprite/atlas/PSB/body-part fallback or motion donor is allowed.
- Canonical development root: `C:\Users\godho\Documents\Codex\fc_agents\integration_p0`, branch `main`.
  Do not work in the obsolete 2026-08-25 thread folder, create branches/worktrees or delegate to agents.
  Company: inspect clean main and remote, then `git pull --ff-only origin main`.
- The narrow final graphics approval is now consumed. Future graphics/Player tests require fresh user authorization.
  No OS mouse/keyboard or desktop switch. Ordinary checks remain sequential, BelowNormal and at most two cores.
  Prefer warm FAST_QA; playing requires no Unity/build. Packaging uses PowerShell 7.2+.
- Previous accumulated PROJECT_STATE content is retained as
  [a historical snapshot](History/Reports/2026-09-07-PROJECT_STATE-before-casual-release.md), not current instructions.
