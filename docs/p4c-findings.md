# P4C visual finishing findings

## P4C.0 — Core and ownership baseline (2026-09-30)

The production entry point is `P4ProceduralSpawner.TrySpawn`, after the final P4B layout, approach defense and turret network have been selected. `P4SpawnTransaction` owns excavated natural rock, changed roofs, cleared plants and spawned things. It has no terrain-layer ledger. Its pre/post natural-rock checksum excludes excavation only, so converting a retained wall currently fails `Commit`. The existing forced-partial failure happens after crops, not specifically after floors or smoothing. A floor pass must therefore precede commit and add two separately injectable failure points.

The installed RimWorld 1.6 Core definitions give a deliberately small first palette:

| Finish | Resolved Core facts | Policy |
|---|---|---|
| `WoodPlankFloor` | 3 WoodLog/cell, no research; flammability 0.22 | Domestic rooms only, after available wood and import allowance are checked. Never fuel/power storage. |
| `Concrete` | 1 Steel/cell, Stonecutting; beauty -1 | Cheap industrial utility fallback, not a claim of cleanliness bonus. |
| `PavedTile` | 2 Steel/cell, Stonecutting | Selected main circulation, thresholds and practical rooms when affordable. |
| `Tile{Sandstone,Granite,Limestone,Slate,Marble}` | 4 matching blocks/cell, Stonecutting, beauty +1 | Optional coherent domestic upgrade only for a locally recognized stone type, but charge its material value unless a finite local quantity is actually available. |
| `SterileTile` | 3 Steel + 12 Silver/cell, SterileMaterials, cleanliness +0.6, beauty -1 | Optional funded medical upgrade, never an assumed default. |

The first pass should omit carpet: Core exposes it through generated terrain templates and the current planner does not have a bounded cloth/dye stock. This is an explicit omission, not a universal claim about carpet legality. Core floors are `layerable`, require `Heavy` terrain affordance, and change path/beauty/cleanliness/fire behavior; the pass must inspect the resolved def and actual original cell. Natural soil, cropland and shoreline cannot be treated as interchangeable foundation.

Core natural rock defs have `building.smoothedThing` mapping to a distinct `Smoothed*` building. The smooth def sets `isNaturalRock=false` and changes beauty/value; it inherits physical wall behavior but is not interchangeable with natural rock in the current `LayoutRoofSupport` snapshot classifier or unowned-rock checksum. The mapping and impassability/roof-holder/opacity must be checked at runtime before a whole-cell conversion. Do not smooth exterior faces merely because one side touches an interior. Stone floor smoothing has a `TerrainDef.smoothedTerrain` runtime member; Core XML does not name a universal smooth-rock floor. If the resolved source lacks a legal smooth target, omit it rather than substituting an arbitrary floor.

Recovery contract: record original top and under-terrain for every owned floor cell before changing either layer; record original natural-rock def and health plus roof for every selected smooth wall. Restore in reverse mutation order, with roof support continuously present during any wall replacement. Extend the exact terrain/roof and unowned-rock audits by excluding only declared, actually selected conversion cells, then verify those cells against their expected result. On rollback compare original top/under-terrain, rock, roof and plants and allow retry. No blanket checksum suppression.

Budget contract: finish spending is only the unused remainder after the final layout material ledger, P4B cover/ports and turret imports. Charge every finish material at its resolved market value, including stone blocks, unless a finite local quantity is recorded. Do not consume the physical food, fuel, ammunition or trade stock a second time. If the allowance cannot cover a whole room at its chosen tier, downgrade the whole room or omit it with a reason; optional finish failure may not reject a valid settlement.

Baseline for same-layout review: the current procedural spawner never calls `terrainGrid.SetTerrain` and never creates a smoothed wall. Thus successful current maps use map-generated ground under rooms and their retained rock is raw. P4C evidence must compare the selected plan footprint with this baseline at the *same* layout digest, not with a different random site.

P4C.0 is definition/contract inspection only. It establishes no runtime success and does not close the remaining implementation or validation gates.

## P4C.1 — Pure finish policy (2026-09-30)

`P4CFinishPlanner` is an engine-independent pass over the final layout and an explicit site/definition snapshot. It ranks kitchen and medical finishes first, then other functional rooms, then social/domestic rooms, and finally selected main corridors. A room gets one floor across its entire interior, including future furniture cells, or receives an omission reason. The small Core policy uses wood for suitable domestic rooms, industrial concrete/paved tile for practical rooms, optional funded stone tile for wealthy domestic rooms, and researched/funded sterile tile for wealthy medical rooms. Poor tribal sites cannot acquire industrial floors through this policy. Smooth floor is chosen only if the whole room resolves to one supported matching source-to-target mapping. Rock walls are limited to interior-facing planned natural walls, with exposed exterior faces, unsupported roof conversions and excess work omitted.

The plan has version `p4c-finish.1`, stable cell/def maps, sorted reasons, allowance/import/local-wood totals and a digest. It computes the remaining import allowance after finalized P4B material/cover/port and turret costs, then caps finish spending to 35% of the settlement import limit. Only wood beyond the finite unused local yield is charged as imported; every stone tile is charged. This is intentionally a conservative construction allowance, not a transfer from physical stock.

Focused pure LayoutTests passed in the existing Docker SDK image (`--only P4C`, 1/1) on 2026-09-30. Assertions cover digest replay, immutable input/layout identity, whole-room choices, unsupported cell and crop exclusion, allowance, unavailable definitions, poor tribal legality and smoothing ownership. This is not yet runtime definition capture, mutation, rollback or visual evidence.

## P4C.2 working evidence — attached outdoor paths (2026-10-01)

The finish pass now includes all planned compound corridors, room thresholds, gatehouse crossings, and the named gate-to-gate arrival lane through the attached field bailey. It does not blanket-pave public yard space, crop cells, or the road beyond the field's outer gate. The field route needed an explicit exception to the otherwise protected field rectangle, while crop cells still take precedence. Gate tiles lie on the perimeter rather than inside `layout.Enclosure`; including them closed a one-cell soil seam in an otherwise concrete route.

The focused P4C test requires a joint site and asserts full selected field-route coverage, a finished compound gate, crop preservation and no floors beyond the named perimeters. The full LayoutTests suite passed (98 cases, zero failures; 463808 ms) and the Release mod build had zero warnings/errors. In a paused disposable flat-map run, `PROCEDURAL_SPAWN_COMPLETE` reported layout `87783659E58DFE21`, 378 finish floors, 45 smoothed walls, 240 crops and no finish omissions. The live terrain grid showed Concrete continuously along x=58, z=17–45, across the field and compound gates; z=9–16 remained Soil outside the outer field gate. This validates that specific live route, not every possible site or P4C.3's reload/compatibility gates.

The [mountain overview](p4c-evidence/mountain-overview.jpg) predates the attached-route expansion; it documents room/wall/crop appearance and is not evidence for final path coverage.
