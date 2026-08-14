# Office UI P0 visual specification

## Diagnosis and layout contract

- Reference screens: 1920×1080 and 2560×1440, both 16:9, with a 1920×1080 CanvasScaler reference and 0.5 width/height match.
- Safe margin is 24 px. Repeated gaps are 8, 16, or 24 px. Interactive targets are at least 48 px high.
- The management hierarchy is: company/time/cash header; family rail; three equal offer cards; active-contract strip; office/time/save controls.
- The prior presenter validated `ManagementUiLayoutMetrics` but separately hard-coded 188 px family cards, an 88 px header, a 280 px progress strip, and a long one-column action list. At 1080p those independent values produced uneven density and avoidable clipping pressure.
- Management body text uses the bundled OFL Pretendard Variable font. Maplestory Bold remains the warm display face, with bundled Maplestory Light as a direct fallback. No operating-system font is used.
- The management-only 9-slice metadata is normalized to the design-token heights: panel border 12 px on an 88 px header, card border 16 px, and tight-cropped button/tab borders 12×5 px. The previous 128/72, 72/48, and 80/56 vertical ratios forced top/bottom borders to overlap or compress. The 10 px button/tab vertical sum leaves a real stretch center at every 48/56 px target height.
- The source panel/card PNGs contain 18/15 px transparent outer gutters. Runtime panel `(18,10,988,492)` and card `(15,10,481,620)` tight slices remove those gutters before applying the 12/16 px borders. Content uses `border + 8 px + 1 px pixel-snap guard` (21 px panel, 25 px card), so outward-rounded glyph pixels still retain at least 8.000 px from the inner frame. Three offer cards use zero preferred/minimum width plus equal flexible width, preventing content-derived runtime width drift.
- The R1 capture fixture records offer border widths `385/358/394` (36 px spread), text safe-inset deficits `13–16 px`, and transparent-gutter button surfaces `22×32/75×48`, `34×32/116×48`, and `94×32/240×48`. Across the replacement normal, disabled, and tab tight crops, the conservative alpha-200 minimum is 93.14% opaque area, 98.93% opaque width, and 94.64% opaque height before 9-slice stretch.
- Pretendard remains the body face and Maplestory Bold remains the heading face. Official `NotoSansKR-VF.ttf` is bundled only as the decomposed-Jamo fallback; both NFC `각` and NFD-style `각` are rendered in player QA. Source: `notofonts/noto-cjk` `Sans/Variable/TTF/Subset/NotoSansKR-VF.ttf`, SHA-256 `9E1D729E7E2B36F9EF439DA102F8C134C10AABE46F1C843BF0ACA5C043B86F76`, SIL OFL 1.1 in `Assets/Fonts/Licenses/LICENSE-NotoSansCJK.txt`.

## Background asset visual spec

The center 70% stays quiet and low-frequency so Unity-rendered Korean UI remains legible. Warm ivory paper, muted sage/teal paint, and restrained walnut at the edges preserve the early-2000s family home-office identity. Corners carry a gentle vignette; the bitmap contains no UI, text, logo, numbers, people, or functional controls.

## Image generation ledger

- Tool mode: built-in `image_gen`, `stylized-concept`; no CLI/API fallback.
- Inputs: the user screenshot was used only as a problem/layout reference. `title_hero_background_v3.png` was used only as a palette/style reference.
- Generated source: `C:\Users\godho\.codex\generated_images\01a000c0-d865-7780-819a-edcc1e3bf75b\exec-ddcc6fd2-b8ce-4f0e-bf7a-4cf6f8d395ec.png` (1672×941).
- Project asset: `Assets/Art/UI/Resources/ManagementUIP0/office_management_background_v1.png` (Lanczos-resampled to 2560×1440, uncompressed standalone import).
- Project SHA-256: `D4DBD4171691DCB2659619689CF6C60C313E23B84F8EDC56FA729F3B26308A5C`.

Final prompt:

> Use case: stylized-concept. Asset type: 2560×1440 Windows game management-screen background bitmap. Input images: Image 1 is a problem/layout reference only—do not reproduce its UI, panels, buttons, text, or screen composition literally. Image 2 is a style and palette reference for the warm early-2000s Korean family-company home-office identity only. Primary request: create a polished textless background surface for a management UI, with a calm low-detail center that supports opaque interface panels and subtly richer office atmosphere near the outer edges. Scene/backdrop: warm home-office materials suggested abstractly through soft ivory paper texture, muted teal painted surfaces, walnut wood grain, and restrained late-afternoon light; no literal interface. Style/medium: crisp high-quality hand-painted game background with very subtle pixel-art influence, clean edges, production-ready, not photorealistic and not blurry. Composition/framing: exact 16:9 landscape intended for 2560×1440. Keep the central 70% and all major UI zones visually quiet and low-frequency. Use gentle center illumination and restrained edge/corner vignette. Edge details must remain sparse and decorative, never form panels or fake controls. Lighting/mood: warm, dependable, welcoming family business; soft directional light; clear enough for dark Korean UI typography. Color palette: warm ivory, muted sage/teal, medium walnut, tiny restrained coral accents only at the far periphery. Materials/textures: fine paper fiber, clean wood grain, subtle painted-wall texture; no heavy noise. Constraints: no people or characters; no text, letters, Korean, English, numbers, logos, company names, watermarks, signs, labels, charts, documents with writing, monitor content, fake buttons, fake cards, fake windows, or UI frames. Do not imitate functional UI in the bitmap. No unnecessary objects. Preserve ample calm space for Unity-rendered UI. Avoid: blur, smeared texture, warped perspective, gibberish writing, dense office clutter, strong highlights behind text, low-contrast fog, excessive transparency, faux glass UI, visual noise.

The F8 showcase behavior is intentionally out of scope. This patch only exposes the existing `C` observation-camera path as a normal HUD and management-screen control.

Management card construction, state binding, dynamic fallback-atlas population, Canvas layout, and TMP mesh generation run while `ScenePreviewJump.IsPresentationLoading` is true. The management Canvas remains active but hidden through a `CanvasGroup` with alpha 0, interaction off, and raycasts off. Showing the screen changes only alpha/input state and live-value diffs. Session-state replacement explicitly invalidates and rebuilds the same active-hidden prewarm; each bind creates fresh child views under component-free persistent hosts so deferred `Destroy` cannot create duplicate layout groups.

## R2e Release/D3D11 acceptance evidence

- Build: Unity `6000.3.21f1`, Windows x64, non-Development, warnings 0, build duration `00:00:06.1113232`.
- 1920×1080: actual snapped offer-card widths `419/418/419 px` (spread `1 px`); minimum text/frame-inner clearance `8.000 px`; family-title overlap `0 px`; panel outside `0 px`; button opaque surface area/width/height `93.1%/99%/95%`; missing glyph/tofu/overflow/text overlap/raycast miss/small button `0`.
- 2560×1440 capture: actual snapped offer-card widths `558/558/558 px` (spread `0 px`); minimum text/frame-inner clearance `11.000 px`; family-title overlap and panel outside `0 px`; missing glyph/tofu/overflow/text overlap `0`.
- Korean proof includes composed `각`, decomposed `각` (`U+1100/U+1161/U+11A8`), digits, won sign, punctuation, and multiplication sign. Release logs report missing glyphs `0`; code-point diagnostics remain armed for any future miss.
- 1080 transition frame times: first management open `p95/p99/max 6.056/6.056/6.056 ms` (`12.103 ms` completion); management→office button, office→management shortcut, management→office shortcut, and restore path each max `6.056 ms` with completion `6.155–7.292 ms`; restart then management open max `6.056 ms`; all gameplay transition frames at or above 50 ms `0`.
- Loading owns the allowed cost: initial loading completes in `2620.187 ms`, with frame-time `p95/p99/max 19.649/77.257/260.789 ms` and three frames at or above 50 ms. Loading UI remains the active foreground while the management Canvas is alpha-zero and non-interactive. The independent 1440 run completes loading in `2506.981 ms`.
- Prewarm/rebind invariants after replacement session: Canvas roots `1`, button/listener hosts `19`, runtime font assets `3`, atlas textures `5`; duplicate roots/listeners/objects/click effects/atlas growth/stale state/exceptions `0`.
- 1080 capture/report: `C:\Users\godho\Documents\Codex\2026-08-14\fc-perf-lag\work\ui_font_diagnosis\current_captures_ui_p0_r2e_final4_1080`.
- 1440 capture/report: `C:\Users\godho\Documents\Codex\2026-08-14\fc-perf-lag\work\ui_font_diagnosis\current_captures_ui_p0_r2e_final4_1440`. The QA machine's physical display is 1920×1080; this capture follows the established Unity supersize-2 path from a 1280×720 16:9 window, while pure layout validation separately evaluates canonical 2560×1440 coordinates.
