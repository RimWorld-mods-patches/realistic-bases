# P4A hand-off prompts

2026-09-23. There are two paste-ready prompts: **A** for the P4 owner, and **B** for the agent that picks up P4A after P4 closes. Both refer to commit `f844e43` on branch `claude/p4a-foundations` (worktree `/Users/roman_gr/Repositories/rimworld/realistic-bases-p4a`), which is based on P4 commit `a41477e`.

---

## Prompt A: what P4A needs from P4 (for the P4 owner)

> You own P4 (terrain variety and stronger fortifications) in Realistic Bases. A separate workstream has built a pure P4A procedural-layout planner on branch `claude/p4a-foundations` (commit `f844e43`, based on your `a41477e`). It is not wired into generation. Integration waits for P4 to finish. Please account for the contract needs below while you complete P4, and tell us which ones you will deliver in P4 and which you consider P4A integration work. Don't change P4A files. Details and line references are in `docs/p4a-foundations.md` §1.3, §5 and §9 on that branch.
>
> **Must-haves before P4A can be integrated:**
>
> 1. **Generalize `PlannedNavigationGrid` / `P4Connectivity`** (`Planning/P4Connectivity.cs`). Today it can express only 7 of 24 P4A test layouts, and all 7 pass. It needs:
>    - an explicit public-circulation mask, instead of `Compound.Points()` minus roofed rooms;
>    - an explicit interior endpoint per gate, instead of `InsideGate` inferring it from the rectangle edge;
>    - gatehouse bands facing any direction, not only north and south (`GatehousePassages`);
>    - multi-cell blocking footprints (a 1×2 bed, a 3×1 bench, a 2×2 generator), not single-cell `PhysicalPlacement`s;
>    - a walled-versus-roofed distinction, with room walls blocking whether or not the room is roofed.
>
>    The authority stays yours. P4A's `ProceduralLayoutValidator` becomes a search pre-filter only.
> 2. **Fix the terrain blocking test** at `P4Connectivity.cs:34`. `Has(TerrainTraits.Water | TerrainTraits.Occupied)` requires **both** flags, so water-only cells, and natural rock cells without an edifice, are not blocked. The fixed compound masks this today; irregular enclosures would expose it. Use `HasAny`, plus a non-standable check.
> 3. **Extend `TerrainMutationFootprint`** with retained thick roof: excavated cells that keep their natural `RoofRockThick`. `TerrainMutationPlanner.Validate` currently demands roof clearance for every roofed excavation (`roofed_excavation_without_roof_ledger`). P4A's rock-protected doctrine keeps the thick roof, which is the whole point against mortars. It proves support with vanilla's 6.9-cell holder rule, counting **only intact natural rock** as a holder, never built walls. The spawner, the recovery and the final audit also need to handle retained roofs: record them, check for collapse after excavation, and roll back.
> 4. **Relax `PhysicalSettlementValidator`**: capacity per `FacilityKind` rather than exactly one facility per kind (P4A can produce several barracks or storage rooms); cooler rotation derived from geometry, not the literal `"West"`; gatehouse band geometry derived from the gates, not `MinX+13..17`; and the field placed outside the compound as an interface rather than at four fixed offsets.
> 5. **Extend the plan model** (`PhysicalPlacement` / `PhysicalFacility`):
>    - multi-cell fixtures, with footprint, rotation, stuff and role;
>    - placement kinds for heater, lamp, torch, campfire, passive cooler, shelf, vitals monitor, stockpile zone, and power yards (solar, wind with its clear zone, geothermal);
>    - an optional mortar court: under the rock-protect doctrine, P4A can deliberately drop the mortar.
>
> **Needed from the P4 adapters (runtime):**
>
> 6. **Snapshot additions**, captured by the map adapter and passed to P4A as `LayoutLocalResources`, not as new `TerrainTraits`:
>    - harvestable wood yield near the site;
>    - local stone block defs (`LocalStoneStuffs`);
>    - river (moving-fluid) cells;
>    - steam geyser positions.
>
>    The P4A input also needs the faction id and `FactionDef.techLevel`, and it needs **persistence** of the P4A layout seed per settlement and of the resolved tech stage per faction, so revisits never reroll.
> 7. **Archetypes feeding the usable mask.** Foothill and excavated-mountain geometry, preserved lakeshore water and island approaches should reach P4A's usable-mask builder, so terrain support can be claimed from live evidence. Watermills are deferred until a perimeter can meet river water; please say whether P4 plans such a perimeter.
>
> Please reply with, for each item: delivered in P4 (with commit), planned for P4A integration, or rejected (with reasons).

---

## Prompt B: pick up P4A (for the next implementer, after P4 is finished)

> You are continuing **P4A — Procedural settlement layouts** for the RimWorld 1.6 mod Realistic Bases. The pure planner foundation and plan steps 1–6 (tech-stage-driven equipment) are complete and tested. Your job is runtime integration, meaning plan steps 7–8 plus the P4A integration checklist. **Do not start this until P4 is marked complete** in `docs/progress.md`, and until the P4 owner has answered the contract requests in `docs/p4a-handoff-prompts.md` (Prompt A).
>
> **Where the work is:**
> - Branch `claude/p4a-foundations`, head `f844e43`, worktree `/Users/roman_gr/Repositories/rimworld/realistic-bases-p4a`. It may also have been brought into the main checkout (`codex/p4-connectivity`) or Terra's worktree; check with `git log`.
> - Commits:
>   - Foundation: `74e5d57`, `7144e9f`, `00efed4`, `b08fe05`.
>   - Plan: `0b4fd06`.
>   - Steps 1–6: `3e9ecde`, `087fe9a`, `ae78272`, `487f661`, `f6d7d65`, `f844e43`.
>
> **Read first, in order:**
> 1. `docs/p4a-foundations.md`: the hardcoded dependencies (§1), reuse and ownership (§2–3), integration checklist (§5), design decisions (§6), evidence (§7), limitations (§8), handoff (§9), and tech-stage equipment (§10).
> 2. `docs/plans/2026-09-23-tech-stage-equipment.md`: the agreed decisions, stage research sets, steps 1–8 as built, and the provisional constants.
> 3. The P4 findings and connectivity docs as they stand when P4 closes, plus the P4 owner's answer to Prompt A.
> 4. The code in `Source/RealisticBases/Planning/`:
>    - `ProceduralLayoutModel.cs`, `ProceduralLayoutPlanner.cs`, `ProceduralLayoutValidator.cs` (planner, validator, adapter, ASCII export)
>    - `SettlementTechStage.cs`, `TechCatalog.cs`, `SettlementRoles.cs`, `LayoutEquipment.cs`, `LayoutPower.cs`, `LayoutLoadout.cs`
>    - tests: `Source/RealisticBases.LayoutTests/`; the synthetic fixture is produced by `tools/extract-core-catalog.py`.
>
> **Rules:**
> - Follow the repository's Docker build convention. The host has no .NET SDK. Use the NuGet volume you're told to use; P4A used `realistic-bases-p4a-nuget` to stay isolated.
>   ```
>   docker run --rm -v "$PWD":/mod -v <nuget-volume>:/root/.nuget/packages -w /mod/Source/RealisticBases mcr.microsoft.com/dotnet/sdk:8.0 dotnet build -c Release
>   docker run --rm -v "$PWD":/mod -v <nuget-volume>:/root/.nuget/packages -w /mod/Source/RealisticBases.LayoutTests mcr.microsoft.com/dotnet/sdk:8.0 dotnet run -c Release
>   docker run --rm -v "$PWD":/mod -v <nuget-volume>:/root/.nuget/packages -w /mod/Source/RealisticBases.PlannerTests mcr.microsoft.com/dotnet/sdk:8.0 dotnet run -c Release
>   ```
> - Baseline at `f844e43`: mod build 0 warnings/0 errors, LayoutTests 62/62, PlannerTests 52/52.
> - Use the repository's `run-rimworld-dev` skill for live tests, and only against an isolated disposable profile. Never use another agent's profile or saves, and never touch normal user saves.
> - Keep pure evidence (synthetic tests) and live evidence (observed maps, logs, paths) separate. A screenshot or label is never proof of pathing, roof support or job access.
> - No traps or IEDs, ever. The catalog's prohibited filter runs before legality.
> - Validate and commit each step separately. Record commits and evidence in `docs/p4a-foundations.md`. Keep the shared milestone docs in sync, following the project's own conventions.
> - The carryover `CLAUDE.md` describes a different project and is not this mod's specification.
>
> **Work, in dependency order:**
> 1. **Adopt the generalized P4 contract** (Prompt A items 1–5). Make `PlannedNavigationGrid` the single acceptance authority, and reduce `ProceduralLayoutValidator` to a pre-filter. Target: every accepted P4A layout is representable and passes `P4Connectivity` (currently 7/24). Replace `ProceduralLayoutAdapter`'s declared losses with real mappings.
> 2. **Plan step 7: runtime catalog adapter.** Build `TechCatalog` from `DefDatabase<ThingDef>` and `ResearchProjectDef`, including DLC and mod defs, reusing the extraction semantics of `tools/extract-core-catalog.py`: ParentName inheritance, building plus recipeMaker research, hidden prerequisites, the producer gate for items, `RawMinimumTiers`, and the vanilla market-value port.
>    - Resolve and **persist** the faction stage (`TechStageModel.TryResolve` with `LayoutSeed.DeriveFaction`) and the settlement layout seed (`LayoutSeed.Derive`).
>    - Capture `LayoutLocalResources`: wood yield, stone stuffs, river cells, geyser cells.
>    - Add a pure-versus-runtime parity test: the Core runtime catalog must equal the fixture for Core defs.
> 3. **Plan step 8: runtime spawning and equipping.** Replace the P2 fixed compound with the P4A planner behind a guarded path, and keep an explicit, logged fixed-compound fallback that reports its selection frequency.
>    - Spawn per-room fixtures with their stuff and rotation (bench interaction offset (0,0,-1)), stockpile zones, yards, fuel and medicine stocks, and components.
>    - Strip resident gear and re-equip from `SettlementLoadout`.
>    - Adapt P3 posts, rally point, mortar court and magazine to the procedural geometry.
>    - Fix the P2 profile defects through the catalog: the tribal `Bed`/`FueledSmithy`, and industrial flak without FlakArmor.
> 4. **Integration checklist** (`docs/p4a-foundations.md` §5):
>    - field placement from the exterior field requirement;
>    - power conduits between the generator, coolers, heaters, lamps, stoves and turrets;
>    - replace `MinimumBuildableCells = 31*25`;
>    - feed the P4 archetype output into the usable mask.
> 5. **Live matrix**:
>    - Core tribe, wood-rich and wood-poor (Stonecutting adaptation)
>    - Outlander at IndustrialEarly, Mid and Late
>    - Pirate at Spacer
>    - cold and hot climates
>    - wall layers 1–3
>    - rock-protected and open doctrines, including a geothermal site
>    - pathing and job audits from gates to every room and fixture
>    - P3 defense regression
>    - save/reload and revisit with no reroll
>    - forced rollback, including retained-roof excavation
>    - one Geological Landforms map
>
>    Then calibrate the provisional constants listed in the plan and in `docs/p4a-foundations.md` §10.
>
> **Known follow-ups, not blockers:**
> - Wind turbines face north only, and their clear zone rarely fits.
> - Rooms don't share walls; there are no corridor loops.
> - The 3-cell margin around every obstacle is conservative.
> - Watermills wait for a river perimeter.
> - The area pre-check is a necessary-condition bound only.
>
> **Out of scope:** trade-stock composition (P5), long-term sustainability, and modded tool items. Don't start P5.
>
> **Decisions:** raise real design choices with the user or lead, each with options and a recommendation. Don't silently change agreed decisions (the tables in §6 and §10 and in the plan).
