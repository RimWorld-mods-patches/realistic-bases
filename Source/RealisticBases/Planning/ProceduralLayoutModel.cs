using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    // P4A foundation prototype (pure, engine independent). Nothing in the live
    // generator references these types yet; see docs/p4a-foundations.md.
    public static class ProceduralLayoutVersions
    {
        public const string Planner = "p4e-facilities.4-defense-allowance";
        public const int Schema = 1;
    }

    public enum HousingStandard
    {
        Barracks,
        PersonalRooms
    }

    public enum LayoutRoomKind
    {
        Barracks,
        Bedroom,
        Jail,
        Medical,
        Kitchen,
        Freezer,
        Pantry,
        Dining,
        Workshop,
        Storage,
        Power,
        Laboratory, // Append: persisted room values must not move.
        MortarMagazine
    }

    public static class SettlementTemperatureTargets
    {
        public const float FreezerC = -10f;
        public const float OccupiedRoomC = 21f;

        public static float ForRoom(LayoutRoomKind kind) =>
            kind == LayoutRoomKind.Freezer ? FreezerC : OccupiedRoomC;
    }

    public enum LayoutFixtureKind
    {
        Bed,
        PrisonBed,
        MedicalBed,
        Stove,
        Workbench,
        Table,
        Chair,
        Recreation,
        Stockpile,
        Heater,
        Campfire,
        WallCooler,
        PassiveCooler,
        Lamp,
        Torch,
        Generator,
        Battery,
        MortarCourt,
        MedicalMonitor,
        Shelf,
        ResearchAnalyzer,
        // A vanilla vent in a power-room wall: fueled generators heat the room,
        // so it exchanges air with outdoor space like a cooler's exhaust side.
        WallVent
    }

    public static class LayoutFixtureKinds
    {
        // Mounted in a wall cell with the room on one side and outdoor air on the other.
        public static bool ThroughWall(LayoutFixtureKind kind) =>
            kind == LayoutFixtureKind.WallCooler || kind == LayoutFixtureKind.WallVent;
    }

    public enum LayoutDoctrine
    {
        OpenGround,
        RockProtected
    }

    // Room/circulation generator behind the shared ProceduralLayout contract,
    // injected through ProceduralLayoutInput. ClustersLayoutEngine is the
    // default. Id enters the input digest, so it must stay stable.
    public interface ILayoutEngine
    {
        string Id { get; }
        ProceduralLayoutResult Plan(ProceduralLayoutInput input, bool requireJointSite);
    }

    // Outdoor temperature range of the tile. The snapshot carries no climate,
    // so it is an explicit input. Thresholds are provisional planning values.
    public sealed class LayoutClimate
    {
        public const int HeatingBelowC = 10;
        public const int CoolingAboveC = 30;
        // RoomTempTracker equalizes thick-roofed rooms toward 15C.
        public const int ThickRoofEquilibriumC = 15;

        public LayoutClimate(int minOutdoorC, int maxOutdoorC)
        {
            if (maxOutdoorC < minOutdoorC) throw new ArgumentOutOfRangeException(nameof(maxOutdoorC));
            MinOutdoorC = minOutdoorC;
            MaxOutdoorC = maxOutdoorC;
        }

        public int MinOutdoorC { get; }
        public int MaxOutdoorC { get; }
        public bool HeatingRequired => MinOutdoorC < HeatingBelowC;
        public bool CoolingRequired => MaxOutdoorC > CoolingAboveC;
        public override string ToString() => MinOutdoorC + ".." + MaxOutdoorC + "C";
    }

    public static class LayoutSeed
    {
        // World seed, tile, faction and profile identity plus the planner version.
        // No compound dimensions participate. The integration must persist the
        // derived value with the settlement so revisits never reroll.
        public static ulong Derive(string worldSeed, int tile, string factionId, string profileId, int profileVersion)
        {
            ulong hash = StableHash.Start;
            hash = StableHash.Add(hash, "p4a-layout-seed");
            hash = StableHash.Add(hash, ProceduralLayoutVersions.Planner);
            hash = StableHash.Add(hash, worldSeed ?? string.Empty);
            hash = StableHash.Add(hash, tile);
            hash = StableHash.Add(hash, factionId ?? string.Empty);
            hash = StableHash.Add(hash, profileId ?? string.Empty);
            return StableHash.Add(hash, profileVersion);
        }

        // Faction-wide seed: world and faction only, so every settlement of the
        // faction shares it. The planner version is deliberately excluded, so a
        // planner bump never re-rolls faction-wide choices such as the tech stage.
        // Integration must still persist the resolved stage per faction.
        public static ulong DeriveFaction(string worldSeed, string factionId)
        {
            ulong hash = StableHash.Start;
            hash = StableHash.Add(hash, "p4a-faction-seed");
            hash = StableHash.Add(hash, worldSeed ?? string.Empty);
            return StableHash.Add(hash, factionId ?? string.Empty);
        }

        // Independent purpose streams, so a rule change in one does not reshuffle the others.
        public static ulong Sub(ulong seed, string purpose) => StableHash.Add(StableHash.Add(StableHash.Start, seed), purpose ?? string.Empty);

        internal static ulong Key(ulong seed, int a, int b = 0, int c = 0)
        {
            ulong hash = StableHash.Add(seed, a);
            hash = StableHash.Add(hash, b);
            return StableHash.Add(hash, c);
        }

        internal static int Roll(ulong seed, string purpose, int modulo) =>
            modulo <= 0 ? 0 : (int)(Sub(seed, purpose) % (ulong)modulo);
    }

    public sealed class LayoutRequirements
    {
        // Preferred stacks per warehouse/freezer room. Racked shelves hold
        // three stacks per cell, so ~120 stacks is one well-shelved ~8x8 room.
        public const int StockpileRoomStacksTarget = 120;
        // Provisional import limits for FromPopulation, equal to the P2 temperate profiles.
        public const int DefaultImportValueLimitElectric = 5500;
        public const int DefaultImportValueLimitNonElectric = 1400;

        public LayoutRequirements(int residents, SettlementCapability capabilities, HousingStandard housing,
            LayoutClimate climate, int mortarBiasPercent, int excavationBudgetCells, int fieldCells,
            IEnumerable<KeyValuePair<LayoutRoomKind, int>> capacityFloors = null, SettlementTechStage stage = null, int importValueLimit = -1,
            int equipmentValueLimit = -1, int stockpileRoomCapacityTarget = 0, bool compactQuestCamp = false,
            int defenseReserveWatts = 0, int logisticsUtilityValueLimit = 0, int saleStorageStacks = 0, int salePerishableStacks = 0,
            int retainedStorageStacks = 0, int retainedPerishableStacks = 0, int questArtillery = 0,
            int defenseImportReserveValue = 0, int baseImportValueLimit = -1)
        {
            if (saleStorageStacks < 0 || salePerishableStacks < 0 || retainedStorageStacks < 0 || retainedPerishableStacks < 0 ||
                compactQuestCamp && (saleStorageStacks + salePerishableStacks + retainedStorageStacks + retainedPerishableStacks > 0))
                throw new ArgumentOutOfRangeException(nameof(saleStorageStacks));
            SaleStorageStacks = saleStorageStacks;
            SalePerishableStacks = salePerishableStacks;
            RetainedStorageStacks = retainedStorageStacks;
            RetainedPerishableStacks = retainedPerishableStacks;
            if (residents <= 0) throw new ArgumentOutOfRangeException(nameof(residents));
            if (mortarBiasPercent < 0 || mortarBiasPercent > 100) throw new ArgumentOutOfRangeException(nameof(mortarBiasPercent));
            if (excavationBudgetCells < 0) throw new ArgumentOutOfRangeException(nameof(excavationBudgetCells));
            if (fieldCells < 0) throw new ArgumentOutOfRangeException(nameof(fieldCells));
            if (stockpileRoomCapacityTarget < 0) throw new ArgumentOutOfRangeException(nameof(stockpileRoomCapacityTarget));
            Residents = residents;
            CompactQuestCamp = compactQuestCamp;
            if (defenseReserveWatts < 0) throw new ArgumentOutOfRangeException(nameof(defenseReserveWatts));
            DefenseReserveWatts = defenseReserveWatts;
            if (defenseImportReserveValue < 0) throw new ArgumentOutOfRangeException(nameof(defenseImportReserveValue));
            DefenseImportReserveValue = defenseImportReserveValue;
            if (questArtillery < 0 || questArtillery > 0 && !compactQuestCamp) throw new ArgumentOutOfRangeException(nameof(questArtillery));
            QuestArtillery = questArtillery;
            if (logisticsUtilityValueLimit < 0 || compactQuestCamp && logisticsUtilityValueLimit != 0)
                throw new ArgumentOutOfRangeException(nameof(logisticsUtilityValueLimit));
            LogisticsUtilityValueLimit = logisticsUtilityValueLimit;
            Capabilities = capabilities;
            Housing = housing;
            Climate = climate ?? throw new ArgumentNullException(nameof(climate));
            MortarBiasPercent = mortarBiasPercent;
            ExcavationBudgetCells = excavationBudgetCells;
            FieldCells = fieldCells;
            Dictionary<LayoutRoomKind, int> floors = new Dictionary<LayoutRoomKind, int>();
            foreach (KeyValuePair<LayoutRoomKind, int> floor in capacityFloors ?? Enumerable.Empty<KeyValuePair<LayoutRoomKind, int>>())
            {
                if (floor.Value <= 0) throw new ArgumentOutOfRangeException(nameof(capacityFloors));
                int existing;
                floors[floor.Key] = floors.TryGetValue(floor.Key, out existing) ? Math.Max(existing, floor.Value) : floor.Value;
            }
            CapacityFloors = new ReadOnlyDictionary<LayoutRoomKind, int>(floors);
            Stage = stage ?? DefaultStage(capabilities);
            ImportValueLimit = importValueLimit >= 0 ? importValueLimit : DefaultImportValueLimit(capabilities);
            BaseImportValueLimit = baseImportValueLimit >= 0 ? baseImportValueLimit :
                Math.Max(0, ImportValueLimit - DefenseImportReserveValue);
            if (BaseImportValueLimit > ImportValueLimit) throw new ArgumentOutOfRangeException(nameof(baseImportValueLimit));
            EquipmentValueLimit = equipmentValueLimit >= 0 ? equipmentValueLimit : DefaultEquipmentValueLimit(residents, Stage, climate);
            StockpileRoomCapacityTarget = stockpileRoomCapacityTarget;
            Digest = ComputeDigest();
        }

        // Capability-derived default: Electricity -> IndustrialEarly, otherwise Neolithic.
        public static SettlementTechStage DefaultStage(SettlementCapability capabilities) =>
            TechStageModel.Create((capabilities & SettlementCapability.Electricity) != 0
                ? SettlementTechStageLevel.IndustrialEarly : SettlementTechStageLevel.Neolithic);

        // Stage default per resident, raised for climates that need heating (LayoutLoadoutModel constants).
        public static int DefaultEquipmentValueLimit(int residents, SettlementTechStage stage, LayoutClimate climate)
        {
            double perResident = LayoutLoadoutModel.DefaultEquipmentValuePerResident(stage.Level) *
                (climate != null && climate.HeatingRequired ? LayoutLoadoutModel.ColdClimateBudgetFactor : 1.0);
            return (int)Math.Round(residents * perResident, MidpointRounding.AwayFromZero);
        }

        public static int DefaultImportValueLimit(SettlementCapability capabilities) =>
            (capabilities & SettlementCapability.Electricity) != 0 ? DefaultImportValueLimitElectric : DefaultImportValueLimitNonElectric;

        public int Residents { get; }
        public bool CompactQuestCamp { get; }
        public int DefenseReserveWatts { get; }
        public int DefenseImportReserveValue { get; }
        // A quest camp's advertised mortars: exactly this many courts, crewed
        // by the quest's own roster.
        public int QuestArtillery { get; }
        public int LogisticsUtilityValueLimit { get; }
        public SettlementCapability Capabilities { get; }
        public HousingStandard Housing { get; }
        public LayoutClimate Climate { get; }
        public int MortarBiasPercent { get; }
        public int ExcavationBudgetCells { get; }
        public int FieldCells { get; }
        public IReadOnlyDictionary<LayoutRoomKind, int> CapacityFloors { get; }
        // Requested tech stage (before step-downs and adaptations).
        public SettlementTechStage Stage { get; }
        // Wood imports beyond this market value reject with material_budget_exceeded.
        public int ImportValueLimit { get; }
        // Material planning cannot consume the portion reserved for optional turret batteries.
        public int BaseImportValueLimit { get; }
        // Resident gear, armoury and medicine/component stocks beyond this market value degrade, then reject.
        public int EquipmentValueLimit { get; }
        // Zero retains the historical split policy; positive values prefer
        // smaller connected stockpiles without discarding any capacity.
        public int StockpileRoomCapacityTarget { get; }
        // P5: the settlement's published trade offer, stored physically in
        // addition to operating reserves (one stack per cell, conservatively).
        public int SaleStorageStacks { get; }
        public int SalePerishableStacks { get; }
        public int RetainedStorageStacks { get; }
        public int RetainedPerishableStacks { get; }
        // All new ordinary bases need an attached-field connection reserved
        // before the larger P4E facility set is packed, including four residents.
        public bool ReservePopulationCirculation => !CompactQuestCamp || Residents > 4 || StockpileRoomCapacityTarget > 0;
        public string Digest { get; }
        public bool Electric => Has(SettlementCapability.Electricity);
        public bool Refrigerated => Has(SettlementCapability.Refrigeration);
        // Capability wish only; the layout uses LayoutKit.MortarCapable (Electric and Turret_Mortar legal).
        public bool MortarCapable => Electric;
        public bool Has(SettlementCapability capability) => (Capabilities & capability) == capability;

        public int Floor(LayoutRoomKind kind)
        {
            int value;
            return CapacityFloors.TryGetValue(kind, out value) ? value : 0;
        }

        public static LayoutRequirements FromPopulation(int residents, SettlementCapability capabilities, LayoutClimate climate,
            HousingStandard housing = HousingStandard.Barracks, IEnumerable<KeyValuePair<LayoutRoomKind, int>> floors = null,
            SettlementTechStage stage = null, int importValueLimit = -1, int equipmentValueLimit = -1)
        {
            bool electric = (capabilities & SettlementCapability.Electricity) != 0;
            return new LayoutRequirements(residents, capabilities, housing, climate,
                electric ? 50 : 0, 60 + residents * (electric ? 70 : 35), residents * 31, floors, stage, importValueLimit, equipmentValueLimit);
        }

        // P2 profile capacities act as floors; population rules may raise them.
        public static LayoutRequirements FromProfile(PhysicalSettlementProfile profile, LayoutClimate climate,
            HousingStandard housing = HousingStandard.Barracks, SettlementTechStage stage = null,
            OrdinarySettlementBudget populationBudget = null, int? mortarBiasPercent = null,
            int saleStorageStacks = 0, int salePerishableStacks = 0, int retainedStorageStacks = 0, int retainedPerishableStacks = 0)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            List<KeyValuePair<LayoutRoomKind, int>> floors = new List<KeyValuePair<LayoutRoomKind, int>>();
            foreach (FacilityDemand demand in profile.Facilities)
            {
                LayoutRoomKind kind;
                if (TryMap(demand.Kind, out kind)) floors.Add(new KeyValuePair<LayoutRoomKind, int>(kind, demand.Capacity));
            }
            bool electric = profile.Has(SettlementCapability.Electricity);
            SettlementTechStage resolved = stage ?? DefaultStage(profile.Capabilities);
            // P2 budgets were set for P2's fixed kits; a staged kit gets at least the stage default.
            int residents = populationBudget?.Residents ?? profile.Residents;
            int equipment = populationBudget?.EquipmentValue ??
                Math.Max(profile.Budget.EquipmentValue, DefaultEquipmentValueLimit(residents, resolved, climate));
            int fieldCells = (int)Math.Ceiling(residents * profile.DailyNutritionPerResident /
                profile.EffectiveNutritionPerCropCellPerDay);
            return new LayoutRequirements(residents, profile.Capabilities, housing, climate,
                mortarBiasPercent ?? (electric ? 50 : 0), 60 + residents * (electric ? 70 : 35), fieldCells, floors, resolved,
                populationBudget?.ImportValue ?? profile.Budget.ImportValueLimit, equipment,
                stockpileRoomCapacityTarget: populationBudget == null ? 0 : StockpileRoomStacksTarget,
                defenseReserveWatts: populationBudget?.DefenseReserveWatts ?? 0,
                logisticsUtilityValueLimit: populationBudget?.DefenseAllowancePercent > 100 ? 300 : 0,
                saleStorageStacks: saleStorageStacks, salePerishableStacks: salePerishableStacks,
                retainedStorageStacks: retainedStorageStacks, retainedPerishableStacks: retainedPerishableStacks,
                defenseImportReserveValue: populationBudget?.DefenseImportReserveValue ?? 0,
                baseImportValueLimit: populationBudget == null ? profile.Budget.ImportValueLimit :
                    populationBudget.ImportValue - populationBudget.DefenseImportReserveValue);
        }

        private static bool TryMap(FacilityKind kind, out LayoutRoomKind result)
        {
            switch (kind)
            {
                case FacilityKind.Jail: result = LayoutRoomKind.Jail; return true;
                case FacilityKind.Medical: result = LayoutRoomKind.Medical; return true;
                case FacilityKind.Kitchen: result = LayoutRoomKind.Kitchen; return true;
                case FacilityKind.Freezer: result = LayoutRoomKind.Freezer; return true;
                case FacilityKind.Pantry: result = LayoutRoomKind.Pantry; return true;
                case FacilityKind.DiningRecreation: result = LayoutRoomKind.Dining; return true;
                case FacilityKind.Workshop: result = LayoutRoomKind.Workshop; return true;
                case FacilityKind.BulkStorage: result = LayoutRoomKind.Storage; return true;
                default: result = LayoutRoomKind.Barracks; return false;
            }
        }

        private string ComputeDigest()
        {
            ulong hash = StableHash.Start;
            hash = StableHash.Add(hash, "p4a-requirements");
            hash = StableHash.Add(hash, Residents);
            hash = StableHash.Add(hash, (int)Capabilities);
            hash = StableHash.Add(hash, (int)Housing);
            hash = StableHash.Add(hash, Climate.MinOutdoorC);
            hash = StableHash.Add(hash, Climate.MaxOutdoorC);
            hash = StableHash.Add(hash, MortarBiasPercent);
            hash = StableHash.Add(hash, ExcavationBudgetCells);
            hash = StableHash.Add(hash, FieldCells);
            foreach (KeyValuePair<LayoutRoomKind, int> floor in CapacityFloors.OrderBy(f => f.Key))
            {
                hash = StableHash.Add(hash, (int)floor.Key);
                hash = StableHash.Add(hash, floor.Value);
            }
            hash = StableHash.Add(hash, Stage.Digest);
            hash = StableHash.Add(hash, ImportValueLimit);
            hash = StableHash.Add(hash, BaseImportValueLimit);
            if (DefenseImportReserveValue > 0) hash = StableHash.Add(hash, DefenseImportReserveValue);
            hash = StableHash.Add(hash, EquipmentValueLimit);
            if (DefenseReserveWatts > 0) hash = StableHash.Add(hash, DefenseReserveWatts);
            if (LogisticsUtilityValueLimit > 0) hash = StableHash.Add(hash, LogisticsUtilityValueLimit);
            if (StockpileRoomCapacityTarget > 0)
            {
                hash = StableHash.Add(hash, "stockpile-room-capacity-target");
                hash = StableHash.Add(hash, StockpileRoomCapacityTarget);
            }
            if (CompactQuestCamp) hash = StableHash.Add(hash, "compact-finite-quest-camp-v1");
            if (QuestArtillery > 0)
            {
                hash = StableHash.Add(hash, "quest-artillery");
                hash = StableHash.Add(hash, QuestArtillery);
            }
            if (SaleStorageStacks + SalePerishableStacks > 0)
            {
                hash = StableHash.Add(hash, "p5-sale-stock");
                hash = StableHash.Add(hash, SaleStorageStacks);
                hash = StableHash.Add(hash, SalePerishableStacks);
            }
            if (RetainedStorageStacks + RetainedPerishableStacks > 0)
            {
                hash = StableHash.Add(hash, "p5-retained-operating-stock");
                hash = StableHash.Add(hash, RetainedStorageStacks);
                hash = StableHash.Add(hash, RetainedPerishableStacks);
            }
            return StableHash.Hex(hash);
        }
    }

    // Space reserved for P4 fortification. P4A never builds these structures;
    // it keeps the terrain they need legal and free.
    public sealed class LayoutFortificationReservation
    {
        public LayoutFortificationReservation(int wallLayers = 1, int setback = 1, int outerClearance = 1,
            int vestibuleDepth = 2, int desiredGates = 2, int minimumGates = 1)
        {
            if (wallLayers < 1 || wallLayers > 3) throw new ArgumentOutOfRangeException(nameof(wallLayers));
            if (setback < 0 || setback > 2) throw new ArgumentOutOfRangeException(nameof(setback));
            if (outerClearance < 0 || outerClearance > 3) throw new ArgumentOutOfRangeException(nameof(outerClearance));
            if (vestibuleDepth < 1 || vestibuleDepth > 4) throw new ArgumentOutOfRangeException(nameof(vestibuleDepth));
            if (minimumGates < 1 || desiredGates < minimumGates || desiredGates > 4) throw new ArgumentOutOfRangeException(nameof(desiredGates));
            WallLayers = wallLayers;
            Setback = setback;
            OuterClearance = outerClearance;
            VestibuleDepth = vestibuleDepth;
            DesiredGates = desiredGates;
            MinimumGates = minimumGates;
        }

        public int WallLayers { get; }
        public int Setback { get; }
        public int OuterClearance { get; }
        public int VestibuleDepth { get; }
        public int DesiredGates { get; }
        public int MinimumGates { get; }
        public int PlacementMargin => Setback + 1 + OuterClearance + 2 * (WallLayers - 1);
    }

    public sealed class LayoutSearchBudget
    {
        public LayoutSearchBudget(int maxSites = 10, int attemptsPerSite = 3, int maxEvaluations = 400000,
            int maxCandidatesPerRoom = 3000, int maxRepairsPerAttempt = 8, int maxRouteNodes = 40000)
        {
            if (maxSites <= 0 || attemptsPerSite <= 0 || maxEvaluations <= 0 || maxCandidatesPerRoom <= 0 ||
                maxRepairsPerAttempt < 0 || maxRouteNodes <= 0) throw new ArgumentOutOfRangeException("Search budget values must be positive.");
            MaxSites = maxSites;
            AttemptsPerSite = attemptsPerSite;
            MaxEvaluations = maxEvaluations;
            MaxCandidatesPerRoom = maxCandidatesPerRoom;
            MaxRepairsPerAttempt = maxRepairsPerAttempt;
            MaxRouteNodes = maxRouteNodes;
        }

        public int MaxSites { get; }
        public int AttemptsPerSite { get; }
        public int MaxEvaluations { get; }
        public int MaxCandidatesPerRoom { get; }
        public int MaxRepairsPerAttempt { get; }
        public int MaxRouteNodes { get; }

        public static LayoutSearchBudget ForPopulation(int residents)
        {
            if (residents <= 0) throw new ArgumentOutOfRangeException(nameof(residents));
            if (residents > OrdinarySettlementPopulation.Maximum) return new LayoutSearchBudget();
            // More rooms/field interfaces need proportionate bounded work, not
            // a smaller roster. Explicit caller budgets still take precedence.
            int population = Math.Max(4, Math.Min(12, residents));
            return new LayoutSearchBudget(maxEvaluations: 25000 * population * population,
                maxRepairsPerAttempt: population * 2);
        }
    }

    public sealed class ProceduralLayoutInput
    {
        private LayoutEquipment equipment;

        public ProceduralLayoutInput(LayoutRequirements requirements, TerrainSnapshot terrain, ulong seed, TechCatalog catalog,
            LayoutFortificationReservation fortification = null, LayoutSearchBudget budget = null, LayoutLocalResources resources = null,
            P4DQuestContract questContract = null, ILayoutEngine engine = null, int plannerDegree = 1)
        {
            if (plannerDegree < 1 || plannerDegree > 8) throw new ArgumentOutOfRangeException(nameof(plannerDegree));
            Engine = engine ?? ClustersLayoutEngine.Instance;
            Requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
            QuestContract = questContract;
            if (requirements.CompactQuestCamp != (questContract != null))
                throw new ArgumentException("A compact quest camp requires its explicit native contract");
            Terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Seed = seed;
            PlannerDegree = plannerDegree;
            Fortification = fortification ?? new LayoutFortificationReservation();
            Budget = budget ?? LayoutSearchBudget.ForPopulation(requirements.Residents);
            Resources = resources ?? LayoutLocalResources.Default;
            Digest = ComputeDigest();
        }

        public ILayoutEngine Engine { get; }
        public LayoutRequirements Requirements { get; }
        public P4DQuestContract QuestContract { get; }
        public TerrainSnapshot Terrain { get; }
        public TechCatalog Catalog { get; }
        public ulong Seed { get; }
        // Degree is execution policy, not plan input: it must not alter the digest.
        public int PlannerDegree { get; }
        public static int AutomaticPlannerDegree => Math.Max(1, Math.Min(8, Environment.ProcessorCount - 1));
        public LayoutFortificationReservation Fortification { get; }
        public LayoutSearchBudget Budget { get; }
        public LayoutLocalResources Resources { get; }
        public string Digest { get; }
        // Deterministic from the other inputs; computed once and shared by planner and validator.
        public LayoutEquipment Equipment => equipment ?? (equipment = LayoutEquipment.Resolve(this));

        private string ComputeDigest()
        {
            ulong hash = StableHash.Start;
            hash = StableHash.Add(hash, ProceduralLayoutVersions.Planner);
            hash = StableHash.Add(hash, "engine:" + Engine.Id);
            hash = StableHash.Add(hash, Requirements.Digest);
            if (QuestContract != null) hash = StableHash.Add(hash, QuestContract.Digest);
            hash = StableHash.Add(hash, Terrain.Digest);
            hash = StableHash.Add(hash, Catalog.Digest);
            hash = StableHash.Add(hash, Resources.Digest);
            hash = StableHash.Add(hash, Seed);
            LayoutFortificationReservation f = Fortification;
            foreach (int value in new[] { f.WallLayers, f.Setback, f.OuterClearance, f.VestibuleDepth, f.DesiredGates, f.MinimumGates })
                hash = StableHash.Add(hash, value);
            LayoutSearchBudget b = Budget;
            foreach (int value in new[] { b.MaxSites, b.AttemptsPerSite, b.MaxEvaluations, b.MaxCandidatesPerRoom, b.MaxRepairsPerAttempt, b.MaxRouteNodes })
                hash = StableHash.Add(hash, value);
            return StableHash.Hex(hash);
        }
    }

    // One room to realize. Candidate sizes are interior dimensions; Width runs
    // along the door wall. Each candidate was proven by running the furnishing rule.
    public sealed class LayoutRoomDemand
    {
        internal LayoutRoomDemand(string id, LayoutRoomKind kind, int capacity, string unit, IList<GridRect> sizes, int batteries = 0)
        {
            Batteries = batteries;
            Id = id;
            Kind = kind;
            Capacity = capacity;
            Unit = unit;
            CandidateSizes = new ReadOnlyCollection<GridRect>(sizes);
        }

        public string Id { get; }
        public LayoutRoomKind Kind { get; }
        public int Capacity { get; }
        public string Unit { get; }
        public IReadOnlyList<GridRect> CandidateSizes { get; }
        // Power room only: batteries placed next to the generators.
        public int Batteries { get; }
        public bool Living => LayoutDemandModel.IsLiving(Kind);
        public int MinimumBoundsArea => CandidateSizes.Count == 0 ? 0 : CandidateSizes.Min(s => (s.Width + 2) * (s.Height + 2));
    }

    public enum LayoutRelationKind
    {
        Near,
        Far
    }

    public sealed class LayoutRelationship
    {
        public LayoutRelationship(string a, string b, LayoutRelationKind kind, int weight, bool hard, int maxDistance)
        {
            A = a;
            B = b;
            Kind = kind;
            Weight = weight;
            Hard = hard;
            MaxDistance = maxDistance;
        }

        public string A { get; }
        public string B { get; }
        public LayoutRelationKind Kind { get; }
        public int Weight { get; }
        // Hard relations are rejected when their public walking distance between
        // door outer cells exceeds MaxDistance; soft ones only shape the score.
        public bool Hard { get; }
        public int MaxDistance { get; }
        public override string ToString() => A + (Kind == LayoutRelationKind.Near ? "~" : "!~") + B + ":" + Weight + (Hard ? "/hard<=" + MaxDistance : string.Empty);
    }

    public sealed class LayoutDemandSet
    {
        internal LayoutDemandSet(IList<LayoutRoomDemand> rooms, IList<LayoutRelationship> relations, IList<string> rejections, int estimatedLoadWatts,
            int criticalWatts = 0, int powerGenerators = 0)
        {
            Rooms = new ReadOnlyCollection<LayoutRoomDemand>(rooms);
            Relationships = new ReadOnlyCollection<LayoutRelationship>(relations);
            Rejections = new ReadOnlyCollection<string>(rejections);
            EstimatedLoadWatts = estimatedLoadWatts;
            CriticalWatts = criticalWatts;
            PowerGenerators = powerGenerators;
        }

        // Worst-case (largest candidate) critical load of the non-power rooms.
        public int CriticalWatts { get; }
        // Dispatchable-only worst case: generators covering the whole load, used before the search.
        public int PowerGenerators { get; }

        public IReadOnlyList<LayoutRoomDemand> Rooms { get; }
        public IReadOnlyList<LayoutRelationship> Relationships { get; }
        public IReadOnlyList<string> Rejections { get; }
        public int EstimatedLoadWatts { get; }
        public int MinimumRoomArea => Rooms.Sum(r => r.MinimumBoundsArea);
    }

    public static class LayoutDemandModel
    {
        public const int MinInterior = 3;
        public const int MaxInterior = 12;
        public const int MaxPowerInteriorLength = 24;
        public const int MaxBarracksBeds = 8;
        public const int MaxSplit = 8;
        public const int MaxSizesPerRoom = 6;
        public const double NearMinimalAreaFactor = 1.35;
        public const int KitchenFoodHardDistance = 24;

        // A room-use policy shared by demand, packing, power and final validation.
        // "Living" includes occupied work spaces, not only sleeping/eating rooms.
        public static bool IsLiving(LayoutRoomKind kind) => kind == LayoutRoomKind.Barracks || kind == LayoutRoomKind.Bedroom ||
            kind == LayoutRoomKind.Jail || kind == LayoutRoomKind.Medical || kind == LayoutRoomKind.Dining ||
            kind == LayoutRoomKind.Kitchen || kind == LayoutRoomKind.Workshop || kind == LayoutRoomKind.Laboratory;

        // Rooms whose wall fixtures need outdoor air behind them: freezer and
        // cooled living-room coolers, and the vented generator room.
        public static bool NeedsOutdoorAir(LayoutRoomKind kind, LayoutRequirements requirements, LayoutKit kit) =>
            kind == LayoutRoomKind.Freezer || (kind == LayoutRoomKind.Power && kit.Vent != null) ||
            (requirements.Climate.CoolingRequired && IsLiving(kind));

        public static bool IsStockpile(LayoutRoomKind kind) => kind == LayoutRoomKind.Storage || kind == LayoutRoomKind.Freezer || kind == LayoutRoomKind.Pantry;

        public static LayoutDemandSet Derive(LayoutRequirements requirements, LayoutKit kit, ulong roomSeed, int extraStorageCells = 0)
        {
            if (extraStorageCells < 0) throw new ArgumentOutOfRangeException(nameof(extraStorageCells));
            if (requirements == null) throw new ArgumentNullException(nameof(requirements));
            if (kit == null) throw new ArgumentNullException(nameof(kit));
            int r = requirements.Residents;
            List<KeyValuePair<LayoutRoomKind, int>> wanted = new List<KeyValuePair<LayoutRoomKind, int>>();
            if (requirements.Housing == HousingStandard.PersonalRooms)
                for (int i = 0; i < r; i++) wanted.Add(new KeyValuePair<LayoutRoomKind, int>(LayoutRoomKind.Bedroom, 1));
            else
            {
                int barracks = (r + MaxBarracksBeds - 1) / MaxBarracksBeds;
                for (int i = 0; i < barracks; i++)
                    wanted.Add(new KeyValuePair<LayoutRoomKind, int>(LayoutRoomKind.Barracks, r / barracks + (i < r % barracks ? 1 : 0)));
            }
            if (!requirements.CompactQuestCamp || requirements.Floor(LayoutRoomKind.Jail) > 0)
                Add(wanted, requirements, LayoutRoomKind.Jail, requirements.CompactQuestCamp ? 1 : Math.Max(1, Ceil(r, 8)));
            Add(wanted, requirements, LayoutRoomKind.Medical, Math.Max(1, Ceil(r, 6)));
            Add(wanted, requirements, LayoutRoomKind.Kitchen, Ceil(r, 8));
            Add(wanted, requirements, kit.Refrigerated ? LayoutRoomKind.Freezer : LayoutRoomKind.Pantry,
                Math.Max(12, 6 * r) + requirements.SalePerishableStacks + requirements.RetainedPerishableStacks);
            Add(wanted, requirements, LayoutRoomKind.Dining, r);
            // Every distinct craft bench once, then more benches up to the population target.
            if (!requirements.CompactQuestCamp)
            {
                Add(wanted, requirements, LayoutRoomKind.Workshop, Math.Max(Math.Max(1, Ceil(r, 6)), kit.WorkshopDefs.Count));
                if (kit.DefFor(SettlementRole.Research) != null) Add(wanted, requirements, LayoutRoomKind.Laboratory, 1);
            }
            // Generator fuel stock is stored physically: its stacks raise the storage demand.
            Add(wanted, requirements, LayoutRoomKind.Storage, Math.Max(12, 6 * r) + extraStorageCells +
                requirements.SaleStorageStacks + requirements.RetainedStorageStacks);

            List<string> rejections = new List<string>();
            List<LayoutRoomDemand> rooms = new List<LayoutRoomDemand>();
            int load = 0;
            int critical = 0;
            int generatorsNeeded = 0;
            foreach (IGrouping<LayoutRoomKind, KeyValuePair<LayoutRoomKind, int>> group in wanted.GroupBy(w => w.Key))
            {
                List<LayoutRoomDemand> built = group.Key == LayoutRoomKind.Barracks || group.Key == LayoutRoomKind.Bedroom
                    ? group.Select((item, i) => Build(group.Key, item.Value, Id(group.Key, i + 1, true), requirements, kit, roomSeed)).ToList()
                    : Split(group.Key, group.Sum(item => item.Value), requirements, kit, roomSeed);
                if (built.Any(d => d == null) || built.Count == 0)
                {
                    rejections.Add("room_demand_exceeds_legal_dimensions:" + group.Key + ":" + group.Sum(item => item.Value));
                    continue;
                }
                rooms.AddRange(built);
                load += built.Sum(d => MaxLoad(d, requirements, kit));
                critical += built.Sum(d => MaxCritical(d, requirements, kit));
            }
            if (kit.Powered && kit.GeneratorWatts > 0 && rejections.Count == 0)
            {
                // Dispatchable-only worst case; the power room's own draw is part of the load it must cover.
                int reserve = requirements.DefenseReserveWatts;
                // Pack generator and battery space for the full defense allowance;
                // finalization trims both to the network that fits.
                int batteries = LayoutPowerModel.Batteries(load + reserve);
                // MaxSplit limits room splitting, not the number of generators
                // one legal power room can hold. Use a physical area bound;
                // furnishing still enforces batteries, working cells and aisles.
                int maximumGenerators = MaxInterior * MaxPowerInteriorLength /
                    Math.Max(1, kit.Generator.SizeX * kit.Generator.SizeZ);
                for (int generators = Math.Max(1, Ceil(load + reserve, kit.GeneratorWatts)); generators <= maximumGenerators; generators++)
                {
                    LayoutRoomDemand power = BuildPowerRoom(requirements, kit, generators, batteries, roomSeed);
                    if (power == null) { rejections.Add("room_demand_exceeds_legal_dimensions:Power:" + generators); break; }
                    int total = load + reserve + MaxLoad(power, requirements, kit);
                    int bridgeLoad = load + reserve + MaxLoad(power, requirements, kit);
                    if (LayoutPowerModel.Batteries(bridgeLoad) > batteries)
                    {
                        // Pack the full bridge, then trim it to the selected network.
                        batteries = LayoutPowerModel.Batteries(bridgeLoad);
                        power = BuildPowerRoom(requirements, kit, generators, batteries, roomSeed);
                        if (power == null) { rejections.Add("room_demand_exceeds_legal_dimensions:Power:" + generators); break; }
                        total = load + reserve + MaxLoad(power, requirements, kit);
                    }
                    if (generators * kit.GeneratorWatts < total) continue;
                    rooms.Add(power);
                    load = total;
                    generatorsNeeded = generators;
                    break;
                }
                if (generatorsNeeded == 0 && rejections.Count == 0) rejections.Add("room_demand_exceeds_legal_dimensions:Power:" + maximumGenerators);
            }
            return new LayoutDemandSet(rooms, Relationships(rooms, roomSeed), rejections, load, critical, generatorsNeeded);
        }

        internal static LayoutRoomDemand BuildPowerRoom(LayoutRequirements requirements, LayoutKit kit, int generators, int batteries, ulong roomSeed) =>
            Build(LayoutRoomKind.Power, generators, "power", requirements, kit, roomSeed, batteries);

        private static void Add(List<KeyValuePair<LayoutRoomKind, int>> wanted, LayoutRequirements requirements, LayoutRoomKind kind, int derived) =>
            wanted.Add(new KeyValuePair<LayoutRoomKind, int>(kind, Math.Max(derived, requirements.Floor(kind))));

        private static int Ceil(int value, int divisor) => (value + divisor - 1) / divisor;

        // Large food/storage demand need not require one unusually large clear
        // rectangle on a boulder-strewn tile. Keep every unit of capacity, but
        // split it into bounded, connected rooms that the existing packer can fit.
        private static List<LayoutRoomDemand> Split(LayoutRoomKind kind, int capacity, LayoutRequirements requirements, LayoutKit kit, ulong roomSeed)
        {
            // The stacks target is a packing preference, not a capacity limit.
            // At the bounded split count, larger legal rooms remain available.
            int minimumCount = IsStockpile(kind) && requirements.StockpileRoomCapacityTarget > 0
                ? Math.Min(MaxSplit, Ceil(capacity, requirements.StockpileRoomCapacityTarget)) : 1;
            for (int count = minimumCount; count <= MaxSplit && count <= capacity; count++)
            {
                List<LayoutRoomDemand> result = new List<LayoutRoomDemand>();
                for (int i = 0; i < count; i++)
                {
                    LayoutRoomDemand room = Build(kind, capacity / count + (i < capacity % count ? 1 : 0), Id(kind, i + 1, count > 1), requirements, kit, roomSeed);
                    if (room == null) break;
                    result.Add(room);
                }
                if (result.Count == count) return result;
            }
            return new List<LayoutRoomDemand> { null };
        }

        private static string Id(LayoutRoomKind kind, int index, bool numbered) => kind.ToString().ToLowerInvariant() + (numbered ? "-" + index : string.Empty);

        private static LayoutRoomDemand Build(LayoutRoomKind kind, int capacity, string id, LayoutRequirements requirements, LayoutKit kit, ulong roomSeed,
            int batteries = 0)
        {
            List<GridRect> legal = new List<GridRect>();
            int maxLength = kind == LayoutRoomKind.Power ? MaxPowerInteriorLength : MaxInterior;
            for (int w = MinInterior; w <= maxLength; w++)
                for (int h = MinInterior; h <= maxLength; h++)
                {
                    if (Math.Min(w, h) > MaxInterior) continue;
                    LayoutFurnishResult result = LayoutFurnishing.Furnish(kind, w, h, (w + 1) / 2, capacity, FurnishContext(kit, requirements, false, null, batteries));
                    if (result.Success) legal.Add(new GridRect(0, 0, w, h));
                }
            if (legal.Count == 0) return null;
            int minimum = legal.Min(s => s.Area);
            List<GridRect> sizes = legal.Where(s => s.Area <= minimum * NearMinimalAreaFactor)
                .OrderBy(s => s.Area).ThenBy(s => Math.Abs(s.Width - s.Height)).ThenBy(s => s.Width).Take(MaxSizesPerRoom)
                .OrderBy(s => LayoutSeed.Key(LayoutSeed.Sub(roomSeed, id), s.Width, s.Height)).ToList();
            return new LayoutRoomDemand(id, kind, capacity, Unit(kind), sizes, batteries);
        }

        internal static LayoutFurnishContext FurnishContext(LayoutKit kit, LayoutRequirements requirements, bool thickRoofed, Func<GridPoint, int> exhaustCost,
            int batteries = 0) =>
            new LayoutFurnishContext(kit, requirements.Climate.HeatingRequired, requirements.Climate.CoolingRequired && !thickRoofed, exhaustCost, batteries);

        // Upper bound on the critical load of a room over its candidate sizes.
        internal static int MaxCritical(LayoutRoomDemand demand, LayoutRequirements requirements, LayoutKit kit)
        {
            if (!kit.Powered) return 0;
            int max = 0;
            foreach (GridRect size in demand.CandidateSizes)
            {
                LayoutFurnishResult result = LayoutFurnishing.Furnish(demand.Kind, size.Width, size.Height, (size.Width + 1) / 2,
                    demand.Capacity, FurnishContext(kit, requirements, false, null, demand.Batteries));
                if (result.Success)
                    max = Math.Max(max, result.Fixtures.Where(f => LayoutPowerModel.IsCritical(demand.Kind, f.Kind, f.Role, f.Watts)).Sum(f => f.Watts));
            }
            return max;
        }

        // Upper bound on the powered load: the largest candidate size and worst-case climate.
        internal static int MaxLoad(LayoutRoomDemand demand, LayoutRequirements requirements, LayoutKit kit)
        {
            if (!kit.Powered) return 0;
            int max = 0;
            foreach (GridRect size in demand.CandidateSizes)
            {
                LayoutFurnishResult result = LayoutFurnishing.Furnish(demand.Kind, size.Width, size.Height, (size.Width + 1) / 2,
                    demand.Capacity, FurnishContext(kit, requirements, false, null, demand.Batteries));
                if (result.Success) max = Math.Max(max, result.Watts);
            }
            return max;
        }

        private static string Unit(LayoutRoomKind kind)
        {
            switch (kind)
            {
                case LayoutRoomKind.Barracks:
                case LayoutRoomKind.Bedroom:
                case LayoutRoomKind.Jail:
                case LayoutRoomKind.Medical: return "beds";
                case LayoutRoomKind.Kitchen: return "stoves";
                case LayoutRoomKind.Workshop:
                case LayoutRoomKind.Laboratory: return "benches";
                case LayoutRoomKind.Dining: return "seats";
                case LayoutRoomKind.Power: return "generators";
                default: return "stockpile-cells";
            }
        }

        // Desired adjacency graph. Seeded weight perturbation (+-1) varies soft
        // preferences between settlements; hard relations are never perturbed.
        private static List<LayoutRelationship> Relationships(List<LayoutRoomDemand> rooms, ulong roomSeed)
        {
            List<LayoutRelationship> result = new List<LayoutRelationship>();
            Func<LayoutRoomKind, IEnumerable<LayoutRoomDemand>> of = kind => rooms.Where(r => r.Kind == kind);
            IEnumerable<LayoutRoomDemand> housing = rooms.Where(r => r.Kind == LayoutRoomKind.Barracks || r.Kind == LayoutRoomKind.Bedroom);
            IEnumerable<LayoutRoomDemand> food = rooms.Where(r => r.Kind == LayoutRoomKind.Freezer || r.Kind == LayoutRoomKind.Pantry);
            foreach (LayoutRoomDemand kitchen in of(LayoutRoomKind.Kitchen))
            {
                foreach (LayoutRoomDemand store in food)
                {
                    result.Add(new LayoutRelationship(kitchen.Id, store.Id, LayoutRelationKind.Near, 5, true, KitchenFoodHardDistance));
                }
                foreach (LayoutRoomDemand dining in of(LayoutRoomKind.Dining)) Soft(result, roomSeed, kitchen, dining, LayoutRelationKind.Near, 4);
            }
            foreach (LayoutRoomDemand home in housing)
            {
                foreach (LayoutRoomDemand dining in of(LayoutRoomKind.Dining)) Soft(result, roomSeed, home, dining, LayoutRelationKind.Near, 2);
                foreach (LayoutRoomDemand jail in of(LayoutRoomKind.Jail)) Soft(result, roomSeed, home, jail, LayoutRelationKind.Far, 2);
                foreach (LayoutRoomDemand medical in of(LayoutRoomKind.Medical)) Soft(result, roomSeed, home, medical, LayoutRelationKind.Near, 1);
            }
            foreach (LayoutRoomDemand workshop in of(LayoutRoomKind.Workshop))
            {
                foreach (LayoutRoomDemand storage in of(LayoutRoomKind.Storage)) Soft(result, roomSeed, workshop, storage, LayoutRelationKind.Near, 3);
                foreach (LayoutRoomDemand power in of(LayoutRoomKind.Power)) Soft(result, roomSeed, workshop, power, LayoutRelationKind.Near, 1);
            }
            return result;
        }

        private static void Soft(List<LayoutRelationship> result, ulong seed, LayoutRoomDemand a, LayoutRoomDemand b, LayoutRelationKind kind, int weight)
        {
            int perturbed = Math.Max(0, weight + LayoutSeed.Roll(LayoutSeed.Sub(seed, a.Id + "|" + b.Id), "weight", 3) - 1);
            if (perturbed > 0) result.Add(new LayoutRelationship(a.Id, b.Id, kind, perturbed, false, int.MaxValue));
        }
    }

    public sealed class LayoutFurnishContext
    {
        public LayoutFurnishContext(LayoutKit kit, bool heating, bool cooling, Func<GridPoint, int> exhaustCost, int batteries = 0)
        {
            Batteries = batteries;
            Kit = kit ?? throw new ArgumentNullException(nameof(kit));
            Heating = heating;
            Cooling = cooling;
            ExhaustCost = exhaustCost;
        }

        public LayoutKit Kit { get; }
        // Power room only: batteries to place (at least one).
        public int Batteries { get; }
        public bool Electric => Kit.Powered;
        public bool Heating { get; }
        public bool Cooling { get; }
        // Cost of taking cooler exhaust through a local wall cell (its outside
        // neighbour is the vanilla hot cell); -1 = not allowed, null = any wall at cost 0.
        public Func<GridPoint, int> ExhaustCost { get; }
    }

    // A fixture in room-local coordinates: interior x 1..W, z 1..H, walls at 0
    // and W+1/H+1, the door at (DoorX, 0) and the interior entry cell at (DoorX, 1).
    public sealed class LayoutLocalFixture
    {
        internal LayoutLocalFixture(LayoutFixtureKind kind, string defName, IEnumerable<GridPoint> cells, IEnumerable<GridPoint> access,
            GridPoint facing, bool blocking, int watts, bool needsFuel, SettlementRole? role)
        {
            Role = role;
            Kind = kind;
            DefName = defName;
            Cells = new ReadOnlyCollection<GridPoint>(cells.ToList());
            Access = new ReadOnlyCollection<GridPoint>((access ?? Enumerable.Empty<GridPoint>()).ToList());
            Facing = facing;
            Blocking = blocking;
            Watts = watts;
            NeedsFuel = needsFuel;
        }

        public LayoutFixtureKind Kind { get; }
        public string DefName { get; }
        public IReadOnlyList<GridPoint> Cells { get; }
        // Interaction cells that must stay free and reachable. Blocking fixtures
        // without one need any adjacent reachable cell (job access).
        public IReadOnlyList<GridPoint> Access { get; }
        // Unit vector from the fixture toward its working side (benches, campfires),
        // into the room (wall coolers), or away from the wall (beds, other fixtures).
        public GridPoint Facing { get; }
        public bool Blocking { get; }
        public int Watts { get; }
        public bool NeedsFuel { get; }
        // The equipment role the fixture fills; null for generators, batteries and the court.
        public SettlementRole? Role { get; }
    }

    public sealed class LayoutFurnishResult
    {
        internal LayoutFurnishResult(List<LayoutLocalFixture> fixtures, int capacity, string failure)
        {
            Fixtures = new ReadOnlyCollection<LayoutLocalFixture>(fixtures);
            Capacity = capacity;
            Failure = failure;
            Watts = fixtures.Sum(f => f.Watts);
        }

        public IReadOnlyList<LayoutLocalFixture> Fixtures { get; }
        public int Capacity { get; }
        public string Failure { get; }
        public int Watts { get; }
        public bool Success => Failure == null;
    }

    // Reusable furnishing rules. Rooms request roles; footprints, passability,
    // interaction cells, power and fuel come from the resolved catalog defs. Wall
    // fixtures stand with their back to a wall and their working side toward the
    // room; every placement is kept only when a flood from the entry cell still
    // reaches all access cells and every placed fixture keeps a reachable neighbour.
    public static class LayoutFurnishing
    {
        private const byte Free = 0;
        private const byte Blocked = 1;
        private const byte KeepFree = 2;
        // One shelf per this many storage-room cells. PROVISIONAL.
        public const int CellsPerShelf = 12;

        public static LayoutFurnishResult Furnish(LayoutRoomKind kind, int w, int h, int doorX, int capacityTarget, LayoutFurnishContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (w < 1 || h < 1 || doorX < 1 || doorX > w) return new LayoutFurnishResult(new List<LayoutLocalFixture>(), 0, "illegal_room_size");
            Room room = new Room(w, h, doorX);
            LayoutKit kit = context.Kit;
            int area = w * h;
            int capacity = 0;
            string failure = null;
            Template light = Light(kit), wallLight = WallLight(kit);
            if (LayoutDemandModel.IsStockpile(kind))
            {
                if (kind == LayoutRoomKind.Freezer)
                {
                    Template cooler = Cool(kit);
                    if (cooler == null || !cooler.WallMounted || !room.PlaceWallCoolers(cooler, Ceil(area, 30), context.ExhaustCost, true)) failure = "freezer_cooler_exhaust";
                }
                // Wall lights hang over racks and stock, so they are planned here
                // and added after the racks; floor lights must claim cells first.
                List<LayoutLocalFixture> wallLights = failure == null ? room.WallLights(wallLight) : null;
                if (failure == null && wallLights == null && (light == null || !room.Place(light, (area + 35) / 36))) failure = "lights";
                int shelfStacks = 0;
                Template shelf = From(kit.DefFor(SettlementRole.Storage), LayoutFixtureKind.Shelf, SettlementRole.Storage);
                // A player fills a warehouse with racks: shelves along the walls,
                // then back-to-back rows with one-cell aisles. Capacity counts the
                // stacks each shelf cell really holds, so demand needs fewer rooms.
                if (failure == null && shelf != null)
                    shelfStacks = room.PlaceStorageRacks(shelf) * shelf.Length * shelf.Depth * shelf.StacksPerCell;
                if (failure == null)
                {
                    List<GridPoint> stock = room.ReachableFree().Where(c => !room.IsKeepFree(c)).ToList();
                    capacity = stock.Count + shelfStacks;
                    if (stock.Count > 0)
                        room.Fixtures.Add(new LayoutLocalFixture(LayoutFixtureKind.Stockpile, "Stockpile", stock, null, new GridPoint(0, 1), false, 0, false,
                            SettlementRole.Storage));
                    if (wallLights != null) room.Fixtures.AddRange(wallLights);
                }
            }
            else
            {
                switch (kind)
                {
                    case LayoutRoomKind.Barracks:
                    case LayoutRoomKind.Bedroom:
                        capacity = PlaceAll(room, From(kit.DefFor(SettlementRole.Sleep), LayoutFixtureKind.Bed, SettlementRole.Sleep), capacityTarget);
                        break;
                    case LayoutRoomKind.Jail:
                        capacity = PlaceAll(room, From(kit.DefFor(SettlementRole.Sleep), LayoutFixtureKind.PrisonBed, SettlementRole.Sleep), capacityTarget);
                        break;
                    case LayoutRoomKind.Medical:
                        capacity = PlaceAll(room, From(kit.DefFor(SettlementRole.MedicalBed), LayoutFixtureKind.MedicalBed, SettlementRole.MedicalBed), capacityTarget);
                        Template monitor = From(kit.DefFor(SettlementRole.MedicalMonitor), LayoutFixtureKind.MedicalMonitor, SettlementRole.MedicalMonitor);
                        if (capacity >= capacityTarget && monitor != null && !room.Place(monitor, 1)) failure = "medical_monitor";
                        Template medicalShelf = From(kit.DefFor(SettlementRole.MedicalStorage), LayoutFixtureKind.Shelf, SettlementRole.MedicalStorage);
                        if (capacity >= capacityTarget && medicalShelf != null && !room.Place(medicalShelf, 1)) failure = "medical_shelf";
                        break;
                    case LayoutRoomKind.Kitchen:
                        capacity = room.PlaceWorkstations(From(kit.DefFor(SettlementRole.Cooking), LayoutFixtureKind.Stove, SettlementRole.Cooking),
                            kit.DefFor(SettlementRole.Seat), capacityTarget);
                        break;
                    case LayoutRoomKind.Workshop:
                        for (int i = 0; kit.WorkshopDefs.Count > 0 && i < capacityTarget; i++)
                        {
                            CatalogDef bench = kit.WorkshopDefs[i % kit.WorkshopDefs.Count];
                            Template template = From(bench, LayoutFixtureKind.Workbench, CraftRoleOf(kit, bench));
                            if (room.PlaceWorkstations(template, kit.DefFor(SettlementRole.Seat), 1) != 1) break;
                            capacity++;
                        }
                        break;
                    case LayoutRoomKind.Laboratory:
                        capacity = room.PlaceWorkstations(From(kit.DefFor(SettlementRole.Research), LayoutFixtureKind.Workbench, SettlementRole.Research),
                            kit.DefFor(SettlementRole.Seat), capacityTarget);
                        CatalogDef analyzer = kit.DefFor(SettlementRole.ResearchAnalyzer);
                        CatalogDef research = kit.DefFor(SettlementRole.Research);
                        if (capacity >= capacityTarget && analyzer != null && research != null &&
                            research.LinkableFacilities.Contains(analyzer.DefName) &&
                            !room.PlaceFacility(From(analyzer, LayoutFixtureKind.ResearchAnalyzer, SettlementRole.ResearchAnalyzer),
                                SettlementRole.Research, analyzer.FacilityMaxDistance)) failure = "research_analyzer_link";
                        break;
                    case LayoutRoomKind.Dining:
                        capacity = 4 * room.PlaceTables(kit, Ceil(capacityTarget, 4));
                        Template recreation = From(kit.DefFor(SettlementRole.Recreation), LayoutFixtureKind.Recreation, SettlementRole.Recreation);
                        if (capacity >= capacityTarget && recreation != null && !room.Place(recreation, 1)) failure = "recreation";
                        break;
                    case LayoutRoomKind.Power:
                        capacity = PlaceAll(room, From(kit.Generator, LayoutFixtureKind.Generator, null, true), capacityTarget);
                        Template battery = From(kit.Battery, LayoutFixtureKind.Battery, null, true);
                        if (capacity >= capacityTarget && (battery == null || !room.Place(battery, Math.Max(1, context.Batteries)))) failure = "batteries";
                        // One vent per two fueled generators, exhausting like a cooler.
                        Template vent = From(kit.Vent, LayoutFixtureKind.WallVent, null, true);
                        if (vent != null) vent.WallMounted = true;
                        if (failure == null && capacity >= capacityTarget && vent != null &&
                            !room.PlaceWallCoolers(vent, Ceil(capacity, 2), context.ExhaustCost, true)) failure = "generator_vent";
                        break;
                }
                if (failure == null && capacity < capacityTarget) failure = "capacity";
                if (failure == null && LayoutDemandModel.IsLiving(kind))
                {
                    if (context.Heating)
                    {
                        Template heat = Heat(kit);
                        if (heat == null || !room.Place(heat, heat.Interaction ? 1 : Ceil(area, 40))) failure = "heating";
                    }
                    if (failure == null && context.Cooling)
                    {
                        Template cool = Cool(kit);
                        bool placed = cool != null && (cool.WallMounted
                            ? room.PlaceWallCoolers(cool, Ceil(area, 40), context.ExhaustCost, false)
                            : room.Place(cool, Ceil(area, 25)));
                        if (!placed) failure = "cooling";
                    }
                }
                if (failure == null)
                {
                    List<LayoutLocalFixture> wallLights = room.WallLights(wallLight);
                    if (wallLights != null) room.Fixtures.AddRange(wallLights);
                    else if (light == null || !room.Place(light, (area + 35) / 36)) failure = "lights";
                }
            }
            if (failure == null && capacity < capacityTarget) failure = "capacity";
            return new LayoutFurnishResult(room.Fixtures, capacity, failure);
        }

        private static int PlaceAll(Room room, Template template, int count) => template != null && room.Place(template, count) ? count : 0;
        private static int Ceil(int value, int divisor) => (value + divisor - 1) / divisor;

        private static SettlementRole? CraftRoleOf(LayoutKit kit, CatalogDef bench)
        {
            foreach (SettlementRole role in LayoutKit.CraftRoles)
            {
                CatalogDef def = kit.DefFor(role);
                if (def != null && def.DefName == bench.DefName) return role;
            }
            return null;
        }

        private sealed class Template
        {
            public LayoutFixtureKind Kind;
            public SettlementRole? Role;
            public string Def;
            public int Length;
            public int Depth;
            public int Watts;
            public bool Fuel;
            public bool Blocking;
            public bool Interaction;
            // Steps from the front row to the interaction cell (vanilla offset (0,0,-z)).
            public int Forward;
            public bool WallMounted;
            public bool SupportsSeat;
            public int StacksPerCell = 1;
        }

        // Footprint along the wall (SizeX) and into the room (SizeZ), vanilla north rotation.
        private static Template From(CatalogDef def, LayoutFixtureKind kind, SettlementRole? role, bool blocking = false)
        {
            if (def == null) return null;
            // Only straight-ahead interaction offsets are modelled (every Core bench uses (0,0,-1)).
            if (def.HasInteractionCell && (def.InteractionOffset.X != 0 || def.InteractionOffset.Z >= 0)) return null;
            return new Template
            {
                Kind = kind,
                Role = role,
                Def = def.DefName,
                Length = def.SizeX,
                Depth = def.SizeZ,
                Watts = Math.Max(0, def.BasePowerConsumption),
                Fuel = def.Fuel.Count > 0,
                Blocking = blocking || def.Passability != CatalogPassability.Standable,
                Interaction = def.HasInteractionCell,
                Forward = def.HasInteractionCell ? Math.Max(1, -def.InteractionOffset.Z - (def.SizeZ - 1) / 2) : 0,
                WallMounted = def.ThingClass == LayoutKit.CoolerThingClass,
                SupportsSeat = def.SupportsSeatedWork,
                StacksPerCell = Math.Max(1, def.MaxItemsInCell)
            };
        }

        private static Template Light(LayoutKit kit)
        {
            CatalogDef def = kit.DefFor(SettlementRole.Light);
            return From(def, def != null && def.BasePowerConsumption > 0 ? LayoutFixtureKind.Lamp : LayoutFixtureKind.Torch, SettlementRole.Light);
        }

        private static Template WallLight(LayoutKit kit)
        {
            CatalogDef def = kit.DefFor(SettlementRole.WallLight);
            return From(def, def != null && def.BasePowerConsumption > 0 ? LayoutFixtureKind.Lamp : LayoutFixtureKind.Torch, SettlementRole.Light);
        }

        private static Template Heat(LayoutKit kit)
        {
            CatalogDef def = kit.DefFor(SettlementRole.Heat);
            return From(def, def != null && def.BasePowerConsumption > 0 ? LayoutFixtureKind.Heater : LayoutFixtureKind.Campfire, SettlementRole.Heat);
        }

        private static Template Cool(LayoutKit kit)
        {
            CatalogDef def = kit.DefFor(SettlementRole.Cool);
            return From(def, def != null && def.ThingClass == LayoutKit.CoolerThingClass ? LayoutFixtureKind.WallCooler : LayoutFixtureKind.PassiveCooler,
                SettlementRole.Cool);
        }

        private sealed class Room
        {
            private readonly int w;
            private readonly int h;
            private readonly int doorX;
            private readonly byte[] state;
            public readonly List<LayoutLocalFixture> Fixtures = new List<LayoutLocalFixture>();

            public Room(int w, int h, int doorX)
            {
                this.w = w;
                this.h = h;
                this.doorX = doorX;
                state = new byte[(w + 2) * (h + 2)];
                state[Index(doorX, 1)] = KeepFree;
            }

            private int Index(int x, int z) => z * (w + 2) + x;
            private bool Interior(GridPoint c) => c.X >= 1 && c.X <= w && c.Z >= 1 && c.Z <= h;
            public bool IsKeepFree(GridPoint c) => state[Index(c.X, c.Z)] == KeepFree;

            // Far wall first (opposite the door), then the side walls, then the door wall.
            private IEnumerable<KeyValuePair<List<GridPoint>, GridPoint>> Slots(int length, int depth)
            {
                for (int t = 1; t + length - 1 <= w; t++) yield return Slot(t, length, depth, 0);
                for (int t = h; t - length + 1 >= 1; t--) yield return Slot(t, length, depth, 1);
                for (int t = h; t - length + 1 >= 1; t--) yield return Slot(t, length, depth, 2);
                for (int t = 1; t + length - 1 <= w; t++) yield return Slot(t, length, depth, 3);
            }

            private KeyValuePair<List<GridPoint>, GridPoint> Slot(int t, int length, int depth, int wall)
            {
                List<GridPoint> cells = new List<GridPoint>();
                GridPoint inward;
                for (int a = 0; a < length; a++)
                    for (int d = 0; d < depth; d++)
                    {
                        switch (wall)
                        {
                            case 0: cells.Add(new GridPoint(t + a, h - d)); break;
                            case 1: cells.Add(new GridPoint(1 + d, t - a)); break;
                            case 2: cells.Add(new GridPoint(w - d, t - a)); break;
                            default: cells.Add(new GridPoint(t + a, 1 + d)); break;
                        }
                    }
                switch (wall)
                {
                    case 0: inward = new GridPoint(0, -1); break;
                    case 1: inward = new GridPoint(1, 0); break;
                    case 2: inward = new GridPoint(-1, 0); break;
                    default: inward = new GridPoint(0, 1); break;
                }
                return new KeyValuePair<List<GridPoint>, GridPoint>(cells, inward);
            }

            public bool Place(Template template, int count)
            {
                for (int placed = 0; placed < count; placed++)
                {
                    bool done = false;
                    foreach (KeyValuePair<List<GridPoint>, GridPoint> slot in Slots(template.Length, template.Depth))
                    {
                        List<GridPoint> cells = slot.Key;
                        GridPoint inward = slot.Value;
                        if (!cells.All(c => Interior(c) && state[Index(c.X, c.Z)] == Free)) continue;
                        List<GridPoint> access = new List<GridPoint>();
                        if (template.Interaction)
                        {
                            // Centre of the front face, the catalog's forward steps toward the room.
                            GridPoint middle = cells[(template.Length / 2) * template.Depth + template.Depth - 1];
                            GridPoint front = new GridPoint(middle.X + inward.X * template.Forward, middle.Z + inward.Z * template.Forward);
                            if (!Interior(front) || state[Index(front.X, front.Z)] == Blocked ||
                                Fixtures.Any(f => f.Access.Contains(front) &&
                                    (f.Kind == LayoutFixtureKind.Workbench || f.Kind == LayoutFixtureKind.Stove))) continue;
                            access.Add(front);
                        }
                        if (TryCommit(cells, access))
                        {
                            // Cells are reserved in the local plan either way; Blocking reports vanilla passability.
                            Fixtures.Add(new LayoutLocalFixture(template.Kind, template.Def, cells, access, inward, template.Blocking, template.Watts,
                                template.Fuel, template.Role));
                            done = true;
                            break;
                        }
                    }
                    if (!done) return false;
                }
                return true;
            }

            // Storage racks: the far and side walls (the door wall keeps the
            // front aisle clear), then back-to-back island rows parallel to the
            // far wall, separated by one-cell aisles, with side aisles kept free.
            // Every placement passes the room's connectivity/access check.
            public int PlaceStorageRacks(Template template)
            {
                int placed = 0;
                Func<List<GridPoint>, GridPoint, bool> commit = (cells, facing) =>
                {
                    if (!cells.All(c => Interior(c) && state[Index(c.X, c.Z)] == Free) || !TryCommit(cells, new List<GridPoint>())) return false;
                    Fixtures.Add(new LayoutLocalFixture(template.Kind, template.Def, cells, null, facing, template.Blocking,
                        template.Watts, template.Fuel, template.Role));
                    placed++;
                    return true;
                };
                foreach (int wall in new[] { 0, 1, 2 })
                {
                    int span = wall == 0 ? w : h;
                    for (int i = 0; i + template.Length <= span; )
                    {
                        int t = wall == 0 ? 1 + i : h - i;
                        KeyValuePair<List<GridPoint>, GridPoint> slot = Slot(t, template.Length, template.Depth, wall);
                        i += commit(slot.Key, slot.Value) ? template.Length : 1;
                    }
                }
                int d = template.Depth;
                for (int top = h - 1 - d; top - d + 1 >= 2; top -= 2 * d + 1)
                    foreach (bool upper in new[] { true, false })
                    {
                        int rowTop = upper ? top : top - d;
                        if (rowTop - d + 1 < 2) continue;
                        for (int x = 3; x + template.Length - 1 <= w - 2; )
                        {
                            List<GridPoint> cells = new List<GridPoint>();
                            for (int a = 0; a < template.Length; a++)
                                for (int k = 0; k < d; k++) cells.Add(new GridPoint(x + a, rowTop - k));
                            x += commit(cells, new GridPoint(0, upper ? 1 : -1)) ? template.Length : 1;
                        }
                    }
                return placed;
            }

            // Places up to count; returns how many fit.
            public int PlaceUpTo(Template template, int count)
            {
                int placed = 0;
                while (placed < count && Place(template, 1)) placed++;
                return placed;
            }

            public int PlaceWorkstations(Template template, CatalogDef seat, int count)
            {
                if (template == null) return 0;
                int placed = 0;
                for (; placed < count; placed++)
                {
                    if (!Place(template, 1)) break;
                    LayoutLocalFixture workstation = Fixtures.Last();
                    if (!template.SupportsSeat || !template.Interaction || seat == null) continue;
                    if (!seat.IsSittable || seat.SizeX != 1 || seat.SizeZ != 1 || seat.Passability != CatalogPassability.Standable) break;
                    GridPoint cell = workstation.Access.Single();
                    // Facing stores the reverse of vanilla rotation. Chair and
                    // bench share rotation: chair looks from working cell to bench.
                    Fixtures.Add(new LayoutLocalFixture(LayoutFixtureKind.Chair, seat.DefName, new[] { cell }, null,
                        workstation.Facing, false, 0, false, SettlementRole.Seat));
                }
                return placed;
            }

            // Core analyzer needs LOS and a bounded center-to-center link. Both
            // are checked against planned room occupancy, then again in engine.
            public bool PlaceFacility(Template template, SettlementRole linkedRole, double maxDistance)
            {
                if (template == null || maxDistance <= 0) return false;
                foreach (KeyValuePair<List<GridPoint>, GridPoint> slot in Slots(template.Length, template.Depth))
                {
                    if (!slot.Key.All(c => Interior(c) && state[Index(c.X, c.Z)] == Free)) continue;
                    GridPoint center = slot.Key[slot.Key.Count / 2];
                    if (!Fixtures.Where(f => f.Role == linkedRole).Any(f =>
                        CenterDistance(f.Cells, slot.Key) <= maxDistance &&
                        P4BFireLane.CanEngage(center, f.Cells[f.Cells.Count / 2], 0, (int)Math.Ceiling(maxDistance),
                            Fixtures.Where(o => o != f).SelectMany(o => o.Cells).ToList()))) continue;
                    if (!TryCommit(slot.Key, new List<GridPoint>())) continue;
                    Fixtures.Add(new LayoutLocalFixture(template.Kind, template.Def, slot.Key, null, slot.Value,
                        template.Blocking, template.Watts, template.Fuel, template.Role));
                    return true;
                }
                return false;
            }

            private static double CenterDistance(IReadOnlyList<GridPoint> a, IReadOnlyList<GridPoint> b)
            {
                double dx = a.Average(c => c.X) - b.Average(c => c.X);
                double dz = a.Average(c => c.Z) - b.Average(c => c.Z);
                return Math.Sqrt(dx * dx + dz * dz);
            }

            // 2x2 tables with a seat on each side. The seat cells stay free (they are the
            // table's access cells); seats are placed only when the Seat role resolves.
            public int PlaceTables(LayoutKit kit, int count)
            {
                CatalogDef tableDef = kit.DefFor(SettlementRole.Table);
                if (tableDef == null || tableDef.SizeX != 2 || tableDef.SizeZ != 2) return 0;
                CatalogDef seatDef = kit.DefFor(SettlementRole.Seat);
                int placed = 0;
                for (int z = h - 2; z >= 2 && placed < count; z--)
                    for (int x = 2; x + 1 <= w - 1 && placed < count; x++)
                    {
                        GridPoint[] table = { new GridPoint(x, z), new GridPoint(x + 1, z), new GridPoint(x, z + 1), new GridPoint(x + 1, z + 1) };
                        GridPoint[] chairs = { new GridPoint(x - 1, z), new GridPoint(x + 2, z + 1), new GridPoint(x, z + 2), new GridPoint(x + 1, z - 1) };
                        if (!table.All(c => Interior(c) && state[Index(c.X, c.Z)] == Free)) continue;
                        if (!chairs.All(c => Interior(c) && state[Index(c.X, c.Z)] == Free)) continue;
                        if (!TryCommit(table.ToList(), chairs.ToList())) continue;
                        Fixtures.Add(new LayoutLocalFixture(LayoutFixtureKind.Table, tableDef.DefName, table, chairs, new GridPoint(0, -1),
                            tableDef.Passability != CatalogPassability.Standable, 0, false, SettlementRole.Table));
                        if (seatDef != null)
                            for (int i = 0; i < chairs.Length; i++)
                            {
                                // Left looks east, right west, top south, bottom north.
                                GridPoint[] reverseFacing = { new GridPoint(-1, 0), new GridPoint(1, 0), new GridPoint(0, 1), new GridPoint(0, -1) };
                                Fixtures.Add(new LayoutLocalFixture(LayoutFixtureKind.Chair, seatDef.DefName, new[] { chairs[i] }, null,
                                    reverseFacing[i], false, 0, false, SettlementRole.Seat));
                            }
                        placed++;
                    }
                return placed;
            }

            // Wall-mounted coolers sit in a straight wall segment (never a corner or the
            // door) with their cold side in the room. exhaustCost(local wall cell) prices
            // the hot-side cell outside that wall, or returns -1 when it cannot take exhaust.
            // Lower cost wins; at equal cost the door wall is preferred.
            // A lit cell is within this many cells of a lamp: about half of the
            // vanilla wall torch's glow radius (9), so the wall lamp's (11) is safe.
            public const int WallLightReachSquared = 20;

            // Lights attached to this room's own wall, facing the wall they hang
            // on. They take no floor: the cell below stays free for racks,
            // stock and furniture. Never on the door or a through-wall fixture.
            // Each lamp lights the most unlit interior cells; null when a cell
            // stays unlit, and the room falls back to floor lights.
            public List<LayoutLocalFixture> WallLights(Template lamp)
            {
                if (lamp == null) return null;
                HashSet<GridPoint> through = new HashSet<GridPoint>(Fixtures.Where(f => LayoutFixtureKinds.ThroughWall(f.Kind)).SelectMany(f => f.Cells));
                List<KeyValuePair<GridPoint, GridPoint>> candidates = new List<KeyValuePair<GridPoint, GridPoint>>();
                for (int x = 1; x <= w; x++) candidates.Add(new KeyValuePair<GridPoint, GridPoint>(new GridPoint(x, 0), new GridPoint(0, 1)));
                for (int z = 1; z <= h; z++) candidates.Add(new KeyValuePair<GridPoint, GridPoint>(new GridPoint(0, z), new GridPoint(1, 0)));
                for (int z = 1; z <= h; z++) candidates.Add(new KeyValuePair<GridPoint, GridPoint>(new GridPoint(w + 1, z), new GridPoint(-1, 0)));
                for (int x = 1; x <= w; x++) candidates.Add(new KeyValuePair<GridPoint, GridPoint>(new GridPoint(x, h + 1), new GridPoint(0, -1)));
                candidates.RemoveAll(c => (c.Key.Z == 0 && c.Key.X == doorX) || through.Contains(c.Key));
                List<GridPoint> unlit = new List<GridPoint>();
                for (int z = 1; z <= h; z++)
                    for (int x = 1; x <= w; x++) unlit.Add(new GridPoint(x, z));
                List<LayoutLocalFixture> lights = new List<LayoutLocalFixture>();
                HashSet<GridPoint> used = new HashSet<GridPoint>();
                Func<GridPoint, GridPoint, bool> litBy = (lampCell, c) =>
                    (lampCell.X - c.X) * (lampCell.X - c.X) + (lampCell.Z - c.Z) * (lampCell.Z - c.Z) <= WallLightReachSquared;
                while (unlit.Count > 0)
                {
                    int best = -1, gain = 0;
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        GridPoint cell = new GridPoint(candidates[i].Key.X + candidates[i].Value.X, candidates[i].Key.Z + candidates[i].Value.Z);
                        if (used.Contains(cell)) continue;
                        int g = unlit.Count(c => litBy(cell, c));
                        if (g > gain) { gain = g; best = i; }
                    }
                    if (best < 0) return null;
                    GridPoint inward = candidates[best].Value;
                    GridPoint at = new GridPoint(candidates[best].Key.X + inward.X, candidates[best].Key.Z + inward.Z);
                    used.Add(at);
                    unlit.RemoveAll(c => litBy(at, c));
                    lights.Add(new LayoutLocalFixture(lamp.Kind, lamp.Def, new[] { at }, null, inward, false, lamp.Watts, lamp.Fuel, lamp.Role));
                }
                return lights;
            }

            public bool PlaceWallCoolers(Template cooler, int count, Func<GridPoint, int> exhaustCost, bool keepHotAwayFromDoor)
            {
                List<KeyValuePair<GridPoint, GridPoint>> walls = new List<KeyValuePair<GridPoint, GridPoint>>();
                for (int x = 1; x <= w; x++) walls.Add(new KeyValuePair<GridPoint, GridPoint>(new GridPoint(x, 0), new GridPoint(0, 1)));
                for (int z = 1; z <= h; z++) walls.Add(new KeyValuePair<GridPoint, GridPoint>(new GridPoint(0, z), new GridPoint(1, 0)));
                for (int z = 1; z <= h; z++) walls.Add(new KeyValuePair<GridPoint, GridPoint>(new GridPoint(w + 1, z), new GridPoint(-1, 0)));
                for (int x = 1; x <= w; x++) walls.Add(new KeyValuePair<GridPoint, GridPoint>(new GridPoint(x, h + 1), new GridPoint(0, -1)));
                List<KeyValuePair<int, KeyValuePair<GridPoint, GridPoint>>> candidates = new List<KeyValuePair<int, KeyValuePair<GridPoint, GridPoint>>>();
                for (int i = 0; i < walls.Count; i++)
                {
                    GridPoint wall = walls[i].Key;
                    bool doorWall = wall.Z == 0;
                    if (doorWall && (wall.X == doorX || (keepHotAwayFromDoor && Math.Abs(wall.X - doorX) <= 1))) continue;
                    int cost = exhaustCost == null ? 0 : exhaustCost(wall);
                    if (cost >= 0) candidates.Add(new KeyValuePair<int, KeyValuePair<GridPoint, GridPoint>>(cost, walls[i]));
                }
                List<KeyValuePair<int, GridPoint>> hot = new List<KeyValuePair<int, GridPoint>>();
                int placed = 0;
                foreach (KeyValuePair<int, KeyValuePair<GridPoint, GridPoint>> candidate in candidates
                    .OrderBy(c => c.Key).ThenBy(c => c.Value.Key.Z == 0 ? 0 : 1)
                    .ThenBy(c => c.Value.Key.Z == 0 ? Math.Abs(Math.Abs(c.Value.Key.X - doorX) - 2) : 0)
                    .ThenBy(c => c.Value.Key.Z).ThenBy(c => c.Value.Key.X))
                {
                    if (placed >= count) break;
                    GridPoint wall = candidate.Value.Key, inward = candidate.Value.Value;
                    GridPoint cold = new GridPoint(wall.X + inward.X, wall.Z + inward.Z);
                    GridPoint hotCell = new GridPoint(wall.X - inward.X, wall.Z - inward.Z);
                    if (state[Index(cold.X, cold.Z)] != Free) continue;
                    // Two pockets may not touch: one would have to be the other's wall.
                    if (hot.Any(o => (o.Key > 0 || candidate.Key > 0) && Math.Abs(o.Value.X - hotCell.X) + Math.Abs(o.Value.Z - hotCell.Z) == 1)) continue;
                    if (!TryCommit(new List<GridPoint>(), new List<GridPoint> { cold })) continue;
                    Fixtures.Add(new LayoutLocalFixture(cooler.Kind, cooler.Def, new[] { wall },
                        new[] { cold }, inward, true, cooler.Watts, cooler.Fuel, cooler.Role));
                    hot.Add(new KeyValuePair<int, GridPoint>(candidate.Key, hotCell));
                    placed++;
                }
                return placed >= count;
            }

            private bool TryCommit(List<GridPoint> blocking, List<GridPoint> keepFree)
            {
                if (keepFree.Any(c => !Interior(c) || state[Index(c.X, c.Z)] == Blocked || blocking.Contains(c))) return false;
                byte[] backup = (byte[])state.Clone();
                foreach (GridPoint c in blocking) state[Index(c.X, c.Z)] = Blocked;
                foreach (GridPoint c in keepFree) state[Index(c.X, c.Z)] = KeepFree;
                if (Connected(blocking)) return true;
                Array.Copy(backup, state, state.Length);
                return false;
            }

            private bool Connected(List<GridPoint> newBlocking)
            {
                HashSet<GridPoint> reach = new HashSet<GridPoint>(ReachableFree());
                for (int z = 1; z <= h; z++)
                    for (int x = 1; x <= w; x++)
                        if (state[Index(x, z)] == KeepFree && !reach.Contains(new GridPoint(x, z))) return false;
                // Every placed fixture, blocking or not, keeps a reachable neighbour (job access).
                IEnumerable<IEnumerable<GridPoint>> blockers = Fixtures.Where(f => !LayoutFixtureKinds.ThroughWall(f.Kind) &&
                        f.Kind != LayoutFixtureKind.Chair && f.Kind != LayoutFixtureKind.Stockpile)
                    .Select(f => (IEnumerable<GridPoint>)f.Cells);
                if (newBlocking.Count > 0) blockers = blockers.Concat(new[] { (IEnumerable<GridPoint>)newBlocking });
                foreach (IEnumerable<GridPoint> cells in blockers)
                    if (!cells.Any(c => Adjacent(c).Any(reach.Contains))) return false;
                return true;
            }

            public List<GridPoint> ReachableFree()
            {
                List<GridPoint> result = new List<GridPoint>();
                bool[] seen = new bool[state.Length];
                Queue<GridPoint> queue = new Queue<GridPoint>();
                GridPoint entry = new GridPoint(doorX, 1);
                queue.Enqueue(entry);
                seen[Index(entry.X, entry.Z)] = true;
                while (queue.Count > 0)
                {
                    GridPoint current = queue.Dequeue();
                    result.Add(current);
                    foreach (GridPoint next in Adjacent(current))
                    {
                        if (!Interior(next) || seen[Index(next.X, next.Z)] || state[Index(next.X, next.Z)] == Blocked) continue;
                        seen[Index(next.X, next.Z)] = true;
                        queue.Enqueue(next);
                    }
                }
                result.Sort();
                return result;
            }

            private static IEnumerable<GridPoint> Adjacent(GridPoint c)
            {
                yield return new GridPoint(c.X, c.Z - 1);
                yield return new GridPoint(c.X - 1, c.Z);
                yield return new GridPoint(c.X + 1, c.Z);
                yield return new GridPoint(c.X, c.Z + 1);
            }
        }
    }
}
