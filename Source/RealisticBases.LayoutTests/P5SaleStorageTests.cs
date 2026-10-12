using System;
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
        int storageCells = LayoutStockCells.ForRooms(result.Layout.Rooms, LayoutRoomKind.Storage).Count;
        int foodCells = LayoutStockCells.ForRooms(result.Layout.Rooms, LayoutRoomKind.Freezer, LayoutRoomKind.Pantry).Count;
        True(storageCells >= capacity(sale, LayoutRoomKind.Storage), "storage cells cover operating plus sale stacks: " + storageCells);
        True(foodCells >= capacity(sale, LayoutRoomKind.Freezer), "food cells cover operating plus perishable sale: " + foodCells);
        bool rejected = false;
        try { new LayoutRequirements(4, Industrial, HousingStandard.Barracks, Temperate, 0, 0, 0, compactQuestCamp: true, saleStorageStacks: 1); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        True(rejected, "quest camps never materialize an ordinary offer");
        Console.WriteLine("  sale sizing storageCells=" + storageCells + " foodCells=" + foodCells + " " + result.Stats);
    }
}
