# P4A foundations: procedural settlement layouts

2026-09-22 · Status: **foundation investigation and pure-planner prototype only**. P4A is not complete; P4 is still in progress under separate ownership; P5 is not started.

This document is the P4A foundation workstream's own record. The shared milestone documents (`implementation-plan.md`, `progress.md`, `p4-findings.md`, `p4-connectivity.md`) were deliberately left unchanged while P4 work runs concurrently.

## 0. Naming, baseline, and isolation

**Naming.** The implementation plan's milestone *P4A — Procedural settlement layouts* (`implementation-plan.md` §3) is the same milestone that commit `6aa4cbc` first recorded as "P4.5" and commit `a41477e` renamed to "P4A". The two labels mean the same thing. It follows P4 and precedes P5. This work covers **foundations** for that milestone: an assessment, a pure planner prototype, and test evidence. It does not renumber the plan and does not close the milestone. Code identifiers use a `Layout`/`ProceduralLayout` prefix instead of a milestone number, so that renaming a milestone never forces a code rename.

**Baseline.** I recorded the committed revision `a41477e` ("Rename deferred procedural layout milestone to P4A") from the active implementation worktree `/Users/roman_gr/.codex/worktrees/75e8/realistic-bases`. When I read it, that worktree was clean at that commit (`git status --short` was empty). Its tree is identical to the main checkout's `ab52c17`: `git diff --stat ab52c17 a41477e` produced no output. I created this work in a separate worktree, `/Users/roman_gr/Repositories/rimworld/realistic-bases-p4a`, on branch `claude/p4a-foundations`. It contains no uncommitted work from another agent.

**Isolation.** The RimWorld Mods symlink `RealisticBasesP0` points to the P4 worktree. All builds for this work ran in the P4A worktree only, so the DLL the game loads was never rebuilt. The Docker builds used a separate NuGet cache volume, `realistic-bases-p4a-nuget`, instead of the shared `rimworld-nuget` volume. That prevents concurrent package-cache writes. This work never started, stopped, or controlled RimWorld, and never used the P4 test profile. P4 interfaces cited below come from committed revision `a41477e`. If P4 changes them before it closes, treat the citations as **provisional**.

## 1. Hardcoded layout dependencies and assumptions

Line numbers refer to `a41477e`.

### 1.1 The fixed compound (`Planning/P2Layout.cs`)

| Location | Hardcoded assumption |
|---|---|
| `PhysicalSettlementPlanner` consts, l.263–266 | The compound is always 31×25 and the field is always 10×13. The candidate search only chooses the **origin** of that fixed rectangle (l.292–304). |
| `TryBuild`, l.309–323 | Every compound cell must be usable. A single unusable cell rejects the origin (`compound_cell_unusable`), so terrain can relocate the compound but never reshape it. |
| `TryBuild`, l.332–336 | Exactly two gates at `MinX+15` on the north and south edges. |
| `BuildFacilities`, l.392–418 | Every room has a constant offset and size: housing (2,2) 7×7, jail (10,2) 5×7, medical (17,2), kitchen (2,11), freezer/pantry (9,11), dining (16,11), workshop (25,11), storage (2,18), and power (24,2) 5×7. Capacities are literals (for example housing=4 and storage=24), not values derived from the profile. |
| `Facility`, l.495–502 | Every door is at the centre of the room's **max-Z** wall. Each room has exactly one interaction cell, at (MinX+1, MinZ+1). |
| `BuildPlacements`, l.504–576 | Four literal bed positions, recreation at (+17,+12), fuel at (+7,+21), generator (+26,+4), battery (+26,+6), a west-facing cooler at (freezer.MinX, MinZ+2), turrets at (+12,+1)/(+18,+1), and cover at dx {10,14,16,20}. Conduit terminals use literal points. Other furniture is placed at the room centre, whatever the room size. |
| `TryFindField`, l.420–443 | Four fixed field offsets around the compound. |
| `TryBuildGatehouseBands` / `GatehouseFootprint`, l.448–493 | Gatehouse bands sit at `MinX+13..+17` beyond the north/south edges only. |
| `PhysicalSettlementValidator`, l.670–685, 705, 770–785 | Exactly one facility per `FacilityKind`, exactly two gates, the cooler rotation must be `"West"`, and `GatehouseGeometryMatches` recomputes the fixed `MinX+13..+17` band. A procedural plan with two barracks, an east-facing cooler, or gates elsewhere would fail this validator even if it were correct. |

### 1.2 Consumers that assume the fixed compound

| Location | Assumption |
|---|---|
| `Planning/P3Encounter.cs` l.161–186 | Non-terrain-following posts, the mortar (c+26,c+20), the rally/fallback points (c+15,c+12)/(c+15,c+9), and the control envelope (compound ±2) use literal compound offsets. The terrain-following variant (l.211–239) still starts from literal desired offsets before snapping to `Logistics`. The magazine is at storage (MinX+1, MinZ+1). |
| `Planning/P4Terrain.cs` l.38 | `MinimumBuildableCells = 31*25`. The archetype pre-screen measures capacity with the fixed rectangle, not with the demand. |
| `Integration/P2MapPlanning.cs` l.46–47 | Seed derivation constructs a P1 `SettlementProfile` from the fixed compound dimensions, so a change to the dimensions changes the seed. |
| `Generation/P2SettlementSpawner.cs` l.71, 156–173, 208, 260, 351–363 | Plant clearing covers the `Compound` rectangle. Food, storage, and medical each use `Single(kind)`. Map state is anchored at the compound's minimum corner. Residents spawn on `Logistics`. Interior cells are `Bounds ±1`. Cover positions are inferred from the post's side of the compound rectangle. |
| `Integration/P2RuntimeAudit.cs` l.75–97 | `Gates[0]` is the attack gate and `Gates[1]` the retreat gate. The freezer/pantry is `Single`. The preserve temperature is sampled at the bounds centre. |
| `Generation/P0Generation.cs` l.133, 173–174 | Reporting and diagnostics use the compound origin and width. |

### 1.3 Assumptions inside the P4 connectivity contract (`Planning/P4Connectivity.cs`)

`PlannedNavigationGrid` is the right **semantic** contract: one public root, cardinal movement, room-local floods, and validation against final obstacles. Its **representation**, however, is tied to the fixed compound:

1. The public mask is `Compound.Points()` minus roofed-room cells (l.39), which requires a rectangular enclosure.
2. `InsideGate` (l.101) infers the interior endpoint from which rectangle edge the gate lies on. A gate on an irregular wall has no inferable endpoint.
3. `GatehousePassages` (l.72–87) assumes that a gate not on `MinZ` is on the south side. East and west gates cannot carry reinforcement bands.
4. Blocking comes from single-cell `PhysicalPlacement`s (l.36). Multi-cell fixtures such as 1×2 beds, 3×1 stoves, 2×2 tables, and 2×2 generators are under-represented.
5. `Roofed` is treated as "walled room" (l.35–40). There is no walled-but-unroofed room.
6. **Observation for the P4 owner (not changed here):** l.34 blocks a cell only when `Has(TerrainTraits.Water | TerrainTraits.Occupied)`, and `Has` requires **both** flags (`P1Model.cs` `TerrainCell.Has`). A water-only or natural-rock-without-edifice cell is therefore not blocked by the grid. This is masked today because `TryBuild` rejects every compound containing such a cell. An irregular enclosure would expose it. `HasAny`, together with a non-standable check, is probably the intended test.

## 2. Reusable primitives and P4 contracts

| Primitive | Reuse decision |
|---|---|
| `GridPoint`, `GridRect`, `StableHash` (`P1Model.cs`, `P1Planner.cs`) | Reused unchanged. No new point, rect, or hash type is added. |
| `TerrainSnapshot`, `TerrainCell`, `TerrainTraits` | Reused as the only terrain input. The planner reads it and never copies or mutates cells. Its `Digest` is checked before and after planning. |
| `PhysicalSettlementProfile`, `SettlementCapability`, `FacilityKind`, `FacilityDemand` (`P2Profiles.cs`) | Reused as the capability/profile source. A layout requirement can be derived from a profile (profile capacities act as floors) or constructed from population and capabilities directly. |
| `PhysicalFacility`, `PhysicalPlacement`, `PhysicalSettlementPlan`, `TerrainMutationFootprint` | Targets of the **adapter** (§3.3). P4A does not replace them. |
| `ConnectedRegionAnalyzer` | Its semantics (cardinal, deterministic, edge metadata) are mirrored for the planner's eroded site mask. The analyzer takes a `TerrainCell` predicate, while the site mask is cell-context dependent (erosion), so the planner runs its own small component pass over a precomputed boolean mask. |
| `PlannedNavigationGrid` / `P4Connectivity.Validate` | This is the **authoritative final validator** after integration. The prototype does not replace it. Every accepted prototype layout is converted by the adapter and passed through `P4Connectivity.Validate` in the tests, for wall layers 1–3. |
| P4 gatehouse crossing contract (`p4-connectivity.md`) | Preserved exactly: bands 5 cells wide with one aligned door, a one-cell vestibule, serial crossing, and an interior endpoint in the one public component. P4A derives the gate position from its own circulation solution, then reserves the same band geometry outside it. |
| P4 edge-route semantics (`TryRouteToEdge`, private) | Re-expressed: terrain-only cardinal BFS from the outermost staging cell to a `MapEdge` cell, bounded, never entering the enclosure or the band footprint. The P2 method is private, so its semantics are copied rather than called. The integration step should expose one shared helper. |

### 2.1 The prototype's pre-acceptance check vs P4's validator

A planner needs a cheap feasibility check **inside** its search loop, on its own irregular-enclosure model, before any `PhysicalSettlementPlan` exists. The prototype's `ProceduralLayoutValidator` applies the P4 connectivity semantics to explicit masks: one root, cardinal-only floods, rooms excluded from public transit, room-local job access, final obstacles, and no exterior re-entry. It also checks P4A-specific properties that P4's grid does not express: multi-cell fixture footprints, irregular perimeter closure, capacity versus demand, and hard relationship distances.

It is **not** a replacement for `PlannedNavigationGrid`. The integration plan (§5) is to generalize `PlannedNavigationGrid` to accept an explicit public mask and explicit gate interior endpoints. The P4A check then becomes a planner-internal pre-filter, and the P4 grid remains the single acceptance authority. Until then, the tests run **both** on every accepted layout, so any divergence shows up.

## 3. Proposed interfaces and ownership boundaries

### 3.1 Inputs (`ProceduralLayoutInput`, immutable, digested)

- `LayoutRequirements`: residents, capabilities, and explicit capacity floors per facility kind. It can be built with `FromProfile(PhysicalSettlementProfile)` or `FromPopulation(residents, capabilities)`.
- `TerrainSnapshot`: the immutable usable-terrain source.
- `ulong Seed`: explicit, with no hidden randomness. Every search ordering is derived from `StableHash(seed, …)`.
- `LayoutFortificationReservation`: wall layers 1–3, inner setback width (the public firing step inside the wall), outer clearance width, and vestibule depth. These are reservations only. P4 owns the walls it builds.
- `LayoutSearchBudget`: maximum sites, attempts per site, room-candidate evaluations (global), repairs per attempt, and exterior-route nodes.

### 3.2 Outputs (`ProceduralLayout`, immutable, digested)

- **Footprint**: enclosure cells, perimeter wall cells (gates excluded), and outer-clearance cells.
- **Rooms**: kind, bounds (walls included, as in P2), one door, facing direction, demanded versus furnished capacity, and multi-cell fixtures with blocking flags and access cells.
- **Corridors**: main and branch segments. The public circulation set is the flood result over final obstacles, not a label.
- **Portals**: gates (with inside endpoint and outward direction) and room doors (with their public cell).
- **Reservations**: entrance vestibules, interaction and job-access cells, the cooler exhaust, the outdoor court, and gatehouse bands and vestibules.
- **Interface requirements**: the per-gate staging cell, the exterior route to the map edge, band geometry per wall layer, and an unresolved exterior field requirement (cell count, fertile).
- **Diagnostics**: stage, code, and detail entries for rejections, repairs, and budget exhaustion, plus counters.

### 3.3 Adapter to the P4 plan (`ProceduralLayoutAdapter.ToPhysicalPlan`)

The adapter produces a `PhysicalSettlementPlan`. Its compound is the bounding rectangle of the enclosure and its walls, and its perimeter is the procedural wall. Gates are the procedural north and south gates, which lie on the bounding rectangle so that P4's `InsideGate` stays valid. The reinforcement gates and bands have exactly P4's geometry. Rooms become roofed `PhysicalFacility`s. Each fixture becomes one `PhysicalPlacement` at its access-adjacent anchor. The adapter supplies an empty mutation footprint and the procedural exterior routes.

Known adapter losses, which are integration prerequisites:

- Multi-cell blocking is reduced to the anchor cell.
- Two rooms of one kind violate `PhysicalSettlementValidator`'s one-per-kind rule.
- The cooler rotation is not always `"West"`.
- The P2 validator's fixed `MinX+15` band check does not apply.

For those reasons, the adapter output is checked with `P4Connectivity.Validate`, not with `PhysicalSettlementValidator`.

### 3.4 Ownership

| Concern | Owner |
|---|---|
| Facility demand, relationship graph, room/corridor/portal geometry, enclosure outline, gate *positions*, and reservations | P4A planner (this prototype) |
| Final connectivity acceptance, wall/gatehouse construction, excavation, mutation ledger, and rollback | P4 (Terra), through the adapter |
| Mountain/lakeshore/island archetypes and terrain mutation | P4. The prototype declares **ordinary land only**. |
| Posts, mortar court final placement, and garrison AI | P3/P4. P4A reserves an outdoor court and a setback firing step only. |
| Spawning, runtime audit, and live evidence | P4 integration after P4 closes |
| Trade-stock quantities | P5 |

## 4. Bounded implementation checklist (this workstream)

| Step | Scope | Evidence required | Status |
|---|---|---|---|
| F0 | This assessment | Documentation review | See §7 |
| F1 | Demand model, relationship graph, and furnishing rules | Pure tests: demand monotonicity and capability legality | See §7 |
| F2 | Seeded bounded placement on an eroded usable mask, street/branch circulation with repair, enclosure, gates, bands, exterior routes, pre-acceptance validator, and diagnostics | Pure tests: determinism, variation, demand, obstacles, rejection cases, budget, and input immutability | See §7 |
| F3 | Adapter to `PhysicalSettlementPlan`, cross-check with `P4Connectivity.Validate` for layers 1–3, and ASCII export of examples | Pure tests plus the committed ASCII examples | See §7 |
| F4 | Findings and handoff | This document | See §7 |

Explicitly **not** in this workstream: changing the live generator, spawner, audit, P3 posts, AI, wall construction, excavation, P4 tests or documents, runtime testing, mountains/water/islands, fields, power conduits, traps or IEDs (never), P5.

## 5. Integration work to perform only after P4 is finished

1. Generalize `PlannedNavigationGrid`: accept an explicit public mask and explicit gate interior endpoints; support east/west gatehouse bands; use multi-cell fixture footprints; distinguish walled from roofed. Fix the `Has`/`HasAny` blocking test (§1.3.6) together with the P4 owner.
2. Relax `PhysicalSettlementValidator` from one-facility-per-kind to capacity-per-kind. Replace literal cooler rotation and band offsets with geometry derived from the plan.
3. Make P3 posts, the rally point, the mortar court, and the magazine derive from procedural geometry (setback firing step, court reservation, storage room fixtures) instead of compound offsets.
4. Replace the `Single(kind)` and `Bounds ±1` uses in the spawner and audit with per-room fixture/stockpile lists. Anchor `P0MapState` to the layout's own origin record.
5. Place fields using the exterior field requirement with fertility, at a location reachable from a gate. Route power conduits between the procedural generator, cooler, stove, and turret positions.
6. Replace `MinimumBuildableCells = 31*25` with the demand-derived area estimate. Derive the seed without fixed compound dimensions, or version that change explicitly.
7. Feed P4 archetype output (foothill, mountain excavation, water preservation) into the usable-mask builder. Until then, these archetypes use P4's path.
8. Add the explicit, logged fixed-compound fallback required by the plan, and report its selection frequency.
9. Run the live matrix: Core flat industrial/tribal, wall layers 1–3, pathing and job audits, P3 regression, and rollback.

## 6. Design (prototype)

Detailed in §7 after implementation.
