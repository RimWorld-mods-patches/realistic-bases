using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    // This is an ownership ledger, not an instruction to mutate the map.  The
    // spawner records the original rock/roof values for precisely these cells
    // before it touches them and restores those values on every failed spawn.
    public sealed class TerrainMutationFootprint
    {
        public TerrainMutationFootprint(IEnumerable<GridPoint> excavationCells,
            IEnumerable<GridPoint> roofClearanceCells, IEnumerable<GridPoint> constructedRoofCells,
            IEnumerable<GridPoint> preservedNaturalRockCells)
        {
            ExcavationCells = Copy(excavationCells);
            RoofClearanceCells = Copy(roofClearanceCells);
            ConstructedRoofCells = Copy(constructedRoofCells);
            PreservedNaturalRockCells = Copy(preservedNaturalRockCells);
            Digest = ComputeDigest();
        }

        public IReadOnlyList<GridPoint> ExcavationCells { get; }
        public IReadOnlyList<GridPoint> RoofClearanceCells { get; }
        public IReadOnlyList<GridPoint> ConstructedRoofCells { get; }
        public IReadOnlyList<GridPoint> PreservedNaturalRockCells { get; }
        public bool HasOwnedLandformMutation => ExcavationCells.Count != 0 || RoofClearanceCells.Count != 0;
        public string Digest { get; }

        private static IReadOnlyList<GridPoint> Copy(IEnumerable<GridPoint> cells) =>
            new ReadOnlyCollection<GridPoint>((cells ?? Enumerable.Empty<GridPoint>()).Distinct().OrderBy(c => c).ToList());

        private string ComputeDigest()
        {
            ulong hash = StableHash.Start;
            hash = StableHash.Add(hash, "p4-terrain-mutation-v1");
            foreach (GridPoint cell in ExcavationCells) hash = Add(hash, cell, 1);
            foreach (GridPoint cell in RoofClearanceCells) hash = Add(hash, cell, 2);
            foreach (GridPoint cell in ConstructedRoofCells) hash = Add(hash, cell, 3);
            foreach (GridPoint cell in PreservedNaturalRockCells) hash = Add(hash, cell, 4);
            return StableHash.Hex(hash);
        }

        private static ulong Add(ulong hash, GridPoint cell, int kind)
        {
            hash = StableHash.Add(hash, kind);
            hash = StableHash.Add(hash, cell.X);
            return StableHash.Add(hash, cell.Z);
        }
    }

    public static class TerrainMutationPlanner
    {
        private static readonly GridPoint[] Neighbors =
        {
            new GridPoint(0, -1), new GridPoint(-1, 0), new GridPoint(1, 0), new GridPoint(0, 1)
        };

        public static TerrainMutationFootprint Plan(TerrainArchetype archetype, TerrainSnapshot terrain,
            GridRect compound, IEnumerable<PhysicalFacility> facilities)
        {
            if (terrain == null) throw new ArgumentNullException(nameof(terrain));
            HashSet<GridPoint> compoundCells = new HashSet<GridPoint>(compound.Points());
            HashSet<GridPoint> excavations = archetype == TerrainArchetype.ExcavatedMountain
                ? new HashSet<GridPoint>(compoundCells.Where(c => terrain[c].Has(TerrainTraits.NaturalRock | TerrainTraits.Roofed)))
                : new HashSet<GridPoint>();
            HashSet<GridPoint> constructedRoofs = new HashSet<GridPoint>((facilities ?? Enumerable.Empty<PhysicalFacility>())
                .Where(f => f.Roofed).SelectMany(f => f.Bounds.Points()));
            HashSet<GridPoint> preserved = new HashSet<GridPoint>();
            foreach (GridPoint cell in terrain.Points())
            {
                if (!terrain[cell].Has(TerrainTraits.NaturalRock) || excavations.Contains(cell)) continue;
                if (Neighbors.Any(n => compoundCells.Contains(new GridPoint(cell.X + n.X, cell.Z + n.Z)))) preserved.Add(cell);
            }
            return new TerrainMutationFootprint(excavations, excavations, constructedRoofs, preserved);
        }

        public static IReadOnlyList<string> Validate(TerrainArchetype archetype, TerrainSnapshot terrain,
            TerrainMutationFootprint footprint)
        {
            List<string> errors = new List<string>();
            if (footprint == null) return new[] { "missing_mutation_footprint" };
            if (archetype != TerrainArchetype.ExcavatedMountain && footprint.HasOwnedLandformMutation)
                errors.Add("unexpected_landform_mutation");
            foreach (GridPoint cell in footprint.ExcavationCells)
            {
                if (!terrain.InBounds(cell) || !terrain[cell].Has(TerrainTraits.NaturalRock | TerrainTraits.Roofed))
                    errors.Add("excavation_not_roofed_natural_rock");
                if (!footprint.RoofClearanceCells.Contains(cell)) errors.Add("excavation_without_roof_ledger");
            }
            if (footprint.ExcavationCells.Intersect(footprint.PreservedNaturalRockCells).Any())
                errors.Add("excavates_preserved_boundary");
            if (footprint.ConstructedRoofCells.Any(c => !terrain.InBounds(c))) errors.Add("roof_out_of_bounds");
            return new ReadOnlyCollection<string>(errors.Distinct().ToList());
        }
    }
}
