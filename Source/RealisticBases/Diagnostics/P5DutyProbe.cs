using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LudeonTK;
using RealisticBases.AI;
using RealisticBases.Core;
using RealisticBases.Integration;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RealisticBases.Diagnostics
{
    // Opt-in disposable six-hour measurements. Outage switches the owned
    // generators off and samples whether every switched-on consumer stays
    // powered from the batteries. Combat spawns native hostile assault waves
    // and measures actual supply use, ammunition exhaustion and medical care.
    // Neither mode adds supplies, residents, orders or research.
    public sealed class P5DutyProbe : MapComponent
    {
        internal const int Horizon = 15000; // six game hours
        private const int MedicalGraceTicks = 2500;
        private enum Mode { None, Arming, Outage, Combat }
        private Mode mode;
        private int start, lastSample, wavesSpawned;
        private Pawn visitor;
        private IntVec3 visitorCell = IntVec3.Invalid;
        private Faction raiders;
        private float wavePoints;
        private readonly List<CompFlickable> switchedOff = new List<CompFlickable>();
        // Generation without a switch, held at zero output for the horizon. It
        // stays spawned: a solar or wind yard can carry the conduit tree.
        internal static readonly HashSet<Thing> Zeroed = new HashSet<Thing>();
        private readonly List<Thing> detached = new List<Thing>();
        private int stableSince = -1;
        private readonly Dictionary<string, int> unpowered = new Dictionary<string, int>();
        private readonly Dictionary<string, int> before = new Dictionary<string, int>();
        private readonly Dictionary<int, int> downedSafeSince = new Dictionary<int, int>();
        private readonly Dictionary<int, int> untendedSafeSince = new Dictionary<int, int>();
        private readonly HashSet<int> rescueJobs = new HashSet<int>(), tendJobs = new HashSet<int>();
        private readonly HashSet<int> downedResidents = new HashSet<int>(), injuredResidents = new HashSet<int>();
        private int ammoOutSamples, neglectSamples, minorUntendedSamples, minResidents = int.MaxValue;
        private readonly Dictionary<string, int> neglectByPawn = new Dictionary<string, int>();
        private readonly Dictionary<string, int> neglectContext = new Dictionary<string, int>();
        private readonly Dictionary<int, Pawn> initial = new Dictionary<int, Pawn>();
        private float minCharge = float.MaxValue, startCharge, unswitchableGeneration;
        public P5DutyProbe(Map map) : base(map) { }
        internal bool Running => mode != Mode.None;

        private P3GarrisonController Controller => map.GetComponent<P3GarrisonController>();

        // Arms first: the outage starts only once every switched-on consumer has
        // been powered for 300 ticks, so start-up power-on delays are not counted.
        public void StartOutage()
        {
            Begin("RealisticBases-P5-before-outage");
            mode = Mode.Arming;
            stableSince = -1;
            P0Log.Message("P5_OUTAGE_PROBE_ARMED", "map=" + map.uniqueID + " tick=" + start);
        }

        private void Arm()
        {
            bool allOn = Consumers().All(c => c.PowerOn || c.PowerNet == null ||
                c.parent.TryGetComp<CompFlickable>()?.SwitchIsOn == false);
            if (!allOn) stableSince = -1;
            else if (stableSince < 0) stableSince = GenTicks.TicksGame;
            if (stableSince >= 0 && GenTicks.TicksGame - stableSince >= 300) { BeginOutage(); return; }
            if (GenTicks.TicksGame - start > 5000)
            {
                mode = Mode.None;
                Find.TickManager.Pause();
                P0Log.Message("P5_OUTAGE_PROBE_COMPLETE", "map=" + map.uniqueID + " passed=False reason=consumers_never_stably_powered " +
                    "unpowered=" + string.Join(",", Consumers().Where(c => !c.PowerOn).Select(c => c.parent.def.defName)));
            }
        }

        // Zero generation: switchable generators are switched off; generation
        // without a switch (solar, wind, ...) is held at zero output for the
        // horizon. Test-only mutations, both logged and undone at the end.
        private void BeginOutage()
        {
            foreach (Thing plant in Controller.P5OwnedThings.Where(t => t.Spawned && t.TryGetComp<CompPowerPlant>() != null).ToList())
            {
                CompFlickable flick = plant.TryGetComp<CompFlickable>();
                if (flick != null)
                {
                    if (!flick.SwitchIsOn) continue;
                    flick.SwitchIsOn = false;
                    switchedOff.Add(flick);
                    continue;
                }
                unswitchableGeneration += Math.Max(0, plant.TryGetComp<CompPowerPlant>().PowerOutput);
                detached.Add(plant);
                Zeroed.Add(plant);
                plant.TryGetComp<CompPowerPlant>().PowerOutput = 0;
            }
            var batteries = Batteries().ToList();
            startCharge = batteries.Sum(b => b.StoredEnergy);
            start = GenTicks.TicksGame;
            mode = Mode.Outage;
            P0Log.Message("P5_OUTAGE_PROBE_START", "map=" + map.uniqueID + " start=" + start + " horizon=" + Horizon +
                " generatorsSwitchedOff=" + switchedOff.Count + " renewablesZeroed=" + detached.Count +
                " zeroedGenerationW=" + unswitchableGeneration.ToString("F0") +
                " batteries=" + batteries.Count + " chargeWd=" + startCharge.ToString("F2") +
                " ratedLoadW=" + Consumers().Sum(c => c.Props.PowerConsumption).ToString("F0") +
                " actualLoadW=" + (-Consumers().Where(c => c.PowerOn).Sum(c => c.PowerOutput)).ToString("F0") +
                " suppliesAdded=0 npcOrdersIssued=0");
        }

        public void StartCombat(float points)
        {
            Settlement settlement = (Settlement)map.Parent;
            raiders = Find.FactionManager.AllFactions.Where(f => !f.IsPlayer && !f.defeated && !f.Hidden && f != settlement.Faction &&
                    f.def.humanlikeFaction && f.HostileTo(settlement.Faction) &&
                    f.def.pawnGroupMakers?.Any(g => g.kindDef == PawnGroupKindDefOf.Combat) == true)
                .OrderBy(f => f.loadID).FirstOrDefault() ?? throw new InvalidOperationException("No hostile raider faction");
            Begin("RealisticBases-P5-before-combat");
            wavePoints = points;
            foreach (string def in new[] { "Shell_HighExplosive", "Shell_Incendiary", "ReinforcedBarrel", "Steel", "MedicineIndustrial", "MedicineHerbal" })
                before[def] = Units(def);
            before["LoadedShells"] = LoadedShells();
            before["BarrelFuel"] = (int)Math.Round(Mortars().Sum(m => m.TryGetComp<CompRefuelable>()?.Fuel ?? 0));
            mode = Mode.Combat;
            P5ReadinessAudit.Audit(map);
            P0Log.Message("P5_COMBAT_PROBE_START", "map=" + map.uniqueID + " start=" + start + " horizon=" + Horizon +
                " raiders=" + raiders.def.defName + " wavePoints=" + points + " waves=3 residents=" + Controller.P5Residents.Count() +
                " stock=" + string.Join(",", before.Select(p => p.Key + ":" + p.Value)) + " suppliesAdded=0 npcOrdersIssued=0");
            try { SpawnWave(); }
            catch (Exception ex)
            {
                mode = Mode.None;
                if (visitor != null && !visitor.Dead && visitorCell.InBounds(map) && visitorCell.Standable(map))
                {
                    visitor.jobs?.StopAll();
                    if (visitor.Spawned) visitor.DeSpawn();
                    GenSpawn.Spawn(visitor, visitorCell, map);
                    if (visitor.drafter != null) visitor.drafter.Drafted = false;
                }
                P0Log.Message("P5_COMBAT_PROBE_START_REJECT", "map=" + map.uniqueID + " tick=" + GenTicks.TicksGame +
                    " reason=" + ex.Message.Replace(' ', '_') + " visitorRestored=" + (visitor?.Spawned ?? false));
                throw;
            }
        }

        private void Begin(string save)
        {
            if (Running || map.GetComponent<P5RoutineProbe>()?.Running == true) throw new InvalidOperationException("A P5 probe is already running");
            if (Controller.P5Residents.Count() < 1) throw new InvalidOperationException("No funded ordinary roster");
            visitor = map.mapPawns.FreeColonistsSpawned.SingleOrDefault()
                ?? throw new InvalidOperationException("Require exactly one disposable visitor");
            GameDataSaveLoader.SaveGame(save);
            visitorCell = visitor.Position;
            Map home = Find.Maps.Single(m => m.IsPlayerHome && m != map);
            IntVec3 homeCell = home.AllCells.OrderBy(c => c.DistanceToSquared(home.Center))
                .First(c => c.Standable(home) && c.GetEdifice(home) == null);
            visitor.jobs.StopAll();
            if (visitor.drafter != null) visitor.drafter.Drafted = false;
            visitor.DeSpawn();
            GenSpawn.Spawn(visitor, homeCell, home);
            start = GenTicks.TicksGame; lastSample = start;
            switchedOff.Clear(); detached.Clear(); unpowered.Clear(); before.Clear(); downedSafeSince.Clear(); untendedSafeSince.Clear();
            rescueJobs.Clear(); tendJobs.Clear(); neglectByPawn.Clear(); neglectContext.Clear(); initial.Clear(); downedResidents.Clear(); injuredResidents.Clear();
            ammoOutSamples = neglectSamples = minorUntendedSamples = wavesSpawned = 0; minResidents = int.MaxValue;
            minCharge = float.MaxValue; unswitchableGeneration = 0;
        }

        private void SpawnWave()
        {
            List<Pawn> pawns = PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms
            {
                groupKind = PawnGroupKindDefOf.Combat, tile = map.Tile, faction = raiders, points = wavePoints
            }).ToList();
            Pawn representative = pawns.FirstOrDefault();
            if (representative == null) throw new InvalidOperationException("Combat raid wave produced no pawns");
            if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && !c.Fogged(map) && c.GetEdifice(map) == null,
                    map, CellFinder.EdgeRoadChance_Hostile, out IntVec3 probeEdge))
                throw new InvalidOperationException("No standable edge cell for hostile path audit");
            GenSpawn.Spawn(representative, probeEdge, map);
            IReadOnlyList<KeyValuePair<string, IntVec3>> perimeter = Controller.P5HostilePerimeterApproaches(representative);
            IReadOnlyList<KeyValuePair<string, IntVec3>> gates = Controller.P5HostileGateApproaches(representative);
            string preferredSide = Controller.P5SuggestedAssaultSide;
            var sameSideGates = gates.Where(p => p.Key == preferredSide).ToList();
            var sameSideWalls = perimeter.Where(p => p.Key == preferredSide).ToList();
            var targetApproaches = sameSideGates.Count > 0 ? sameSideGates : sameSideWalls;
            var paths = targetApproaches.Select(p => new
            {
                p.Key,
                p.Value,
                Edge = CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && !c.Fogged(map) &&
                    EdgeMatchesSide(c, p.Key) &&
                    map.reachability.CanReach(c, p.Value, PathEndMode.OnCell, TraverseParms.For(representative)),
                    map, CellFinder.EdgeRoadChance_Hostile, out IntVec3 edge) ? (IntVec3?)edge : null
            }).ToList();
            string sideAudit = string.Join(";", perimeter.Select(p => p.Key).Concat(gates.Select(p => p.Key))
                .Distinct().OrderBy(side => side, StringComparer.Ordinal).Select(side =>
                    side + ":outerApproaches=" + perimeter.Count(p => p.Key == side) +
                    ":gateApproaches=" + gates.Count(p => p.Key == side) +
                    ":sameSideGateEdge=" + (sameSideGates.Where(p => p.Key == side).Any(p =>
                        CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && !c.Fogged(map) &&
                            EdgeMatchesSide(c, side) && map.reachability.CanReach(c, p.Value, PathEndMode.OnCell,
                                TraverseParms.For(representative)), map, CellFinder.EdgeRoadChance_Hostile, out _)) ? "yes" : "no")));
            var selected = paths.FirstOrDefault(p => p.Edge.HasValue);
            if (selected == null)
            {
                foreach (Pawn pawn in pawns.Where(p => p.Spawned)) pawn.DeSpawn();
                throw new InvalidOperationException("No same-side hostile path to a gate or outer wall; selectedSide=" +
                    (preferredSide ?? "none") + "; sides=" + sideAudit);
            }
            IntVec3 edgeCell = selected.Edge.Value;
            P0Log.Message("P5_COMBAT_EDGE_AUDIT", "map=" + map.uniqueID + " side=" + selected.Key +
                " probeEdge=" + probeEdge + " edge=" + edgeCell + " outerApproach=" + selected.Value +
                " gateApproach=" + sameSideGates.Contains(new KeyValuePair<string, IntVec3>(selected.Key, selected.Value)) +
                " target=" + Controller.SupportCenter + " hostileTraverse=True doorsClosedToEnemies=True sameSideEdge=True sides=" + sideAudit);
            IntVec3 edge = edgeCell;
            if (representative.Position != edge)
            {
                representative.DeSpawn();
                GenSpawn.Spawn(representative, edge, map);
            }
            foreach (Pawn pawn in pawns.Where(p => p != representative))
                GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(edge, map, 6), map);
            LordMaker.MakeNewLord(raiders, new LordJob_AssaultColony(raiders, canKidnap: false, canTimeoutOrFlee: false,
                canSteal: false), map, pawns);
            wavesSpawned++;
            P0Log.Message("P5_COMBAT_WAVE", "map=" + map.uniqueID + " wave=" + wavesSpawned + " tick=" + GenTicks.TicksGame +
                " pawns=" + pawns.Count + " edge=" + edge + " kinds=" + string.Join(",", pawns.Select(p => p.kindDef.defName)));
        }

        private bool EdgeMatchesSide(IntVec3 cell, string side)
        {
            switch (side)
            {
                case "north": return cell.z == map.Size.z - 1;
                case "south": return cell.z == 0;
                case "east": return cell.x == map.Size.x - 1;
                case "west": return cell.x == 0;
                default: return false;
            }
        }

        // Owned operating units of one definition, loose or carried by residents.
        private int Units(string defName) => Controller.P5OwnedThings
            .Concat(Controller.P5Residents.SelectMany(p => p.inventory.innerContainer.Concat(
                p.carryTracker.CarriedThing == null ? Enumerable.Empty<Thing>() : new[] { p.carryTracker.CarriedThing })))
            .Distinct().Where(t => !t.Destroyed && t.def.defName == defName).Sum(t => t.stackCount);

        private IEnumerable<CompPowerBattery> Batteries() => Controller.P5OwnedThings.Where(t => t.Spawned)
            .Select(t => t.TryGetComp<CompPowerBattery>()).Where(b => b != null);
        private IEnumerable<CompPowerTrader> Consumers() => Controller.P5OwnedThings.Where(t => t.Spawned)
            .Select(t => t.TryGetComp<CompPowerTrader>()).Where(p => p != null && p.Props.PowerConsumption > 0 &&
                p.parent.TryGetComp<CompPowerPlant>() == null);
        private IEnumerable<Building_TurretGun> Mortars() => Controller.P5OwnedThings.OfType<Building_TurretGun>()
            .Where(t => t.Spawned && t.gun?.TryGetComp<CompChangeableProjectile>() != null);
        private int LoadedShells() => Mortars().Sum(m => m.gun.TryGetComp<CompChangeableProjectile>().loadedCount);
        private bool HostilesPresent() => map.mapPawns.AllPawnsSpawned.Any(p => !p.Dead && !p.Downed &&
            p.Faction != null && p.Faction.HostileTo(map.ParentFaction) && !p.IsPrisoner);
        private bool HostileNear(Pawn pawn) => map.mapPawns.AllPawnsSpawned.Any(p => !p.Dead && !p.Downed &&
            p.Faction != null && p.Faction.HostileTo(map.ParentFaction) && p.Position.InHorDistOf(pawn.Position, 25f));

        public override void MapComponentTick()
        {
            if (!Running || GenTicks.TicksGame - lastSample < 60) return;
            lastSample = GenTicks.TicksGame;
            if (mode == Mode.Arming) { Arm(); return; }
            int elapsed = GenTicks.TicksGame - start;
            if (mode == Mode.Outage) SampleOutage(); else SampleCombat(elapsed);
            if (elapsed >= Horizon || !(map.Parent is Settlement) || !Controller.P5Residents.Any()) Finish(elapsed);
        }

        private void SampleOutage()
        {
            minCharge = Math.Min(minCharge, Batteries().Sum(b => b.StoredEnergy));
            foreach (CompPowerTrader consumer in Consumers())
            {
                CompFlickable flick = consumer.parent.TryGetComp<CompFlickable>();
                if (consumer.PowerOn || flick != null && !flick.SwitchIsOn || consumer.PowerNet == null) continue;
                unpowered.TryGetValue(consumer.parent.def.defName, out int n);
                unpowered[consumer.parent.def.defName] = n + 1;
            }
        }

        private void SampleCombat(int elapsed)
        {
            if (wavesSpawned < 3 && elapsed >= wavesSpawned * (Horizon / 3)) SpawnWave();
            List<Pawn> residents = Controller.P5Residents.ToList();
            minResidents = Math.Min(minResidents, residents.Count(p => !p.Downed));
            bool hostiles = HostilesPresent();
            if (hostiles && Mortars().Any() && LoadedShells() == 0 && Controller.DebugOperatingStock("Shell_HighExplosive") == 0 &&
                Controller.DebugOperatingStock("Shell_Incendiary") == 0) ammoOutSamples++;
            // Neglect needs an idle medical system: a support worker exists and
            // nobody is tending or rescuing. A busy doctor is care in progress.
            bool caring = residents.Any(p => p.CurJobDef == JobDefOf.TendPatient || p.CurJobDef == JobDefOf.Rescue);
            bool helper = !caring && residents.Any(p => !p.Downed && !p.health.HasHediffsNeedingTend() &&
                Controller.P5AssignedRole(p) == "Support");
            foreach (Pawn pawn in residents) initial[pawn.thingIDNumber] = pawn;
            foreach (Pawn pawn in residents)
            {
                int id = pawn.thingIDNumber;
                if (pawn.CurJobDef == JobDefOf.Rescue && pawn.CurJob != null) rescueJobs.Add(pawn.CurJob.loadID);
                if (pawn.CurJobDef == JobDefOf.TendPatient && pawn.CurJob != null) tendJobs.Add(pawn.CurJob.loadID);
                if (pawn.Downed) downedResidents.Add(id);
                if (pawn.health.HasHediffsNeedingTend()) injuredResidents.Add(id);
                bool safe = !HostileNear(pawn) && helper;
                Track(downedSafeSince, id, pawn.Downed && !pawn.InBed() && safe);
                // Urgent: bleeding. Minor wounds may wait longer and are reported only.
                Track(untendedSafeSince, id, pawn.health.HasHediffsNeedingTend() && pawn.health.hediffSet.BleedRateTotal > 0 && safe);
                if (pawn.health.HasHediffsNeedingTend() && safe) minorUntendedSamples++;
            }
        }

        // Neglect: a resident stays downed outside a bed, or untended, for longer
        // than the grace period while no hostile is near and a support worker is free.
        private void Track(Dictionary<int, int> since, int id, bool condition)
        {
            if (!condition) { since.Remove(id); return; }
            if (!since.ContainsKey(id)) since[id] = GenTicks.TicksGame;
            else if (GenTicks.TicksGame - since[id] > MedicalGraceTicks)
            {
                neglectSamples++;
                string key = id + (since == downedSafeSince ? ":downed" : ":bleeding");
                neglectByPawn.TryGetValue(key, out int n);
                neglectByPawn[key] = n + 1;
                // What the patient and the support workers were doing meanwhile.
                Pawn patient = Controller.P5Residents.FirstOrDefault(p => p.thingIDNumber == id);
                var context = new List<string> { "patient:" + (patient?.InBed() == true ? "bed:" : "") + (patient?.CurJobDef?.defName ?? "none") +
                    ":" + (patient == null ? "?" : Controller.P5AssignedRole(patient)) };
                context.AddRange(Controller.P5Residents.Where(p => Controller.P5AssignedRole(p) == "Support")
                    .Select(p => "support:" + (p.CurJobDef?.defName ?? "none")));
                foreach (string c in context) { neglectContext.TryGetValue(c, out int m); neglectContext[c] = m + 1; }
            }
        }

        private void Finish(int elapsed)
        {
            Mode finished = mode;
            mode = Mode.None;
            Find.TickManager.Pause();
            foreach (CompFlickable flick in switchedOff) flick.SwitchIsOn = true;
            foreach (Thing plant in detached) Zeroed.Remove(plant);
            bool horizon = elapsed >= Horizon;
            bool parentOk = map.Parent is Settlement;
            if (finished == Mode.Outage)
            {
                bool passed = horizon && parentOk && unpowered.Count == 0;
                P0Log.Message("P5_OUTAGE_PROBE_COMPLETE", "map=" + map.uniqueID + " passed=" + passed + " elapsed=" + elapsed +
                    " generatorsRestored=" + switchedOff.Count + " renewablesRestored=" + detached.Count(p => !Zeroed.Contains(p)) +
                    "/" + detached.Count + " zeroedGenerationW=" + unswitchableGeneration.ToString("F0") +
                    " startChargeWd=" + startCharge.ToString("F2") + " minChargeWd=" + minCharge.ToString("F2") +
                    " unpoweredSamples=" + (unpowered.Count == 0 ? "0" : string.Join(",", unpowered.Select(p => p.Key + ":" + p.Value))) +
                    " suppliesAdded=0");
            }
            else
            {
                var used = before.Keys.ToDictionary(k => k, k => before[k] - (k == "LoadedShells" ? LoadedShells() : k == "BarrelFuel"
                    ? (int)Math.Round(Mortars().Sum(m => m.TryGetComp<CompRefuelable>()?.Fuel ?? 0)) : Units(k)));
                bool medicalObserved = injuredResidents.Count == 0 || tendJobs.Count > 0;
                bool passed = horizon && parentOk && ammoOutSamples == 0 && neglectSamples == 0 && medicalObserved;
                P0Log.Message("P5_COMBAT_PROBE_COMPLETE", "map=" + map.uniqueID + " passed=" + passed + " elapsed=" + elapsed +
                    " settlementActive=" + parentOk + " waves=" + wavesSpawned + " residentsLeft=" + Controller.P5Residents.Count() +
                    " minStanding=" + (minResidents == int.MaxValue ? 0 : minResidents) + " hostilesLeft=" + HostilesPresent() +
                    " used=" + string.Join(",", used.Select(p => p.Key + ":" + p.Value)) +
                    " deaths=" + string.Join(",", initial.Values.Where(p => p.Dead).Select(p => p.thingIDNumber +
                        ":bloodLoss" + (p.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss)?.Severity ?? 0).ToString("F2") +
                        ":wasDowned" + downedResidents.Contains(p.thingIDNumber))) +
                    " ammoOutSamples=" + ammoOutSamples + " downed=" + downedResidents.Count + " injured=" + injuredResidents.Count +
                    " rescueJobs=" + rescueJobs.Count + " tendJobs=" + tendJobs.Count + " urgentNeglectSamples=" + neglectSamples + " safeUntendedSamples=" + minorUntendedSamples +
                    " untendedAtEnd=" + Controller.P5Residents.Count(p => p.health.HasHediffsNeedingTend()) + (neglectByPawn.Count == 0 ? "" : " neglect=" +
                        string.Join(",", neglectByPawn.Select(p => p.Key + ":" + p.Value)) + " context=" +
                        string.Join(",", neglectContext.Select(p => p.Key + ":" + p.Value))) +
                    " suppliesAdded=0 npcOrdersIssued=0");
                P5ReadinessAudit.Audit(map);
            }
            // Keep the parked visitor home while hostiles remain on the map.
            if (visitor != null && !visitor.Dead && parentOk && !HostilesPresent() && visitorCell.InBounds(map) && visitorCell.Standable(map))
            {
                visitor.jobs?.StopAll();
                if (visitor.Spawned) visitor.DeSpawn();
                GenSpawn.Spawn(visitor, visitorCell, map);
            }
        }

        // An interrupted measurement is never resumed or credited.
        public override void ExposeData()
        {
            int saved = (int)mode;
            Scribe_Values.Look(ref saved, "p5DutyMode", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && saved != 0)
            {
                mode = Mode.None;
                P0Log.Message("P5_DUTY_PROBE_INTERRUPTED", "map=" + map.uniqueID + " reason=reload repeatFromPreProbeSave=True");
            }
        }
    }

    public static class P5DutyDebugActions
    {
        [DebugAction("Realistic Bases P5", "Start P5 six-hour outage probe", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Outage() => P5DebugActions.CurrentSettlement().Map.GetComponent<P5DutyProbe>().StartOutage();

        [DebugAction("Realistic Bases P5", "Start P5 six-hour combat probe", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Combat() => P5DebugActions.CurrentSettlement().Map.GetComponent<P5DutyProbe>().StartCombat(250f);
    }

    [HarmonyPatch(typeof(CompPowerPlant), nameof(CompPowerPlant.UpdateDesiredPowerOutput))]
    internal static class P5DutyZeroGenerationPatch
    {
        private static void Postfix(CompPowerPlant __instance)
        {
            if (P5DutyProbe.Zeroed.Count > 0 && P5DutyProbe.Zeroed.Contains(__instance.parent)) __instance.PowerOutput = 0;
        }
    }

    [HarmonyPatch(typeof(Settlement), nameof(Settlement.ShouldRemoveMapNow))]
    internal static class P5DutyMapHoldPatch
    {
        private static bool Prefix(Settlement __instance, ref bool alsoRemoveWorldObject, ref bool __result)
        {
            if (!P4DQuestDebugActions.Disposable || __instance.Map?.GetComponent<P5DutyProbe>()?.Running != true) return true;
            // Test instrumentation only: hold the map while the parked visitor is away.
            alsoRemoveWorldObject = false;
            __result = false;
            return false;
        }
    }
}
