# P4E — Facility and defense adjustments

2026-10-01 · Planned and authorized for implementation after P4D

## Outcome and boundaries

Complete the user's six adjustments from the grand plan: temperature control in occupied work rooms, stage-appropriate laboratories, usable correctly oriented seating, medicine on shelves, compact mortar magazines with up to three mortars for larger settlements, and a larger ordinary-settlement defense allowance with meaningful autocannon availability. Reuse the procedural planner, resolved catalog, shared transaction, P3 controller, P4C finishes and P4D adapters. Implement and validate each checkpoint separately; these are concrete changes, not a new colony simulation.

Preserve population, terrain/roof/field invariants, existing stocks and their ownership, quest identities/signals, and public/job/defense/service paths. New fixtures need space, material and power before spawning. Do not hide room-packing failures by lowering the funded population, omitting a required objective, or making corridors impassable. Keep no traps/IEDs. General production sustainability and canonical trade/revisit reconciliation remain P5; broad release balance remains P6.

## 1. Temperature control by actual room use

Inventory every room kind, including Workshop and the new Laboratory, against the existing occupied-room policy. Audit the actual room assignment of each heat/cool fixture, not just its presence somewhere in the plan. Apply the existing climate and stage policy to rooms in which residents live or work; a new type must not fall through a hardcoded list. Keep freezer targets distinct. Storage, generators and magazines do not automatically need residential comfort control.

Use legal heating/cooling with connected power or finite fuel, reserved interaction cells and genuinely outdoor exhaust. A shared thermal arrangement is acceptable only when actual connectivity/behavior supports it; a nearby cooler in another sealed room does not count. Recompute connected load/headroom and stock demand, retaining defense and essential utility priorities. For primitive stages, report the limits of legal passive cooling/heating rather than inventing air conditioning or promising exact targets in extreme weather.

Validate representative hot/cold occupied-room cases with live room temperatures after sufficient settling time, powered/fueled status, targets, outdoor conditions and elapsed ticks. Do not claim success from a thermostat value alone. Preserve known freezer/exhaust-pocket and retained-roof cases. No full seasonal or thermal optimization simulator is required.

## 2. Laboratory for each eligible stage

Add a Laboratory room demand and research-equipment role using the existing room/fixture/catalog system. Inspect actual research prerequisites and resolved bench definitions: prefer a simple research bench at stages where it is legal, a powered advanced bench when legal and affordable, and a multi-analyzer only when the prerequisite/coupling rules are satisfied. Freeze a small explicit stage matrix during P4E.0, including supported primitive stages; do not gate the entire room on electricity or assume every faction owns the best bench. Record unsupported stages precisely.

Provide the bench's correct working cell, a legal seat where supported, lighting and the same applicable room temperature policy. Validate usable analyzer linkage in game instead of inferring it from room co-location. Include footprint, power, material cost, P4C floor classification, circulation and generation digest/version changes. Append or explicitly migrate persisted enum values instead of shifting old room/role meanings accidentally.

Research equipment makes the settlement plausible; automated research progression, dynamically unlocking new faction technology and a new NPC research-job system are outside this step. Ordinary settlements of eligible stages should plan the lab. Preserve P4D's compact temporary-camp policy: do not force a full lab into a quest camp unless its requirements call for one.

## 3. Functional seats and orientation

Inspect vanilla seating/workbench rules and fix both placement and rotation. A chair by the side of a workbench is insufficient: where the work type supports seating, place it at the correct working position facing the bench. Dining seats must face their associated table; handle all table/bench rotations and sides using resolved footprints/interaction geometry. Extend the same rule to laboratory benches and supported other workstations, without placing chairs at non-seated interaction types such as mortar operating cells.

Treat seats as legitimate occupants of their intended interaction cells where the game permits this. Preserve reachability and passability; avoid double-reserving the same seat for incompatible fixtures or blocking doors/storage/corridors. Use stage-legal chairs or stools and account for their material cost. Validate all orientations with pure geometry fixtures, then observe real sitting/eating or seated work in game using an isolated probe if the NPC controller does not normally perform that task. Do not introduce a broad autonomous crafting system to prove a seat works.

## 4. Medicine on accessible shelves

Reserve a legal small shelf/shelf capacity in or immediately serving Medical storage and place the existing medicine there. Reuse the current pooled-room stock/shelf selection and resolved filters, footprint, stack limits and stuff legality. Keep medicine away from interaction/door cells and reachable to tending/support jobs. Do not treat a visually empty shelf with an incompatible filter as storage.

Preserve finite medicine counts, quality/condition where applicable, and reload identity. Do not duplicate medicine onto both shelf and ground or reduce stocks to fit. If a preferred shelf cannot fit, attempt a bounded legal smaller shelf or nearby medical-storage placement and record why; a stage lacking any legal shelf must have an explicit capability decision rather than an unsupported def. Demonstrate the shelf placement and access in game, not only an assigned stockpile filter. P5 continues to own trade-stock reconciliation.

## 5. Mortar position, magazine and multiple crews

### One usable storage cell, enclosed and roofed

Interpret the requested **1×1 micro warehouse** as one usable interior storage cell containing a legal small shelf/cabinet, with surrounding walls and a door; the enclosure's total footprint is necessarily larger. Start with an existing Core one-cell storage fixture if its actual filter/capacity and access rules work. Do not invent a new cabinet asset. Validate that a pawn outside or at the doorway can actually fetch/load from it; if the one-cell interior cannot function, record the exact mechanic and use the smallest functional enlargement, retaining the compact intent.

Keep the magazine close to its assigned mortar through a short legal loading route, roof only the store, and leave the mortar and firing/operating area unroofed as required by resolved defs. The door and roof must form a real room/enclosure without cutting off the operating cell or public route. Derive mortar orientation and working side from actual interaction geometry. The user's diagram is a spatial intent reference, not authoritative dimensions:

```text
Mortar position
____
_XX_
_OX_
#@__

# shell store; @ pawn; O operating-side orientation marker
```

Inspect shell explosion/fire behavior. Separate storage from beds, fuel and sole escape paths; retain a usable retreat route and avoid placing explosive stacks directly against the operator where a slightly longer loading route materially improves safety. Enclosure and a roof are not a claim of blast immunity. Use bounded spacing/stock rules and a representative live hazard observation, not a general explosion simulator. Preserve finite total shell/barrel quantities, type-selection restrictions and operating-versus-trade ownership. Distribute stocks across stores or shared reserves with one ledger; never multiply the whole former reserve per mortar without accounting for it. P5 still calibrates the encounter-wide reserve horizon.

### Capacity and controller changes

Use the actual permanent resident roster for ordinary-settlement capacity, excluding prisoners, rescue targets, visitors and player pawns:

| Residents | Maximum mortar count, subject to legality, staffing, space and resources |
|---|---|
| Up to 8 | At most 1 |
| 9 | At most 2 |
| 10 or more | At most 3 |

These are upper limits, not guaranteed counts. Low-tech or unsupported profiles still have zero. Keep operators within the funded roster and preserve useful direct-fire/reserve staffing; do not create extra crew. Apply a separate quest-owned cap/allowance for P4D rather than granting extra artillery because a quest has many native pawns.

The current controller and several encounter/planner APIs hold one mortar reference. Replace these assumptions coherently with stable mortar/post/operator identities, one operator per gun and one role per pawn. Handle different rotations, distinct magazines/loading routes, destruction, crew loss, reassignment, loading and barrels, exhaustion, fallback and multiple reservations without contention. Preserve existing single-mortar saves through explicit migration/defaults and active reload; do not simply spawn additional guns while all operators still reference the first one. New generation need not retrofit additional mortars into existing loaded maps.

## 6. More turrets and appropriate autocannons

The user's P4E request **explicitly authorizes an increase in ordinary-settlement defense spending**, superseding the earlier P4B decision to defer any increase. Inspect the current base/import/equipment/defense ledgers and make the adjustment named, bounded and visible in diagnostics. Keep the original population/wealth distribution; do not inflate the combat roster or commandeer operating/trade stocks.

Choose and document a provisional increase using matched same-seed/terrain/stage/population comparisons. A small sweep of the existing allowance and modest increases is sufficient; no global search or blanket adoption of the simulation's 3–4× recommendation is required. Ensure the increased allowance can fund coherent emplacements, initial/reserve ammunition, service access and necessary dispatchable power. If geometry, guard staffing, power or the current group cap is the binding constraint, address it explicitly where safe instead of raising a number that produces no additional defenses. Optional omissions remain acceptable on constrained terrain and poor settlements.

Treat "big auto turret" as the vanilla **autocannon turret (`Turret_Autocannon`)**. Verify its resolved research/prerequisite and faction-stage eligibility and retain IndustrialLate uranium slug support. Prove autocannons can actually be selected and spawned in suitable profiles; a catalog entry hidden behind an unconditional uranium-first choice is insufficient. Select a useful weapon mix against actual range/blind-zone/approach needs, with the established mini-turret/pawn support for large guns. Do not require every settlement to own all gun types or silently give primitive factions industrial weapons.

Retain blast spacing, simultaneous guard staffing, cover, service/retreat paths, final occupancy, finite ammunition and power audits. Compare useful staffed defense and connected load, not only object count. Live checks must observe autocannon firing and consumption/rearm, relevant close support, and power-loss behavior. Report added construction/loot value for later P6 tuning. P4D's authoritative native threat/loot budgets remain unchanged unless a specific quest adapter contract is separately revised and tested.

## Implementation checkpoints and acceptance

Use `docs/p4e-findings.md` for evidence and document chosen defaults. Commit every validated step separately; do not stop after the pure plan or one screenshot.

| Step | Scope | Validation gate | Status |
|---|---|---|---|
| P4E.0 | Baseline room/seat/storage/mortar/budget audit and resolved stage matrix | Recorded in p4e-findings; one-cell source feasibility remains subject to live fetching | Complete |
| P4E.1 | Room-use temperature policy and laboratory demand/equipment | Six-stage/climate, power/bench and 12/12 clutter fixtures; live hot/cold industrial room temperatures and active analyzer in findings | Complete; integrated matrix remains P4E.5 |
| P4E.2 | Correct seated work/dining orientations and medical shelving | All cardinal orientations; actual seating and accessible finite medicine on legal shelves, no path/stock regression | Pending |
| P4E.3 | Compact roofed mortar magazines and multi-mortar planning/controller | Resident cap/staffing fixtures; live distinct operators, loading/barrels/finite depletion, safe routes, crew/gun loss and old/new save reload | Pending |
| P4E.4 | Explicit increased ordinary defense allowance and meaningful autocannon selection | Matched before/after counts, useful staffing, costs/power; live autocannon operation and close support; primitive/quest budget exclusions | Pending |
| P4E.5 | Integrated regression, recovery and closeout | Relevant full suites/Release build, representative ordinary/quest live and active reload, forced late rollback/retry and visual findings | Pending |

Pure cases include supported tech stages; hot/cold/mild room policy; all seat rotations; medicine capacity/filter failures; 8/9/10/12-resident mortar boundaries plus illegal/understaffed/underfunded cases; duplicate crew/stock rejection; deterministic replay and unchanged authoritative quest rosters/budgets. Preserve P4C finish classification for the new lab and any new roofed store, and the existing no-field quest path.

Use a compact live corpus that covers an industrial workshop/lab, modest primitive capability, a 9-resident two-mortar case and a 10+-resident three-mortar case where resources/geometry permit, and a high-industrial autocannon case. Record omissions separately; an extra unstaffed mortar does not pass. Cover a constrained mountain or shoreline layout, P4D bandit/rescue smoke including the original prisoner and native completion, and representative established GL/native-fallback compatibility. Compare actual operator jobs, targets, stocks, net load and pathing with planned values.

Force a late transaction failure after new facilities/stores/guns and recover P4C terrain/rock/finish state, stock counts and protected P4D holder/identity state where applicable before retry/fallback. Save/reload with multiple manned mortars and partly used stocks; verify unique assignments and no replenishment. Follow existing disposable profile/process safeguards and repository skills; no normal saves or unnecessary tooling installation.

**Exit:** all six requested changes have production implementation and recorded pure/build/live evidence, with realistic capability/resource limits; actual room thermal behavior, laboratory equipment, seating and medicine access work; compact magazines and up to three correctly staffed mortars operate with finite stocks; suitable ordinary settlements gain useful turret defense and eligible autocannons can fire/rearm. Existing P4C/P4D contracts, deterministic planning, recovery and active reload remain valid. P5/P6 responsibilities stay separate.
