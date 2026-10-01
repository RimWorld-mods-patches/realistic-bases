using System;
using System.Linq;
using RealisticBases.Planning;

internal static partial class Program
{
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
