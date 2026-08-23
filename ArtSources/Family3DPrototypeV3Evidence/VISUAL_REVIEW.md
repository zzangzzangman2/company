# Family 3D Motion Lab V3 — D3D11 visual review

Status: `MOTION_PROXY_PASS / FINAL_FAMILY_IDENTITY_NOT_ELIGIBLE`

## First rendered frame

- `frame_0000.png` was inspected at native `1280x720`.
- Overlay and metadata both read `clock=0.000000000`, `SW`, `P0`, `phase=0.000000000`.
- All four roots begin on the expected top/right route corner, already facing SW.
- Receipt startup position error and yaw error are both `0`; first-update root step is `0`.
- There is no black/loading frame, startup teleport, or startup corrective turn.

## Complete run

- Visible Windows player confirmed with a non-zero window handle.
- Renderer: `Direct3D11`, NVIDIA GeForce RTX 3080 Ti.
- `417` contiguous native-size PNGs; H.264 review video is `1280x720`, `30fps`,
  `417` frames, `13.9s`.
- Captured motion-clock span is `12.2230s`, covering a full continuous route loop.
- Captured transition sequence is `SW -> NW -> NE -> SE -> SW`.
- Each direction contains all six pose bins (`pose mask 63`) and multiple gait cycles.

## Anatomical gait and phase parity

The following files were inspected at original resolution:

- `SW_24pose_closeup_sheet_v3.png`
- `NW_24pose_closeup_sheet_v3.png`
- `NE_24pose_closeup_sheet_v3.png`
- `SE_24pose_closeup_sheet_v3.png`
- all four corresponding context sheets
- `four-turns-28frame-v3.png`
- `family-3d-four-direction-synced-loop-v3.mp4`

Across all four directions and all four actors:

- two legs and two feet remain present;
- P0 and P3 visibly exchange the leading anatomical foot;
- both arms remain present and swing on the same shared phase;
- the body does not split at the waist and the head/waist/legs do not turn independently;
- travel and animation do not pause at corners;
- pelvis peak-to-peak is only `0.4343%–0.4505%` of standing height, so no excessive bobbing is visible.

Measured P0/P3 left-minus-right lead:

| Actor | P0 | P3 | Alternates | Pelvis / height |
|---|---:|---:|:---:|---:|
| Player | +0.55247 | -0.48157 | yes | 0.4505% |
| Father | +0.59666 | -0.52009 | yes | 0.4343% |
| Mother | +0.57457 | -0.50083 | yes | 0.4478% |
| Older Sister | +0.55799 | -0.48638 | yes | 0.4478% |

## Automatic gates

- `automaticGatesPass=true`
- exact startup state: pass
- direction order, four-direction coverage, and all P0-P5 masks: pass
- root position/delta continuity: `0` violation frames
- maximum route position error: `0`
- maximum route-step error: `0.000000477`
- maximum actor yaw divergence: `0`
- visual child root position/rotation drift: `0` for all actors
- whole-run audible-risk violations: `0`
- post-enforcement audio violations: `0`
- final listener/source state: listener `0`, sources `2/2` muted, `0` playing

## Scope boundary

This passes the common 3D locomotion/controller/showroom gate only. The four models are labeled
Styloo motion proxies, not the approved Player/Father/Mother/Older Sister identities. They must not
be promoted to production or described as final family meshes. Final identity review requires four
complete textured Humanoid FBX/GLB meshes made from the prepared identity turnarounds.
