# P2 first complete physical settlement findings

Updated: 2026-09-20

## Status and evidence rule

P2 is **in progress**. P0 and P1 evidence remains valid for their own integration and planning contracts, but it is not evidence that P2 facilities, utilities, supplies, defenses, or capability profiles work. A building appearing in a plan, source trace, log, or screenshot is not sufficient: P2 closes only after pure rules, compilation, spawning, and the required live runtime properties are separately observed.

Evidence is classified as:

- **pure tests:** deterministic profile, budget, placement, topology, and capability rules without Verse or map mutation;
- **build/static checks:** repository-prescribed Docker Release build, XML parsing, whitespace, and prohibited-content checks;
- **runtime checks:** isolated RimWorld observations of the realized map, reachability/usability, roofs, doors, interaction cells, power behavior, freezer temperature/exhaust, finite stocks, crop initialization, equipment, defenses, and recovery.

No test is recorded as passed until it has run. Reduced fallback layouts are named and assessed against the same facility contract; the P1 diagnostic shell does not qualify as a P2 settlement.

## Bounded implementation steps and commits

| Step | Intended result | Commit | Evidence |
|---|---|---|---|
| P2.0 | Scope, checklist, assumptions, and evidence policy | `f4c2c64` | Document review |
| P2.1 | Pure versioned industrial/tribal profiles and quantitative budgets | `038ca4d` | 20/20 combined pure tests passed; Docker Release build 0 warnings/errors |
| P2.2 | Pure deterministic full-facility layout and validator | This step's feature commit | 29/29 combined pure tests passed; Docker Release build 0 warnings/errors |
| P2.3 | Plan-driven spawning and realized-map audit | Pending | Pending |
| P2.4 | Runtime/recovery/optional-mod matrix and closeout | Pending | Pending |

## Initial supported profiles and declared scenarios

Both profiles target a small roster of four residents on an ordinary temperate, roofless, buildable site. Facility sizing is derived from that roster rather than incidental room area. The initial profile boundary is deliberately narrow; mountain, island, arid, extreme-temperature, and sustained-autonomy contracts remain later milestones.

The industrial-temperate profile will provide four ordinary sleeping places, separate medical and empty-prison beds, electric cooking and refrigeration, outdoor legal food crops, dining/recreation, workshop and categorized storage, one wall layer with faction-owned gatehouses, a connected generation/conduit/battery network, finite startup food and generator fuel, firearms/armor, cover, and capability-legal turrets. Its declared startup power scenarios are daytime normal load and night critical load, with finite battery/fuel state represented explicitly. The freezer must be observed below its target threshold with a cooler whose cold side faces the room and whose hot side exhausts to an unroofed, passable exterior cell.

The tribal profile will provide the same resident, jail, kitchen, agriculture, dining/recreation, workshop, storage, medical, perimeter, gatehouse, equipment, and cover categories using tribal-legal content. It uses a pantry and finite preserved/raw food and fuel instead of refrigeration, conduits, batteries, generators, electric benches, or turrets. No electric substitute may be inserted by fallback.

Startup food is computed as `4 residents × 1.6 nutrition/day × 8 effective days × 1.25 safety factor = 64 nutrition`. The industrial profile carries 72 simple-meal-equivalent units at 0.9 nutrition (64.8 total); the tribal profile carries 1,280 pemmican-equivalent units at 0.05 nutrition (64 total). Effective crop output is conservatively declared as 0.052 nutrition/cell/day, requiring `ceil(6.4 / 0.052) = 124` crop cells. Crop area and initialized growth value are counted in the resource/value report, but initialized plants will be spawned only once and never replenished. Imports have a hard value allowance distinct from the locally sourced construction budget. Equipment, stocks, structures, and initialized crops all consume a recorded budget category.

The industrial profile declares a 1,000 W fueled generator, 300 Wd starting storage for ordinary day/night cases, and a separate 600 Wd service buffer. Its modeled cases are 12 hours at 850 W load, 12 hours at 650 W critical load, and a six-hour 650 W generator-service outage ending with 437 Wd. These are planning calculations only until actual RimWorld networks, fuel behavior, storage, and loads are observed in P2.3/P2.4. The tribal profile has no power scenario and rejects refrigeration, batteries, electric cooking, power facilities, and turrets.

Both resource budgets enforce independent local construction and imported-value ceilings. The digest covers schema/rules versions, residents, capabilities, crop and nutrition assumptions, finite stocks, facility demands, power scenarios, budget totals, and equipment lists. Industrial and tribal digests are stable and distinct.

## P2.2 layout observations

The initial supported layout is a deterministic 31×25 one-layer compound plus a separate 10×13 (130-cell) legal field. It contains four ordinary bed placements, separate jail and medical beds, kitchen, freezer or pantry, dining/recreation, workshop, bulk storage, field, gatehouse, defense area, and industrial-only power area. Roofed facility envelopes have explicit perimeter doors and interaction cells; the remaining compound interior is an explicit logistics reservation. The validator rejects missing capacity, overlapping roofed facilities, overlapping non-conduit placements, obstructed/illegal field cells, disconnected interaction access, and prohibited trap/IED names.

North and south faction-gate openings have independent deterministic routes to the map edge. Route searches are bounded by the smaller of the snapshot area and 10,000 visited cells. The two-gate topology is intended to preserve an approach and a retreat; actual hostile door ownership and realized pathing remain P2.3 runtime checks.

The industrial plan includes a fueled generator, battery, cooler, electric workbenches, conduit tree, two miniturret reservations, and four cover positions. Every powered terminal is on or adjacent to the single planned conduit tree. The cooler is placed in the freezer's west wall with its declared cold side east into the room and hot side west into an exterior, unroofed snapshot cell. The tribal plan contains a pantry, fueled benches, non-firearm equipment, and cover, and the validator hard-rejects any generator, battery, conduit, cooler, turret, or electric-def placement.

Local wall material is now an explicit, stable, sorted input to the layout digest and the selected wall material must belong to that supplied local palette. Map-side discovery of the actual rock/material palette and def legality remains part of P2.3 integration rather than being inferred from terrain labels in pure code.

## Fixed inherited contracts

- Planning reads an immutable terrain snapshot and performs no Verse or map mutation.
- Stable schema/profile versions, explicit seeds, deterministic ordering, bounded candidate/route work, and plan digests remain mandatory.
- Spawning consumes a validated plan in a separate phase and preserves rollback-before-fallback behavior.
- The dedicated ordinary-settlement generator remains narrowly guarded and off by default.
- `MapCommonBase` terrain/mutator order, settlement-time terrain preservation, playable edge arrival, and final-map auditing remain in force.
- No primary, repair, recovery, or fallback path may generate a trap or IED.
- P0 coarse revisit state and regenerated defenders are not expanded or represented as P2 persistence completion.
- P2 proves startup readiness, not sustained NPC autonomy, advanced sector AI, specialized mortar tactics, or terrain archetypes assigned to later milestones.

## Exit gate

P2 completes only when both supported profiles realize every required facility and all are reachable and usable; ordinary housing covers four residents without counting jail or medical beds; prohibited defenses are absent; gates have correct faction ownership and preserve attack and retreat topology; the industrial network survives its declared day/night/load scenarios using real connected generation, storage, and finite fuel; freezer temperature is observed with valid exhaust; quantified food bridges the declared effective first-harvest delay; initialized stock/crop value is accounted once; and the tribal profile contains no electric substitutes. P1 determinism, rejection, partial-spawn rollback, and optional-mod compatibility must retain appropriate regression coverage.

## Current evidence and pending checks

P2.2 is complete. The combined P1/P2 console suite passed 29/29 tests in the prescribed SDK container. The nine layout cases cover deterministic valid industrial planning, tribal exclusions, every facility demand, legal field capacity, cooler exhaust geometry, a connected power tree, dual gate/edge routes plus facility logistics, deterministic bounded rejection, and declared-local material selection. The mod then built Release with zero warnings and zero errors. This proves pure plan properties only; no P2 realized-map property has yet been claimed. P2.3 is next. P3 is unstarted.
