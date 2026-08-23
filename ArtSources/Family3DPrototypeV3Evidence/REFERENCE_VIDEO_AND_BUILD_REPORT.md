# Reference video and isolated Family 3D Motion Lab V3

Status: `MOTION_PROXY_PASS / IDENTITY_KEY_ART_CANDIDATE_PASS / FINAL_MESH_NOT_PRESENT`

## Reference video reviewed end to end

- Source: `C:/Users/godho/Downloads/캐릭.mp4`
- Duration: `10:39.418`
- Video: `1280x720`, `30fps`, H.264/AAC
- SHA-256: `39DB58386FC8FFF7CF6D173A5552538C6D01F64959AEDC60495A8DC3E263843E`
- Review coverage: the complete `00:00–10:39.418`, not only the first character proxy.

Character-only progression observed:

1. grey proxy used to establish scale and travel;
2. coloured/free proxy and locomotion-speed correction;
3. a 2D attempt rejected for depth/rotation/consistency limits;
4. identity key-art comparison;
5. complete image-to-3D body and rigged animation outputs;
6. Blender 360-degree review;
7. wrong jump/roll/gameplay animation failures repaired;
8. neutral showroom, same-motion comparison;
9. one unified skinned character used in actual gameplay.

Transferred rule:

`one complete body + one common Humanoid contract + one shared motion clock + same-motion showroom + actual-game recheck`

RPG combat/world-generation content was intentionally excluded.

## Rejected internal attempts

- V1: rejected because it hardcoded a BGM result and mislabeled projected directions.
- V2: rejected after independent audit found its serialized startup root differed from the runtime
  `clock=0` route by `4.24264` units and `90°`; its one-second QA warm-up hid that defect.
- V3 Run1/BuildFinal: internal diagnostics only. They were superseded before delivery because their
  first captured frame was slightly after motion clock zero and their receipts were not yet fully
  fail-closed.

Only `BuildRun3` plus `D3D11QaRun3Visible` is the current candidate.

## V3 implementation

- Source: `Assets/FamilyCompany/Experimental/Family3DPrototype`
- Final isolated build: `Artifacts/Family3DPrototypeV3/BuildRun3`
- Unity `6000.3.21f1`, Built-in RP, Windows x64 Development build.
- Proxy model: Styloo CC0 `allinone.fbx`, valid Humanoid Avatar.
- Shared animation: `PlayerHumanoidWalk.fbx` / `mixamo.com` for all four actors.
- Locked left/right cycle: `0.99380799s`, `120.7477` steps/min.
- Controller travel: `1.0 world unit/s`; retargeted animation root motion is discarded.
- One outer bottom-centre root owns translation/yaw; the visual child is reset after every sample.
- Fixed-camera mapping: world `-Z/-X/+Z/+X` = office `SW/NW/NE/SE`.
- Builder and runtime both obtain the startup position/direction from the same public route evaluator.
- First rendered frame is frozen at exact `clock=0 / SW / P0`, then continuous motion begins.

## Actual visible D3D11 result

- A real player window was confirmed by non-zero Win32 window handle.
- D3D11 renderer: NVIDIA GeForce RTX 3080 Ti.
- 417 contiguous `1280x720` PNGs, with no missing indices.
- Review MP4: H.264, `1280x720`, `30fps`, `417` frames, `13.9s`.
- Actual captured motion: `12.25096s`; capture span `12.22300s`.
- Transition evidence: `SW -> NW -> NE -> SE -> SW`.
- Direction pose masks: `63 / 63 / 63 / 63` (P0 through P5 present in every direction).
- Metadata-driven evidence selector chose one contiguous turn-safe cycle per direction; no V2 fixed
  frame numbers were reused.
- All four actors use the same clip, phase offset, and cycle.
- All four reverse the anatomical leading foot between P0 and P3.
- Root continuity, expected route, actor yaw parity, and visual child-root drift violations: `0`.
- Audible-risk and post-enforcement audio violations across the full run: `0`.
- Automatic receipt: `AUTO_PASS_VISUAL_REVIEW_REQUIRED`; independent full-resolution visual review
  also passes the motion-proxy scope.

See `D3D11QaRun3Visible/VISUAL_REVIEW.md`, `qa-receipt.json`, the four close-up sheets,
`four-turns-28frame-v3.png`, and `family-3d-four-direction-synced-loop-v3.mp4`.

## Identity inputs and remaining dependency

Four 3D identity turnaround key-art candidates are prepared under
`Artifacts/Family3DIdentityTurnaroundsV1`. They preserve the latest approved hatless Player V6 and
the Father/Mother/Older Sister identity cues. They are images, not meshes.

Styloo is one female chibi base with outfit toggles. It cannot become exact final Player, Father,
Mother, and Older Sister bodies. The Run3 executable therefore proves locomotion architecture and
QA only; it is not a final family-character executable and is not eligible for production.

The remaining hard dependency is four complete textured Humanoid FBX/GLB models generated from the
turnarounds, or authorization to authenticate and spend credits in the image-to-3D/rigging workflow.
Higgsfield CLI preflight succeeded locally without login or generation, but account workflow/model
enumeration stops at browser OAuth authentication. No login, upload, job, or credit use occurred.

Production/default/Downloads files remained unchanged; see `PRODUCTION_ISOLATION_AUDIT.md`.
