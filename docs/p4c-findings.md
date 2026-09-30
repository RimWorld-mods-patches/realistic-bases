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
