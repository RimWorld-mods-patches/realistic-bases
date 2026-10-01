using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RealisticBases.Integration;

namespace RealisticBases.Diagnostics
{
    // These actions exercise real Core quest scripts and native caravan arrival.
    // Every mutation is restricted to the repository's disposable profile.
    public static class P4DQuestDebugActions
    {
        private const string Category = "Realistic Bases P4D";
        private const string SaveName = "RealisticBases-P4D-Active";

        internal static bool Disposable => Path.GetFileName(Path.GetDirectoryName(
            Path.GetDirectoryName(GenFilePaths.FilePathForSavedGame(SaveName)))) == "rimworld-dev-profile";

        private static Quest LastQuest => Find.QuestManager.QuestsListForReading.LastOrDefault(q =>
            q.root?.defName == "OpportunitySite_BanditCamp" ||
            q.root?.defName == "OpportunitySite_PrisonerWillingToJoin");

        private static Site QuestSite(Quest q) => q?.PartsListForReading
            .OfType<QuestPart_SpawnWorldObject>().Select(p => p.worldObject).OfType<Site>().FirstOrDefault();

        internal static void Log(string evt, string details) =>
            Verse.Log.Message("[RealisticBases:P4D] event=" + evt + " tick=" + GenTicks.TicksGame + " " + details);

        [DebugAction(Category, "P4D generate native bandit quest", allowedGameStates = AllowedGameStates.Playing)]
        private static void Bandit() => Generate("OpportunitySite_BanditCamp");

        [DebugAction(Category, "P4D generate native rescue quest", allowedGameStates = AllowedGameStates.Playing)]
        private static void Rescue() => Generate("OpportunitySite_PrisonerWillingToJoin");

        [DebugAction(Category, "P4D reject next quest before mutation", allowedGameStates = AllowedGameStates.Playing)]
        private static void RejectNext() => Force(P4DForcedFailure.BeforeMutation);

        [DebugAction(Category, "P4D fail next quest after base", allowedGameStates = AllowedGameStates.Playing)]
        private static void FailNext() => Force(P4DForcedFailure.AfterBase);

        [DebugAction(Category, "P4D fail next quest after transfer", allowedGameStates = AllowedGameStates.Playing)]
        private static void FailTransfer() => Force(P4DForcedFailure.AfterTransfer);

        private static void Force(P4DForcedFailure point)
        {
            if (!Disposable) throw new InvalidOperationException("P4D requires disposable profile");
            P4DQuestGeneration.ForcedFailure = point;
            Log("TEST_FORCE_FAILURE", "point=" + point);
        }

        [DebugAction(Category, "P4D shorten native quest expiry", allowedGameStates = AllowedGameStates.Playing)]
        private static void Expiry()
        {
            if (!Disposable) throw new InvalidOperationException("P4D requires disposable profile");
            QuestPart_WorldObjectTimeout timeout = LastQuest?.PartsListForReading.OfType<QuestPart_WorldObjectTimeout>()
                .SingleOrDefault(p => p.State == QuestPartState.Enabled) ?? throw new InvalidOperationException("No enabled native timeout");
            timeout.delayTicks = GenTicks.TicksGame - timeout.EnableTick + 30;
            Log("TEST_EXPIRY", "quest=" + LastQuest.id + " ticksLeft=" + timeout.TicksLeft + " nativeOutcomePending=true");
        }

        private static void Generate(string name)
        {
            if (!Disposable) throw new InvalidOperationException("P4D requires disposable profile");
            Quest q = QuestUtility.GenerateQuestAndMakeAvailable(DefDatabase<QuestScriptDef>.GetNamed(name), 700f);
            QuestUtility.SendLetterQuestAvailable(q);
            Audit();
        }

        [DebugAction(Category, "P4D accept latest quest", allowedGameStates = AllowedGameStates.Playing)]
        private static void Accept()
        {
            if (!Disposable) throw new InvalidOperationException("P4D requires disposable profile");
            Quest q = LastQuest ?? throw new InvalidOperationException("No P4D quest");
            q.Accept(Find.Maps.SelectMany(m => m.mapPawns.FreeColonistsSpawned).FirstOrDefault());
            Audit();
        }

        [DebugAction(Category, "P4D arrive latest quest by caravan", allowedGameStates = AllowedGameStates.Playing)]
        private static void Arrive()
        {
            if (!Disposable) throw new InvalidOperationException("P4D requires disposable profile");
            Site site = QuestSite(LastQuest) ?? throw new InvalidOperationException("No P4D site");
            Pawn pawn = Find.Maps.Where(m => m.IsPlayerHome).SelectMany(m => m.mapPawns.FreeColonistsSpawned)
                .OrderBy(p => p.thingIDNumber).FirstOrDefault();
            if (pawn != null) pawn.DeSpawn();
            else
            {
                Caravan source = Find.WorldObjects.Caravans.FirstOrDefault(c => c.Faction == Faction.OfPlayer &&
                    c.PawnsListForReading.Any(p => p.IsColonist && !p.Downed));
                pawn = source?.PawnsListForReading.First(p => p.IsColonist && !p.Downed) ??
                    throw new InvalidOperationException("No disposable colonist or caravan");
                source.RemovePawn(pawn);
                if (source.PawnsListForReading.Count == 0) source.Destroy();
            }
            Caravan caravan = CaravanMaker.MakeCaravan(new[] { pawn }, Faction.OfPlayer, site.Tile, true);
            Log("TEST_ARRIVAL", "site=" + site.ID + " pawn=" + pawn.ThingID + " caravan=" + caravan.ID);
            new CaravanArrivalAction_VisitSite(site).Arrived(caravan);
        }

        [DebugAction(Category, "P4D audit latest quest", allowedGameStates = AllowedGameStates.Playing)]
        private static void Audit()
        {
            Quest q = LastQuest;
            Site site = QuestSite(q);
            Log("QUEST_AUDIT", "quest=" + q?.id + " root=" + q?.root?.defName + " state=" + q?.State +
                " site=" + site?.ID + " spawned=" + site?.Spawned + " map=" + site?.Map?.uniqueID +
                " faction=" + site?.Faction?.def.defName + " generator=" + site?.MapGeneratorDef?.defName +
                " timeout=" + site?.WorldObjectTimeoutTicksLeft + " parts=" +
                string.Join(",", site?.parts.Select(p => p.def.defName) ?? Enumerable.Empty<string>()));
            foreach (SitePart p in site?.parts ?? Enumerable.Empty<SitePart>())
                Log("PART_AUDIT", "site=" + site.ID + " part=" + p.def.defName + " worker=" + p.def.Worker.GetType().FullName +
                    " hidden=" + p.hidden + " threat=" + p.parms.threatPoints + " loot=" + p.parms.lootMarketValue +
                    " seed=" + p.parms.randomValue + " expected=" + p.expectedEnemyCount + " held=" +
                    string.Join(",", p.things?.Select(t => t.ThingID) ?? Enumerable.Empty<string>()) + " steps=" +
                    string.Join(",", p.def.ExtraGenSteps.Select(d => d.defName + ":" + d.order + ":" + d.genStep.GetType().Name)));
            foreach (QuestPart p in q?.PartsListForReading ?? Enumerable.Empty<QuestPart>())
            {
                Log("QUEST_PART", "quest=" + q.id + " type=" + p.GetType().Name + " detail=" + p.ToString());
                if (p is QuestPart_DropPods reward)
                    Log("REWARD_AUDIT", "quest=" + q.id + " signal=" + reward.inSignal + " parent=" + reward.mapParent?.ID +
                        " items=" + string.Join(",", reward.Things.Select(t => t.ThingID + ":" + t.stackCount +
                            ":spawned=" + t.SpawnedOrAnyParentSpawned)));
                if (p is QuestPart_PawnsArrive arrivals)
                    Log("REWARD_AUDIT", "quest=" + q.id + " signal=" + arrivals.inSignal +
                        " pawns=" + string.Join(",", arrivals.pawns.Select(t => t.ThingID + ":spawned=" + t.Spawned + ":faction=" + t.Faction?.def.defName)));
            }
            if (site?.Map == null) return;
            Map map = site.Map;
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
                Log("PAWN_AUDIT", "site=" + site.ID + " pawn=" + p.ThingID + " kind=" + p.kindDef.defName +
                    " faction=" + p.Faction?.def.defName + " prisoner=" + p.IsPrisoner + " host=" + p.guest?.HostFaction?.def.defName +
                    " join=" + p.mindState.WillJoinColonyIfRescued + " cell=" + p.Position + " health=" + p.health.summaryHealth.SummaryHealthPercent +
                    " lord=" + p.GetLord()?.LordJob.GetType().Name + " tags=" + string.Join(",", p.questTags ?? Enumerable.Empty<string>()) +
                    " edgePath=" + map.reachability.CanReachMapEdge(p.Position, TraverseParms.For(TraverseMode.PassDoors)));
            Log("MAP_AUDIT", "site=" + site.ID + " pawns=" + map.mapPawns.AllPawnsSpawned.Count +
                " threats=" + GenHostility.AnyHostileActiveThreatToPlayer(map, true, true) +
                " turrets=" + map.listerThings.AllThings.Count(t => t is Building_Turret) +
                " prisonBeds=" + map.listerThings.AllThings.OfType<Building_Bed>().Count(b => b.ForPrisoners) +
                " prisonRooms=" + map.listerThings.AllThings.OfType<Building_Bed>().Where(b => b.ForPrisoners)
                    .Select(b => b.GetRoom()).Distinct().Count() + " fog=" + map.AllCells.Count(c => c.Fogged(map)));
        }

        [DebugAction(Category, "P4D defeat latest quest hostile force", allowedGameStates = AllowedGameStates.Playing)]
        private static void Defeat()
        {
            if (!Disposable) throw new InvalidOperationException("P4D requires disposable profile");
            Map map = QuestSite(LastQuest)?.Map ?? throw new InvalidOperationException("No quest map");
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned.Where(p => !p.IsPrisoner && p.HostileTo(Faction.OfPlayer)).ToList())
                p.Kill(null);
            foreach (Building_Turret t in map.listerThings.AllThings.OfType<Building_Turret>()
                .Where(t => t.HostileTo(Faction.OfPlayer)).ToList()) t.Destroy(DestroyMode.KillFinalize);
            Log("TEST_DEFEAT", "nativeSignalsPending=true");
            Audit();
        }

        [DebugAction(Category, "P4D breach doors and offer native rescue", allowedGameStates = AllowedGameStates.Playing)]
        private static void OfferRescue()
        {
            if (!Disposable) throw new InvalidOperationException("P4D requires disposable profile");
            Map map = QuestSite(LastQuest)?.Map ?? throw new InvalidOperationException("No quest map");
            Pawn target = map.GetComponent<P4DMapState>().OriginalPrisoner ??
                map.mapPawns.AllPawnsSpawned.Single(p => p.IsPrisoner && p.mindState.WillJoinColonyIfRescued);
            Pawn rescuer = map.mapPawns.FreeColonistsSpawned.First(p => p != target);
            if (GenHostility.AnyHostileActiveThreatToPlayer(map, true, true)) throw new InvalidOperationException("Defeat guards before rescue test");
            // Only test instrumentation breaches hostile doors. Production never
            // grants pass-through access or changes enemy ownership to pass audits.
            foreach (Building_Door door in map.listerThings.AllThings.OfType<Building_Door>()
                .Where(d => d.HostileTo(Faction.OfPlayer)).ToList())
                door.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 9999f, instigator: rescuer));
            if (!rescuer.CanReach(target, PathEndMode.Touch, Danger.Deadly)) throw new InvalidOperationException("No native rescue path after breach");
            rescuer.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.OfferHelp, target), JobTag.Misc);
            Log("TEST_OFFER_HELP", "pawn=" + target.ThingID + " rescuer=" + rescuer.ThingID +
                " job=" + rescuer.CurJob?.def.defName + " nativeJoinPending=true");
        }

        [DebugAction(Category, "P4D kill original rescue target", allowedGameStates = AllowedGameStates.Playing)]
        private static void KillTarget()
        {
            if (!Disposable) throw new InvalidOperationException("P4D requires disposable profile");
            Map map = QuestSite(LastQuest)?.Map ?? throw new InvalidOperationException("No quest map");
            Pawn target = map.GetComponent<P4DMapState>().OriginalPrisoner ??
                map.mapPawns.AllPawnsSpawned.Single(p => p.IsPrisoner && p.mindState.WillJoinColonyIfRescued);
            target.Kill(null);
            Log("TEST_TARGET_DEATH", "pawn=" + target.ThingID + " nativeOutcomePending=true");
            Audit();
        }

        [DebugAction(Category, "P4D depart latest quest by caravan", allowedGameStates = AllowedGameStates.Playing)]
        private static void Depart()
        {
            if (!Disposable) throw new InvalidOperationException("P4D requires disposable profile");
            Site site = QuestSite(LastQuest) ?? throw new InvalidOperationException("No quest site");
            Caravan caravan = CaravanExitMapUtility.ExitMapAndCreateCaravan(site.Map.mapPawns.FreeColonistsSpawned.ToList(),
                Faction.OfPlayer, site.Tile, site.Tile, PlanetTile.Invalid, false);
            Log("TEST_DEPARTURE", "site=" + site.ID + " caravan=" + caravan.ID + " nativeRemovalPending=true");
        }

        [DebugAction(Category, "P4D save active quest test", allowedGameStates = AllowedGameStates.Playing)]
        private static void Save()
        {
            if (!Disposable) throw new InvalidOperationException("P4D requires disposable profile");
            GameDataSaveLoader.SaveGame(SaveName);
            Audit();
        }

        [DebugAction(Category, "P4D reload active quest test", allowedGameStates = AllowedGameStates.Playing)]
        private static void Reload()
        {
            if (!Disposable) throw new InvalidOperationException("P4D requires disposable profile");
            GameDataSaveLoader.CheckVersionAndLoadGame(SaveName);
        }
    }

    [HarmonyPatch(typeof(SignalManager), nameof(SignalManager.SendSignal))]
    internal static class P4DQuestSignalAudit
    {
        private static void Prefix(Signal signal)
        {
            if (P4DQuestDebugActions.Disposable && signal.tag.StartsWith("Quest", StringComparison.Ordinal))
                P4DQuestDebugActions.Log("NATIVE_SIGNAL", "tag=" + signal.tag);
        }
    }
}
