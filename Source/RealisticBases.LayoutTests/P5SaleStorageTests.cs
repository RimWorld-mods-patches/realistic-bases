using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RealisticBases.Planning;

// P5 physical trade stock: the planner sizes storage for the canonical offer
// in addition to operating reserves (docs/p5-encounter-plan.md, P5.3).
internal static partial class Program
{
    private static void P5SaleStorageSizing()
    {
        SettlementTechStage stage = TechStageModel.Create(SettlementTechStageLevel.IndustrialLate);
        OrdinarySettlementBudget budget = OrdinarySettlementPopulation.Create(P2ProfileCatalog.IndustrialTemperate, stage, Temperate, 100);
        LayoutRequirements plain = LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate, Temperate, stage: stage, populationBudget: budget);
        LayoutRequirements none = LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate, Temperate, stage: stage, populationBudget: budget,
            saleStorageStacks: 0, salePerishableStacks: 0);
        LayoutRequirements sale = LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate, Temperate, stage: stage, populationBudget: budget,
            saleStorageStacks: 30, salePerishableStacks: 12);
        Equal(plain.Digest, none.Digest, "no offer keeps the historical requirements digest");
        True(sale.Digest != plain.Digest, "the offer is a planning input");
        Func<LayoutRequirements, LayoutRoomKind, int> capacity = (r, kind) =>
            Input(Plains(180), r, 1).Equipment.Demand.Rooms.Where(d => d.Kind == kind).Sum(d => d.Capacity);
        // Generator fuel cells are re-derived with the larger estimate, so
        // storage grows by at least the sale stacks.
        True(capacity(sale, LayoutRoomKind.Storage) >= capacity(plain, LayoutRoomKind.Storage) + 30, "ordinary and hazardous sale stacks add storage");
        Equal(capacity(plain, LayoutRoomKind.Freezer) + 12, capacity(sale, LayoutRoomKind.Freezer), "perishable sale stacks add freezer space");
        ProceduralLayoutInput input = Input(Plains(180), sale, 1, new LayoutFortificationReservation(1));
        ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
        Accepted(result);
        Equal(0, ProceduralLayoutValidator.Validate(input, result.Layout).Count, "sized settlement validates");
        int storageCells = P5StockSlots(input, result.Layout, LayoutRoomKind.Storage);
        int foodCells = P5StockSlots(input, result.Layout, LayoutRoomKind.Freezer, LayoutRoomKind.Pantry);
        True(storageCells >= capacity(sale, LayoutRoomKind.Storage), "storage cells cover operating plus sale stacks: " + storageCells);
        True(foodCells >= capacity(sale, LayoutRoomKind.Freezer), "food cells cover operating plus perishable sale: " + foodCells);
        bool rejected = false;
        try { new LayoutRequirements(4, Industrial, HousingStandard.Barracks, Temperate, 0, 0, 0, compactQuestCamp: true, saleStorageStacks: 1); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        True(rejected, "quest camps never materialize an ordinary offer");
        LayoutRequirements retained = LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate, Temperate,
            stage: stage, populationBudget: budget, saleStorageStacks: 30, salePerishableStacks: 12,
            retainedStorageStacks: 3, retainedPerishableStacks: 25);
        True(retained.Digest != sale.Digest, "retained original Things are explicit planning inputs");
        True(capacity(retained, LayoutRoomKind.Storage) >= capacity(sale, LayoutRoomKind.Storage) + 3,
            "operating archive adds storage without reducing sale storage");
        Equal(capacity(sale, LayoutRoomKind.Freezer) + 25, capacity(retained, LayoutRoomKind.Freezer),
            "retained perishables keep their own food-storage demand");
        ProceduralLayoutInput retainedInput = Input(Plains(180), retained, 1, new LayoutFortificationReservation(1));
        ProceduralLayoutResult retainedPlan = ProceduralLayoutPlanner.Plan(retainedInput, requireJointSite: true);
        Accepted(retainedPlan);
        True(P5StockSlots(retainedInput, retainedPlan.Layout, LayoutRoomKind.Storage) >= capacity(retained, LayoutRoomKind.Storage),
            "physical storage covers both authorities");
        bool questRetainedRejected = false;
        try { new LayoutRequirements(4, Industrial, HousingStandard.Barracks, Temperate, 0, 0, 0,
            compactQuestCamp: true, retainedStorageStacks: 1); }
        catch (ArgumentOutOfRangeException) { questRetainedRejected = true; }
        True(questRetainedRejected, "quest camps reject retained ordinary operating stock");
        Console.WriteLine("  sale sizing storageCells=" + storageCells + " foodCells=" + foodCells + " " + result.Stats);
    }

    private static int P5StockSlots(ProceduralLayoutInput input, ProceduralLayout layout, params LayoutRoomKind[] kinds)
    {
        var rooms = layout.Rooms.Where(r => kinds.Contains(r.Kind)).ToArray();
        var shelves = rooms.SelectMany(r => r.Fixtures).Where(f => f.Kind == LayoutFixtureKind.Shelf)
            .SelectMany(f => f.Cells.Select(c => new { Cell = c, Slots = input.Catalog.Defs.Single(d => d.DefName == f.DefName).MaxItemsInCell }))
            .ToDictionary(p => p.Cell, p => p.Slots);
        return LayoutStockCells.ForRooms(rooms, kinds).Sum(c => shelves.TryGetValue(c, out int slots) ? slots : 1);
    }

    private static void P5NativeTradeStorage()
    {
        var cells = new List<TerrainCell>();
        string[] rows = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "p5-native-trade-rejected-traits.rle"));
        Equal(250, rows.Length, "captured native settlement height");
        foreach (string row in rows)
        {
            int width = 0;
            foreach (string token in row.Split(','))
            {
                string[] parts = token.Split('*');
                int count = int.Parse(parts[1]); width += count;
                for (int i = 0; i < count; i++) cells.Add(new TerrainCell((TerrainTraits)Convert.ToUInt16(parts[0], 16)));
            }
            Equal(250, width, "captured native settlement width");
        }
        var terrain = new TerrainSnapshot(250, 250, cells, "p5-native-trade-CDF248A8E1087ABF");
        var climate = new LayoutClimate(-2, 38);
        var stage = TechStageModel.Create(SettlementTechStageLevel.IndustrialEarly);
        var budget = OrdinarySettlementPopulation.Create(P2ProfileCatalog.IndustrialTemperate, stage, climate, 75);
        var req = LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate, climate, stage: stage,
            populationBudget: budget, saleStorageStacks: 111, salePerishableStacks: 13);
        Equal(5, req.Residents, "keep the actual funded roster");
        var input = new ProceduralLayoutInput(req, terrain, 0x8DD10F58FC412C58UL, RuntimeFlatCatalogFixture.Build(),
            new LayoutFortificationReservation(1), resources: new LayoutLocalResources(63713, new[] { "BlocksGranite", "BlocksMarble" }));
        var result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
        Accepted(result);
        True(P5StockSlots(input, result.Layout, LayoutRoomKind.Storage) >=
            result.Demand.Rooms.Where(r => r.Kind == LayoutRoomKind.Storage).Sum(r => r.Capacity), "whole ordinary/hazardous offer fits racks");
        True(P5StockSlots(input, result.Layout, LayoutRoomKind.Freezer, LayoutRoomKind.Pantry) >=
            result.Demand.Rooms.Where(r => r.Kind == LayoutRoomKind.Freezer || r.Kind == LayoutRoomKind.Pantry).Sum(r => r.Capacity),
            "whole perishable offer fits food storage");
        Console.WriteLine("  native traded tile layout=" + result.Layout.Digest + " storageRooms=" +
            result.Layout.Rooms.Count(r => r.Kind == LayoutRoomKind.Storage) + " " + result.Stats);
    }
}
