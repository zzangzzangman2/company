# Player/Father rendered-shoe tile-safe candidate

Status: `productionEligible=false`. This is still the command-line
`-familyCompanyLegacy2DScaleCandidate`; it does not replace production/default until the user
approves the actual GIF. Player V8 and Father V19 retain their same-package mesh, Avatar, skin,
action-613 clip, `poseStrength=1`, travel facing and limb curves.

## Corrected failure

The preceding evidence was wrong. It measured only `HumanBodyBones.LeftFoot/RightFoot`, which are
ankle pivots. The user enlarged frame 14 and correctly showed Father's visible forefoot covering a
tile line even though the ankle pivot was reported 7px away. The old `8.135/7.096px` claim is
superseded and must not be used as acceptance evidence.

QA now requires `LeftToes/RightToes`, grounds ankle and toe, expands the sole axis by `0.65` behind
the ankle and `0.45` beyond the toe, and subtracts a conservative `4px` rendered-shoe half-width.
A planted shoe fails if less than `2px` of clear floor remains before the nearest tile line.

Twenty additional phase-only candidates all failed that corrected envelope. The final candidate
therefore keeps the existing `stride 0.99380799 / phase 0.64` and the unmodified package walk, but
applies the minimum whole-character translation needed to keep the contacted shoe inside a
`0.20-cell` tile inset. The correction releases during the authored airborne gap and resets before
seat alignment. It never moves a limb bone, never replaces the clip, and is absent from
production/default.

## Final hidden D3D11 result

- Unity `6000.3.21f1`, D3D11, deterministic 24 fps, all `89` approach frames captured;
- Player/Father conservative planted-shoe clearance: `3.562 / 3.562px`;
- planted contact samples: `65 / 66`; contact frames below `2px`: `0 / 0`;
- independent X-axis sweep: `3.870 / 3.870px`, `0 / 0` touch frames;
- independent Y-axis sweep: `3.870 / 3.870px`, `0 / 0` touch frames;
- the actors move oppositely in each sweep, covering `+X/-X/+Y/-Y`;
- semantic route starts on exact tile centres; maximum centre-line deviation
  `0.000002 / 0.000212` world units;
- foot-midpoint tile-centre error median/max: Player `3.170 / 7.470px`, Father
  `2.514 / 3.249px`, inside the locked `4/8px` gate;
- visual overlap `0`, agent penetration `0`;
- real purchased V31 work routes ended `Working/Working` at `seat_player/seat_father`;
- seated tile-centre error `0/0`; knees Player `83.78/95.48`, Father `94.60/107.66` degrees;
- working static/interaction/agent violations `0/0/0`; retired visible renderers `0`.

Every Father and Player frame was reviewed in six enlarged chronological sheets, including the
reported frame 14. The contacted shoe has visible floor pixels before the line; both characters
retain two legs/shoes, two arms/hands, upright bodies, opposite arm swing and the existing walk
timing. Automatic PASS is supporting evidence only; user review remains the promotion authority.

The unchanged production/default profile separately passed hidden D3D11 at
`Artifacts/PlayerFather3DDefaultRegressionShoeInsetChange-20260901/` with stride `0.7950477`,
`productionEligible=True` and overlap `0`. Candidate correction is not active there.

All company-PC Unity runs used standalone `-batchmode`, `CreateNoWindow=true`, hidden process style
and continuous `MainWindowHandle == 0` monitoring. No Unity or Blender window was opened.

## Review media

- `player-father-tile-center-map-walk.gif`: full map, all 89 frames;
- `player-father-tile-center-walk-zoom.gif`: enlarged moving corridor, all 89 frames;
- `father-user-reported-contact-frame14-fixed-4x.png`: direct enlarged replacement for the
  user-reported bad frame;
- `zoom-all-frames-000-014.png` through `zoom-all-frames-075-088.png`: every frame in order;
- `player-father-foot-tile-trace.csv`: X-run ankle, toe, shoe-envelope and contact values;
- `player-father-foot-tile-trace-y-axis.csv`: independent Y-run trace;
- `player-father-foot-tile-sweep-x-axis-result.txt` and
  `player-father-foot-tile-sweep-y-axis-result.txt`: four-direction summary;
- `player-father-avoidance.png`, `player-father-working.png`, and interaction result/final receipts.

## SHA-256

| File | SHA-256 |
| --- | --- |
| `father-user-reported-contact-frame14-fixed-4x.png` | `0C9C454124837D7534A7E8379A6179EB2B5D4342772584D6F5AB040581039466` |
| `player-father-tile-center-map-walk.gif` | `466C54D202C1E37168A104F27C3E5BE6A831D33422E889E4CEA49DD6A744D7FF` |
| `player-father-tile-center-walk-zoom.gif` | `EB406336707776812139F0D67E85CB584B29EFEF3DF54CEEF414521A170A316B` |
| `player-father-3d-interaction-result.txt` | `A48FC70C52386AB6AC652C5CBA2D11517F826F08DC067BA8718FD5013DF89D00` |
| `player-father-3d-interaction-final.txt` | `D126FD8872E88D72674DE719869A47EEA54EE298E0BF168B6C97997C4C6537E8` |
| `player-father-foot-tile-trace.csv` | `F2CA2F09C43DF60C0B5E3194C4C41C9F0ED3A7C5C97960F82CDB16CFCDA80D1A` |
| `player-father-foot-tile-trace-y-axis.csv` | `C783B9597289752E1279C9C721B70D66B47E9DD4840591B4030441FDC74053CF` |
| `player-father-foot-tile-sweep-x-axis-result.txt` | `116B6E051AE80282BAA7FFCE51F608366D220B2C48C9A686E66CFFAB1AA5B2A2` |
| `player-father-foot-tile-sweep-y-axis-result.txt` | `4E32C5C4AF207E33CC4A82B90793293FA1144E8071991B6ADD8B6E5019C863A4` |
| `player-father-avoidance.png` | `698D3C4CB034A8C7FD83D812672913CC79A44B2007A14C482BFB1A46248A94AC` |
| `player-father-working.png` | `CB6CF5FC8AE1B334E6CB9AB5EF1D6BA93974D11F26BAC5D1E4EB4459231C4D63` |
