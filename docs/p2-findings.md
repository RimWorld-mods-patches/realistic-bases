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
| P2.0 | Scope, checklist, assumptions, and evidence policy | This documentation checkpoint | Document review |
| P2.1 | Pure versioned industrial/tribal profiles and quantitative budgets | Pending | Pending |
| P2.2 | Pure deterministic full-facility layout and validator | Pending | Pending |
| P2.3 | Plan-driven spawning and realized-map audit | Pending | Pending |
| P2.4 | Runtime/recovery/optional-mod matrix and closeout | Pending | Pending |

## Initial supported profiles and declared scenarios

Both profiles target a small roster of four residents on an ordinary temperate, roofless, buildable site. Facility sizing is derived from that roster rather than incidental room area. The initial profile boundary is deliberately narrow; mountain, island, arid, extreme-temperature, and sustained-autonomy contracts remain later milestones.

The industrial-temperate profile will provide four ordinary sleeping places, separate medical and empty-prison beds, electric cooking and refrigeration, outdoor legal food crops, dining/recreation, workshop and categorized storage, one wall layer with faction-owned gatehouses, a connected generation/conduit/battery network, finite startup food and generator fuel, firearms/armor, cover, and capability-legal turrets. Its declared startup power scenarios are daytime normal load and night critical load, with finite battery/fuel state represented explicitly. The freezer must be observed below its target threshold with a cooler whose cold side faces the room and whose hot side exhausts to an unroofed, passable exterior cell.

The tribal profile will provide the same resident, jail, kitchen, agriculture, dining/recreation, workshop, storage, medical, perimeter, gatehouse, equipment, and cover categories using tribal-legal content. It uses a pantry and finite preserved/raw food and fuel instead of refrigeration, conduits, batteries, generators, electric benches, or turrets. No electric substitute may be inserted by fallback.

Startup food will be computed from resident nutrition demand, a declared first-harvest delay, an explicit safety margin, and effective crop-growth assumptions. Crop area and initialized growth value are counted in the resource/value report, but initialized plants are spawned only once and never replenished. Imports have a hard value allowance distinct from the locally sourced construction budget. Equipment, stocks, structures, and initialized crops all consume a recorded budget category.

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

P2.0 document review is complete. No P2 implementation, pure test, build, or live runtime result has yet been claimed. P2.1 is the next step. P3 is unstarted.
