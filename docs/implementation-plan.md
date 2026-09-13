# Realistic Bases: implementation plan

2026-09-14 · Proposed sequence · No implementation completed

Read the [design and feasibility assessment](design-feasibility.md) for intended behavior and [research notes](research-notes.md) for evidence. Milestones below are release gates, not claims of completed work.

## 1. Deliverables and boundaries

| Deliverable | Scope decision |
|---|---|
| High-level design, feasibility, source review, implementation plan | In scope now; these documents |
| RimWorld 1.6 mod scaffold and diagnostics | Planned P0–P1 |
| Terrain analysis, deterministic procedural layout, facility contracts | Planned P1–P2 |
| Tech/local-material profiles, walls and gates | Planned P2; layered variants P4 |
| All requested facility categories | Planned spatial implementation P2; sustained operation P5 |
| Weapons, armor, turrets, barricades, sandbags, mortars and physical stocks | Planned P2–P3 |
| Defensive NPC behavior and essential resupply | Planned P3 |
| Mountain/lake placement, Geological Landforms compatibility | Planned P4; early terrain-order experiment in P0 |
| Ongoing food/fuel/shell production, labor, and stock accounting | Planned P5; early NPC-work experiment in P0 |
| Persistent damage, depletion, casualties and revisits | Planned P5; early lifecycle experiment in P0 |
| Specialized mortar shell selection/targeting | Planned P6; HE/incendiary baseline P3 |
| Modded factions, additional DLC, Combat Extended | Rescheduled to optional adapters after the Core release; separate compatibility workloads |
| Detailed off-map simulation of every pawn | Declined; unnecessary runtime/save burden for this design |
| Coarse off-map economy and trading | Rescheduled; frozen finite balances are the initial persistence policy |
| Traps, IEDs, trap-based killboxes | Declined by explicit user requirement; prohibited in all releases |
| Multiplayer, custom wide gates, vertical levels | Rescheduled beyond initial release; no implied support |

## 2. Architecture to establish

Use a standalone C#/XML mod, with no mandatory dependency on either reference base generator. Choose Harmony only for narrowly scoped integration points established by P0. Reuse appropriate vanilla classes and defs; avoid copying the game implementation into the distributable mod.

Proposed code areas:

| Area | Responsibility |
|---|---|
| `Generation/Integration` | Eligibility, generator selection, step order, competing-generator detection, failure recovery |
| `Planning` | Immutable inputs, terrain snapshot, candidate search, room graph, geometry, validation |
| `Capabilities` | Faction profiles, def catalogs, crop/material/equipment filtering, import limits |
| `Economy` | Food, fuel, power, ammunition, storage and labor budgets |
| `Spawning` | Ordered placement of terrain edits, buildings, contents and residents; track owned mutations |
| `AI` | Settlement Lord, duties, sector assignment, support work and NPC job adapters |
| `Persistence` | Saved identity, plan version, live state, baseline/deltas and lifecycle reconciliation |
| `Compatibility` | Optional Geological Landforms, faction, DLC and ammunition adapters |
| `Diagnostics` | Generation report, overlays, seed replay, runtime resource/job traces and settings |

Central records: `SettlementProfile`, `TerrainSnapshot`, `SettlementPlan`, `FacilityContract`, `ResourceBudget`, `DefenseSector`, and `SettlementState`. These are proposed internal names, not existing game APIs. Plan/state ownership must use the relevant map and settlement identity, never the current UI map.

Hard invariants live in validators shared by planning, fallback, and final-map checks. Def and template loading should reject unsupported or prohibited content before generation. Only approved defensive defs enter the spawn catalog; include class/component/category checks for traps and deny unknown modded defenses until classified.

## 3. Milestones

### P0 — Resolve the technical risks

**Purpose:** establish that the intended integration, enemy operation, and lifecycle are reachable in RimWorld 1.6.4871, then pin the supported game build.

1. Create the minimal mod/build setup and verify a Core-only launch. Establish references to the installed assembly, a debug log, and reproducible test saves. Check available build tooling; the carryover project instructions are not proof of this project's setup.
2. Trace the ordinary settlement map-generator selection and generation chain. Prototype selecting a dedicated generator only for eligible settlements. Replace its settlement/power steps together while preserving terrain/mutator steps. Record every Harmony target and guard actually needed.
3. Run a generated debug layout after terrain exists. Repeat with Geological Landforms. Record call order, terrain/roof checksums, spawn exclusions, and final-map validity. Include late custom terrain steps and map-preview generation in the investigation.
4. Assign two hostile NPCs to a custom Lord: demonstrate holding a post under damage, and real pickup/use of a nearby shell or fuel item.
5. Demonstrate one NPC producing a meal from real ingredients, refueling a generator, and performing one growing task on faction-owned facilities. Record work-tree/ownership blockers and the smallest viable adapter for each. No infinite stocks or changed player faction as a workaround.
6. Test save/load, caravan retreat, map removal, return, and settlement defeat on a disposable save. Establish which state and pawn references survive, and how a depleted stock/destroyed building will be restored without resetting.
7. Force planner rejection before spawning and a controlled failure during spawning. Establish recovery that leaves a playable map/arrival, restores owned mutations where necessary, and does not invoke trap generation.

**Exit:** a short technical findings document with observed logs, selected hooks, a working hostile-worker example, lifecycle strategy, and recovery behavior. If a spike fails, revise the relevant contract before building on it. Source inspection alone does not pass these experiments.

### P1 — Planner and validation harness

**Depends on:** P0 integration and recovery.

Implement stable seeds/profiles, terrain snapshots, connected-region analysis, bounded site candidates, simple district adjacency, occupancy reservations, and plan validation. Start with rectangular room envelopes but allow different compound outlines and circulation graphs.

Create debug overlays for terrain suitability, room labels, routes, gate openings, roof support, power networks, defense sectors, and rejected constraints. Export a compact generation report containing input versions, seed, chosen archetype, capacities, supply status, repairs/fallbacks, and timing.

**Exit:** repeated fixed-input planning produces the same plan; invalid terrain is rejected; searches terminate by deterministic limits; property tests exercise disconnected land, narrow corridors, obstructed interaction cells, and impossible capacity. No runtime map objects are mutated during planning.

### P2 — First complete physical settlement

**Depends on:** P1.

Build one industrial temperate profile and one tribal profile. Generate beds/barracks, an empty jail, kitchen, appropriate pantry/freezer, fields, dining/recreation, workshop, storage, medical area, power where applicable, and one wall layer with gatehouses. Size everything from a small resident roster and resource budget.

Implement actual local-material selection, a limited import allowance, legal crops, cooler/exhaust layout, power generation/conduits/batteries, finite startup food/fuel, armor/weapons, and basic defensive cover/turrets. Place functional interaction cells and reserve logistics space. Spawn initialized stocks and crop growth stages once at generation, with their value accounted for.

**Exit:** every facility is reachable and usable in the realized map, housing covers residents, no trap/IED exists in owned generation, each power network survives the declared startup scenarios, and food reserves bridge the stated first-harvest delay. Tribal profiles do not receive electric substitutions. These checks demonstrate readiness, not sustained autonomy.

### P3 — Playable defensive encounter

**Depends on:** P2 and P0 NPC-job findings.

Implement the custom garrison Lord, sectors/posts, weapon-aware assignment, pursuit/route limits, gate defense, reserves, breach response, safe needs relief, and inner fallback. Add mortar operators with HE and, where validated, incendiary shells. Provide finite magazines, reload materials and barrels, and essential hauling/refueling/tending roles.

Run battles from multiple approaches, at night, under long-range harassment, after a gate breach, with an incendiary threat, and with an operator or power source removed. Test that a closed gate does not leave all defenders behind opaque walls with no firing options. Test resource exhaustion and recovery from invalid posts.

**Exit: first playable milestone.** A newly generated base supports an encounter, defenders hold and respond locally, consumables are physically used, reload preserves state, and player pawns are unaffected. Publish this milestone only as a supplied encounter prototype; ongoing colony production and unload/revisit persistence are not yet certified.

### P4 — Terrain variety and stronger fortifications

**Depends on:** P3, plus P0 terrain evidence.

Add foothill/mountain carving, roof support, preservation of natural rock boundaries, outdoor utility/mortar areas, lakeshores, peninsulas, and islands with verified approaches. Add one-to-three wall layers, coherent material palettes and reinforcement budgets, multiple gates when suitable, and climate-driven food/power alternatives.

Integrate Geological Landforms through generated terrain first; add metadata/per-cell biome access only where needed. Register tested versions. Detect geometry-affecting incompatibilities, including altered water passability and unexpected late terrain changes.

**Exit:** accepted plans survive final-map validation across the terrain matrix. Unsupported island/mountain candidates fall back without damaging the landform. Outer appearance, arrival access, and circulation receive visual review; screenshots alone do not establish pathing or roof safety.

### P5 — Sustained operation and persistent consequences

**Depends on:** P3; apply P4 archetypes as they become valid.

Implement bounded NPC work selection and real cooking, cultivation, hauling, refueling, ammunition production, repair/cleaning, tending, and prisoner care where prisoners exist. Maintain target stocks from consumption and labor budgets; include job failure recovery and work/defense competition. Prove both sow/harvest cycles and recipe completion, not only job assignment.

Implement loaded-map serialization and unloaded baseline/delta reconciliation: damaged/destroyed structures, inventories, fuel, battery charge, casualties, captured residents, and removed loot. Begin with no off-map resource growth. Version the schema and test migration; stop controller ownership on faction transfer/claim/defeat.

**Exit: full core operational milestone.** Each supported self-supporting profile completes at least two measured crop cycles and its declared seasonal survival horizon with actual consumption/production and no hidden refill. A supplied profile reports a finite horizon and depletes as expected. Ammo production replaces expended shells only after consuming ingredients/work. Re-entry does not recreate loot or residents; power/storage state survives; captured assets obey the player.

This is the earliest milestone that can claim the requested autonomous settlement behavior. Persistent global economic growth remains optional future work.

### P6 — Balance, specialized tactics, and release

**Depends on:** P5 and supported P4 terrain coverage.

Add bounded special-shell selection and targeting, and verify actual EMP/smoke use rather than just stock presence. Keep these disabled for profiles without validated behavior. Calibrate faction capabilities, resident/defense caps, reserve horizons, material imports, loot, threat presentation, and generation/runtime cost. Complete the mod's settings, user documentation, debug reporting, and supported-version matrix.

Compare against vanilla and the two reference mods on a declared test set using reproducible seed/settings records; each generator runs separately. Measure operational and encounter outcomes rather than asserting superiority from appearance. Plan any optional faction/DLC/ammunition adapters as separately gated changes.

**Exit:** release checklist passes; no unresolved severe/medium correctness defects; known limitations are documented; mod removal/migration claims match tested behavior.

## 4. Validation strategy

Separate pure planning tests from live engine checks. Pure tests cannot prove a pawn takes a job or a mortar fires; live screenshots cannot prove a planner is deterministic.

| Test area | Required evidence |
|---|---|
| Determinism | Same plan digest for fixed world/profile/settings/mod versions; isolated random streams; saved bases do not reroll after setting changes |
| Geometry | No overlaps/out-of-bounds placement; valid foundations/roofs; every required interaction cell and resident route reachable under correct faction rules |
| Defenses | Enclosure closes except controlled entrances; attacks can reach a breach/gate; posts have actual firing opportunities; no prohibited defense defs |
| Power | Runtime traces for representative day/night and combat demand, fuel exhaustion, recharge and disconnected circuits |
| Food/work | Actual harvested nutrition, completed meals, consumed inputs, hauling time, reserve depletion and worker capacity |
| Ammunition | Shell/barrel/reload stock changes match use; exhaustion stops firing; shell production consumes ingredients and labor |
| NPC behavior | Post occupancy, outside-perimeter pursuit events, breach response, needs fulfillment, job stalls and retreat behavior |
| Lifecycle | Save/load while hauling/reloading/falling back; unload/re-entry; defeat; capture; faction change; missing optional defs |
| Performance | Generation time distribution, candidate count, allocations where measurable, tick cost and save growth at declared map/population sizes |

Use a pairwise matrix spanning tribal/industrial/advanced capability profiles; small/medium/large supported maps; flat/hill/mountain; fertile/cold/arid; land/lakeshore/island; warm/growing and cold/non-growing seasons; day/night; Core and Core+Geological Landforms. Add targeted cases for extreme heat/cold, tiny land pockets, deep water, narrow passes, insufficient fertile area, and scarce fuel.

Initial test-corpus proposal: 100 generation seeds per supported archetype/profile family for development, increasing to 1,000 across the supported release matrix. These are planning targets, not statistical guarantees or measured results. Every accepted generation must pass hard invariants; record fallback rate and reasons separately so “always uses the fallback” cannot count as success.

Choose generation and tick-time ceilings after P0/P1 profiling on a named machine. Optimize the demonstrated bottlenecks. Use bounded candidate searches, cached connectivity, staggered/event-driven assignment updates, and local invalidation after breaches. Do not scan the full map per pawn per tick or run Unity/map mutations on background workers.

For qualitative comparison, score coherent routes, realistic landform preservation, facility coverage, and visible tactical choices. For quantitative comparison, use powered-critical-load time, food/supply horizon, fraction of usable posts, idle/stalled worker time, unwarranted pursuit, generation failure rate, and loot/defense value. Keep difficulty and comparison settings explicit.

## 5. Dependencies, estimates, and remaining decisions

The critical path is **integration and NPC/lifecycle experiments → planner → usable physical base → defensive encounter → sustained operation and persistence**. Elaborate layouts are not the critical path.

For one developer comfortable with RimWorld C#/XML and with regular game-test access, use these provisional effort ranges only for planning: P0 1–2 weeks; P1 1–2; P2 2–3; P3 2–4; P4 2–4; P5 4–8; P6 2–4. That is roughly 6–11 developer-weeks to the first playable milestone and 14–27 for the full Core release. Re-estimate after P0; these are judgment-based ranges with no measured project velocity. Optional compatibility work is additional.

Defaults selected to keep work actionable: Core 1.6; small industrial vertical slice; one-to-three wall thickness; ordinary doors as gates; conditional natural terrain enclosure; no antigrain by default; bounded imports; finite stocks; real loaded-map jobs; frozen off-map balances first. Confirm or revise these during design review, but no clarification is required to start P0.

Decisions requiring experimental evidence: exact generator hook and recovery path, NPC work adapters, acceptable seasonal simulation cost, unload/revisit representation, and attainable performance limits. Record their outcomes before committing to downstream implementation contracts.
