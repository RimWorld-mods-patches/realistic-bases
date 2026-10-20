using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

namespace RealisticBases.Generation
{
    // Pure checks shared by the P4 spawner and LayoutTests. Turret layout
    // legality is owned by ProceduralLayoutValidator/P4BTurretPlanner;
    // nearby guard posts are a best-effort encounter priority only.
    internal sealed class P4BSpawnGuardLookup
    {
        public P4BSpawnGuardLookup(P4BTurretPlan turret, int threats, int answered, GridPoint? guardPost)
        {
            Turret = turret;
            Threats = threats;
            Answered = answered;
            GuardPost = guardPost;
        }

        public P4BTurretPlan Turret { get; }
        public int Threats { get; }
        public int Answered { get; }
        public GridPoint? GuardPost { get; }
    }

    internal sealed class P4BProceduralSpawnPreflightResult
    {
        public P4BProceduralSpawnPreflightResult(string failure, SettlementLoadout loadout,
            IReadOnlyList<P4BSpawnGuardLookup> guardLookups)
        {
            Failure = failure;
            Loadout = loadout;
            GuardLookups = guardLookups;
        }

        public string Failure { get; }
        public bool Accepted => Failure == null;
        public SettlementLoadout Loadout { get; }
        public IReadOnlyList<P4BSpawnGuardLookup> GuardLookups { get; }
    }

    internal static class P4BProceduralSpawnPreflight
    {
        public static Dictionary<GridPoint, HashSet<string>> BuildSniperGuardSectors(
            IEnumerable<P4BSpawnGuardLookup> lookups)
        {
            Dictionary<GridPoint, HashSet<string>> sectors = new Dictionary<GridPoint, HashSet<string>>();
            foreach (P4BSpawnGuardLookup lookup in lookups ?? Enumerable.Empty<P4BSpawnGuardLookup>())
            {
                if (!lookup.GuardPost.HasValue) continue;
                GridPoint guard = lookup.GuardPost.Value;
                if (!sectors.TryGetValue(guard, out HashSet<string> sides))
                    sectors.Add(guard, sides = new HashSet<string>(System.StringComparer.Ordinal));
                sides.Add(lookup.Turret.Side);
            }
            return sectors;
        }

        public static P4BProceduralSpawnPreflightResult Evaluate(ProceduralLayoutInput input,
            ProceduralLayout layout, bool includeGuardLookups = true)
        {
            if (layout == null)
                return new P4BProceduralSpawnPreflightResult("missing_procedural_layout", null,
                    new P4BSpawnGuardLookup[0]);

            IReadOnlyList<string> errors = ProceduralLayoutValidator.Validate(input, layout);
            if (errors.Count != 0)
                return new P4BProceduralSpawnPreflightResult("invalid_layout:" + string.Join(",", errors), null,
                    new P4BSpawnGuardLookup[0]);

            SettlementLoadout loadout = LayoutLoadoutModel.Build(input, layout);
            errors = ProceduralLayoutValidator.ValidateLoadout(input, layout, loadout);
            if (!loadout.Success || errors.Count != 0)
                return new P4BProceduralSpawnPreflightResult("invalid_loadout:" + loadout.Rejection + ":" +
                    string.Join(",", errors), loadout, new P4BSpawnGuardLookup[0]);

            List<P4BTurretPlan> turrets = (layout.Turret?.All ?? Enumerable.Empty<P4BTurretPlan>()).ToList();
            if (!includeGuardLookups || layout.ApproachDefense == null || turrets.Count == 0)
                return new P4BProceduralSpawnPreflightResult(null, loadout, new P4BSpawnGuardLookup[0]);

            HashSet<GridPoint> opaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
            List<P4BSpawnGuardLookup> lookups = new List<P4BSpawnGuardLookup>(turrets.Count);
            foreach (P4BTurretPlan turret in turrets)
            {
                P4BTurretPlanner.SniperGuardCoverage(layout.ApproachDefense, turret, opaque,
                    out int threats, out int answered, out GridPoint? guard);
                lookups.Add(new P4BSpawnGuardLookup(turret, threats, answered, guard));
            }
            return new P4BProceduralSpawnPreflightResult(null, loadout, lookups);
        }
    }
}
