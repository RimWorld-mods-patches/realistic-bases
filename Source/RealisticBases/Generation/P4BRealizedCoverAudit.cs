using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Integration;
using RealisticBases.Planning;
using Verse;

namespace RealisticBases.Generation
{
    // Measure vanilla cover after the structures exist. This is deliberately
    // diagnostic: a clear firing lane does not prove that a pawn will stand,
    // aim, or survive here during an encounter.
    internal static class P4BRealizedCoverAudit
    {
        internal static void Run(Map map, P4BApproachDefensePlan defense,
            IReadOnlyDictionary<P4BTurretPlan, Thing> turrets)
        {
            if (defense == null) return;
            foreach (P4BAttackSide side in defense.Sides.Where(s => s.Exposed))
                foreach (GridPoint post in side.Positions)
                    Measure(map, side, "post", post.ToString(), new LocalTargetInfo(Cell(post)), Cell(post));
            foreach (KeyValuePair<P4BTurretPlan, Thing> pair in turrets)
                foreach (P4BAttackSide side in defense.Sides.Where(s => s.Exposed))
                    Measure(map, side, "turret", pair.Value.def.defName + "@" + pair.Value.Position,
                        new LocalTargetInfo(pair.Value), pair.Value.Position);
        }

        private static void Measure(Map map, P4BAttackSide side, string kind, string targetId,
            LocalTargetInfo target, IntVec3 targetCell)
        {
            int visible = 0, noCover = 0;
            float total = 0f, minimum = 1f, maximum = 0f;
            foreach (GridPoint sample in side.Samples)
            {
                IntVec3 shooter = Cell(sample);
                if (!shooter.InBounds(map) || !GenSight.LineOfSight(shooter, targetCell, map)) continue;
                float block = CoverUtility.CalculateOverallBlockChance(target, shooter, map);
                visible++;
                if (block <= 0.001f) noCover++;
                total += block;
                minimum = Math.Min(minimum, block);
                maximum = Math.Max(maximum, block);
            }
            P2Log.Message("PROCEDURAL_DIRECTIONAL_COVER_AUDIT", "map=" + map.uniqueID +
                " side=" + side.Id + " kind=" + kind + " target=" + targetId +
                " reachableSamples=" + side.Samples.Count + " visibleSamples=" + visible +
                " noCover=" + noCover + " meanBlockPct=" + Percent(visible == 0 ? 0f : total / visible) +
                " minBlockPct=" + Percent(visible == 0 ? 0f : minimum) +
                " maxBlockPct=" + Percent(maximum) +
                " targetType=" + (kind == "post" ? "cell_proxy" : "actual_thing"));
        }

        private static int Percent(float chance) => (int)Math.Round(chance * 100f);
        private static IntVec3 Cell(GridPoint point) => new IntVec3(point.X, 0, point.Z);
    }
}
