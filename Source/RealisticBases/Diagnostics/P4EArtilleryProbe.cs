using System;
using System.Linq;
using RealisticBases.Persistence;
using RealisticBases.Integration;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealisticBases.Diagnostics
{
    // Explicit disposable validation, never a permanent extra crew or supply.
    public sealed class P4EArtilleryProbe : MapComponent
    {
        private Thing gun;
        private int deadline;
        public P4EArtilleryProbe(Map map) : base(map) { }

        public void Start()
        {
            if (!P4DQuestDebugActions.Disposable || !(map.Parent is Settlement) || !map.GetComponent<P0MapState>().Active)
                throw new InvalidOperationException("Requires disposable procedural settlement");
            if (deadline != 0) throw new InvalidOperationException("Probe already active");
            Thing[] guns = map.listerThings.AllThings.Where(t => t.def == ThingDefOf.Turret_Mortar).ToArray();
            if (guns.Length != 1 || Find.Storyteller.difficulty.classicMortars) throw new InvalidOperationException("Requires one non-classic mortar");
            Thing[] initial = map.listerThings.AllThings.Where(t => t.Position.GetEdifice(map) is Building_Storage &&
                t.GetRoom()?.CellCount == 1).ToArray();
            if (initial.Where(t => t.def.defName == "Shell_HighExplosive").Sum(t => t.stackCount) != 12 ||
                initial.Where(t => t.def.defName == "ReinforcedBarrel").Sum(t => t.stackCount) != 2)
                throw new InvalidOperationException("Loading probe requires fresh, unconsumed finite stocks");
            Pawn anchor = map.mapPawns.FreeColonistsSpawned.FirstOrDefault() ?? throw new InvalidOperationException("No disposable visitor");
            gun = guns[0];
            // Keep the visitor still, beyond direct-fire range, so the map
            // survives while its real funded NPC crew performs native jobs.
            IntVec3 edge = map.AllCells.Where(c => c.OnEdge(map) && c.Standable(map))
                .OrderByDescending(c => c.DistanceToSquared(gun.Position)).First();
            anchor.jobs.StopAll(); anchor.DeSpawn(); GenSpawn.Spawn(anchor, edge, map);
            anchor.drafter.Drafted = true;
            CompRefuelable barrel = gun.TryGetComp<CompRefuelable>();
            float previous = barrel.Fuel;
            if (previous <= 0) throw new InvalidOperationException("Requires a loaded initial barrel");
            barrel.ConsumeFuel(previous); // Consumes one embedded barrel; adds nothing.
            deadline = Find.TickManager.TicksGame + 5000;
            P2Log.Message("P4E_LOGISTICS_PROBE_STARTED", "map=" + map.uniqueID + " gun=" + gun.thingIDNumber +
                " embeddedBarrelConsumed=" + previous + " visitor=" + anchor.thingIDNumber + "@" + edge + " deadline=" + deadline);
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref gun, "p4eLoadingProbeGun");
            Scribe_Values.Look(ref deadline, "p4eLoadingProbeDeadline");
        }

        public override void MapComponentTick()
        {
            if (deadline == 0 || Find.TickManager.TicksGame % 15 != 0) return;
            CompChangeableProjectile shell = (gun as Building_TurretGun)?.gun?.TryGetComp<CompChangeableProjectile>();
            Pawn op = gun?.TryGetComp<CompMannable>()?.ManningPawn;
            Thing[] stocks = map.listerThings.AllThings.Where(t => t.Position.GetEdifice(map) is Building_Storage &&
                t.GetRoom()?.CellCount == 1).ToArray();
            int he = stocks.Where(t => t.def.defName == "Shell_HighExplosive").Sum(t => t.stackCount);
            int barrels = stocks.Where(t => t.def.defName == "ReinforcedBarrel").Sum(t => t.stackCount);
            bool passed = op != null && shell?.LoadedShell?.defName == "Shell_HighExplosive" && shell.loadedCount == 1 &&
                gun.TryGetComp<CompRefuelable>().Fuel > 0 && he == 11 && barrels == 1;
            if (!passed && Find.TickManager.TicksGame < deadline) return;
            P2Log.Message("P4E_LOGISTICS_PROBE_COMPLETE", "map=" + map.uniqueID + " passed=" + passed +
                " operator=" + (op?.thingIDNumber.ToString() ?? "none") + " looseHE=" + he + " loaded=" + (shell?.loadedCount ?? 0) +
                " spareBarrels=" + barrels + " barrelFuel=" + gun?.TryGetComp<CompRefuelable>()?.Fuel);
            deadline = 0; gun = null;
            Find.TickManager.Pause();
        }
    }
}
