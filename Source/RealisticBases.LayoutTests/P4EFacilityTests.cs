using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

internal static partial class Program
{
    private static void P4EMortarMagazine()
    {
        var rotations = new HashSet<string>();
        int seen = 0;
        foreach (ulong seed in Enumerable.Range(1, 12).Select(i => (ulong)i))
        {
            ProceduralLayoutInput input = Input(Plains(160), LayoutRequirements.FromPopulation(7, Industrial, Temperate,
                stage: TechStageModel.Create(SettlementTechStageLevel.IndustrialLate)), seed);
            ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input);
            Accepted(result);
            if (result.Layout.Court == null) continue;
            seen++;
            LayoutRoom store = result.Layout.Rooms.Single(r => r.Kind == LayoutRoomKind.MortarMagazine);
            Equal(1, store.Interior.Points().Count(), "one usable magazine cell");
            Equal("ShelfSmall", store.Fixtures.Single().DefName, "Core one-cell shelf");
            Equal(7, store.Bounds.Points().Count(c => store.IsEdge(c) && !c.Equals(store.Door)), "seven enclosure walls");
            True(store.Bounds.Points().All(result.Layout.ConstructedRoof.Contains), "only the store is roofed");
            True(!result.Layout.Court.Cells.Concat(result.Layout.Court.Access).Any(result.Layout.ConstructedRoof.Contains), "gun and operator remain unroofed");
            IReadOnlyList<GridPoint> route = ProceduralLayoutValidator.MagazineLoadingRoute(input, result.Layout);
            True(route.Count > 0 && route.Count <= 2, "entrance at the operating position or one adjacent public step");
            Equal(store.DoorOutside, route.Last(), "loading ends at magazine entrance");
            Contains(ProceduralLayoutValidator.Validate(input, result.Layout.WithAddedObstacles(new[] { store.Door })),
                "room_door_disconnected_mortar-magazine");
            GridPoint stock = store.Interior.Points().Single(), op = result.Layout.Court.Access.Single();
            True(Math.Abs(store.DoorOutside.X - op.X) + Math.Abs(store.DoorOutside.Z - op.Z) <= 1,
                "magazine access is beside the operator, not ten cells away");
            True((stock.X - op.X) * result.Layout.Court.Facing.X + (stock.Z - op.Z) * result.Layout.Court.Facing.Z >= 0,
                "magazine lies on the working side of the mortar");
            True((op.X - store.Door.X) * -store.Facing.X + (op.Z - store.Door.Z) * -store.Facing.Z > 0,
                "door faces back toward the operating position");
            if (seen == 1)
            {
                // A correctly shaped roofed warehouse still fails if moved
                // away from the crew: reproduce the user's placement report.
                GridPoint shift = new GridPoint(result.Layout.Court.Facing.X * 10, result.Layout.Court.Facing.Z * 10);
                GridPoint Move(GridPoint c) => new GridPoint(c.X + shift.X, c.Z + shift.Z);
                LayoutFixture oldShelf = store.Fixtures.Single();
                var farShelf = new LayoutFixture(oldShelf.Kind, oldShelf.DefName, oldShelf.Cells.Select(Move), null,
                    oldShelf.Facing, oldShelf.Blocking, 0, false, store.Id, role: oldShelf.Role, stuff: oldShelf.Stuff);
                var demand = new LayoutRoomDemand(store.Id, store.Kind, 1, store.Unit, new[] { new GridRect(0, 0, 3, 3) });
                var far = new LayoutRoom(demand, new GridRect(store.Bounds.MinX + shift.X, store.Bounds.MinZ + shift.Z, 3, 3),
                    Move(store.Door), store.Facing, 1, new[] { farShelf }, false, new GridPoint[0]);
                ProceduralLayout l = result.Layout;
                var wrong = new ProceduralLayout(l.InputDigest, l.Seed, l.Doctrine, l.MortarCapable, l.Anchor,
                    l.Rooms.Select(r => r.Id == store.Id ? far : r), l.Corridors, l.Court, l.Enclosure, l.BuiltWalls,
                    l.NaturalWalls, l.Gates, l.Excavation, l.RetainedThickRoof, l.ConstructedRoof, l.WallLayers,
                    l.FieldCellsRequired, l.Relationships, l.ExtraObstacles, l.Notes, l.TechStage, l.RolesDigest,
                    l.EquipmentDigest, l.WallStuff, l.Materials, l.Power, l.ExhaustPockets, l.OuterSections,
                    l.JointSite, l.FieldWall, l.Turret, l.ApproachDefense, l.PerimeterReinforcement);
                Contains(ProceduralLayoutValidator.Validate(input, wrong), "mortar_magazine_not_beside_operating_position");
            }
            LayoutFixture court = result.Layout.Court;
            string rotation = court.Rotation; rotations.Add(rotation);
            int x = court.Cells.Min(c => c.X), z = court.Cells.Min(c => c.Z);
            GridPoint origin = new GridPoint(x + (rotation == "South" || rotation == "West" ? 1 : 0),
                z + (rotation == "South" || rotation == "East" ? 1 : 0));
            GridPoint step = rotation == "North" ? new GridPoint(0, -1) : rotation == "South" ? new GridPoint(0, 1) :
                rotation == "East" ? new GridPoint(-1, 0) : new GridPoint(1, 0);
            Equal(court.Access.Single(), new GridPoint(origin.X + step.X, origin.Z + step.Z), "actual rotated Core mortar operating cell");
        }
        True(seen >= 6, "representative flat layouts keep a stocked-magazine-capable mortar: " + seen);
        True(rotations.Count >= 3, "varied operating sides are covered: " + string.Join(",", rotations));
        Console.WriteLine("  magazines=" + seen + "/12 rotations=" + string.Join(",", rotations));
    }

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
