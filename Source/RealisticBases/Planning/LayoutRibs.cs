using System;
using System.Collections.Generic;
using System.Linq;

namespace RealisticBases.Planning
{
    // Corridor-first packing for open-ground styles: the main hallway gets a
    // dining hub with a corridor loop and ribs on both sides at two room
    // depths apart along its length, and rooms fill the blocks between
    // corridors. Ribs only grow outward; the cells where a
    // rib will continue or a new rib will start stay reserved, so rooms never
    // cross a corridor line. Rooms rank frontage by walking distance from the
    // dining hall, so food, then work, then housing fill outward.
    internal sealed partial class LayoutAttempt
    {
        private const byte RibReserve = 13;

        private sealed class Rib
        {
            public Segment Segment;
            public bool Closed;
        }

        private readonly List<Rib> ribs = new List<Rib>();
        private readonly HashSet<GridPoint> ribReserved = new HashSet<GridPoint>();
        // Spine columns (cell coordinate along the main axis) and sides where a rib may start.
        private readonly List<KeyValuePair<int, GridPoint>> ribSlots = new List<KeyValuePair<int, GridPoint>>();
        private GridPoint? hubOrigin;
        private int ribStep;
        private int streetVersion;
        private int walkVersion = -1;
        private Dictionary<GridPoint, int> walk;
        private int growths;
        // Field approach terminal and the direction along the field wall.
        private KeyValuePair<GridPoint, GridPoint>? fieldTip;
        private int fieldHalfTip;

        private bool OkForRib(GridPoint c) => grid.CanPlace(c) && (occ[grid.Index(c)] == Free || occ[grid.Index(c)] == RibReserve) &&
            (grid.At(c) != LayoutCellClass.Rock || CanExcavate(c));

        private static int Dot(GridPoint c, GridPoint v) => c.X * v.X + c.Z * v.Z;

        private Segment Spine => segments.First(s => s.Main);

        private GridPoint SpineWidth => new GridPoint(Spine.Axis.Z, Spine.Axis.X);

        // The spine cell on the given side of the section at a spine column, or null off the spine.
        private GridPoint? SpineEdge(int column, GridPoint side)
        {
            foreach (List<GridPoint> section in Spine.Sections)
                if (Dot(section[0], Spine.Axis) == column)
                    return side.Equals(SpineWidth) ? section[section.Count - 1] : section[0];
            return null;
        }

        // Spine, dining hub with its loop, and the first ribs left and right.
        private bool LayCorridors(out LayoutRoomDemand hubRoom)
        {
            hubRoom = demand.Rooms.FirstOrDefault(r => r.Kind == LayoutRoomKind.Dining);
            ribStep = LayoutClusters.TypicalDepth(demand.Rooms) + 3;
            List<int> hubColumns = hubRoom == null ? null : PlaceHub(hubRoom);
            AddFieldRibs();
            if (hubColumns == null)
            {
                if (hubRoom != null) Fail("hub_loop_blocked", hubRoom.Id + " spine=" + Spine.Sections.Count +
                    " doorTaken=" + hubMiss[0] + " roomUnfit=" + hubMiss[1] + " loopUnfit=" + hubMiss[2]);
                hubRoom = null;
                int middle = Dot(SpineMiddle(), Spine.Axis);
                hubColumns = new List<int> { middle };
            }
            GridPoint width = SpineWidth;
            GridPoint[] sides = { width, new GridPoint(-width.X, -width.Z) };
            foreach (int column in hubColumns)
                foreach (GridPoint side in sides)
                    if (!ribs.Any(r => r.Segment.Root.Equals(SpineEdge(column, side) ?? new GridPoint(-1, -1)) && r.Segment.Axis.Equals(side)))
                        AddRib(column, side, ribStep);
            int low = hubColumns.Min(), high = hubColumns.Max();
            int first = Spine.Sections.Min(s => Dot(s[0], Spine.Axis)), last = Spine.Sections.Max(s => Dot(s[0], Spine.Axis));
            for (int k = 1; ; k++)
            {
                bool any = false;
                foreach (int column in new[] { low - k * branchSpacing, high + k * branchSpacing })
                {
                    if (column < first || column > last) continue;
                    any = true;
                    foreach (GridPoint side in sides) ribSlots.Add(new KeyValuePair<int, GridPoint>(column, side));
                }
                if (!any) break;
            }
            // Every slot along the spine gets its rib up front; a blocked slot
            // stays available for growth.
            foreach (KeyValuePair<int, GridPoint> slot in ribSlots.ToList())
                if (AddRib(slot.Key, slot.Value, ribStep)) ribSlots.Remove(slot);
            RefreshReserves();
            notes.Add("corridors hub=" + (hubRoom == null ? "none" : hubRoom.Id) + " ribs=" + ribs.Count + " slots=" + ribSlots.Count +
                " ribStep=" + ribStep);
            return true;
        }

        // The first rib pair runs along the field wall on the compound side:
        // from both ends of the field approach tip, parallel to the wall, as
        // far as the wall reaches. Rooms then pack against the field.
        private void AddFieldRibs()
        {
            if (!fieldTip.HasValue || jointSite == null || jointSite.Wall.Count == 0) return;
            GridPoint terminal = fieldTip.Value.Key, tangent = fieldTip.Value.Value;
            int low = jointSite.Wall.Min(c => Dot(c, tangent)), high = jointSite.Wall.Max(c => Dot(c, tangent));
            foreach (int sign in new[] { -1, 1 })
            {
                GridPoint dir = new GridPoint(tangent.X * sign, tangent.Z * sign);
                GridPoint root = new GridPoint(terminal.X + dir.X * fieldHalfTip, terminal.Z + dir.Z * fieldHalfTip);
                Segment rib = new Segment { Id = "field-rib-" + (sign < 0 ? "low" : "high"), Axis = dir, Root = root };
                // Existing streets and plazas on the line are crossed, not duplicated.
                for (GridPoint c = new GridPoint(root.X + dir.X, root.Z + dir.Z);
                    Dot(c, tangent) >= low && Dot(c, tangent) <= high;
                    c = new GridPoint(c.X + dir.X, c.Z + dir.Z))
                {
                    if (IsPublic(c)) continue;
                    if (!OkForStreet(c)) break;
                    rib.Sections.Add(new List<GridPoint> { c });
                }
                if (rib.Sections.Count == 0 || !Claim(rib)) continue;
                rib.Keep = rib.Sections.Count;
                segments.Add(rib);
                ribs.Add(new Rib { Segment = rib, Closed = true });
            }
        }

        // The dining hall on the spine near its middle, with a corridor along
        // its other three sides that joins the spine twice. Returns the two
        // spine columns its side corridors start from.
        private readonly int[] hubMiss = new int[3];

        private List<int> PlaceHub(LayoutRoomDemand room)
        {
            GridPoint axis = Spine.Axis, width = SpineWidth;
            double mid = HubCentre();
            ulong hubSeed = LayoutSeed.Sub(seed, "hub");
            IEnumerable<int> order = Enumerable.Range(0, Spine.Sections.Count).OrderBy(i => Math.Abs(Dot(Spine.Sections[i][0], axis) - mid))
                .ThenBy(i => LayoutSeed.Key(hubSeed, i, 0, 0));
            GridPoint[] sides = LayoutSeed.Roll(hubSeed, "side", 2) == 0
                ? new[] { width, new GridPoint(-width.X, -width.Z) }
                : new[] { new GridPoint(-width.X, -width.Z), width };
            foreach (int i in order)
                foreach (GridPoint side in sides)
                    foreach (GridRect size in room.CandidateSizes.OrderByDescending(s => Math.Min(s.Width, s.Height)))
                    {
                        if (stats.Evaluations >= input.Budget.MaxEvaluations) { stats.BudgetExhausted = true; return null; }
                        stats.Evaluations++;
                        GridPoint street = side.Equals(width) ? Spine.Sections[i][Spine.Sections[i].Count - 1] : Spine.Sections[i][0];
                        GridPoint door = new GridPoint(street.X + side.X, street.Z + side.Z);
                        if (!grid.CanPlace(door) || occ[grid.Index(door)] != Free) { hubMiss[0]++; continue; }
                        Candidate candidate = Evaluate(room, door, side, (size.Width + 1) / 2, size.Width, size.Height);
                        if (candidate == null) { hubMiss[1]++; continue; }
                        List<Segment> loop = HubLoop(candidate.Bounds, axis, side);
                        if (loop == null) { hubMiss[2]++; continue; }
                        Commit(room, candidate);
                        foreach (Segment segment in loop)
                        {
                            Claim(segment);
                            segments.Add(segment);
                            streetVersion++;
                            if (!segment.Retain) ribs.Add(new Rib { Segment = segment });
                        }
                        AddSideDoors(room.Id);
                        hubOrigin = placed[room.Id].DoorOutside;
                        return loop.Where(s => !s.Retain).Select(s => Dot(s.Root, axis)).ToList();
                    }
            return null;
        }

        private int FieldLength(GridPoint axis) =>
            jointSite.Wall.Max(c => Dot(c, axis)) - jointSite.Wall.Min(c => Dot(c, axis)) + 1;

        // The middle of the spine along its axis. With a field, the line runs
        // from the field's far wall to the spine's far end.
        private double HubCentre()
        {
            GridPoint axis = Spine.Axis;
            int first = Spine.Sections.Min(x => Dot(x[0], axis)), last = Spine.Sections.Max(x => Dot(x[0], axis));
            if (jointSite == null || jointSite.Wall.Count == 0) return (first + last) / 2.0;
            int fieldLow = jointSite.Wall.Min(c => Dot(c, axis)), fieldHigh = jointSite.Wall.Max(c => Dot(c, axis));
            return fieldLow + fieldHigh < first + last ? (fieldLow + last) / 2.0 : (first + fieldHigh) / 2.0;
        }

        // One more door on each loop side of the dining hall, nearest the
        // middle of that wall, where no fixture or cooler uses the cell or
        // the floor behind it.
        private void AddSideDoors(string roomId)
        {
            LayoutRoom hall = placed[roomId];
            HashSet<GridPoint> furnished = new HashSet<GridPoint>(hall.Fixtures.SelectMany(f => f.Cells));
            GridPoint middle = new GridPoint(hall.Bounds.MinX + hall.Bounds.Width / 2, hall.Bounds.MinZ + hall.Bounds.Height / 2);
            List<GridPoint> doors = hall.Bounds.Points()
                .Where(c => hall.IsEdge(c) && !c.Equals(hall.Door) &&
                    !((c.X == hall.Bounds.MinX || c.X == hall.Bounds.MaxX) && (c.Z == hall.Bounds.MinZ || c.Z == hall.Bounds.MaxZ)))
                .Where(c =>
                {
                    GridPoint outside = hall.OutsideOf(c);
                    GridPoint inside = new GridPoint(2 * c.X - outside.X, 2 * c.Z - outside.Z);
                    return IsStreet(outside) && !hall.DoorOutside.Equals(outside) && !furnished.Contains(c) && !furnished.Contains(inside) &&
                        (grid.At(c) != LayoutCellClass.Rock || dug.Contains(c));
                })
                .GroupBy(c => hall.OutsideOf(c).X - c.X + 3 * (hall.OutsideOf(c).Z - c.Z))
                .Where(side => side.Key != -(hall.Facing.X + 3 * hall.Facing.Z))
                .Select(side => side.OrderBy(c => Manhattan(c, middle)).ThenBy(c => c).First())
                .ToList();
            if (doors.Count == 0) return;
            LayoutRoom doored = hall.WithSideDoors(doors);
            rooms[rooms.IndexOf(hall)] = doored;
            placed[roomId] = doored;
            foreach (GridPoint door in doors) { roomWalls.Remove(door); wallOwner.Remove(door); }
            notes.Add("hubSideDoors=" + string.Join(",", doors));
        }

        // Two side corridors from the spine past the back wall and a back
        // corridor between them; null when any cell is unusable.
        private List<Segment> HubLoop(GridRect bounds, GridPoint axis, GridPoint side)
        {
            List<GridPoint> cells = bounds.Points().ToList();
            int lowA = cells.Min(c => Dot(c, axis)) - 1, highA = cells.Max(c => Dot(c, axis)) + 1;
            int nearD = cells.Min(c => Dot(c, side)) - 1, backD = cells.Max(c => Dot(c, side)) + 1;
            List<Segment> loop = new List<Segment>();
            foreach (int a in new[] { lowA, highA })
            {
                GridPoint root = CellAt(axis, side, a, nearD);
                if (!IsStreet(root) || !Spine.Cells.Contains(root)) return null;
                Segment arm = new Segment { Id = "hub-" + (a == lowA ? "low" : "high"), Axis = side, Root = root };
                for (int d = nearD + 1; d <= backD; d++)
                {
                    GridPoint c = CellAt(axis, side, a, d);
                    if (IsPublic(c)) continue;
                    if (!OkForStreet(c)) return null;
                    arm.Sections.Add(new List<GridPoint> { c });
                }
                arm.Keep = arm.Sections.Count;
                loop.Add(arm);
            }
            Segment back = new Segment { Id = "hub-back", Axis = axis, Root = CellAt(axis, side, lowA, backD), Retain = true };
            for (int a = lowA + 1; a < highA; a++)
            {
                GridPoint c = CellAt(axis, side, a, backD);
                if (IsPublic(c)) continue;
                if (!OkForStreet(c)) return null;
                back.Sections.Add(new List<GridPoint> { c });
            }
            loop.Add(back);
            int dig = loop.SelectMany(s => s.Cells).Count(c => grid.At(c) == LayoutCellClass.Rock);
            if (dig > 0 && dug.Count + dig > Budget) return null;
            return loop;
        }

        // An existing corridor or plaza cell, which a new corridor joins instead of claiming.
        private bool IsPublic(GridPoint c) => IsStreet(c) || (grid.InBounds(c) && occ[grid.Index(c)] == Plaza);

        // The cell whose coordinate along axis is a and along side is d.
        private static GridPoint CellAt(GridPoint axis, GridPoint side, int a, int d) =>
            new GridPoint(axis.X * a + side.X * d, axis.Z * a + side.Z * d);

        // A rib from the spine at a column, outward on one side.
        private bool AddRib(int column, GridPoint side, int length)
        {
            GridPoint? root = SpineEdge(column, side);
            if (!root.HasValue) return false;
            Segment rib = new Segment { Id = "rib-" + ribs.Count, Axis = side, Root = root.Value };
            Rib entry = new Rib { Segment = rib };
            if (Grow(entry, length) < 2) return false;
            if (!Claim(rib)) return false;
            segments.Add(rib);
            ribs.Add(entry);
            streetVersion++;
            return true;
        }

        // Appends up to length cells at the rib tip; stops at an obstacle or on
        // meeting another corridor, which closes the rib.
        private int Grow(Rib rib, int length)
        {
            Segment s = rib.Segment;
            int added = 0;
            for (int k = 0; k < length && !rib.Closed; k++)
            {
                GridPoint tip = s.Sections.Count == 0 ? s.Root : s.Sections[s.Sections.Count - 1][0];
                GridPoint c = new GridPoint(tip.X + s.Axis.X, tip.Z + s.Axis.Z);
                if (!OkForRib(c)) { rib.Closed = true; break; }
                s.Sections.Add(new List<GridPoint> { c });
                added++;
                GridPoint ahead = new GridPoint(c.X + s.Axis.X, c.Z + s.Axis.Z);
                if (IsStreet(ahead) || (grid.InBounds(ahead) && occ[grid.Index(ahead)] == Plaza)) rib.Closed = true;
            }
            return added;
        }

        // Keeps every rib's continuation and every future rib start free of rooms.
        private void RefreshReserves()
        {
            foreach (GridPoint c in ribReserved) if (occ[grid.Index(c)] == RibReserve) occ[grid.Index(c)] = Free;
            ribReserved.Clear();
            Action<GridPoint, GridPoint> reserveLine = (from, dir) =>
            {
                for (int k = 1; k <= ribStep; k++)
                {
                    GridPoint c = new GridPoint(from.X + k * dir.X, from.Z + k * dir.Z);
                    if (!grid.CanPlace(c) || occ[grid.Index(c)] != Free) break;
                    occ[grid.Index(c)] = RibReserve;
                    ribReserved.Add(c);
                }
            };
            foreach (Rib rib in ribs.Where(r => !r.Closed))
                reserveLine(rib.Segment.Sections.Count == 0 ? rib.Segment.Root : rib.Segment.Sections[rib.Segment.Sections.Count - 1][0], rib.Segment.Axis);
            foreach (KeyValuePair<int, GridPoint> slot in ribSlots)
            {
                GridPoint? root = SpineEdge(slot.Key, slot.Value);
                if (root.HasValue) reserveLine(root.Value, slot.Value);
            }
        }

        private void ReleaseReserves()
        {
            foreach (GridPoint c in ribReserved) if (occ[grid.Index(c)] == RibReserve) occ[grid.Index(c)] = Free;
            ribReserved.Clear();
        }

        // One corridor growth step nearest the dining hall: extend a rib or
        // start the next one. False when nothing can grow.
        private bool GrowCorridors()
        {
            Dictionary<GridPoint, int> distance = WalkFromHub();
            Func<GridPoint, int> cost = c => distance.TryGetValue(c, out int d) ? d : 1000;
            var options = new List<KeyValuePair<int, Func<bool>>>();
            foreach (Rib rib in ribs.Where(r => !r.Closed))
            {
                Rib r = rib;
                GridPoint tip = r.Segment.Sections.Count == 0 ? r.Segment.Root : r.Segment.Sections[r.Segment.Sections.Count - 1][0];
                options.Add(new KeyValuePair<int, Func<bool>>(cost(tip), () =>
                {
                    int before = r.Segment.Sections.Count;
                    ReleaseReserves();
                    int added = Grow(r, ribStep);
                    if (added == 0) return false;
                    List<GridPoint> fresh = r.Segment.Sections.Skip(before).SelectMany(x => x).ToList();
                    Segment probe = new Segment { Sections = fresh.Select(c => new List<GridPoint> { c }).ToList() };
                    if (!Claim(probe))
                    {
                        r.Segment.Sections.RemoveRange(before, added);
                        r.Closed = true;
                        return false;
                    }
                    streetVersion++;
                    return true;
                }));
            }
            foreach (KeyValuePair<int, GridPoint> slot in ribSlots.ToList())
            {
                GridPoint? root = SpineEdge(slot.Key, slot.Value);
                if (!root.HasValue) continue;
                KeyValuePair<int, GridPoint> s = slot;
                options.Add(new KeyValuePair<int, Func<bool>>(cost(root.Value) + branchSpacing / 2, () =>
                {
                    ribSlots.Remove(s);
                    ReleaseReserves();
                    return AddRib(s.Key, s.Value, ribStep);
                }));
            }
            foreach (KeyValuePair<int, Func<bool>> option in options.OrderBy(o => o.Key))
            {
                bool grown = option.Value();
                RefreshReserves();
                if (grown) return true;
            }
            return false;
        }

        // Public walking distance from the dining hall's door (or the spine
        // middle) over corridors and plazas.
        private Dictionary<GridPoint, int> WalkFromHub()
        {
            if (walk != null && walkVersion == streetVersion) return walk;
            walk = new Dictionary<GridPoint, int>();
            Queue<GridPoint> queue = new Queue<GridPoint>();
            GridPoint start = hubOrigin ?? SpineMiddle();
            walk[start] = 0;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                GridPoint c = queue.Dequeue();
                foreach (GridPoint step in ProceduralLayoutPlanner.Cardinal)
                {
                    GridPoint n = new GridPoint(c.X + step.X, c.Z + step.Z);
                    if (!grid.InBounds(n) || walk.ContainsKey(n)) continue;
                    byte o = occ[grid.Index(n)];
                    if (o != Street && o != Plaza) continue;
                    walk[n] = walk[c] + 1;
                    queue.Enqueue(n);
                }
            }
            walkVersion = streetVersion;
            return walk;
        }

        private int WalkCost(GridPoint street) => WalkFromHub().TryGetValue(street, out int d) ? d : 200;

        // A room placed after packing (the power room): same corridor growth,
        // with the reservations lifted again for the yards and enclosure.
        private bool PlaceLate(LayoutRoomDemand room)
        {
            bool done = PlaceInBlocks(room);
            ReleaseReserves();
            return done;
        }

        private bool PlaceInBlocks(LayoutRoomDemand room)
        {
            bool done = TryPlaceRoom(room);
            int limit = input.Budget.MaxRepairsPerAttempt + 2 * demand.Rooms.Count;
            while (!done && !stats.BudgetExhausted && growths < limit)
            {
                growths++;
                stats.Repairs++;
                if (!GrowCorridors()) break;
                ProceduralLayoutPlanner.Note(diagnostics, "repair", "corridor_grown", Site + " for " + room.Id);
                done = TryPlaceRoom(room);
            }
            if (!done && !stats.BudgetExhausted)
            {
                ReleaseReserves();
                done = PlaceByConnectedExtension(room, envelopeSearch);
                streetVersion++;
                RefreshReserves();
            }
            if (!done) Fail(stats.BudgetExhausted ? "budget_exhausted" : "room_unplaceable", room.Id + " growths=" + growths);
            return done;
        }
    }
}
