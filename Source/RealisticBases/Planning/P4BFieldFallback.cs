using System;
using System.Collections.Generic;
using System.Linq;

namespace RealisticBases.Planning
{
    public static class P4BFieldFallback
    {
        // Select existing compound-side gate posts that can fire along the
        // field crossing once the threatened compound gate is breached/open.
        // Other outer posts remain available while the compound is intact.
        public static IReadOnlyList<string> Select(ProceduralLayoutInput input, ProceduralLayout layout,
            EncounterPlan encounter)
        {
            if (input == null || layout == null || encounter == null) throw new ArgumentNullException();
            if (layout.FieldWall == null || layout.FieldWall.GateIndex < 0 ||
                layout.FieldWall.GateIndex >= layout.Gates.Count) return new string[0];
            LayoutGate gate = layout.Gates[layout.FieldWall.GateIndex];
            HashSet<GridPoint> opaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
            opaque.Remove(gate.Cell);
            opaque.ExceptWith(gate.BandDoors);
            List<GridPoint> crossing = Enumerable.Range(1, 8).Select(n =>
                new GridPoint(gate.Cell.X + gate.Outward.X * n, gate.Cell.Z + gate.Outward.Z * n))
                .Where(c => input.Terrain.InBounds(c) && !opaque.Contains(c) &&
                    input.Terrain[c].Has(TerrainTraits.Standable)).ToList();
            return encounter.Posts.Where(p => p.Inner && p.Id.StartsWith(
                    "gate-" + layout.FieldWall.GateIndex + "-", StringComparison.Ordinal) &&
                    crossing.Any(c => P4BFireLane.CanEngage(p.Cell, c, 0, 25, opaque)))
                .Select(p => p.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
        }
    }
}
