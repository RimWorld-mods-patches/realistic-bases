using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RealisticBases.AI;
using RealisticBases.Core;
using RealisticBases.Integration;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace RealisticBases.Diagnostics
{
    // Opt-in disposable measurement only. It does not change resident needs,
    // stocks, equipment, research, generator output or the controller doctrine.
    public sealed class P5RoutineProbe : MapComponent
    {
        private bool running;
        private bool interrupted;
        private int start, finish, lastSample;
        private Pawn visitor;
        private Faction previousHost;
        private bool previousDrafted;
        private IntVec3 previousCell = IntVec3.Invalid;
        private int settlementId = -1;
        private List<int> initialIds = new List<int>();
        private readonly HashSet<int> ate = new HashSet<int>();
        private readonly HashSet<int> slept = new HashSet<int>();
        private readonly HashSet<int> sleptInBed = new HashSet<int>();
        private readonly HashSet<int> refuelJobs = new HashSet<int>();
        private readonly Dictionary<int, float> lastFuel = new Dictionary<int, float>();
        private int fuelIncreases, missingSamples, starvingSamples, downedSamples, powerOffSamples;
        private float minimumFood = 1, minimumRest = 1;
        public P5RoutineProbe(Map map) : base(map) { }
        internal bool Running => running;

        public void Start()
        {
            P5DebugActions.CurrentSettlement();
            if (running) throw new InvalidOperationException("Routine probe already running");
            var residents = map.GetComponent<P3GarrisonController>().P5Residents.ToList();
            if (residents.Count < 4) throw new InvalidOperationException("No funded ordinary roster");
            visitor = map.mapPawns.FreeColonistsSpawned.SingleOrDefault()
                ?? throw new InvalidOperationException("Require exactly one disposable visitor");
            if (visitor.guest == null || visitor.HostFaction != null || visitor.questTags?.Count > 0)
                throw new InvalidOperationException("Visitor cannot have guest/quest ownership");
            GameDataSaveLoader.SaveGame("RealisticBases-P5-before-routine");
            previousHost = visitor.HostFaction;
            previousDrafted = visitor.Drafted;
            previousCell = visitor.Position;
            settlementId = map.Parent.ID;
            Map home = Find.Maps.Single(m => m.IsPlayerHome && m != map);
            IntVec3 homeCell = home.AllCells.OrderBy(c => c.DistanceToSquared(home.Center))
                .First(c => c.Standable(home) && c.GetEdifice(home) == null);
            visitor.jobs.StopAll();
            if (visitor.drafter != null) visitor.drafter.Drafted = false;
            visitor.DeSpawn();
            GenSpawn.Spawn(visitor, homeCell, home);
            start = GenTicks.TicksGame; finish = checked(start + 3 * 60000); lastSample = start - 150;
            initialIds = residents.Select(p => p.thingIDNumber).OrderBy(id => id).ToList();
            ate.Clear(); slept.Clear(); sleptInBed.Clear(); refuelJobs.Clear(); lastFuel.Clear();
            fuelIncreases = missingSamples = starvingSamples = downedSamples = powerOffSamples = 0;
            minimumFood = minimumRest = 1;
            running = true;
            P5ReadinessAudit.Audit(map);
            P0Log.Message("P5_ROUTINE_PROBE_START", "map=" + map.uniqueID + " start=" + start + " finish=" + finish +
                " residents=" + initialIds.Count + " visitor=" + visitor.ThingID +
                " disposableMapHold=True visitorParkedHome=" + home.uniqueID +
                " npcOrdersIssued=0 suppliesAdded=0 futureProductionCredited=0");
        }

        public override void MapComponentTick()
        {
            if (!running || GenTicks.TicksGame - lastSample < 150) return;
            lastSample = GenTicks.TicksGame;
            var controller = map.GetComponent<P3GarrisonController>();
            var residents = controller.P5Residents.ToList();
            bool parentChanged = !(map.Parent is Settlement) || map.Parent.ID != settlementId;
            if (residents.Count != initialIds.Count || residents.Any(p => !initialIds.Contains(p.thingIDNumber))) missingSamples++;
            foreach (Pawn pawn in residents)
            {
                if (pawn.Downed) downedSamples++;
                if (pawn.needs?.food != null)
                {
                    minimumFood = Math.Min(minimumFood, pawn.needs.food.CurLevelPercentage);
                    if (pawn.needs.food.Starving) starvingSamples++;
                    if (pawn.needs.food.GainingFood()) ate.Add(pawn.thingIDNumber);
                }
                if (pawn.needs?.rest != null) minimumRest = Math.Min(minimumRest, pawn.needs.rest.CurLevelPercentage);
                if (pawn.CurJobDef == JobDefOf.LayDown && pawn.needs?.rest?.Resting == true)
                {
                    slept.Add(pawn.thingIDNumber);
                    if (pawn.CurJob.targetA.Thing is Building_Bed) sleptInBed.Add(pawn.thingIDNumber);
                }
                if (pawn.CurJobDef == JobDefOf.Refuel && pawn.CurJob != null) refuelJobs.Add(pawn.CurJob.loadID);
            }
            foreach (Thing thing in controller.P5OwnedThings.Where(t => t.Spawned && !(t is Building_Turret)))
            {
                var fuel = thing.TryGetComp<CompRefuelable>();
                if (fuel != null)
                {
                    if (lastFuel.TryGetValue(thing.thingIDNumber, out float prior) && fuel.Fuel > prior + 0.01f) fuelIncreases++;
                    lastFuel[thing.thingIDNumber] = fuel.Fuel;
                }
                var power = thing.TryGetComp<CompPowerTrader>();
                if (thing.def.defName == "Cooler" && power?.PowerOn == false) powerOffSamples++;
            }
            if (GenTicks.TicksGame >= finish || missingSamples > 0 || parentChanged)
            {
                bool fullHorizon = GenTicks.TicksGame >= finish;
                running = false;
                Find.TickManager.Pause();
                RestoreVisitor();
                bool passed = fullHorizon && !parentChanged && missingSamples == 0 && starvingSamples == 0 && downedSamples == 0 &&
                    ate.Count == initialIds.Count && sleptInBed.Count == initialIds.Count && fuelIncreases > 0;
                P0Log.Message("P5_ROUTINE_PROBE_COMPLETE", "map=" + map.uniqueID + " passed=" + passed +
                    " elapsed=" + (GenTicks.TicksGame - start) + " target=180000 residents=" + initialIds.Count +
                    " ate=" + ate.Count + " slept=" + slept.Count + " sleptInBed=" + sleptInBed.Count + " nativeRefuelJobs=" + refuelJobs.Count +
                    " physicalFuelIncreases=" + fuelIncreases + " missingSamples=" + missingSamples +
                    " starvingSamples=" + starvingSamples + " downedSamples=" + downedSamples +
                    " minFood=" + minimumFood.ToString("F3") + " minRest=" + minimumRest.ToString("F3") +
                    " coolerPowerOffSamples=" + powerOffSamples + " parentChanged=" + parentChanged +
                    " disposableMapHold=True npcOrdersIssued=0 suppliesAdded=0");
                P5ReadinessAudit.Audit(map);
            }
            else if ((GenTicks.TicksGame - start) / 15000 != (GenTicks.TicksGame - start - 150) / 15000)
                P0Log.Message("P5_ROUTINE_PROBE_PROGRESS", "map=" + map.uniqueID + " elapsed=" + (GenTicks.TicksGame - start) +
                    " ate=" + ate.Count + " slept=" + slept.Count + " refuels=" + fuelIncreases +
                    " starvingSamples=" + starvingSamples + " downedSamples=" + downedSamples);
        }
        public override void ExposeData()
        {
            // Do not resume a half-measured run with missing in-memory counters.
            // Native residents/stock remain saved normally; repeat from the named
            // pre-probe save for a complete uninterrupted horizon measurement.
            Scribe_References.Look(ref visitor, "p5RoutineVisitor");
            Scribe_References.Look(ref previousHost, "p5RoutineVisitorHost");
            Scribe_Values.Look(ref previousDrafted, "p5RoutineVisitorDrafted", false);
            Scribe_Values.Look(ref previousCell, "p5RoutineVisitorCell", IntVec3.Invalid);
            Scribe_Values.Look(ref running, "p5RoutineRunning", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && running)
            {
                running = false;
                interrupted = true;
            }
        }
        public override void FinalizeInit()
        {
            if (!interrupted) return;
            interrupted = false;
            RestoreVisitor();
            P0Log.Message("P5_ROUTINE_PROBE_INTERRUPTED", "map=" + map.uniqueID + " reason=reload repeatFromPreProbeSave=True");
        }
        private void RestoreVisitor()
        {
            if (visitor == null || visitor.Dead) return;
            visitor.jobs?.StopAll();
            visitor.guest?.SetGuestStatus(previousHost, GuestStatus.Guest);
            if (previousCell.InBounds(map) && previousCell.Standable(map) && visitor.Map != map)
            {
                if (visitor.Spawned) visitor.DeSpawn();
                GenSpawn.Spawn(visitor, previousCell, map);
            }
            if (visitor.drafter != null) visitor.drafter.Drafted = previousDrafted;
        }
    }

    [HarmonyPatch(typeof(Settlement), nameof(Settlement.ShouldRemoveMapNow))]
    internal static class P5RoutineMapHoldPatch
    {
        private static bool Prefix(Settlement __instance, ref bool alsoRemoveWorldObject, ref bool __result)
        {
            if (!P4DQuestDebugActions.Disposable || __instance.Map?.GetComponent<P5RoutineProbe>()?.Running != true) return true;
            // Test instrumentation only. Native defeat remains untouched, and
            // ordinary removal/revisit must be verified separately in P5.4.
            alsoRemoveWorldObject = false;
            __result = false;
            return false;
        }
    }
}
