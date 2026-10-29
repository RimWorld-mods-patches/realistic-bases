using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

// Geometry-based eight-way route model, not a reimplementation of PawnPath's
// terrain/pawn costs. The recorded log contains summaries, not full native legs.
internal static class CalderaFieldRouteRegression
{
    public static void Run(ProceduralLayoutInput input, ProceduralLayout layout)
    {
        var defended = P4BDefenderArea.Build(input, layout);
        var interior = P4BPerimeterDepth.Envelope(input.Terrain, layout).Concat(layout.JointSite.Compound.Points())
            .Concat(layout.JointSite.Field.Points()).Distinct().ToArray();
        var gates = layout.Gates.Select(g => g.Cell).Concat(layout.ApproachDefense.SallyPorts.SelectMany(p => p.Doors))
            .Concat(new[] { layout.FieldWall.InnerGate.Value, layout.FieldWall.OuterGate }).ToArray();
        var opaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
        var doors = new HashSet<GridPoint>(gates.Concat(layout.Gates.SelectMany(g => g.BandDoors))
            .Concat(layout.Rooms.SelectMany(r => r.Doors)));
        var blocked = new HashSet<GridPoint>(opaque.Except(doors));
        bool Walkable(GridPoint c) => input.Terrain.InBounds(c) && !blocked.Contains(c) &&
            (input.Terrain[c].Has(TerrainTraits.Standable) || layout.Excavation.Contains(c) ||
                input.Terrain[c].Has(TerrainTraits.ClearablePlant));
        IReadOnlyList<GridPoint> NativeModel(GridPoint from, GridPoint to)
        {
            var previous = new Dictionary<GridPoint, GridPoint>();
            var costs = new Dictionary<GridPoint, int> { [from] = 0 };
            var queue = new PriorityQueue<GridPoint, (int, int)>();
            int order = 0; queue.Enqueue(from, (0, order++));
            while (queue.TryDequeue(out GridPoint here, out var priority))
            {
                if (priority.Item1 != costs[here]) continue;
                if (here.Equals(to))
                {
                    var path = new List<GridPoint> { here };
                    while (!here.Equals(from)) { here = previous[here]; path.Add(here); }
                    path.Reverse(); return path;
                }
                for (int z = -1; z <= 1; z++)
                    for (int x = -1; x <= 1; x++)
                    {
                        if (x == 0 && z == 0) continue;
                        var next = new GridPoint(here.X + x, here.Z + z);
                        if (!Walkable(next)) continue;
                        if (x != 0 && z != 0 && (!Walkable(new GridPoint(here.X + x, here.Z)) ||
                            !Walkable(new GridPoint(here.X, here.Z + z)))) continue;
                        int cost = costs[here] + (x != 0 && z != 0 ? 18 : 13) + (doors.Contains(next) ? 45 : 0);
                        if (costs.TryGetValue(next, out int known) && known <= cost) continue;
                        costs[next] = cost; previous[next] = here; queue.Enqueue(next, (cost, order++));
                    }
            }
            return null;
        }
        var post = new GridPoint(145, 43);
        var gate = new GridPoint(143, 47);
        var center = ProceduralLayoutValidator.PublicCirculation(input, layout)
            .OrderBy(c => Math.Abs(c.X - (int)Math.Round(layout.Enclosure.Average(c => c.X))) +
                Math.Abs(c.Z - (int)Math.Round(layout.Enclosure.Average(c => c.Z)))).ThenBy(c => c).First();
        var homes = layout.Rooms.Where(r => r.Kind == LayoutRoomKind.Barracks || r.Kind == LayoutRoomKind.Bedroom)
            .SelectMany(r => r.Interior.Points()).Where(Walkable).Take(4).ToArray();
        Check(input.Digest == "1D9ACE282BC88CD1" && layout.Digest == "7C899E3CEF005B1D", "Caldera replay anchors");
        Check(!defended.Contains(new GridPoint(131, 34)), "recorded native outside cell remains forbidden");
        Check(layout.ApproachDefense.AllPosts.Contains(post) && gates.Contains(gate), "recorded post and access gate");
        Check(homes.Length == 4, "four modeled starts");
        int eligible = 0, restrictedEligible = 0;
        var cache = new Dictionary<(GridPoint, GridPoint), IReadOnlyList<GridPoint>>();
        IReadOnlyList<GridPoint> CachedModel(GridPoint from, GridPoint to)
        {
            if (!cache.TryGetValue((from, to), out var path)) cache[(from, to)] = path = NativeModel(from, to);
            return path;
        }
        foreach (var home in homes)
        {
            var nearestEight = gates.Distinct().OrderBy(g => Math.Abs(g.X - home.X) + Math.Abs(g.Z - home.Z))
                .ThenBy(g => g).Take(8).ToArray();
            Check(!nearestEight.Contains(new GridPoint(117, 47)) && !nearestEight.Contains(new GridPoint(124, 47)),
                "nearest-eight pruning omits the legal compound/field gateways");
            var direct = CachedModel(home, post);
            Check(direct != null && direct.Count >= 57 && direct.Count <= 59 && direct.Any(c => !defended.Contains(c)),
                "modeled direct path is found, short, and leaves the declared area");
            if (P4BFieldPostPath.Evaluate(home, post, center, gate, nearestEight, CachedModel,
                defended, interior, opaque.ToArray(), direct).Allowed) restrictedEligible++;
            var result = P4BFieldPostPath.Evaluate(home, post, center, gate, gates, CachedModel,
                defended, interior, opaque.ToArray(), direct);
            if (result.Allowed) eligible++;
        }
        Console.WriteLine("caldera-field-route modeled=True restrictedEligible=" + restrictedEligible + " eligible=" + eligible +
            " defenders=4 post=" + post + " accessGate=" + gate + " layout=" + layout.Digest);
        Check(restrictedEligible == 0, "old nearest-eight search reproduces the spawn gate rejection");
        Check(eligible >= 2, "outside field post requires at least two P3-legal routes");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Caldera field-route regression: " + message);
    }
}
