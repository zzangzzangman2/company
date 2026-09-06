# First business loop — local, unreleased evidence

Base source: `ee3e812fc9feebbb90f6aea83cb77dcc35320bcf`, main working changes, Unity 6000.3.21f1.
This is not a production release receipt. The latest public game remains `fc-win-20260906.3`.

## Passed

- Pure simulation/save-mapper suite: `Artifacts/FastQa/runs/20260906-235639-393`, 8.336 seconds.
  [Raw summary](pure-summary.txt). Four-member common settlement, exactly-once rewards, time gates,
  pinned lesson/retry, own development/failure/retry, learned-technology benefit without new grants,
  trial sale, maintenance/billing/deadline/missed period/zero-customer recovery, save migration.
- Final focused Editor suite: `Artifacts/FastQa/starter-final-validation-2.log`, execute-method 11.314 seconds.
  OfficeNavigationValidation (1.823s), OfficePresentationMicroActionValidation (0.153s),
  OfficeRuntimeOccupancyPresenceValidation (0.084s), OfficeRuntimePathCacheValidation (0.057s),
  OfficeSharedLocomotionStrictValidation (0.262s), PrototypeValidation including starter/JSON (8.736s),
  WorkforceCapabilityValidation (0.193s). All seven methods PASS and Editor reports PASS.
- Actual D3D11 FastQA Player: `Artifacts/FastQa/StarterProduct/20260906-235524`.
  [Raw result](player-result.txt), [sampled work trace](physical-work.csv),
  [actual office](physical-work.png), [actual progress UI](first-work-progress.png).
  Normal empty new game; four sets bought through transaction API. First lesson accepted and father
  assigned through real UI raycast + managed pointer events. No native input, route/pose injection,
  time jump or save writes. Four-times normal clock: 264 elapsed game minutes, including 248 required
  desk minutes and 248 observed desk minutes. 3,678 seated samples, 241 travel samples, 4 person-hours
  credited. Earlier successful run `20260906-234921` independently reached the same 4-hour block.
- The background host verified the fixed user main, five saves/backups, and interactive desktop were
  unchanged. Private desktop was never activated. No public cache update, publish, push or shutdown.
- Initial/assigned/progress product card text-overflow checks pass at 1280x720. Screenshots were inspected.

The Player used the build from `20260906-235349-373` (18.755s). After that run, retry offer cloning was
encapsulated into `SubcontractOffer.WithOfferId` with identical terms, and three existing save-version
assertions were updated to v12. Those edits are covered by the final pure/Editor tests above; the run is
not misrepresented as a test of a later binary. No work/nav/UI behavior changed after this Player run.
Final current-source FastQA scripts build also PASS: `20260906-235739-460`, 16.159 seconds total,
13.951 seconds build. This compile/build is not an additional Player observation or release approval.

## Failures found and resolved in this implementation

- Small-window HUD used a stale CanvasScaler value: new product labels overflowed. The layout now uses
  the same current screen-derived scale as the font calculation; viewport remains scrollable.
- Assigned father stayed blocked beside the stationary player for the entire workday. The planner
  tested only the peer's nearest cell, while movement tested body radius. Dynamic route searches now
  reject swept edges obstructed by adjacent bodies. Assigned tasks use dynamic planning from the start;
  stationary stale intent does not permanently retain moving-peer priority. Existing radii/tolerance,
  tile-centre rails, models/poses and furniture are unchanged. Editor test proves the detour edges are
  executable with `CanMove`; actual Player proves arrival and useful work.
- Travel/alignment delay must not be credited as desk work: assigned work clock advances only during
  actual work, checked by the required/observed 248-minute assertion.
- An extra regression caught retry business code reading the obsolete RequiredSpeed property. Offer
  cloning now owns compatibility fields; the original boundary assertion remains intact and passes.

## Not certified

- No native purchase/rotation click certification and no full-week business loop via normal Player.
  The full lifecycle is a pure simulation test with explicit work contributions, not proof of actual
  four-family production play. Player verification covers father's first 4/26-person-hour block only.
- No fresh full four-character walk/seated production gate or public update/restart gate for this content.
  Release QA and explicit deployment approval remain separate.
- Broad suite `Artifacts/FastQa/runs/20260906-231520-138` is NOT green: it stops at the pre-existing
  OlderSister candidate albedo (`older-sister-v3-higgsfield-sd-repair-albedo.png`) imported as Compressed
  where the validator requires CompressedHQ or Uncompressed. This untouched candidate was not changed
  to make a content task pass. Earlier strict heap-growth failure did not recur in the final focused run;
  its threshold was not relaxed.

See [workflow and reproduction](../../STARTER_BUSINESS_LOOP.md).
