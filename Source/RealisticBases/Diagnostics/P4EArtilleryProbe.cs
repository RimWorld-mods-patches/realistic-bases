using System;
using System.Collections.Generic;
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
        private List<Thing> artillery = new List<Thing>();
        private int deadline;
        private int initialHe = 12;
        private Thing hazardShell;
        private int hazardObserved;
        private readonly List<Tuple<Thing, int>> hazardBuildings = new List<Tuple<Thing, int>>();
        public P4EArtilleryProbe(Map map) : base(map) { }

        public void StartHazard(Thing shell)
        {
            if (!P4DQuestDebugActions.Disposable || deadline != 0 || hazardShell != null)
                throw new InvalidOperationException("Requires an idle disposable probe");
            hazardShell = shell;
            hazardObserved = 0;
            float radius = shell.TryGetComp<CompExplosive>().ExplosiveRadius();
            hazardBuildings.Clear();
            hazardBuildings.AddRange(map.listerThings.AllThings.OfType<Building>()
                .Where(t => t.Position.InHorDistOf(shell.Position, radius + 2))
                .Select(t => Tuple.Create((Thing)t, t.HitPoints)));
            shell.TryGetComp<CompExplosive>().StartWick();
        }

        public void Start()
        {
            if (!P4DQuestDebugActions.Disposable || !(map.Parent is Settlement) || !map.GetComponent<P0MapState>().Active)
                throw new InvalidOperationException("Requires disposable procedural settlement");
            if (deadline != 0) throw new InvalidOperationException("Probe already active");
            Thing[] guns = map.listerThings.AllThings.Where(t => t.def == ThingDefOf.Turret_Mortar).ToArray();
            if (guns.Length < 1 || guns.Length > 3 || Find.Storyteller.difficulty.classicMortars) throw new InvalidOperationException("Requires one to three non-classic mortars");
            Thing[] initial = map.listerThings.AllThings.Where(t => t.Position.GetEdifice(map) is Building_Storage &&
                t.GetRoom()?.CellCount == 1).ToArray();
            int heStock = initial.Where(t => t.def.defName == "Shell_HighExplosive").Sum(t => t.stackCount);
            if ((heStock != 12 && heStock != 12 * guns.Length) ||
                map.GetComponent<RealisticBases.AI.P3GarrisonController>().DebugOperatingStock("ReinforcedBarrel") != 2)
                throw new InvalidOperationException("Loading probe requires fresh, unconsumed finite stocks");
            initialHe = heStock;
            Pawn anchor = map.mapPawns.FreeColonistsSpawned.FirstOrDefault() ?? throw new InvalidOperationException("No disposable visitor");
            gun = guns[0];
            artillery = guns.OrderBy(t => t.thingIDNumber).ToList();
            // Keep the visitor still, beyond direct-fire range, so the map
            // survives while its real funded NPC crew performs native jobs.
            IntVec3 edge = map.AllCells.Where(c => c.OnEdge(map) && c.Standable(map))
                .OrderByDescending(c => guns.Min(t => c.DistanceToSquared(t.Position))).First();
            anchor.jobs.StopAll(); anchor.DeSpawn(); GenSpawn.Spawn(anchor, edge, map);
            anchor.drafter.Drafted = true;
            CompRefuelable barrel = gun.TryGetComp<CompRefuelable>();
            float previous = barrel.Fuel;
            if (previous <= 0) throw new InvalidOperationException("Requires a loaded initial barrel");
            barrel.ConsumeFuel(previous); // Consumes one embedded barrel; adds nothing.
            deadline = Find.TickManager.TicksGame + 10000;
            P2Log.Message("P4E_LOGISTICS_PROBE_STARTED", "map=" + map.uniqueID + " gun=" + gun.thingIDNumber +
                " guns=" + guns.Length + " initialHe=" + initialHe + " embeddedBarrelConsumed=" + previous + " visitor=" + anchor.thingIDNumber + "@" + edge + " deadline=" + deadline);
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref gun, "p4eLoadingProbeGun");
            Scribe_Collections.Look(ref artillery, "p4eLoadingProbeGuns", LookMode.Reference);
            Scribe_Values.Look(ref deadline, "p4eLoadingProbeDeadline");
            Scribe_Values.Look(ref initialHe, "p5LoadingProbeInitialHe", 12);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && artillery == null)
                artillery = gun == null ? new List<Thing>() : new List<Thing> { gun };
        }

        public override void MapComponentTick()
        {
            if (hazardShell != null)
            {
                if (hazardShell.Destroyed && hazardObserved == 0) hazardObserved = Find.TickManager.TicksGame;
                if (hazardObserved != 0 && Find.TickManager.TicksGame >= hazardObserved + 120)
                {
                    P2Log.Message("P4E_MAGAZINE_HAZARD_COMPLETE", "map=" + map.uniqueID + " shellGone=" + hazardShell.Destroyed +
                        " observedTicks=120 damagedBuildings=" + hazardBuildings.Count(p => p.Item1.Destroyed || p.Item1.HitPoints < p.Item2) +
                        " destroyedBuildings=" + hazardBuildings.Count(p => p.Item1.Destroyed) +
                        " roofedEnclosureIsBlastproof=False");
                    hazardShell = null; hazardBuildings.Clear(); Find.TickManager.Pause();
                }
                return;
            }
            if (deadline == 0 || Find.TickManager.TicksGame % 15 != 0) return;
            CompChangeableProjectile shell = (gun as Building_TurretGun)?.gun?.TryGetComp<CompChangeableProjectile>();
            Pawn op = gun?.TryGetComp<CompMannable>()?.ManningPawn;
            Thing[] stocks = map.listerThings.AllThings.Where(t => t.Position.GetEdifice(map) is Building_Storage &&
                t.GetRoom()?.CellCount == 1).ToArray();
            int he = stocks.Where(t => t.def.defName == "Shell_HighExplosive").Sum(t => t.stackCount);
            int barrels = map.GetComponent<RealisticBases.AI.P3GarrisonController>().DebugOperatingStock("ReinforcedBarrel");
            var operators = artillery.Select(t => t?.TryGetComp<CompMannable>()?.ManningPawn).ToArray();
            bool passed = artillery.Count > 0 && operators.All(p => p != null) && operators.Distinct().Count() == artillery.Count &&
                artillery.All(t => t != null && t.Spawned && t.TryGetComp<CompRefuelable>().Fuel > 0 &&
                    (t as Building_TurretGun)?.gun?.TryGetComp<CompChangeableProjectile>() is CompChangeableProjectile p &&
                    p.LoadedShell?.defName == "Shell_HighExplosive" && p.loadedCount == 1) && he <= initialHe - artillery.Count && barrels == 1;
            if (!passed && Find.TickManager.TicksGame < deadline) return;
            P2Log.Message("P4E_LOGISTICS_PROBE_COMPLETE", "map=" + map.uniqueID + " passed=" + passed +
                " guns=" + artillery.Count + " operators=" + string.Join(",", operators.Select(p => p?.thingIDNumber.ToString() ?? "none")) +
                " operator=" + (op?.thingIDNumber.ToString() ?? "none") + " looseHE=" + he +
                " loadedTotal=" + artillery.OfType<Building_TurretGun>().Sum(t => t.gun?.TryGetComp<CompChangeableProjectile>()?.loadedCount ?? 0) +
                " initialHe=" + initialHe + " firedBeforeAllReady=" + (initialHe - he - artillery.Count) + " loaded=" + (shell?.loadedCount ?? 0) +
                " spareBarrels=" + barrels + " barrelFuel=" + gun?.TryGetComp<CompRefuelable>()?.Fuel);
            deadline = 0; gun = null; artillery.Clear();
            Find.TickManager.Pause();
        }
    }
}
