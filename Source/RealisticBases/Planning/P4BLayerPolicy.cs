using System;

namespace RealisticBases.Planning
{
    // The default is a preference distribution, not a promise that unsuitable
    // terrain or an exhausted construction budget can support reinforcement.
    public static class P4BLayerPolicy
    {
        public static int Select(ulong settlementSeed, SettlementTechStageLevel stage,
            int equipmentValueLimit, int importValueLimit, int residents)
        {
            if (residents <= 0 || equipmentValueLimit < 0 || importValueLimit < 0)
                throw new ArgumentOutOfRangeException(nameof(residents));
            int roll = (int)(LayoutSeed.Sub(settlementSeed, "p4b-wall-layers") % 100UL);
            int preferred = roll < 50 ? 1 : roll < 80 ? 2 : 3;
            int techCap = stage == SettlementTechStageLevel.Neolithic ? 1 :
                stage <= SettlementTechStageLevel.IndustrialEarly ? 2 : 3;
            // This is the settlement's declared spend capacity, not an invented
            // world-map cash balance. P5 later owns canonical inventory/wealth.
            long spendPerResident = ((long)equipmentValueLimit + importValueLimit) / residents;
            int budgetCap = spendPerResident < 1200 ? 1 : spendPerResident < 3000 ? 2 : 3;
            return Math.Min(preferred, Math.Min(techCap, budgetCap));
        }
    }
}
