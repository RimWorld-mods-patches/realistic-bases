using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using HarmonyLib;
using LudeonTK;
using RealisticBases.Core;
using RealisticBases.Integration;
using RealisticBases.Planning;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealisticBases.Diagnostics
{
    // Disposable live gallery only; uses naturally occurring world tiles.
    public static class GeologicalLandformTour
    {
        private static readonly string[] Forms = { "Archipelago", "Atoll", "Badlands", "Caldera", "Canyon", "CaveEntrance", "Cirque", "Cliff", "CliffAndCoast", "CliffCorner", "Coast", "CoastCorner", "CoastalIsland", "Cove", "CoveWithIsland", "Crater", "DesertPlateau", "DryLake", "Fjord", "Glacier", "Gorge", "IceOasis", "Island", "Lake", "LakeWithIsland", "Landbridge", "LoneMountain", "Oasis", "Peninsula", "Rift", "River", "RiverConfluence", "RiverDelta", "RiverIsland", "RiverSource", "RiverTerrain", "SecludedCove", "SecludedValley", "Sinkhole", "Skerry", "SurfaceCave", "SwampHill", "Tombolo", "Valley" };
        private static int current = -1;
        private static PlanetTile? pendingTile;
        private static string[] generatedLandforms;

        // GL prepares this context in its Priority.First prefix and clears it
        // in its postfix. Tile mutators alone do not prove a form was applied
        // (GL may filter it for this world object).
        [HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.GenerateContentsIntoMap))]
        private static class CaptureGeneratorContext
        {
            [HarmonyPrefix, HarmonyPriority(Priority.Last)]
            private static void Prefix(Map map)
            {
                if (!pendingTile.HasValue || map.Tile != pendingTile.Value) return;
                Type landform = AccessTools.TypeByName("GeologicalLandforms.GraphEditor.Landform");
                var forms = landform == null ? null : AccessTools.Property(landform, "GeneratingLandforms")?.GetValue(null) as IEnumerable;
                generatedLandforms = forms?.Cast<object>().Select(f =>
                    AccessTools.Property(f.GetType(), "Id")?.GetValue(f) as string).Where(id => id != null).ToArray();
                P0Log.Message("GL_TOUR_CONTEXT", "form=" + Forms[current] + " tile=" + map.Tile +
                    " map=" + map.uniqueID + " landforms=" +
                    (generatedLandforms == null ? "unavailable" : string.Join(",", generatedLandforms)));
            }
        }

        [DebugAction("Realistic Bases tour", "Log Geological Landforms tour inventory", allowedGameStates = AllowedGameStates.Playing)]
        public static void Inventory()
        {
            RequireDisposable();
            foreach (string form in Forms)
                P0Log.Message("GL_TOUR_INVENTORY", "form=" + form + " candidates=" + Candidates(form).Count);
        }

        [DebugAction("Realistic Bases tour", "Generate next Geological Landforms tour example", allowedGameStates = AllowedGameStates.Playing)]
        public static void Next()
        {
            RequireDisposable();
            if (++current >= Forms.Length)
            {
                P0Log.Message("GL_TOUR_FINISHED", "definitions=" + Forms.Length);
                return;
            }
            Generate();
        }

        [DebugAction("Realistic Bases tour", "Retry current Geological Landforms tour example", allowedGameStates = AllowedGameStates.Playing)]
        public static void Retry()
        {
            RequireDisposable();
            if (current < 0 || current >= Forms.Length) throw new InvalidOperationException("No current GL tour form");
            Generate();
        }

        [DebugAction("Realistic Bases tour", "Resume GL tour after recorded Caldera case", allowedGameStates = AllowedGameStates.Playing)]
        public static void ResumeAfterRecordedCases()
        {
            RequireDisposable();
            current = Array.IndexOf(Forms, "Caldera");
            P0Log.Message("GL_TOUR_CURSOR", "last=Caldera next=" + Forms[current + 1]);
        }

        [DebugAction("Realistic Bases tour", "Skip known Cliff timeout and continue tour", allowedGameStates = AllowedGameStates.Playing)]
        public static void SkipKnownCliffTimeout()
        {
            RequireDisposable();
            current = Array.IndexOf(Forms, "Cliff");
            P0Log.Message("GL_TOUR_CURSOR", "skipped=Cliff knownTimeout=true next=" + Forms[current + 1]);
        }

        [DebugAction("Realistic Bases tour", "Recheck recorded GL Atoll tile", allowedGameStates = AllowedGameStates.Playing)]
        private static void RecordedAtoll() => RecheckRecordedTile("Atoll", 68248);

        [DebugAction("Realistic Bases tour", "Recheck recorded GL Badlands tile", allowedGameStates = AllowedGameStates.Playing)]
        private static void RecordedBadlands() => RecheckRecordedTile("Badlands", 109873);

        [DebugAction("Realistic Bases tour", "Recheck recorded GL Caldera tile", allowedGameStates = AllowedGameStates.Playing)]
        private static void RecordedCaldera() => RecheckRecordedTile("Caldera", 89176);

        [DebugAction("Realistic Bases tour", "Recheck recorded GL Coast tile", allowedGameStates = AllowedGameStates.Playing)]
        private static void RecordedCoast() => RecheckRecordedTile("Coast", 55094);

        [DebugAction("Realistic Bases tour", "Recheck recorded GL CoastCorner tile", allowedGameStates = AllowedGameStates.Playing)]
        private static void RecordedCoastCorner() => RecheckRecordedTile("CoastCorner", 71102);

        [DebugAction("Realistic Bases tour", "Recheck recorded GL CoastalIsland tile", allowedGameStates = AllowedGameStates.Playing)]
        private static void RecordedCoastalIsland() => RecheckRecordedTile("CoastalIsland", 92492);

        [DebugAction("Realistic Bases tour", "Recheck recorded GL CoveWithIsland candidate tile", allowedGameStates = AllowedGameStates.Playing)]
        private static void RecordedCoveCandidate() => RecheckRecordedTile("CoveWithIsland", 50492);

        [DebugAction("Realistic Bases tour", "Recheck recorded GL DesertPlateau tile", allowedGameStates = AllowedGameStates.Playing)]
        private static void RecordedDesertPlateau() => RecheckRecordedTile("DesertPlateau", 63300);

        [DebugAction("Realistic Bases tour", "Recheck recorded GL RiverSource tile", allowedGameStates = AllowedGameStates.Playing)]
        private static void RecordedRiverSource() => RecheckRecordedTile("RiverSource", 67416);

        private static void RecheckRecordedTile(string form, int tileId)
        {
            RequireDisposable();
            if (Find.World.info.seedString != "narwhal")
                throw new InvalidOperationException("Recorded GL tour tiles require the narwhal disposable world");
            current = Array.IndexOf(Forms, form);
            Generate(new PlanetTile(tileId));
        }

        private static void RequireDisposable()
        {
            if (!P4DQuestDebugActions.Disposable) throw new InvalidOperationException("Disposable profile only");
        }

        private static List<PlanetTile> Candidates(string form)
        {
            var surface = Find.WorldGrid.Surface;
            var result = new List<PlanetTile>();
            for (int i = 0; i < surface.TilesCount; i++)
            {
                Tile tile = surface[i];
                if (tile.hilliness == Hilliness.Impassable || !tile.PrimaryBiome.canBuildBase ||
                    !tile.PrimaryBiome.canAutoChoose || !tile.Mutators.Any(m => m.defName == "GL_" + form) ||
                    !TileFinder.IsValidTileForNewSettlement(tile.tile)) continue;
                result.Add(tile.tile);
            }
            return result;
        }

        private static void Generate(PlanetTile? recordedTile = null)
        {
            string form = Forms[current];
            var candidates = Candidates(form);
            if (candidates.Count == 0)
            {
                P0Log.Message("GL_TOUR_UNAVAILABLE", "form=" + form + " reason=no_eligible_world_tile");
                return;
            }
            var temperate = candidates.Where(t => Find.WorldGrid[t].PrimaryBiome == BiomeDefOf.TemperateForest).ToList();
            PlanetTile chosen = (temperate.Count > 0 ? temperate : candidates).RandomElement();
            if (recordedTile.HasValue)
            {
                if (!candidates.Contains(recordedTile.Value))
                    throw new InvalidOperationException("Recorded GL tour tile is no longer an eligible candidate");
                chosen = recordedTile.Value;
            }
            P0Log.Message("GL_TOUR_SELECTED", "form=" + form + " tile=" + chosen + " candidates=" + candidates.Count);
            RealisticBasesMod.Settings.forceP2ProfileNext = "industrial";
            P4AMapPlanner.DebugStageNext = SettlementTechStageLevel.IndustrialLate;
            pendingTile = chosen;
            generatedLandforms = null;
            try
            {
                // The tour can generate many isolated hostile maps; moving a
                // home colonist on every case exhausts the home-map pawns and
                // prevents later forms from reaching map/planner generation.
                P0DebugActions.GenerateIsolatedHostileSettlement(false, pickTile: () => chosen, mapSide: 200);
                bool applied = generatedLandforms != null && generatedLandforms.Contains(form, StringComparer.Ordinal);
                P0Log.Message(applied ? "GL_TOUR_CONFIRMED" : "GL_TOUR_UNCONFIRMED",
                    "form=" + form + " tile=" + chosen + " requestedFormApplied=" + applied +
                    " reason=" + (applied ? "generated_map_context" : generatedLandforms == null
                        ? "missing_generator_context" : "selected_form_absent_from_generator_context"));
            }
            finally { pendingTile = null; generatedLandforms = null; }
            Find.TickManager.Pause();
        }
    }
}
