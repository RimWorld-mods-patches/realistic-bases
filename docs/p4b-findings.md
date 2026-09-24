# P4B defensive layout: findings and validation

2026-09-24 · Status: P4B.0 complete; P4B.1 partial wall-section evidence, exit gate open

The [P4B plan](p4b-defense-plan.md) is the scope authority. P4 and P4A remain open independently: their lakeshore, climate, tribal, optional-mod and exact rejected-flat-tile checks are not credited to P4B or silently closed. P5–P7 are not part of this implementation.

## Baseline inspected at `daba7f3`

| Existing contract | Observed implementation | P4B consequence |
|---|---|---|
| Wall layers | `ProceduralLayoutPlanner.ChooseGates` reserves a five-cell-wide gatehouse band for each layer beyond the first. `ProceduralLayout.BuiltWalls` is the single general enclosure boundary. | Prior two-/three-layer reports prove serial gate doors/bands, **not** additional physical exterior layers on eligible sections. Record section coverage and omissions explicitly. |
| Planning / transaction | `ProceduralLayoutValidator.Validate` checks the pure plan. `P4ProceduralSpawner.TrySpawn` then preflights fields, conduits and occupied cells before `P4SpawnTransaction`, with rollback on partial failure. | Defense geometry, equipment and supply must enter the plan/preflight before any mutation and use the same transaction. |
| Materials | `LayoutEquipment.Count` accounts for room/enclosure walls, doors and power, but the validator currently counts only `BuiltWalls` plus exhaust-pocket walls, excluding gate-band walls from its wall input. | Count every new wall and gate-band wall, plus selected defense stuff; enforce bounded import value before spawning. |
| P3 posts | `GeometryEncounterPlanner` chooses gate posts within two public cells of doors and ranged posts by adjacency to perimeter walls. It does not trace line of fire through final opaque occupancy. | A wall-adjacent post is not evidence of a firing opportunity. Supply verified positions and fallback/retreat routes to P3. |
| Defensive equipment | The procedural spawner creates planned room fixtures, yards, and a mortar. It does not place selected turrets or cover around the enclosure. `SettlementRole.Defense` lists autocannon/mini-turret/sandbags/barricade, but `Turret_Sniper` and `SniperTurret` are absent. | Add stage legality and actual pre-spawn defense selection; catalog entries alone are not placement evidence. |
| Power / stocks | Conduits are routed from fixture terminals; finite mortar shells and generator fuel are spawned. There is no planned turret load or uranium stock for the procedural path. | Include turret watts, service cells, loaded fuel and reserve uranium in effective finite budgets and final net validation. |

These are code observations, not runtime results. The previous P3 and P4/P4A observations remain valid for their stated paths, but none closes a P4B gate.

## Acceptance gates and evidence log

| Step | Pure/build evidence required | Live evidence required | Result |
|---|---|---|---|
| P4B.0 | Inspect source and record baseline, immutable defense inputs/outputs, reservation/occupancy ownership, test corpus, recovery boundary | None | Documentation baseline only |
| P4B.1 | Deterministic 1/2/3-layer section topology on flat, irregular, corner, narrow, rock and water fixtures; exact physical wall/door counts; legal materials/imports; one public root with every crossing and service/retreat path | Final-map structures/terrain/roof/path audit for representative accepted plans | Partial: pure rock/water/irregular matrix and flat service-lane audit; constrained live and narrow/corner recovery remain |
| P4B.2 | Geometry-driven post and turret range/line-of-fire checks for intended gate state; blocked-lane and blind-zone negatives; reachable cover/service/fallback | Actual defender/turret shots, cover use, and unobstructed lanes on accepted maps | Pending |
| P4B.3 | IndustrialLate research prerequisite closure and lower-tier exclusions; resolved footprint/power/range/refuel/explosion values; effective difficulty-sensitive uranium accounting including loaded ammunition; power/material/threat/stock budget and transaction preflight | Turret firing, power loss, uranium consumption/rearm/exhaustion, breached gate/wall fallback and active reload | Pending |
| P4B.4 | Full pure suites, Docker Release, static no-trap/IED and final occupancy/recovery regressions | Representative player raid, Core and GL terrain, forced partial rollback and save/reload | Pending |

Every validation entry must distinguish pure test, build, runtime log, and observed game behavior. Screenshots may illustrate geometry but do not establish firing, cover, pathing or consumable depletion. A plan rejection with zero mutations is safe recovery evidence, not evidence that the fortification works. Do not mark P4B complete without the live matrix.

## P4B.1 prototype and flat-terrain prerequisite (not a completed gate)

The current prototype selects a seed-weighted 50/30/20 preference for one,
two or three layers, capped by tech stage and declared construction/import
spend per resident. A bounded fallback reduces layers when terrain cannot
support an outer section. Eligible straight gate-adjacent wall runs gain
physical outer sections connected to serial gate bands. The exact wall/door
material ledger, final wall/door realization and gate reachability are checked.
The pure layout suite and PlannerTests pass; Docker Release builds without
warnings or errors.

On the disposable Core flat-map test, an open-ground three-layer plan realized
6/6 outer wall cells, 8/8 band walls, 3/3 gate doors and 1/1 reachable gate;
a second flat-labeled, rock-bearing map realized 3/3, 8/8, 4/4 and 2/2.
Neither proves full concentric enclosure coverage: these are declared short
reinforcement sections. The captured flat-tile plant regression and 460/460
plant rollback are detailed in [P4 findings](p4-findings.md).

The next P4B.1 checkpoint requires at least three physical outer-wall cells
per declared section, an inner run supporting every farther-out cell, and a
continuous walkable lane from each section end to its own gate crossing.
Candidates lacking an outer section reject before mutation; production's
bounded layer fallback can then choose a feasible thickness. The pure suite
accepts 1/2/3 layers on pond/ridge, mountain-face, shoreline and water-corner
fixtures, with a deliberately blocked lane rejected by the validator. The
spawner now clears lane plants within the transaction and audits both actual
lane cells and endpoint reachability. A disposable Core flat three-layer
generation realized 6/6 outer walls, 8/8 band walls, 3/3 doors, 1/1 gate
routes, 12/12 clear service cells and 2/2 reachable service termini. Both
pure suites and Docker Release passed; the game process was stopped afterward.

A separate clean three-layer forced partial-spawn test aborted after 613 owned
things and restored 379/379 cleared plants plus the terrain/roof checksum.
The same plan then spawned successfully: 6/6 outer walls, 16/16 band walls,
6/6 doors, 2/2 gate routes, 12/12 clear service cells and 2/2 reachable
service termini. Its cooler audit also found one freezer unit at −10°C and
four occupied-room units at +21°C. This tests rollback of the new lane-clearing
footprint on flat Core terrain, not constrained-terrain recovery or combat.

P4B.1 remains open for a narrow/corner rejection matrix and live constrained
terrain and roof checks. P4B.2–P4B.4 have not been implemented.

## Implementation interface to preserve

Add a defense plan derived from the immutable procedural layout and resolved runtime catalog *before* `TrySpawn` mutates the map. Its selected walls/doors/fixtures/stock and route reservations must be part of one final occupancy model, material and power ledger, digest/report, and preflight. The existing transaction owns every defense spawn so a forced failure rolls it back. P3 consumes verified posts from that final model; it must not independently infer ranged utility from wall proximity. Repair is bounded and deterministic; a rejected candidate remains zero-mutation. Natural rock may contribute to protection, but it is not a fabricated built-wall layer.
