using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

internal static partial class Program
{
    private static void P4ESeatsAndMedicalShelves()
    {
        HashSet<string> rotations = new HashSet<string>();
        foreach (ulong seed in Enumerable.Range(1, 12).Select(i => (ulong)i))
        {
            ProceduralLayoutResult result = Plan(Plains(140), LayoutRequirements.FromPopulation(7, Industrial, Cold,
                stage: TechStageModel.Create(SettlementTechStageLevel.IndustrialLate)), seed);
            Accepted(result);
            foreach (LayoutRoom room in result.Layout.Rooms)
            {
                LayoutFixture[] chairs = room.Fixtures.Where(f => f.Kind == LayoutFixtureKind.Chair).ToArray();
                Equal(chairs.Length, chairs.SelectMany(c => c.Cells).Distinct().Count(), "unique chair cells");
                foreach (LayoutFixture bench in room.Fixtures.Where(f => f.Kind == LayoutFixtureKind.Workbench || f.Kind == LayoutFixtureKind.Stove))
                {
                    LayoutFixture chair = chairs.SingleOrDefault(c => c.Cells.Contains(bench.Access.Single()));
                    True(chair != null, "work seat " + bench.DefName);
                    Equal(bench.Rotation, chair.Rotation, "work chair faces bench");
                    rotations.Add(bench.Rotation);
                }
                foreach (LayoutFixture table in room.Fixtures.Where(f => f.Kind == LayoutFixtureKind.Table))
                    foreach (GridPoint seat in table.Access)
                    {
                        LayoutFixture chair = chairs.Single(c => c.Cells.Contains(seat));
                        GridPoint looksAt = new GridPoint(seat.X - chair.Facing.X, seat.Z - chair.Facing.Z);
                        True(table.Cells.Contains(looksAt), "dining chair faces adjacent table cell");
                    }
            }
            LayoutRoom medical = result.Layout.Rooms.Single(r => r.Kind == LayoutRoomKind.Medical);
            LayoutFixture shelf = medical.Fixtures.Single(f => f.Kind == LayoutFixtureKind.Shelf);
            Equal("ShelfSmall", shelf.DefName, "prefer small medical shelf");
            True(LayoutStockCells.ForRooms(result.Layout.Rooms, LayoutRoomKind.Medical).Take(shelf.Cells.Count).SequenceEqual(shelf.Cells), "medical shelf first");
            True(!shelf.Cells.Contains(medical.Door) && !medical.Fixtures.SelectMany(f => f.Access).Intersect(shelf.Cells).Any(), "shelf avoids working/door cells");
        }
        Equal(4, rotations.Count, "all four workstation orientations");
        foreach (SettlementTechStageLevel level in new[] { SettlementTechStageLevel.Neolithic, SettlementTechStageLevel.Medieval })
        {
            ProceduralLayoutResult result = Plan(Plains(140), LayoutRequirements.FromPopulation(7, Tribal, Temperate,
                stage: TechStageModel.Create(level)), 33);
            Accepted(result);
            Equal(level != SettlementTechStageLevel.Neolithic, result.Layout.Rooms.Where(r => r.Kind == LayoutRoomKind.Medical)
                .Any(r => r.Fixtures.Any(f => f.Kind == LayoutFixtureKind.Shelf)), "legal shelf stage");
        }
        TechCatalog withoutSmall = new TechCatalog("no-small-shelf", CoreCatalog.Defs.Where(d => d.DefName != "ShelfSmall"), CoreCatalog.Research);
        LayoutEquipment fallback = LayoutEquipment.Resolve(LayoutRequirements.FromPopulation(7, Industrial, Cold), withoutSmall, null, 2, 9);
        Equal("Shelf", fallback.Kit.DefFor(SettlementRole.MedicalStorage).DefName, "bounded legal full-shelf fallback");
    }

    private static void P4ELaboratoryStageMatrix()
    {
        foreach (SettlementTechStageLevel level in TechStageModel.Levels)
        {
            LayoutRequirements requirements = new LayoutRequirements(7, level >= SettlementTechStageLevel.IndustrialEarly ? Industrial : Tribal,
                HousingStandard.Barracks, Temperate, 0, 500, 0, stage: TechStageModel.Create(level));
            LayoutEquipment equipment = LayoutEquipment.Resolve(requirements, CoreCatalog, null, 2, 77);
            True(equipment.Rejection == null, "equipment " + level + ":" + equipment.Rejection);
            LayoutRoomDemand lab = equipment.Demand.Rooms.Single(r => r.Kind == LayoutRoomKind.Laboratory);
            Equal(1, lab.Capacity, "single lab bench");
            foreach (GridRect size in lab.CandidateSizes)
            {
                LayoutFurnishResult furnished = LayoutFurnishing.Furnish(lab.Kind, size.Width, size.Height, (size.Width + 1) / 2,
                    lab.Capacity, new LayoutFurnishContext(equipment.Kit, false, false, null));
                True(furnished.Success, "lab furnishings " + level);
                LayoutLocalFixture bench = furnished.Fixtures.Single(f => f.Role == SettlementRole.Research);
                Equal(level >= SettlementTechStageLevel.IndustrialMid ? "HiTechResearchBench" : "SimpleResearchBench", bench.DefName, "stage bench");
                Equal(1, bench.Access.Count, "working cell");
                True(furnished.Fixtures.Any(f => f.Role == SettlementRole.Light), "lab light");
                Equal(level >= SettlementTechStageLevel.IndustrialLate ? 1 : 0,
                    furnished.Fixtures.Count(f => f.Kind == LayoutFixtureKind.ResearchAnalyzer), "stage analyzer");
                if (level >= SettlementTechStageLevel.IndustrialLate)
                    True(furnished.Watts >= 450, "bench/analyzer powered demand");
            }
            LayoutRequirements camp = new LayoutRequirements(7, Industrial, HousingStandard.Barracks, Temperate, 0, 500, 0,
                stage: TechStageModel.Create(level), compactQuestCamp: true);
            True(!LayoutEquipment.Resolve(camp, CoreCatalog, null, 2, 77).Demand.Rooms.Any(r => r.Kind == LayoutRoomKind.Laboratory), "compact camp excluded");
        }
        Equal(11, (int)LayoutRoomKind.Laboratory, "room enum appended");
        Equal(19, (int)SettlementRole.Research, "role enum appended");
    }

    private static void P4EOccupiedRoomClimate()
    {
        foreach (LayoutClimate climate in new[] { Cold, Hot, Temperate })
        {
            LayoutRequirements requirements = new LayoutRequirements(7, Industrial, HousingStandard.Barracks, climate, 0, 500, 0,
                stage: TechStageModel.Create(SettlementTechStageLevel.IndustrialLate));
            LayoutEquipment equipment = LayoutEquipment.Resolve(requirements, CoreCatalog, null, 2, 77);
            foreach (LayoutRoomDemand room in equipment.Demand.Rooms)
            {
                GridRect size = room.CandidateSizes.First();
                LayoutFurnishResult furnished = LayoutFurnishing.Furnish(room.Kind, size.Width, size.Height, (size.Width + 1) / 2,
                    room.Capacity, new LayoutFurnishContext(equipment.Kit, climate.HeatingRequired, climate.CoolingRequired, null, room.Batteries));
                True(furnished.Success, "climate furnishing " + room.Kind + ":" + furnished.Failure);
                if (LayoutDemandModel.IsLiving(room.Kind))
                {
                    Equal(climate.HeatingRequired, furnished.Fixtures.Any(f => f.Role == SettlementRole.Heat), "occupied heat " + room.Kind);
                    Equal(climate.CoolingRequired, furnished.Fixtures.Any(f => f.Role == SettlementRole.Cool), "occupied cool " + room.Kind);
                }
                else if (room.Kind != LayoutRoomKind.Freezer)
                    True(!furnished.Fixtures.Any(f => f.Role == SettlementRole.Heat || f.Role == SettlementRole.Cool), "storage/power not comfort rooms");
            }
            True(equipment.Demand.EstimatedLoadWatts > 0 && equipment.Demand.PowerGenerators > 0, "load budget recomputed");
        }
        True(LayoutDemandModel.IsLiving(LayoutRoomKind.Kitchen) && LayoutDemandModel.IsLiving(LayoutRoomKind.Workshop) &&
            LayoutDemandModel.IsLiving(LayoutRoomKind.Laboratory), "all occupied work rooms");
    }
}
