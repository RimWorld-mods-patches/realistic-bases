using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    // A small resolved Core-only catalog. Values are material market value, not
    // new physical stock: the final layout has already funded its equipment.
    public sealed class P4CFloorDef
    {
        public P4CFloorDef(string defName, int valuePerCell, int woodPerCell = 0)
        {
            if (string.IsNullOrEmpty(defName) || valuePerCell < 0 || woodPerCell < 0)
                throw new ArgumentOutOfRangeException(nameof(defName));
            DefName = defName;
            ValuePerCell = valuePerCell;
            WoodPerCell = woodPerCell;
        }

        public string DefName { get; }
        public int ValuePerCell { get; }
        public int WoodPerCell { get; }
    }

    public sealed class P4CFinishSite
    {
        public P4CFinishSite(IEnumerable<GridPoint> floorable,
            IEnumerable<KeyValuePair<GridPoint, string>> smoothFloors = null,
            IEnumerable<KeyValuePair<GridPoint, string>> smoothWalls = null)
        {
            Floorable = new HashSet<GridPoint>(floorable ?? Enumerable.Empty<GridPoint>());
            SmoothFloors = new ReadOnlyDictionary<GridPoint, string>((smoothFloors ?? Enumerable.Empty<KeyValuePair<GridPoint, string>>())
                .ToDictionary(pair => pair.Key, pair => pair.Value));
            SmoothWalls = new ReadOnlyDictionary<GridPoint, string>((smoothWalls ?? Enumerable.Empty<KeyValuePair<GridPoint, string>>())
                .ToDictionary(pair => pair.Key, pair => pair.Value));
        }

        public IReadOnlyCollection<GridPoint> Floorable { get; }
        public IReadOnlyDictionary<GridPoint, string> SmoothFloors { get; }
        public IReadOnlyDictionary<GridPoint, string> SmoothWalls { get; }
    }

    public sealed class P4CFinishPlan
    {
        public const string Version = "p4c-finish.1";

        internal P4CFinishPlan(IEnumerable<KeyValuePair<GridPoint, string>> floors,
            IEnumerable<KeyValuePair<GridPoint, string>> walls, IEnumerable<string> reasons,
            int allowance, int importValue, int localWood)
        {
            Floors = new ReadOnlyDictionary<GridPoint, string>(new SortedDictionary<GridPoint, string>(
                floors.ToDictionary(pair => pair.Key, pair => pair.Value)));
            Walls = new ReadOnlyDictionary<GridPoint, string>(new SortedDictionary<GridPoint, string>(
                walls.ToDictionary(pair => pair.Key, pair => pair.Value)));
            Reasons = new ReadOnlyCollection<string>(reasons.OrderBy(r => r, StringComparer.Ordinal).ToList());
            Allowance = allowance;
            ImportValue = importValue;
            LocalWood = localWood;
            ulong hash = StableHash.Add(StableHash.Start, Version);
            foreach (KeyValuePair<GridPoint, string> pair in Floors)
            {
                hash = StableHash.Add(hash, pair.Key.X);
                hash = StableHash.Add(hash, pair.Key.Z);
                hash = StableHash.Add(hash, pair.Value);
            }
            foreach (KeyValuePair<GridPoint, string> pair in Walls)
            {
                hash = StableHash.Add(hash, pair.Key.X);
                hash = StableHash.Add(hash, pair.Key.Z);
                hash = StableHash.Add(hash, pair.Value);
            }
            foreach (string reason in Reasons) hash = StableHash.Add(hash, reason);
            hash = StableHash.Add(hash, allowance);
            hash = StableHash.Add(hash, importValue);
            hash = StableHash.Add(hash, localWood);
            Digest = StableHash.Hex(hash);
        }

        public IReadOnlyDictionary<GridPoint, string> Floors { get; }
        public IReadOnlyDictionary<GridPoint, string> Walls { get; }
        public IReadOnlyList<string> Reasons { get; }
        public int Allowance { get; }
        public int ImportValue { get; }
        public int LocalWood { get; }
        public string Digest { get; }
    }

    public static class P4CFinishPlanner
    {
        private static readonly GridPoint[] Cardinal =
        {
            new GridPoint(0, -1), new GridPoint(-1, 0), new GridPoint(1, 0), new GridPoint(0, 1)
        };

        public static P4CFinishPlan Plan(ProceduralLayoutInput input, ProceduralLayout layout,
            P4CFinishSite site, IEnumerable<P4CFloorDef> available)
        {
            if (input == null || layout == null || site == null) throw new ArgumentNullException();
            Dictionary<string, P4CFloorDef> defs = (available ?? Enumerable.Empty<P4CFloorDef>())
                .ToDictionary(d => d.DefName, StringComparer.Ordinal);
            int baseSpend = P4BApproachDefensePlanner.Materials(input, layout,
                layout.ApproachDefense?.CoverCells.Count() ?? 0).ImportValue +
                (layout.Turret?.ImportValue ?? 0);
            // This cap limits decorative wealth even on rich sites. Existing
            // defense and facility commitments have first claim on imports.
            int allowance = Math.Max(0, Math.Min(input.Requirements.ImportValueLimit - baseSpend,
                input.Requirements.ImportValueLimit * 35 / 100));
            int localWoodLeft = input.Resources.LocalWoodYield == int.MaxValue ? 0 :
                Math.Max(0, input.Resources.LocalWoodYield - layout.Materials.LocalWoodUsed);
            int localWood = 0, spent = 0;
            Dictionary<GridPoint, string> floors = new Dictionary<GridPoint, string>();
            Dictionary<GridPoint, string> walls = new Dictionary<GridPoint, string>();
            List<string> reasons = new List<string>();
            HashSet<GridPoint> protectedCells = new HashSet<GridPoint>(layout.FieldWall?.Fields ?? new GridPoint[0]);
            protectedCells.UnionWith(layout.JointSite?.Field.Points() ?? Enumerable.Empty<GridPoint>());
            protectedCells.UnionWith(layout.PowerYards.SelectMany(y => y.Cells));
            protectedCells.UnionWith(layout.Court?.Cells ?? new GridPoint[0]);
            HashSet<GridPoint> excavation = new HashSet<GridPoint>(layout.Excavation);
            HashSet<GridPoint> floorable = new HashSet<GridPoint>(site.Floorable.Except(protectedCells)
                .Where(c => input.Terrain.InBounds(c) && input.Terrain[c].Has(TerrainTraits.SupportsHeavy) &&
                    !input.Terrain[c].Has(TerrainTraits.Water) &&
                    (!input.Terrain[c].Has(TerrainTraits.Occupied) || excavation.Contains(c))));
            bool industrial = layout.TechStage.Level >= SettlementTechStageLevel.IndustrialEarly;
            bool wealthy = input.Requirements.Residents >= 9;
            string stone = input.Resources.LocalStoneStuffs
                .Select(s => s.StartsWith("Blocks", StringComparison.Ordinal) ? "Tile" + s.Substring(6) : null)
                .Where(s => s != null && defs.ContainsKey(s)).OrderBy(s => s, StringComparer.Ordinal).FirstOrDefault();

            int Priority(LayoutRoomKind kind)
            {
                switch (kind)
                {
                    case LayoutRoomKind.Kitchen: return 0;
                    case LayoutRoomKind.Medical: return 1;
                    case LayoutRoomKind.Freezer: return 2;
                    case LayoutRoomKind.Pantry: return 3;
                    case LayoutRoomKind.Workshop: return 4;
                    case LayoutRoomKind.Power: return 5;
                    case LayoutRoomKind.Storage: return 6;
                    case LayoutRoomKind.Jail: return 7;
                    case LayoutRoomKind.Dining: return 8;
                    case LayoutRoomKind.Bedroom: return 9;
                    default: return 10;
                }
            }

            foreach (LayoutRoom room in layout.Rooms.OrderBy(r => Priority(r.Kind)).ThenBy(r => r.Id, StringComparer.Ordinal))
            {
                List<GridPoint> cells = room.Interior.Points().OrderBy(c => c).ToList();
                if (cells.Count == 0 || cells.Any(c => !floorable.Contains(c) || floors.ContainsKey(c)))
                { reasons.Add("room:" + room.Id + ":unsupported_ground"); continue; }
                bool domestic = room.Kind == LayoutRoomKind.Barracks || room.Kind == LayoutRoomKind.Bedroom ||
                    room.Kind == LayoutRoomKind.Dining;
                bool clean = room.Kind == LayoutRoomKind.Kitchen || room.Kind == LayoutRoomKind.Medical ||
                    room.Kind == LayoutRoomKind.Freezer || room.Kind == LayoutRoomKind.Pantry;
                List<string> choices = new List<string>();
                if (room.Kind == LayoutRoomKind.Medical && wealthy) choices.Add("SterileTile");
                if (domestic && wealthy && stone != null) choices.Add(stone);
                if (domestic) choices.Add("WoodPlankFloor");
                if (industrial)
                {
                    if (clean || domestic) choices.Add("PavedTile");
                    choices.Add("Concrete");
                }
                // A pre-existing natural-rock floor can be smoothed only when
                // the whole interior resolves to one smooth terrain def.
                string smooth = cells.All(c => site.SmoothFloors.ContainsKey(c))
                    ? site.SmoothFloors[cells[0]] : null;
                if (smooth != null && cells.Any(c => site.SmoothFloors[c] != smooth)) smooth = null;
                if (smooth != null) choices.Insert(0, smooth);
                bool placed = false;
                foreach (string name in choices.Distinct(StringComparer.Ordinal))
                {
                    P4CFloorDef def;
                    if (!defs.TryGetValue(name, out def)) continue;
                    int wood = Math.Min(localWoodLeft, checked(cells.Count * def.WoodPerCell));
                    int cost = def.WoodPerCell == 0 ? checked(cells.Count * def.ValuePerCell) :
                        (int)Math.Ceiling((cells.Count * def.WoodPerCell - wood) *
                            (double)def.ValuePerCell / def.WoodPerCell);
                    if (cost > allowance - spent) continue;
                    foreach (GridPoint c in cells) floors.Add(c, name);
                    spent += cost;
                    localWood += wood;
                    localWoodLeft -= wood;
                    placed = true;
                    if (name != choices[0]) reasons.Add("room:" + room.Id + ":downgrade:" + choices[0] + "->" + name);
                    break;
                }
                if (!placed) reasons.Add("room:" + room.Id + ":no_legal_affordable_finish");
            }

            if (industrial)
            {
                P4CFloorDef pathDef;
                if (defs.TryGetValue("Concrete", out pathDef))
                    foreach (LayoutCorridor corridor in layout.Corridors.Where(c => c.Main).OrderBy(c => c.Id, StringComparer.Ordinal))
                    {
                        if (corridor.Cells.Any(c => !floorable.Contains(c) || floors.ContainsKey(c)))
                        { reasons.Add("corridor:" + corridor.Id + ":unsupported_ground"); continue; }
                        List<GridPoint> cells = corridor.Cells.Where(c => floorable.Contains(c) && !floors.ContainsKey(c))
                            .Distinct().OrderBy(c => c).ToList();
                        if (cells.Count < 2) continue;
                        int cost = checked(cells.Count * pathDef.ValuePerCell);
                        if (cost > allowance - spent) { reasons.Add("corridor:" + corridor.Id + ":budget"); continue; }
                        foreach (GridPoint c in cells) floors.Add(c, pathDef.DefName);
                        spent += cost;
                    }
            }

            // Smooth interior-facing retained stone only. Whole-cell changes on
            // an exposed exterior face are intentionally excluded. Recheck roof
            // support with every selected conversion counted as temporarily
            // absent, so no selected wall is the sole natural holder.
            HashSet<GridPoint> interior = new HashSet<GridPoint>(layout.Rooms.SelectMany(r => r.Interior.Points())
                .Concat(layout.Corridors.SelectMany(c => c.Cells)));
            HashSet<GridPoint> compound = new HashSet<GridPoint>(layout.JointSite?.Compound.Points() ?? layout.Enclosure);
            foreach (GridPoint c in layout.NaturalWalls.Concat(layout.Rooms.SelectMany(r => r.NaturalWalls)).Distinct().OrderBy(c => c))
            {
                string target;
                if (!site.SmoothWalls.TryGetValue(c, out target) || string.IsNullOrEmpty(target)) continue;
                if (walls.Count >= 160) { reasons.Add("rock:smoothing_cap"); break; }
                if (!Cardinal.Any(d => interior.Contains(new GridPoint(c.X + d.X, c.Z + d.Z)))) continue;
                if (Cardinal.Any(d =>
                {
                    GridPoint n = new GridPoint(c.X + d.X, c.Z + d.Z);
                    return input.Terrain.InBounds(n) && !compound.Contains(n) &&
                        input.Terrain[n].Has(TerrainTraits.Standable) &&
                        !input.Terrain[n].Has(TerrainTraits.NaturalRock);
                })) { reasons.Add("rock:" + c + ":exterior_face"); continue; }
                if (LayoutRoofSupport.Unsupported(input.Terrain,
                    layout.Excavation.Concat(walls.Keys).Concat(new[] { c }), layout.RetainedThickRoof).Count != 0)
                { reasons.Add("rock:" + c + ":roof_support"); continue; }
                walls.Add(c, target);
            }
            return new P4CFinishPlan(floors, walls, reasons, allowance, spent, localWood);
        }
    }
}
