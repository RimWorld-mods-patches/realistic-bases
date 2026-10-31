using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RealisticBases.Planning;

internal sealed class RejectedFieldCandidateExport : IDisposable
{
    private readonly string directory;
    private int index;
    private RejectedFieldCandidateExport(string directory)
    {
        this.directory = directory;
        Directory.CreateDirectory(directory);
        P4BFieldWallPlanner.ArrivalFailureObserver = Write;
    }
    public static IDisposable Capture(string directory) => string.IsNullOrEmpty(directory) ? null : new RejectedFieldCandidateExport(directory);
    public void Dispose() => P4BFieldWallPlanner.ArrivalFailureObserver = null;
    private static int[] Point(GridPoint c) => new[] { c.X, c.Z };
    private static int[] Rect(GridRect r) => new[] { r.MinX, r.MinZ, r.Width, r.Height };
    private static int[][] Points(IEnumerable<GridPoint> cells) => cells?.Distinct().OrderBy(c => c).Select(Point).ToArray();
    private static int[][] PathCells(IEnumerable<GridPoint> cells) => cells?.Select(Point).ToArray();
    private void Write(ProceduralLayoutInput input, ProceduralLayout layout, P4BFieldArrivalFailure failure)
    {
        index++;
        var terrain = input.Terrain;
        File.WriteAllText(Path.Combine(directory, "terrain.json"), JsonSerializer.Serialize(new
        {
            width = terrain.Width, height = terrain.Height, inputDigest = input.Digest,
            terrainDigest = terrain.Digest, traits = terrain.Points().Select(c => (int)terrain[c].Traits).ToArray()
        }));
        var data = new
        {
            index, stage = "field_arrival_route", inputDigest = input.Digest, layoutDigest = layout.Digest,
            residents = input.Requirements.Residents, requiredCrops = input.Requirements.FieldCells,
            maxEvaluations = input.Budget.MaxEvaluations, anchor = Point(layout.Anchor),
            compound = Rect(layout.JointSite.Compound), field = Rect(layout.JointSite.Field),
            crops = Points(failure.Crops), lanes = Points(failure.Lane),
            enclosure = Points(layout.Enclosure), walls = Points(layout.BuiltWalls.Concat(layout.OuterWalls)),
            naturalWalls = Points(layout.NaturalWalls), excavation = Points(layout.Excavation),
            corridors = Points(layout.Corridors.SelectMany(c => c.Cells)),
            rooms = layout.Rooms.Select(r => new
            {
                id = r.Id, kind = r.Kind.ToString(), bounds = Rect(r.Bounds), interior = Rect(r.Interior),
                capacity = r.Capacity, doors = Points(r.Doors)
            }).ToArray(),
            fixtures = layout.AllFixtures.Select(f => new { name = f.DefName, kind = f.Kind.ToString(), cells = Points(f.Cells) }).ToArray(),
            yards = layout.PowerYards.Select(y => new { name = y.DefName, cells = Points(y.Cells) }).ToArray(),
            gates = layout.Gates.Select(g => new
            {
                cell = Point(g.Cell), outward = Point(g.Outward), inside = Point(g.Inside),
                staging = Point(g.Staging), doors = Points(g.BandDoors), walls = Points(g.BandWalls),
                exteriorRoute = PathCells(g.ExteriorRoute)
            }).ToArray(),
            failure = new
            {
                gateIndex = failure.GateIndex, innerGate = Point(failure.InnerGate), outerGate = Point(failure.OuterGate),
                outward = Point(failure.Outward), walls = Points(failure.Walls), arrival = PathCells(failure.Arrival),
                invalidArrival = Points(failure.InvalidArrival), outsideRoute = PathCells(failure.OutsideRoute),
                occupied = Points(failure.Occupied), baseline = Points(failure.Baseline), doors = Points(failure.Doors)
            }
        };
        string json = JsonSerializer.Serialize(data);
        File.WriteAllText(Path.Combine(directory, "candidate-latest.json"), json);
        if (index <= 8) File.WriteAllText(Path.Combine(directory, "candidate-" + index + ".json"), json);
        Console.WriteLine("rejected-field-candidate index=" + index + " layout=" + layout.Digest +
            " gate=" + failure.GateIndex + " outsideFound=" + (failure.OutsideRoute != null) +
            " invalidArrival=" + failure.InvalidArrival.Length);
    }
}
