using System.Collections.Generic;
using System.Linq;
using RealisticBases.Core;
using RealisticBases.Planning;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealisticBases.Persistence
{
    public sealed class P0SettlementState : IExposable
    {
        public int settlementId = -1;
        public int generationCount;
        public int woodRemaining = -1;
        public bool markerWallDestroyed;
        public bool defeated;
        public string factionDefName;
        public List<int> pawnIds = new List<int>();
        public string proceduralLayoutSeed;
        public string proceduralLayoutPlanner;
        public string proceduralLayoutDigest;

        public void ExposeData()
        {
            Scribe_Values.Look(ref settlementId, "settlementId", -1);
            Scribe_Values.Look(ref generationCount, "generationCount", 0);
            Scribe_Values.Look(ref woodRemaining, "woodRemaining", -1);
            Scribe_Values.Look(ref markerWallDestroyed, "markerWallDestroyed", false);
            Scribe_Values.Look(ref defeated, "defeated", false);
            Scribe_Values.Look(ref factionDefName, "factionDefName");
            Scribe_Collections.Look(ref pawnIds, "pawnIds", LookMode.Value);
            Scribe_Values.Look(ref proceduralLayoutSeed, "proceduralLayoutSeed");
            Scribe_Values.Look(ref proceduralLayoutPlanner, "proceduralLayoutPlanner");
            Scribe_Values.Look(ref proceduralLayoutDigest, "proceduralLayoutDigest");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && pawnIds == null)
            {
                pawnIds = new List<int>();
            }
        }
    }

    public sealed class P4AFactionStageState : IExposable
    {
        public int factionLoadId = -1;
        public int level = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref factionLoadId, "factionLoadId", -1);
            Scribe_Values.Look(ref level, "stageLevel", -1);
        }
    }

    public sealed class P0WorldState : GameComponent
    {
        private List<P0SettlementState> settlements = new List<P0SettlementState>();
        private List<P4AFactionStageState> factionStages = new List<P4AFactionStageState>();

        public P0WorldState(Game game)
        {
        }

        public P0SettlementState GetOrCreate(int settlementId, Faction faction)
        {
            P0SettlementState state = settlements.FirstOrDefault(s => s.settlementId == settlementId);
            if (state == null)
            {
                state = new P0SettlementState
                {
                    settlementId = settlementId,
                    factionDefName = faction?.def?.defName
                };
                settlements.Add(state);
            }
            return state;
        }

        public ulong GetOrCreateProceduralSeed(Settlement settlement, string worldSeed, string profileId, int profileVersion)
        {
            P0SettlementState state = GetOrCreate(settlement.ID, settlement.Faction);
            ulong seed;
            if (!string.IsNullOrEmpty(state.proceduralLayoutSeed) &&
                ulong.TryParse(state.proceduralLayoutSeed, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out seed)) return seed;
            seed = LayoutSeed.Derive(worldSeed, settlement.Tile.tileId, settlement.Faction.loadID.ToString(), profileId, profileVersion);
            state.proceduralLayoutSeed = seed.ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
            state.proceduralLayoutPlanner = ProceduralLayoutVersions.Planner;
            return seed;
        }

        public bool TryGetOrCreateFactionStage(Faction faction, string worldSeed, out SettlementTechStage stage, out string diagnostic)
        {
            stage = null;
            diagnostic = null;
            if (faction == null || faction.def == null) { diagnostic = "missing_faction"; return false; }
            P4AFactionStageState saved = factionStages.FirstOrDefault(s => s.factionLoadId == faction.loadID);
            if (saved != null && saved.level >= (int)SettlementTechStageLevel.Neolithic &&
                saved.level <= (int)SettlementTechStageLevel.Spacer)
            {
                stage = TechStageModel.Create((SettlementTechStageLevel)saved.level);
                return true;
            }
            TechTier tier;
            if (!System.Enum.TryParse(faction.def.techLevel.ToString(), out tier))
            {
                diagnostic = "unsupported_faction_tech_level:" + faction.def.techLevel;
                return false;
            }
            if (!TechStageModel.TryResolve(tier, LayoutSeed.DeriveFaction(worldSeed, faction.loadID.ToString()),
                out stage, out diagnostic)) return false;
            factionStages.Add(new P4AFactionStageState { factionLoadId = faction.loadID, level = (int)stage.Level });
            return true;
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref settlements, "p0SettlementStates", LookMode.Deep);
            Scribe_Collections.Look(ref factionStages, "p4aFactionStages", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && settlements == null)
            {
                settlements = new List<P0SettlementState>();
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit && factionStages == null)
                factionStages = new List<P4AFactionStageState>();
        }

        public override void LoadedGame()
        {
            P0Log.Message("GAME_LOADED", "worldStates=" + settlements.Count);
        }
    }

    public sealed class P0MapState : MapComponent
    {
        private bool active;
        private int settlementId = -1;
        private int originalFactionLoadId = -1;
        private IntVec3 origin = IntVec3.Invalid;
        private IntVec3 stockCell = IntVec3.Invalid;
        private IntVec3 markerWallCell = IntVec3.Invalid;
        private string terrainAtSettlement;
        private int lastObservedWood = -1;
        private bool completionReported;

        public P0MapState(Map map) : base(map)
        {
        }

        public bool Active => active;

        public IntVec3 ArrivalTarget => stockCell;

        public void Initialize(Settlement settlement, IntVec3 layoutOrigin, IntVec3 woodCell, IntVec3 wallCell, string terrainChecksum)
        {
            active = true;
            settlementId = settlement.ID;
            originalFactionLoadId = settlement.Faction?.loadID ?? -1;
            origin = layoutOrigin;
            stockCell = woodCell;
            markerWallCell = wallCell;
            terrainAtSettlement = terrainChecksum;
            P0Log.Message("MAP_STATE_INIT", "map=" + map.uniqueID + " settlement=" + settlementId +
                " origin=" + origin + " stock=" + stockCell + " markerWall=" + markerWallCell);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref active, "p0Active", false);
            Scribe_Values.Look(ref settlementId, "p0SettlementId", -1);
            Scribe_Values.Look(ref originalFactionLoadId, "p0OriginalFactionLoadId", -1);
            Scribe_Values.Look(ref origin, "p0Origin", IntVec3.Invalid);
            Scribe_Values.Look(ref stockCell, "p0StockCell", IntVec3.Invalid);
            Scribe_Values.Look(ref markerWallCell, "p0MarkerWallCell", IntVec3.Invalid);
            Scribe_Values.Look(ref terrainAtSettlement, "p0TerrainAtSettlement");
            Scribe_Values.Look(ref lastObservedWood, "p0LastObservedWood", -1);
            Scribe_Values.Look(ref completionReported, "p0CompletionReported", false);
        }

        public override void FinalizeInit()
        {
            if (active)
            {
                P0Log.Message("MAP_STATE_FINALIZE", "map=" + map.uniqueID + " settlement=" + settlementId +
                    " wood=" + CountWood() + " markerWallPresent=" + MarkerWallPresent());
            }
        }

        public override void MapGenerated()
        {
            if (!active || map.generatorDef != Core.RBDefOf.RB_P0_Base_Faction)
            {
                return;
            }

            string finalChecksum = Generation.P0MapAudit.TerrainRoofChecksum(map);
            bool terrainUnchanged = terrainAtSettlement == finalChecksum;
            bool noProhibited = !map.listerThings.AllThings.Any(Generation.P0MapAudit.IsProhibitedDefense);
            bool edgeReachable = stockCell.IsValid && map.reachability.CanReachMapEdge(stockCell, TraverseParms.For(TraverseMode.PassDoors));
            P0Log.Message("FINAL_VALIDATE", "map=" + map.uniqueID + " terrainAtSettlement=" + terrainAtSettlement +
                " finalTerrain=" + finalChecksum + " terrainUnchangedAfterSettlement=" + terrainUnchanged +
                " noTrapsOrIEDs=" + noProhibited + " edgeReachablePassDoors=" + edgeReachable +
                " mutators=" + string.Join(",", map.TileInfo.Mutators.Select(m => m.defName).ToArray()));
        }

        public override void MapComponentTick()
        {
            if (!active)
            {
                return;
            }

            if ((Find.TickManager.TicksGame + map.uniqueID) % 250 == 0)
            {
                int wood = CountWood();
                if (wood != lastObservedWood)
                {
                    P0Log.Message("RESOURCE_DELTA", "map=" + map.uniqueID + " settlement=" + settlementId +
                        " woodBefore=" + lastObservedWood + " woodNow=" + wood);
                    lastObservedWood = wood;
                }

                if (!completionReported)
                {
                    int meals = map.listerThings.ThingsOfDef(ThingDefOf.MealSimple).Sum(t => t.stackCount);
                    int ricePlants = map.listerThings.AllThings.Count(t => t.def.defName == "Plant_Rice" && t.Faction == null);
                    if (meals > 0 && ricePlants > 0)
                    {
                        completionReported = true;
                        P0Log.Message("WORK_SEQUENCE_OBSERVED", "map=" + map.uniqueID +
                            " meals=" + meals + " ricePlants=" + ricePlants + " wood=" + wood);
                    }
                }
            }

            if (map.Parent == null || map.Parent.ID != settlementId ||
                (map.Parent is Settlement settlement && settlement.Faction?.loadID != originalFactionLoadId))
            {
                P0SettlementState state = Current.Game.GetComponent<P0WorldState>()?.GetOrCreate(settlementId, null);
                if (state != null && !state.defeated)
                {
                    state.defeated = true;
                    P0Log.Message("SETTLEMENT_OWNERSHIP_ENDED", "map=" + map.uniqueID + " settlement=" + settlementId +
                        " parent=" + map.Parent?.GetType().Name);
                }
                active = false;
            }
        }

        public override void MapRemoved()
        {
            if (!active)
            {
                return;
            }

            P0SettlementState state = Current.Game.GetComponent<P0WorldState>()?.GetOrCreate(settlementId, map.ParentFaction);
            if (state == null)
            {
                P0Log.Error("MAP_REMOVED", "missing P0WorldState for settlement=" + settlementId);
                return;
            }

            state.woodRemaining = CountWood();
            state.markerWallDestroyed = !MarkerWallPresent();
            List<int> survivingPawnIds = map.mapPawns.SpawnedPawnsInFaction(map.ParentFaction)
                .Where(p => p.RaceProps.Humanlike && !p.Dead)
                .Select(p => p.thingIDNumber)
                .OrderBy(id => id)
                .ToList();
            if (survivingPawnIds.Count > 0)
            {
                state.pawnIds = survivingPawnIds;
            }
            P0Log.Message("MAP_REMOVED", "map=" + map.uniqueID + " settlement=" + settlementId +
                " persistedWood=" + state.woodRemaining + " persistedMarkerWallDestroyed=" + state.markerWallDestroyed +
                " persistedPawnIds=" + string.Join(",", state.pawnIds.Select(id => id.ToString()).ToArray()));
        }

        private int CountWood()
        {
            if (!stockCell.IsValid || !stockCell.InBounds(map))
            {
                return 0;
            }
            return stockCell.GetThingList(map).Where(t => t.def == ThingDefOf.WoodLog).Sum(t => t.stackCount);
        }

        private bool MarkerWallPresent()
        {
            return markerWallCell.IsValid && markerWallCell.InBounds(map) &&
                markerWallCell.GetThingList(map).Any(t => t.def == ThingDefOf.Wall);
        }
    }
}
