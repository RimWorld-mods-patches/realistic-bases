# Plan: tech-stage-driven settlement equipment

2026-09-23 · Status: **steps 1–6 implemented (pure); steps 7–8 pending P4**. See `docs/p4a-foundations.md` §10 for what was built. This plan extends the P4A foundation (`docs/p4a-foundations.md`). It does not start P5, and it does not modify P4 work in progress.

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
  - Smithing: FueledSmithy and medieval melee. SimpleHelmet also needs Smithing, but its techLevel is Industrial, so the ceiling rule makes it legal only from IndustrialEarly. Medieval front-liners wear PlateArmor without a helmet.
  - ComplexClothing: HandTailoringBench and ordinary clothes.
  - FlakArmor needs Machining. HospitalBed needs MicroelectronicsBasics. VitalsMonitor needs MultiAnalyzer.
  - PrecisionRifling (assault and sniper rifles) needs GasOperation and MicroelectronicsBasics.
  - The revolver, bolt-action rifle, and pump shotgun declare no research of their own, but they inherit the recipeMaker prerequisite Gunsmithing from `BaseMakeableGun`. Their techLevel is Industrial. Gunsmithing is in IndustrialEarly.
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
| Power | Dispatchable fuel generators cover the critical load (freezer coolers, heaters when the climate needs heat, vitals monitors, powered turrets). Renewables cover only non-critical load, on open-sky yards **inside the enclosure**. Batteries stay indoors. The rock-protect doctrine means fuel only. Ladder: IndustrialEarly = wood-fired generators plus wind; from IndustrialMid, chemfuel generators (chemfuel is legal from Mid), solar and geothermal. |
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
3. **Role table.** Ordered preferences for sleep, medical bed and monitor, seat, table, light, heat, cool, cooking, crafting benches (smithy, tailoring, stonecutter, machining, fabrication), storage, recreation, door, defense, and utility belts. Unresolved roles become diagnostics. Legality is monotone in the stage, so a plain legality filter can never make step-down help. Primitive fallbacks therefore carry a `MaxStage` cap (for example Bedroll up to Medieval). Options can also carry a `MinStage` that keeps them out of simpler tiers: Cooking is ElectricStove, FueledStove (Medieval and up), then Campfire (Neolithic only), so tribes cook on campfires. An essential role with no legal, tier-acceptable option means the settlement cannot equip its tier: the stage steps down one level, keeping adaptations, the reason is logged, and the whole kit is re-resolved at the lower tier. An essential role that is still unresolved at Neolithic rejects with `essential_role_unresolved:<role>`.
   - Tests: resolution per stage; step-down behaviour.
4. **Planner integration (pure).** Furnishing asks for roles; footprints, blocking, interaction offsets, and power come from `CatalogDef`. Spots are 1×1 and non-blocking. Materials are chosen per stage from local stuff, with import accounting. The Stonecutting adaptation uses the new snapshot inputs `LocalWoodYield` and `LocalStoneStuffs`. The workshop gains a stonecutter under the adaptation.
   - Tests: tribal layout contains no ComplexFurniture items; wood-poor stony map → stone walls and imported fuel wood; wood- and stone-poor map → budget rejection.
5. **Power progression (pure).** Replace the generator count with a mix solver: a dispatchable floor for the critical load, renewables for the rest where legal and on open-sky yards inside the enclosure, and batteries sized to the outage scenario. Add a three-day fuel stock. The protect doctrine means fuel only.
   - As built: river and steam geyser cells are `LayoutLocalResources` inputs (`RiverCells`, `SteamGeyserCells`), not snapshot traits, because `TerrainSnapshot` belongs to P1/P4. Yards are placed per attempt after the other rooms; the power room is sized afterwards, and the dispatchable-only worst case sizes storage and the pre-search checks. Batteries = max(1, ceil(critical W × 6 h / 24 / 600 Wd)).
   - Dispatchable fuel from IndustrialMid: wood when the local yield covers the stock after construction; otherwise chemfuel (imported at its catalog value); otherwise imported wood. The fuel stock is stored physically and raises the storage demand.
   - Limitations: wind turbines are placed with rotation North only (other rotations unverified). The watermill is deferred to the P4 river archetype work and is reported as `power_source_unplaceable:WatermillGenerator:river_outside_enclosure` (or `no_river`), because the P4A grid never encloses moving water. Geysers are reserved for geothermal only (`no_geyser` / `no_fit`). Follow-up: wind turbines rarely fit (in 7 of 8 checked Early layouts there was no clear zone), because the North-only 7×18 zone crosses rooms that line the 2-wide streets; other rotations or placing yards before rooms would raise the fit rate.
   - Tests: tribal has none; early's dispatchable is wood-fired only (wind is legal at Early); mid on open sky adds solar or wind; rock-protected mid is fuel only with a larger stock; fuel quantity matches rate × horizon.
6. **Gear and stock model (pure).** Weapon choice per P3 role × stage; apparel by layer, climate, and stage; quality distribution; armoury of spares at about 25%; medicine and component tiers; utility belts. Everything is charged to the equipment budget, and trade stock stays separate (P5).
   - As built: a standalone `SettlementLoadout` from `LayoutLoadoutModel.Build(input, layout)`, checked by `ProceduralLayoutValidator.ValidateLoadout`. Item values port vanilla `CalculatedBaseMarketValue` (costs + stuff + work × 0.0036) with provisional quality factors. The equipment budget default is a per-resident stage table (325 / 900 / 1000 / 1600 / 2400 / 5200, ×1.2 when the climate needs heating), calibrated from measured Normal-quality kits; `FromProfile` takes the larger of the P2 budget and the stage default. Overruns degrade quality one step, then drop the armoury, then utility items, then reject with `equipment_budget_exceeded`. Apparel fabric: leather for tribes, cloth for Medieval and IndustrialEarly, synthread from IndustrialMid.
   - Tests: role × stage golden tables; cold climate adds parkas and dusters; budget overrun rejects.
7. **Runtime catalog adapter (after P4).** Build `TechCatalog` from `DefDatabase<ThingDef>`, including DLC and mod defs. Persist the stage per faction. Capture `LocalWoodYield`, `LocalStoneStuffs`, `River`, and `SteamGeyser` in the snapshot adapter.
8. **Runtime equipping and spawning (after P4).** Strip and re-equip residents, spawn stocks, and fix the P2 profiles through the catalog. Live matrix: a Core tribe (wood-rich and wood-poor), Outlander early, mid, and late, Pirate Spacer, cold and hot climates, rock-protected and open doctrines, and one Geological Landforms map, plus save/reload.

## Out of scope

Trade-stock composition (P5), modded tool items, long-term production and sustainability, and new Spacer power sources.

## Open calibration values (provisional)

The Medieval chance for tribes, the stage weights within each band, the ×1.25 wood threshold, armoury spare share, quality distributions, and battery sizing all need live calibration before they are treated as final. So do the role MaxStage/MinStage caps, the renewable capacity factors, the role shares, the quality value factors, and the equipment budget table.
