# P4A hand-off prompts

2026-09-23 scope update: prompts A–C below are archived handoff context. The
current assignment authorizes P4A integration alongside P4 terrain completion.
The live generator uses `ProceduralLayout` directly and validates it with
`ProceduralLayoutValidator`; the fixed-plan adapter is diagnostic. Instructions
below to wait for P4 closeout, avoid P4A files, or continue the fixed compound
have been superseded.

2026-09-23. There are two paste-ready prompts: **A** for the P4 owner, and **B** for the agent that picks up P4A after P4 closes. Both refer to commit `f844e43` on branch `claude/p4a-foundations` (worktree `/Users/roman_gr/Repositories/rimworld/realistic-bases-p4a`), which is based on P4 commit `a41477e`.

---

## Prompt A: what P4A needs from P4 (for the P4 owner)

> You own P4 (terrain variety and stronger fortifications) in Realistic Bases. A separate workstream has built a procedural settlement planner (P4A) on branch `claude/p4a-foundations` (commit `f844e43`, based on your `a41477e`). It is pure code and is not wired into generation. Integration waits for P4 to finish.
>
> **What P4A does NOT need from you.** P4A has its own planner, final-occupancy validator (`ProceduralLayoutValidator`) and layout model (`ProceduralLayout`). Procedural layouts will be validated and spawned from that model directly. So there is no need to:
> - generalize `PlannedNavigationGrid` / `P4Connectivity`;
> - relax `PhysicalSettlementValidator`;
> - extend `PhysicalSettlementPlan`, `PhysicalFacility` or `PhysicalPlacement`.
>
> Your fixed-compound path can keep them as they are.
>
> **What P4A does need.** Please build these into P4 where they naturally belong, or say which you would leave to P4A integration. Don't change P4A files.
>
> 1. **Retained thick roof in the terrain mutation ledger and recovery.** P4A's rock-protected doctrine digs rooms and streets under a mountain and **keeps** the natural `RoofRockThick` over them, which is what protects against mortars. It proves support with vanilla's 6.9-cell roof-holder rule, counting only intact natural rock as a holder, never built walls. Today `TerrainMutationFootprint` / `TerrainMutationPlanner.Validate` require roof clearance for every roofed excavation (`roofed_excavation_without_roof_ledger`). Needed:
>    - a retained-roof category in the ledger;
>    - spawner recovery that records and restores excavated rock around kept roofs;
>    - a post-excavation collapse check in the audit;
>    - rollback that restores rock under a kept roof.
> 2. **Transactional spawn and recovery that doesn't require `PhysicalSettlementPlan`.** P4A's spawner needs the same machinery `P2SettlementSpawner` has: mutation-footprint capture, owned-thing tracking, rock and roof restoration, checksums, and a safe pre-mutation rejection. It should be callable with a footprint plus a list of owned things, not with the P2 plan type. A small reusable service or helper is enough.
> 3. **A P3 encounter input that doesn't depend on the fixed compound.** `EncounterPlanner.Plan` takes a `PhysicalSettlementPlan` and still starts from compound offsets. P4A needs an entry point that takes explicit geometry:
>    - enclosure cells, perimeter walls and gates (on any side, with their inside endpoints);
>    - public circulation cells;
>    - an optional mortar court. P4A's rock-protect doctrine may drop it deliberately, and the mortar may then be absent.
>
>    Posts, rally and fallback points, and the magazine should come from that input.
> 4. **Archetype output P4A can consume per cell.** P4A classifies cells as open, covered, diggable rock, or unusable. Please expose the P4 archetype result so that P4A can use it:
>    - foothill and excavated-mountain regions where excavation is allowed;
>    - preserved lakeshore water;
>    - verified island and causeway approaches.
>
>    Also say whether P4 plans a perimeter that can meet river water; P4A defers watermills until then.
> 5. **Map inputs and persistence.** P4A needs these from the map and world, captured by whichever adapter you consider right:
>    - harvestable wood yield near the site;
>    - local stone block defs;
>    - river (moving-fluid) cells;
>    - steam geyser positions;
>    - the faction id and `FactionDef.techLevel`.
>
>    P4A also needs somewhere to persist the settlement's layout seed and the faction's resolved tech stage, so revisits never reroll.
>
> **For your own path (FYI, not needed by P4A):**
> - `P4Connectivity.cs:34` uses `Has(TerrainTraits.Water | TerrainTraits.Occupied)`, which requires **both** flags. Water-only cells, and natural rock cells without an edifice, are not blocked. The fixed compound hides this today.
> - If live pathing tests teach you a new connectivity rule, please record it, so P4A's validator can adopt it too.
>
> Please reply with, for each numbered item: delivered in P4 (with commit), planned for P4A integration, or rejected (with reasons).

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
> 3. The P4 findings and connectivity docs as they stand when P4 closes, plus the P4 owner's answer to Prompt A (retained roof, reusable spawn/recovery, geometry-based P3 input, archetype cells, map inputs and persistence).
> 4. The code in `Source/RealisticBases/Planning/`:
>    - `ProceduralLayoutModel.cs`, `ProceduralLayoutPlanner.cs`, `ProceduralLayoutValidator.cs` (planner, validator, diagnostic P4 adapter, ASCII export)
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
> 1. **Keep P4A's own contract as the authority.** `ProceduralLayoutValidator` accepts procedural layouts, and `ProceduralLayout` is the spawn model. Don't route procedural layouts through `PhysicalSettlementPlan`, `PhysicalSettlementValidator` or `P4Connectivity`. `ProceduralLayoutAdapter` and its cross-check test stay as a diagnostic only. Adopt what P4 delivered for Prompt A items 1–5, and implement whatever P4 left to P4A integration.
> 2. **Plan step 7: runtime catalog adapter.** Build `TechCatalog` from `DefDatabase<ThingDef>` and `ResearchProjectDef`, including DLC and mod defs, reusing the extraction semantics of `tools/extract-core-catalog.py`: ParentName inheritance, building plus recipeMaker research, hidden prerequisites, the producer gate for items, `RawMinimumTiers`, and the vanilla market-value port.
>    - Resolve and **persist** the faction stage (`TechStageModel.TryResolve` with `LayoutSeed.DeriveFaction`) and the settlement layout seed (`LayoutSeed.Derive`).
>    - Capture `LayoutLocalResources`: wood yield, stone stuffs, river cells, geyser cells.
>    - Add a pure-versus-runtime parity test: the Core runtime catalog must equal the fixture for Core defs.
> 3. **Plan step 8: runtime spawning and equipping.** Add a guarded generation path that plans with P4A and spawns directly from `ProceduralLayout`, using P4's reusable transactional spawn and recovery (Prompt A item 2). A P4A rejection must leave the map untouched and playable, through the existing pre-mutation rejection path.
>    - Spawn per-room fixtures with their stuff and rotation (bench interaction offset (0,0,-1)), stockpile zones, power yards, fuel, medicine and component stocks, and retained-roof excavation (Prompt A item 1).
>    - Strip resident gear and re-equip from `SettlementLoadout`.
>    - Feed P3 through its geometry-based input (Prompt A item 3).
>    - Procedural settlements use `LayoutRequirements` and the tech catalog, not P2 profiles.
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

---

## Prompt C: finish P4, including picking up the P4A work (for the P4 owner)

> You own **P4 — Terrain variety and stronger fortifications** for the RimWorld 1.6 mod Realistic Bases. Finish P4 to its exit gate. Along the way, bring the completed P4A foundation work into your line of history, and deliver the few contracts P4A needs from P4.
>
> ### 1. Pick up the P4A work from git (do this first)
>
> The P4A work is on branch **`claude/p4a-foundations`** in the same repository. Every worktree shares one `.git`, so the branch is already visible: no fetch or push is needed. It is built on your commit `a41477e`.
>
> - **Additions only.** It does not modify any file that existed at `a41477e`. It adds:
>   - `Source/RealisticBases/Planning/`: `ProceduralLayoutModel.cs`, `ProceduralLayoutPlanner.cs`, `ProceduralLayoutValidator.cs`, `SettlementTechStage.cs`, `TechCatalog.cs`, `SettlementRoles.cs`, `LayoutEquipment.cs`, `LayoutPower.cs`, `LayoutLoadout.cs`.
>   - `Source/RealisticBases.LayoutTests/`: a separate pure test project with a generated Core catalog fixture.
>   - `tools/extract-core-catalog.py`.
>   - Docs: `docs/p4a-foundations.md`, `docs/p4a-handoff-prompts.md`, `docs/plans/2026-09-23-tech-stage-equipment.md`, and `docs/p4a-examples/`.
> - **Nothing references the new code yet.** The generator, spawner, audits and AI are untouched. The planning files still compile into the mod DLL (default compile items), and they are net472-safe.
>
> Steps:
> 1. In your worktree, run `git status`. Commit or stash your own work first; never merge onto a dirty tree.
> 2. Integrate:
>    - If your HEAD is still `a41477e` (detached): `git checkout --detach claude/p4a-foundations`. This is a fast-forward.
>    - If you have commits after `a41477e`: `git merge --no-ff claude/p4a-foundations -m "Merge P4A foundation and tech-stage planner"`. Conflicts are only possible if you created files with the same paths.
>    - If you work on a branch, merge it into that branch instead.
> 3. Verify, using your usual NuGet volume, and don't run it concurrently with another build on the same volume:
>    ```
>    docker run --rm -v "$PWD":/mod -v <nuget-volume>:/root/.nuget/packages -w /mod/Source/RealisticBases mcr.microsoft.com/dotnet/sdk:8.0 dotnet build -c Release
>    docker run --rm -v "$PWD":/mod -v <nuget-volume>:/root/.nuget/packages -w /mod/Source/RealisticBases.PlannerTests mcr.microsoft.com/dotnet/sdk:8.0 dotnet run -c Release
>    docker run --rm -v "$PWD":/mod -v <nuget-volume>:/root/.nuget/packages -w /mod/Source/RealisticBases.LayoutTests mcr.microsoft.com/dotnet/sdk:8.0 dotnet run -c Release
>    ```
>    Expected: 0 warnings/0 errors; your PlannerTests unchanged (52/52 at `a41477e`); LayoutTests 62/62.
>    Rebuilding refreshes the DLL your RimWorld Mods symlink loads. Restart the game after building, as usual.
> 4. Read `docs/p4a-foundations.md` (§6 decisions, §10 tech-stage equipment) and Prompt A in `docs/p4a-handoff-prompts.md` for context. Don't edit the P4A files. If a P4 change breaks a P4A test, report it rather than editing P4A code.
>
> ### 2. Finish P4
>
> Complete the remaining P4 checklist in `docs/p4-findings.md` and `docs/progress.md`, under your existing evidence policy: pure tests, the Docker build, and live Core and Geological Landforms observations kept separate. Commit each validated step. That covers:
> - the P4.2 foothill case;
> - the P4.3 lakeshore, peninsula and island live cases, including rejection;
> - P4.4: layered fortifications, palettes and budgets, climate alternatives, the P3/save-reload regression, the Core and GL matrix, visual review, and the exit decision.
>
> The fixed compound remains your P4 vehicle. P4 does **not** need to adopt the P4A planner.
>
> ### 3. Deliver what P4A needs from P4
>
> P4A validates and spawns procedural layouts from its own model (`ProceduralLayoutValidator`, `ProceduralLayout`). Don't generalize `P4Connectivity`, and don't relax `PhysicalSettlementValidator` or extend the P2 plan types for P4A. Do build these, where they fit P4 naturally:
>
> 1. **Kept thick roof in the terrain mutation ledger and recovery.** Add a category for excavated cells that keep their natural `RoofRockThick`. Today `TerrainMutationPlanner.Validate` requires roof clearance for every roofed excavation. Recovery must record and restore rock around kept roofs; the audit must check for roof collapse after excavation; and rollback must restore rock under a kept roof. Support rule: vanilla's 6.9-cell roof holder, counting only intact natural rock, never built walls.
> 2. **Reusable transactional spawn and recovery** callable with a mutation footprint plus a list of owned things, not with `PhysicalSettlementPlan`. It covers capture, owned-thing tracking, rock and roof restoration, checksums, and a safe pre-mutation rejection.
> 3. **A geometry-based P3 encounter input.** It takes enclosure cells, perimeter walls, gates on any side with their inside endpoints, public circulation cells, and an optional mortar court (P4A's rock-protect doctrine can drop the mortar). Posts, rally and fallback points, and the magazine are derived from it, not from compound offsets.
> 4. **Per-cell archetype output P4A can consume:** foothill and mountain regions where excavation is allowed, preserved lakeshore water, and verified island or causeway approaches. Also say whether a perimeter meeting river water is planned; P4A defers watermills until then.
> 5. **Map inputs and persistence:** wood yield near the site, local stone block defs, river cells, steam geyser positions, faction id and `FactionDef.techLevel`, and persistence of the settlement's P4A layout seed and the faction's tech stage.
>
> Each item gets its own tests, and live evidence where it touches the map. If an item doesn't belong in P4, say so, and record it as a P4A integration task instead.
>
> FYI, for your own path: `P4Connectivity.cs:34` uses `Has(Water | Occupied)`, which requires both flags, so water-only and natural rock cells without an edifice aren't blocked.
>
> ### 4. Close-out
>
> - Record the P4 exit decision in your docs, as usual.
> - In `docs/progress.md`, note that the P4A foundation and tech-stage planner (plan steps 1–6, pure) are merged, and that P4A integration (plan steps 7–8 and `docs/p4a-foundations.md` §5/§9) can start.
> - Answer items 1–5 with: delivered (commit), left to P4A integration, or rejected (with reasons). Append the answer to `docs/p4a-handoff-prompts.md` under "P4 owner's answer".
> - Don't start P4A integration or P5.
>
> ### Constraints
>
> - No traps or IEDs.
> - Use isolated disposable RimWorld profiles, and never touch normal user saves.
> - The carryover `CLAUDE.md` describes a different project.
> - Commit each validated step separately.
> - Don't rewrite or rebase the P4A commits; merge them.
