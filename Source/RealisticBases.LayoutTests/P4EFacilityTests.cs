using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

internal static partial class Program
{
    private static void P4ELogisticsSmokeBudget()
    {
        var stage = TechStageModel.Create(SettlementTechStageLevel.IndustrialMid);
        var tiny = LayoutRequirements.FromPopulation(7, Industrial, Temperate, stage: stage, equipmentValueLimit: 1);
        var kit = LayoutEquipment.Resolve(tiny, CoreCatalog, null, 2, 77).Kit;
        var minimal = LayoutLoadoutModel.Build(tiny, kit, CoreCatalog, 77, 1, false);
        True(minimal.Degradations.Contains("utility_dropped"), "fixture reaches the actual utility-drop boundary");
        var req = new LayoutRequirements(7, Industrial, HousingStandard.Barracks, Temperate, 50, 500, 0,
            stage: stage, equipmentValueLimit: minimal.EquipmentValue, logisticsUtilityValueLimit: 300);
        var retained = LayoutLoadoutModel.Build(req, kit, CoreCatalog, 77, 1, false);
        True(retained.Success, "bounded top-up retains otherwise affordable gear and smoke");
        Equal(7, retained.Residents.Count, "no additional resident from utility budget");
        var smoke = retained.Residents.Single(r => r.Role == LoadoutRole.Support).Utility;
        Equal("Apparel_SmokepopBelt", smoke.DefName, "resolved legal support pack retained");
        True(retained.EquipmentValueLimit > req.EquipmentValueLimit &&
            retained.EquipmentValueLimit - req.EquipmentValueLimit <= smoke.Value, "only actual pack value funded");
        True(retained.Diagnostics.Any(d => d.StartsWith("logistics_utility_topup=")), "named allowance visible");
        foreach (var level in new[] { SettlementTechStageLevel.Neolithic, SettlementTechStageLevel.Medieval, SettlementTechStageLevel.IndustrialEarly })
        {
            var lower = LayoutRequirements.FromPopulation(7, level < SettlementTechStageLevel.IndustrialEarly ? Tribal : Industrial,
                Temperate, stage: TechStageModel.Create(level));
            var lowerKit = LayoutEquipment.Resolve(lower, CoreCatalog, null, 2, 77).Kit;
            var lowerLoadout = LayoutLoadoutModel.Build(lower, lowerKit, CoreCatalog, 77, 1, false);
            True(!lowerLoadout.Residents.Any(r => r.Utility?.DefName == "Apparel_SmokepopBelt"), "research capability not granted at " + level);
        }
    }

    private static void P4EDefenseAllowance()
    {
        var post = new[] { new GridPoint(0, 0) };
        Equal("Turret_Autocannon", P4BTurretPlanner.HeavyWeaponOrder(new[] { new GridPoint(20, 0) }, post,
            new string[0], 34.9, 45.9)[0], "near approaches prefer autocannon");
        Equal("Turret_Sniper", P4BTurretPlanner.HeavyWeaponOrder(new[] { new GridPoint(40, 0) }, post,
            new string[0], 34.9, 45.9)[0], "long-only lanes retain uranium");
        Equal("Turret_Sniper", P4BTurretPlanner.HeavyWeaponOrder(new[] { new GridPoint(20, 0), new GridPoint(40, 0) }, post,
            new[] { "Turret_Autocannon" }, 34.9, 45.9)[0], "existing autocannon permits complementary long-range group");
        int autos = 0, slugs = 0;
        foreach (var level in new[] { SettlementTechStageLevel.IndustrialMid, SettlementTechStageLevel.IndustrialLate })
        {
            var stage = TechStageModel.Create(level);
            foreach (int wealth in new[] { 100, 180 })
            {
                foreach (int allowance in new[] { 100, 125, 150 })
                {
                    int guns = 0, groups = 0, guards = 0, value = 0, generators = 0, watts = 0;
                    for (ulong seed = 1; seed <= 3; seed++)
                    {
                        var budget = OrdinarySettlementPopulation.Create(P2ProfileCatalog.IndustrialTemperate, stage, Temperate, wealth, allowance);
                        var baseline = OrdinarySettlementPopulation.Create(P2ProfileCatalog.IndustrialTemperate, stage, Temperate, wealth, 100);
                        Equal(baseline.Residents, budget.Residents, "no roster inflation");
                        Equal(baseline.EquipmentValue, budget.EquipmentValue, "no equipment-budget inflation");
                        var req = LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate, Temperate, stage: stage, populationBudget: budget);
                        Equal(allowance == 100 ? 0 : 960, req.DefenseReserveWatts, "ordinary-only dispatchable reservation");
                        var input = new ProceduralLayoutInput(req, Plains(180), seed, CoreCatalog,
                            new LayoutFortificationReservation(1), resources:
                            new LayoutLocalResources(100000, new[] { "BlocksGranite" }));
                        var result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true); Accepted(result);
                        var layout = result.Layout;
                        True(layout.Materials.ImportValue <= req.ImportValueLimit, "construction and fuel remain funded");
                        if (layout.Power != null)
                        {
                            True(layout.Power.Generators * layout.Power.GeneratorWatts + layout.Power.RenewableWatts -
                                layout.Power.CriticalWatts - layout.Power.NonCriticalWatts >= req.DefenseReserveWatts, "real dispatchable headroom");
                            Equal(LayoutPowerModel.Batteries(layout.Power.CriticalWatts + req.DefenseReserveWatts),
                                layout.Power.Batteries, "defense included in outage buffer");
                            generators += layout.Power.Generators;
                        }
                        if (layout.Turret == null) continue;
                        var staffing = P4BStaffing.Audit(input, layout, layout.Turret);
                        Equal(staffing.Guns, staffing.GuardedGuns, "all guns concurrently guarded");
                        Equal(staffing.Guns, staffing.CloseSupportedGuns, "all guns close-supported");
                        guns += layout.Turret.All.Count(); groups += layout.Turret.Groups.Count();
                        guards += staffing.GuardedGuns; value += layout.Turret.ImportValue; watts += layout.Turret.TotalWatts;
                        autos += layout.Turret.All.Count(t => t.DefName == "Turret_Autocannon");
                        slugs += layout.Turret.All.Count(t => t.DefName == "Turret_Sniper");
                    }
                    Console.WriteLine("  sweep stage=" + level + " wealth=" + wealth + " allowance=" + allowance +
                        " guns=" + guns + " groups=" + groups + " guards=" + guards + " turretValue=" + value +
                        " generators=" + generators + " turretWatts=" + watts);
                }
            }
        }
        True(autos > 0, "autocannons actually selected in resolved eligible layouts");
        Console.WriteLine("  sweep autocannons=" + autos + " uranium=" + slugs);
        foreach (var level in new[] { SettlementTechStageLevel.Neolithic, SettlementTechStageLevel.Medieval, SettlementTechStageLevel.IndustrialEarly })
        {
            bool primitive = level < SettlementTechStageLevel.IndustrialEarly;
            var profile = primitive ? P2ProfileCatalog.TribalTemperate : P2ProfileCatalog.IndustrialTemperate;
            var budget = OrdinarySettlementPopulation.Create(profile, TechStageModel.Create(level), Temperate, 100);
            Equal(100, budget.DefenseAllowancePercent, "primitive/early exclusions");
        }
        Equal(0, LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate, Temperate).DefenseReserveWatts,
            "legacy/native quest profile budgets unchanged without ordinary wealth contract");
    }

    private static void P4EBarrelWarehouse()
    {
        var magazine = new[] { new GridPoint(1, 1) };
        var warehouse = new[] { new GridPoint(9, 9), new GridPoint(1, 1), new GridPoint(9, 10) };
        var barrels = LayoutStockCells.ForArtillerySupply("ReinforcedBarrel", magazine, warehouse);
        Equal(magazine[0], barrels[0], "magazine remains preferred");
        Equal(3, barrels.Count, "bounded warehouse overflow without duplicated cells");
        foreach (string shell in new[] { "Shell_HighExplosive", "Shell_Incendiary" })
            Equal(1, LayoutStockCells.ForArtillerySupply(shell, magazine, warehouse).Count, "ammunition stays in magazine");
    }

    private static void P4EClearedThermalPlants()
    {
        var req = LayoutRequirements.FromPopulation(7, Industrial, new LayoutClimate(-1, 40),
            stage: TechStageModel.Create(SettlementTechStageLevel.IndustrialLate), importValueLimit: 16000);
        var input = Input(Plains(120), req, 15);
        var result = ProceduralLayoutPlanner.Plan(input); Accepted(result);
        var room = result.Layout.Rooms.First(r => r.Fixtures.Any(f => f.Kind == LayoutFixtureKind.WallCooler));
        var cooler = room.Fixtures.First(f => f.Kind == LayoutFixtureKind.WallCooler);
        var reference = new GridPoint(room.Interior.MinX, room.Interior.MinZ);
        var untouched = new GridPoint(0, 0);
        var plants = new HashSet<GridPoint> { reference, cooler.ColdCell.Value, untouched };
        var terrain = new TerrainSnapshot(120, 120, input.Terrain.Points().Select(c => plants.Contains(c)
            ? new TerrainCell(TerrainTraits.Buildable | TerrainTraits.ClearablePlant | TerrainTraits.Fertile | TerrainTraits.SupportsHeavy)
            : input.Terrain[c]), "blocking plants cleared by planned room");
        var partition = LayoutRoomPartition.Build(terrain, result.Layout);
        True(partition.RoomAt(reference) >= 0, "owned room clears a non-standable tree at its reference cell");
        Equal(partition.RoomAt(reference), partition.RoomAt(cooler.ColdCell.Value), "same final served room");
        Equal(-1, partition.RoomAt(untouched), "unowned blocking vegetation remains a separator");
    }

    private static void P4EFlatCoolerTile()
    {
        var cells = new List<TerrainCell>();
        foreach (string row in System.IO.File.ReadAllLines(System.IO.Path.Combine(AppContext.BaseDirectory, "p4e-flat-cooler-traits.rle")))
            foreach (string token in row.Split(','))
            {
                string[] p = token.Split('*');
                for (int n = 0; n < int.Parse(p[1]); n++) cells.Add(new TerrainCell((TerrainTraits)Convert.ToUInt16(p[0], 16)));
            }
        Equal(25600, cells.Count, "same captured map dimensions");
        var climate = new LayoutClimate(-1, 40);
        var stage = TechStageModel.Create(SettlementTechStageLevel.IndustrialLate);
        var budget = OrdinarySettlementPopulation.Create(P2ProfileCatalog.IndustrialTemperate, stage, climate, 150);
        var req = LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate, climate, stage: stage,
            populationBudget: budget, mortarBiasPercent: 100);
        var input = new ProceduralLayoutInput(req, new TerrainSnapshot(160, 160, cells, "live-flat-cooler-93DDC95416B2BC26"),
            0xB0CC855318FF46C8UL, RuntimeFlatCatalogFixture.Build(), new LayoutFortificationReservation(1), resources:
            new LayoutLocalResources(29888, new[] { "BlocksGranite", "BlocksLimestone", "BlocksSlate" },
                steamGeyserCells: new[] { new GridPoint(65, 124), new GridPoint(25, 32) }));
        var result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
        if (!result.Success) foreach (var diagnostic in result.Diagnostics.Where(d => d.Stage == "attempt" && d.ToString().Contains("cooler")).Take(6))
            Console.WriteLine("  cooler fixture " + diagnostic);
        Accepted(result);
        Equal(10, LayoutLoadoutModel.Build(input, result.Layout).Residents.Count, "no roster shrink");
        Console.WriteLine("  flat cooler tile layout=" + result.Layout.Digest + " " + result.Stats);
    }

    private static void P4ETenRejectedTile()
    {
        string[] rows = System.IO.File.ReadAllLines(System.IO.Path.Combine(AppContext.BaseDirectory, "p4e-ten-rejected-traits.rle"));
        var cells = new List<TerrainCell>();
        foreach (string row in rows)
            foreach (string token in row.Split(','))
            {
                string[] parts = token.Split('*');
                TerrainTraits traits = (TerrainTraits)Convert.ToUInt16(parts[0], 16);
                for (int i = 0; i < int.Parse(parts[1]); i++) cells.Add(new TerrainCell(traits));
            }
        Equal(25600, cells.Count, "captured exact 160x160 map, not resized");
        var terrain = new TerrainSnapshot(160, 160, cells, "p4e-live-ten-rejected-ED06EE5676C0012E");
        var climate = new LayoutClimate(-4, 37);
        var stage = TechStageModel.Create(SettlementTechStageLevel.IndustrialLate);
        var budget = OrdinarySettlementPopulation.Create(P2ProfileCatalog.IndustrialTemperate, stage, climate, 150, defenseAllowancePercent: 100);
        var requirements = LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate, climate, stage: stage,
            populationBudget: budget, mortarBiasPercent: 100);
        var input = new ProceduralLayoutInput(requirements, terrain, 0x15A894EE21B8FC83UL, CoreCatalog,
            new LayoutFortificationReservation(1), resources: new LayoutLocalResources(28352,
                new[] { "BlocksGranite", "BlocksLimestone", "BlocksMarble" },
                steamGeyserCells: new[] { new GridPoint(65, 45), new GridPoint(129, 106) }));
        var result = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
        Accepted(result);
        Equal(10, LayoutLoadoutModel.Build(input, result.Layout).Residents.Count, "funded roster unchanged");
        True(result.Layout.Rooms.Any(r => r.Kind == LayoutRoomKind.Medical) &&
            result.Layout.Rooms.Any(r => r.Kind == LayoutRoomKind.Workshop), "both formerly unplaceable required rooms");
        Console.WriteLine("  exact rejected tile layout=" + result.Layout.Digest + " guns=" + result.Layout.Courts.Count +
            " field=" + result.Layout.JointSite.Field + " compound=" + result.Layout.JointSite.Compound + " " + result.Stats);
    }

    private static void P4EArtilleryCapacity()
    {
        foreach (int residents in new[] { 4, 8, 9, 10, 12 })
        {
            int cap = residents >= 10 ? 3 : residents == 9 ? 2 : 1;
            var req = LayoutRequirements.FromPopulation(residents, Industrial, Temperate,
                stage: TechStageModel.Create(SettlementTechStageLevel.IndustrialLate), importValueLimit: 15000);
            LayoutEquipment kit = LayoutEquipment.Resolve(req, CoreCatalog, null, 2, 1);
            Equal(cap, P4EArtillery.Capacity(req, kit.Kit), "permanent roster cap");
            int best = 0;
            for (ulong seed = 1; seed <= 8; seed++)
            {
                var input = Input(Plains(180), req, seed);
                ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input); Accepted(result);
                var layout = result.Layout;
                True(layout.Courts.Count <= cap, "never exceeds roster allowance");
                Equal(layout.Courts.Count, layout.Rooms.Count(r => r.Kind == LayoutRoomKind.MortarMagazine), "one distinct store per gun");
                Equal(layout.Courts.Count, layout.Courts.SelectMany(c => c.Access).Distinct().Count(), "distinct working cells");
                True(layout.Materials.ImportValue >= P4EArtillery.ImportValue(CoreCatalog, layout.Courts.Count), "guns and shared supplies funded");
                foreach (var court in layout.Courts)
                    True(ProceduralLayoutValidator.MagazineLoadingRoute(input, layout, court).Count > 0, "all stores accessible");
                Equal(layout.Courts.Count, layout.WithAddedObstacles(new GridPoint[0]).Courts.Count, "clones retain all guns");
                best = Math.Max(best, layout.Courts.Count);
                if (best == cap) break;
            }
            Equal(cap, best, "attainable when resources/space permit residents=" + residents);
        }
        int one = P4EArtillery.ImportValue(CoreCatalog, 1), two = P4EArtillery.ImportValue(CoreCatalog, 2), three = P4EArtillery.ImportValue(CoreCatalog, 3);
        True(Math.Abs((two - one) - (three - two)) <= 1, "only gun and embedded barrel marginal cost; aggregate ceiling differs by at most one");
        True(two < one * 2 && three < one * 3, "shared finite supplies");
        var primitive = LayoutRequirements.FromPopulation(12, Tribal, Temperate);
        Equal(0, P4EArtillery.Capacity(primitive, LayoutEquipment.Resolve(primitive, CoreCatalog, null, 2, 1).Kit), "primitive no guns");
        var poor = LayoutRequirements.FromPopulation(10, Industrial, Temperate, importValueLimit: one - 1);
        var unarmed = Plan(Plains(180), poor, 1); Accepted(unarmed);
        Equal(0, unarmed.Layout.Courts.Count, "insufficient resources omit optional artillery only");
    }

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
