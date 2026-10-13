# Realistic Bases: implementation plan

2026-09-14 · Scope revised 2026-09-28 · Implementation status is tracked in [progress](progress.md)

Read the [design and feasibility assessment](design-feasibility.md) for intended behavior and [research notes](research-notes.md) for evidence. Milestones below are release gates, not claims of completed work.

## 1. Deliverables and boundaries

| Deliverable | Scope decision |
|---|---|
| High-level design, feasibility, source review, implementation plan | In scope now; these documents |
| RimWorld 1.6 mod scaffold and diagnostics | Planned P0–P1 |
| Terrain analysis, deterministic procedural layout, facility contracts | Planned P1–P2 |
| Tech/local-material profiles, walls and gates | Baseline P2/P4A; coordinated exterior layers/gates, turrets and cover in P4B |
| All requested facility categories | Physical implementation P2; encounter operation P5; full sustained operation post-MVP |
| Weapons, armor, turrets, barricades, sandbags, mortars and physical stocks | Planned P2–P3 |
| Defensive NPC behavior and essential resupply | Planned P3 |
| Mountain/lake placement, Geological Landforms compatibility | Planned P4; early terrain-order experiment in P0 |
| Budget-driven ordinary populations; average 7, low wealth 4, wealthy up to 12 | P4A population/input integration; facilities, gear and finite reserves follow the funded roster |
| Supported settlement-like quest locations | P4D complete for exact Core bandit/rescue contracts; native threat/content/objective/lifecycle preservation verified; competing GL pipelines remain native |
| Encounter food/fuel/ammunition reserves and essential NPC support | P5; finite stocks and best-effort jobs over a declared short horizon |
| Trade inventory reflected in physical supplies, distinct from operating reserves | P5; ordinary-item snapshot/ownership/transaction integration |
| Long-term food/fuel/shell production balance and seasonal survival | Post-MVP; explicitly deferred in [supply scope](supply-and-mvp-scope.md) |
| Persistent damage, depletion, casualties and revisits | Planned P5; early lifecycle experiment in P0 |
| Specialized mortar shell selection/targeting | Planned P6; HE/incendiary baseline P3 |
| Route diagnostics and evidence-driven layout/encounter tuning | P6; reuse existing relationships, occupancy validation and bounded search |
| Settlement specializations | P7, final planned milestone after MVP; agriculture/industry first, then feasibility-ranked profiles |
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

**2026-09-24 planning update:** Adopt the bounded recommendations from the [colleague-design review](colleague-design-review.md) in future P5/P6 work and add P7 specialization as the final planned milestone. This does not expand current P4/P4A closeout. Keep the existing planner; the large room catalog, simulated annealing, general clustering/room-merging frameworks, friendly visits and Multiplayer integration remain deferred or outside the initial release.

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

### P4 — Terrain variety and environmental integration

**Depends on:** P3, plus P0 terrain evidence.

**2026-09-24 scope update:** Continue P4 terrain/environment work on the P4A procedural generator. Move coordinated exterior-wall layers, turret/cover placement and their defensive validation into P4B. Existing gate-band and baseline enclosure evidence is retained but does not establish layered exterior walls. The separate fixed-compound matrix remains superseded.

Add foothill/mountain carving, roof support, preservation of natural rock boundaries, outdoor utility/mortar areas, lakeshores, peninsulas, and islands with verified approaches. Keep baseline enclosure/gate legality and climate-driven food/power alternatives here; reserve usable terrain and material inputs for P4B fortifications.

Integrate Geological Landforms through generated terrain first; add metadata/per-cell biome access only where needed. Register tested versions. Detect geometry-affecting incompatibilities, including altered water passability and unexpected late terrain changes.

**Exit:** accepted plans survive final-map validation across the terrain matrix. Unsupported island/mountain candidates fall back without damaging the landform. Outer appearance, arrival access, and circulation receive visual review; screenshots alone do not establish pathing or roof safety.

### P4A — Procedural settlement layouts

**Depends on:** P3 and the reusable P4 terrain/mutation contracts. P4 and P4A now proceed together under the user's authorization of 2026-09-23.

Replace the fixed whole-settlement compound with a bounded, seeded planner that
derives facility capacities from the profile/population, packs terrain-legal room
envelopes and districts, generates their shared circulation and portals, and then
derives the baseline enclosure, gate endpoints and fortification reservations from
that same solution. Existing gate-band support remains; complete exterior layering
and coordinated defensive equipment belong to P4B. Reusable small room-furnishing patterns remain allowed. A compact
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
access remains connected through the baseline enclosure and existing gate bands; and representative
live pathing, defense, visual, and rollback cases pass. The pure foundation and
tech-stage steps 1–6 are merged. Runtime integration and remaining P4 terrain work
share one generation path; neither milestone closes from the fixed-layout prototype.

**Implementation order:** runtime catalog and local resources; persisted seed and
faction stage; plan and final occupancy validation; transactional spawning of
walls, doors, room fixtures, fields, utilities, equipment, and finite stocks;
geometry-based P3; terrain and baseline gates plus P4B reservations; Core and Geological
Landforms normal/rejection/recovery, save/reload, and visual/path/job evidence.

**2026-09-28 population update:** Newly generated non-quest settlements derive their roster from a stable per-settlement relative spend capacity and the existing equipment/import budget model. Normal wealth funds seven residents, low wealth can fund four, and wealthy settlements are capped at twelve. The provisional wealth distribution has an expected population of seven across settlements, independently of player wealth and storyteller raid points. Tech/climate determine the standard resident-kit cost; equipment affordability determines population before housing, crops, storage, supplies and roles are planned. Keep gear and import allowances distinct, never inflate an insufficient budget after selecting a roster, and do not add a separate unfunded worker/mortar crew. Persist the wealth choice; preserve previously generated legacy four-resident plans. See [ordinary population policy](ordinary-settlement-population.md).

**2026-09-29 upfront reservation:** Size the first envelope from full facility demand plus population-dependent circulation/frontage allowance. Protect the connected field approach and multi-layer gate frontage before room packing; distinguish protected walking lanes from physical occupancy. Retain hard geometry/material/path validation and the full funded roster through optional-defense fallback. A larger footprint alone is not proof of a connected, usable settlement; synthetic evidence and remaining limits are recorded in the population policy.

### P4B — Defensive layout planner

**Depends on:** P4 terrain contracts and P4A procedural layout. **Status:** active; P4B.0 baseline recorded, implementation/live evidence pending. Detailed steps and evidence: [P4B defensive layout plan](p4b-defense-plan.md) and [findings](p4b-findings.md).

Add 1–3 physical exterior-wall layers along declared eligible sections, coherent materials/import budgets, connected crossings through every layer, defender firing/retreat positions, sandbags/barricades and powered turrets. Include uranium slug turrets (`Turret_Sniper`, research `SniperTurret`) from IndustrialLate, with prerequisite closure, finite uranium rearming, effective range/line-of-fire, power, service and threat-budget checks. Availability is conditional on resources and viable geometry, not guaranteed for every settlement.

P4A reserves geometry; P4B plans defenses before the shared spawn transaction and revalidates final occupancy, utilities, roofs, terrain, interactions and routes. Feed actual positions into P3. Permit bounded repair/replanning, never disconnected walls or assumed firing through opaque structures. Use small adaptable arrangements rather than another whole-settlement generator.

Borrow only lightweight fortification ideas: coverage of approaches, reduced blind spots and overlapping support where feasible, an inner fallback, and connected service/retreat routes. Validate them against RimWorld behavior; no general tactical optimizer, mandatory star forts, traps or IEDs.

**2026-09-30 adopted simulation-review work:** Extend P4B.2–P4B.4 with threat detection at attacked wall/gate segments, bounded reinforcement from a funded mobile reserve, actual simultaneous staffing of supporting turret/pawn groups, and field-to-compound fallback. Rank combined useful fire and vulnerable approaches instead of unconditionally preferring a new turret front. Retain staffed mortars and stage-legal long-range guns against stationary distant attackers. Compare changes at fixed terrain/roster/budget and verify wall assaults, decoys/split fronts, sniper harassment, movement under fire and distinct field/compound/garrison outcomes in game. Details and evidence limits are in the [adopted P4B work](p4b-defense-plan.md#adopted-simulation-review-work--2026-09-30). This is planned work: no unlimited pursuit, universal eight-turret groups, blanket budget increase, larger population or shell-reserve reduction is adopted. P5 reserve sizing and P6 difficulty tuning remain downstream.

**Exit:** demonstrated layered perimeter geometry and connected gates; usable, realistically staffed ranged/turret positions and cover; bounded response to wall/gate threats and field-to-compound fallback; finite supply/power operation including uranium depletion/rearm; representative raid, save/reload, terrain/GL and transactional recovery evidence. Gatehouse bands alone do not prove layered outer walls. P5 budgets the resulting defense load; P6 balances it.

### P4C — Visually realistic bases

**Depends on:** the production P4/P4A layout and P4B's final geometry/route contracts. **Status:** planned and authorized as the next assignment after P4B; preserve its completed implementation and behavior. Detailed scope and checkpoints: [P4C visual plan](p4c-visual-plan.md).

Add a bounded finishing pass: coherent floors by room function, a small faction/stage/local-material/wealth palette, selected existing circulation paths and thresholds, and safe smoothing of eligible retained rock walls and floors inside excavated rooms. Prioritize practical interiors before optional domestic accents; sterile floors and luxury finishes require actual legality and budget. Keep crops, water, exterior mountain faces and unrelated terrain intact. Preserve generated geometry, population, equipment, defenses, roof support and job/public/service access.

Plan finishes deterministically after geometry is fixed and apply them within the shared spawn transaction. Account for construction/import value without consuming operating/trade reserves twice. Extend explicit mutation ownership and rollback to terrain layers and retained-rock conversions; audit approved changes without disabling unowned terrain/rock preservation. Unsupported or unaffordable finishes downgrade or omit with reasons. P4E retains laboratories, temperature-control gaps, seating and storage adjustments; P4C does not add a new decorating framework or economy simulator.

Implement P4C.0 definition/transaction inspection, P4C.1 pure finish policy, P4C.2 transactional integration/recovery and P4C.3 visual/live/reload validation, committing each validated checkpoint. Record before/after or same-layout overlay evidence for industrial open ground, mountain interiors and a modest tribal settlement, plus protected crop/water boundaries and representative available GL compatibility.

**Exit:** visibly coherent, affordable room/path finishes and safe interior rock smoothing on the production generator; preserved terrain, crops, roofs, circulation and defense behavior; deterministic planning, exact forced rollback/retry and active save/reload; representative visual/mechanical evidence and documented omissions. Full unload/revisit persistence remains P5. P4C does not close incomplete neighboring milestones.

### P4D — Quest-location support

**Depends on:** P4/P4A procedural generation, P3 scoped AI/lifecycle, P4B defenses and merged P4C finishes. **Status:** complete for the bounded Core bandit/rescue contracts. Detailed contracts/checkpoints: [P4D quest-location plan](p4d-quest-plan.md); evidence and deliberate GL/other-generator native routing: [P4D findings](p4d-findings.md). Ordinary settlement eligibility guards are preserved.

Implement Core bandit-camp elimination first, then guarded prisoner rescue (`PrisonerWillingToJoin` with `Outpost`). Allow only verified whole site-part/faction/generator combinations; unknown threats, special DLC sites and third-party generators retain native behavior. Guarded item stashes are a documented follow-on candidate, not required initial scope.

Adapt authoritative quest threat points, pawn-group seed/kinds/occupants, loot allowance and objective references to the shared planner. Never apply ordinary population/wealth rolls, cap a quest roster at twelve, reroll it to fit, or add unfunded defenders. Use compact camp requirements and deliberate finite-supply/no-field support only where the contract allows it. Reserve objective space before packing; preserve the actual prisoner/items and native custody, tags and rescue behavior. P3 controls only admitted combatants.

Replace only verified generation steps and preserve native callbacks, arrival/fog/bookkeeping, signals, rewards, timeouts and removal rules. Extend transactional ownership to borrowed quest objects: rejection changes nothing, and late failure restores holders, identities, generation context and P4C terrain/rock/finish state before native fallback runs once. Do not emit irreversible quest outcomes during provisional generation. Site-scoped state and active reload must not duplicate pawns, loot or rewards; verify native departure/re-entry/removal behavior rather than applying ordinary settlement regeneration.

Commit P4D.0 source/native baselines, P4D.1 pure contracts, P4D.2 bandit integration, P4D.3 protected-prisoner integration and P4D.4 validation/closeout separately. Test real native quests through success/failure/expiry as applicable, target rescue/death, rewards, departure, active reload and forced rollback, plus relevant terrain/GL and unsupported-site regression. Keep P4E changes separate.

**Exit:** both initial contracts have a documented support matrix and pure/build/live normal, rejection, late recovery and lifecycle evidence. Original threat inputs, objective identities, rewards and signals survive; unsupported/competing generators and ordinary settlements retain their routing. A generated screenshot or bandit-only pass does not close P4D.

### P4E — Small adjustments

**Status:** complete (2026-10-12); P4E.0–P4E.5 verified, with final recovery, warehouse fetching and Core/GL evidence in the findings. Laboratories are decorative, with no NPC research progression/jobs. Detailed checkpoints, resource rules and acceptance: [P4E facility and defense plan](p4e-facilities-plan.md). The six user requests below remain the scope authority.

1. Check which rooms are still missing the temperature control. For example, a workshop.
2. Add a missing laboratory room for each eligible level with reasonable equipment (should be based on the research tree levels).
3. The workshop desks (and possibly others) do not have a chair or a stool by their side. Chairs and stools should be correctly oriented. Currently for dining tables they are not.
4. Medicine should also be stored on a shelf.
5. Mortar position improvements. The shells should be stored in a separate micro warehouse near the mortar post. It should be 1x1 warehouse with a small cabinet and a door (roofed). This is a safety measure plus it reduces reload time. Settlements with more than 8 pawns can have up to 3 mortars (9 -> <=2, 10+ -> <=3).
    ```
    Mortar position
    
    ____
    _XX_
    _OX_
    #@__
    
    # — shell storage
    
    @ — pawn
    O — tile indicating the mortar's operating-side orientation
    ```
6. Increase the defense budget, we need more turrets. Also make big auto turret available for the relevant settlements.

Implement P4E.0 baseline/definition audit, P4E.1 room temperature and laboratory equipment, P4E.2 seat orientation and medicine shelves, P4E.3 compact mortar magazines and multiple crews, P4E.4 increased ordinary defense allowance/autocannon selection, and P4E.5 integrated validation. Commit each validated checkpoint.

Reuse existing planner/catalog/transaction/P3 systems. Interpret the 1×1 magazine as one usable storage cell plus walls/door, verifying actual loading access and using the smallest functional enlargement only if needed. Mortar limits are at most one through eight residents, two at nine, and three at ten or more, subject to stage, funded crew, space and supplies. Migrate the existing single-mortar controller/save contract coherently. An enclosure does not guarantee explosive safety.

The defense-budget increase is explicitly authorized here; choose a bounded documented default through matched comparisons and account for added power/ammunition. Make the vanilla autocannon (`Turret_Autocannon`) genuinely selectable at legal stages while preserving uranium slug and close-support rules. Keep ordinary population and P4D quest-owned threat/loot/target contracts unchanged. New labs and fixtures must retain P4C finishes, room/roof safety and all interaction/public/service routes; P5 supply/trade and P6 broad balance remain separate.

**Exit:** all six requests have validated production behavior: observed work-room thermal control and eligible labs, functional oriented seating and medicine shelves, accessible finite mortar stores with distinct crews up to the roster cap, and an increased useful ordinary turret defense including live autocannon operation. Relevant full suites/build, old/new active reload, late rollback/retry, representative terrain/compatibility and P4D regressions pass; findings and limitations are recorded.

### P5 — Encounter supplies, trade stock, and persistent consequences

**Status:** authorized and in progress (2026-10-12), from completed P4E `be39af9`.
See [P5 checkpoint plan](p5-encounter-plan.md) and [P5 findings](p5-findings.md).

**Depends on:** P3, P4, P4A and P4B. Revised by the user on 2026-09-20; [supply and MVP scope](supply-and-mvp-scope.md) defines the detailed contract.

Use three in-game days as a provisional routine-supply horizon and a separately stated combat duty budget (initial target: six hours of engagement at declared equipment-specific firing duty within that visit). These are tunable planning defaults, not evidence of how long players stay. Back routine needs and defense with finite actual stocks, account for food spoilage and actual connected power loads, and maintain essential needs/refueling/reload/hauling/tending support. Production is best effort; no unobserved harvest or manufacture may fund the guaranteed encounter budget. Existing larger P2 reserves need not be reduced.

Divide inventory into operating reserves and sale goods without double counting. Prototype access to the settlement's actual current trade inventory and restock generation before selecting integration hooks. Read access can generate stock, so snapshot acquisition must be explicit, saved and separate from the pure planner. Reuse existing stock when present; initialize previously absent stock once through a guarded supported path. For supported ordinary goods, materialize actual trade quantities/attributes into storage or transfer ownership through a single-authority adapter. Completed purchases, sales, looting, destruction, emergency reserve transfers and reload/re-entry must reconcile to those same quantities. Do not reroll goods or leave both a sellable copy and lootable copy. Test lazy/empty-stock and restock boundaries. Trade-stock generation is allowed as the game's bounded trade abstraction; encounter-time replenishment and an economic simulator are not required or allowed as shortcuts.

Keep trade goods within storage and loot/value constraints by changing capacity or the authoritative offer coherently before publishing it; never silently truncate already offered stock or duplicate it. Persist/log unsupported item categories; live trade pawns/animals and exotic mod containers need separate adapters and are not materialized in the first MVP trade adapter. They must not be cloned or silently destroyed. Unknown trader behavior is reported as unsupported rather than claimed consistent. Hostility may block trading but must not reroll previously existing goods.

Review physical stock placement alongside reserve/trade ownership. Check the actual fuel and shell definitions for hazardous behavior; where needed, reserve a small separate accessible stock area away from beds, food and the sole evacuation route. Preserve finite quantities, capacity, hauling/reload access and the single trade-stock authority. Do not require a new building type for every stock category or implement a general blast simulator. Validate realized placement and resupply, with a representative hazard case for any supported explosive stock.

Retain loaded-map serialization and unloaded damage/depletion/casualty reconciliation. Shortening the food horizon does not defer no-duplication correctness: bought or looted goods stay gone, killed/captured residents do not respawn, power/fuel state survives, and claimed assets stop obeying the NPC controller. P0's coarse regenerated defenders remain a prototype limitation to resolve here.

**Exit: MVP encounter-readiness milestone.** Supported profiles have quantified routine/combat coverage, essential support actually works without player orders, stocks deplete honestly, and declared loads/horizons are observed in representative encounter tests. Supported trade items match the canonical offer/transactions and never count twice or regenerate on empty reads, reload or re-entry. Negative tests include exhausted fuel/ammo, loss of a critical worker or generator, buy-then-attack, sell-then-attack, loot-then-revisit, empty stock and a restock boundary. Failure under overwhelming damage is allowed; unexplained refill or fabricated pass evidence is not. Check required state/capture persistence independently of long-term sustainability.

**Outside MVP:** two completed crop cycles, seasonal/winter self-sufficiency, long-running balanced shell manufacture, sustained labor/economic optimization, and off-map production/convoy simulation. Keep facilities and plausible supporting resources, and document unverified long-term behavior. P6 can release the MVP without these deferred tests; use the explicit post-MVP backlog in the supply scope document.

### P5A — Additional improvements

1. A room with fueled generators can be overheated, add a ventilation connected to a room with outdoor temperature.
2. Add more wood to the warehouse, the generators must be filled fully from the start (do not cut this fill from the storage).
3. Add more shells. At least 30 HE + 10 incendiary shells per mortar.
4. The fridge rooms should be used only for the things that require freezing. Other things must be stored in the regular warehouses. Add more cabinets if needed. Cabinets are incorrectly oriented.
5. We still have no floors in some rooms.
6. The wealthiest spacer settlements can have uranium/plasteel walls.

**Status (2026-10-13, branch `claude/p5a-improvements`):** implemented; pure tests and the layout matrix pass, live checks pending.
1. Power rooms with fueled generators get one vanilla `Vent` per two generators (`LayoutFixtureKind.WallVent`). The vents reuse the cooler exhaust path (street or sealed vent shaft), and the validator and spawner audit check that each one opens to outdoor air.
2. Generators spawn with full tanks. `LayoutPowerPlan.FuelStock` = full tanks + the three-day warehouse stock, and storage space is sized from the warehouse part only.
3. 30 HE + 10 incendiary per mortar. One magazine shelf cell holds exactly those three stacks. The import budget is still charged only the declared six-hour duty (12 HE + 2 incendiary); the larger reserve is operating stock.
4. Freezer shelves accept only perishables (defs with `CompRottable`). Warehouse shelves refuse them when a freezer exists. Shelves now face into their rooms, and the magazine shelf faces its door.
5. Every room has a floor choice (flagstone or planks before industrial floors). Each room's cheapest floor is reserved before paths and upgrades spend the allowance, and a room with some unsupported cells is still floored on the rest.
6. Spacer-stage settlements with 11+ residents (the 165–180% wealth rolls) build their perimeter, outer-layer and gate-band walls from plasteel or uranium. This is logged with its value and kept apart from the import budget (`PROCEDURAL_PERIMETER_STUFF`).

### P5B — Better management

1. All the defenders shouldn't sleep at the same time, they should have some rotation.
2. The defenders should have different states of the threat: 
  - when the caravan arrives but it doesn't attack, they don't need to put max defenders to the posts. They can use mortars and have reasonable number of defenders on the posts (the posts relevant for the potential direction of the attack). The others can do some everyday activities/sleep/eat.
  - when the attackers start the assault, everyone should be alarmed, even the ones sleeping
3. Currently defenders go to some strange point when they change activity (internal post?). It looks strange.

### P6 — Balance, specialized tactics, and release

**Depends on:** P5, P4B defenses and supported P4 terrain coverage.

Add bounded special-shell selection and targeting, and verify actual EMP/smoke use rather than just stock presence. Keep these disabled for profiles without validated behavior. Calibrate faction capabilities, resident/defense caps, reserve horizons, material imports, loot, threat presentation, and generation/runtime cost. Complete the mod's settings, user documentation, debug reporting, and supported-version matrix.

Add a compact route report over final occupancy for food storage–kitchen, food storage–dining, workshop–materials and defensive post–medical access. Use public circulation plus legal room-local paths to relevant interactions or stocks; no private-room shortcuts or exterior bypasses. Reuse/cache existing distance calculations. Treat distances as quality diagnostics, not daily traffic simulation or new hard rejection rules.

Use that evidence to decide whether missing soft preferences (for example food storage–dining) or route-based scoring of a small fixed number of valid candidates improves layouts. Reuse the existing sparse relationship model; proximity, mandatory access and functional isolation remain distinct. Do not add relationships already implemented. Compare identical seed/terrain sets for route quality, acceptance/fallback rate, diversity and search work. If tuning has no demonstrated benefit, record that result and retain the current behavior; no new optimizer is required.

Preserve deterministic generation while tuning: stable candidate ordering, fixed evaluation budgets, purpose-specific seeded randomness and reproduction diagnostics containing relevant input/settings/catalog versions or digests. Timing is observational, never a search stopping rule. These practices do not establish Multiplayer support.

Judge defenses through representative player settlement raids: usable firing positions, retreat access, breach response and readable tactical choices. Tune within faction, resource and threat budgets rather than maximize defender invulnerability or force every approach into a perfect killbox. This extends the existing encounter review, not the set of simulated raid/DLC systems.

Compare against vanilla and the two reference mods on a declared test set using reproducible seed/settings records; each generator runs separately. Measure operational and encounter outcomes rather than asserting superiority from appearance. Plan any optional faction/DLC/ammunition adapters as separately gated changes.

**Exit: MVP release gate.** Release checklist passes; no unresolved severe/medium correctness defects; known limitations and post-MVP sustainability work are documented; mod removal/migration claims match tested behavior. Revised P5 requires encounter readiness and stock consistency, not long-term self-sufficiency.

### P7 — Settlement specialization (post-MVP)

**Depends on:** P6 release validation and P5 stock/persistence contracts. **Status:** planned, not started. P8 follows it as the last planned item; P6 remains the MVP release gate.

Start with two specializations in addition to the existing general-purpose profile:

- **Agricultural:** a larger share of suitable fields or tech-appropriate protected growing space, food handling/preservation and storage, with crops and scale constrained by climate, fertility, labor and resource budgets.
- **Industrial:** a larger share of faction-appropriate workshops, material/product storage and power capacity, with short workshop–storage routes and supporting inputs. Do not grant equipment or production chains beyond the faction's capability.

Evaluate the following additional specializations, implementing only those that provide a clear difference with supported facilities and bounded effort:

| Candidate | Distinctive facilities/resources | Feasibility boundary |
|---|---|---|
| Mining and quarrying | Mineral/stone handling, cutting/smelting where legal, bulk storage, terrain-led work areas | Differentiate a stone quarry from ore extraction through local resources; no fabricated renewable ore or requirement to simulate a regional mine economy. |
| Ranching and animal products | Pens/shelter, feed storage and climate-appropriate animal support | Requires usable NPC animal care and finite feed; do not add livestock until ownership/lifecycle works. Live trade animals remain outside the ordinary-item adapter; supported animal products can be considered separately. |
| Forestry and wood processing | Woodland harvesting areas, wood stores and faction-appropriate woodworking | Only where biome/resources support it; no guaranteed tree-regrowth cycle or invented sawmill building. Avoid if it adds only a cosmetic stock difference. |
| Trading and distribution hub | More trade storage, handling space and accessible internal routes | Depends on canonical stock/storage/value constraints. No friendly-visit UI, convoy simulation or automatic access to all goods. |
| Military and logistics outpost | Greater barracks, armoury, magazine and defensive/support emphasis | A strategic role rather than a manufacturing economy. Stay within threat/loot budgets and finite supplies; do not automatically make it the richest trader. |
| Medical supply settlement | Treatment space, herb cultivation where suitable, medicine storage/production where legal | Verify supported recipes and inputs; avoid a mandatory hospital-service or visitor simulation. |
| Research and technical settlement | Tech-appropriate laboratory/precision workshop emphasis and specialist material storage | Research has no automatic trade output. Include only if it produces a meaningful playable layout; no new research progression, quest or DLC system required. |

These are candidate profiles, not seven mandatory new subsystems. Start with agriculture/industry, then rank the candidates by visible gameplay difference, existing supported defs/jobs, terrain suitability and implementation cost. Shared small facility patterns are allowed; defer candidates that require a new simulation. Keep specialization separate from faction tech, terrain archetype and defensive doctrine: an industrial profile may still be coastal or mountain-based, and higher defense does not imply higher tech.

Implement these as small profile/demand adjustments in the existing procedural planner, not fixed whole-settlement templates or a separate generator. Specialization changes room capacities, facility emphasis, spatial preferences and resource allocation; all settlements retain essential housing, food, medical, recreation, defense and encounter support. Keep resident/wealth/defense limits comparable when demonstrating the differences.

Choose specialization deterministically from faction capabilities and terrain/resource suitability, allow an explicit debug override for testing, and persist its identity/version with the settlement. Use general-purpose generation when a specialization is infeasible, with a recorded reason. Do not force agriculture onto unsuitable terrain or reroll an established settlement after settings change. Apply the feature to newly generated settlements first; existing visited maps and their stocks remain intact unless an explicit migration is designed and tested.

**Trade-stock feasibility check, before promising specialized offers:** inspect the supported settlement trader/stock-generator hooks and determine whether we can influence product categories, weights and quantities at initial stock creation and legitimate restock. Check faction/mod compatibility, saved identity, ownership and offer publication boundaries. Separate the ability to place an existing offer on the map (P5) from the ability to change what products the settlement sells (P7). Record supported hooks and limitations, then run a small controlled initial-stock/restock/transaction/reload experiment. If safe influence is unavailable, retain the authoritative offer unchanged and label trade specialization unsupported; physical specialization can still proceed without invented sale goods.

Use the P5 adapter to align future authoritative trade-stock generation with specialization only where that feasibility check succeeds: agriculture can bias eligible crop/food goods, industry eligible materials/manufactured goods. Existing offers and completed transactions remain authoritative; do not infer new stock quantities from room labels, replace an already published offer, duplicate reserve goods or promise an unsupported trader integration. Specialization is a coherent starting profile and best-effort production emphasis, not a requirement for seasonal self-sufficiency, simulated trade routes or an off-map economy.

**Exit:** each additional candidate has a recorded feasibility decision (implemented, deferred or rejected, with reasons); implemented candidates meet the same validation gates. Controlled same-terrain/tech/population comparisons show meaningful agricultural versus industrial differences in capacities, geometry and supporting resources beyond labels or decoration; repeated fixed inputs reproduce the same result. Representative live cases pass normal facility/route/defense/utility checks. Supported future trade offers and physical stocks agree, and save/reload/revisit preserve specialization, depletion and ownership. Infeasible-profile fallback is observed and reported. No traps/IEDs or new long-term sustainability gate.

### P8 — Off-map recovery (last planned item)

**Depends on:** the P5 encounter archive/restore (`P5EncounterArchive`, reserve ledger, stock epochs). **Status:** planned, not started.

P5 freezes an unloaded ordinary settlement: a revisit restores the exact damage, destroyed buildings, generator fuel, battery charge, loaded shells and operating-reserve remainder, however much time has passed. Only the native trade offer expires (30 days), and resident health follows native world-pawn ticking. This is conservative but implausible over long absences.

Tie recovery to the native 30-day restock cycle, the point where vanilla already treats a settlement as having moved on:

- **Under 30 days since the last unload:** unchanged P5 behaviour. A short retreat-and-return finds the base exactly as it was left.
- **30 days or more:** the settlement has recovered. Damage is repaired, archived-destroyed buildings are rebuilt, generator tanks and batteries are full, loaded shells are reset to the fresh state, and operating reserves are restocked to the planned amounts. This means clearing the asset archive and the reserve ledger at that boundary, not fabricating partial states.
- **Roster:** losses stay meaningful. Dead and captured residents are not replaced, and the surviving roster is not topped up. Residents keep their own gear and inventory.
- **Trade stock:** follows the existing epoch policy, with off-map native restock and no restock on an attack re-entry. Any change to that policy is a separate decision.
- **No proportional decay or partial repair:** it adds rules for little gameplay benefit.

Record the unload tick in the archive, make the recovery decision a pure function of elapsed ticks (testable at the boundary), and log `recovered` versus `frozen` on re-entry. Quest sites (P4D) are out of scope because native removal prevents revisits.

**Exit:** pure boundary tests cover elapsed 0 ticks, 30 days minus one tick and 30 days. A live revisit before the boundary reproduces the P5 frozen state, and one after it shows repaired and rebuilt buildings, full tanks and charge, and restocked reserves, with the same surviving residents and no replacements. Save/reload preserves the unload tick.

## 4. Validation strategy

Separate pure planning tests from live engine checks. Pure tests cannot prove a pawn takes a job or a mortar fires; live screenshots cannot prove a planner is deterministic.

| Test area | Required evidence |
|---|---|
| Determinism | Same plan digest for fixed world/profile/settings/mod versions; isolated random streams; saved bases do not reroll after setting changes |
| Geometry | No overlaps/out-of-bounds placement; valid foundations/roofs; every required interaction cell and resident route reachable under correct faction rules |
| Route quality (P6) | Final-occupancy detour fixture; repeatable route report; same-seed comparison of any tuning against validity/fallback rate, diversity and bounded work |
| Stock placement (P5) | Supported hazardous stocks have justified placement, legal resupply access and unchanged authoritative quantities |
| Specialization (P7) | Agricultural/industrial capacity and layout differences under controlled inputs; suitability fallback; stable identity and canonical trade-stock consistency |
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

The remaining MVP path is **P4/P4A procedural terrain and facilities → P4B defensive layout → P5 bounded supplies, safe stock placement, trade consistency and persistence → P6 route/encounter tuning and release validation**. **P7 settlement specialization follows the MVP, beginning with agriculture/industry and evaluating the additional candidate profiles; P8 off-map recovery is the last planned item.** P0–P3 are recorded complete; the progress tracker is authoritative for status. Full sustainability remains a separate unsequenced post-MVP backlog, not a prerequisite for P7.

The original effort estimate is superseded by completed P0–P2 work and this scope revision. Re-estimate remaining work after the P5 trade ownership/restock spike: shorter sustainability validation removes work, while faithful trade-stock reconciliation adds a distinct integration risk. No replacement schedule is claimed without that evidence.

Current defaults: Core 1.6; one-to-three wall thickness; ordinary doors as gates; conditional natural terrain enclosure; no traps/IEDs or default antigrain; bounded prior imports; finite routine/combat reserves; separate trade-stock ownership; best-effort real jobs; frozen off-map operating balances. Provisional encounter targets are three in-game days routine and six game hours combat at declared duty. Calibrate without silently changing acceptance criteria.

Remaining decisions requiring experimental evidence include trade-stock acquisition/ownership/restock hooks, complete unload/revisit reconciliation, measured encounter load/duty budgets, and attainable performance limits. Keep the accepted P0/P1 boundaries; seasonal simulation cost is post-MVP.
