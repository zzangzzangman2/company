# Family Locomotion Rig V1

`FC-FAMILY-LOCOMOTION-RIG-V1` is the canonical source for the four family walk loops.
It replaces full-body six-frame generation with deterministic two-bone leg assembly under the
committed direction-specific identity upper bodies.

- The five tracked chroma-green PNGs contain isolated left/right thigh and lower-leg/foot parts.
- `rig_manifest_v1.json` pins every raw SHA-256, direction row, garment seam, hip, and bone profile.
- `Tools/build_family_locomotion_rig_v1.py` is the only publisher. It generates 4 characters ×
  8 directions × 6 phases, explicit cyan/magenta anatomy markers, sheets, contact sheets, fixed-floor
  motion GIFs, and numeric foot-lock metrics.
- Phase 0-2 always use anatomical left support; phase 3-5 always use anatomical right support.
- A support foot moves backward in sprite space by the exact projected runtime root step
  (`19.235... px/phase`), so its projected world position may drift by at most 1 px.
- Opposite directions are deterministic whole-frame mirrors. No upper/lower waist splice, global
  visual-root easing, or per-frame body recentering is permitted.

Generate a review candidate:

```powershell
python .\Tools\build_family_locomotion_rig_v1.py
```

Publish only after the marker/manifest QA passes:

```powershell
python .\Tools\build_family_locomotion_rig_v1.py --write
```

Publishing overwrites only the existing 192 runtime PNGs and eight sheet PNGs. Existing Unity
`.meta` files and GUIDs are preserved.
