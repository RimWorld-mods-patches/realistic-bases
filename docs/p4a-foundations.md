# P4A foundations: procedural settlement layouts

2026-09-23 scope update: the pure foundation and tech-stage steps 1–6 are merged into `codex/p4-finish`. The user has authorized live P4A integration now, with remaining P4 terrain work applied to that generator. P4 and P4A are both in progress; P5 is unstarted. Historical handoff and sequencing descriptions below document the earlier design and are superseded by this update. `ProceduralLayout` and `ProceduralLayoutValidator` are the authority for the new runtime path; its adapter to `PhysicalSettlementPlan` remains diagnostic.

This document is the P4A foundation workstream's own record. The shared milestone documents (`implementation-plan.md`, `progress.md`, `p4-findings.md`, `p4-connectivity.md`) were deliberately left unchanged while P4 work runs concurrently.

## 0. Naming, baseline, and isolation

**Naming.** The implementation plan's milestone *P4A — Procedural settlement layouts* (`implementation-plan.md` §3) is the same milestone that commit `6aa4cbc` first recorded as "P4.5" and commit `a41477e` renamed to "P4A". The two labels mean the same thing. It follows P4 and precedes P5. This work covers **foundations** for that milestone: an assessment, a pure planner prototype, and test evidence. It does not renumber the plan and does not close the milestone. Code identifiers use a `Layout`/`ProceduralLayout` prefix instead of a milestone number, so that renaming a milestone never forces a code rename.

**Baseline.** I recorded the committed revision `a41477e` ("Rename deferred procedural layout milestone to P4A") from the active implementation Codex worktree. When I read it, that worktree was clean at that commit (`git status --short` was empty). Its tree is identical to the main checkout's `ab52c17`: `git diff --stat ab52c17 a41477e` produced no output. I created this work in a separate worktree, `realistic-bases-p4a`, on branch `claude/p4a-foundations`. It contains no uncommitted work from another agent.

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

It is **not** a replacement for `PlannedNavigationGrid`. The integration plan (§5) is to generalize `PlannedNavigationGrid` to accept an explicit public mask and explicit gate interior endpoints. The P4A check then becomes a planner-internal pre-filter, and the P4 grid remains the single acceptance authority. Until then, the tests run the P4A check on every accepted layout. They also run `P4Connectivity.Validate` on every layout the current P4 representation can express, and they count the layouts it cannot express (§7).

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

The adapter produces a `PhysicalSettlementPlan`. Its compound is the bounding rectangle of the enclosure and its walls, and its perimeter is the procedural wall. Gates are the procedural gates. A layout is representable only when every gate lies on the bounding rectangle, so that P4's `InsideGate` infers the correct endpoint, and when no multilayer band runs east/west. Otherwise the adapter returns no plan and lists the incompatibilities. The reinforcement gates and bands have exactly P4's geometry. Rooms become roofed `PhysicalFacility`s. Each fixture becomes one `PhysicalPlacement` at its access-adjacent anchor. The adapter supplies an empty mutation footprint and the procedural exterior routes.

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

| Step | Scope | Evidence | Status / commit |
|---|---|---|---|
| F0 | This assessment | Documentation review | Complete: `74e5d57` |
| F1 | Demand model, relationship graph, and furnishing rules (climate, lighting, wall-backed benches, personal rooms) | Pure tests for demand scaling, climate fixtures, and bench orientation | Complete: `7144e9f` |
| F2 | Seeded bounded street-first placement with repair branches, synthetic excavation with natural roof support, enclosure, free gates, bands, exterior routes, validator, and diagnostics | Pure tests for determinism, variation, demand, obstacles, rejections, budget/immutability, and rock doctrine | Complete: `7144e9f` |
| F3 | Adapter to `PhysicalSettlementPlan`, cross-check with `P4Connectivity.Validate`, and ASCII export | Pure cross-check test and examples in `docs/p4a-examples/` | Complete: `7144e9f` (code), `00efed4` (examples) |
| F4 | Findings and handoff | This document (§6–§9) | Complete: this commit |

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

## 6. Design decisions (agreed with the user on 2026-09-22)

| Decision | Choice | Consequence |
|---|---|---|
| Gate placement | **Free.** Gates may use any straight run of built wall where public circulation meets the perimeter. | `P4Connectivity` can cross-check only the layouts whose gates happen to be representable. Generalizing the P4 grid (§5.1) is a hard integration prerequisite. |
| Under-rock bases | **Synthetic excavation is implemented.** It is exercised only on synthetic mountains. | This is still no claim of live mountain support. Terra's P4 excavation, mutation ledger and rollback stay authoritative. |
| Seed | `LayoutSeed.Derive(worldSeed, tile, factionId, profileId, profileVersion)` plus the P4A planner version. It contains no compound dimensions. Purpose sub-seeds: `rooms`, `site`, `attempt`, `doctrine`, `branches`, `gate`, and others. | The integration must persist the seed with the settlement so revisits never reroll. |
| Mortar versus protection | A profile `MortarBiasPercent` rolled once against the `doctrine` sub-seed. The chosen doctrine falls back to the other one and records why. | Profiles that cannot use a mortar always prefer rock protection when it exists. A dropped mortar is written into `Notes` as a capability loss. |
| Excavation bound | A profile cell budget (`60 + R×70` industrial, `60 + R×35` tribal). A hard reject per candidate. The score is protection gained against cells dug. | The budget is checked again by the validator. |
| Roof support | A retained thick roof must reach **intact natural rock** within 6.9 cells through retained-roof cells. **Built walls never count** as holders. | This mirrors vanilla `RoofCollapseUtility.WithinRangeOfRoofHolder` (radius 6.9, flood over roofed cells). I confirmed from `Roofs.xml` that `RoofRockThick` has `canCollapse` true. |
| Retained roof ledger | `ProceduralLayout.RetainedThickRoof` sits next to Terra's `TerrainMutationFootprint` type. | P4's current `Validate` would demand roof clearance for these cells, so a ledger extension is a prerequisite (§5). |
| Generation approach | Street-first growth: a seeded 2-wide spine with entrance plazas at both ends, seeded and repair branches, rooms placed on the frontage, a setback, an outline wall, and gates. | Public connectivity holds by construction, and the validator confirms it independently. |

Requirements the user added:

- **Temperature control in every living room** (barracks, bedroom, jail, medical, dining) when the climate requires it. Industrial settlements use `Heater` and a wall-mounted `Cooler` whose exhaust goes to an open-sky public cell. Tribal settlements use a `Campfire` and a `PassiveCooler`. Rooms fully under thick roof skip cooling, because `RoomTempTracker` pulls them toward 15 °C. Climate is an explicit `LayoutClimate` input, because the snapshot has none. The thresholds (heating below 10 °C, cooling above 30 °C) are provisional.
- **Lighting in every room**: `StandingLamp` for industrial settlements and `TorchLamp` for tribal ones.
- **Batteries indoors.** The power room is always roofed. Generator count is derived from the worst-case powered load, including the power room's own draw.
- **Workbenches and stoves** stand with their back row against a wall and their interaction cell facing the room. The test checks the vanilla rotation convention (`interactionCellOffset` (0,0,-1) rotated) against the planned access cell. P2's current layout places benches at the room centre (§1.1).
- **Rich settlements** (`HousingStandard.PersonalRooms`) get one bedroom per resident instead of 8-bed barracks.

Other design points:

- **Fixtures.** Footprints come from Core defs: bed 1×2, stove/bench 3×1 with an interaction cell, generator 2×2, battery 1×2, table 2×2 with four chairs. Every fixture is treated conservatively as blocking.
- **Furnishing.** Capacity is the result of actually furnishing a candidate size. A placement is kept only if a flood from the door's interior cell still reaches every access cell.
- **Oversized demands** split into at most 8 rooms of a kind.
- **Placement margin.** Rooms and streets keep `setback + 1 + outerClearance` (default 3) Chebyshev cells from unusable terrain, which guarantees a legal outline wall. The margin is conservative. Isolated boulders consume a 7×7 area.

## 7. Evidence (synthetic planner evidence only; no live game evidence)

The commands run in the repository-prescribed SDK container. A separate NuGet cache volume keeps them isolated from the P4 worktree:

```text
docker run --rm -v "$PWD":/mod -v realistic-bases-p4a-nuget:/root/.nuget/packages \
  -w /mod/Source/RealisticBases mcr.microsoft.com/dotnet/sdk:8.0 dotnet build -c Release
docker run --rm -v "$PWD":/mod -v realistic-bases-p4a-nuget:/root/.nuget/packages \
  -w /mod/Source/RealisticBases.LayoutTests mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet run -c Release -- --export /mod/docs/p4a-examples
docker run --rm -v "$PWD":/mod -v realistic-bases-p4a-nuget:/root/.nuget/packages \
  -w /mod/Source/RealisticBases.PlannerTests mcr.microsoft.com/dotnet/sdk:8.0 dotnet run -c Release
```

Results after plan step 6 (the tech-stage equipment commits are listed in §10; the foundation itself was first recorded at `7144e9f` with 18/18):

- The mod assembly (net472) built with **0 warnings and 0 errors**. The new planning files compile into the DLL, but nothing references them.
- The new `RealisticBases.LayoutTests` passed **62/62** in about 11 s: the 18 foundation tests and 44 tech-stage equipment tests.
- The existing `RealisticBases.PlannerTests` passed **52/52** with no regressions. `Program.cs` was not modified.
- `git diff --check` was clean.

| Required demonstration | Test and observed result |
|---|---|
| Identical inputs produce identical layouts and digests | Replayed digest, ASCII, and search trace are identical. |
| Several seeds give internal differences beyond translation or rotation | Seeds 1–6 on the same 120×120 plains are pairwise **not** equivalent under all 8 rigid motions. 6/6 distinct door-adjacency topologies and 6/6 distinct room-size sets. |
| Population/demand changes capacity and geometry coherently | 4→16 residents: enclosure 617→1048 cells, rooms 9→10 (barracks split), interior area 156→423, more stoves and tables. Barracks split at 8 beds. Storage and beds grow with population. |
| Obstacles alter the layout | Plains versus the same map with a pond, a rock ridge, and boulders (same seed): non-equivalent layouts, with nothing placed on water or obstacles. |
| Connectivity and access hold after planned blocking footprints | The validator floods over final walls, room walls, multi-cell fixtures, the court, and undug rock. Injected blockers are each reported: a door's outside cell (`room_door_disconnected_kitchen`), a stove's interaction cell (`interaction_unreachable_kitchen_Stove`), a ring around gate 1's inside endpoint (`gate1_disconnected`, even though its exterior route exists, so exterior re-entry is not accepted), the root (`public_root_blocked`), and a full street cut. |
| Bedrooms, prisons, and freezers never become corridors | Every room has exactly one portal, and no room cell belongs to public circulation. Every door and gate endpoint is in the one public component. |
| Impossible demand, disconnected terrain, and narrow passages reject predictably | `insufficient_usable_area` (population 20 on 40×40; small islands; two pockets joined only by a 3-wide pass, each eroded to 196 cells against ~504 needed). `room_demand_exceeds_legal_dimensions` (400 residents; a floor of 400 dining seats). `no_valid_layout` with `no_gate_candidate` (a 58×58 island with no dry route to the map edge: no bridge or water crossing is assumed). `search_budget_exhausted` (120 tribal residents on 120×120 pass the loose area pre-check but exceed the bounded search). 120 industrial residents now reject earlier with `room_demand_exceeds_legal_dimensions:Power:8`, because their load needs more than eight generators. |
| Search is bounded and inputs are unchanged | `maxEvaluations=60` rejects after exactly 60 evaluations. The terrain, input, and requirement digests are unchanged. Every counter stays within its budget. |
| Rock doctrine | Tribal, cold, mountain: `RockProtected`, 3/4 living rooms under retained thick roof, 225/270 cells excavated, no unsupported roof cell, rock used as natural perimeter. Industrial with bias 100 gives `OpenGround` with an open-sky mortar court. Bias 0 gives `RockProtected`, no court, and the note `mortar_court_dropped_for_rock_protection`. A 20×20 dug hall is unsupported, while a 12×12 room held by its rock walls is supported. |
| Wall layers 1–3 | Every gate reserves (L−1) aligned band doors, 4(L−1) band walls, vestibules, staging, and an exterior route. The validator checks alignment, terrain, overlap, and the route never re-entering the enclosure. |
| P4 adapter cross-check | 7/24 layouts (layers 1–3 × 8 seeds) are representable by `PlannedNavigationGrid` at `a41477e`, and all 7 pass `P4Connectivity.Validate` (1/24 before the catalog furnishing of step 4 changed the layouts). The others are not representable, because their gates are not on the bounding rectangle or have east/west bands (§1.3). They are counted and reported, not forced. Power yards map to blocking generator placements. |

The inspectable examples in `docs/p4a-examples/` each carry a legend and, for the mountain cases, a roof view:

- three plains seeds
- 16 residents
- obstacles
- 3 wall layers
- industrial cold personal rooms
- industrial hot barracks
- tribal cold barracks
- tribal rock-protected
- the mortar and protect doctrines
- Industrial Early with a wind turbine and its clear zone (`plains-industrial-early-wind`)
- Industrial Mid with a geothermal yard on a steam geyser (`plains-industrial-mid-geothermal`)
- Industrial Mid rock-protected, fuel-only power (`mountain-industrial-mid-rock-protected-power`)

Since plan step 4 the examples show the catalog kit: tribal bedrolls, torches, campfire kitchens, one crafting spot, stools, stockpile zones and wooden walls; shelves ('k'), vitals monitors ('V'), power yards ('Y') and wind clear zones ('w') where the stage has them. Each header lists the material ledger and the power plan.

## 8. Limitations of the foundation

- **Evidence.** All of it is pure and synthetic. No live map, spawning, pathing, `TraverseParms`, job, temperature, or roof collapse was observed.
- **Mountains.** Mountain and under-rock support is synthetic only. Live mountains, lakeshores, and islands are not claimed. The ordinary land and rock classes do not model shallow water (always blocked) or thin rock roofs (every roofed rock is treated as thick).
- **Gate count.** A layout may accept one gate (`MinimumGates=1`) when the second spine end is inside rock. This happened in the rock-protected example. The shortfall is written into `Notes`.
- **Geometry.**
  - Room envelopes are rectangular.
  - Neighbouring rooms have double walls; there are no shared walls.
  - One door per room.
  - The main street is 2 wide and straight, with no loops except by chance.
  - The margin around every unusable cell is conservative.
- **Fixtures.** Since plan step 4, blocking follows catalog passability (anything not `Standable` blocks), so spots, bedrolls, seats and horseshoe pins are non-blocking and every placed fixture still keeps a reachable neighbour. Only straight-ahead interaction offsets are modelled; others reject with `interaction_offset_unsupported:<def>`. Heater and cooler counts are area rules of thumb (40 cells each, passive coolers 25), not thermal simulation. The heating and cooling thresholds are provisional.
- **Not planned yet:** fields (only the required count is output), power conduits, P3 posts, doors' materials, and trade and stock allocation.
- **Validator.** It duplicates P4 connectivity semantics for the irregular model. §2.1 and §5.1 explain how this converges into the one P4 authority.
- **Pre-check.** The area pre-check is a necessary condition only. Marginal demands fall through to the bounded search and can end with `search_budget_exhausted` rather than an area rejection.

## 9. Handoff: what remains for P4A after P4 is final

The foundation delivers a working, deterministic, bounded pure planner, reached as `ProceduralLayoutPlanner.Plan(ProceduralLayoutInput)`. It derives demand from population, capability, climate, and wealth, grows circulation, places furnished rooms, optionally digs under rock, encloses the result, and chooses gates and bands. It explains rejections, validates its own final occupancy, and exports ASCII. None of it is wired into generation.

Remaining P4A work, in dependency order, to start only after P4 closes:

1. **Generalize P4 connectivity with the P4 owner (§5.1).** Add explicit public masks and gate endpoints, gatehouse bands in any direction, multi-cell footprints, and a walled-versus-roofed distinction. Fix the `Has`/`HasAny` blocking test. Then make `ProceduralLayoutValidator` a pre-filter only, and reach a 24/24 adapter cross-check.
2. **Extend the mutation ledger** with retained thick roof (`RetainedThickRoof`). Add live collapse checks and rollback for excavation-with-roof.
3. **Adapt P2 and P3 consumers** (§5.2–§5.4): capacity per kind, per-room fixtures, procedural posts, the mortar court, and the magazine.
4. **Integrate the P4 archetypes into the usable mask** (§5.7): foothill, lakeshore preservation, island approaches. Only then claim terrain support.
5. **Place fields and route conduits** (§5.5). Add the logged fixed fallback and its frequency (§5.8).
6. **Persist the P4A seed** with the settlement, version the planner, and replace `MinimumBuildableCells` (§5.6).
7. **Run the live matrix** (§5.9): Core flat industrial and tribal, cold and hot climates, wall layers 1–3, rock-protected and mortar doctrines, pathing and job audits, P3 regression, rollback, and visual review. Keep the separation between pure and live evidence.

Improvements the tests suggested but that are not required:

- shared room walls
- corridor loops and wider plazas
- a smarter margin, so interior boulders no longer consume a 7×7 area
- an area pre-check calibrated from observed enclosure-to-room ratios, which are roughly 1.3–1.5 on plains

P4A is **not complete**, and P5 is not started.

## 10. Tech-stage equipment (plan steps 1–6)

`docs/plans/2026-09-23-tech-stage-equipment.md` extends this foundation. Steps 1–6 are implemented as pure, engine-independent code with synthetic evidence only; steps 7–8 (runtime catalog adapter, equipping and spawning) wait for P4. Nothing is wired into the live generator.

| Step | What was built | Commit |
|---|---|---|
| 1. Stage model | `SettlementTechStage` (six cumulative research sets, ceilings, adaptations with reasons, `TryStepDown`), faction bands, `LayoutSeed.DeriveFaction` | `3e9ecde` |
| 2. Catalog and legality | `CatalogDef`, `TechCatalog`, prohibited-content filter (traps, IEDs) before legality; `tools/extract-core-catalog.py` and the Core 1.6.4871 test fixture | `087fe9a` |
| 3. Role table | `RoleTable` with MaxStage/MinStage caps, `RoleResolver` with step-down ("cannot equip its tier"), `TechCatalog.RawMinimumTiers` | `ae78272` |
| 4. Planner integration | `LayoutEquipment` / `LayoutKit`, role-driven furnishing from catalog footprints, Stonecutting adaptation, construction stuff, wood and stone ledger | `487f661` |
| 5. Power progression | `LayoutPowerPlan`: dispatchable floor, geothermal/solar/wind yards, batteries, fuel stock, chemfuel from Mid | `f6d7d65` |
| 6. Gear and stock | `SettlementLoadout`: roles, weapons, apparel by stage and climate, quality, armoury, medicine, components, equipment budget | this commit |

Evidence: 44 new LayoutTests (62/62 total), PlannerTests 52/52 unchanged, mod build 0/0. Highlights: the six stage sets are closed under vanilla prerequisites; no trap or IED is ever legal; tribal layouts and loadouts contain no ComplexFurniture, smithy, gun, flak or industrial medicine; wood-poor stony tribes adapt Stonecutting and build in stone; the power plan always covers the critical load with dispatchable generators; loadouts pass `ValidateLoadout` for every stage and climate.

Measured equipment value per resident (8 residents, seed 5, unlimited budget; gear + 25% armoury + medicine and components; vanilla `CalculatedBaseMarketValue` with quality factors 0.5/0.75/1/1.25/1.5/2.5/5), which calibrated the provisional budget table:

| Stage | Temperate | Cold | Default budget per resident |
|---|---|---|---|
| Neolithic | 289 | 495 | 325 |
| Medieval | 869 | 901 | 900 |
| IndustrialEarly | 664 | 852 | 1000 |
| IndustrialMid | 1553 | 2015 | 1600 |
| IndustrialLate | 2343 | 2465 | 2400 |
| Spacer | 5110 | 5159 | 5200 |

Climates that need heating multiply the default by 1.2; colder kits above that degrade quality first.

Provisional constants (all named in code): faction band weights and the Medieval chance for tribes; role MaxStage/MinStage caps; the ×1.25 wood-scarcity threshold, the enclosure-outline estimate (4 × √(1.5 × room area)) and the 3-day fuel horizon; `CellsPerShelf`; renewable capacity factors (solar 0.5, wind 0.4, watermill and geothermal 1.0), four units per source, a six-hour battery bridge; loadout role shares, armoury share, medicine and component stocks, quality weights and value factors; import limits (5500 / 1400) and the equipment budget table.

Known limits: wind turbines face North only and rarely fit between packed rooms; the watermill is deferred to the P4 river archetype; exotic raw stuff is gated by `RawMinimumTiers`; Steel, cloth and component costs of furniture are summarized but not charged; the loadout is standalone (not part of the layout digest).

Remaining (after P4): step 7 builds `TechCatalog` from `DefDatabase` (DLC and mods), persists the stage per faction, and fills `LayoutLocalResources` (wood yield, local stone, river and geyser cells) from the live map. Step 8 strips and re-equips residents, spawns stocks, and fixes the P2 profiles through the catalog, verified by the live matrix in the plan.
