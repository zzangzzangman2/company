# Office Build Editor / Furniture Economy V1

## Branch boundary and repository snapshot

- Isolated worktree: `C:\Users\godho\Documents\Codex\2026-08-14\family-company-build-editor\work\repo`
- Branch: `codex/build-editor-furniture`
- Start HEAD: `715ef105596df015903362fb01e140f987e8b52b`
- Start `origin/main`: `52a787f7c821b3297c1118299bad003089b7362c`
- Canonical `main` was ahead of `origin/main` by one commit. It also had an unrelated dirty
  `OfficeTileMigrationQa.cs` wall-task change; that file was not copied or edited here.
- This branch does not modify the main HUD, wall/door definitions or art, seating claim code,
  stamina/needs policy, or rendering settings.

## Audit: canonical versus legacy

| Concern | Canonical service extended | Legacy/prototype behavior not duplicated |
|---|---|---|
| semantic layout | `Simulation/OfficeGrid/OfficeGrid.cs`, `OfficeGridLayouts.CreateStarterOfficeV1()` | no scene-transform layout state |
| layout saves | `Save/OfficeGrid/OfficeGridSaveAdapter` (sub-schema v4) | no text export as a save source |
| validation | `OfficeLayoutEditRules` pure immutable grid transforms | old F2 immediate sprite/grid mutation path replaced |
| path grid | `OfficeRuntimeOccupancy`, `OfficeRuntimePathService` | no second preview occupancy; ghost never registers |
| interactions | `OfficeInteractionCatalog`, offer resolver/factory, lifecycle reservations | no hard-coded `water-cooler-1` target |
| seating | `OfficeSeatSlot`, runtime seating claims and assigned four workstations | no new seat-claim implementation |
| wallet | `CompanyState` and balanced `LedgerTransaction` | no private editor currency |
| whole save | `GameSaveMapper` top-level schema v8 | v1-v7 remain readable |
| actor preservation | `StarterOfficeRuntimeBootstrap.ApplyLayout` snapshot/diff rebuild | no family respawn or locked-character creation |

The old `OfficeLayoutEditModeController` applied every drag immediately, mirrored rather than
quarter-rotated, offered free deletion, and had no purchase, storage, sale, wallet, transaction,
or save basis. V1 replaces that controller with a preview-first transaction UI while preserving
the canonical Starter runtime rebuild entry.

## Canonical furniture definitions

Every row below is an `OfficeFurnitureDefinition`. Rotation is `SouthEast -> SouthWest ->
NorthWest -> NorthEast`; 90-degree turns swap footprint width/height. `instanceId` is never used
as a definition ID.

| stable definition ID | Korean name | category | base footprint | capability / capacity | access | desired facing | nav | sprite policy | 2000 base KRW | gameplay KRW | resale | maintenance/day base |
|---|---|---:|---:|---|---|---|---|---|---:|---:|---:|---:|
| `desk_with_pc` | CRT 업무 책상 | Work | 2x1 | WorkDesk / 1 | cardinal | SE | block | canonical + exact Resources direction hook; safe provisional guard | 1,350,000 | 337,500 | 35% | 900 |
| `swivel_chair` | 사무용 회전의자 | Seating | 1x1 | Seat / 1 | seat cell | NW | pass | canonical + exact Resources direction hook; safe provisional guard | 160,000 | 40,000 | 55% | 100 |
| `reception_counter` | 접수 카운터 | Work | 2x1 | WorkDesk / 1 | cardinal | SE | block | canonical authored/mirror | 360,000 | 90,000 | 50% | 250 |
| `meeting_table` | 4인 회의 탁자 | Work | 2x1 | WorkDesk / 4 | cardinal | SE | block | canonical authored/mirror | 420,000 | 105,000 | 50% | 250 |
| `document_bookcase` | 문서 책장 | Storage | 1x1 | Filing / 1 | cardinal | SE | block | canonical authored/mirror | 180,000 | 45,000 | 55% | 120 |
| `fax_copier` | 팩스·레이저 복합기 | Equipment | 1x1 | Printer / 1 | cardinal | SE | block | canonical authored/mirror | 2,400,000 | 600,000 | 30% | 2,200 |
| `water_dispenser` | 냉온수 정수기 | Refreshment | 1x1 | WaterSource / 1 | cardinal | SE | block | canonical authored/mirror | 380,000 | 95,000 | 40% | 700 |
| `drink_vending_machine` | 음료·간식 자판기 | Refreshment | 1x1 | DrinkVending / 1 | cardinal | SE | block | exact Resources hook; transparent procedural safety guard | 2,600,000 | 650,000 | 45% | 3,000 |
| `sofa` | 2인 휴식 소파 | Rest | 2x1 | Seat, RestSeat / 2 | adjacent/two | SE | block | canonical authored/mirror | 520,000 | 130,000 | 50% | 300 |
| `coffee_table` | 커피 서비스 테이블 | Refreshment | 2x1 | CoffeeSource / 2 | adjacent/two | SE | block | canonical authored/mirror | 210,000 | 52,500 | 50% | 500 |
| `potted_plant` | 실내 화분 | Decoration | 1x1 | none | none | SE | block | canonical authored/mirror | 55,000 | 13,750 | 25% | 100 |
| `partition` | 사무용 파티션 | Divider | 1x2 | none | none | NW | block | canonical authored/mirror | 130,000 | 32,500 | 50% | 80 |
| `filing_cabinet` | 4단 철제 서류함 | Storage | 1x1 | Filing / 1 | cardinal | SE | block | canonical authored/mirror | 220,000 | 55,000 | 55% | 150 |

Structural entrance/wall IDs are cataloged as non-player-editable, non-purchasable records so
the parallel wall branch remains the sole owner.

An `OfficeFurnitureInstanceState` persists stable `instanceId`, `definitionId`, placed/stored
state, origin, quarter rotation, legacy/purchased basis state, gameplay purchase basis, acquired
minute, and idempotent purchase transaction ID. Existing layout furniture migrates as
`LegacyIncluded`; identical definitions can have any number of distinct instances.

## Pricing sources and inference

- Currency/base year: integer KRW, year 2000.
- Statistics Korea's official regional CPI table (2020=100) reports 2000 all-items 64.1 versus
  2024 115.6, and household furnishings 66.5 versus 115.4. That implies roughly 1.805x and 1.736x
  price-level changes, respectively:
  `https://www.kostat.go.kr/boardDownload.es?bid=12000&list_no=437385&seq=3`
- Public Procurement Service explains that Multiple Award Schedule prices are competitively
  contracted and includes office furniture/electronics:
  `https://www.pps.go.kr/kor/content.do?key=00727`
- A current official Nara Marketplace office-furniture specification gives a 22-item procurement
  base total of KRW 35,302,630, used only as a modern institutional-furniture order-of-magnitude
  cross-check, not as item-by-item truth:
  `https://www.g2b.go.kr/pn/pnp/pnpe/UntyAtchFile/downloadFile.do?bidPbancNo=R25BK01027373&bidPbancOrd=000&fileSeq=2&fileType=`

Inference: modern institutional order-of-magnitude values were divided by the relevant official
CPI ratio, reconciled against the existing KRW 5,000,000 starting company scale, and rounded to
reasonable KRW 5,000/10,000 base-year anchors. Real 2000 reference values remain in each
definition. A single explicit `GameplayPriceScaleBasisPoints = 2500` (25%) is then applied to
every item; there are no hidden per-item multipliers.

Purchases debit `office_furniture_assets` and credit cash. Purchased-asset sales debit cash,
debit explicit disposal loss, and credit the original asset basis. Migrated legacy items use a
separate sale-income account because no historical asset-opening entry existed. Failed placement,
cancel, insufficient funds, and repeated command IDs change cash by zero.

## Placement and transaction invariants

- Buildable cells are restored interior walkable-floor cells only. Visible non-walkable perimeter
  floor and structural wall cells never become buildable.
- Entire rotated footprint must fit; player furniture cannot overlap another player furniture.
- The canonical interior entrance `(8,1)` must remain open.
- BFS from the entrance must reach all four assigned seat approaches and at least one access cell
  for every capability-bearing facility.
- Desk/chair workstation rotation turns desk footprint, chair, seat facing, approach cell, and
  subcell operator anchor as one immutable grid transform.
- Preview grids never call runtime occupancy, path reservation, or wallet methods.
- Purchase computes layout and inventory first, posts the ledger once, then swaps layout/inventory
  into `GameState` together. Same transaction ID and instance is idempotent.
- Move/rotate/store are free. Active interaction or seat claim causes refusal. The four assigned
  family workstations cannot be stored or sold.
- Confirmed runtime apply uses the existing actor snapshot rebuild; family identity, transient
  autonomy, energy, seat and contract data are not reconstructed by build mode.

## UI and integration contracts

The editor includes category filtering, thumbnail, name, price, owned/placed counts, footprint,
capability, current cash, post-confirm cash, green/red footprint and sprite ghost, exact Korean
failure reason, R rotation, confirm, ESC/right-click cancel, storage, sale confirmation, and a
resolution-scaled right panel. It pauses `Time.timeScale` and the main bootstrap while open and
restores both exactly on close.

Company-hub / main-UI integration (no sixth bottom tab):

```csharp
const string id = OfficeBuildEditorNavigationAdapter.EntryId; // company.hub.build_editor
if (!OfficeBuildEditorNavigationAdapter.TryOpen(id, out string failure))
    ShowToast(failure);
```

The company HUD branch owns the route `office world -> 회사 -> company hub -> 건축·편집 card`.
This branch does not edit its files.

Stamina/needs integration:

```csharp
var query = new OfficeRuntimeFurnitureCapabilityAdapter(starterRuntime, gameState);
var water = query.FindAvailableForAgent(
    OfficeFurnitureCapability.WaterSource, memberId, currentCell, agentRadius);
```

The same API supports `DrinkVending` and `RestSeat`; it returns only placed, statically reachable
instances with a claimable capacity slot. Need thresholds and recovery amounts remain owned by
the stamina branch.

## Built-in image generation audit

Existing canonical PNGs were inspected and retained for desk/CRT, chair, reception, meeting,
bookcase, fax/copier, water dispenser, sofa, coffee table, plant, partition, and filing cabinet.
Only vending-machine kind art was absent. The built-in `image_gen` path was used (no CLI/API
fallback) with the canonical water dispenser and fax/copier as style-only references.

Exact four-direction generation prompt template (the direction phrase was independently replaced
with SE/SW/NW/NE):

> Use case: stylized-concept. Asset type: single isometric game furniture sprite. Primary request:
> a year-2000 Korean office beverage-and-snack vending machine, rotated so its operating front with
> product window, coin slot, and dispensing bay faces [DIRECTION]. This is the [SUFFIX] rotation in
> a fixed 3/4 isometric game camera. Style/medium: match the two provided canonical Family Company
> office furniture images: warm, casual, crisp 2D pixel-friendly isometric rendering, compact chunky
> shapes, mint/cream/warm brown palette, restrained highlights, hard clean alpha edges. The provided
> water dispenser and fax copier are style, scale, camera, lighting, pixel-density, and palette
> references only; they are not edit targets and must not be copied as the requested object.
> Composition: one machine only, fixed project 2:1 isometric camera, exactly one 1x1 floor-tile
> footprint, centered ground pivot near the bottom-center, consistent apparent size with the
> reference water dispenser, generous transparent padding on a landscape sprite canvas. Lighting:
> upper-left soft office light consistent with references; rotate object geometry honestly while
> keeping the world light fixed. Constraints: genuinely transparent RGBA background, no text, no
> letters, no numbers, no brand, no logo, no watermark, no character, no floor or tile baked in, no
> cast shadow outside its footprint, no scenery, no border, no other objects, clean readable
> silhouette. Do not fake this rotation with a horizontal flip when buttons, product window, depth,
> lighting, or perspective would be wrong.

Generated source paths:

- SE `C:\Users\godho\.codex\generated_images\019ffe03-7dc2-7751-99e4-1806af95adc6\exec-7ae347ac-213e-4fc0-9ef9-9f9eb71b23d6.png`
- SW `...\exec-b4b4510e-930c-4ec4-a0ce-1200e99ac1bb.png`
- NW `...\exec-61edb167-1029-4971-a427-5ba6615a3fac.png`
- NE `...\exec-d2c1df70-264b-4071-88c5-32f4a6f14e65.png`
- per-direction alpha-removal edits:
  `C:\Users\godho\.codex\generated_images\019ffe03-7dc2-7751-99e4-1806af95adc6\exec-70c0f69b-8d84-4570-9b7f-70856d7c93c4.png`,
  `C:\Users\godho\.codex\generated_images\019ffe03-7dc2-7751-99e4-1806af95adc6\exec-c7bf7bf8-200a-49a7-967c-220e5505f0c2.png`,
  `C:\Users\godho\.codex\generated_images\019ffe03-7dc2-7751-99e4-1806af95adc6\exec-98add20b-754a-4008-9988-1acd2c708dc4.png`,
  `C:\Users\godho\.codex\generated_images\019ffe03-7dc2-7751-99e4-1806af95adc6\exec-82bd2fd3-f0c4-41ed-ae6f-65658b0437c2.png`
- strict alpha probes:
  `C:\Users\godho\.codex\generated_images\019ffe03-7dc2-7751-99e4-1806af95adc6\exec-3e47a8bb-ad8b-426e-9ae4-e378a3de9716.png`,
  `C:\Users\godho\.codex\generated_images\019ffe03-7dc2-7751-99e4-1806af95adc6\exec-9fe3569e-6305-4048-819f-03218bf9e7dd.png`

All ten outputs were visually inspected. Alpha histograms were also measured: every output was
`alpha min=255, max=255`, with zero transparent pixels. Checker/white/black backgrounds were baked
in, so no generated image was accepted or copied into `Assets`; consequently there is no false
claim of a clean alpha asset or stable image GUID. A small genuinely transparent procedural
vending sprite is retained as a runtime safety guard, and exact Resources IDs
`OfficeBuildFurniture/<kind>_<se|sw|nw|ne>` remain ready for approved PNGs. This art blocker must be
closed before final-art approval; it does not block the semantic/runtime editor candidate.

## Canonical ground geometry and movement hand-off

`OfficeFurnitureDefinition.Geometry` is now the engine-independent source of truth for player
furniture geometry. Every profile is authored once facing SouthEast and deterministically rotated
to SouthWest/NorthWest/NorthEast. It contains:

- semantic footprint dimensions for that rotation;
- a floor-contact polygon in quarter-cell units and collision rows baked from that polygon;
- a separate visual-occlusion ground envelope, reference 320x160 isometric pixel projection, and
  occlusion height (sprite alpha is never read as collision);
- per-capacity interaction access anchors with the actor's desired facing;
- per-seat front/left/right egress sockets.

All 13 player-editable definitions have deterministic four-direction profiles. Placement topology rejects every interaction slot
whose authored access alternatives are blocked/unreachable and every seat slot whose front/left/
right egress alternatives are blocked/unreachable.

Movement integration is deliberately read-only:

```csharp
IReadOnlyOfficeFurnitureGeometryQuery geometry = OfficeFurnitureGeometryQuery.Shared;
OfficeFurnitureGeometrySnapshot snapshot = geometry.Resolve(placedFurniture);
// snapshot.Profile.BakedSolidGroundRows: 4x4 subcells per tile
// snapshot.WorldSolidGroundPolygon: exact world quarter-cell ground polygon
// snapshot.InteractionAccessSockets / snapshot.SeatEgressSockets: claim candidates
```

The movement owner must replace the legacy `OfficeFurnitureCollisionCatalog.asset` lookup with
this query while keeping path, reservation, interaction, and seat lifecycle in its existing
services. This branch does not duplicate those lifecycles.

## QA status

Passed with Unity 6000.3.21f1:

- clean Bee compile of Simulation, Save, Presentation and Editor assemblies;
- `OFFICE_FURNITURE_BUILD_SYSTEM_QA: PASS`: all 13 purchasable definitions x four geometry
  rotations; every definition's purchase, 90-degree rotate, move, store, stored placement and exact
  sale refund; exact single debit/idempotency; funds failure; stored state/schema v8 round trip;
  v7 migration; collision/occlusion separation; access/egress/path/entrance checks; family ID and
  energy preservation;
- `OFFICE_BUILD_EDITOR_RUNTIME_QA: PASS`: actual Prototype PlayMode company-hub adapter open,
  timescale/bootstrap pause, vending purchase and actor-preserving runtime rebuild, rendered Sprite
  at the semantic tile anchor, reachable `DrinkVending` capability, four canonical actors, and exact
  pause restoration on close;
- `FAMILY_COMPANY_OFFICE_GRID_T1_VALIDATION: PASS`;
- `OFFICE_FURNITURE_TILE_SNAP_VALIDATION: PASS`;
- `OFFICE_ISOMETRIC_DEPTH_VALIDATION: PASS`;
- `OFFICE_LAYOUT_EDIT_RULES_VALIDATION: PASS`;
- runtime interaction offer/lifecycle and occupancy-presence validations: PASS;
- `PrototypeValidation.Run`: PASS;
- `git diff --check`: PASS.

Observed pre-existing/non-scope validation failures were not hidden: `ManagementLoopValidation`
fails its unchanged starter-contract completion assertion, `OfficePresentationMicroActionValidation`
fails unchanged step-vs-jump serialized equality, and `OfficeSeatingSaveValidation` fails its
unchanged Unity `JsonUtility` missing-field-null assumption. The successful Prototype validation
also exercises the current contract/runtime path. PlayMode emitted one Unity SearchDatabase
`ArgumentOutOfRangeException` while indexing, after which the runtime QA completed and exited 0.

Candidate readiness: **ready for integration with declared owners**. The company-HUD owner still
needs to call `OfficeBuildEditorNavigationAdapter`; the movement owner still needs to consume the
read-only geometry query without changing its reservation/seat lifecycle. Approved transparent
four-direction vending PNGs remain final-art polish; the runtime uses a transparent procedural
fallback today. Starting base is `715ef105596df015903362fb01e140f987e8b52b`. No main merge,
remote push, Windows release build, or user deployment was performed.
