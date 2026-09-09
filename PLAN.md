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

### Pending decisions (recommendation noted; confirm when reached)

- **Container edit rules** — implemented: add/remove only while Pending,
  never remove the last container. (Confirmed by tests Joachim wrote.)
- **`ShipSettings` fate** — recommend: delete at the EF step; capacity lives
  on `Ship` only.

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

## Phase B — Route ordering + suggested stow (← NEXT)

### B1 — Route ordering (prerequisite for B2)

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

- `StowageForecaster`: pure, recomputed (never stored) — reserves regions
  per destination from SCU totals (1 SCU = 1 cell, exact), ramp-outward in
  drop order, contiguous per destination, frontier no-backfill (design
  packer rules). Pending lines only; frozen placements are immovable input.
- StowagePlanner honors the destination's reservation as a soft preference
  at pickup (try region first, greedy fallback).
- UI: SUGGESTED STOW toggle, stow check note (min-X per stop vs drop order),
  ghost regions for pending cargo, route & manifest column with ▲/▼ drop
  order, capacity legend + segments, stop-number badges.

## Definition of done, all phases

- `dotnet build` green with analyzers at latest-all, warnings-as-errors.
- Planner + domain invariants covered by NUnit tests Joachim wrote.
- Manifest flow still two taps for the happy path.
