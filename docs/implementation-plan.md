# Realistic Bases: implementation plan

2026-09-14 · Scope revised 2026-09-20 · Implementation status is tracked in [progress](progress.md)

Read the [design and feasibility assessment](design-feasibility.md) for intended behavior and [research notes](research-notes.md) for evidence. Milestones below are release gates, not claims of completed work.

## 1. Deliverables and boundaries

| Deliverable | Scope decision |
|---|---|
| High-level design, feasibility, source review, implementation plan | In scope now; these documents |
| RimWorld 1.6 mod scaffold and diagnostics | Planned P0–P1 |
| Terrain analysis, deterministic procedural layout, facility contracts | Planned P1–P2 |
| Tech/local-material profiles, walls and gates | Planned P2; layered variants P4 |
| All requested facility categories | Physical implementation P2; encounter operation P5; full sustained operation post-MVP |
| Weapons, armor, turrets, barricades, sandbags, mortars and physical stocks | Planned P2–P3 |
| Defensive NPC behavior and essential resupply | Planned P3 |
| Mountain/lake placement, Geological Landforms compatibility | Planned P4; early terrain-order experiment in P0 |
| Encounter food/fuel/ammunition reserves and essential NPC support | P5; finite stocks and best-effort jobs over a declared short horizon |
| Trade inventory reflected in physical supplies, distinct from operating reserves | P5; ordinary-item snapshot/ownership/transaction integration |
| Long-term food/fuel/shell production balance and seasonal survival | Post-MVP; explicitly deferred in [supply scope](supply-and-mvp-scope.md) |
| Persistent damage, depletion, casualties and revisits | Planned P5; early lifecycle experiment in P0 |
| Specialized mortar shell selection/targeting | Planned P6; HE/incendiary baseline P3 |
| Modded factions, additional DLC, Combat Extended | Rescheduled to optional adapters after the Core release; separate compatibility workloads |
| Detailed off-map simulation of every pawn | Declined; unnecessary runtime/save burden for this design |
| Coarse off-map production, economic simulation and supply convoys | Post-MVP; ordinary trade-stock consistency remains P5 |
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
| `Economy` | Encounter reserves, trade-stock ownership/reconciliation, food/fuel/power/ammunition budgets; long-term balance deferred |
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

**2026-09-23 scope update:** Complete the remaining P4 terrain and fortification work on the P4A procedural generator. The fixed P2 compound is historical prototype evidence. Its separate remaining test matrix is superseded by the procedural runtime matrix below.

Add foothill/mountain carving, roof support, preservation of natural rock boundaries, outdoor utility/mortar areas, lakeshores, peninsulas, and islands with verified approaches. Add one-to-three wall layers, coherent material palettes and reinforcement budgets, multiple gates when suitable, and climate-driven food/power alternatives.

Integrate Geological Landforms through generated terrain first; add metadata/per-cell biome access only where needed. Register tested versions. Detect geometry-affecting incompatibilities, including altered water passability and unexpected late terrain changes.

**Exit:** accepted plans survive final-map validation across the terrain matrix. Unsupported island/mountain candidates fall back without damaging the landform. Outer appearance, arrival access, and circulation receive visual review; screenshots alone do not establish pathing or roof safety.

### P4A — Procedural settlement layouts

**Depends on:** P3 and the reusable P4 terrain/mutation contracts. P4 and P4A now proceed together under the user's authorization of 2026-09-23.

Replace the fixed whole-settlement compound with a bounded, seeded planner that
derives facility capacities from the profile/population, packs terrain-legal room
envelopes and districts, generates their shared circulation and portals, and then
derives the enclosure, gates, and one-to-three-layer gatehouse crossings from that
same solution. Reusable small room-furnishing patterns remain allowed. A compact
fixed arrangement may exist only as an explicitly labelled exceptional fallback;
its selection frequency and reason must be reported.

Translation, rotation, material variation, site selection, or choosing between
fixed templates alone does not satisfy this milestone. Packing, circulation,
perimeter, portals, and terrain constraints must repair or reject together while
preserving the established facility, job-access, roof, defense, recovery, and
bounded-deterministic-search contracts.

**Exit:** fixed input replays identically; several seeds on fixed terrain yield
meaningfully different internal layouts beyond translation/rotation; population or
facility demand changes capacity and geometry coherently; terrain changes selected
layout rather than merely relocating/rejecting the same rectangle; all room/job
access remains connected for each supported wall thickness; and representative
live pathing, defense, visual, and rollback cases pass. The pure foundation and
tech-stage steps 1–6 are merged. Runtime integration and remaining P4 terrain work
share one generation path; neither milestone closes from the fixed-layout prototype.

**Implementation order:** runtime catalog and local resources; persisted seed and
faction stage; plan and final occupancy validation; transactional spawning of
walls, doors, room fixtures, fields, utilities, equipment, and finite stocks;
geometry-based P3; terrain and one-to-three-layer gates; Core and Geological
Landforms normal/rejection/recovery, save/reload, and visual/path/job evidence.

### P5 — Encounter supplies, trade stock, and persistent consequences

**Depends on:** P3, P4, and P4A. Revised by the user on 2026-09-20; [supply and MVP scope](supply-and-mvp-scope.md) defines the detailed contract.

Use three in-game days as a provisional routine-supply horizon and a separately stated combat duty budget (initial target: six hours of engagement at declared equipment-specific firing duty within that visit). These are tunable planning defaults, not evidence of how long players stay. Back routine needs and defense with finite actual stocks, account for food spoilage and actual connected power loads, and maintain essential needs/refueling/reload/hauling/tending support. Production is best effort; no unobserved harvest or manufacture may fund the guaranteed encounter budget. Existing larger P2 reserves need not be reduced.

Divide inventory into operating reserves and sale goods without double counting. Prototype access to the settlement's actual current trade inventory and restock generation before selecting integration hooks. Read access can generate stock, so snapshot acquisition must be explicit, saved and separate from the pure planner. Reuse existing stock when present; initialize previously absent stock once through a guarded supported path. For supported ordinary goods, materialize actual trade quantities/attributes into storage or transfer ownership through a single-authority adapter. Completed purchases, sales, looting, destruction, emergency reserve transfers and reload/re-entry must reconcile to those same quantities. Do not reroll goods or leave both a sellable copy and lootable copy. Test lazy/empty-stock and restock boundaries. Trade-stock generation is allowed as the game's bounded trade abstraction; encounter-time replenishment and an economic simulator are not required or allowed as shortcuts.

Keep trade goods within storage and loot/value constraints by changing capacity or the authoritative offer coherently before publishing it; never silently truncate already offered stock or duplicate it. Persist/log unsupported item categories; live trade pawns/animals and exotic mod containers need separate adapters and are not materialized in the first MVP trade adapter. They must not be cloned or silently destroyed. Unknown trader behavior is reported as unsupported rather than claimed consistent. Hostility may block trading but must not reroll previously existing goods.

Retain loaded-map serialization and unloaded damage/depletion/casualty reconciliation. Shortening the food horizon does not defer no-duplication correctness: bought or looted goods stay gone, killed/captured residents do not respawn, power/fuel state survives, and claimed assets stop obeying the NPC controller. P0's coarse regenerated defenders remain a prototype limitation to resolve here.

**Exit: MVP encounter-readiness milestone.** Supported profiles have quantified routine/combat coverage, essential support actually works without player orders, stocks deplete honestly, and declared loads/horizons are observed in representative encounter tests. Supported trade items match the canonical offer/transactions and never count twice or regenerate on empty reads, reload or re-entry. Negative tests include exhausted fuel/ammo, loss of a critical worker or generator, buy-then-attack, sell-then-attack, loot-then-revisit, empty stock and a restock boundary. Failure under overwhelming damage is allowed; unexplained refill or fabricated pass evidence is not. Check required state/capture persistence independently of long-term sustainability.

**Outside MVP:** two completed crop cycles, seasonal/winter self-sufficiency, long-running balanced shell manufacture, sustained labor/economic optimization, and off-map production/convoy simulation. Keep facilities and plausible supporting resources, and document unverified long-term behavior. P6 can release the MVP without these deferred tests; use the explicit post-MVP backlog in the supply scope document.

### P6 — Balance, specialized tactics, and release

**Depends on:** P5 and supported P4 terrain coverage.

Add bounded special-shell selection and targeting, and verify actual EMP/smoke use rather than just stock presence. Keep these disabled for profiles without validated behavior. Calibrate faction capabilities, resident/defense caps, reserve horizons, material imports, loot, threat presentation, and generation/runtime cost. Complete the mod's settings, user documentation, debug reporting, and supported-version matrix.

Compare against vanilla and the two reference mods on a declared test set using reproducible seed/settings records; each generator runs separately. Measure operational and encounter outcomes rather than asserting superiority from appearance. Plan any optional faction/DLC/ammunition adapters as separately gated changes.

**Exit: MVP release gate.** Release checklist passes; no unresolved severe/medium correctness defects; known limitations and post-MVP sustainability work are documented; mod removal/migration claims match tested behavior. Revised P5 requires encounter readiness and stock consistency, not long-term self-sufficiency.

## 4. Validation strategy

Separate pure planning tests from live engine checks. Pure tests cannot prove a pawn takes a job or a mortar fires; live screenshots cannot prove a planner is deterministic.

| Test area | Required evidence |
|---|---|
| Determinism | Same plan digest for fixed world/profile/settings/mod versions; isolated random streams; saved bases do not reroll after setting changes |
| Geometry | No overlaps/out-of-bounds placement; valid foundations/roofs; every required interaction cell and resident route reachable under correct faction rules |
| Defenses | Enclosure closes except controlled entrances; attacks can reach a breach/gate; posts have actual firing opportunities; no prohibited defense defs |
| Power | Runtime traces for representative day/night and combat demand, fuel exhaustion, recharge and disconnected circuits |
| Food/work | Encounter reserve coverage, spoilage, essential jobs and actual consumed inputs; repeated harvest/seasonal balance deferred |
| Trade stock | Canonical offer versus physical quantities; reserve separation; buy/sell/loot/reload/revisit/empty-stock/restock cases |
| Ammunition | Shell/barrel/reload stock changes match use; exhaustion stops firing; optional best-effort production consumes real ingredients/work |
| NPC behavior | Post occupancy, outside-perimeter pursuit events, breach response, needs fulfillment, job stalls and retreat behavior |
| Lifecycle | Save/load while hauling/reloading/falling back; unload/re-entry; defeat; capture; faction change; missing optional defs |
| Performance | Generation time distribution, candidate count, allocations where measurable, tick cost and save growth at declared map/population sizes |

Use a pairwise matrix spanning tribal/industrial/advanced capability profiles; small/medium/large supported maps; flat/hill/mountain; fertile/cold/arid; land/lakeshore/island; warm/growing and cold/non-growing seasons; day/night; Core and Core+Geological Landforms. Add targeted cases for extreme heat/cold, tiny land pockets, deep water, narrow passes, insufficient fertile area, and scarce fuel.

Initial test-corpus proposal: 100 generation seeds per supported archetype/profile family for development, increasing to 1,000 across the supported release matrix. These are planning targets, not statistical guarantees or measured results. Every accepted generation must pass hard invariants; record fallback rate and reasons separately so “always uses the fallback” cannot count as success.

Choose generation and tick-time ceilings after P0/P1 profiling on a named machine. Optimize the demonstrated bottlenecks. Use bounded candidate searches, cached connectivity, staggered/event-driven assignment updates, and local invalidation after breaches. Do not scan the full map per pawn per tick or run Unity/map mutations on background workers.

For qualitative comparison, score coherent routes, realistic landform preservation, facility coverage, and visible tactical choices. For quantitative comparison, use powered-critical-load time, food/supply horizon, fraction of usable posts, idle/stalled worker time, unwarranted pursuit, generation failure rate, and loot/defense value. Keep difficulty and comparison settings explicit.

## 5. Dependencies, estimates, and remaining decisions

The remaining MVP path is **defensive encounter → supported terrain/fortifications → bounded encounter supplies, trade-stock consistency and persistence → release validation**. P0–P2 are recorded complete; the progress tracker is authoritative for status. Full sustainability is a separate post-MVP workstream.

The original effort estimate is superseded by completed P0–P2 work and this scope revision. Re-estimate remaining work after the P5 trade ownership/restock spike: shorter sustainability validation removes work, while faithful trade-stock reconciliation adds a distinct integration risk. No replacement schedule is claimed without that evidence.

Current defaults: Core 1.6; one-to-three wall thickness; ordinary doors as gates; conditional natural terrain enclosure; no traps/IEDs or default antigrain; bounded prior imports; finite routine/combat reserves; separate trade-stock ownership; best-effort real jobs; frozen off-map operating balances. Provisional encounter targets are three in-game days routine and six game hours combat at declared duty. Calibrate without silently changing acceptance criteria.

Remaining decisions requiring experimental evidence include trade-stock acquisition/ownership/restock hooks, complete unload/revisit reconciliation, measured encounter load/duty budgets, and attainable performance limits. Keep the accepted P0/P1 boundaries; seasonal simulation cost is post-MVP.
