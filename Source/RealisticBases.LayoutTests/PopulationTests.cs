using System;
using System.Linq;
using RealisticBases.Planning;

internal static partial class Program
{
    private static void OrdinaryPopulationBudgets()
    {
        Equal(16, OrdinarySettlementPopulation.P4EMaxDefenseCrossings, "twelve compound plus four field crossings");
        Equal(1100, OrdinarySettlementPopulation.P4ETurretImportReservePercent, "approved wealth ceiling");
        Equal(3300, OrdinarySettlementPopulation.EstimateDefenseReserveWatts(
            TechStageModel.Create(SettlementTechStageLevel.IndustrialMid)), "T5a mid-stage power reserve");
        Equal(4000, OrdinarySettlementPopulation.EstimateDefenseReserveWatts(
            TechStageModel.Create(SettlementTechStageLevel.IndustrialLate)), "T5a late-stage power reserve");
        Equal(120352, OrdinarySettlementPopulation.EstimateDefenseImportValue(
            TechStageModel.Create(SettlementTechStageLevel.IndustrialMid)), "full mid-stage composition import including cover");
        Equal(166240, OrdinarySettlementPopulation.EstimateDefenseImportValue(
            TechStageModel.Create(SettlementTechStageLevel.IndustrialLate)), "full late-stage composition import including cover");
        foreach (PhysicalSettlementProfile profile in new[] { P2ProfileCatalog.IndustrialTemperate, P2ProfileCatalog.TribalTemperate })
        foreach (SettlementTechStageLevel level in Enum.GetValues(typeof(SettlementTechStageLevel)))
        foreach (LayoutClimate climate in new[] { Temperate, Cold, Hot })
        {
            SettlementTechStage stage = TechStageModel.Create(level);
            int total = 0, previousCount = 0, previousEquipment = 0, previousImports = 0;
            for (int roll = 0; roll < 100; roll++)
            {
                OrdinarySettlementBudget budget = OrdinarySettlementPopulation.Create(profile, stage, climate,
                    OrdinarySettlementPopulation.WealthPercentForRoll(roll));
                True(budget.Residents >= 4 && budget.Residents <= 12, "ordinary resident bounds");
                True(budget.Residents >= previousCount && budget.EquipmentValue >= previousEquipment &&
                    budget.ImportValue >= previousImports, "population and allowances increase with wealth");
                True((long)budget.Residents * budget.StandardResidentCost <= budget.EquipmentValue,
                    "resident standard kits fit the declared equipment allowance");
                previousCount = budget.Residents;
                previousEquipment = budget.EquipmentValue;
                previousImports = budget.ImportValue;
                total += budget.Residents;
            }
            Equal(700, total, "exact expected mean seven across all wealth rolls");
            OrdinarySettlementBudget average = OrdinarySettlementPopulation.Create(profile, stage, climate, 100);
            Equal(7, average.Residents, "normal wealth supports seven");
            Equal(profile.Budget.ImportValueLimit, average.BaseImportValue, "normal wealth retains base import budget");
            bool industrialDefense = profile.Has(SettlementCapability.Electricity) &&
                stage.Level >= SettlementTechStageLevel.IndustrialMid;
            int fullDefenseReserve = industrialDefense ? OrdinarySettlementPopulation.EstimateDefenseImportValue(stage) : 0;
            int defenseReserve = Math.Min(fullDefenseReserve,
                (int)Math.Round(average.BaseImportValue * OrdinarySettlementPopulation.P4ETurretImportReservePercent /
                    100.0, MidpointRounding.AwayFromZero));
            Equal(defenseReserve, average.DefenseImportReserveValue, "turret imports reserve up front within the approved wealth cap");
            Equal((int)Math.Round(average.BaseImportValue * average.DefenseAllowancePercent / 100.0,
                MidpointRounding.AwayFromZero) + defenseReserve, average.ImportValue,
                "ordinary import allowance and battery reserve are both included");
            Equal(industrialDefense ? OrdinarySettlementPopulation.EstimateDefenseReserveWatts(stage) : 0, average.DefenseReserveWatts,
                "dispatchable reserve matches maximum crossing composition for tech stage");
            Equal(4, OrdinarySettlementPopulation.Create(profile, stage, climate, 60).Residents, "poor supports four");
            Equal(12, OrdinarySettlementPopulation.Create(profile, stage, climate, 180).Residents, "rich capped at twelve");
        }
        Equal(4, OrdinarySettlementPopulation.AffordableResidents(400, 100), "minimum affordability boundary");
        Equal(7, OrdinarySettlementPopulation.AffordableResidents(799, 100), "no fractional funded resident");
        Equal(12, OrdinarySettlementPopulation.AffordableResidents(100000, 100), "rich cap");
        bool rejected = false;
        try { OrdinarySettlementPopulation.AffordableResidents(399, 100); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        True(rejected, "insufficient allowance is not silently inflated to fund four");
    }

    private static void OrdinaryPopulationPersistence()
    {
        foreach (ulong seed in Enumerable.Range(0, 1000).Select(i => (ulong)i))
        {
            int selected = OrdinarySettlementPopulation.SelectWealthPercent(seed);
            Equal(selected, OrdinarySettlementPopulation.SelectWealthPercent(seed), "seed replay");
            Equal(selected, OrdinarySettlementPopulation.ResolveSavedWealth(-1, false, seed), "new identity selects once");
            Equal(selected, OrdinarySettlementPopulation.ResolveSavedWealth(selected, true, seed + 1000),
                "saved allowance wins over changed seed");
            Equal(0, OrdinarySettlementPopulation.ResolveSavedWealth(-1, true, seed), "generated legacy plan keeps four");
            Equal(0, OrdinarySettlementPopulation.ResolveSavedWealth(0, false, seed), "legacy marker survives reload");
        }
        Equal(9, Enumerable.Range(0, 1000).Select(i => OrdinarySettlementPopulation.SelectWealthPercent((ulong)i))
            .Distinct().Count(), "all wealth bands occur across stable settlement seeds");
        Equal(4, LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate, Temperate).Residents,
            "historical fixed-profile fixtures remain unchanged");
        Equal(0, LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate, Temperate).StockpileRoomCapacityTarget,
            "legacy stockpile packing remains unchanged");
    }

    private static void OrdinaryPopulationLayouts()
    {
        foreach (PhysicalSettlementProfile profile in new[] { P2ProfileCatalog.IndustrialTemperate, P2ProfileCatalog.TribalTemperate })
        foreach (int wealth in new[] { 60, 100, 180 })
        {
            SettlementTechStage stage = TechStageModel.Create(profile.Has(SettlementCapability.Electricity)
                ? SettlementTechStageLevel.IndustrialLate : SettlementTechStageLevel.Neolithic);
            OrdinarySettlementBudget budget = OrdinarySettlementPopulation.Create(profile, stage, Temperate, wealth);
            LayoutRequirements requirements = LayoutRequirements.FromProfile(profile, Temperate, stage: stage, populationBudget: budget);
            Equal(budget.Residents, requirements.Residents, "requirements use affordable roster");
            Equal(budget.EquipmentValue, requirements.EquipmentValueLimit, "no replacement equipment allowance");
            Equal(budget.ImportValue, requirements.ImportValueLimit, "shared wealth applies to import allowance");
            Equal(budget.DefenseImportReserveValue, requirements.DefenseImportReserveValue,
                "requirements carry the battery import reserve separately");
            Equal(budget.ImportValue - budget.DefenseImportReserveValue, requirements.BaseImportValueLimit,
                "base materials cannot spend the turret import reserve");
            Equal((int)Math.Ceiling(budget.Residents * profile.DailyNutritionPerResident /
                profile.EffectiveNutritionPerCropCellPerDay), requirements.FieldCells, "crop demand covers actual population");
            ProceduralLayoutInput input = Input(Plains(180), requirements, 1, new LayoutFortificationReservation(1));
            ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
            Accepted(result);
            if (budget.DefenseReserveWatts > 0)
            {
                True(result.Layout.Power != null, "electrified population layout keeps a power plan");
                Equal(budget.DefenseReserveWatts, result.Layout.Power.DefenseReserveWatts,
                    "power plan retains the stage's full-crossing battery reserve");
            }
            else True(result.Layout.Power == null || result.Layout.Power.DefenseReserveWatts == 0,
                "primitive layouts do not reserve turret generator watts");
            True(result.Layout.Materials.OrdinaryImportValue <=
                requirements.BaseImportValueLimit, "base materials leave the turret import reserve available");
            Equal(0, ProceduralLayoutValidator.Validate(input, result.Layout).Count, "complete final joint-site layout");
            Equal(1, result.Layout.Corridors.Count(c => c.Main), "one principal street, with a retained field branch");
            Equal(budget.Residents, result.Layout.AllFixtures.Count(f => f.Kind == LayoutFixtureKind.Bed), "bed for every resident");
            True(result.Layout.FieldWall.Fields.Count >= requirements.FieldCells, "attached unroofed field covers population");
            True(result.Layout.Notes.Any(n => n.StartsWith("fieldApproachReserved=")), "field attachment protected before room packing");
            SettlementLoadout loadout = LayoutLoadoutModel.Build(input, result.Layout);
            Equal(budget.Residents, loadout.Residents.Count, "one equipment kit per resident");
            Equal(0, ProceduralLayoutValidator.ValidateLoadout(input, result.Layout, loadout).Count, "finite legal population equipment");
            Console.WriteLine("  ordinary population " + profile.Id + " wealth=" + wealth + " residents=" + budget.Residents +
                " equipment=" + budget.EquipmentValue + " imports=" + budget.ImportValue + " fields=" + result.Layout.FieldWall.Fields.Count +
                " powerReserve=" + (result.Layout.Power?.DefenseReserveWatts ?? 0) + " generators=" + (result.Layout.Power?.Generators ?? 0) +
                " defenseGenerators=" + (result.Layout.Power?.DefenseGenerators ?? 0) + " batteries=" + (result.Layout.Power?.Batteries ?? 0));
        }
    }

    private static void OrdinaryPopulationReservation()
    {
        TerrainSnapshot terrain = Plains(200);
        int previousSide = 0;
        foreach (int wealth in new[] { 60, 100, 180 })
        {
            OrdinarySettlementBudget budget = OrdinarySettlementPopulation.Create(P2ProfileCatalog.IndustrialTemperate,
                TechStageModel.Create(SettlementTechStageLevel.IndustrialLate), Temperate, wealth);
            ProceduralLayoutInput input = Input(terrain, LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate,
                Temperate, stage: TechStageModel.Create(SettlementTechStageLevel.IndustrialLate), populationBudget: budget), 1);
            int requiredArea = input.Equipment.Demand.MinimumRoomArea * 3 / 2;
            P4BJointSiteResult sites = P4BJointSitePlanner.Find(terrain, input.Requirements.FieldCells,
                requiredArea, input.Fortification.PlacementMargin, 1234, maxCandidates: 2,
                maxEvaluations: 128, residents: budget.Residents);
            True(sites.Candidates.Count > 0, "initial reservation available");
            int side = sites.Candidates[0].Compound.Width;
            int expected = Math.Max(38, (int)Math.Ceiling(Math.Sqrt(requiredArea)) + (requiredArea >= 500 ? 22 : 14))
                + 2 * Math.Max(0, budget.Residents - 4);
            Equal(expected, side, "initial extent includes facilities plus population circulation allowance");
            True(side > previousSide, "reserve grows before packing rather than after a failed pack");
            True(sites.Evaluated <= 2 * 128 && sites.FieldOriginsScanned <= 2 * terrain.Width * terrain.Height,
                "coarse/refined preflight bounded independently of room repair");
            previousSide = side;
            foreach (LayoutRoomKind kind in new[] { LayoutRoomKind.Storage, LayoutRoomKind.Freezer })
            {
                LayoutRoomDemand[] rooms = input.Equipment.Demand.Rooms.Where(r => r.Kind == kind).ToArray();
                True(rooms.All(r => r.Capacity <= LayoutRequirements.StockpileRoomStacksTarget), "ordinary stockpile rectangles stay packable");
                True(rooms.Sum(r => r.Capacity) >= 6 * budget.Residents, "splitting does not discard storage demand");
            }
        }
    }

    private static void OrdinaryPopulationClutter()
    {
        TerrainSnapshot terrain = Build(160, 160, (x, z) =>
        {
            uint cluster = (uint)((x / 3) * 73856093 ^ (z / 3) * 19349663);
            cluster ^= cluster >> 13;
            cluster *= 1274126177u;
            if (cluster % 1000 < 46) return TerrainTraits.NaturalRock | TerrainTraits.Occupied |
                TerrainTraits.SupportsHeavy | TerrainTraits.Roofed;
            uint water = (uint)((x / 5) * 19349663 ^ (z / 5) * 83492791);
            water ^= water >> 13;
            if (water % 1000 < 12) return DeepWater;
            uint point = (uint)(x * 19349663 ^ z * 83492791);
            point ^= point >> 13;
            if (point % 1000 < 19) return Ground | TerrainTraits.Occupied;
            return point % 1000 < 460 ? Ground | TerrainTraits.ClearablePlant : Ground;
        });
        string before = terrain.Digest;
        PhysicalSettlementProfile profile = P2ProfileCatalog.IndustrialTemperate;
        LayoutClimate climate = new LayoutClimate(-8, 33);
        SettlementTechStage stage = TechStageModel.Create(SettlementTechStageLevel.IndustrialLate);
        foreach (int wealth in new[] { 60, 100, 180 })
        foreach (int layers in new[] { 1, 2, 3 })
        {
            OrdinarySettlementBudget budget = OrdinarySettlementPopulation.Create(profile, stage, climate, wealth);
            LayoutRequirements needs = LayoutRequirements.FromProfile(profile, climate, stage: stage, populationBudget: budget);
            LayoutLocalResources resources = new LayoutLocalResources(30000, new[] { "BlocksGranite", "BlocksMarble" },
                steamGeyserCells: new[] { new GridPoint(70, 32), new GridPoint(58, 69) });
            ProceduralLayoutInput input = null;
            ProceduralLayoutResult result = null;
            string fallback = "none";
            // Match production: optional exterior layers may fall back, but
            // never lower population, facilities, crops or equipment budgets.
            for (int effectiveLayers = layers; effectiveLayers >= 1; effectiveLayers--)
            {
                input = new ProceduralLayoutInput(needs, terrain, 0xE615D0C09671DAF4UL,
                    CoreCatalog, new LayoutFortificationReservation(effectiveLayers), budget: new LayoutSearchBudget(), resources: resources);
                result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
                if (result.Success) break;
                fallback = result.RejectionCode;
            }
            Accepted(result);
            Equal(budget.Residents, result.Layout.AllFixtures.Count(f => f.Kind == LayoutFixtureKind.Bed),
                "flat clutter does not shrink the resident roster");
            Equal(0, ProceduralLayoutValidator.Validate(input, result.Layout).Count, "all facilities and routes fit clutter");
            True(result.Stats.EvaluationCount <= 400000, "upfront geometry fixes fit the original search bound");
            True(result.Layout.Notes.Any(n => n.StartsWith("fieldApproachReserved=")), "reserved attachment survives the full pack");
            Equal(before, terrain.Digest, "read-only planning");
            Console.WriteLine("  ordinary clutter residents=" + budget.Residents + " targetLayers=" + layers +
                " effectiveLayers=" + input.Fortification.WallLayers + " layerFallback=" + fallback + " " + result.Stats);
        }
    }

    private static void OrdinaryPopulationLayeredFrontage()
    {
        SettlementTechStage stage = TechStageModel.Create(SettlementTechStageLevel.IndustrialLate);
        OrdinarySettlementBudget budget = OrdinarySettlementPopulation.Create(P2ProfileCatalog.IndustrialTemperate,
            stage, Temperate, 180);
        LayoutRequirements needs = LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate,
            Temperate, stage: stage, populationBudget: budget);
        foreach (int layers in new[] { 2, 3 })
        {
            ProceduralLayoutInput input = Input(Plains(180), needs, 1, new LayoutFortificationReservation(layers));
            ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
            Accepted(result);
            Equal(12, result.Layout.AllFixtures.Count(f => f.Kind == LayoutFixtureKind.Bed), "layered frontage does not shrink population");
            True(P4BPerimeterDepth.Minimum(Plains(180), result.Layout) >= layers, "every breach route crosses all layers");
            Equal(0, ProceduralLayoutValidator.Validate(input, result.Layout).Count, "layered public/service/field paths remain legal");
            True(result.Layout.Notes.Any(n => n.StartsWith("fieldApproachReserved=")), "frontage was reserved before packing");
            GridPoint[] occupied = result.Layout.Enclosure.Concat(result.Layout.Rooms.SelectMany(r => r.Bounds.Points()))
                .Concat(result.Layout.PowerYards.SelectMany(y => y.Cells))
                .Concat(result.Layout.AllFixtures.SelectMany(f => f.Cells)).ToArray();
            string diagnostic;
            True(P4BFieldWallPlanner.PlanJoint(input, result.Layout, occupied, out diagnostic) != null,
                "reserved walking lanes are not physical crossing obstacles: " + diagnostic);
            ProceduralLayout obstructed = result.Layout.WithAddedObstacles(result.Layout.Gates.Select(g =>
                new GridPoint(g.Cell.X + g.Outward.X, g.Cell.Z + g.Outward.Z)));
            True(P4BFieldWallPlanner.PlanJoint(input, obstructed, occupied.Concat(obstructed.ExtraObstacles), out diagnostic) == null,
                "a real obstacle on the same reserved crossing still blocks attachment");
            True(diagnostic.Contains("compound_field_crossing_blocked"), "physical crossing rejection stays explicit: " + diagnostic);
            Console.WriteLine("  ordinary layered frontage residents=12 layers=" + layers + " " + result.Stats);
        }
    }

    private static void OrdinaryPopulationStocks()
    {
        SettlementTechStage stage = TechStageModel.Create(SettlementTechStageLevel.IndustrialLate);
        foreach (int wealth in new[] { 60, 100, 120, 180 })
        {
            OrdinarySettlementBudget budget = OrdinarySettlementPopulation.Create(P2ProfileCatalog.IndustrialTemperate,
                stage, Temperate, wealth);
            ProceduralLayoutInput input = Input(Plains(180), LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate,
                Temperate, stage: stage, populationBudget: budget), 1, new LayoutFortificationReservation(1));
            ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
            Accepted(result);
            foreach (LayoutRoomKind kind in new[] { LayoutRoomKind.Storage, LayoutRoomKind.Freezer })
            {
                LayoutRoom[] rooms = result.Layout.Rooms.Where(r => r.Kind == kind).ToArray();
                GridPoint[] shelves = rooms.SelectMany(r => r.Fixtures).Where(f => f.Kind == LayoutFixtureKind.Shelf)
                    .SelectMany(f => f.Cells).ToArray();
                var cells = LayoutStockCells.ForRooms(result.Layout.Rooms, kind);
                True(shelves.Length > 0 && cells.Take(shelves.Length).SequenceEqual(shelves), "legal shelves are used first");
                Equal(cells.Count, cells.Distinct().Count(), "no duplicated stock cells");
                foreach (LayoutRoom room in rooms)
                {
                    True(cells.Any(room.Interior.Contains), "every split room contributes stock capacity");
                    True(!cells.Intersect(room.Fixtures.SelectMany(f => f.Access)).Any(), "stock never occupies fixture access");
                }
                True(cells.All(c => rooms.Any(r => r.Interior.Contains(c))), "stock never overflows into unrelated rooms");
                // Racked rooms: shelf stacks carry most capacity, so demand
                // needs no more rooms than the stacks target implies.
                int shelfStacks = shelves.Length * 3;
                True(shelfStacks * 2 >= rooms.Sum(r => r.Capacity), kind + " capacity is mostly racked shelves");
                int demanded = input.Equipment.Demand.Rooms.Where(r => r.Kind == kind).Sum(r => r.Capacity);
                True(rooms.Length <= (demanded + LayoutRequirements.StockpileRoomStacksTarget - 1) / LayoutRequirements.StockpileRoomStacksTarget,
                    kind + " rooms follow the stacks target: rooms=" + rooms.Length + " demand=" + demanded);
            }
            SettlementLoadout loadout = LayoutLoadoutModel.Build(input, result.Layout);
            Func<string, int> limit = name => CoreCatalog.Defs.Single(d => d.DefName == name).StackLimit;
            int stacks = loadout.Components.Concat(loadout.Armoury).Sum(item =>
                (item.Count + limit(item.DefName) - 1) / limit(item.DefName));
            if (result.Layout.Power?.FuelStock > 0)
                stacks += (result.Layout.Power.FuelStock + limit(result.Layout.Power.FuelDef) - 1) /
                    limit(result.Layout.Power.FuelDef);
            True(LayoutStockCells.ForRooms(result.Layout.Rooms, LayoutRoomKind.Storage).Count >= stacks,
                "all finite loadout/fuel stocks fit even without counting extra shelf slots");
        }
    }
}
