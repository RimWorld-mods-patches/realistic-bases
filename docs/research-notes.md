# Research notes and evidence

Reviewed 2026-09-14. These notes distinguish observed implementation from design inference. No mods were installed or run for this assessment; no benchmark or gameplay superiority claim has been established.

## Reference revisions

The public repositories were inspected from temporary checkouts, outside the project. Links below pin the reviewed revisions so later changes do not silently change the evidence.

| Project | Revision |
|---|---|
| Vanilla Base Generation Expanded | [`400def9c829ae375e150b8c79f13af649913e0b6`](https://github.com/Vanilla-Expanded/VanillaBaseGenerationExpanded/tree/400def9c829ae375e150b8c79f13af649913e0b6) |
| Large Faction Bases | [`3d4ad947d6b694aee9cb92471b5e581f177f670e`](https://github.com/emipa606/LargeFactionBases/tree/3d4ad947d6b694aee9cb92471b5e581f177f670e) |
| Geological Landforms | [`e8035e2b2fdb46ceb92aa159e17e72fdc5dcc421`](https://github.com/m00nl1ght-dev/GeologicalLandforms/tree/e8035e2b2fdb46ceb92aa159e17e72fdc5dcc421) |

Local primary evidence is the installed game's Core XML and a local decompilation of `Assembly-CSharp` 1.6.4871 (`vanilla-decompiled/Assembly-CSharp-1.6.4871`, outside this repository). The installed Version.txt (`Version.txt` in the game folder) reads `1.6.4871 rev595`, matching the decompilation's documented version. This establishes the assessment baseline, not the latest published RimWorld release. Game paths are relative to the RimWorld installation folder and decompiled paths to that decompilation; names and methods identify the evidence on another installation.

## E1 — Settlement generation has a usable integration boundary

The local Settlement.MapGeneratorDef getter (decompiled `RimWorld.Planet.Settlement.cs:80`) returns a configured override when present, otherwise the player or faction generator. Core's BaseFactionMapGenerator.xml (`Data/Core/Defs/MapGeneration/BaseFactionMapGenerator.xml:15` in the game folder) places `Settlement` at order 400 and `SettlementPower` at 401; CommonMapGenerator.xml (`Data/Core/Defs/MapGeneration/CommonMapGenerator.xml:67` in the game folder) defines rocks at 200, terrain at 210, and post-terrain mutators at 220.

The inspected GenStep_Settlement.CanScatterAt (decompiled `RimWorld.GenStep_Settlement.cs:32`) rejects roofed centers:

```csharp
if (c.Roofed(map))
{
    return false;
}
```

**Inference:** use a dedicated settlement planner after terrain generation; merely enlarging vanilla's rectangle does not provide deliberate mountain excavation. Selecting a separate generator is promising, but exact hook guards, other mods' patches, arrival variables, later generation effects, and exception recovery need the P0 experiment.

## E2 — Vanilla defense explicitly becomes an assault

In LordJob_DefendBase.CreateGraph (decompiled `RimWorld.LordJob_DefendBase.cs:55`), `transition3` leads from defense to `LordToil_AssaultColony`; its triggers include:

```csharp
transition3.AddTrigger(new Trigger_TicksPassed(delayBeforeAssault));
transition3.AddTrigger(new Trigger_UrgentlyHungry());
```

The same graph also has harm/loss and other triggers. LordToil_DefendBase.UpdateAllDuties (decompiled `RimWorld.LordToil_DefendBase.cs:19`) assigns a common base-center duty. Core's DefendBase duty (`Data/Core/Defs/DutyDefs/Duties_NonPlayerHome.xml:5` in the game folder) combines point defense, needs, temperature safety, and wandering.

**Inference:** custom registered-garrison state/duties are warranted. A larger base-center radius or deleting one assault trigger would not implement sector assignment, pursuit limits, relief, and fallback. Their tactical effectiveness remains untested.

## E3 — What the two base mods actually expose

VBGE's [metadata](https://github.com/Vanilla-Expanded/VanillaBaseGenerationExpanded/blob/400def9c829ae375e150b8c79f13af649913e0b6/About/About.xml) declares Vanilla Expanded Framework as a dependency. Its [faction patch](https://github.com/Vanilla-Expanded/VanillaBaseGenerationExpanded/blob/400def9c829ae375e150b8c79f13af649913e0b6/1.6/Patches/Settlements.xml) supplies `KCSG.CustomGenOption` and settlement choices. [Outlander definitions](https://github.com/Vanilla-Expanded/VanillaBaseGenerationExpanded/blob/400def9c829ae375e150b8c79f13af649913e0b6/1.6/Defs/SettlementDefs/Outlanders.xml) configure tagged structure counts and defense/stockpile options. KCSG runtime internals were not audited, so absence of a feature in these XML files is not proof of its absence throughout the framework.

LFB's [1.6 patch](https://github.com/emipa606/LargeFactionBases/blob/3d4ad947d6b694aee9cb92471b5e581f177f670e/1.6/Patches/RimWar_Large.xml) replaces the `Settlement` gen step. Its [generator](https://github.com/emipa606/LargeFactionBases/blob/3d4ad947d6b694aee9cb92471b5e581f177f670e/Source/LargeFactionBase/LargeFactionBase/GenStep_LargeFactionBase.cs) includes `edgeIED`/`edgeSpikes` paths; [power source](https://github.com/emipa606/LargeFactionBases/blob/3d4ad947d6b694aee9cb92471b5e581f177f670e/Source/LargeFactionBase/LargeFactionBase/GenStep_LargePower.cs) contains network connection and generator/battery placement logic. Existence of that class is not proof that every 1.6 generation path invokes it. Both [DefendBase2](https://github.com/emipa606/LargeFactionBases/blob/3d4ad947d6b694aee9cb92471b5e581f177f670e/Source/LargeFactionBase/RimWorld/LordJob_DefendBase2.cs) and [DefendBase3](https://github.com/emipa606/LargeFactionBases/blob/3d4ad947d6b694aee9cb92471b5e581f177f670e/Source/LargeFactionBase/RimWorld/LordJob_DefendBase3.cs) retain assault transitions.

**Inference:** use these as reference architectures, not a drop-in foundation for the requested trap-free behavior. Running two settlement replacements together requires explicit arbitration. No third-party source or assets were copied into a mod implementation here.

## E4 — NPC production is a distinct engineering problem

JobGiver_Work.GetPriority (decompiled `RimWorld.JobGiver_Work.cs:21`) requires work settings; PawnCanUseWorkGiver (decompiled `RimWorld.JobGiver_Work.cs:268`) checks `nonColonistsCanDo` alongside pawn status and work capabilities. WorkGiver_Grower.PotentialWorkCellsGlobal (decompiled `RimWorld.WorkGiver_Grower.cs:20`) begins its building scan with:

```csharp
List<Building> bList = pawn.Map.listerBuildings.allBuildingsColonist;
```

This is a concrete obstacle to blindly reusing the normal grower-building scan for an enemy hydroponics room, not proof that no vanilla work code can serve NPCs. RefuelWorkGiverUtility (decompiled `RimWorld.RefuelWorkGiverUtility.cs:30`) checks matching building/pawn faction, for example.

**Inference:** provide scoped job-selection adapters, reuse validated drivers, and demonstrate actual completed work. Audit further ownership, zone, storage, bill, home-area and needs assumptions per supported job in P0/P5. Do not globally grant every enemy colonist work behavior.

## E5 — Mortars need real supply and specialized loading logic

JobDriver_ManTurret.FindAmmoForTurret (decompiled `RimWorld.JobDriver_ManTurret.cs:50`) searches nearby reachable, reservable shells. Its player shell-filter branch does not apply to ordinary NPCs, and the NPC validator excludes projectiles whose damage does not harm health. Its fuel branch handles mortar barrels and the classic-mortars setting. The loading toils consume the carried shell.

JobGiver_ManTurrets (decompiled `RimWorld.JobGiver_ManTurrets.cs:19`) checks ammunition and fuel availability before assigning a turret. Building_TurretGun.TryStartShootSomething (decompiled `RimWorld.Building_TurretGun.cs:355`) contains the overhead-projectile/roof check. Current mortar components are in Buildings_Security_Turrets.xml (`Data/Core/Defs/ThingDefs_Buildings/Buildings_Security_Turrets.xml:544` in the game folder).

**Inference:** allocate operators, unroofed courts, nearby accessible magazines and barrels. Smoke/EMP tactics require explicit loader/target support. Check each available shell's active damage/recipe definitions; a stockpile alone cannot prove it will be fired appropriately.

## E6 — Stored power is energy, not generating capacity

CompPower (decompiled `RimWorld.CompPower.cs:20`) exposes the conversion from watts to watt-days per tick. CompPowerBattery (decompiled `RimWorld.CompPowerBattery.cs:68`) drains self-discharge energy and applies charging efficiency in `AddEnergy`. PowerNet (decompiled `RimWorld.PowerNet.cs:181`) computes energy gain and stored energy across connected components.

**Inference:** size generation and storage using time-varying demand and separate networks. Initial battery charge can establish an existing settlement's starting state but cannot stand in for replenishment. Validate temperatures and power behavior by ticking the game, including recharge after night/combat demand.

## E7 — Unloaded settlement maps require a persistence policy

Settlement.ShouldRemoveMapNow (decompiled `RimWorld.Planet.Settlement.cs:212`) permits removal after the listed map-removal blockers cease. `Notify_MyMapRemoved` also handles previously generated inhabitants.

**Inference:** saving a map component is not, on its own, a design for repeated visits after map removal. The mod needs lifecycle-aware resource/damage/roster reconciliation or an explicitly narrower encounter-only scope. The plan treats no-duplication revisit behavior as a full-release requirement; the exact representation is pending P0.

## E8 — Geological Landforms compatibility is feasible but unproven

The reviewed [1.6 landform worker](https://github.com/m00nl1ght-dev/GeologicalLandforms/blob/e8035e2b2fdb46ceb92aa159e17e72fdc5dcc421/Sources/GeologicalLandforms/World/TileMutatorWorker_Landform.cs) modifies elevation, fertility, caves, terrain, biome-related data and water flow through generation hooks. Its [MapGenerator patch](https://github.com/m00nl1ght-dev/GeologicalLandforms/blob/e8035e2b2fdb46ceb92aa159e17e72fdc5dcc421/Sources/GeologicalLandforms/Patches/Patch_Verse_MapGenerator.cs) prepares/cleans generation context and adds steps. Its [scatterer patch](https://github.com/m00nl1ght-dev/GeologicalLandforms/blob/e8035e2b2fdb46ceb92aa159e17e72fdc5dcc421/Sources/GeologicalLandforms/Patches/Patch_RimWorld_GenStep_Scatterer.cs) temporarily changes scatterer constraints.

**Inference:** read actual terrain and retain its generation chain; do not assume a conventional central square or fixed constraints. Inspect the finalized map after custom steps as well. Lake and mountain support depends on viable geometry, not just detecting the mod package or a landform name.

## What remains unverified

No compilation, map generation, NPC job loop, defensive encounter, thermal/power simulation, save/load, unload/revisit, performance benchmark, or reference-mod gameplay comparison was performed. Build estimates are engineering judgments. Feasibility confidence reflects inspected extension points and known constraints; P0 is designed to turn the highest-risk assumptions into observed behavior.
