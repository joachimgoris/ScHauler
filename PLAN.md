# ScHauler — Phase 2 Plan: Cargo Containers, Stowage & Route Ordering

## Working agreement (read first, applies to every session)

- **Joachim writes all code by hand.** Your role: design review, diagnosis,
  explaining trade-offs, and running `dotnet build` / `dotnet test` to verify
  his transcriptions. **Do not edit or create source files** unless he
  explicitly says "write this one" for a specific file.
- When his code diverges from the plan, say so and show the diff — don't
  silently fix it.
- Respect CLAUDE.md conventions: strict analyzers stay on (per-rule
  .editorconfig suppressions only, with comments), DDD style is deliberate,
  NUnit constraint model, protanopia-safe UI (blue↔orange semantics + text
  verbs, never color alone).
- Data is disposable (contracts reset per game session). Schema changes =
  delete `data/hauler.db` and restart. Do NOT introduce EF migrations.

## Context

Personal Blazor Server (.NET 10) app tracking chained Star Citizen hauling
contracts. Model: Contract → CargoLine → CargoContainer (game-dictated box
sizes 1/2/4/8/16/24/32 SCU). ManifestPlanner (pure, tested) groups actionable
lines by location. Only ship in use: **Drake Ironclad**.

## Decision log

### 2026-08-31

- **Line pickup and delivery are atomic.** All of a line's boxes move
  together; partiality exists only at line granularity. Containers carry no
  status; `CargoLine.Status` stays stored with `Advance()`/`Correct()`.
- **Lost boxes ⇒ contract cancelled in-game ⇒ hard delete** (cascade). No
  Abandoned status.
- **`ContainerSize` is a smart enum** (sealed class, fixed instances,
  `FromScu`, int converter). Invalid sizes unrepresentable.
- **Containers mandatory at line creation** (`AddLine` takes a non-empty
  sizes list). **Line SCU is derived** from containers.
- **Correcting a line out of PickedUp clears stowage state** (originally
  zones; now placements — the boxes left the hold).

### 2026-09-02

- **Self-validating properties where possible**: purely per-property
  invariants live in a private-set full property with a validating setter;
  factories/methods assign without re-guarding. Cross-object invariants
  (uniqueness, status-gated edits) stay in factories/behavior methods.
  Retrofit opportunistically. Caveat: EF materializes via backing field, so
  setters don't run on load.

### 2026-09-03 — stowage supersedes zones

- **Freeform zones are dead** (never shipped). Joachim already hand-draws a
  stowage plan in a notepad every session; zone names are not specific
  enough. Replaced by **computed stowage**:
  - The app **plans** placements; Joachim loads the ship to match (as with
    the notepad). No manual coordinate entry in v1.
  - **Placements are computed once at pickup, stored on the container, and
    frozen** — recomputes never move a box already aboard (incremental
    packing, never global repack). Delivered lines free their cells;
    placement value kept as history.
  - **Packing rules**: same dropOff piles together, packed tight (adjacent +
    stacked); different dropOffs never touch — ≥1 cell clearance, no
    mixed-destination stacking; in bounds; no overlap; no floating boxes;
    boxes never span a walkway (enforced by bays, below); deterministic.
- **The Ironclad hold is four bays** (measured in-game, walkways excluded):
  fwd-port 20×6×6 and fwd-starboard 20×6×6 (ramp side), aft-port 10×6×6 and
  aft-starboard 10×6×6. Total 2,160 cells. Bays are seeded fixed geometry
  with names (labels stay text — protanopia rule), plus drawing offsets so
  walkway gaps render truthfully.
- **Stacking 6-high is easy in-game (confirmed 2026-09-04)** — the planner
  uses the full hold height, no cap.
- **Box footprints in 1-SCU cells (all confirmed in-game 2026-09-04)**:
  1=1×1×1, 2=2×1×1, 4=2×2×1, 8=2×2×2, 16=4×2×2, 24=6×2×2 (added 2026-09-04,
  wiki-confirmed), 32=8×2×2. Boxes can be **rotated horizontally**
  (length/width swap) — a placement concern (`Rotated` flag). Height never rotates.
- **`StopAction` carries the box breakdown** (`BoxCount(Size, Count)` list,
  largest first) — the planner is the single tested source for manifest
  rendering; shown inline under the commodity, no expand needed until A2.
- **Grid decoupled from capacity bar**: hold geometry is for packing/drawing;
  `CargoCapacityScu` stays stored.
- **Capacity = 2,160.** The advertised 2,204 includes the secure cargo
  compartment, which Joachim doesn't use for hauling missions. Seed the
  Ironclad with 2,160 (= the measured 30×12×6 main hold).

### 2026-09-03 (evening) — field report from first real session (implemented + E2E-verified 2026-09-04)

- **The box breakdown is only revealed at pickup, not at accept.** Contract
  terminal shows totals per line; boxes materialize at the pickup point.
  Supersedes "containers mandatory at line creation" and "line SCU derived":
  - `CargoLine.Scu` is **stored** again (the contracted total); `ScuConverter`
    and its convention/property mapping return. Containers start empty.
  - New `CargoLine.PickUp(sizes)`: requires Pending, non-empty sizes, and
    **strict sum == contracted SCU** (game guarantees it; mismatch = typo).
    Replaces any previously entered containers, sets PickedUp.
  - `Advance()` no longer leaves Pending ("use PickUp"); it remains the
    PickedUp → Delivered transition. `Correct` back to Pending **keeps**
    containers (mis-tap shouldn't lose box data; re-pickup is prefilled).
  - Tap counters move from contract entry (back to a total-SCU input) to the
    manifest PICK UP flow as a shared BoxCounters component. Deliver stays
    two taps; pickup = tap → counters → confirm (replaces the notepad).
  - A2 unaffected: pickup is exactly when the stowage planner needs sizes.
- Commodity autocomplete via static list + datalist (Services/Commodities).
- SQLite cannot ORDER BY DateTimeOffset — Contracts page orders client-side.

### 2026-09-07 — cargo-hold redesign adopted (design_handoff_cargo_hold/)

- Manifest cargo hold rebuilt to the design bundle's spec: ramp gutter +
  depth ruler (cells from ramp), container-query cell sizing, visible 1-SCU
  grid, spine/corridor structure hatching, bay label chips with used/capacity,
  true-footprint destination-colored stacks (merged ×n hN labels),
  tap-to-isolate + SHOW ALL. AS LOADED = real frozen placements, always.
- **Deferred to Phase B** (need route order): AS LOADED/SUGGESTED STOW toggle,
  stow check note, stop numbers, route & manifest column with ▲/▼ reorder,
  capacity-band legend chips + per-destination bar segments. The design's
  "suggested stow packer" (last stop deepest, contiguous per destination,
  frontier no-backfill) IS the B2 forecaster spec.
- **Design README correction**: its footprint table lists 24 SCU as 4×2×3;
  game truth (wiki-verified) is 6×2×2 — the repo implementation wins.
- **Protanopia caveat on the color pool** (#f87171/#a3e635/#fb923c/#5fd0a8
  are confusable): acceptable for 2-3 destination runs; if 4+ destinations
  get muddy in the field, add stop-number badges to stacks (B2 has stop
  numbers anyway).
- Sequencing confirmed: **B1 route ordering first, then B2 suggested stow**.

### 2026-09-09 — field decision: stack vertically first

- StowagePlanner prefers towers over layers: candidate ranking is now
  (touches-own-pile, bay, X, Y, Z) — boxes stack to bay height before the
  pile spreads, building a wall outward from the ramp. Was layer-first;
  changed on field feedback (multiple 1 SCU boxes should stack, not carpet).

### 2026-10-21 — B2 breakdown decided

- **Forecast regions are cell sets, not rectangles.** Output is
  `destination → set of (bay, x, y, z)`; the planner consumes it as a
  membership test, the UI projects it top-down. No region geometry type.
- **Depth = `DrawOffsetX + x`** everywhere (forecaster, stow check). The
  draw offset already encodes "cells from ramp" for the ruler, so it is the
  one honest cross-bay depth.
- **Clearance = skip to the next full depth slice** between destinations.
  Coarse but guarantees the planner can honor the region.
- **Drop order is page state, passed to the service per call**, never
  persisted (data is disposable; route order is the default anyway).
- **Ghosts always on, no AS LOADED/SUGGESTED STOW toggle** until field use
  says otherwise.

### 2026-10-21 (afternoon) — the hold has a hard edge

Field run with a 900 SCU contract on top of 1,122 SCU aboard + 192 pending:

- **Restow when the incremental plan fails** (`StowagePlanner.Repack`):
  every aboard box is re-planned from an empty hold, lines in drop order so
  earlier drops end nearest the ramp. Supersedes "placements are frozen"
  for this one case; the manifest shows "RESTOW — N box(es) already aboard
  changed place" so Joachim reloads to match.
- **Capacity is a gate, twice.** A contract whose SCU would exceed the hold
  next to what is aboard or pending is not accepted (NewContract guard). A
  pickup that would exceed capacity, or whose boxes do not fit even after a
  restow, is refused: nothing saved, line stays pending, "NOT LOADED" note.
  Supersedes "boxes stay unplaced with a note" - unplaced boxes can now only
  come from data corrected by hand on the contracts page.
- Geometry beats SCU: 2,022 SCU in 32-SCU boxes does not fit a 2,160 hold
  (61 boxes, 54 slots). The accept-time guard is SCU-only because box sizes
  are unknown until pickup; the dock-time refusal is the geometric truth.
- **Tiered stow** (same day, after measuring): with the 1-cell moat between
  destinations, a sixth destination next to 1,122 SCU got at most 72 SCU
  whatever the box size - six moated piles fragment four bays. So the moat
  is kept only while there is room: pickup tries *incremental with moat >
  incremental touching > restow with moat > restow touching*, and the
  manifest says "TIGHT STOW" when piles touch. Stacking across destinations
  stays forbidden (support rule). `allowTouching` on `Plan`/`Repack`.
- **Burial is per bay (2026-10-29).** The walkway reaches every bay, so
  forward-bay cargo never buries aft-bay cargo. The stow check, the
  planner's "don't sit in front of an earlier drop" rule and the
  forecaster's frontier cap all compare X within one bay and lane, never
  canvas-wide. Removed three false "buried behind Seraphim" warnings on the
  restowed 1,794 SCU load.
- **What the Ironclad takes, measured on the 1,314 SCU / 7-drop load that
  Joachim set up by hand**: a further line maxes at 480 SCU as 16s, 504 as
  8s or 4s, 648 as 1s (tight restow). A 700 SCU line does not fit with any
  real box mix; the dock refuses it in ~250 ms. The waste is inherent to
  32-SCU boxes (8 long in 20- and 10-long bays: 54 slots = 1,728 SCU) plus
  own-destination-only support above partial towers.

### 2026-10-29 (afternoon) — Phase C decided

Two field annoyances, both small, both opt-in to what B2 already computes:

- **The pinned drop order dies when the ship moves.** It lives in the page
  (`_dropOrder` + `_manualDropOrder`), so every location change and every
  page reload falls back to the route heuristic. Decision: the pin is ship
  state, persisted, cleared only by an explicit "ROUTE ORDER" tap.
- **Geometry decides at the dock, SCU decides at the contract.** A 700 SCU
  line passes the NewContract guard and is refused at pickup. Decision:
  show, for the line being picked up, the largest uniform box size that
  still fits (dry run of the same tiers the dock uses). Shown in the
  pickup dialog first; NewContract stays SCU-only because other pending
  lines are not aboard yet, so any geometry there would be a guess.

### 2026-10-29 (late) — other ships, other users: noted, not decided

Joachim is considering opening the tool to other haulers, which means
other ships than the Ironclad. Where that stands today:

- **Ships are already data.** `Ship` + `ShipBay` (dims, draw offsets) and
  every planner are generic; the only Ironclad-specific code is the seed in
  `LocationSeed` and the "Ironclad" caption in `HoldView.razor`. Another
  ship = another bay layout row set; no planner work. Open questions per
  ship: walkway reach (burial is per bay because the Ironclad walkway
  reaches every bay - a ship with a single deep hold would need a
  canvas-wide rule again) and ramp side.
- **A ship catalog + "active ship" is a small phase** (seed several hulls,
  pick one, capacity and bays follow). The manifest currently takes
  `Ships.OrderBy(Name).First()`.
- **Other users is the big step, not other ships**: accounts, per-user
  contracts and ship, hosting beyond the home server, backups. That is a
  different app shape; decide the product question (shared instance vs
  self-hosted container per hauler) before any code.

### Pending decisions (recommendation noted; confirm when reached)

- **Container edit rules** — implemented: add/remove only while Pending,
  never remove the last container. (Confirmed by tests Joachim wrote.)
- **`ShipSettings` fate** — done: deleted; capacity lives on `Ship` only.
- **Where fit advice shows (C2)** — recommend: pickup dialog only. Upgrade
  path: a "CHECK FIT" tap on a pending line in NewContract/Contracts that
  runs the same dry run against what is aboard *now*, labelled as such.

## Phase A — Cargo containers & ship manifest (✓ COMPLETE 2026-09-03)

Domain rework order (build stays green):

1. **✓ done** `ContainerSize` smart enum.
2. **✓ done** `CargoContainer` — typed id, FK to CargoLine, size. (Zone
   fields to be removed in step 4 cleanup.)
3. **✓ done** `CargoLine` — derived SCU, containers-at-birth,
   add/remove-container guards. Contract-entry tap-counter UI also done.
4. **✓ done** `Ship` (name + self-validating `CargoCapacityScu`, `ShipId`) —
   NO zones. Cleanup: delete `ShipZone.cs` draft, `ShipZoneId`,
   `CargoContainer.ZoneId`/`AssignZone`/`ClearZone`, the four zone-lifecycle
   tests in DomainInvariantTests, and zone tests in ShipTests. Keep
   `Create_RejectsBlankName`, capacity tests.
5. **✓ done** Contract hard-delete flow (cascade) for in-game cancellations.
6. **✓ done** EF: configurations/converters for new types, `AutoInclude()` on the
   containers navigation, delete `ShipSettings` (DbSet, seed,
   ManifestService reads/writes move to `Ship`), seed the Ironclad.
   Delete `data/hauler.db`.
   - **Fix known bug while in there:** `CargoLineConfiguration` maps the
     pickup FK as `HasForeignKey(l => l.PickupLocation)` (navigation) —
     should be `PickupLocationId`.
7. **✓ done** Planner + tests where behavior changes (tallies/StopAction shape). Test
   helpers state status explicitly via `Correct` (lesson from the
   2026-08-30 helper bug).

UI (after 6): manifest PICK UP stays one line-level action; DELIVER expands
to the line's containers; capacity bar counts PickedUp lines' SCU; contract
cancel (hard delete) with confirm step. Two-tap happy path is the bar.

## Phase A2 — Computed stowage (✓ COMPLETE 2026-09-07; E2E-verified two-pile plan)

1. **✓ done** `ContainerSize` gains footprint (L×W×H in cells) after in-game check.
2. **✓ done** `ShipBay` — seeded fixed geometry per ship (name, dims, drawing offset).
   Ironclad: the four measured bays above.
3. **✓ done** `CargoContainer.Placement` — nullable value object (bay id, x, y, z,
   rotated). Set at pickup from the planner's proposal; cleared by
   `Correct` out of PickedUp; kept as history on Delivered.
4. **✓ done (2026-09-07)** `StowagePlanner` — pure, EF-free, deterministic; the packing rules from
   the decision log; incremental (existing placements are immovable input).
   Heavy TDD: this is the algorithmic core. Rewrite tests first.
5. **✓ done (2026-09-07)** UI wiring: `PickUpAsync` applies the plan; top-down SVG hold view per bay, piles labeled with destination text +
   stack-height badge (×N). Pickup flow unchanged (plan recomputes and
   redraws). Manual drag-to-override is explicitly v2.

## Phase B — Route ordering + suggested stow (✓ COMPLETE 2026-10-21)

### B1 — Route ordering (prerequisite for B2)

**Progress: ✓ COMPLETE 2026-09-18 (B2 ✓ 2026-10-21). Steps 1-4 done (hierarchy+RouteNet,
seed+new-location picker, greedy stop ordering, and step 4:
Ship.CurrentLocationId, the manifest "Docked at" selector, implicit set on
pickup/deliver, ordered stops through ManifestService, route-card drop order
defaulting to route order). Dropped deliberately on a laziness pass: the UI
label naming the heuristic and a reset-to-route button — the ▲/▼ steppers
already cover reordering.**

**No coordinates, no pathfinding.** `Location.ParentId` hierarchy
(location → planet/moon → system); hop-based distance: same location 0,
same body 1, same system 2, cross-system 3.

- "Current location" selector on the manifest (completing an action there
  may set it implicitly — discuss).
- Greedy nearest-neighbor next-stop suggestion; tiebreak prefers stops that
  free the most SCU when near capacity.
- Stays in ManifestPlanner (pure, tested); heuristic named in the UI so it
  never looks authoritative.
- Seed data gains parents for existing locations.

### B2 — Suggested stow (the design bundle's remaining scope)

**Progress: ✓ steps 1-5 implemented 2026-10-21 (Claude wrote the code at
Joachim's request — exception to the working agreement), 99 tests green,
verified in the browser with a two-contract run. Three deviations from the
breakdown below, each learned from the live run:**

- **Planner preference is "fewest cells outside the region", not "all
  inside"** — a one-slice slab can never contain an 8-long box, so the
  strict rule sent the 32 SCU box to the ramp. Fewest-outside pulls it to
  the slab's deep edge instead (`OversizedBox_IsPulledToTheReservedRegionsEdge`).
- **Drop order runs the route heuristic over every drop-off, aboard or
  pending** (`DropOrder(lines, net, current, onboard, capacity, manual)`),
  not "deliver stops first, pending alphabetical" — that rule flipped the
  order the moment a line was picked up, which made the forecast lie.
- **Forecaster frontier**: a region never goes deeper than one clearance
  slice short of cargo already aboard for a *later* drop; **stow check is
  lane-aware** (only overlapping canvas-Y lanes can bury each other), so
  side-by-side stacks are not flagged.

Field run with 200-500 SCU contracts (same day, five contracts, 1,122 SCU
aboard) added two planner/forecaster rules and one check:

- **Planner never sits in front of an earlier drop's cargo when any other
  spot exists** (`Plan(..., dropOrder)`; ranking key is now *buries,
  outside, touches-own-pile, position*). Depth and lanes use the same
  canvas convention as the stow check.
- **Forecaster frontier falls back when the cap excludes everything** -
  later-drop cargo already in the frontmost row (depth 0 is the row nearest
  the ramp; the ramp itself is never blocked by hold cargo) must not zero
  the forecast; the stow check already reports that layout.
- **Timing guard**: 15 x 32 SCU boxes into an empty Ironclad plan in ~100
  ms (`FifteenLargeBoxes_PlanWithinATabletTap`, bound 500 ms). A 6.8 s
  figure seen once was browser-harness latency, not the app (41-96 ms end
  to end when measured from the click).

Observed, by design: a multi-line contract is just N destinations (lines
merge into existing destinations by drop-off); same-body sites (New
Babbage, Port Tressler, the microTech depots) come out consecutive in the
drop order because they are 1 hop apart. Each site still gets its own slab
plus a clearance slice - with five drops around one body that costs up to
4 x 72 cells; the `ponytail:` halo note in step 1 is the upgrade path.

Remaining: none for B2. `tests/e2e/suggested-stow.mjs` is written in the
style of the other scripts but needs `playwright` installed to run.

Order was: 1 → 2 → 3 → 4 → 5; build stayed green after each. Steps 1, 2 and
4 are pure + NUnit (tests first, `StowagePlannerTests`/`StopOrderingTests`
helpers were the template).

#### Shared vocabulary

- **Depth** of a cell = `bay.DrawOffsetX + x` (ruler cells from the ramp).
  Aft bays sit at offset 21, so depth is already global across bays — no
  new geometry needed.
- **Cell key** = the planner's existing `(ShipBayId, X, Y, Z)` tuple.
- **Drop order** = destinations in delivery sequence, first delivered first.
  Covers every drop-off of a non-delivered line (aboard *and* pending).

#### 1. `StowageForecaster` (Services/, pure, EF-free, TDD)

```csharp
public sealed record StowForecast(
    IReadOnlyDictionary<LocationId, IReadOnlySet<(ShipBayId BayId, int X, int Y, int Z)>> Regions,
    int UnreservedScu);

public static StowForecast Forecast(
    IReadOnlyList<ShipBay> bays,
    IReadOnlyList<CargoContainer> aboard,   // frozen placements = immovable
    IReadOnlyList<CargoLine> pending,       // Pending status only; others ignored
    IReadOnlyList<LocationId> dropOrder);
```

Algorithm (the design packer: last stop deepest, contiguous, no backfill):

1. Free cells = every bay cell minus cells covered by `aboard` placements
   (reuse the planner's `Cells` enumeration — make it `internal static`
   rather than copying it).
2. Walk free cells once in **(depth desc, bay index, Y, Z)** order — Z
   innermost so a destination fills towers before spreading, matching the
   2026-09-09 field decision.
3. For destinations in **reverse drop order**, take `sum(pending SCU for
   that destination)` cells off the walk (1 SCU = 1 cell, exact). A
   destination with no pending SCU gets no region.
4. **Clearance**: after a destination's last cell, skip the rest of that
   depth slice plus one full slice before the next destination starts. This
   is what keeps the planner's "different destinations never touch" rule
   satisfiable at the border. `ponytail:` wastes ≤2 slices (≤144 cells) per
   boundary; tighten to a 1-cell halo if the hold ever runs short.
5. When the walk runs out, remaining SCU accumulates in `UnreservedScu`
   (UI shows "forecast short by N SCU").

Never stored; recomputed on every manifest read. Deterministic by
construction (single pass over a fixed order).

Tests (all on a `OneBayShip`-style fixture; extend with a two-bay fixture
whose second bay has a larger `DrawOffsetX`):

- `LastDrop_GetsTheDeepestCells`
- `FirstDrop_EndsNearestTheRamp`
- `Regions_AreContiguousInWalkOrder` (no gaps inside one region)
- `Destinations_AreSeparatedByAClearanceSlice`
- `FrozenPlacements_AreNeverReserved`
- `Region_FillsATowerBeforeSpreading` (Z before Y)
- `PendingOnly_PickedUpLinesAreIgnored`
- `Overflow_ReportsUnreservedScu`
- `Forecast_IsDeterministic`

#### 2. Planner honors the reservation (soft preference)

- `StowagePlanner.Plan` gains one optional parameter:
  `IReadOnlySet<(ShipBayId, int, int, int)>? reserved = null` — the picked-up
  line's own region from the forecast (null = today's behavior).
- `FindBest` key becomes `(InRegion, TouchesOwnPile, bay, x, y, z, rotated)`
  where `InRegion = 0` when every cell of the candidate lies in `reserved`.
  Everything else unchanged — greedy fallback is the existing search.
- Tests: `ReservedRegion_IsPreferred_OverTheOrigin`,
  `ReservedRegion_IsSkipped_WhenTheBoxDoesNotFitIt` (falls back, never
  returns null just because the region is too small),
  `NullReservation_BehavesAsBefore` (run the existing determinism test with
  `reserved: null` — no new test needed if the parameter is optional).

#### 3. Drop order crosses the service boundary

Today `_dropOrder` lives in `Manifest.razor` as location *names* derived
from deliver stops only; pending-only destinations are invisible. B2 needs
ids, all destinations, and the service needs the order at pickup time.

- `ManifestPlanner.DefaultDropOrder(stops, lines) → IReadOnlyList<(LocationId Id, string Name)>`:
  deliver stops in route order first, then pending-only drop-offs
  alphabetically. `ponytail:` alphabetical tail; sort it by hops from the
  last stop if it ever picks a silly order in the field. One test:
  `DefaultDropOrder_RouteStopsFirst_ThenPendingDestinationsAlphabetical`.
- `ManifestSnapshot` gains `Destinations` (that list) and `Forecast`
  (`StowForecast`) and `StowCheck` (step 4).
- `GetManifestAsync(IReadOnlyList<LocationId>? dropOrder = null)`: null →
  default order; a manual order from the page is used as-is, with any
  destination missing from it appended in default order (same merge the
  page does today in `SyncDropOrder`, moved into the planner so it is
  tested once).
- `PickUpAsync(cargoLineId, sizes, IReadOnlyList<LocationId>? dropOrder)`:
  runs the forecast on the *other* pending lines plus this line's
  destination, then passes `forecast.Regions[line.DropOffLocationId]` to
  `StowagePlanner.Plan` as `reserved`.
- Page: `_dropOrder` becomes `List<LocationId>`; names come from
  `snapshot.Destinations`. `RouteManifest`/`HoldView` keep taking names +
  colors — map at the page edge. ▲/▼ now calls `RefreshAsync()` with the
  manual order so ghosts and the stow check redraw (one local SQLite read,
  fine on the tablet). `_manualDropOrder` reset on `MoveTo` stays.

#### 4. Stow check (pure, in `ManifestPlanner`)

```csharp
public sealed record StowConflict(string BlockedDropOff, string BlockingDropOff);
public static IReadOnlyList<StowConflict> StowCheck(IEnumerable<CargoContainer> aboard, IReadOnlyList<Location> dropOrder);
```

For each destination with boxes aboard, take `minDepth` over its boxes. A
conflict is an earlier drop whose `minDepth` is **greater** than a later
drop's — the earlier cargo is buried behind cargo that leaves later. Report
each pair once; empty list = "stow matches drop order". Depth uses the same
`DrawOffsetX + x` formula as the forecaster. Tests:
`StowCheck_Empty_WhenStowMatchesDropOrder`,
`StowCheck_FlagsEarlierDropBuriedBehindLaterDrop`,
`StowCheck_IgnoresUnplacedBoxes`.

#### 5. UI (Manifest.razor, RouteManifest.razor, HoldView.razor)

Protanopia rule holds: every new signal is text or a number, color is
secondary.

- **Ghost regions**: `HoldView` gets a `Ghosts` parameter — per bay, per
  destination, the top-down footprint (distinct X,Y of the region's cells)
  with the tower height as a `hN` badge, drawn as dashed outlines in the
  destination color *under* the real boxes. Isolate applies to ghosts too.
  `ponytail:` no AS LOADED / SUGGESTED STOW toggle — ghosts are always on;
  add the toggle only if they clutter the hold in the field.
- **Stop-number badges**: `1`, `2`, … from drop order on every real stack
  and ghost and on each route card. This also retires the 2026-09-07
  protanopia caveat on the four-color pool.
- **Route & manifest column**: route cards now include pending-only
  destinations (greyed row count "0 aboard · N SCU pending"); ▲/▼ unchanged.
- **Capacity bar**: per-destination segments in drop order (aboard SCU
  solid, pending SCU hatched) + legend chips "1 Lorville 48/120".
- **Notes** under the hold: stow check conflicts as text ("Lorville (drop 1)
  is buried behind Baijini Point (drop 2)") and the forecast shortfall
  ("Suggested stow short by N SCU").
- E2E: one happy-path run — accept two contracts with different drop-offs,
  pick both up, verify the second's boxes land in its ghost region and the
  stow check is clean; swap the drop order with ▲/▼ and verify the note
  appears.

#### Explicitly out of B2

Manual drag-to-override, persisted drop order, a global repack, forecasting
rotated footprints (regions are cells, boxes still rotate at pickup).

## Phase C — Pinned drop order + fit advice (✓ COMPLETE 2026-10-29)

**Implemented the same afternoon (Claude wrote the code at Joachim's
request), 121 tests green, verified in the browser on the 1,794 SCU load:
the pin survived a full page reload and "ROUTE ORDER" cleared it; a 96 SCU
line got "FITS as 2 SCU boxes or smaller — tight stow" and the dock then
refused 3 × 32 with "NOT LOADED", as advised. Notes:**

- `StowageAdvisor.Split` is public (tests need it; no InternalsVisibleTo).
- Measured: next to the tight-restowed 1,822 SCU field load only 96 SCU
  fits, and only as 2 SCU boxes; 128 does not. Advice costs ~100 ms when
  the first size fits and ~1.2 s in the worst case (7 sizes × 4 tiers); a
  line larger than the free cell count is refused without planning.
- Existing databases need `ALTER TABLE Ships ADD COLUMN PinnedDropOrder
  TEXT NOT NULL DEFAULT ''` or a delete (EnsureCreated adds no columns).

Order was: C1 → C2; independent, C1 first because it deletes page state that
C2's dialog would otherwise have to thread through.

### C1 — Persisted drop order

Pure/domain first (NUnit), then EF, then page. Schema change: delete
`data/hauler.db`.

1. **`Ship.PinnedDropOrder`** (`IReadOnlyList<LocationId>`, empty = route
   order). `PinDropOrder(IReadOnlyList<LocationId>)` guards: not null, no
   duplicates. `ClearDropOrder()`. `MoveTo` does **not** touch it any more.
   Tests: pin round-trips, duplicate throws, clear empties, move keeps pin.
2. **Persistence in Data/** — one text column, Guids joined with `,`, via a
   value converter + comparer in `ShipConfiguration`. No owned entity, no
   join table: the list is tiny and never queried. Test: converter
   round-trips empty and three ids.
3. **Service** — `GetManifestAsync()` and `PickUpAsync(id, sizes)` lose the
   `dropOrder` parameter and read `ship.PinnedDropOrder` (empty → null →
   route order; `ManifestPlanner.DropOrder` already drops stale ids and
   appends newcomers, so delivered destinations fall out by themselves).
   New `PinDropOrderAsync(IReadOnlyList<LocationId>)` and
   `ClearDropOrderAsync()`. `ManifestSnapshot` gains `bool DropOrderPinned`.
4. **Page/components** — delete `_manualDropOrder`; `MoveStopAsync` swaps
   in a copy of the current order and calls `PinDropOrderAsync`, then
   refreshes. `RouteManifest` shows a "ROUTE ORDER" button (text, no
   color-only state) only while pinned; tapping clears. Reloading the page
   or moving the ship keeps the pin.

### C2 — Fit advice in the pickup dialog

1. **Extract the tiers** from `ManifestService.PickUpAsync` into a pure
   `StowagePlanner.PlanTiered(bays, aboardLines, destination, boxes, dropOrder)`
   where `boxes` is `IReadOnlyList<(CargoContainerId Id, ContainerSize Size)>`
   and the result is `(PickUpOutcome-like tier, placements)`: moat >
   touching > restow moat > restow touching > null. `Repack` learns to take
   the extra line as a box list instead of a `CargoLine`, so a dry run
   never needs a mutated entity. `PickUpAsync` becomes: guard SCU, move,
   `line.PickUp(sizes)`, `PlanTiered` with the real container ids, apply.
   Existing planner/service tests must stay green unchanged — this step is
   a refactor.
2. **`StowageAdvisor.LargestBoxThatFits`** (Services/, pure, TDD): for each
   `ContainerSize` descending, split the line's SCU greedily into boxes no
   larger than that size (500 → 15×32 + 16 + 4), dry-run `PlanTiered`,
   return the first size that fits with its tier, or null. Stops at the
   first hit, so the common case costs one plan (~100 ms), the refused
   case at most 7 × 4 tries. `ponytail:` smaller boxes are assumed to fit
   whenever larger ones do — true for own-destination support, not proven.
   Tests: empty hold → 32 fits planned; the measured 1,314 SCU fixture →
   700 SCU returns null, 480 returns 16 with restow.
3. **Dialog** — on `ActionTappedAsync` the page calls a new
   `ManifestService.AdviseAsync(cargoLineId)` and shows one text line above
   the counters: "FITS as 32 SCU boxes", "FITS as 16 SCU boxes or smaller —
   restow needed", "TIGHT — …", or "WON'T FIT — no box mix fits, even after
   a restow". Text first, never color alone.

#### Explicitly out of C

Fit advice on NewContract (see pending decisions), drag-to-reorder, a
global repack button, forecasting rotated footprints.

## Definition of done, all phases

- `dotnet build` green with analyzers at latest-all, warnings-as-errors.
- Planner + domain invariants covered by NUnit tests Joachim wrote.
- Manifest flow still two taps for the happy path.
