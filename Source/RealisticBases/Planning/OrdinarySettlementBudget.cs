using System;

namespace RealisticBases.Planning
{
    // Relative spend capacity, not player wealth or a simulated world-map cash balance.
    // A normal allowance supports seven standard resident kits. Population is
    // derived from the resulting equipment budget before rooms/stocks are planned.
    public sealed class OrdinarySettlementBudget
    {
        internal OrdinarySettlementBudget(int wealthPercent, int equipmentValue, int importValue, int residentCost,
            int defenseAllowancePercent = 100, int baseImportValue = -1)
        {
            WealthPercent = wealthPercent;
            EquipmentValue = equipmentValue;
            ImportValue = importValue;
            StandardResidentCost = residentCost;
            DefenseAllowancePercent = defenseAllowancePercent;
            BaseImportValue = baseImportValue < 0 ? importValue : baseImportValue;
            Residents = OrdinarySettlementPopulation.AffordableResidents(equipmentValue, residentCost);
        }

        public int WealthPercent { get; }
        public int EquipmentValue { get; }
        public int ImportValue { get; }
        public int StandardResidentCost { get; }
        public int Residents { get; }
        public int DefenseAllowancePercent { get; }
        public int BaseImportValue { get; }
    }

    public static class OrdinarySettlementPopulation
    {
        public const int Minimum = 4;
        public const int Average = 7;
        public const int Maximum = 12;
        public const int MinimumWealthPercent = 60;
        public const int MaximumWealthPercent = 180;
        public const string Version = "ordinary-population.1";
        public const int P4EDefenseAllowancePercent = 125;
        public const int P4EDefenseReserveWatts = 960;

        // Provisional relative-budget distribution. With the affordability rule,
        // these weights give populations 4..12 and an exact expected mean of 7.
        private static readonly int[] WealthPercents = { 60, 75, 90, 100, 120, 135, 150, 165, 180 };
        private static readonly int[] Weights = { 11, 14, 18, 22, 14, 8, 6, 4, 3 };

        public static int SelectWealthPercent(ulong settlementSeed) =>
            WealthPercentForRoll((int)(LayoutSeed.Sub(settlementSeed, Version + "-wealth") % 100UL));

        public static int ResolveSavedWealth(int savedPercent, bool previouslyGenerated, ulong settlementSeed)
        {
            if (savedPercent == 0 || (savedPercent >= MinimumWealthPercent && savedPercent <= MaximumWealthPercent))
                return savedPercent;
            return previouslyGenerated ? 0 : SelectWealthPercent(settlementSeed);
        }

        public static int WealthPercentForRoll(int roll)
        {
            if (roll < 0 || roll >= 100) throw new ArgumentOutOfRangeException(nameof(roll));
            for (int i = 0; i < Weights.Length; i++)
            {
                if (roll < Weights[i]) return WealthPercents[i];
                roll -= Weights[i];
            }
            throw new InvalidOperationException("Population wealth weights must total 100");
        }

        public static int AffordableResidents(int equipmentValue, int standardResidentCost)
        {
            if (standardResidentCost <= 0 || equipmentValue < (long)Minimum * standardResidentCost)
                throw new ArgumentOutOfRangeException(nameof(equipmentValue), "Budget cannot support the minimum resident roster");
            return Math.Min(Maximum, equipmentValue / standardResidentCost);
        }

        public static OrdinarySettlementBudget Create(PhysicalSettlementProfile profile,
            SettlementTechStage stage, LayoutClimate climate, int wealthPercent, int defenseAllowancePercent = -1)
        {
            if (profile == null || stage == null || climate == null) throw new ArgumentNullException();
            if (wealthPercent < MinimumWealthPercent || wealthPercent > MaximumWealthPercent)
                throw new ArgumentOutOfRangeException(nameof(wealthPercent));
            int residentCost = Math.Max((int)Math.Ceiling(profile.Budget.EquipmentValue / (double)profile.Residents),
                LayoutRequirements.DefaultEquipmentValueLimit(1, stage, climate));
            int equipment = Scale(checked(residentCost * Average), wealthPercent);
            int baseImports = Scale(profile.Budget.ImportValueLimit, wealthPercent);
            bool industrialDefense = profile.Has(SettlementCapability.Electricity) &&
                stage.Level >= SettlementTechStageLevel.IndustrialMid;
            int allowance = industrialDefense ? (defenseAllowancePercent < 0 ? P4EDefenseAllowancePercent : defenseAllowancePercent) : 100;
            if (allowance < 100 || allowance > 150) throw new ArgumentOutOfRangeException(nameof(defenseAllowancePercent));
            return new OrdinarySettlementBudget(wealthPercent, equipment, Scale(baseImports, allowance), residentCost, allowance, baseImports);
        }

        private static int Scale(int value, int percent) =>
            checked((int)Math.Round(value * (double)percent / 100, MidpointRounding.AwayFromZero));
    }
}
