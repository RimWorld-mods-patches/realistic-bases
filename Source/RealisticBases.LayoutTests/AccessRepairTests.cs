using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

internal static partial class Program
{
    private static void RoomAccessRepair()
    {
        var input = AccessRepairInput();
        string before = input.Digest;
        var result = LayoutAttempt.ProbeRoomAccess(input, false);
        True(result.Placed, "the room still has to be placed");
        True(result.ReachableChamber >= 25, "placing a room across the neck must not strand the remaining chamber");
        True(result.Repairs > 0, "reserve access before committing the blocking room");
        True(result.CorridorOverlap == 0, "public access never passes through the placed room");
        True(result.Evaluations <= input.Budget.MaxEvaluations, "access repair keeps the evaluation allowance");
        Equal(before, input.Digest, "access repair leaves its terrain and input unchanged");
    }

    private static void RoomAccessRepairBounds()
    {
        foreach (var input in new[] { AccessRepairInput(repairs: 0), AccessRepairInput(routeNodes: 8), AccessRepairInput(water: true) })
        {
            var result = LayoutAttempt.ProbeRoomAccess(input, false);
            Equal(0, result.Repairs, "an unavailable repair or route allowance cannot grant a new corridor");
            Equal(0, result.ReachableChamber, "the bounded probe leaves the unreachable chamber alone");
            True(result.Evaluations <= input.Budget.MaxEvaluations, "failed recovery stays bounded");
        }
        var exhausted = LayoutAttempt.ProbeRoomAccess(AccessRepairInput(), true);
        Equal(0, exhausted.Repairs, "access recovery shares the ordinary corridor-growth allowance");
    }

    private static ProceduralLayoutInput AccessRepairInput(int repairs = 8, int routeNodes = 4000, bool water = false)
    {
        var terrain = Plains(60);
        if (water) terrain = new TerrainSnapshot(60, 60, terrain.Points().Select(c =>
            c.Equals(new GridPoint(30, 20)) ? new TerrainCell(TerrainTraits.Water) : terrain[c]), "access-water");
        return new ProceduralLayoutInput(LayoutRequirements.FromPopulation(4, Industrial, Temperate, stage: MidStage),
            terrain, 1234, CoreCatalog, budget: new LayoutSearchBudget(maxEvaluations: 5000,
                maxCandidatesPerRoom: 128, maxRepairsPerAttempt: repairs, maxRouteNodes: routeNodes));
    }
}

namespace RealisticBases.Planning
{
    internal sealed partial class LayoutAttempt
    {
        internal static (bool Placed, int ReachableChamber, int Repairs, int Evaluations, int CorridorOverlap)
            ProbeRoomAccess(ProceduralLayoutInput input, bool exhaustedGrowth)
        {
            var grid = LayoutTerrainGrid.Build(input.Terrain, 0);
            var stats = new LayoutSearchStats();
            var run = new LayoutAttempt(input, input.Equipment.Demand, grid, new GridPoint(10, 16),
                LayoutDoctrine.OpenGround, false, input.Seed, stats, new List<LayoutDiagnostic>(),
                new ProceduralLayoutStageTimings());
            for (int i = 0; i < run.occ.Length; i++) run.occ[i] = CourtCell;
            var chamber = new GridRect(31, 13, 15, 15);
            // The side room has no initial street frontage. A preserved
            // connection makes it usable without occupying the far chamber.
            foreach (GridPoint c in new GridRect(20, 17, 7, 7).Points()
                .Concat(new GridRect(23, 21, 7, 7).Points()).Concat(chamber.Points()))
                run.occ[grid.Index(c)] = Free;
            for (int x = 10; x <= 30; x++) run.occ[grid.Index(new GridPoint(x, 20))] = Free;
            var main = new Segment { Id = "main", Main = true, Axis = new GridPoint(1, 0), Root = new GridPoint(10, 16) };
            for (int x = 10; x <= 23; x++) main.Sections.Add(new List<GridPoint> { new GridPoint(x, 16) });
            var branch = new Segment { Id = "branch", Axis = new GridPoint(0, 1), Root = main.Root };
            for (int z = 17; z <= 20; z++) branch.Sections.Add(new List<GridPoint> { new GridPoint(10, z) });
            run.segments.Add(main); run.segments.Add(branch);
            foreach (GridPoint c in main.Cells.Concat(branch.Cells)) run.occ[grid.Index(c)] = Street;
            if (exhaustedGrowth) run.growths = input.Budget.MaxRepairsPerAttempt + 2 * run.demand.Rooms.Count;
            var room = new LayoutRoomDemand("neck-probe", LayoutRoomKind.Storage, 1, "stockpile-cells",
                new[] { new GridRect(0, 0, 5, 5) });
            bool placed = run.TryPlaceRoom(room);
            var reached = new HashSet<GridPoint>(run.segments.SelectMany(s => s.Cells));
            var queue = new Queue<GridPoint>(reached);
            while (queue.Count > 0)
            {
                GridPoint c = queue.Dequeue();
                foreach (GridPoint step in ProceduralLayoutPlanner.Cardinal)
                {
                    var next = new GridPoint(c.X + step.X, c.Z + step.Z);
                    if (run.OkForStreet(next) && reached.Add(next)) queue.Enqueue(next);
                }
            }
            return (placed, chamber.Points().Count(c => run.occ[grid.Index(c)] == Free && reached.Contains(c)),
                stats.Repairs, stats.EvaluationCount,
                run.segments.SelectMany(s => s.Cells).Count(c => run.rooms.Any(r => r.Bounds.Contains(c))));
        }
    }
}
