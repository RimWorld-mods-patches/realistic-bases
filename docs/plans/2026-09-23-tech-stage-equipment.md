# Plan: tech-stage-driven settlement equipment

2026-09-23 · Status: **design agreed; not implemented**. This plan extends the P4A foundation (`docs/p4a-foundations.md`). It does not start P5, and it does not modify P4 work in progress.

## Goal

A settlement's furniture, buildings, construction materials, power, weapons, apparel, utility gear, and stocks follow its faction's technology stage. Legality is derived from vanilla research prerequisites, not from hand-written lists. Tribal settlements stay tribal: no Bed, Shelf, stone blocks (unless adapted), smithy, or powered items.

## Vanilla evidence (Core 1.6 defs)

- **Settlement factions:** Tribe (Neolithic), Outlander (Industrial), Pirate (Spacer). There is no Core Medieval faction.
- **Starting research:**
  - `TribalStart` = Pemmican, TreeSowing, PsychoidBrewing, PassiveCooler, RecurveBow.
  - `ClassicStart` = ComplexFurniture, Stonecutting, ComplexClothing, Electricity, AirConditioning, NutrientPaste, PassiveCooler.
- **Research gates:**
  - ComplexFurniture: Bed, Shelf, DiningChair, ChessTable, OrnateDoor, Vent.
  - Stonecutting: TableStonecutter, which is needed for stone blocks.
  - Smithing: FueledSmithy, SimpleHelmet, and medieval melee.
  - ComplexClothing: HandTailoringBench and ordinary clothes.
  - FlakArmor needs Machining. HospitalBed needs MicroelectronicsBasics. VitalsMonitor needs MultiAnalyzer.
  - PrecisionRifling (assault and sniper rifles) needs GasOperation and MicroelectronicsBasics.
  - The revolver, bolt-action rifle, and pump shotgun need no research; their techLevel is Industrial.
- **Generators:**
  - WoodFired: 2×2, 1000 W, 22 wood/day.
  - Chemfuel: 2×2, 1000 W, 4.5 chemfuel/day.
  - WindTurbine: 7×2, up to 2300 W, needs a clear zone.
  - Solar: 4×4, 1700 W in daytime.
  - Watermill: 5×6 on a river.
  - Geothermal: 6×6 on a steam geyser.
  - Battery: 600 Wd at 50% efficiency.
  - Core has no Spacer-only source.
- **Existing defects this fixes at integration:** the P2 tribal profile uses `Bed`. P2 and the P4A tribal workshop use `FueledSmithy` and `HandTailoringBench`. The P2 industrial profile uses `Apparel_FlakVest` without FlakArmor.

## Agreed decisions

| Topic | Decision |
|---|---|
| Stage source | `FactionDef.techLevel` sets the band. A faction-wide sub-seed picks the stage within it. The stage is persisted per faction, so every settlement of a faction shares it. |
| Ladder | Six stages: Neolithic, Medieval, IndustrialEarly, IndustrialMid, IndustrialLate, Spacer. Each is a cumulative research set plus a `TechCeiling`. |
| Bands | Neolithic factions get Neolithic, with a small seeded chance of Medieval. Medieval factions (mods) get Medieval. Industrial factions get Early–Late. Spacer factions get Mid–Spacer. |
| Catalog | A research filter over live defs plus an ordered role-preference table. The pure planner receives an immutable `TechCatalog` snapshot. |
| Wood scarcity (tribal) | A Stonecutting adaptation is granted when local wood < construction need × 1.25 and local stone exists. Walls use stone blocks, and fuel wood is charged as imported. If both wood and stone are missing, all wood is imported, or the settlement rejects with `material_budget_exceeded`. |
| Power | Dispatchable fuel generators cover the critical load. Renewables cover only non-critical load, on open-sky yards **inside the enclosure**. Batteries stay indoors. The rock-protect doctrine means fuel only. |
| Residents | Fully generated from the stage. Vanilla supplies body, skills, and traits; gear is removed and re-equipped by P3 role, climate, and budget. |
| "Tools" | Work buildings by stage, component spares, and utility belts or packs. Modded tool items are out of scope. |

## Stage research sets (cumulative)

| Stage | Added research | TechCeiling |
|---|---|---|
| Neolithic | TribalStart | Neolithic |
| Medieval | ComplexFurniture, ComplexClothing, Stonecutting, Smithing, LongBlades, PlateArmor, Greatbow, CarpetMaking | Medieval |
| IndustrialEarly | ClassicStart, Batteries, Machining, Gunsmithing, Mortars | Industrial |
| IndustrialMid | BlowbackOperation, GasOperation, FlakArmor, GunTurrets, MicroelectronicsBasics, SterileMaterials, HospitalBed, PrecisionRifling, DrugProduction, MedicineProduction, SolarPanels, WatermillGenerator, GeothermalPower, BiofuelRefining, SmokepopBelt, Firefoam | Industrial |
| IndustrialLate | MultiAnalyzer, VitalsMonitor, MultibarrelWeapons, HeavyTurrets, Fabrication, Prosthetics | Industrial |
| Spacer | ShieldBelt, ChargedShot, ReconArmor, PoweredArmor, Bionics | Spacer |

The prerequisite-closure check against Core defs found SterileMaterials (needed by HospitalBed) and Prosthetics (needed by Bionics); both were added to the table.

Adaptations (such as Stonecutting) add research per settlement with a recorded reason. The IED research is never used: the prohibited-content classifier removes traps and IEDs before legality is checked.

## Implementation steps

Each step is validated (Docker Release build with 0 warnings/errors, pure tests, `git diff --check`) and committed separately. Steps 1–6 are pure and can start now in the P4A worktree. Steps 7–8 need P4 to be finished.

1. **Stage model.** Add `SettlementTechStage` (level, ceiling, research set, adaptations with reasons), the stage bands, `LayoutSeed` faction sub-seed selection, and a schema version.
   - Tests: stable per-faction stage; every band respected; the sets are monotonic.
2. **Catalog and legality.** Add `CatalogDef` and `TechCatalog`. `IsLegal` checks research ⊆ set and techLevel ≤ ceiling, after the prohibited-content filter. Generate a synthetic Core catalog fixture from the extracted def data for the pure tests.
   - Tests: golden legal and illegal lists per stage (tribal: Bedroll yes, Bed no, FueledSmithy no, Revolver no); no trap or IED at any stage.
3. **Role table.** Ordered preferences for sleep, medical bed and monitor, seat, table, light, heat, cool, cooking, crafting benches (smithy, tailoring, stonecutter, machining, fabrication), storage, recreation, door, defense, and utility belts. Unresolved roles become diagnostics, and an essential unresolved role steps the stage down one level.
   - Tests: resolution per stage; step-down behaviour.
4. **Planner integration (pure).** Furnishing asks for roles; footprints, blocking, interaction offsets, and power come from `CatalogDef`. Spots are 1×1 and non-blocking. Materials are chosen per stage from local stuff, with import accounting. The Stonecutting adaptation uses the new snapshot inputs `LocalWoodYield` and `LocalStoneStuffs`. The workshop gains a stonecutter under the adaptation.
   - Tests: tribal layout contains no ComplexFurniture items; wood-poor stony map → stone walls and imported fuel wood; wood- and stone-poor map → budget rejection.
5. **Power progression (pure).** Replace the generator count with a mix solver: a dispatchable floor for the critical load, renewables for the rest where legal and on open-sky yards inside the enclosure, and batteries sized to the outage scenario. Add a three-day fuel stock. Add snapshot traits `River` and `SteamGeyser`; until the adapter provides them, those sources are reported as legal but unplaceable. The protect doctrine means fuel only.
   - Tests: tribal has none; early is fuel only; mid on open sky adds solar or wind; rock-protected mid is fuel only with a larger stock; fuel quantity matches rate × horizon.
6. **Gear and stock model (pure).** Weapon choice per P3 role × stage; apparel by layer, climate, and stage; quality distribution; armoury of spares at about 25%; medicine and component tiers; utility belts. Everything is charged to the equipment budget, and trade stock stays separate (P5).
   - Tests: role × stage golden tables; cold climate adds parkas and dusters; budget overrun rejects.
7. **Runtime catalog adapter (after P4).** Build `TechCatalog` from `DefDatabase<ThingDef>`, including DLC and mod defs. Persist the stage per faction. Capture `LocalWoodYield`, `LocalStoneStuffs`, `River`, and `SteamGeyser` in the snapshot adapter.
8. **Runtime equipping and spawning (after P4).** Strip and re-equip residents, spawn stocks, and fix the P2 profiles through the catalog. Live matrix: a Core tribe (wood-rich and wood-poor), Outlander early, mid, and late, Pirate Spacer, cold and hot climates, rock-protected and open doctrines, and one Geological Landforms map, plus save/reload.

## Out of scope

Trade-stock composition (P5), modded tool items, long-term production and sustainability, and new Spacer power sources.

## Open calibration values (provisional)

The Medieval chance for tribes, the stage weights within each band, the ×1.25 wood threshold, armoury spare share, quality distributions, and battery sizing all need live calibration before they are treated as final.
