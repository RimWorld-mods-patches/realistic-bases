# Scarce cropland and minimum main-field size

An ordinary settlement keeps its original population and nutrition demand when
natural cropland is scarce. After the full-field site search fails, an explicit
food contract can combine an available natural field, funded cultivation at an
eligible tech stage, and finite food imports. All existing material, excavation,
room-access, roof, storage, power and defense checks still apply.

The minimum main field is **25 actual eligible natural crop cells**. Fields
with 0–24 cells are omitted entirely; exactly 25 cells are retained. This count
excludes hauling lanes, hydroponics, bonus yard crops and unusable fertile cells.
A retained field cannot choose an alternative gate interface with fewer than 25
crops. An omitted field has no wall, gates, access ports, outside field posts,
physical reservation or natural-production credit. Its former search rectangle
remains metadata for deterministic compound growth, but is excluded from defender
areas, finishing protection and native garrison envelopes. Ordinary compound
arrivals, approach defense and optional turret planning still run.

`P4BJointSite.MinimumMainFieldCells` owns the threshold. `HasMainField` and
`FieldReservation` distinguish physical land use from search geometry. A fieldless
ordinary layout is legal only with an explicit food contract crediting zero
natural crops; unrelated missing-field errors remain rejected by spawn preflight.

Eligible hydroponics require captured native basin fertility and sun-lamp radius,
legal research, fully lit basin cells, a roofed grow room, heating/cooling, usable
access, power and fuel. Hydroponics research belongs to IndustrialEarly and its
later cumulative stages. Packing uses both basin orientations: a radius-5.8 lamp
can serve at most 24 basins in the supported 11×11 interior, with an empty corner
entrance and climate fixtures. Capacity is not an instruction to purchase surplus
basins: a food plan buys the number needed and physically feasible within budget.

Initial food covers the profile's first-harvest horizon (currently eight days)
with the existing safety factor. Startup food and the import supplement share
one inventory ledger. Imports cover production shortfall and horizon top-up
without double counting. Future harvests do not reduce that initial inventory;
there is no automatic continuous resupply. Crop production remains an estimate,
not proof that NPC farming jobs operate over the entire horizon.

## Regression coverage

`FoodSupplyTests` checks 0/24/25 boundaries, rejects sub-threshold alternative
interfaces, verifies one-lamp packing, retains a useful partial natural field,
and runs a complete fieldless ordinary settlement through layout/loadout/spawn
preflight. The latter checks no field reservations or ghost defended rectangle,
zero natural crop credit, funded cultivation/imports and unchanged terrain.

The captured DesertPlateau input `E2431BDDBD3CC306` / terrain
`7131D30BFF05BE36` previously enclosed two crops. With omission and dense packing,
its offline replay accepts layout `C75ECFEFF0F9B0B6`: no main field, 23 basins,
one sun lamp, 143 startup meals, eight funded fuel days and ordinary imports
8,243 / 8,250.

A fresh native recheck on RimWorld 1.7.4995 confirmed the actual GL DesertPlateau
context and accepted that same layout. The spawn audit counted 92 planted basin
cells and one sun lamp; all 128 planned consumers powered on one network, all
eight residents had beds and ten empty housing cells, and ordinary gates and
sally ports remained reachable. Independent inventory reads counted 143 meals
and 468 warehouse chemfuel, alongside 390 generator startup fuel. Both forced
floor/smoothing failures restored the item ledger, 41/41 loose chunks, terrain
checksum and layers (0/0 pre-existing plants on this footprint). The settlement
was shown paused at tick 583 and saved as `DesertPlateau-Threshold25-2026-11-04`.
This is generation/static-access evidence, not elapsed farming or defender
movement validation. Historical Caldera's separate 899/899-plant rollback record
is unchanged.

The complete layout suite passed 200 tests with one existing skipped peninsula
case; the planner/garrison suite passed. The mod build completed with zero errors
and four existing warnings. Exact logs, captures, screenshot and reproducible
replay script remain under the ignored `fixer/main-field-threshold/` investigation.

Historical Badlands input/terrain anchors and Caldera layout
`7C899E3CEF005B1D` still replay unchanged. The separate Caldera modeled spawn-gate
regression retains its zero-versus-four eligible-defender result. Original and
current native RiverSource replays also pass at their recorded layouts.
