# Office Build Editor / Furniture Economy V1

## Current integration status

- Build editor와 MainNavigation adapter는 현재 main 계보에 통합되어 있다.
- 가구 재고는 전체 저장 스키마 v8에서 도입되었고, 현재 전체 스키마는 v10이며 v1~v9를 읽는다. OfficeGrid 하위 스키마는 v4, 가구 재고 하위 스키마는 v1이다.
- 진입점은 `사무실 → 회사 → 사무실 관리`다. 하단 여섯 번째 탭이나 별도 wallet/save를 만들지 않는다.
- 실제 새 게임은 바닥·외곽만 있는 빈 13×13 사무실로 시작하고 카테고리별 가구를 여기서 구매·배치한다. furnished `StarterOfficeV1`은 기존 저장/QA fixture다.
- 배치 geometry는 `OfficeRuntimeOccupancy`가 read-only query로 직접 소비한다. 알려진 가구는 canonical 4방향 profile, 이전 저장의 미등록 콘텐츠는 부분 legacy profile 없이 전체 셀 차단 fallback을 사용한다. 최종 seating/stamina 결합과 portable build 상태는 [PROJECT_STATE.md](PROJECT_STATE.md)를 따른다.

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

Structural entrance/wall IDs are cataloged as non-player-editable, non-purchasable records. The
perimeter layout/presentation boundary remains their sole owner.

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
- Placement origin is always an integer tile. A 1x1 item is anchored at that tile center and a
  multi-tile item at the exact center of its complete rotated footprint; pointer/world coordinates
  never become persistent placement state.
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

`MainNavigationV2`가 route `office world -> 회사 -> company hub -> 사무실 관리 card`를 소유하고 위 adapter를 호출한다. 편집기가 열린 동안만 시뮬레이션을 멈추고 닫으면 회사 허브로 돌아간다.

Stamina/needs integration:

```csharp
var query = new OfficeRuntimeFurnitureCapabilityAdapter(starterRuntime, gameState);
var water = query.FindAvailableForAgent(
    OfficeFurnitureCapability.WaterSource, memberId, currentCell, agentRadius);
```

The same API supports `DrinkVending` and `RestSeat`; it returns only placed, statically reachable
instances with a claimable capacity slot. Need thresholds and recovery amounts belong to the
stamina/needs simulation, whose final main integration is pending in [PROJECT_STATE.md](PROJECT_STATE.md).

## Built-in image generation audit

Existing canonical PNGs were inspected and retained for desk/CRT, chair, reception, meeting,
bookcase, fax/copier, water dispenser, sofa, coffee table, plant, partition, and filing cabinet.
Only vending-machine kind art was absent. The built-in `image_gen` path was used (no CLI/API
fallback). The canonical office target, water dispenser and fax/copier were inspected as local
style/camera references; the accepted vending design was then rotated from one consistent design.

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
> reference water dispenser, generous padding on a landscape sprite canvas. Lighting:
> upper-left soft office light consistent with references; rotate object geometry honestly while
> keeping the world light fixed. Constraints: a flat uniform magenta chroma field, no text, no
> letters, no numbers, no brand, no logo, no watermark, no character, no floor or tile baked in, no
> cast shadow outside its footprint, no scenery, no border, no other objects, clean readable
> silhouette. Do not fake this rotation with a horizontal flip when buttons, product window, depth,
> lighting, or perspective would be wrong.

The first transparency attempts were correctly rejected: their histograms were fully opaque or
contained baked checker/white/black fields. The accepted retry used an opaque magenta-only field.
The generator quantized the nominal `#FF00FF` field slightly per image, so no hand-authored alpha
estimate was used. The repository's official border-key tool sampled each field and ran exactly:

`remove_chroma_key.py --auto-key border --soft-matte --transparent-threshold 18 --opaque-threshold 210 --despill --edge-contract 1`

The four chroma and alpha sources are stored under
`Assets/Art/Office/Tiles/Furniture/Source/office_drink_vending_machine_<se|sw|nw|ne>_*_v1.png`.
`OfficeBuildVendingArtBuilder` deterministically creates exact Resources IDs
`OfficeBuildFurniture/drink_vending_machine_<se|sw|nw|ne>` as 640×512 RGBA hard-alpha Sprites,
180 PPU, Point, mipmap disabled, uncompressed, pivot `(320,28)`. SE/SW visibly expose opposing
operating fronts; NW/NE expose the opposing rear+side surfaces, so none is a fake label or runtime
flip. Visual inspection passed on the generated chroma sources and transparent runtime outputs;
automated QA found zero visible magenta-fringe pixels. The procedural sprite remains only as a
missing/corrupt-resource safety guard and is not selected when the approved runtime asset exists.

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

Runtime movement now resolves this query while keeping path, reservation, interaction, and seat
lifecycle in their existing services. A saved kind/facing absent from the canonical query is blocked
as a full rectangle rather than inheriting a partial legacy mask. The editor does not duplicate those
lifecycles.

## QA status

Passed with Unity 6000.3.21f1:

- clean Bee compile of Simulation, Save, Presentation and Editor assemblies;
- `OFFICE_FURNITURE_BUILD_SYSTEM_QA: PASS`: all 13 purchasable definitions x four geometry
  rotations; every definition's purchase, 90-degree rotate, move, store, stored placement and exact
  sale refund; exact single debit/idempotency; funds failure; stored state/schema v8 round trip;
  v7 migration plus 52 schema-v3 geometry round trips; collision/occlusion separation;
  access/egress/path/entrance checks; family ID and energy preservation;
- `FAMILY_COMPANY_OFFICE_FURNITURE_COLLISION_QA: PASS`: 10,368 direct/path cases across 8
  directions, four family radii, 30/60/120fps and 1x/2x/4x; 52 canonical profiles, 1,216 subcell
  checks, 16 full-cell fallback subcells, 416 actual Sprite-alpha attack paths, visible pass-through
  and resolved-endpoint penetration zero;
- `OFFICE_BUILD_EDITOR_RUNTIME_QA: PASS`: actual Prototype PlayMode company-hub adapter open,
  timescale/bootstrap pause, vending purchase and actor-preserving runtime rebuild, rendered Sprite
  at the semantic tile anchor, reachable `DrinkVending` capability, four canonical actors, and exact
  pause restoration on close;
- `OFFICE_BUILD_VENDING_ART_QA: PASS`: four unique real rotations; exact directional Resources
  selection; deterministic 640×512 RGBA hard-alpha runtime outputs; 180 PPU; `(320,28)` ground
  pivot; Point/no mip/uncompressed importer; front/rear classification; magenta fringe zero;
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

Current status: `MainNavigationV2` calls `OfficeBuildEditorNavigationAdapter`, four-direction vending
art is loaded through the additive Resources hook, and movement/path occupancy consumes the
read-only canonical geometry query without changing reservation/seat lifecycle. Final release state
and Windows build identity remain governed by [PROJECT_STATE.md](PROJECT_STATE.md).
