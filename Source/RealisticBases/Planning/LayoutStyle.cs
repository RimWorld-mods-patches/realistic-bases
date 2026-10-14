using System;
using System.Collections.Generic;
using System.Linq;

namespace RealisticBases.Planning
{
    // Layout engine v2 style presets (docs/plans/2026-10-12-layout-engine-v2.md).
    // One generator; a style only changes how clusters join and how hallways grow.
    public enum LayoutStyleKind
    {
        Superstructure,
        Town,
        Mountain
    }

    public sealed class LayoutStyle
    {
        private LayoutStyle(LayoutStyleKind kind, bool detachedClusters, double sameClusterShare, double otherClusterShare)
        {
            Kind = kind;
            DetachedClusters = detachedClusters;
            SameClusterShare = sameClusterShare;
            OtherClusterShare = otherClusterShare;
        }

        public LayoutStyleKind Kind { get; }
        // Town: clusters are separate buildings with at least one open cell between them.
        public bool DetachedClusters { get; }
        // Score bonus per wall cell shared with a room of the same / another cluster.
        public double SameClusterShare { get; }
        public double OtherClusterShare { get; }

        public static readonly LayoutStyle Superstructure = new LayoutStyle(LayoutStyleKind.Superstructure, false, 2.0, 2.0);
        public static readonly LayoutStyle Town = new LayoutStyle(LayoutStyleKind.Town, true, 2.0, 0);
        public static readonly LayoutStyle Mountain = new LayoutStyle(LayoutStyleKind.Mountain, false, 1.5, 1.0);

        // Rock-protected sites carve chambers; powered settlements build one
        // compact block; unpowered (tribal/medieval) ones build separate houses.
        public static LayoutStyle Select(LayoutDoctrine doctrine, LayoutKit kit, LayoutRequirements requirements)
        {
            if (doctrine == LayoutDoctrine.RockProtected) return Mountain;
            if (requirements.CompactQuestCamp || !kit.Powered) return Town;
            return Superstructure;
        }
    }

    public enum LayoutClusterKind
    {
        Food,
        Production,
        Living,
        Care,
        Power
    }

    // Rooms a player builds together. Packing order inside a cluster puts the
    // room that most needs exterior walls (the freezer's coolers) at its end.
    public static class LayoutClusters
    {
        public static LayoutClusterKind Of(LayoutRoomKind kind)
        {
            switch (kind)
            {
                case LayoutRoomKind.Freezer:
                case LayoutRoomKind.Pantry:
                case LayoutRoomKind.Kitchen:
                case LayoutRoomKind.Dining: return LayoutClusterKind.Food;
                case LayoutRoomKind.Storage:
                case LayoutRoomKind.Workshop:
                case LayoutRoomKind.Laboratory: return LayoutClusterKind.Production;
                case LayoutRoomKind.Barracks:
                case LayoutRoomKind.Bedroom: return LayoutClusterKind.Living;
                case LayoutRoomKind.Power: return LayoutClusterKind.Power;
                default: return LayoutClusterKind.Care;
            }
        }

        private static int Rank(LayoutRoomKind kind)
        {
            switch (kind)
            {
                case LayoutRoomKind.Freezer:
                case LayoutRoomKind.Pantry: return 0;
                case LayoutRoomKind.Kitchen: return 1;
                case LayoutRoomKind.Dining: return 2;
                case LayoutRoomKind.Storage: return 0;
                case LayoutRoomKind.Workshop: return 1;
                case LayoutRoomKind.Laboratory: return 2;
                case LayoutRoomKind.Medical: return 0;
                case LayoutRoomKind.Jail: return 1;
                default: return 0;
            }
        }

        // Cluster order, then in-cluster order; the power room is placed after
        // the power yards by the shared back half and is excluded here.
        public static IReadOnlyList<LayoutRoomDemand> Order(IEnumerable<LayoutRoomDemand> rooms) =>
            rooms.Where(r => r.Kind != LayoutRoomKind.Power)
                .OrderBy(r => (int)Of(r.Kind)).ThenBy(r => Rank(r.Kind)).ThenBy(r => r.Id, StringComparer.Ordinal).ToList();

        // Typical room depth (interior cells away from the door wall), used to
        // space parallel hallway branches so blocks back onto each other.
        public static int TypicalDepth(IEnumerable<LayoutRoomDemand> rooms)
        {
            List<int> depths = rooms.Where(r => r.CandidateSizes.Count > 0)
                .Select(r => r.CandidateSizes.Min(s => s.Height)).OrderBy(d => d).ToList();
            return depths.Count == 0 ? LayoutDemandModel.MinInterior : depths[depths.Count / 2];
        }
    }
}
