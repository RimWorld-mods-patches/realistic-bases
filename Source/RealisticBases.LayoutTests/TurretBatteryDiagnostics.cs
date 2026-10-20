using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RealisticBases.Generation;
using RealisticBases.Planning;

internal static partial class Program
{
    private const double EnemySniperRange = 44.9;

    private sealed class TurretCoverRejectedCandidate
    {
        public GridPoint Gate;
        public string Side;
        public GridPoint Origin;
        public GridPoint? Cell;
        public string Condition;
    }

    private sealed class TurretServiceRouteFailure
    {
        public GridPoint Gate;
        public string Side;
        public GridPoint Origin;
        public GridPoint RouteOrigin;
        public List<GridPoint> ServiceEndpoints;
        public string Condition;
    }

    private sealed class TurretBetweenNestCandidate
    {
        public string Type, Stage, Rejection;
        public GridPoint Gate, NestA, NestB, StretchCell, Origin;
        public GridPoint? Blocker;
    }

    private sealed class TurretBetweenNestPair
    {
        public string Side;
        public GridPoint GateA, GateB, NestA, NestB, OuterWallA, OuterWallB;
        public List<GridPoint> StretchCells, WalkCells;
    }

    private sealed class TurretFaceCrossing
    {
        public GridPoint Gate, Nest, Face;
        public int WalkIndex, Distance;
    }

    private sealed class TurretFaceWalk
    {
        public string Side;
        public GridPoint Outward;
        public List<GridPoint> Cells;
        public List<TurretFaceCrossing> Crossings;
    }
    private sealed class TurretWallLoopCell
    {
        public string Side;
        public GridPoint Cell, Outward;
        public int LoopIndex;
    }
    private sealed class TurretWallBoundaryEdge
    {
        public int X1, Z1, X2, Z2, Direction;
        public GridPoint Cell, Outward;
        public string Side;
        public bool Used;
    }

    private sealed class TurretFaceCoverageGap
    {
        public string Side, Reason;
        public int Length, NearbyCrossings, LegalCandidates;
        public double NearestNestDistance;
        public bool FieldFace;
        public List<GridPoint> Cells = new List<GridPoint>();
    }

    private static void T4ARenewableFixtures()
    {
        LayoutPowerYard panel = new LayoutPowerYard(LayoutPowerModel.Solar, new GridPoint[0], new GridPoint(0, 0),
            null, "North", 1700, 0.5);
        LayoutPowerYard turbine = new LayoutPowerYard(LayoutPowerModel.Wind, new GridPoint[0], new GridPoint(0, 0),
            null, "North", 2300, 0.4);
        Equal(566, LayoutPowerModel.FirmWatts(new[] { panel }), "solar credit includes charging losses");
        Equal(613, LayoutPowerModel.FirmWatts(new[] { turbine }), "wind credit includes charging losses");
        Equal(1, LayoutPowerModel.RenewableStorageBatteries(new[] { panel, turbine }), "off-share storage is added to the bridge");
        Equal(1, LayoutPowerModel.Generators(100, 0, new[] { panel }, 1000), "intermittent critical credit retains fuel backup");
        foreach (bool steam in new[] { true, false })
        {
            ProceduralLayoutInput input = new ProceduralLayoutInput(
                LayoutRequirements.FromPopulation(8, Industrial, Temperate, stage: MidStage),
                Plains(130), steam ? 1UL : 17UL, CoreCatalog,
                resources: steam ? new LayoutLocalResources(int.MaxValue, null, null, CentreGeysers) : null);
            Stopwatch watch = Stopwatch.StartNew();
            ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input);
            watch.Stop();
            Accepted(result);
            LayoutPowerPlan power = result.Layout.Power;
            True(steam ? power.Yards.Any(y => y.DefName == LayoutPowerModel.Geothermal) :
                power.Yards.Any(y => y.DefName == LayoutPowerModel.Solar) && power.Yards.Any(y => y.DefName == LayoutPowerModel.Wind),
                "renewable comparison fixture retains the requested sources");
            if (steam) Equal(0, power.Generators, "geothermal alone funds this fixture's entire connected load");
            else Equal(LayoutPowerModel.Batteries(power.CriticalWatts + power.NonCriticalWatts) +
                power.RenewableStorageBatteries, power.Batteries, "wind and solar keep the bridge plus off-share storage");
            True(P4BTurretPlanner.AvailableWatts(result.Layout) > 0, "renewable firm output is available to turret planning");
            Console.WriteLine("  t4a-renewable " + (steam ? "steam" : "wind-solar") + " input=" + input.Digest +
                " sources=" + string.Join(",", power.Yards.Select(y => y.DefName)) +
                " averageW=" + power.RenewableWatts + " firmW=" + power.FirmWatts + " storageBatteries=" + power.RenewableStorageBatteries + " criticalW=" + power.CriticalWatts +
                " nonCriticalW=" + power.NonCriticalWatts + " generators=" + power.Generators +
                " batteries=" + power.Batteries + " fuel=" + power.FuelStock + " plannerMs=" + watch.ElapsedMilliseconds);
        }
    }

    private static void PrintTurretBatteryDiagnostics(ProceduralLayoutInput input,
        ProceduralLayout layout, string label, long plannerMilliseconds,
        IEnumerable<LayoutDiagnostic> diagnostics = null)
    {
        P4BTurretPlan network = layout.Turret;
        if (network == null)
        {
            Console.WriteLine("  turret-t0 " + label + " turrets=0 watts=0 import=0 plannerMs=" +
                plannerMilliseconds + " reason=no_turret_plan decision=" + string.Join(";",
                    (diagnostics ?? Enumerable.Empty<LayoutDiagnostic>()).Where(d => d.Stage == "turret")));
            foreach (IGrouping<string, GridPoint> crossing in (layout.ApproachDefense?.Posts
                .Select(post => post.AccessGate) ?? Enumerable.Empty<GridPoint>())
                .Distinct().OrderBy(c => CrossingName(layout, c), StringComparer.Ordinal)
                .GroupBy(c => CrossingName(layout, c), StringComparer.Ordinal))
                Console.WriteLine("  turret-t0-crossing " + label + " crossing=" + crossing.Key +
                    " types=none turrets=0 exposure=0 answered=0 unanswered=0 watts=0 import=0");
            Console.WriteLine("  turret-t0-total " + label + " denseExposure=0 answered=0 unanswered=0");
            return;
        }
        if (layout.ApproachDefense == null)
        {
            Console.WriteLine("  turret-t0 " + label + " inputDigest=" + input.Digest + " layoutDigest=" +
                layout.Digest + " turrets=" + network.All.Count() + " watts=" + network.TotalWatts +
                " import=" + network.ImportValue + " plannerMs=" + plannerMilliseconds +
                " reason=no_approach_defense");
            return;
        }

        List<P4BTurretPlan> guns = network.All.OrderBy(g => g.AccessGate ?? new GridPoint(-1, -1))
            .ThenBy(g => g.Side, StringComparer.Ordinal).ThenBy(g => g.DefName, StringComparer.Ordinal)
            .ThenBy(g => g.Origin).ToList();
        string types = string.Join(",", guns.GroupBy(g => g.DefName, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + ":" + g.Count()));
        Console.WriteLine("  turret-t0 " + label + " inputDigest=" + input.Digest + " layoutDigest=" + layout.Digest +
            " types=" + types + " turrets=" + guns.Count + " watts=" + network.TotalWatts +
            " import=" + network.ImportValue + " plannerMs=" + plannerMilliseconds);

        P4BStaffingAudit staffing = P4BStaffing.Audit(input, layout, network);
        HashSet<GridPoint> baseOpaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
        Dictionary<P4BTurretPlan, (int Exposure, int Answered)> metrics = guns.ToDictionary(g => g,
            g => TurretExposure(input, layout, g, guns, staffing, baseOpaque));
        HashSet<GridPoint> defendedGates = new HashSet<GridPoint>(layout.ApproachDefense?.Posts
            .Select(post => post.AccessGate) ?? Enumerable.Empty<GridPoint>());
        var crossings = defendedGates.OrderBy(c => CrossingName(layout, c), StringComparer.Ordinal)
            .GroupBy(c => CrossingName(layout, c), StringComparer.Ordinal);
        int totalExposure = 0, totalAnswered = 0;
        foreach (IGrouping<string, GridPoint> crossing in crossings)
        {
            HashSet<GridPoint> gates = new HashSet<GridPoint>(crossing);
            List<P4BTurretPlan> crossingGuns = guns.Where(g => g.AccessGate.HasValue && gates.Contains(g.AccessGate.Value)).ToList();
            int exposure = crossingGuns.Sum(g => metrics[g].Exposure);
            int answered = crossingGuns.Sum(g => metrics[g].Answered);
            totalExposure += exposure;
            totalAnswered += answered;
            string crossingTypes = string.Join(",", crossingGuns.GroupBy(g => g.DefName, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + ":" + g.Count()));
            int crossingWatts = crossingGuns.Sum(g => g.Watts);
            int crossingImport = crossingGuns.Sum(PhysicalTurretImportValue);
            Console.WriteLine("  turret-t0-crossing " + label + " crossing=" + crossing.Key +
                " types=" + (crossingTypes.Length == 0 ? "none" : crossingTypes) +
                " turrets=" + crossingGuns.Count + " exposure=" + exposure + " answered=" + answered +
                " unanswered=" + (exposure - answered) + " watts=" + crossingWatts + " import=" + crossingImport);
        }
        Console.WriteLine("  turret-t0-total " + label + " denseExposure=" + totalExposure +
            " answered=" + totalAnswered + " unanswered=" + (totalExposure - totalAnswered));
        foreach (P4BTurretPlan gun in guns)
        {
            (int exposure, int answered) = metrics[gun];
            Console.WriteLine("  turret-t0-gun " + label + " crossing=" + CrossingName(layout,
                gun.AccessGate ?? new GridPoint(-1, -1)) + " type=" + gun.DefName + " origin=" + gun.Origin +
                " betweenNests=" + gun.BetweenNests +
                " exposure=" + exposure + " answered=" + answered + " unanswered=" + (exposure - answered) +
                " watts=" + gun.Watts + " import=" + PhysicalTurretImportValue(gun));
        }
    }

    private static List<TurretCoverRejectedCandidate> TurretCoverRejections(
        IEnumerable<LayoutDiagnostic> diagnostics)
    {
        List<TurretCoverRejectedCandidate> result = new List<TurretCoverRejectedCandidate>();
        foreach (LayoutDiagnostic diagnostic in diagnostics ?? Enumerable.Empty<LayoutDiagnostic>())
        {
            if (diagnostic.Stage != "turret") continue;
            Match decisions = Regex.Match(diagnostic.Detail, @"decisions=\[(.*)\]");
            if (!decisions.Success) continue;
            foreach (Match decision in Regex.Matches(decisions.Groups[1].Value,
                @"(?:^|;)(\d+,\d+)/([a-z]+):[^;]*?coverRejections=\[([^\]]*)\]"))
            {
                GridPoint gate = ParseDiagnosticPoint(decision.Groups[1].Value);
                foreach (string item in decision.Groups[3].Value.Split('|'))
                {
                    string[] fields = item.Split('>');
                    if (fields.Length != 3) continue;
                    result.Add(new TurretCoverRejectedCandidate
                    {
                        Gate = gate,
                        Side = decision.Groups[2].Value,
                        Origin = ParseDiagnosticPoint(fields[0]),
                        Cell = fields[1] == "none" ? (GridPoint?)null : ParseDiagnosticPoint(fields[1]),
                        Condition = fields[2]
                    });
                }
            }
        }
        return result;
    }

    private static List<TurretServiceRouteFailure> TurretServiceRouteFailures(ProceduralLayout layout,
        IEnumerable<LayoutDiagnostic> diagnostics)
    {
        List<TurretServiceRouteFailure> result = new List<TurretServiceRouteFailure>();
        foreach (LayoutDiagnostic diagnostic in diagnostics ?? Enumerable.Empty<LayoutDiagnostic>())
        {
            if (diagnostic.Stage != "turret") continue;
            foreach (Match block in Regex.Matches(diagnostic.Detail, @"serviceRouteFailures=\[([^\]]*)\]"))
            {
                foreach (string item in block.Groups[1].Value.Split('|'))
                {
                    string[] fields = item.Split('>');
                    if (fields.Length != 4) continue;
                    GridPoint gate = ParseDiagnosticPoint(fields[1]);
                    string side = string.Join(",", (layout.ApproachDefense?.Posts ?? Array.Empty<P4BPost>())
                        .Where(p => p.AccessGate.Equals(gate)).Select(p => p.Side)
                        .Distinct().OrderBy(s => s, StringComparer.Ordinal));
                    result.Add(new TurretServiceRouteFailure
                    {
                        Gate = gate,
                        Side = side.Length == 0 ? "none" : side,
                        Origin = ParseDiagnosticPoint(fields[0]),
                        RouteOrigin = ParseDiagnosticPoint(fields[1]),
                        ServiceEndpoints = fields[2] == "none" ? new List<GridPoint>() :
                            fields[2].Split(',').Chunk(2).Select(pair => new GridPoint(
                                int.Parse(pair[0]), int.Parse(pair[1]))).ToList(),
                        Condition = fields[3]
                    });
                }
            }
        }
        return result;
    }

    private static List<TurretBetweenNestCandidate> TurretBetweenNestCandidates(
        IEnumerable<LayoutDiagnostic> diagnostics)
    {
        List<TurretBetweenNestCandidate> result = new List<TurretBetweenNestCandidate>();
        foreach (LayoutDiagnostic diagnostic in diagnostics ?? Enumerable.Empty<LayoutDiagnostic>())
        {
            if (diagnostic.Stage != "turret") continue;
            foreach (Match block in Regex.Matches(diagnostic.Detail, @"betweenCandidates\{([^}]*)\}"))
                foreach (string token in block.Groups[1].Value.Split('|'))
                {
                    string[] fields = token.Split('~');
                    if (fields.Length != 9) continue;
                    result.Add(new TurretBetweenNestCandidate
                    {
                        Type = fields[0], Gate = ParseDiagnosticPoint(fields[1]),
                        NestA = ParseDiagnosticPoint(fields[2]), NestB = ParseDiagnosticPoint(fields[3]),
                        StretchCell = ParseDiagnosticPoint(fields[4]), Origin = ParseDiagnosticPoint(fields[5]),
                        Stage = fields[6], Rejection = fields[7],
                        Blocker = fields[8] == "none" ? (GridPoint?)null : ParseDiagnosticPoint(fields[8])
                    });
                }
        }
        return result;
    }

    private static List<TurretBetweenNestPair> TurretBetweenNestPairs(ProceduralLayout layout)
    {
        List<TurretBetweenNestPair> pairs = new List<TurretBetweenNestPair>();
        List<TurretWallLoopCell> all = TurretOuterWallLoop(layout);
        foreach (List<TurretWallLoopCell> loop in all.GroupBy(c => c.LoopIndex).OrderBy(g => g.Key).Select(g => g.ToList()))
        {
            List<TurretFaceCrossing> crossings = (layout.ApproachDefense?.Posts ?? Array.Empty<P4BPost>())
                .GroupBy(p => p.AccessGate).Select(g => g.First()).Select(post =>
            {
                GridPoint face = TurretOuterWallFace(layout, post.AccessGate, post.Outward);
                int index = loop.Select((cell, i) => new { Cell = cell.Cell, Index = i })
                    .OrderBy(c => TurretManhattan(c.Cell, face)).ThenBy(c => c.Index)
                    .Select(c => c.Index).DefaultIfEmpty(-1).First();
                return new TurretFaceCrossing { Gate = post.AccessGate, Nest = post.Cell, Face = face,
                    WalkIndex = index, Distance = index < 0 ? int.MaxValue : TurretManhattan(loop[index].Cell, face) };
            }).Where(c => c.WalkIndex >= 0 && c.Distance <= 2).OrderBy(c => c.WalkIndex).ToList();
            if (loop.Count < 2 || crossings.Count < 2) continue;
            for (int i = 0; i < crossings.Count; i++)
            {
                TurretFaceCrossing a = crossings[i], b = crossings[(i + 1) % crossings.Count];
                List<GridPoint> stretch = new List<GridPoint>();
                for (int index = (a.WalkIndex + 1) % loop.Count, steps = 0;
                    index != b.WalkIndex && steps++ < loop.Count; index = (index + 1) % loop.Count)
                    stretch.Add(loop[index].Cell);
                if (stretch.Count == 0) continue;
                pairs.Add(new TurretBetweenNestPair { Side = loop[(a.WalkIndex + 1) % loop.Count].Side,
                    GateA = a.Gate, GateB = b.Gate, NestA = a.Nest, NestB = b.Nest,
                    OuterWallA = loop[a.WalkIndex].Cell, OuterWallB = loop[b.WalkIndex].Cell,
                    WalkCells = loop.Select(c => c.Cell).ToList(), StretchCells = stretch });
            }
        }
        return pairs;
    }

    private static List<TurretWallLoopCell> TurretOuterWallLoop(ProceduralLayout layout)
    {
        HashSet<GridPoint> walls = new HashSet<GridPoint>(layout.BuiltWalls.Concat(layout.NaturalWalls)
            .Concat(layout.OuterWalls).Concat(layout.Gates.SelectMany(g => g.BandWalls.Concat(g.BandDoors).Concat(new[] { g.Cell })))
            .Concat(layout.FieldWall?.Walls ?? Enumerable.Empty<GridPoint>())
            .Concat(layout.FieldWall?.AccessPorts.SelectMany(p => p.Doors) ?? Enumerable.Empty<GridPoint>()));
        Dictionary<GridPoint, GridPoint> specialOutward = new Dictionary<GridPoint, GridPoint>();
        foreach (LayoutGate gate in layout.Gates)
            foreach (GridPoint door in gate.BandDoors.Concat(new[] { gate.Cell })) specialOutward[door] = gate.Outward;
        foreach (P4BSallyPort port in (layout.ApproachDefense?.SallyPorts ?? Array.Empty<P4BSallyPort>())
            .Concat(layout.FieldWall?.AccessPorts ?? Array.Empty<P4BSallyPort>()))
            foreach (GridPoint door in port.Doors) specialOutward[door] = port.Outward;
        if (layout.FieldWall != null) specialOutward[layout.FieldWall.OuterGate] = layout.Gates[layout.FieldWall.GateIndex].Outward;
        List<P4BAttackSide> sides = layout.ApproachDefense.Sides.ToList();
        List<TurretWallBoundaryEdge> edges = new List<TurretWallBoundaryEdge>();
        foreach (GridPoint wall in walls)
        {
            Add(new GridPoint(0, 1), 0, wall.X * 2 - 1, wall.Z * 2 + 1, wall.X * 2 + 1, wall.Z * 2 + 1);
            Add(new GridPoint(1, 0), 1, wall.X * 2 + 1, wall.Z * 2 + 1, wall.X * 2 + 1, wall.Z * 2 - 1);
            Add(new GridPoint(0, -1), 2, wall.X * 2 + 1, wall.Z * 2 - 1, wall.X * 2 - 1, wall.Z * 2 - 1);
            Add(new GridPoint(-1, 0), 3, wall.X * 2 - 1, wall.Z * 2 - 1, wall.X * 2 - 1, wall.Z * 2 + 1);
            void Add(GridPoint outward, int direction, int x1, int z1, int x2, int z2)
            {
                GridPoint outside = new GridPoint(wall.X + outward.X, wall.Z + outward.Z);
                if (walls.Contains(outside)) return;
                P4BAttackSide side = sides.FirstOrDefault(s => s.Outward.Equals(outward));
                if (side == null && specialOutward.TryGetValue(wall, out GridPoint special) && special.Equals(outward))
                    side = sides.FirstOrDefault(s => s.Outward.Equals(outward));
                if (side != null) edges.Add(new TurretWallBoundaryEdge { X1 = x1, Z1 = z1, X2 = x2, Z2 = z2,
                    Cell = wall, Outward = outward, Side = side.Id, Direction = direction });
            }
        }
        Dictionary<(int X, int Z), List<TurretWallBoundaryEdge>> starts = edges.GroupBy(e => (e.X1, e.Z1))
            .ToDictionary(g => g.Key, g => g.ToList());
        List<TurretWallLoopCell> result = new List<TurretWallLoopCell>();
        int loopId = 0;
        foreach (TurretWallBoundaryEdge start in edges)
        {
            if (start.Used) continue;
            List<TurretWallBoundaryEdge> trace = new List<TurretWallBoundaryEdge>();
            TurretWallBoundaryEdge current = start;
            bool closed = false;
            for (int step = 0; step <= edges.Count; step++)
            {
                if (current.Used) { closed = current == start; break; }
                current.Used = true; trace.Add(current);
                if (current.X2 == start.X1 && current.Z2 == start.Z1) { closed = true; break; }
                if (!starts.TryGetValue((current.X2, current.Z2), out List<TurretWallBoundaryEdge> options)) break;
                current = options.Where(e => !e.Used)
                    .OrderBy(e => TurretTurnRank((e.Direction - current.Direction + 4) % 4))
                    .ThenBy(e => e.Direction).FirstOrDefault();
                if (current == null) break;
            }
            long area = 0;
            for (int i = 0; i < trace.Count; i++)
            {
                TurretWallBoundaryEdge a = trace[i], b = trace[(i + 1) % trace.Count];
                area += (long)a.X1 * b.Z1 - (long)b.X1 * a.Z1;
            }
            if (!closed || area >= 0 || trace.Count < 4) continue;
            result.AddRange(trace.Select(e => new TurretWallLoopCell { Cell = e.Cell, Outward = e.Outward,
                Side = e.Side, LoopIndex = loopId }));
            loopId++;
        }
        return result;
    }

    private static int TurretTurnRank(int turn) => turn == 1 ? 0 : turn == 0 ? 1 : turn == 3 ? 2 : 3;

    private static List<TurretFaceWalk> TurretFaceWalks(ProceduralLayout layout)
    {
        List<TurretFaceWalk> walks = new List<TurretFaceWalk>();
        foreach (P4BAttackSide side in layout.ApproachDefense?.Sides.Where(s => s.Exposed) ?? Array.Empty<P4BAttackSide>())
        {
            HashSet<GridPoint> cells = new HashSet<GridPoint>(side.Faces);
            foreach (P4BSallyPort port in (layout.ApproachDefense?.SallyPorts ?? Array.Empty<P4BSallyPort>())
                .Concat(layout.FieldWall?.AccessPorts ?? Array.Empty<P4BSallyPort>())
                .Where(p => p.Outward.Equals(side.Outward))) cells.Add(port.Outer);
            if (layout.FieldWall != null && layout.Gates[layout.FieldWall.GateIndex].Outward.Equals(side.Outward))
                cells.Add(layout.FieldWall.OuterGate);
            foreach (LayoutGate gate in layout.Gates.Where(g => g.Outward.Equals(side.Outward)))
                cells.Add(gate.BandDoors.Concat(new[] { gate.Cell })
                    .OrderByDescending(c => TurretNormalProjection(c, side.Outward)).First());
            HashSet<GridPoint> remaining = new HashSet<GridPoint>(cells);
            while (remaining.Count > 0)
            {
                GridPoint root = remaining.OrderBy(c => c).First();
                Queue<GridPoint> queue = new Queue<GridPoint>();
                List<GridPoint> component = new List<GridPoint>();
                remaining.Remove(root); queue.Enqueue(root);
                while (queue.Count > 0)
                {
                    GridPoint here = queue.Dequeue(); component.Add(here);
                    foreach (GridPoint d in new[] { new GridPoint(0, -1), new GridPoint(-1, 0),
                        new GridPoint(1, 0), new GridPoint(0, 1) })
                    {
                        GridPoint next = new GridPoint(here.X + d.X, here.Z + d.Z);
                        if (remaining.Remove(next)) queue.Enqueue(next);
                    }
                }
                AddWalk(component.OrderBy(c => TurretTangentProjection(c, side.Outward))
                    .ThenBy(c => TurretNormalProjection(c, side.Outward)).ThenBy(c => c).ToList());

                void AddWalk(List<GridPoint> walkCells)
                {
                    List<TurretFaceCrossing> crossings = layout.ApproachDefense.Posts
                        .Where(p => p.Side == side.Id && p.Outward.Equals(side.Outward))
                        .GroupBy(p => p.AccessGate).Select(g => g.First()).Select(p =>
                        {
                            GridPoint wallFace = TurretOuterWallFace(layout, p.AccessGate, side.Outward);
                            int index = walkCells.FindIndex(c => c.Equals(wallFace));
                            if (index < 0)
                            {
                                int tangent = TurretTangentProjection(wallFace, side.Outward);
                                index = walkCells.Select((c, i) => new { Cell = c, Index = i })
                                    .Where(x => Math.Abs(x.Cell.X - wallFace.X) + Math.Abs(x.Cell.Z - wallFace.Z) <= 1)
                                    .OrderBy(x => Math.Abs(x.Cell.X - wallFace.X) + Math.Abs(x.Cell.Z - wallFace.Z))
                                    .Select(x => x.Index).DefaultIfEmpty(-1).First();
                            }
                            return new TurretFaceCrossing { Gate = p.AccessGate, Nest = p.Cell,
                                Face = wallFace, WalkIndex = index };
                        }).Where(c => c.WalkIndex >= 0).OrderBy(c => c.WalkIndex).ToList();
                    walks.Add(new TurretFaceWalk { Side = side.Id, Outward = side.Outward,
                        Cells = walkCells.ToList(), Crossings = crossings });
                }
            }
        }
        return walks;
    }

    private static GridPoint TurretOuterWallFace(ProceduralLayout layout, GridPoint gate, GridPoint outward)
    {
        P4BSallyPort port = (layout.ApproachDefense?.SallyPorts ?? Array.Empty<P4BSallyPort>())
            .Concat(layout.FieldWall?.AccessPorts ?? Array.Empty<P4BSallyPort>())
            .FirstOrDefault(p => p.Doors.Contains(gate));
        if (port != null) return port.Outer;
        if (layout.FieldWall != null && layout.FieldWall.OuterGate.Equals(gate)) return gate;
        foreach (LayoutGate perimeterGate in layout.Gates)
        {
            List<GridPoint> doors = perimeterGate.BandDoors.Concat(new[] { perimeterGate.Cell }).ToList();
            if (doors.Contains(gate)) return doors.OrderByDescending(c => c.X * outward.X + c.Z * outward.Z).First();
        }
        return gate;
    }

    private static List<TurretFaceCoverageGap> TurretFacesWithoutNearbyNest(ProceduralLayout layout)
    {
        List<TurretFaceCoverageGap> gaps = new List<TurretFaceCoverageGap>();
        HashSet<GridPoint> arrival = new HashSet<GridPoint>(layout.Gates.SelectMany(g => g.ExteriorRoute)
            .Concat(layout.FieldWall?.ArrivalRoute ?? Enumerable.Empty<GridPoint>()));
        foreach (TurretFaceWalk walk in TurretFaceWalks(layout))
        {
            List<GridPoint> nests = layout.ApproachDefense.Posts.Where(p => p.Side == walk.Side)
                .SelectMany(p => p.Slots).Distinct().ToList();
            foreach (var normalRun in walk.Cells.GroupBy(c => TurretNormalProjection(c, walk.Outward)))
            {
                List<GridPoint> run = new List<GridPoint>();
                int previousTangent = int.MinValue;
                void FlushRun()
                {
                    if (run.Count == 0) return;
                    double nearestNest = nests.Count == 0 ? double.MaxValue : run.Min(cell =>
                        nests.Min(nest => Distance(cell, nest)));
                    if (nearestNest <= 14) { run.Clear(); return; }
                    int routeCells = run.Count(cell => arrival.Any(route => TurretManhattan(cell, route) <= 1));
                    bool fieldFace = layout.FieldWall != null && run.Any(layout.FieldWall.Walls.Contains);
                    int sideIndex = P4BApproachDefensePlanner.SideIndex(walk.Outward);
                    int legalCandidates = !fieldFace && sideIndex >= 0 &&
                        sideIndex < layout.ApproachDefense.CompoundGateCandidates.Count
                        ? layout.ApproachDefense.CompoundGateCandidates[sideIndex] : 0;
                    int crossings = walk.Crossings.Count(c => run.Any(cell =>
                        TurretManhattan(cell, c.Face) <= 1 || TurretManhattan(cell, c.Gate) <= 1));
                    string reason = routeCells > 0 ? "arrival_route_along_face" : crossings > 0
                        ? "crossing_present_but_nest_not_within14" : fieldFace
                            ? "no_field_crossing_selected" : "no_compound_crossing_selected";
                    if (!fieldFace && routeCells == 0 && legalCandidates == 0)
                        reason += ";no_legal_compound_gate_candidate_on_side";
                    else if (!fieldFace && legalCandidates > 0)
                        reason += ";other_compound_gate_candidates=" + legalCandidates;
                    if (routeCells > 0) reason += ";arrivalAdjacentCells=" + routeCells;
                    gaps.Add(new TurretFaceCoverageGap { Side = walk.Side, Length = run.Count,
                        NearestNestDistance = nearestNest, NearbyCrossings = crossings,
                        LegalCandidates = legalCandidates, FieldFace = fieldFace, Reason = reason,
                        Cells = run.ToList() });
                    run.Clear();
                }
                foreach (GridPoint cell in normalRun.OrderBy(c => TurretTangentProjection(c, walk.Outward)))
                {
                    int tangent = TurretTangentProjection(cell, walk.Outward);
                    if (tangent > previousTangent + 1) FlushRun();
                    run.Add(cell);
                    previousTangent = tangent;
                }
                FlushRun();
            }
        }
        return gaps.OrderBy(g => g.Side, StringComparer.Ordinal).ThenByDescending(g => g.Length).ToList();
    }

    private static int TurretTangentProjection(GridPoint point, GridPoint outward) =>
        point.X * -outward.Z + point.Z * outward.X;

    private static int TurretManhattan(GridPoint a, GridPoint b) =>
        Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z);

    private static int TurretNormalProjection(GridPoint point, GridPoint outward) =>
        point.X * outward.X + point.Z * outward.Z;

    private static IEnumerable<GridPoint> TurretGridLine(GridPoint start, GridPoint end)
    {
        int x = start.X, z = start.Z;
        int dx = Math.Abs(end.X - start.X), sx = start.X < end.X ? 1 : -1;
        int dz = -Math.Abs(end.Z - start.Z), sz = start.Z < end.Z ? 1 : -1;
        int error = dx + dz;
        while (true)
        {
            yield return new GridPoint(x, z);
            if (x == end.X && z == end.Z) yield break;
            int twice = 2 * error;
            if (twice >= dz) { error += dz; x += sx; }
            if (twice <= dx) { error += dx; z += sz; }
        }
    }

    private static GridPoint ParseDiagnosticPoint(string value)
    {
        string[] xy = value.Split(',');
        return new GridPoint(int.Parse(xy[0]), int.Parse(xy[1]));
    }

    private static void RunTurretBatteryT4OutcomeTests()
    {
        if (only == null || only.IndexOf("P4B joint-site search preserves", StringComparison.OrdinalIgnoreCase) >= 0)
            Run("P4B joint-site search preserves its digest across degrees 1 and 8", P4BJointSiteDegreeDeterminism, 45);
        if (only == null || only.IndexOf("T4 outcome", StringComparison.OrdinalIgnoreCase) < 0) return;
        Run("T4 outcome wealth sweep", TurretBatteryT4Outcomes, 400);
    }

    private static void P4BSpawnerPreflightOutcomes()
    {
        SettlementTechStage late = TechStageModel.Create(SettlementTechStageLevel.IndustrialLate);
        List<(string Label, ProceduralLayoutInput Input)> cases = new List<(string, ProceduralLayoutInput)>();
        foreach (ulong seed in new ulong[] { 1, 2, 5 })
        {
            OrdinarySettlementBudget budget = OrdinarySettlementPopulation.Create(
                P2ProfileCatalog.IndustrialTemperate, late, Temperate, 180);
            LayoutRequirements requirements = LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate,
                Temperate, stage: late, populationBudget: budget);
            cases.Add(("total12-seed" + seed + "-w180", new ProceduralLayoutInput(requirements,
                Plains(180), seed, CoreCatalog, new LayoutFortificationReservation(1))));
        }
        OrdinarySettlementBudget broadBudget = OrdinarySettlementPopulation.Create(
            P2ProfileCatalog.IndustrialTemperate, late, Temperate, 180);
        LayoutRequirements broadRequirements = LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate,
            Temperate, stage: late, populationBudget: broadBudget);
        cases.Add(("broad-flat-w180", new ProceduralLayoutInput(broadRequirements,
            Plains(160), 1, CoreCatalog, new LayoutFortificationReservation(1))));
        LayoutClimate foothillClimate = new LayoutClimate(-9, 32);
        OrdinarySettlementBudget foothillBudget = OrdinarySettlementPopulation.Create(
            P2ProfileCatalog.IndustrialTemperate, late, foothillClimate, 90);
        LayoutRequirements foothillRequirements = LayoutRequirements.FromProfile(
            P2ProfileCatalog.IndustrialTemperate, foothillClimate, stage: late, populationBudget: foothillBudget);
        cases.Add(("foothill-seed3849C7F216B8A3E2-late-wealth90", new ProceduralLayoutInput(foothillRequirements,
            Plains(160), 0x3849C7F216B8A3E2UL, CoreCatalog, new LayoutFortificationReservation(1))));
        cases.Add(("foothill-ridge-seed3849C7F216B8A3E2", new ProceduralLayoutInput(broadRequirements,
            Obstacles(160), 0x3849C7F216B8A3E2UL, CoreCatalog, new LayoutFortificationReservation(1))));

        foreach ((string label, ProceduralLayoutInput input) in cases)
        {
            ProceduralLayoutResult planned = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
            Accepted(planned);
            True(planned.Layout.Turret != null && planned.Layout.Turret.All.Count() > 0,
                label + " has its T6 turret network");
            P4BProceduralSpawnPreflightResult preflight = P4BProceduralSpawnPreflight.Evaluate(
                input, planned.Layout);
            True(preflight.Accepted, label + " accepted by the shared spawner preflight: " + preflight.Failure);
            Equal(0, ProceduralLayoutValidator.Validate(input, planned.Layout).Count,
                label + " remains valid under planner-owned turret rules");
            True(P4BTurretPlanner.PowerTerminalsConnected(input, planned.Layout, planned.Layout.Turret),
                label + " connects every powered fixture, generator yard, battery and turret through legal wiring terrain");
            Equal(planned.Layout.Turret.All.Count(), preflight.GuardLookups.Count,
                label + " retains one optional guard lookup per planned turret");
            int withGuard = preflight.GuardLookups.Count(g => g.GuardPost.HasValue);
            Console.WriteLine("  spawner preflight " + label + " digest=" + planned.Layout.Digest +
                " turrets=" + planned.Layout.Turret.All.Count() + " guardPriority=" + withGuard +
                "/" + preflight.GuardLookups.Count + " missingGuardIsNonFatal=true");
            P4BSpawnGuardLookup missingGuard = new P4BSpawnGuardLookup(
                planned.Layout.Turret.All.First(), threats: 10, answered: 0, guardPost: null);
            Equal(0, P4BProceduralSpawnPreflight.BuildSniperGuardSectors(new[] { missingGuard }).Count,
                label + " omits post-priority data when no guard exists");
        }
    }

    private static void RunT5aPowerHeadroomTests()
    {
        if (only == null || only.IndexOf("T5a power headroom", StringComparison.OrdinalIgnoreCase) < 0) return;
        Run("T5a power headroom removes partial batteries and preserves completed ones", T5aPowerHeadroom, 15);
    }

    private static void T5aPowerHeadroom()
    {
        SettlementTechStage late = TechStageModel.Create(SettlementTechStageLevel.IndustrialLate);
        OrdinarySettlementBudget budget = OrdinarySettlementPopulation.Create(
            P2ProfileCatalog.IndustrialTemperate, late, Temperate, 180);
        LayoutRequirements requirements = LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate,
            Temperate, stage: late, populationBudget: budget);
        ProceduralLayoutInput input = new ProceduralLayoutInput(requirements, Plains(180), 1, CoreCatalog,
            new LayoutFortificationReservation(1));
        ProceduralLayoutResult planned = ProceduralLayoutPlanner.Plan(input, requireJointSite: true);
        Accepted(planned);
        ProceduralLayout source = planned.Layout;
        LayoutPowerPlan originalPower = source.Power;
        int critical = originalPower.CriticalWatts;
        int available = 4 * 80;
        int nonCritical = source.Power.Generators * source.Power.GeneratorWatts + source.Power.FirmWatts -
            critical - available;
        True(nonCritical >= 0, "headroom fixture leaves a nonnegative background load");
        LayoutPowerPlan cappedPower = new LayoutPowerPlan(critical, nonCritical,
            input.Equipment.Kit.Generator, originalPower.Generators, originalPower.Batteries,
            originalPower.Yards, input.Equipment.Kit.GeneratorFuel, originalPower.FuelStock,
            originalPower.Diagnostics, originalPower.DefenseReserveWatts, originalPower.DefenseGenerators,
            originalPower.ReservedGenerators, originalPower.ReservedBatteries,
            originalPower.ReleasedFuelStorageCells, originalPower.PackedMaterialImportValue);
        ProceduralLayout layout = new ProceduralLayout(source.InputDigest, source.Seed, source.Doctrine,
            source.MortarCapable, source.Anchor, source.Rooms, source.Corridors, source.Court,
            source.Enclosure, source.BuiltWalls, source.NaturalWalls, source.Gates, source.Excavation,
            source.RetainedThickRoof, source.ConstructedRoof, source.WallLayers, source.FieldCellsRequired,
            source.Relationships, source.ExtraObstacles, source.Notes, source.TechStage, source.RolesDigest,
            source.EquipmentDigest, source.WallStuff, source.Materials, cappedPower, source.ExhaustPockets,
            source.OuterSections, source.JointSite, source.FieldWall, null, source.ApproachDefense,
            source.PerimeterReinforcement, source.Courts, source.BonusFields);
        HashSet<GridPoint> occupied = new HashSet<GridPoint>(layout.Enclosure
            .Concat(layout.Rooms.SelectMany(r => r.Bounds.Points()))
            .Concat(layout.PowerYards.SelectMany(y => y.Cells))
            .Concat(layout.AllFixtures.SelectMany(f => f.Cells))
            .Concat(layout.OuterSections.SelectMany(s => s.ServiceLane(layout.Gates[s.GateIndex]))));
        P4BTurretPlan network = P4BTurretPlanner.Plan(input, layout, layout.FieldWall, occupied,
            out string diagnostic);
        True(network != null, "planner keeps turrets that fit the capped power headroom");
        Equal(240, network.TotalWatts, "one complete three-mini battery fits under the power cap");
        Equal(3, network.All.Count(g => g.DefName == "Turret_MiniTurret"),
            "the completed battery remains after power stops the next crossing");
        True(diagnostic.Contains("powerHeadroomStopped=True"), "planner stops when no mini headroom remains");
        True(diagnostic.Contains("powerLimitedCrossings=10") && diagnostic.Contains("batteryMiniShortfall=30"),
            "diagnostic reports the power limited crossings and missing minimum minis");
        True(diagnostic.Contains("power_headroom_incomplete_battery_removed placed=1/3 final=0/3 shortfall=3"),
            "incomplete battery is removed and its full shortfall is reported");
        Console.WriteLine("  turret-t5a-power-headroom availableW=" +
            P4BTurretPlanner.AvailableWatts(layout) + " plannedW=" + network.TotalWatts + " " + diagnostic);
    }

    private static void TurretBatteryT4Outcomes()
    {
        string filter = Environment.GetEnvironmentVariable("T4_OUTCOME_FILTER");
        string[] filterLabels = string.IsNullOrEmpty(filter) ? Array.Empty<string>() : filter.Split(',');
        int plannerDegree = TurretOutcomePlannerDegree();
        List<Action> cases = new List<Action>();
        SettlementTechStage industrialLate = TechStageModel.Create(SettlementTechStageLevel.IndustrialLate);
        foreach (ulong seed in Enumerable.Range(1, 8).Select(n => (ulong)n))
        {
            OrdinarySettlementBudget budget = OrdinarySettlementPopulation.Create(
                P2ProfileCatalog.IndustrialTemperate, industrialLate, Temperate, 180);
            LayoutRequirements requirements = LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate,
                Temperate, stage: industrialLate, populationBudget: budget);
            ProceduralLayoutInput input = new ProceduralLayoutInput(requirements, Plains(180), seed, CoreCatalog,
                new LayoutFortificationReservation(1), plannerDegree: plannerDegree);
            cases.Add(() => RunTurretBatteryOutcome(input, "total12-seed" + seed + "-w180", 180, true, filterLabels));
        }

        foreach (int wealth in new[] { 60, 100, 180 })
        {
            LayoutRequirements requirements = V2Funded(P2ProfileCatalog.TribalTemperate,
                SettlementTechStageLevel.Neolithic, Temperate, wealth);
            ProceduralLayoutInput input = new ProceduralLayoutInput(requirements, Lakeshore(180), 1, CoreCatalog,
                new LayoutFortificationReservation(1), plannerDegree: plannerDegree);
            cases.Add(() => RunTurretBatteryOutcome(input, "lakeshore-joint-w" + wealth, wealth, true, filterLabels));
        }

        foreach (int wealth in new[] { 60, 100, 180 })
        {
            OrdinarySettlementBudget budget = OrdinarySettlementPopulation.Create(
                P2ProfileCatalog.IndustrialTemperate, industrialLate, Temperate, wealth);
            LayoutRequirements requirements = LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate,
                Temperate, stage: industrialLate, populationBudget: budget);
            ProceduralLayoutInput input = new ProceduralLayoutInput(requirements, Plains(160), 1, CoreCatalog,
                new LayoutFortificationReservation(1), plannerDegree: plannerDegree);
            cases.Add(() => RunTurretBatteryOutcome(input, "broad-flat-w" + wealth, wealth, true, filterLabels));
        }
        if (string.Equals(Environment.GetEnvironmentVariable("T4_OUTCOME_SEQUENTIAL"), "1", StringComparison.Ordinal))
        {
            foreach (Action run in cases) run();
        }
        else
        {
            System.Threading.Tasks.Parallel.ForEach(cases,
                new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 4 }, run => run());
        }
    }

    private static int TurretOutcomePlannerDegree()
    {
        string value = Environment.GetEnvironmentVariable("T4_PLANNER_DEGREE");
        if (string.IsNullOrEmpty(value)) return 1;
        int degree;
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out degree) || degree < 1 || degree > 8)
            throw new ArgumentOutOfRangeException("T4_PLANNER_DEGREE", "Planner degree must be from 1 through 8.");
        return degree;
    }

    private static void P4BJointSiteDegreeDeterminism()
    {
        SettlementTechStage stage = TechStageModel.Create(SettlementTechStageLevel.IndustrialLate);
        OrdinarySettlementBudget budget = OrdinarySettlementPopulation.Create(
            P2ProfileCatalog.IndustrialTemperate, stage, Temperate, 180);
        LayoutRequirements requirements = LayoutRequirements.FromProfile(P2ProfileCatalog.IndustrialTemperate,
            Temperate, stage: stage, populationBudget: budget);
        TerrainSnapshot terrain = Plains(180);
        LayoutFortificationReservation fortification = new LayoutFortificationReservation(1);
        ProceduralLayoutInput sequential = new ProceduralLayoutInput(requirements, terrain, 5, CoreCatalog,
            fortification, plannerDegree: 1);
        ProceduralLayoutInput parallel = new ProceduralLayoutInput(requirements, terrain, 5, CoreCatalog,
            fortification, plannerDegree: 8);
        Equal(sequential.Digest, parallel.Digest, "execution degree is excluded from the input digest");
        ProceduralLayoutResult one = ProceduralLayoutPlanner.Plan(sequential, requireJointSite: true);
        ProceduralLayoutResult eight = ProceduralLayoutPlanner.Plan(parallel, requireJointSite: true);
        True(one.Success && eight.Success, "both degree runs must produce a complete plan");
        Equal(one.Layout.Digest, eight.Layout.Digest, "layout digest is independent of search degree");
        Equal(string.Join("|", one.Diagnostics.Where(d => d.Stage == "joint_site")),
            string.Join("|", eight.Diagnostics.Where(d => d.Stage == "joint_site")),
            "joint-site candidates and rejection counts preserve their seeded order");
    }

    private static void RunTurretBatteryOutcome(ProceduralLayoutInput input, string label, int wealth, bool jointSite,
        string[] filterLabels)
    {
        if (filterLabels.Length > 0 && !filterLabels.Any(f => label.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)) return;
        TimeSpan cpuBefore = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        Stopwatch watch = Stopwatch.StartNew();
        ProceduralLayoutResult result = ProceduralLayoutPlanner.Plan(input, requireJointSite: jointSite);
        watch.Stop();
        double plannerCpuMilliseconds = (System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime - cpuBefore).TotalMilliseconds;
        long plannerAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        if (result.Layout == null)
        {
            string rejections = TopIncompleteBatteryRejections(result.Diagnostics);
            string turretDetails = string.Join(";", result.Diagnostics.Where(d => d.Stage == "turret").Select(d => d.Detail));
            Console.WriteLine("  turret-t4-outcome " + label + " wealth=" + wealth + " residents=" + input.Requirements.Residents +
                " status=failed rejection=" + result.RejectionCode + " plannerDegree=" + input.PlannerDegree +
                " plannerMs=" + watch.ElapsedMilliseconds +
                " plannerCpuMs=" + plannerCpuMilliseconds.ToString("F2", CultureInfo.InvariantCulture) +
                " plannerAllocatedBytes=" + plannerAllocatedBytes.ToString(CultureInfo.InvariantCulture) +
                " plannerStages=" + StageTimingText(result.StageTimings, watch.Elapsed.TotalMilliseconds) +
                " incompleteCrossingRejects=" + rejections + " diagnostics=" + string.Join(";",
                    result.Diagnostics.Where(d => d.Stage == "turret")));
            if (turretDetails.Length > 0) Console.WriteLine("  turret-t4-diagnostic " + label + " " + turretDetails);
            throw new InvalidOperationException(label + ": " + result.RejectionCode + " " +
                string.Join(";", result.Diagnostics.TakeLast(8)));
        }
        ProceduralLayout layout = result.Layout;
        if (label == "total12-seed1-w180")
            AssertSeedOneEastOuterGateNest(layout);
        LayoutRoom powerRoom = layout.Rooms.FirstOrDefault(r => r.Kind == LayoutRoomKind.Power);
        string powerRoomSize = powerRoom == null ? "none" :
            powerRoom.Interior.Width + "x" + powerRoom.Interior.Height;
        P4BTurretPlan network = layout.Turret;
        Equal(0, ProceduralLayoutValidator.Validate(input, layout).Count, "outcome final layout validates");
        if (network != null) True(P4BTurretPlanner.PowerTerminalsConnected(input, layout, network),
            "trimmed outcome retains every turret power terminal");
        List<P4BTurretPlan> guns = (network?.All ?? Enumerable.Empty<P4BTurretPlan>()).ToList();
        foreach (var crossing in guns.GroupBy(g => g.AccessGate))
        {
            True(crossing.Count(g => g.DefName == "Turret_MiniTurret") <= 6, "crossing mini limit includes heavy support");
            True(crossing.Count(g => g.DefName == "Turret_Autocannon") <= 2, "crossing autocannon limit");
            True(crossing.Count(g => g.DefName == "Turret_Sniper") <= 1, "crossing slug limit");
        }
        if (layout.Power != null && input.Requirements.DefenseReserveWatts > 0)
        {
            LayoutPowerModel.Loads(layout.Rooms, out int critical, out int nonCritical);
            Equal(LayoutPowerModel.Generators(critical + (network?.TotalWatts ?? 0), nonCritical,
                layout.Power.Yards, layout.Power.GeneratorWatts), layout.Power.Generators,
                "trim keeps the minimum generators for actual connected load");
            Equal(LayoutPowerModel.Batteries(critical + nonCritical + (network?.TotalWatts ?? 0)) + layout.Power.RenewableStorageBatteries,
                layout.Power.Batteries, "trim keeps the actual six-hour battery bridge");
            True(layout.Power.ReservedGenerators >= layout.Power.Generators, "trim adds no generator");
            True(layout.Power.ReservedBatteries >= layout.Power.Batteries, "trim adds no battery");
        }
        int defended = layout.ApproachDefense?.Posts.Select(p => p.AccessGate).Distinct().Count() ?? 0;
        Dictionary<GridPoint?, int> minis = guns.Where(g => g.DefName == "Turret_MiniTurret")
            .GroupBy(g => g.AccessGate).ToDictionary(g => g.Key, g => g.Count());
        int withBattery = minis.Count(kv => kv.Value >= 3);
        int withoutBattery = Math.Max(0, defended - withBattery);
        int[] counts = minis.Where(kv => kv.Value >= 3).Select(kv => kv.Value).OrderBy(n => n).ToArray();
        string spacingFallbackLevels = "radius+2:" + guns.Count(g => g.SpacingLevel == 0) +
            ",radius:" + guns.Count(g => g.SpacingLevel == 1) +
            ",miniBelowRadius:" + guns.Count(g => g.SpacingLevel == 2);
        string batteryMinMedianMax = counts.Length == 0 ? "none" : counts.Min() + "/" +
            (counts.Length % 2 == 1 ? counts[counts.Length / 2] :
                (counts[counts.Length / 2 - 1] + counts[counts.Length / 2]) / 2.0) + "/" + counts.Max();
        string byType = string.Join(",", guns.GroupBy(g => g.DefName, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + ":" + g.Count()));
        int conduitCellsBefore = PlannedConduitCells(input, layout, LegacyPowerAccessCells(layout));
        int conduitCellsAfter = PlannedConduitCells(input, layout, P4BTurretPlanner.PowerAccessCells(layout));
        int reservedWatts = layout.Power?.DefenseReserveWatts ?? 0;
        int reservedImport = input.Requirements.DefenseImportReserveValue;
        int baseImport = Math.Max(0, layout.Materials.ImportValue - (network?.ImportValue ?? 0));
        string rejectionCodes = TopIncompleteBatteryRejections(result.Diagnostics);
        string sidesWithoutNest = string.Join(",", (layout.ApproachDefense?.Sides ?? Array.Empty<P4BAttackSide>())
            .Where(s => s.Exposed && s.Positions.Count == 0).Select(s => s.Id).OrderBy(s => s, StringComparer.Ordinal));
        string sidesMissingSupport = TurretSupportFailureSides(result.Diagnostics);
        P4BStaffingAudit staffingAudit = network == null ? null : P4BStaffing.Audit(input, layout, network);
        Console.WriteLine("  turret-t4-outcome " + label + " wealth=" + wealth + " residents=" + input.Requirements.Residents +
            " status=accepted types=" + (byType.Length == 0 ? "none" : byType) +
            " defendedCrossings=" + defended + " batteryCrossings=" + withBattery + " withoutBattery=" + withoutBattery +
            " minisPerBatteryMinMedianMax=" + batteryMinMedianMax +
            " heavies=" + guns.Count(g => g.DefName == "Turret_Autocannon" || g.DefName == "Turret_Sniper") +
            " wattsUsedReserved=" + (network?.TotalWatts ?? 0) + "/" + reservedWatts +
            " renewableAverageFirm=" + (layout.Power?.RenewableWatts ?? 0) + "/" + (layout.Power?.FirmWatts ?? 0) +
            " renewableStorageBatteries=" + (layout.Power?.RenewableStorageBatteries ?? 0) +
            " turretImportSpentReserved=" + (network?.ImportValue ?? 0) + "/" + reservedImport +
            " generatorsReservedKept=" + (layout.Power?.ReservedGenerators ?? 0) + "/" + (layout.Power?.Generators ?? 0) +
            " batteriesReservedKept=" + (layout.Power?.ReservedBatteries ?? 0) + "/" + (layout.Power?.Batteries ?? 0) +
            " powerRoomBeforeAfter=" + powerRoomSize + "/" + powerRoomSize +
            " conduitCellsBeforeAfter=" + conduitCellsBefore + "->" + conduitCellsAfter +
            " fuelReservedKept=" + LayoutPowerModel.FuelStock(input.Equipment.Kit.Generator,
                layout.Power?.ReservedGenerators ?? 0) + "/" + (layout.Power?.FuelStock ?? 0) +
            " warehouseCellsReleased=" + (layout.Power?.ReleasedFuelStorageCells ?? 0) +
            " materialImportPackedKept=" + (layout.Power?.PackedMaterialImportValue ?? 0) + "/" + layout.Materials.ImportValue +
            " baseImportSpent=" + baseImport + " totalImportCap=" + input.Requirements.ImportValueLimit +
            " staffingAuditClose=" + (staffingAudit?.CloseSupportedGuns ?? 0) + "/" + (staffingAudit?.Guns ?? 0) +
            " sidesWithoutNest=" + (sidesWithoutNest.Length == 0 ? "none" : sidesWithoutNest) +
            " sidesMissingTurretSupport=" + (sidesMissingSupport.Length == 0 ? "none" : sidesMissingSupport) +
            " betweenNestsTurrets=" + guns.Count(g => g.BetweenNests) +
            " spacingFallbackLevels=" + spacingFallbackLevels +
            " plannerDegree=" + input.PlannerDegree +
            " plannerMs=" + watch.ElapsedMilliseconds +
            " plannerCpuMs=" + plannerCpuMilliseconds.ToString("F2", CultureInfo.InvariantCulture) +
            " plannerAllocatedBytes=" + plannerAllocatedBytes.ToString(CultureInfo.InvariantCulture) +
            " plannerStages=" + StageTimingText(result.StageTimings, watch.Elapsed.TotalMilliseconds) +
            " incompleteCrossingRejects=" + rejectionCodes);
        foreach (TurretFaceCoverageGap gap in TurretFacesWithoutNearbyNest(layout))
            Console.WriteLine("  turret-t5-face-gap " + label + " side=" + gap.Side +
                " faceType=" + (gap.FieldFace ? "field" : "compound") + " length=" + gap.Length +
                " nearestNestDistance=" + (gap.NearestNestDistance == double.MaxValue ? "none" :
                    gap.NearestNestDistance.ToString("0.0")) + " crossings=" + gap.NearbyCrossings +
                " otherGateCandidates=" + gap.LegalCandidates + " reason=" + gap.Reason);
        if (rejectionCodes.Length == 0)
        {
            string turretDetails = string.Join(";", result.Diagnostics.Where(d => d.Stage == "turret").Select(d => d.Detail));
            if (turretDetails.Length > 0)
                Console.WriteLine("  turret-t4-diagnostic " + label + " " + turretDetails);
        }
        PrintTurretBatteryDiagnostics(input, layout, label, watch.ElapsedMilliseconds, result.Diagnostics);
        string turretDetailsForShortfall = string.Join(";", result.Diagnostics
            .Where(d => d.Stage == "turret").Select(d => d.Detail));
        if (turretDetailsForShortfall.Length > 0)
            Console.WriteLine("  turret-t4-diagnostic " + label + " " + turretDetailsForShortfall);
        string mapExportDirectory = Environment.GetEnvironmentVariable("T4_MAP_EXPORT_DIR");
        string mapFilter = Environment.GetEnvironmentVariable("T4_MAP_EXPORT_FILTER");
        bool exportMap = string.IsNullOrEmpty(mapFilter) || mapFilter.Split(',').Any(f =>
            label.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0);
        if (!string.IsNullOrEmpty(mapExportDirectory) && exportMap)
            ExportTurretMap(input, layout, label, result.Diagnostics, mapExportDirectory);
    }

    private static void AssertSeedOneEastOuterGateNest(ProceduralLayout layout)
    {
        GridPoint east = new GridPoint(1, 0);
        GridPoint gate = layout.FieldWall.OuterGate;
        HashSet<GridPoint> arrival = new HashSet<GridPoint>(layout.Gates.SelectMany(g => g.ExteriorRoute)
            .Concat(layout.FieldWall.ArrivalRoute));
        P4BPost nest = layout.ApproachDefense.Posts.FirstOrDefault(post =>
            post.AccessGate.Equals(gate) && post.Outward.Equals(east));
        True(nest != null, "T6 seed 1 reuses the east field outer gate for its defensive nest");
        True(nest.Route.Skip(1).Where(arrival.Contains).All(cell =>
            P4BApproachDefensePlanner.IsFieldArrivalCrossingCell(cell, gate, east, arrival)),
            "the outer-gate nest shares only its first two gate-axis cells with arrival");
        True(nest.Slots.All(cell => !arrival.Contains(cell)) && nest.CoverCells.All(cell => !arrival.Contains(cell)) &&
            (!nest.WallReturn.HasValue || !arrival.Contains(nest.WallReturn.Value)),
            "the outer-gate nest keeps slots, cover, and return off arrival");
        int eastSide = P4BApproachDefensePlanner.SideIndex(east);
        Equal(1, P4BApproachDefensePlanner.FieldGateCrossings(layout)[eastSide],
            "the defended outer gate consumes exactly one east field-crossing cap");
        True(!layout.ApproachDefense.SallyPorts.Any(port => port.Field && port.Outward.Equals(east)),
            "the existing outer gate is reused without adding an east field door");
    }

    private static string StageTimingText(ProceduralLayoutStageTimings t, double totalMilliseconds)
    {
        string F(double n) => n.ToString("F2", CultureInfo.InvariantCulture);
        string Stage(double ms) => F(ms) + "/" + F(totalMilliseconds <= 0 ? 0 : ms * 100 / totalMilliseconds) + "%";
        return "siteCandidate=" + Stage(t.SiteAndCandidateSearchMilliseconds) +
            ",fieldWall=" + Stage(t.FieldWallMilliseconds) +
            ",fitDefenseAccess=" + Stage(t.FitDefenseAccessMilliseconds) +
            ",approachDefense=" + Stage(t.ApproachDefenseMilliseconds) +
            ",turretPlanning=" + Stage(t.TurretPlanningMilliseconds) +
            ",powerTrimLoop=" + Stage(t.PowerTrimMilliseconds) +
            ",validator=" + Stage(t.ValidationMilliseconds) + "/" + t.ValidationCallCount +
            ",scoring=" + Stage(t.ScoringMilliseconds);
    }

    private static int PlannedConduitCells(ProceduralLayoutInput input, ProceduralLayout layout,
        IEnumerable<GridPoint> accessCells)
    {
        try
        {
            IReadOnlyList<GridPoint> route = P4PowerConduitPlanner.Plan(input, layout, accessCells);
            return P4PowerConduitPlanner.ConduitCellCount(layout, route);
        }
        catch (InvalidOperationException)
        {
            return -1;
        }
    }

    // Reconstruct the pre-change access-cell set to measure the path-length
    // effect of allowing the same routes beneath newly reserved built walls.
    private static HashSet<GridPoint> LegacyPowerAccessCells(ProceduralLayout layout)
    {
        return new HashSet<GridPoint>(layout.Enclosure
            .Concat(layout.ApproachDefense?.SallyPorts.SelectMany(p => p.Doors) ?? Enumerable.Empty<GridPoint>())
            .Concat(layout.ApproachDefense?.SallyPorts.SelectMany(p => p.InteriorRoute) ?? new GridPoint[0])
            .Concat(layout.Gates.Select(g => g.Cell))
            .Concat(layout.Gates.SelectMany(g => g.BandDoors.Concat(g.OuterVestibule)))
            .Concat(layout.FieldWall == null ? Enumerable.Empty<GridPoint>() : layout.FieldWall.Access
                .Concat(new[] { layout.FieldWall.InnerGate.Value, layout.FieldWall.OuterGate })
                .Concat(layout.FieldWall.ArrivalRoute.TakeWhile(c => !c.Equals(layout.FieldWall.OuterGate))))
            .Concat((layout.Turret)?.All.SelectMany(t => t.ServiceRoute.Concat(t.Footprint)) ?? Enumerable.Empty<GridPoint>()));
    }

    private static void ExportTurretMap(ProceduralLayoutInput input, ProceduralLayout layout,
        string label, IEnumerable<LayoutDiagnostic> diagnostics, string directory)
    {
        Directory.CreateDirectory(directory);
        List<P4BTurretPlan> guns = (layout.Turret?.All ?? Enumerable.Empty<P4BTurretPlan>()).ToList();
        P4BStaffingAudit staffing = guns.Count == 0 ? null : P4BStaffing.Audit(input, layout, layout.Turret);
        HashSet<GridPoint> opaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
        Dictionary<P4BTurretPlan, (int Exposure, int Answered)> gunMetrics = guns.ToDictionary(g => g,
            g => TurretExposure(input, layout, g, guns, staffing, opaque));
        List<P4BSallyPort> ports = (layout.ApproachDefense?.SallyPorts ?? Array.Empty<P4BSallyPort>()).ToList();
        List<P4BPost> posts = (layout.ApproachDefense?.Posts ?? Array.Empty<P4BPost>()).ToList();
        Dictionary<string, Dictionary<string, int>> rejections = IncompleteCrossingRejections(diagnostics);
        List<TurretCoverRejectedCandidate> coverRejections = TurretCoverRejections(diagnostics);
        List<TurretServiceRouteFailure> serviceRouteFailures = TurretServiceRouteFailures(layout, diagnostics);
        List<TurretFaceWalk> outerFaceWalks = TurretFaceWalks(layout);
        List<TurretWallLoopCell> outerWallPerimeterLoop = TurretOuterWallLoop(layout);
        List<TurretFaceCoverageGap> faceCoverageGaps = TurretFacesWithoutNearbyNest(layout);
        List<GridPoint> coverFailureCells = coverRejections.Where(c => c.Cell.HasValue)
            .Select(c => c.Cell.Value).Distinct().OrderBy(c => c).ToList();
        List<GridPoint> unreachableServiceEndpoints = serviceRouteFailures.SelectMany(r => r.ServiceEndpoints)
            .Distinct().OrderBy(c => c).ToList();
        StringBuilder json = new StringBuilder();
        json.AppendLine("{");
        json.Append("  \"base\": ").Append(Json(label)).AppendLine(",");
        json.Append("  \"layoutDigest\": ").Append(Json(layout.Digest)).AppendLine(",");
        List<P4BAttackSide> attackSides = (layout.ApproachDefense?.Sides ?? Array.Empty<P4BAttackSide>()).ToList();
        json.AppendLine("  \"attackSides\": [");
        for (int i = 0; i < attackSides.Count; i++)
        {
            P4BAttackSide side = attackSides[i];
            json.Append("    {\"id\": ").Append(Json(side.Id)).Append(", \"exposed\": ")
                .Append(side.Exposed ? "true" : "false").Append(", \"outward\": ")
                .Append(JsonPoint(side.Outward)).Append(", \"samples\": ").Append(JsonPoints(side.Samples))
                .Append(", \"positions\": ").Append(JsonPoints(side.Positions)).Append("}")
                .AppendLine(i + 1 == attackSides.Count ? string.Empty : ",");
        }
        json.AppendLine("  ],");
        string[] sidesWithoutNest = (layout.ApproachDefense?.Sides ?? Array.Empty<P4BAttackSide>())
            .Where(s => s.Exposed && s.Positions.Count == 0).Select(s => s.Id)
            .OrderBy(s => s, StringComparer.Ordinal).ToArray();
        json.Append("  \"sidesWithoutNest\": ").Append("[").Append(string.Join(",", sidesWithoutNest.Select(Json)))
            .AppendLine("],");
        string[] sidesMissingSupport = TurretSupportFailureSides(diagnostics)
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        json.Append("  \"candidateSidesMissingTurretSupport\": ").Append("[")
            .Append(string.Join(",", sidesMissingSupport.Select(Json))).AppendLine("],");
        json.AppendLine("  \"outerWallFaceWalks\": [");
        for (int i = 0; i < outerFaceWalks.Count; i++)
        {
            TurretFaceWalk walk = outerFaceWalks[i];
            json.Append("    {\"side\": ").Append(Json(walk.Side)).Append(", \"outward\": ")
                .Append(JsonPoint(walk.Outward)).Append(", \"cells\": ").Append(JsonPoints(walk.Cells))
                .Append(", \"crossings\": [");
            for (int j = 0; j < walk.Crossings.Count; j++)
            {
                TurretFaceCrossing crossing = walk.Crossings[j];
                json.Append("{\"gate\": ").Append(JsonPoint(crossing.Gate)).Append(", \"nest\": ")
                    .Append(JsonPoint(crossing.Nest)).Append(", \"faceCell\": ").Append(JsonPoint(crossing.Face))
                    .Append(", \"walkIndex\": ").Append(crossing.WalkIndex).Append("}")
                    .Append(j + 1 == walk.Crossings.Count ? string.Empty : ",");
            }
            json.Append("]}").AppendLine(i + 1 == outerFaceWalks.Count ? string.Empty : ",");
        }
        json.AppendLine("  ],");
        json.AppendLine("  \"outerWallPerimeterLoop\": [");
        for (int i = 0; i < outerWallPerimeterLoop.Count; i++)
        {
            TurretWallLoopCell cell = outerWallPerimeterLoop[i];
            json.Append("    {\"loopIndex\": ").Append(cell.LoopIndex).Append(", \"index\": ").Append(i).Append(", \"cell\": ")
                .Append(JsonPoint(cell.Cell)).Append(", \"outward\": ").Append(JsonPoint(cell.Outward))
                .Append(", \"side\": ").Append(Json(cell.Side)).Append("}")
                .AppendLine(i + 1 == outerWallPerimeterLoop.Count ? string.Empty : ",");
        }
        json.AppendLine("  ],");
        json.AppendLine("  \"faceCoverageGaps\": [");
        for (int i = 0; i < faceCoverageGaps.Count; i++)
        {
            TurretFaceCoverageGap gap = faceCoverageGaps[i];
            json.Append("    {\"side\": ").Append(Json(gap.Side)).Append(", \"faceType\": ")
                .Append(Json(gap.FieldFace ? "field" : "compound")).Append(", \"length\": ")
                .Append(gap.Length).Append(", \"nearestNestDistance\": ")
                .Append(gap.NearestNestDistance == double.MaxValue ? "null" : gap.NearestNestDistance.ToString("0.0"))
                .Append(", \"crossings\": ").Append(gap.NearbyCrossings).Append(", \"otherGateCandidates\": ")
                .Append(gap.LegalCandidates).Append(", \"reason\": ").Append(Json(gap.Reason))
                .Append(", \"cells\": ").Append(JsonPoints(gap.Cells)).Append("}")
                .AppendLine(i + 1 == faceCoverageGaps.Count ? string.Empty : ",");
        }
        json.AppendLine("  ],");
        List<TurretFaceCoverageGap> routeBlockedFaces = faceCoverageGaps
            .Where(g => g.Reason.Contains("arrival_route_along_face")).ToList();
        json.AppendLine("  \"routeBlockedFaces\": [");
        for (int i = 0; i < routeBlockedFaces.Count; i++)
        {
            TurretFaceCoverageGap gap = routeBlockedFaces[i];
            json.Append("    {\"side\": ").Append(Json(gap.Side)).Append(", \"faceType\": ")
                .Append(Json(gap.FieldFace ? "field" : "compound")).Append(", \"length\": ")
                .Append(gap.Length).Append(", \"reason\": ").Append(Json(gap.Reason))
                .Append(", \"cells\": ").Append(JsonPoints(gap.Cells)).Append("}")
                .AppendLine(i + 1 == routeBlockedFaces.Count ? string.Empty : ",");
        }
        json.AppendLine("  ],");
        HashSet<GridPoint> arrivalRouteCells = new HashSet<GridPoint>(layout.Gates.SelectMany(g => g.ExteriorRoute)
            .Concat(layout.FieldWall?.ArrivalRoute ?? Enumerable.Empty<GridPoint>()));
        List<GridPoint> nestRouteSharedArrival = posts.SelectMany(p => p.Route).Distinct()
            .Where(arrivalRouteCells.Contains).OrderBy(c => c).ToList();
        json.AppendLine("  \"arrivalRouteClearance\": {");
        json.Append("    \"arrivalCells\": ").Append(JsonPoints(arrivalRouteCells.OrderBy(c => c))).AppendLine(",");
        json.Append("    \"nestRouteSharedCells\": ").Append(JsonPoints(nestRouteSharedArrival)).AppendLine(",");
        json.Append("    \"nestSlotOverlaps\": ").Append(JsonPoints(posts.SelectMany(p => p.Slots)
            .Where(arrivalRouteCells.Contains).Distinct().OrderBy(c => c))).AppendLine(",");
        json.Append("    \"nestCoverOverlaps\": ").Append(JsonPoints(posts.SelectMany(p => p.CoverCells)
            .Where(arrivalRouteCells.Contains).Distinct().OrderBy(c => c))).AppendLine(",");
        json.Append("    \"turretFootprintOverlaps\": ").Append(JsonPoints(guns.SelectMany(g => g.Footprint)
            .Where(arrivalRouteCells.Contains).Distinct().OrderBy(c => c))).AppendLine(",");
        json.Append("    \"turretCoverOverlaps\": ").Append(JsonPoints(guns.SelectMany(g => g.CoverCells)
            .Where(arrivalRouteCells.Contains).Distinct().OrderBy(c => c))).AppendLine();
        json.AppendLine("  },");
        json.AppendLine("  \"serviceRouteFailures\": [");
        for (int i = 0; i < serviceRouteFailures.Count; i++)
        {
            TurretServiceRouteFailure failure = serviceRouteFailures[i];
            json.Append("    {\"gate\": ").Append(JsonPoint(failure.Gate)).Append(", \"side\": ")
                .Append(Json(failure.Side)).Append(", \"origin\": ").Append(JsonPoint(failure.Origin))
                .Append(", \"routeOrigin\": ").Append(JsonPoint(failure.RouteOrigin))
                .Append(", \"serviceEndpoints\": ").Append(JsonPoints(failure.ServiceEndpoints))
                .Append(", \"condition\": ").Append(Json(failure.Condition)).Append("}")
                .AppendLine(i + 1 == serviceRouteFailures.Count ? string.Empty : ",");
        }
        json.AppendLine("  ],");
        List<P4BPost> unmatchedFaceCrossings = (layout.ApproachDefense?.Posts ?? Array.Empty<P4BPost>())
            .GroupBy(p => new { p.Side, p.AccessGate }).Select(g => g.First())
            .Where(p => !outerFaceWalks.Any(w => w.Side == p.Side && w.Crossings.Any(c => c.Gate.Equals(p.AccessGate))))
            .OrderBy(p => p.Side, StringComparer.Ordinal).ThenBy(p => p.AccessGate).ToList();
        json.AppendLine("  \"crossingsWithoutOuterFaceWalk\": [");
        for (int i = 0; i < unmatchedFaceCrossings.Count; i++)
        {
            P4BPost post = unmatchedFaceCrossings[i];
            json.Append("    {\"side\": ").Append(Json(post.Side)).Append(", \"gate\": ")
                .Append(JsonPoint(post.AccessGate)).Append(", \"nest\": ").Append(JsonPoint(post.Cell))
                .Append("}").AppendLine(i + 1 == unmatchedFaceCrossings.Count ? string.Empty : ",");
        }
        json.AppendLine("  ],");
        json.AppendLine("  \"asciiGrid\": {");
        json.Append("    \"legend\": ").Append(Json(LayoutAsciiExport.Legend +
            "\n        '!' rejected turret-cover blocker cell  '?' unreachable non-cover service endpoint")).AppendLine(",");
        int minX, minZ, maxX, maxZ;
        AsciiBounds(layout, input.Terrain, 20, out minX, out minZ, out maxX, out maxZ);
        string[] rendered = LayoutAsciiExport.Render(input.Terrain, layout, 20).Split('\n');
        string[] rows = rendered.Where(row => row.Length > 0).TakeLast(maxZ - minZ + 1).ToArray();
        OverlayServiceRouteFailureCells(rows, minX, maxZ, unreachableServiceEndpoints);
        OverlayCoverFailureCells(rows, minX, maxZ, coverFailureCells);
        json.Append("    \"originOffset\": {\"x\": ").Append(minX).Append(", \"z\": ").Append(maxZ)
            .AppendLine("},");
        json.Append("    \"width\": ").Append(maxX - minX + 1).AppendLine(",");
        json.Append("    \"height\": ").Append(rows.Length).AppendLine(",");
        json.AppendLine("    \"rowOrder\": \"north_to_south\",");
        json.AppendLine("    \"rows\": [");
        for (int i = 0; i < rows.Length; i++)
            json.Append("      ").Append(Json(rows[i])).AppendLine(i + 1 == rows.Length ? string.Empty : ",");
        json.AppendLine("    ]");
        json.AppendLine("  },");

        json.AppendLine("  \"crossings\": [");
        for (int i = 0; i < ports.Count; i++)
        {
            P4BSallyPort port = ports[i];
            string id = (port.Field ? "field-sally@" : "compound-sally@") + port.Cell;
            List<P4BPost> crossingPosts = posts.Where(p => port.Doors.Contains(p.AccessGate)).ToList();
            string[] sides = crossingPosts.Select(p => p.Side).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToArray();
            List<P4BTurretPlan> crossingGuns = guns.Where(g => g.AccessGate.HasValue &&
                port.Doors.Contains(g.AccessGate.Value)).ToList();
            List<TurretCoverRejectedCandidate> crossingCoverRejections = coverRejections
                .Where(r => port.Doors.Contains(r.Gate)).ToList();
            List<TurretServiceRouteFailure> crossingRouteFailures = serviceRouteFailures
                .Where(r => port.Doors.Contains(r.Gate)).ToList();
            int minis = crossingGuns.Count(g => g.DefName == "Turret_MiniTurret");
            int heavies = crossingGuns.Count(g => g.DefName == "Turret_Autocannon" || g.DefName == "Turret_Sniper");
            bool failedAttempt = sides.Any(side => rejections.ContainsKey(port.Cell + "/" + side));
            string status = minis >= 3 ? "complete" : failedAttempt ? "failed" : "none";
            var top = sides.Where(side => rejections.ContainsKey(port.Cell + "/" + side))
                .SelectMany(side => rejections[port.Cell + "/" + side])
                .GroupBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(g => new { Code = g.Key, Count = g.Sum(x => x.Value) })
                .OrderByDescending(x => x.Count).ThenBy(x => x.Code, StringComparer.Ordinal).FirstOrDefault();
            json.AppendLine("    {");
            json.Append("      \"id\": ").Append(Json(id)).AppendLine(",");
            json.Append("      \"doorCells\": ").Append(JsonPoints(port.Doors)).AppendLine(",");
            json.Append("      \"side\": ").Append(Json(sides.Length == 0 ? "none" : string.Join(",", sides))).AppendLine(",");
            json.Append("      \"status\": ").Append(Json(status)).AppendLine(",");
            json.Append("      \"minis\": ").Append(minis).AppendLine(",");
            json.Append("      \"heavies\": {\"total\": ").Append(heavies)
                .Append(", \"autocannon\": ").Append(crossingGuns.Count(g => g.DefName == "Turret_Autocannon"))
                .Append(", \"sniper\": ").Append(crossingGuns.Count(g => g.DefName == "Turret_Sniper")).AppendLine("},");
            json.Append("      \"spacingFallbackLevels\": {\"radiusPlus2\": ")
                .Append(crossingGuns.Count(g => g.SpacingLevel == 0)).Append(", \"radius\": ")
                .Append(crossingGuns.Count(g => g.SpacingLevel == 1)).Append(", \"miniBelowRadius\": ")
                .Append(crossingGuns.Count(g => g.SpacingLevel == 2)).AppendLine("},");
            json.Append("      \"topRejection\": ");
            if (top == null) json.AppendLine("null,");
            else json.Append("{\"code\": ").Append(Json(top.Code)).Append(", \"count\": ").Append(top.Count).AppendLine("},");
            json.AppendLine("      \"rejectedCandidateOriginSamples\": [");
            for (int j = 0; j < crossingCoverRejections.Count; j++)
            {
                TurretCoverRejectedCandidate rejected = crossingCoverRejections[j];
                json.Append("        {\"code\": \"turret_cover\", \"side\": ").Append(Json(rejected.Side))
                    .Append(", \"origin\": ").Append(JsonPoint(rejected.Origin)).Append(", \"cell\": ");
                if (rejected.Cell.HasValue) json.Append(JsonPoint(rejected.Cell.Value));
                else json.Append("null");
                json.Append(", \"condition\": ").Append(Json(rejected.Condition)).Append("}")
                    .AppendLine(j + 1 == crossingCoverRejections.Count ? string.Empty : ",");
            }
            json.AppendLine("      ],");
            json.AppendLine("      \"rejectedCandidateOriginSamplesNote\": \"Complete turret_cover candidate origin, blocker cell, and condition records.\",");
            json.AppendLine("      \"serviceRouteFailureSamples\": [");
            for (int j = 0; j < crossingRouteFailures.Count; j++)
            {
                TurretServiceRouteFailure failedRoute = crossingRouteFailures[j];
                json.Append("        {\"side\": ").Append(Json(failedRoute.Side))
                    .Append(", \"origin\": ").Append(JsonPoint(failedRoute.Origin))
                    .Append(", \"routeOrigin\": ").Append(JsonPoint(failedRoute.RouteOrigin))
                    .Append(", \"serviceEndpoints\": ").Append(JsonPoints(failedRoute.ServiceEndpoints))
                    .Append(", \"condition\": ").Append(Json(failedRoute.Condition)).Append("}")
                    .AppendLine(j + 1 == crossingRouteFailures.Count ? string.Empty : ",");
            }
            json.AppendLine("      ]");
            json.Append("    }").AppendLine(i + 1 == ports.Count ? string.Empty : ",");
        }
        json.AppendLine("  ],");

        json.AppendLine("  \"coverFailureCells\": [");
        var failureCellGroups = coverRejections.Where(r => r.Cell.HasValue).GroupBy(r => r.Cell.Value)
            .OrderBy(g => g.Key).ToList();
        for (int i = 0; i < failureCellGroups.Count; i++)
        {
            var group = failureCellGroups[i];
            json.Append("    {\"cell\": ").Append(JsonPoint(group.Key)).Append(", \"rejectedAttempts\": ")
                .Append(group.Count()).Append(", \"conditions\": [")
                .Append(string.Join(",", group.Select(r => r.Condition).Distinct().OrderBy(c => c, StringComparer.Ordinal)
                    .Select(Json))).Append("]}")
                .AppendLine(i + 1 == failureCellGroups.Count ? string.Empty : ",");
        }
        json.AppendLine("  ],");

        json.AppendLine("  \"unreachableServiceEndpoints\": [");
        var endpointGroups = serviceRouteFailures.SelectMany(r => r.ServiceEndpoints.Select(c => new { Cell = c, Route = r }))
            .GroupBy(x => x.Cell).OrderBy(g => g.Key).ToList();
        for (int i = 0; i < endpointGroups.Count; i++)
        {
            var group = endpointGroups[i];
            json.Append("    {\"cell\": ").Append(JsonPoint(group.Key)).Append(", \"rejectedAttempts\": ")
                .Append(group.Count()).Append(", \"candidateOrigins\": ")
                .Append(JsonPoints(group.Select(x => x.Route.Origin).Distinct().OrderBy(c => c)))
                .Append("}").AppendLine(i + 1 == endpointGroups.Count ? string.Empty : ",");
        }
        json.AppendLine("  ],");

        json.AppendLine("  \"turrets\": [");
        for (int i = 0; i < guns.Count; i++)
        {
            P4BTurretPlan gun = guns[i];
            (int exposure, int answered) = gunMetrics[gun];
            json.AppendLine("    {");
            json.Append("      \"type\": ").Append(Json(gun.DefName)).AppendLine(",");
            json.Append("      \"betweenNests\": ").Append(gun.BetweenNests ? "true" : "false").AppendLine(",");
            json.Append("      \"footprint\": ").Append(JsonPoints(gun.Footprint)).AppendLine(",");
            json.Append("      \"coverCells\": ").Append(JsonPoints(gun.CoverCells)).AppendLine(",");
            json.Append("      \"crossingId\": ").Append(Json(CrossingName(layout,
                gun.AccessGate ?? new GridPoint(-1, -1)))).AppendLine(",");
            json.Append("      \"exposure\": {\"answered\": ").Append(answered)
                .Append(", \"unanswered\": ").Append(exposure - answered).AppendLine("}");
            json.Append("    }").AppendLine(i + 1 == guns.Count ? string.Empty : ",");
        }
        json.AppendLine("  ],");

        json.AppendLine("  \"nestSlots\": [");
        for (int i = 0; i < posts.Count; i++)
        {
            P4BPost post = posts[i];
            json.Append("    {\"crossingId\": ").Append(Json(CrossingName(layout, post.AccessGate)))
                .Append(", \"side\": ").Append(Json(post.Side)).Append(", \"accessGate\": ")
                .Append(JsonPoint(post.AccessGate)).Append(", \"slots\": ").Append(JsonPoints(post.Slots))
                .Append(", \"coverCells\": ").Append(JsonPoints(post.CoverCells)).Append("}")
                .AppendLine(i + 1 == posts.Count ? string.Empty : ",");
        }
        json.AppendLine("  ],");
        json.Append("  \"sniperPosts\": ").Append(JsonPoints(staffing?.SniperPosts ?? Array.Empty<GridPoint>())).AppendLine(",");

        json.AppendLine("  \"attackerExposureGrid\": [");
        List<GridPoint> attackers = new List<GridPoint>();
        for (int x = 0; x < input.Terrain.Width; x += 2)
            for (int z = 0; z < input.Terrain.Height; z += 2) attackers.Add(new GridPoint(x, z));
        for (int i = 0; i < attackers.Count; i++)
        {
            GridPoint cell = attackers[i];
            bool reachable = layout.ApproachDefense.AttackerCanReach(cell);
            bool sees = reachable && guns.Any(g =>
            {
                if (Distance(g.Origin, cell) > EnemySniperRange) return false;
                HashSet<GridPoint> blockers = new HashSet<GridPoint>(opaque);
                blockers.ExceptWith(g.Footprint);
                return P4BFireLane.CanEngage(cell, g.Origin, 0, 45, blockers);
            });
            bool answered = sees && IsExposureAnswered(cell, guns, staffing, opaque);
            json.Append("    {\"x\": ").Append(cell.X).Append(", \"z\": ").Append(cell.Z)
                .Append(", \"attackerReachable\": ").Append(reachable ? "true" : "false")
                .Append(", \"canSeeAnyTurret\": ").Append(sees ? "true" : "false")
                .Append(", \"answered\": ").Append(answered ? "true" : "false").Append("}")
                .AppendLine(i + 1 == attackers.Count ? string.Empty : ",");
        }
        json.AppendLine("  ]");
        json.AppendLine("}");
        File.WriteAllText(Path.Combine(directory, label + ".json"), json.ToString());
    }

    private static Dictionary<string, Dictionary<string, int>> IncompleteCrossingRejections(
        IEnumerable<LayoutDiagnostic> diagnostics)
    {
        Dictionary<string, Dictionary<string, int>> totals = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        foreach (LayoutDiagnostic diagnostic in diagnostics ?? Enumerable.Empty<LayoutDiagnostic>())
        {
            if (diagnostic.Stage != "turret") continue;
            Match decisions = Regex.Match(diagnostic.Detail, @"decisions=\[(.*)\]");
            if (!decisions.Success) continue;
            string[] tokens = decisions.Groups[1].Value.Split(';');
            HashSet<string> incomplete = new HashSet<string>(tokens
                .Where(t => t.EndsWith(":incomplete_battery_removed", StringComparison.Ordinal))
                .Select(t => t.Substring(0, t.IndexOf(':'))), StringComparer.Ordinal);
            foreach (string token in tokens)
            {
                int split = token.IndexOf(':');
                if (split <= 0) continue;
                string key = token.Substring(0, split);
                if (!incomplete.Contains(key) || token.EndsWith(":incomplete_battery_removed", StringComparison.Ordinal)) continue;
                if (!totals.TryGetValue(key, out Dictionary<string, int> counts))
                    totals[key] = counts = new Dictionary<string, int>(StringComparer.Ordinal);
                string reason = StripTurretFunnel(token.Substring(split + 1));
                MatchCollection matches = Regex.Matches(reason, @"([a-z][a-z0-9_]*):(\d+)");
                foreach (Match match in matches)
                {
                    string code = match.Groups[1].Value;
                    counts.TryGetValue(code, out int old);
                    counts[code] = old + int.Parse(match.Groups[2].Value);
                }
                if (matches.Count == 0 && !reason.StartsWith("accepted ", StringComparison.Ordinal) &&
                    reason != "incomplete_battery_removed")
                {
                    counts.TryGetValue(reason, out int old);
                    counts[reason] = old + 1;
                }
            }
        }
        return totals;
    }

    private static bool IsExposureAnswered(GridPoint cell, IReadOnlyList<P4BTurretPlan> guns,
        P4BStaffingAudit staffing, HashSet<GridPoint> opaque)
    {
        if (staffing != null && staffing.SniperPosts.Any(post => Distance(post, cell) <= EnemySniperRange &&
            P4BFireLane.CanEngage(post, cell, 0, 45, opaque))) return true;
        if (guns.Where(g => g.DefName == "Turret_Sniper").Any(slug =>
        {
            double distance = Distance(slug.Origin, cell);
            HashSet<GridPoint> blockers = new HashSet<GridPoint>(opaque);
            blockers.ExceptWith(slug.Footprint);
            return distance >= slug.MinimumRange && distance <= slug.MaximumRange &&
                P4BFireLane.CanEngage(slug.Origin, cell, 12, 46, blockers);
        })) return true;
        return guns.Where(g => g.DefName == "Turret_MiniTurret").Any(mini =>
            Distance(mini.Origin, cell) <= mini.MaximumRange && P4BFireLane.CanEngage(mini.Origin, cell,
                0, (int)Math.Floor(mini.MaximumRange), opaque));
    }

    private static void AsciiBounds(ProceduralLayout layout, TerrainSnapshot terrain, int margin,
        out int minX, out int minZ, out int maxX, out int maxZ)
    {
        GridRect bounds = layout.BoundingRect;
        List<GridPoint> extras = layout.Gates.SelectMany(g => g.BandWalls.Concat(new[] { g.Staging }))
            .Concat(layout.OuterWalls).Concat(layout.FieldWall?.Walls ?? Array.Empty<GridPoint>())
            .Concat(layout.ApproachDefense?.CoverCells ?? Array.Empty<GridPoint>())
            .Concat(layout.Turret?.All.SelectMany(g => g.Footprint.Concat(g.CoverCells)) ?? Array.Empty<GridPoint>()).ToList();
        minX = Math.Max(0, Math.Min(bounds.MinX, extras.Select(c => c.X).DefaultIfEmpty(bounds.MinX).Min()) - margin);
        minZ = Math.Max(0, Math.Min(bounds.MinZ, extras.Select(c => c.Z).DefaultIfEmpty(bounds.MinZ).Min()) - margin);
        maxX = Math.Min(terrain.Width - 1, Math.Max(bounds.MinX + bounds.Width - 1,
            extras.Select(c => c.X).DefaultIfEmpty(bounds.MinX + bounds.Width - 1).Max()) + margin);
        maxZ = Math.Min(terrain.Height - 1, Math.Max(bounds.MinZ + bounds.Height - 1,
            extras.Select(c => c.Z).DefaultIfEmpty(bounds.MinZ + bounds.Height - 1).Max()) + margin);
    }

    private static void OverlayCoverFailureCells(string[] rows, int minX, int maxZ,
        IEnumerable<GridPoint> cells)
    {
        foreach (GridPoint cell in cells ?? Enumerable.Empty<GridPoint>())
        {
            int rowIndex = maxZ - cell.Z;
            int column = cell.X - minX;
            if (rowIndex < 0 || rowIndex >= rows.Length || column < 0 || column >= rows[rowIndex].Length) continue;
            char[] row = rows[rowIndex].ToCharArray();
            row[column] = '!';
            rows[rowIndex] = new string(row);
        }
    }

    private static void OverlayServiceRouteFailureCells(string[] rows, int minX, int maxZ,
        IEnumerable<GridPoint> cells)
    {
        foreach (GridPoint cell in cells ?? Enumerable.Empty<GridPoint>())
        {
            int rowIndex = maxZ - cell.Z;
            int column = cell.X - minX;
            if (rowIndex < 0 || rowIndex >= rows.Length || column < 0 || column >= rows[rowIndex].Length) continue;
            char[] row = rows[rowIndex].ToCharArray();
            row[column] = '?';
            rows[rowIndex] = new string(row);
        }
    }

    private static string JsonPoints(IEnumerable<GridPoint> points) => "[" + string.Join(",", (points ?? Enumerable.Empty<GridPoint>())
        .Select(JsonPoint)) + "]";
    private static string JsonPoint(GridPoint point) => "{\"x\":" + point.X + ",\"z\":" + point.Z + "}";
    private static string Json(string value) => "\"" + (value ?? string.Empty).Replace("\\", "\\\\")
        .Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";

    private static string TopIncompleteBatteryRejections(IEnumerable<LayoutDiagnostic> diagnostics)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (LayoutDiagnostic diagnostic in diagnostics ?? Enumerable.Empty<LayoutDiagnostic>())
        {
            if (diagnostic.Stage != "turret") continue;
            // Decision entries include nested rejectedBefore=[...] lists, so
            // capture through the final closing bracket of the decision vector.
            Match decisions = Regex.Match(diagnostic.Detail, @"decisions=\[(.*)\]");
            if (!decisions.Success) continue;
            string[] tokens = decisions.Groups[1].Value.Split(';');
            HashSet<string> incomplete = new HashSet<string>(tokens
                .Where(t => t.EndsWith(":incomplete_battery_removed", StringComparison.Ordinal))
                .Select(t => t.Substring(0, t.IndexOf(':'))), StringComparer.Ordinal);
            foreach (string token in tokens)
            {
                int split = token.IndexOf(':');
                if (split <= 0 || !incomplete.Contains(token.Substring(0, split))) continue;
                string reason = StripTurretFunnel(token.Substring(split + 1));
                MatchCollection reasonCounts = Regex.Matches(reason, @"([a-z][a-z0-9_]*):(\d+)");
                foreach (Match match in reasonCounts)
                {
                    string code = match.Groups[1].Value;
                    int count = int.Parse(match.Groups[2].Value);
                    counts.TryGetValue(code, out int existing);
                    counts[code] = existing + count;
                }
                if (reasonCounts.Count == 0 && !reason.StartsWith("accepted ", StringComparison.Ordinal) &&
                    reason != "incomplete_battery_removed")
                {
                    counts.TryGetValue(reason, out int existing);
                    counts[reason] = existing + 1;
                }
            }
        }
        return string.Join(",", counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(5).Select(kv => kv.Key + ":" + kv.Value));
    }

    private static int PhysicalTurretImportValue(P4BTurretPlan gun) =>
        gun.ImportValue - (gun.Support?.ImportValue ?? 0) - gun.AdditionalGroups.Sum(group => group.ImportValue);

    private static string TurretSupportFailureSides(IEnumerable<LayoutDiagnostic> diagnostics)
    {
        HashSet<string> sides = new HashSet<string>(StringComparer.Ordinal);
        foreach (LayoutDiagnostic diagnostic in diagnostics ?? Enumerable.Empty<LayoutDiagnostic>())
        {
            if (diagnostic.Stage != "turret") continue;
            foreach (Match match in Regex.Matches(diagnostic.Detail, @"noNearbyNestSides=\[([^\]]*)\]"))
                foreach (string side in match.Groups[1].Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    sides.Add(side);
        }
        return string.Join(",", sides.OrderBy(s => s, StringComparer.Ordinal));
    }

    private static string StripTurretFunnel(string reason)
    {
        int index = reason.IndexOf(" betweenNestsFrontageRejects=", StringComparison.Ordinal);
        return index < 0 ? reason : reason.Substring(0, index);
    }

    private static void PrintPowerLedgerDiagnostics(ProceduralLayoutInput input, ProceduralLayout layout, string label)
    {
        string dispatchable = input.Equipment.Notes.FirstOrDefault(note => note.StartsWith("dispatchable:", StringComparison.Ordinal))
            ?? "dispatchable:none";
        string renewables = string.Join(",", layout.PowerYards.Where(y => y.DefName.Contains("Generator"))
            .GroupBy(y => y.DefName, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key + ":" + g.Count()));
        string dispatchablePlan = layout.Power == null ? "none" : layout.Power.GeneratorDef + ":" + layout.Power.Generators;
        string powerFuel = layout.Power == null ? "none" : layout.Power.FuelDef + ":" + layout.Power.FuelStock;
        Console.WriteLine("  power-t1 " + label + " dispatchablePlan=" + dispatchablePlan +
            " renewables=" + (renewables.Length == 0 ? "none" : renewables) + " powerFuel=" + powerFuel +
            " fuel=" + layout.Materials.GeneratorFuelDef + ":" + layout.Materials.GeneratorFuel +
            " fuelImported=" + layout.Materials.GeneratorFuelImported +
            " import=" + layout.Materials.ImportValue + " " + dispatchable);
    }

    private static (int Exposure, int Answered) TurretExposure(ProceduralLayoutInput input,
        ProceduralLayout layout, P4BTurretPlan gun, IReadOnlyList<P4BTurretPlan> guns,
        P4BStaffingAudit staffing, HashSet<GridPoint> baseOpaque)
    {
        HashSet<GridPoint> lineBlockers = new HashSet<GridPoint>(baseOpaque.Concat(guns
            .Where(other => !ReferenceEquals(other, gun)).SelectMany(other => other.Footprint)));
        HashSet<GridPoint> defenderBlockers = new HashSet<GridPoint>(baseOpaque.Concat(guns.SelectMany(other => other.Footprint)));
        int exposure = 0, answered = 0;
        for (int x = 0; x < input.Terrain.Width; x += 2)
            for (int z = 0; z < input.Terrain.Height; z += 2)
            {
                GridPoint attacker = new GridPoint(x, z);
                if (!layout.ApproachDefense.AttackerCanReach(attacker) ||
                    Distance(gun.Origin, attacker) > EnemySniperRange ||
                    !P4BFireLane.CanEngage(attacker, gun.Origin, 0, 45, lineBlockers)) continue;
                exposure++;
                bool answeredByPost = staffing.SniperPosts.Any(post =>
                    Distance(post, attacker) <= EnemySniperRange &&
                    P4BFireLane.CanEngage(post, attacker, 0, 45, defenderBlockers));
                bool answeredBySlug = guns.Where(other => other.DefName == "Turret_Sniper").Any(slug =>
                {
                    double distance = Distance(slug.Origin, attacker);
                    HashSet<GridPoint> slugBlockers = new HashSet<GridPoint>(baseOpaque.Concat(guns
                        .Where(other => !ReferenceEquals(other, slug)).SelectMany(other => other.Footprint)));
                    return distance >= slug.MinimumRange && distance <= slug.MaximumRange &&
                        P4BFireLane.CanEngage(slug.Origin, attacker, 0, 45, slugBlockers);
                });
                bool answeredByMini = gun.DefName == "Turret_MiniTurret" &&
                    Distance(gun.Origin, attacker) <= 28.9 &&
                    P4BFireLane.CanEngage(gun.Origin, attacker, 0, 29, lineBlockers);
                if (answeredByPost || answeredBySlug || answeredByMini) answered++;
            }
        return (exposure, answered);
    }

    private static string CrossingName(ProceduralLayout layout, GridPoint accessGate)
    {
        P4BSallyPort port = layout.ApproachDefense?.SallyPorts.FirstOrDefault(p => p.Doors.Contains(accessGate));
        if (port != null) return (port.Field ? "field" : "compound") + "-sally@" + port.Cell;
        LayoutGate arrival = layout.Gates.FirstOrDefault(g => g.Cell.Equals(accessGate) ||
            g.BandDoors.Contains(accessGate));
        if (arrival != null) return "arrival@" + arrival.Cell;
        return "access@" + accessGate;
    }

    private static double Distance(GridPoint a, GridPoint b)
    {
        long dx = a.X - b.X, dz = a.Z - b.Z;
        return Math.Sqrt(dx * dx + dz * dz);
    }
}
