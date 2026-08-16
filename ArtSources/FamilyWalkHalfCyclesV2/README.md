# Family Walk Half-Cycles V2

This folder is the reproducible source layer for the four playable family members' 8-direction,
6-frame walk cycles. Runtime frames stay under `Assets/Art/Characters/*/Pixel/HighMotion/Frames`.

## Production contract

- Each direction starts from three canonical half-cycle poses: contact, recoil, passing.
- Frames 3, 4, and 5 are the opposite-foot half of the cycle. North and South use exact horizontal
  mirrors of their own 0, 1, and 2 poses. Directional pairs (NE/NW, E/W, SE/SW) use the matching
  opposite-facing half-cycle mirror so the body keeps facing the travel direction.
- Every runtime frame is 256x256 RGBA with hard alpha and a common floor contact at y=247.
- Frame 0 versus frame 3 must change at least 30% of the full character silhouette. All six frames
  must be unique and must match the assembled runtime sheets.
- `Tools/build_family_walk_half_cycles_v2.py` is the deterministic writer and
  `Tools/test_family_walk_half_cycles_v2.py` is the release gate.

## ImageGen provenance

Mode: OpenAI built-in ImageGen, identity-preserving game-production raster generation. The existing
approved family sprite was supplied as the identity/camera reference. The accepted generation prompt
template was:

> Create one three-panel pixel-art walking half-cycle for the referenced Family Company character,
> facing [DIRECTION]: contact, recoil, passing in strict time order. Preserve the exact face, hair,
> age, body proportions, outfit, colors, isometric camera, perceived scale, and hard pixel-cluster
> rendering. Make the planted foot and swinging foot unmistakable; swing the opposite arm naturally;
> keep head, torso, hips, hands, knees, shoes, and (for mother) the complete skirt hem visible in
> every panel. Use one character only on a flat pure #00FF00 chroma field with generous padding.
> No text, floor, shadow, furniture, extra limbs, clipped feet, static duplicate pose, blur, or
> interpolation.

The accepted regenerated raw strips are retained in `RawImageGen/`. Other half-cycle rows reused the
approved existing poses and were normalized by the same deterministic builder. Generated assets are
project-owned under the user's existing rights declaration; no third-party art was added.
