# Shared Stamina and Recovery Integration Plan

Status: implementation candidate complete on `codex/stamina-needs-sim`, based on `9ad8eb7` and
validated locally after build-editor candidates `e056812` and `a7bdc7`. It has not been merged or
pushed.

## Canonical ownership

- `CharacterStaminaRoster` is the intended pure `GameState` child for family members and future
  employees. Character IDs resolve through one common default profile plus optional catalog
  overrides; no `FamilyRole` switch or per-character code is allowed.
- Existing `FamilyMemberState.Energy` is the current Korean `체력` value already used by saves,
  contracts, autonomy, and UI. Integration must make it a compatibility percent projection of the
  new canonical units, not a second independently mutable meter.
- Existing `EmployeeStats.Stamina` remains a static legacy ability rating until its effect is
  migrated into profile data. It must not become another current stamina source.
- The dormant `Simulation/AutonomyNeeds` energy basis-points state must remain disconnected or be
  retired. Connecting it in parallel would double-own and double-drain energy.

## Common defaults

| Data | Default |
| --- | ---: |
| max / initial | 10,000 / 10,000 units |
| recovery intent threshold | remaining 25% (2,500 units) |
| post-recovery resume floor | remaining 35% (3,500 units) |
| caution color threshold | remaining 50% (5,000 units) |
| Idle / OffDuty / Sleep | 0 units per GameTime minute |
| Walking | 4 units per GameTime minute |
| DeskWork / Typing / Meeting | 12 / 16 / 16 units per GameTime minute |
| Administration / Reception / Printing | 11 / 10 / 10 units per GameTime minute |
| OutsideWork | 14 units per GameTime minute |
| Water | 4 GameTime minutes, 3,500 units on successful Complete |
| Restroom | 8 GameTime minutes, 4,000 units on successful Complete |
| Lounge | 20 GameTime minutes, 5,000 units on successful Complete |

Every recovery definition must lift even zero stamina to its profile's 35% resume floor. This leaves
a default 10% hysteresis above the departure threshold and prevents an immediate
leave/return/leave loop. Values remain data-only and may be overridden by
character or employee ID together with max, initial, threshold, and drain rates.

## Authoritative time and decisions

- Stamina stores its current activity and advances only across explicit GameTime activity segments.
  A frame clock, `Time.deltaTime`, and Unity `Time.timeScale` are never inputs.
- Long advances use bounded arithmetic rather than a per-minute loop. The simulation yields at the
  first 25% crossing or Performing-duration boundary so the coordinator can process the runtime
  decision before advancing the remaining GameTime.
- `CharacterStaminaRoster` keeps all members on one processed-minute invariant and advances every
  member atomically to the earliest decision boundary.
- Final `SimulationRunner` integration must replace its current "advance global time to the final
  target first" order with an earliest-boundary loop: preview boundary, advance global `GameTime`
  and all simulations only to that boundary, process activity/runtime decisions, then continue.
  Otherwise a threshold at minute N would be observed only after global time had already passed N.
- Exact threshold comparison uses integer cross multiplication. A non-round override such as
  max=101 does not treat 26/101 as 25%.

## Runtime lifecycle contract after seating integration

Building/furniture task `019ffe03-7dc2-7751-99e4-1806af95adc6` owns the finalized read-only
capability and live furniture-instance query API. Until that API lands, stamina defines only
`IStaminaRecoveryCapabilityQueryAdapter`; it does not copy the building capability types or maintain
a fallback furniture catalog. At integration, the adapter maps external `WaterSource` and
`DrinkVending` results to Water candidates and external `RestSeat` results to Lounge candidates.
Every result is transient and tagged with its source revision. Candidate instance IDs may be used to
make deterministic advisory ordering stable, but no instance, location, cell, or capability is stored
in a stamina plan or save.

The post-B coordinator must map exact runtime transitions, correlated by the stamina request key and
stamped with current `GameTime.ElapsedMinutes`:

1. `RecoveryRequested`: query live Water, Restroom, and Lounge availability through the finalized
   building/furniture adapter. Restroom remains pending the post-A semantic/layout integration.
2. Deterministically choose only the semantic interaction kind. The existing interaction lifecycle
   re-queries the current capability view, then atomically selects the concrete furniture instance,
   offer, and approach cell and owns its claim. Advisory capacity is never treated as reservation.
3. After reservation success, accept the stamina plan and pause assigned work while preserving task
   ID and remaining work.
4. Existing safe work stop completes, then StandUp and seat exit complete.
5. Navigation begins with the correlated interaction handle.
6. Live arrival validation succeeds, facility facing alignment succeeds, and Performing begins.
7. Performing consumes its configured GameTime duration. No stamina is granted yet. The coordinator
   must receive a true pure `CanCompleteRuntimeInteraction` preflight before asking runtime to
   release the claim; an early runtime Complete is forbidden.
8. The runtime lifecycle successfully completes and releases the claim. In the same correlated
   GameTime transaction, and only then, stamina
   commit the configured recovery once.
9. Force the current assigned-seat interaction. An exact-seat Working event leaves preserved work
   paused; confirm the pure recovery lifecycle first, then issue the correlated runtime resume
   command for the saved task and remaining work.

Required post-B runtime seam:

- request-key-correlated transition/terminal events from `OfficeRuntimeAgent` for safe stop, stand,
  navigation start, validated arrival, alignment/Performing, completion, abort, and assigned-seat
  Working;
- an explicit recovery Complete/Abort API whose result proves claim release before the pure state is
  changed;
- assigned-contract pause/resume that preserves remaining work and blocks production advancement
  throughout recovery;
- stale event rejection and exact authoritative-minute ordering (`AdvanceTo(eventMinute)` before
  applying the event).

Failure ordering is always runtime terminal Abort/release first, then pure abort/reselection. A
failed return releases its transient handle and produces a new assigned-seat return key. Layout
rebuild, disable, destroy, and path failure must leave zero claims.

## Save/load boundary

- Top-level save version is v9 with a distinct nested `staminaState` roster. Do not reuse the
  existing `stamina` field, which is the static `EmployeeStats.Stamina` value.
- v1-v8 migration converts legacy `energy` percent into profile units and seeds processed time from
  save `elapsedMinutes`, preventing historical drain replay.
- v9 stores semantic units, current activity, processed minute, recovery phase/progress, request and
  retry counters, and the exact profile fingerprint. It never stores claims, concrete facilities,
  queried capabilities/instances, paths, transforms, or scene objects.
- Because runtime claims are transient, load normalizes SafeStopping through Performing to
  `RecoveryRequested`, discards uncommitted performance progress, and requires fresh reservation,
  arrival, facing, Performing, and Complete. A recovery already successfully completed may retain
  `ReturningToAssignedSeat` and reacquire the assigned-seat route.

## Restroom future capability

The current office has claimable water/vending and sofa facilities but no restroom definition or
placed facility. The adapter therefore exposes no restroom candidate and never teleports to a
synthetic location. `Restroom` remains a fail-closed future capability until a separate placed,
reachable, capacity-owning facility is implemented.

## Overhead presentation

- One auto-installed presenter consumes only `ICharacterStaminaReadModel` and intersects it with
  `StarterOfficeRuntimeBootstrap.Actors`. Future registered employee actors therefore receive bars
  without code changes.
- One near-camera WorldSpace canvas contains all runtime-generated bars. It has no text,
  `GraphicRaycaster`, or per-character scene objects. The ScreenSpaceOverlay office/management HUD
  remains above it.
- The presenter follows projected `SpriteRenderer.bounds`, keeps an output-pixel-sized 48x8 shell,
  remains visible at 100%, and switches stable teal / caution yellow / critical red by integer basis
  points. It hides away, disabled, destroyed, perspective-camera, and offscreen actors.
- Debug snapshots expose ID, current/max/ratio, processed minute, lifecycle phase/activity,
  visibility, world anchor, screen rectangle, and actor depth order without production text.
- Strict furniture occlusion would require the shared depth graph and is deferred until B lands. The
  independent policy keeps a present/on-camera actor's bar readable above world furniture while HUD
  remains unobstructed.

## Deferred shared-file integration and QA

Likely shared integration files after rebase include `FamilyMemberState`, `PrototypeStateFactory`,
`AutonomousOfficeSimulation`, `SimulationRunner`, `GameSaveDto`, `GameSaveMapper`,
`OfficeAutonomyCoordinator`, `OfficeRuntimeAgent`, runtime contracts/workstation coordination,
interaction catalog/semantic enums, and the post-A office layout/content catalog. These must not be
edited in the independent phase.

The independent .NET harness currently covers common defaults/overrides, exact 26%/25% behavior,
activity-segment and frame partition determinism, GameTime pause, threshold yields, long jumps,
atomic roster clocks, semantic planning, all three completion-only lifecycles, stale keys,
abort/reselection, return retry, runtime-loss save normalization, profile fingerprints, legacy
migration, future employee fallback, transient-state exclusion, and large-value overflow guards.

Unity 6000.3.21f1 gates pass for pure stamina, GameState 1x/2x/4x partitions and v9 roundtrip,
prototype/micro-action determinism, furniture build/query, and PlayMode runtime integration. The
runtime QA proves claim visibility, deletion fail-closed behavior, sticky recovery across three
routine-autonomy refreshes, Performing-only completion/release, exact assigned-seat/task return,
and four visible family bars; its capture and summary are in `Artifacts/StaminaRuntimeQa/`.
