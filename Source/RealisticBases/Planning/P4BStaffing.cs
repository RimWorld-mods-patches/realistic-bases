using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    public sealed class P4BStaffingAudit
    {
        internal P4BStaffingAudit(int guns, int guarded, int closeSupported, int mortarCrew, int reserve,
            IEnumerable<GridPoint> sniperPosts, IEnumerable<GridPoint> linePosts)
        {
            Guns = guns; GuardedGuns = guarded; CloseSupportedGuns = closeSupported;
            MortarCrew = mortarCrew; MobileReserve = reserve;
            SniperPosts = new ReadOnlyCollection<GridPoint>(sniperPosts.ToList());
            LinePosts = new ReadOnlyCollection<GridPoint>(linePosts.ToList());
        }
        public int Guns { get; }
        public int GuardedGuns { get; }
        public int CloseSupportedGuns { get; }
        public int MortarCrew { get; }
        public int MobileReserve { get; }
        public IReadOnlyList<GridPoint> SniperPosts { get; }
        public IReadOnlyList<GridPoint> LinePosts { get; }
        public int ResponderDependentGuns => Guns - GuardedGuns;
    }

    // A post is one physical firing position. One staffed corner may support
    // several guns only when its shots actually cover every claimed gun.
    public static class P4BStaffing
    {
        public static P4BStaffingAudit Audit(ProceduralLayoutInput input, ProceduralLayout layout,
            P4BTurretPlan network, SettlementLoadout loadout = null)
        {
            if (input == null || layout == null || network == null) throw new ArgumentNullException();
            loadout = loadout ?? LayoutLoadoutModel.Build(input, layout);
            List<P4BTurretPlan> guns = network.All.ToList();
            if (layout.ApproachDefense == null || !loadout.Success)
                return new P4BStaffingAudit(guns.Count, 0, 0, 0, 0,
                    new GridPoint[0], new GridPoint[0]);
            HashSet<GridPoint> opaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
            opaque.UnionWith(guns.SelectMany(g => g.Footprint));
            List<ResidentLoadout> snipers = loadout.Residents.Where(r =>
                r.Weapon?.DefName == "Gun_SniperRifle").ToList();
            List<ResidentLoadout> others = loadout.Residents.Where(r => r.Weapon != null &&
                r.Weapon.DefName != "Gun_SniperRifle" &&
                input.Catalog.TryGet(r.Weapon.DefName, out CatalogDef def) &&
                def.Category == CatalogCategory.Weapon && def.MaximumRange > 0)
                .OrderBy(r => WeaponRange(input.Catalog, r)).ThenBy(r => r.Index).ToList();
            int mortarCrew = layout.Court != null && others.Count > 0 ? 1 : 0;
            if (mortarCrew > 0) others.RemoveAt(0);
            int reserve = loadout.Residents.Count >= 5 && others.Count >= 3 ? 1 : 0;
            if (reserve > 0) others.RemoveAt(0);

            List<IReadOnlyCollection<GridPoint>> guardOptions = guns.Select(g =>
                P4BTurretPlanner.SniperGuardOptions(layout.ApproachDefense, g, opaque)).ToList();
            List<GridPoint> guardPosts = GreedyCover(guardOptions, snipers.Count);
            int guarded = guardOptions.Count(options => options.Any(guardPosts.Contains));
            HashSet<int> closeCovered = new HashSet<int>(Enumerable.Range(0, guns.Count).Where(i =>
                guardPosts.Any(post => CanClose(layout.ApproachDefense, guns[i], post, 44, opaque))));
            HashSet<GridPoint> occupied = new HashSet<GridPoint>(guardPosts);
            List<GridPoint> linePosts = new List<GridPoint>();
            foreach (ResidentLoadout resident in others.OrderByDescending(r => WeaponRange(input.Catalog, r))
                .ThenBy(r => r.Index))
            {
                int range = WeaponRange(input.Catalog, resident);
                GridPoint? best = layout.ApproachDefense.AllPosts.Where(p => !occupied.Contains(p))
                    .Select(p => new { Post = p, Gain = Enumerable.Range(0, guns.Count)
                        .Count(i => !closeCovered.Contains(i) &&
                            CanClose(layout.ApproachDefense, guns[i], p, range, opaque)) })
                    .Where(p => p.Gain > 0).OrderByDescending(p => p.Gain).ThenBy(p => p.Post)
                    .Select(p => (GridPoint?)p.Post).FirstOrDefault();
                if (!best.HasValue) continue;
                occupied.Add(best.Value); linePosts.Add(best.Value);
                foreach (int i in Enumerable.Range(0, guns.Count))
                    if (CanClose(layout.ApproachDefense, guns[i], best.Value, range, opaque)) closeCovered.Add(i);
            }
            return new P4BStaffingAudit(guns.Count, guarded, closeCovered.Count, mortarCrew, reserve,
                guardPosts, linePosts);
        }

        // Also used by focused staffing fixtures to prove that disjoint
        // positions cannot borrow one simultaneous marksman.
        public static List<GridPoint> GreedyCover(IReadOnlyList<IReadOnlyCollection<GridPoint>> options, int staff)
        {
            List<GridPoint> selected = new List<GridPoint>();
            HashSet<int> covered = new HashSet<int>();
            if (options == null || staff <= 0) return selected;
            for (int slot = 0; slot < staff; slot++)
            {
                var best = options.SelectMany(o => o).Distinct().Where(p => !selected.Contains(p))
                    .Select(p => new { Post = p, Gain = Enumerable.Range(0, options.Count)
                        .Count(i => !covered.Contains(i) && options[i].Contains(p)) })
                    .Where(p => p.Gain > 0).OrderByDescending(p => p.Gain).ThenBy(p => p.Post)
                    .FirstOrDefault();
                if (best == null) break;
                selected.Add(best.Post);
                foreach (int i in Enumerable.Range(0, options.Count))
                    if (options[i].Contains(best.Post)) covered.Add(i);
            }
            return selected;
        }

        private static int WeaponRange(TechCatalog catalog, ResidentLoadout resident) =>
            resident.Weapon != null && catalog.TryGet(resident.Weapon.DefName, out CatalogDef def)
                ? (int)Math.Floor(def.MaximumRange) : 0;

        private static bool CanClose(P4BApproachDefensePlan defense, P4BTurretPlan gun, GridPoint post,
            int range, HashSet<GridPoint> opaque)
        {
            P4BAttackSide side = defense.Sides.FirstOrDefault(s => s.Id == gun.Side && s.Exposed &&
                s.Positions.Contains(post));
            return side != null && gun.Footprint.Min(c => Distance(c, post)) <= 14 &&
                side.Samples.Count(target => gun.Footprint.Min(c => Distance(c, target)) <= 18 &&
                    P4BFireLane.CanEngage(post, target, 0, range, opaque)) >= 2;
        }

        private static double Distance(GridPoint a, GridPoint b)
        {
            long x = a.X - b.X, z = a.Z - b.Z;
            return Math.Sqrt(x * x + z * z);
        }
    }
}
