using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

// Cooler orientation and exhaust (docs/plans/2026-09-23-cooler-exhaust.md).
internal static partial class Program
{
    private static void RunCoolerExhaustTests()
    {
        Run("cooler rotation matches vanilla hot and cold sides", CoolerRotationMatchesVanilla);
        Run("room partition follows vanilla's outdoor-temperature rule", RoomPartitionFollowsVanillaRule);
        Run("open-ground coolers cool their room and exhaust outdoors", OpenGroundCoolersExhaustOutdoors);
        Run("rock-protected coolers use sealed exhaust pockets within the roof-hole budget", RockProtectedCoolersUsePockets);
        Run("validator rejects unsealed or roofed exhaust pockets", ValidatorRejectsBadPockets);
    }

    // Vanilla Building_Cooler: cold = Position + South.RotatedBy(rot), hot = Position + North.RotatedBy(rot).
    private static GridPoint VanillaHot(GridPoint cell, string rotation)
    {
        switch (rotation)
        {
            case "East": return new GridPoint(cell.X + 1, cell.Z);
            case "South": return new GridPoint(cell.X, cell.Z - 1);
            case "West": return new GridPoint(cell.X - 1, cell.Z);
            default: return new GridPoint(cell.X, cell.Z + 1);
        }
    }

    private static GridPoint VanillaCold(GridPoint cell, string rotation)
    {
        GridPoint hot = VanillaHot(cell, rotation);
        return new GridPoint(2 * cell.X - hot.X, 2 * cell.Z - hot.Z);
    }

    private static IEnumerable<KeyValuePair<LayoutRoom, LayoutFixture>> Coolers(ProceduralLayout layout) =>
        layout.Rooms.SelectMany(r => r.Fixtures.Where(f => f.Kind == LayoutFixtureKind.WallCooler).Select(f => new KeyValuePair<LayoutRoom, LayoutFixture>(r, f)));

    private static void CoolerRotationMatchesVanilla()
    {
        HashSet<string> rotations = new HashSet<string>();
        int coolers = 0;
        foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5, 6, 7, 8 })
        {
            // Hot industrial settlements: a freezer plus wall coolers in every living room.
            ProceduralLayoutResult result = Plan(Plains(120), LayoutRequirements.FromPopulation(6, Industrial, Hot), seed);
            Accepted(result);
            foreach (KeyValuePair<LayoutRoom, LayoutFixture> pair in Coolers(result.Layout))
            {
                LayoutRoom room = pair.Key;
                LayoutFixture cooler = pair.Value;
                coolers++;
                rotations.Add(cooler.Rotation);
                GridPoint cell = cooler.Cells[0];
                GridPoint cold = VanillaCold(cell, cooler.Rotation);
                GridPoint hot = VanillaHot(cell, cooler.Rotation);
                True(room.Interior.Contains(cold), room.Id + " cold side " + cold + " must be inside the cooled room (rotation " + cooler.Rotation + ")");
                True(!room.Bounds.Contains(hot), room.Id + " hot side " + hot + " must be outside the cooled room (rotation " + cooler.Rotation + ")");
                Equal(cold, cooler.ColdCell.Value, room.Id + " ColdCell agrees with vanilla");
                Equal(hot, cooler.HotCell.Value, room.Id + " HotCell agrees with vanilla");
                Equal(hot, cooler.Exhaust.Value, room.Id + " exhaust is the vanilla hot cell");
            }
        }
        True(coolers >= 8, "hot-climate layouts place coolers: " + coolers);
        True(rotations.Count == 4, "all four rotations exercised: " + string.Join(",", rotations.OrderBy(r => r)));
    }

    // Minimal synthetic plan: only walls, roofs and pockets matter to the partition.
    private static ProceduralLayout PartitionLayout(IEnumerable<GridPoint> walls, IEnumerable<GridPoint> constructedRoof,
        IEnumerable<LayoutExhaustPocket> pockets = null) =>
        new ProceduralLayout(string.Empty, 0, LayoutDoctrine.OpenGround, false, new GridPoint(0, 0), Enumerable.Empty<LayoutRoom>(),
            Enumerable.Empty<LayoutCorridor>(), null, Enumerable.Empty<GridPoint>(), walls, Enumerable.Empty<GridPoint>(),
            Enumerable.Empty<LayoutGate>(), Enumerable.Empty<GridPoint>(), Enumerable.Empty<GridPoint>(), constructedRoof, 1, 0,
            Enumerable.Empty<LayoutRelationship>(), Enumerable.Empty<GridPoint>(), Enumerable.Empty<string>(), null, string.Empty,
            string.Empty, string.Empty, null, null, pockets);

    private static IEnumerable<GridPoint> Ring(int minX, int minZ, int maxX, int maxZ) =>
        new GridRect(minX, minZ, maxX - minX + 1, maxZ - minZ + 1).Points().Where(c => c.X == minX || c.X == maxX || c.Z == minZ || c.Z == maxZ);

    private static void RoomPartitionFollowsVanillaRule()
    {
        GridPoint cell = new GridPoint(5, 5);
        GridPoint[] ring = { new GridPoint(4, 5), new GridPoint(6, 5), new GridPoint(5, 4), new GridPoint(5, 6) };
        TerrainSnapshot open = Plains(12);
        TerrainSnapshot roofed = Build(12, 12, (x, z) => Ground | TerrainTraits.Roofed);

        LayoutRoomPartition pocket = LayoutRoomPartition.Build(open, PartitionLayout(ring, Enumerable.Empty<GridPoint>()));
        True(pocket.IsOutdoor(cell), "a sealed unroofed 1x1 pocket uses outdoor temperature: " + pocket.Describe(cell));
        True(pocket.RoomAt(cell) != pocket.RoomAt(new GridPoint(1, 1)), "walls split the pocket from the outside");
        True(pocket.RoomAt(ring[0]) < 0, "a wall is a separator, not a room");

        LayoutRoomPartition covered = LayoutRoomPartition.Build(roofed, PartitionLayout(ring, Enumerable.Empty<GridPoint>()));
        True(!covered.IsOutdoor(cell), "a roofed pocket does not: " + covered.Describe(cell));
        True(covered.IsOutdoor(new GridPoint(1, 1)), "a region touching the map edge is outdoor even when roofed");
        LayoutExhaustPocket cleared = new LayoutExhaustPocket(cell, ring, true, false, new[] { "freezer" });
        True(LayoutRoomPartition.Build(roofed, PartitionLayout(ring, Enumerable.Empty<GridPoint>(), new[] { cleared })).IsOutdoor(cell),
            "clearing the pocket roof makes it outdoor");

        // 3x3 interior: at least ceil(9 x 25%) = 3 unroofed cells are needed.
        List<GridPoint> walls = Ring(3, 3, 7, 7).ToList();
        List<GridPoint> interior = new GridRect(4, 4, 3, 3).Points().ToList();
        GridPoint inside = new GridPoint(5, 5);
        True(LayoutRoomPartition.Build(open, PartitionLayout(walls, interior.Take(6))).IsOutdoor(inside), "3 of 9 unroofed is outdoor");
        True(!LayoutRoomPartition.Build(open, PartitionLayout(walls, interior.Take(7))).IsOutdoor(inside), "2 of 9 unroofed is indoor");
    }

    private static void OpenGroundCoolersExhaustOutdoors()
    {
        int coolers = 0, publicExhaust = 0;
        foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5, 6 })
        {
            TerrainSnapshot terrain = Plains(120);
            ProceduralLayoutResult result = Plan(terrain, LayoutRequirements.FromPopulation(6, Industrial, Hot), seed);
            Accepted(result);
            ProceduralLayout layout = result.Layout;
            LayoutRoomPartition partition = LayoutRoomPartition.Build(terrain, layout);
            HashSet<GridPoint> pockets = new HashSet<GridPoint>(layout.ExhaustPockets.Select(p => p.Cell));
            foreach (KeyValuePair<LayoutRoom, LayoutFixture> pair in Coolers(layout))
            {
                coolers++;
                GridPoint served = new GridPoint(pair.Key.Interior.MinX, pair.Key.Interior.MinZ);
                Equal(partition.RoomAt(served), partition.RoomAt(pair.Value.ColdCell.Value), pair.Key.Id + " cools its own room");
                True(partition.IsOutdoor(pair.Value.HotCell.Value), pair.Key.Id + " exhausts outdoors: " + partition.Describe(pair.Value.HotCell.Value));
                if (!pockets.Contains(pair.Value.HotCell.Value)) publicExhaust++;
            }
            True(layout.ExhaustRoofClearance.Count == 0, "open ground clears no rock roof");
        }
        True(coolers > 0 && publicExhaust * 2 > coolers, "open ground mostly exhausts onto open-sky streets: " + publicExhaust + "/" + coolers);
        Console.WriteLine("  open-ground coolers: " + coolers + " (" + publicExhaust + " into public open-sky space)");
    }

    private static void RockProtectedCoolersUsePockets()
    {
        int pockets = 0, holes = 0, layouts = 0;
        foreach (LayoutClimate climate in new[] { Temperate, Hot })
            foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5, 6 })
            {
                TerrainSnapshot terrain = Mountain(130, 110);
                LayoutRequirements requirements = new LayoutRequirements(6, Industrial, HousingStandard.Barracks, climate, 0, 600, 186);
                ProceduralLayoutInput input = Input(terrain, requirements, seed);
                ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input);
                Accepted(result);
                ProceduralLayout layout = result.Layout;
                Equal(LayoutDoctrine.RockProtected, layout.Doctrine, "doctrine");
                layouts++;
                LayoutRoomPartition partition = LayoutRoomPartition.Build(terrain, layout);
                foreach (KeyValuePair<LayoutRoom, LayoutFixture> pair in Coolers(layout))
                    True(partition.IsOutdoor(pair.Value.HotCell.Value), pair.Key.Id + " exhausts outdoors under rock: " + partition.Describe(pair.Value.HotCell.Value));
                int cooled = input.Equipment.Demand.Rooms.Count(r => r.Kind == LayoutRoomKind.Freezer ||
                    (climate.CoolingRequired && LayoutDemandModel.IsLiving(r.Kind)));
                True(layout.ExhaustRoofClearance.Count <= (cooled + 1) / 2, "roof holes within budget");
                True(!layout.ExhaustRoofClearance.Any(layout.RetainedThickRoof.Contains), "a cleared hole is not retained roof");
                Equal(0, LayoutRoofSupport.Unsupported(terrain, layout.Excavation, layout.RetainedThickRoof).Count, "rock roof still supported");
                foreach (LayoutExhaustPocket pocket in layout.ExhaustPockets)
                {
                    True(pocket.RoomIds.Count > 0, "pocket serves a room");
                    // A wall built for a neighbouring pocket may seal this one too.
                    True(ProceduralLayoutValidator.Neighbors(pocket.Cell).All(n => layout.ExhaustPocketWalls.Contains(n) ||
                        layout.Rooms.Any(r => r.IsEdge(n) && !n.Equals(r.Door)) ||
                        (terrain[n].Has(TerrainTraits.NaturalRock) && !layout.Excavation.Contains(n))), "pocket sealed on all sides");
                }
                pockets += layout.ExhaustPockets.Count;
                holes += layout.ExhaustRoofClearance.Count;
                if (seed == 4 && climate == Temperate) ExportLayout("mountain-industrial-exhaust-pockets", result, true);
            }
        True(pockets > 0, "rock layouts use exhaust pockets");
        True(holes > 0, "some pockets sit deep enough to clear rock roof");
        Console.WriteLine("  rock-protected: " + layouts + " layouts, " + pockets + " pockets, " + holes + " roof holes");
    }

    private static ProceduralLayout WithPockets(ProceduralLayout l, IEnumerable<LayoutExhaustPocket> pockets) =>
        new ProceduralLayout(l.InputDigest, l.Seed, l.Doctrine, l.MortarCapable, l.Anchor, l.Rooms, l.Corridors, l.Court, l.Enclosure,
            l.BuiltWalls, l.NaturalWalls, l.Gates, l.Excavation, l.RetainedThickRoof, l.ConstructedRoof, l.WallLayers, l.FieldCellsRequired,
            l.Relationships, l.ExtraObstacles, l.Notes, l.TechStage, l.RolesDigest, l.EquipmentDigest, l.WallStuff, l.Materials, l.Power, pockets);

    // Bounded deterministic search for an accepted plan with a pocket of one kind.
    private static (ProceduralLayoutInput, ProceduralLayout, LayoutExhaustPocket) PocketFixture(Func<LayoutExhaustPocket, bool> kind)
    {
        foreach (TerrainSnapshot terrain in new[] { Mountain(130, 110), Plains(120) })
            for (ulong seed = 1; seed <= 24; seed++)
            {
                LayoutRequirements requirements = terrain.Width == 130
                    ? new LayoutRequirements(6, Industrial, HousingStandard.Barracks, Temperate, 0, 600, 186)
                    : LayoutRequirements.FromPopulation(6, Industrial, Hot, HousingStandard.PersonalRooms);
                ProceduralLayoutInput input = Input(terrain, requirements, seed);
                ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input);
                LayoutExhaustPocket pocket = result.Success ? result.Layout.ExhaustPockets.FirstOrDefault(kind) : null;
                if (pocket != null) return (input, result.Layout, pocket);
            }
        throw new InvalidOperationException("no pocket fixture of the requested kind");
    }

    private static void ValidatorRejectsBadPockets()
    {
        var (input, layout, walled) = PocketFixture(p => p.Walls.Count > 0);
        Equal(0, ProceduralLayoutValidator.Validate(input, layout).Count, "accepted layout validates");
        // Drop one pocket wall: the pocket opens to public space.
        LayoutExhaustPocket open = new LayoutExhaustPocket(walled.Cell, walled.Walls.Skip(1), walled.RoofCleared, walled.Excavated, walled.RoomIds);
        List<string> unsealed = ProceduralLayoutValidator.Validate(input, WithPockets(layout, layout.ExhaustPockets.Select(p => p == walled ? open : p))).ToList();
        True(unsealed.Any(e => e.StartsWith("pocket_" + walled.Cell) && e.EndsWith("_not_sealed")), "unsealed pocket rejected: " + string.Join(",", unsealed.Take(5)));

        var (deepInput, deepLayout, deep) = PocketFixture(p => p.RoofCleared);
        Equal(0, ProceduralLayoutValidator.Validate(deepInput, deepLayout).Count, "accepted deep-pocket layout validates");
        input = deepInput;
        layout = deepLayout;
        // Keep the rock roof over a deep pocket: vanilla would treat it as indoors.
        LayoutExhaustPocket roofed = new LayoutExhaustPocket(deep.Cell, deep.Walls, false, deep.Excavated, deep.RoomIds);
        List<string> covered = ProceduralLayoutValidator.Validate(input, WithPockets(layout, layout.ExhaustPockets.Select(p => p == deep ? roofed : p))).ToList();
        True(covered.Contains("pocket_" + deep.Cell + "_roof_ledger"), "roofed pocket fails the roof ledger: " + string.Join(",", covered.Take(5)));
        True(covered.Any(e => e.StartsWith("cooler_exhaust_not_outdoor_")), "roofed pocket fails vanilla's outdoor rule: " + string.Join(",", covered.Take(5)));
    }
}
