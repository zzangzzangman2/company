# Mother North Walk V2 source

These three RGB chroma-key PNGs are the approved ImageGen half-cycle for the mother's north/back-view walk.
They stay outside `Assets/` so Unity does not import multi-megabyte production sources during ordinary iteration.

## Source contract

| File | Phase | SHA-256 |
| --- | --- | --- |
| `mother_north_half_0_contact.png` | viewer-right support contact; viewer-left arm back/down | `25E2298FCFE2BE644E9074974732F18BCC1D378DAEC5A47EF133910F6905BEDA` |
| `mother_north_half_1_recoil.png` | viewer-right support recoil; both arms pass centre | `EFBC7D09E3085F0B7DFDBB60A238D87C22B4765C15339AC81A799FE9B1684C2B` |
| `mother_north_half_2_passing.png` | viewer-left support passing; viewer-right arm back/down | `C385311B40040FAF1D73EFEBFEE13E10F70CCE87D769D67DC23FC2965B0BC37A` |

`Tools/build_mother_north_walk_v2.py` removes green chroma, normalizes the full body to 225 px with a y=247 ground line,
and creates phases 3/4/5 as pixel-exact horizontal mirrors of phases 0/1/2. This makes the second step physically opposite
without asking a generative model to infer six-frame temporal order.

## Final production prompt

Use case: identity-preserve pose-guided production pixel-art sprite. Preserve the supplied mother's braided brown hair,
pink cardigan, teal long skirt, stockings, brown shoes, body proportions, north/back camera, palette, and polished Korean-game
pixel rendering. Create one full-body pose centered on a flat pure-green chroma field with every hand and shoe visible.

- Phase 0: viewer-right shoe is the long/lower support foot; viewer-left shoe is short/high. Viewer-left arm swings back/down;
  viewer-right arm bends forward/up. The skirt responds slightly toward viewer-left.
- Phase 1: viewer-right remains the support foot while the stride shortens. Both arms travel toward centre and the skirt settles.
- Phase 2: viewer-left becomes the long/lower support foot; viewer-right lifts short/high. Viewer-left arm is forward/up and
  viewer-right arm is back/down. The skirt begins responding toward viewer-right.

Constraints repeated for every phase: stable head/torso/root and scale; contralateral arm/leg motion; separated complete feet;
no static symmetric arms, duplicate pose, merged or extra limbs, crop, front/side view, face turn, camera drift, blur, text,
border, shadow, or watermark.

## Validation

- `python Tools/build_mother_north_walk_v2.py --check`
- `python Tools/test_mother_north_walk_v2.py`
- `python Tools/split_high_motion_sheets.py --verify-only --character mother`
- `python Tools/measure_animation_coherence.py --motion walk --strict`
