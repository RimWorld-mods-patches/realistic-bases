# P4B defensive layout: findings and validation

2026-09-27 · Status: P4B.0–P4B.1 complete; P4B.2 in progress; P4B.3–P4B.4 open

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
| P4B.1 | Deterministic 1/2/3-layer section topology on flat, irregular, corner, narrow, rock and water fixtures; exact physical wall/door counts; legal materials/imports; one public root with every crossing and service/retreat path | Final-map structures/terrain/roof/path audit for representative accepted plans | Complete: pure wall-connectivity/corner recovery and flat/mountain live return, path, checksum and forced-rollback audits; GL compatibility remains in P4B.4 |
| P4B.2 | Joint field/compound reservation, attached field wall and gates, exterior posts and cover, blocked-lane and blind-zone negatives | Actual defender shots, cover use, and unobstructed lanes on accepted maps | In progress: final pure plan and live spawned field/wall/gate/cover/post audits pass; P3 reaches posts. Actual exterior-post firing/cover use remains unverified. |
| P4B.3 | IndustrialLate research prerequisite closure and lower-tier exclusions; resolved footprint/power/range/refuel/explosion values; effective difficulty-sensitive uranium accounting including loaded ammunition; power/material/threat/stock budget and transaction preflight | Turret firing, power loss, uranium consumption/rearm/exhaustion, breached gate/wall fallback and active reload | Pending |
| P4B.4 | Full pure suites, Docker Release, static no-trap/IED and final occupancy/recovery regressions | Representative player raid, Core and GL terrain, forced partial rollback and save/reload | Pending |

Every validation entry must distinguish pure test, build, runtime log, and observed game behavior. Screenshots may illustrate geometry but do not establish firing, cover, pathing or consumable depletion. A plan rejection with zero mutations is safe recovery evidence, not evidence that the fortification works. Do not mark P4B complete without the live matrix.

## P4B.1 physical wall layers and access

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

The strengthened P4B.1 prototype requires at least three physical outer-arm cells
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

The remaining corner-recovery and live constrained-terrain/roof checks are
recorded below. P4B.2–P4B.4 remain open.

### Detached-wall correction

A user screenshot exposed a defect in the earlier prototype: straight arms
joined the gate band but ended as freestanding piers. The previous live
wall-count and service-lane audits did **not** prove physical connectivity.
Each flank now has an explicit return to the baseline/previous layer; a flank
without a straight arm uses a short gate-band corner return. A candidate
without every required return rejects before mutation, and the return cells
participate in the digest, material ledger, occupancy and spawn transaction.
The validator checks return geometry and anchor ownership. A new pure graph
regression checks that every outer wall cell has a cardinal wall path to the
enclosure. The 1/2/3-layer flat, pond/ridge, mountain-face, shoreline,
water-corner and narrow-approach fixtures pass, as do PlannerTests and the
Docker Release build. In a fresh disposable Core flat three-layer run, the
spawn audit reported 11/11 outer walls, 5/5 return cells, 4/4 anchored
returns, 8/8 band walls, 3/3 gate doors and 2/2 reachable service endpoints.
The in-game view showed the gate flanks returning into the existing wall.
This is flat-map connectivity evidence only, not a constrained-terrain,
combat or P4B closeout. The test process was stopped afterward.

### Corner recovery and constrained live terrain

A new pure water-corner fixture places a permanent obstruction at a previously
valid return cell. The old plan is rejected for `outer_section_unreturned`;
bounded replanning produces a different valid three-layer layout whose every
outer wall cell remains cardinally connected to the enclosure. The input
terrain digest is unchanged. The full layout suite and Release build pass.

A fresh disposable Core Mountain/Caves map accepted three requested layers
without downshift: 11/11 outer walls, 5/5 return cells, 4/4 anchored returns,
8/8 band walls, 3/3 doors, 1/1 gate route and 2/2 reachable service termini.
It excavated 106 rock cells. Final-map validation reported unchanged terrain
after the settlement/power steps, preserved terrain/roof state, no traps or
IEDs and a reachable player arrival. In a separate clean Mountain/Coast run,
a forced partial spawn rolled back 623 owned things, restored 514/514 plants
and the terrain/roof checksum, then the same three-layer plan spawned with the
same wall/return/path counts. This completes P4B.1's Core wall/access and
recovery gate. It does not prove fire lanes, cover use, turret operation, GL
compatibility, or the P4B.4 raid/save matrix.

## P4B.2 sightline baseline (not a completed gate)

### Joint-site pure preflight checkpoint (not yet integrated)

`P4BJointSitePlanner` now supplies a bounded, deterministic field-first pairing prototype. Its compact square candidates count connected, unroofed, fertile crop cells after the reserved wall and one-cell hauling lane are removed, then require a neighboring connected compound envelope with the requested usable-cell capacity and outside-post clearance. Pure regressions replay the reservation digest, reject an attractive isolated soil patch in favor of one with adjacent compound room, and reject roofed fields. This is a search contract only: the main procedural layout does not yet consume these reservations, and the field wall is not spawned. The next checkpoint must join it to room packing and validate the actual shared boundary/crossings.

The next pure checkpoint threaded the selected pair through an opt-in detailed-layout path: the existing room/street/power planner sees only the compound envelope as placeable, the resulting layout carries the immutable site digest, and final validation rejects any crop, field-wall or outside-post footprint consumed by compound geometry or constructed roof. A four-resident 160×160 flat synthetic case produced a complete valid layout and exact replay. This remains opt-in for pure tests; live `P4AMapPlanner` has not switched over and no field wall is yet authorized to spawn.

The pure final-geometry checkpoint now precomputes a bounded family of field gate/hauling-lane alternatives inside each paired site. The selected compound gate must align with one of those previously reserved interfaces. The field wall's two arcs each attach physically to the compound perimeter (directly or through bounded exterior connectors); the replacement arrival path crosses the inner and outer field gates and reaches the map edge, rather than relying on the obsolete compound-only route. Final layout acceptance includes this wall plan, exact crop cells, outside posts and its additional wall/door construction ledger. The flat four-resident fixture passes deterministic replay and final validation. This is still pure/synthetic evidence; the live planner and spawner have not switched over.

2026-09-27 live checkpoint: the production P4A path now requires the joint site and spawns its attached enclosure transactionally. A clean disposable Core run accepted a one-layer 160×160 map with 156 unroofed rice cells (124 minimum), 88/88 attached field-wall cells, 2/2 gates, 2/2 cover fixtures, and 2/2 reachable exterior posts. The spawn audit found zero wild plants within the established field and at least two P3-legal pawn paths per post. Later P3 audits reached three occupied posts with zero defenders outside defended bounds; the game view showed the rice-filled outdoor enclosure and main-perimeter connection. Rice is placed at generation; no growing zone or farm-work/replanting loop is claimed. Earlier post-path audit rejections rolled back all spawned things and restored all cleared plants; ordinary site-search rejections still occur on some live maps with zero mutations, usually `room_unplaceable`. This is not yet P4B.2 closeout: no raid has demonstrated exterior-post firing or cover use, and P4B.3 turret operation is still open.

The user clarified the target topology: keep the main field unroofed and outdoors, surround it with wall, and put fighting positions outside that field-facing wall. Planting within the existing inner courtyard is not the target. A gate-adjacent exterior-post prototype was tested in a disposable Core flat map (two reachable posts, two barricades, two P3 assignments), but it did not enclose the field or prove live shots. That prototype was removed from production rather than treating its counts as P4B.2 evidence. The remaining implementation must co-plan the field, its perimeter and crossings, and posts beyond it.

The inherited `GeometryEncounterPlanner` chooses gate posts near the inside
endpoint and ranged posts beside a wall, setting `HasLineOfFire=true` without
tracing a shot. `P4BFireLane` now supplies a conservative pure prefilter for
Euclidean range and grid occlusion, including closed opaque doors and diagonal
wall slits. Synthetic clear, blocked-gate, exterior-flank, minimum/maximum
range and diagonal-slit cases pass. It is not a substitute for RimWorld's
runtime shot check. A diagnostic-only generated-plan audit samples the first
12 cells of each exterior gate route against planned opaque walls and closed
doors. In a clean Core flat three-layer plan, it found 0/6 inherited direct
posts with a shot and 0/12 sampled approach cells covered. The map still
spawned and passed its separate wall/access audit; this result identifies a
combat gap, not a generation failure. P4B.2 must next plan exterior fighting
positions, cover and fallback routes in final occupancy, then confirm actual
shots and defender use in-game before treating any post as valid.

## Implementation interface to preserve

Add a defense plan derived from the immutable procedural layout and resolved runtime catalog *before* `TrySpawn` mutates the map. Its selected walls/doors/fixtures/stock and route reservations must be part of one final occupancy model, material and power ledger, digest/report, and preflight. The existing transaction owns every defense spawn so a forced failure rolls it back. P3 consumes verified posts from that final model; it must not independently infer ranged utility from wall proximity. Repair is bounded and deterministic; a rejected candidate remains zero-mutation. Natural rock may contribute to protection, but it is not a fabricated built-wall layer.
