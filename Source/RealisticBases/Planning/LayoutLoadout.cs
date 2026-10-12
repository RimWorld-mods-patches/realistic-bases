using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    // Resident gear and settlement stocks by tech stage (pure, engine independent;
    // plan step 6). Trade stock stays separate (P5).
    public enum LoadoutRole
    {
        RangedLine,
        Marksman,
        CloseDefense,
        Heavy,
        MeleeReserve,
        Support
    }

    // Mirrors Verse's QualityCategory order.
    public enum LoadoutQuality
    {
        Awful,
        Poor,
        Normal,
        Good,
        Excellent,
        Masterwork,
        Legendary
    }

    public sealed class LoadoutItem
    {
        internal LoadoutItem(string defName, string stuff, LoadoutQuality? quality, int count, int unitValue)
        {
            DefName = defName;
            Stuff = stuff;
            Quality = quality;
            Count = count;
            UnitValue = unitValue;
        }

        public string DefName { get; }
        // Stuff for stuffable defs, otherwise null.
        public string Stuff { get; }
        // Null for items without quality (medicine, components).
        public LoadoutQuality? Quality { get; }
        public int Count { get; }
        public int UnitValue { get; }
        public int Value => UnitValue * Count;
        public override string ToString() => DefName + (Stuff == null ? string.Empty : "(" + Stuff + ")") +
            (Quality.HasValue ? "[" + Quality.Value + "]" : string.Empty) + (Count == 1 ? string.Empty : "x" + Count);

        internal ulong AddTo(ulong hash)
        {
            hash = StableHash.Add(hash, DefName);
            hash = StableHash.Add(hash, Stuff ?? string.Empty);
            hash = StableHash.Add(hash, Quality.HasValue ? (int)Quality.Value : -1);
            hash = StableHash.Add(hash, Count);
            return StableHash.Add(hash, UnitValue);
        }
    }

    public sealed class ResidentLoadout
    {
        internal ResidentLoadout(int index, LoadoutRole role, bool elite, LoadoutItem weapon, IEnumerable<LoadoutItem> apparel, LoadoutItem utility)
        {
            Index = index;
            Role = role;
            Elite = elite;
            Weapon = weapon;
            Apparel = new ReadOnlyCollection<LoadoutItem>(apparel.ToList());
            Utility = utility;
        }

        public int Index { get; }
        public LoadoutRole Role { get; }
        public bool Elite { get; }
        public LoadoutItem Weapon { get; }
        // Worn apparel, armour first.
        public IReadOnlyList<LoadoutItem> Apparel { get; }
        // Belt item (pop packs, shield belt) or null.
        public LoadoutItem Utility { get; }
        public IEnumerable<LoadoutItem> Items => new[] { Weapon, Utility }.Where(i => i != null).Concat(Apparel);
        public override string ToString() => "#" + Index + " " + Role + (Elite ? "*" : string.Empty) + " " + string.Join(" ", Items);
    }

    public sealed class SettlementLoadout
    {
        internal SettlementLoadout(SettlementTechStage stage, IEnumerable<ResidentLoadout> residents, IEnumerable<LoadoutItem> armoury,
            IEnumerable<LoadoutItem> medicine, IEnumerable<LoadoutItem> components, int valueLimit, IEnumerable<string> degradations,
            IEnumerable<string> diagnostics, string rejection)
        {
            Stage = stage;
            Residents = new ReadOnlyCollection<ResidentLoadout>(residents.ToList());
            Armoury = new ReadOnlyCollection<LoadoutItem>(armoury.ToList());
            Medicine = new ReadOnlyCollection<LoadoutItem>(medicine.ToList());
            Components = new ReadOnlyCollection<LoadoutItem>(components.ToList());
            EquipmentValueLimit = valueLimit;
            Degradations = new ReadOnlyCollection<string>(degradations.ToList());
            Diagnostics = new ReadOnlyCollection<string>(diagnostics.ToList());
            Rejection = rejection;
            EquipmentValue = AllItems.Sum(i => i.Value);
            Digest = ComputeDigest();
        }

        public SettlementTechStage Stage { get; }
        public IReadOnlyList<ResidentLoadout> Residents { get; }
        // Spare weapons and apparel sets.
        public IReadOnlyList<LoadoutItem> Armoury { get; }
        public IReadOnlyList<LoadoutItem> Medicine { get; }
        public IReadOnlyList<LoadoutItem> Components { get; }
        public int EquipmentValue { get; }
        public int EquipmentValueLimit { get; }
        // quality_degraded, armoury_dropped, utility_dropped, in the order applied.
        public IReadOnlyList<string> Degradations { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        // equipment_budget_exceeded, or null.
        public string Rejection { get; }
        public bool Success => Rejection == null;
        public string Digest { get; }
        public IEnumerable<LoadoutItem> AllItems => Residents.SelectMany(r => r.Items).Concat(Armoury).Concat(Medicine).Concat(Components);

        private string ComputeDigest()
        {
            ulong hash = StableHash.Start;
            hash = StableHash.Add(hash, "p4a-loadout");
            hash = StableHash.Add(hash, Stage.Digest);
            foreach (ResidentLoadout resident in Residents)
            {
                hash = StableHash.Add(hash, resident.Index);
                hash = StableHash.Add(hash, (int)resident.Role);
                hash = StableHash.Add(hash, resident.Elite ? 1 : 0);
                foreach (LoadoutItem item in resident.Items) hash = item.AddTo(hash);
            }
            foreach (LoadoutItem item in Armoury.Concat(Medicine).Concat(Components)) hash = item.AddTo(hash);
            hash = StableHash.Add(hash, EquipmentValueLimit);
            foreach (string line in Degradations.Concat(Diagnostics)) hash = StableHash.Add(hash, line);
            return StableHash.Hex(StableHash.Add(hash, Rejection ?? string.Empty));
        }
    }

    public static class LayoutLoadoutModel
    {
        // PROVISIONAL role shares (percent of residents, rounded half away from zero).
        public const int MarksmanPercent = 15;
        public const int CloseDefensePercent = 20;
        public const int HeavyPercent = 10;
        public const int MeleeReservePercent = 15;
        public const int SupportPercent = 10;
        // PROVISIONAL: spare weapons and apparel sets for this share of residents.
        public const int ArmouryPercent = 25;
        // PROVISIONAL medicine stock: per resident plus a reserve.
        public const int MedicinePerResident = 2;
        public const int MedicineReserve = 5;
        // PROVISIONAL component stocks.
        public const int ComponentIndustrialSpare = 2;
        public const int ComponentSpacerStock = 2;
        // Vanilla StatWorker_MarketValue.ValuePerWork.
        public const double ValuePerWork = 0.0036;
        public const string EquipmentBudgetExceeded = "equipment_budget_exceeded";
        public const string Steel = "Steel";
        public const string Wood = "WoodLog";

        // PROVISIONAL equipment budget per resident by stage, calibrated from measured temperate
        // Normal-quality kits (gear + armoury + stocks; see docs/p4a-foundations.md).
        public static int DefaultEquipmentValuePerResident(SettlementTechStageLevel level)
        {
            switch (level)
            {
                case SettlementTechStageLevel.Neolithic: return 325;
                case SettlementTechStageLevel.Medieval: return 900;
                case SettlementTechStageLevel.IndustrialEarly: return 1000;
                case SettlementTechStageLevel.IndustrialMid: return 1600;
                case SettlementTechStageLevel.IndustrialLate: return 2400;
                default: return 5200;
            }
        }

        // PROVISIONAL: budget multiplier when the climate requires heating (parkas, dusters).
        public const double ColdClimateBudgetFactor = 1.2;

        // PROVISIONAL market value multipliers per quality.
        public static IReadOnlyList<double> QualityValueFactors { get; } = new ReadOnlyCollection<double>(new[] { 0.5, 0.75, 1.0, 1.25, 1.5, 2.5, 5.0 });

        // PROVISIONAL quality weights (percent, Awful..Legendary) per stage.
        public static IReadOnlyList<int> QualityWeights(SettlementTechStageLevel level)
        {
            switch (level)
            {
                case SettlementTechStageLevel.Neolithic: return Array.AsReadOnly(new[] { 10, 40, 45, 5, 0, 0, 0 });
                case SettlementTechStageLevel.Medieval: return Array.AsReadOnly(new[] { 0, 30, 55, 15, 0, 0, 0 });
                case SettlementTechStageLevel.IndustrialEarly: return Array.AsReadOnly(new[] { 0, 15, 60, 25, 0, 0, 0 });
                case SettlementTechStageLevel.IndustrialMid: return Array.AsReadOnly(new[] { 0, 10, 55, 35, 0, 0, 0 });
                case SettlementTechStageLevel.IndustrialLate: return Array.AsReadOnly(new[] { 0, 0, 55, 40, 5, 0, 0 });
                default: return Array.AsReadOnly(new[] { 0, 0, 20, 55, 25, 0, 0 });
            }
        }

        private static readonly SettlementTechStageLevel Medieval = SettlementTechStageLevel.Medieval;

        // Weapon preferences, best first; primitive options are capped at Medieval.
        public static IReadOnlyList<RoleOption> WeaponOptions(LoadoutRole role)
        {
            switch (role)
            {
                case LoadoutRole.Marksman:
                    return Options("Gun_SniperRifle", "Gun_ChargeRifle", "Gun_BoltActionRifle", Capped("Bow_Great"), Capped("Bow_Recurve"), Capped("Bow_Short"));
                case LoadoutRole.CloseDefense:
                    return Options("Gun_ChainShotgun", "Gun_HeavySMG", "Gun_PumpShotgun", "MeleeWeapon_LongSword", "MeleeWeapon_Spear", Capped("MeleeWeapon_Club"));
                case LoadoutRole.Heavy:
                    return Options("Gun_Minigun", "Gun_LMG");
                case LoadoutRole.MeleeReserve:
                    return Options("MeleeWeapon_LongSword", "MeleeWeapon_Mace", "MeleeWeapon_Gladius", "MeleeWeapon_Ikwa", "MeleeWeapon_Spear",
                        Capped("MeleeWeapon_Club"), "MeleeWeapon_Knife");
                case LoadoutRole.Support:
                    return Options("Gun_Autopistol", "Gun_Revolver", "MeleeWeapon_Knife");
                default:
                    return Options("Gun_ChargeRifle", "Gun_AssaultRifle", "Gun_BoltActionRifle", Capped("Bow_Great"), Capped("Bow_Recurve"),
                        Capped("Bow_Short"), Capped("Pila"));
            }
        }

        private static object Capped(string defName) => RoleOption.Def(defName, Medieval);

        private static IReadOnlyList<RoleOption> Options(params object[] options) =>
            new ReadOnlyCollection<RoleOption>(options.Select(o => o as RoleOption ?? RoleOption.Def((string)o)).ToList());

        public static bool IsMelee(string weaponDefName) => weaponDefName != null && weaponDefName.StartsWith("MeleeWeapon_", StringComparison.Ordinal);
        public static bool IsCombat(LoadoutRole role) => role != LoadoutRole.Support;
        public static bool IsFrontLine(LoadoutRole role) => role == LoadoutRole.RangedLine || role == LoadoutRole.CloseDefense || role == LoadoutRole.MeleeReserve;

        public static SettlementLoadout Build(ProceduralLayoutInput input, ProceduralLayout layout)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.QuestContract != null) return input.QuestContract.DescribeLoadout(input.Equipment.Kit.Stage);
            return Build(input.Requirements, input.Equipment.Kit, input.Catalog, LayoutSeed.Sub(input.Seed, "loadout"),
                layout == null || layout.Power == null ? 0 : layout.Power.Generators, layout != null && layout.Court != null);
        }

        public static SettlementLoadout Build(LayoutRequirements requirements, LayoutKit kit, TechCatalog catalog, ulong seed, int generators, bool mortarCrew)
        {
            if (requirements == null) throw new ArgumentNullException(nameof(requirements));
            if (kit == null) throw new ArgumentNullException(nameof(kit));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            SettlementTechStage stage = kit.Stage;
            List<string> diagnostics = new List<string>();
            List<LoadoutRole> roles = Roles(requirements.Residents, stage.Level, mortarCrew);
            int elite = roles.IndexOf(LoadoutRole.Heavy) >= 0 ? roles.IndexOf(LoadoutRole.Heavy) : roles.IndexOf(LoadoutRole.RangedLine);
            Builder builder = new Builder(catalog, stage, requirements.Climate, seed, diagnostics);
            List<ResidentPlan> plans = roles.Select((role, i) => builder.Resident(i, role, i == elite)).ToList();

            // Armoury: spares for 25% of residents, alternating line rifles and reserve melee, plus apparel sets.
            int spares = (requirements.Residents * ArmouryPercent + 99) / 100;
            List<PlannedItem> armoury = new List<PlannedItem>();
            for (int i = 0; i < spares; i++)
            {
                PlannedItem weapon = builder.Weapon(1000 + i, i % 2 == 0 ? LoadoutRole.RangedLine : LoadoutRole.MeleeReserve, false);
                if (weapon != null) armoury.Add(weapon);
                armoury.AddRange(builder.Apparel(2000 + i, LoadoutRole.RangedLine, false));
            }

            List<LoadoutItem> medicine = new List<LoadoutItem>();
            bool industrialMedicine = stage.Level >= SettlementTechStageLevel.IndustrialMid && catalog.IsLegal("MedicineIndustrial", stage);
            string medicineDef = industrialMedicine ? "MedicineIndustrial" : "MedicineHerbal";
            if (catalog.IsLegal(medicineDef, stage))
                medicine.Add(new LoadoutItem(medicineDef, null, null, MedicinePerResident * requirements.Residents + MedicineReserve, UnitValue(catalog, medicineDef, null, null)));
            else diagnostics.Add("loadout_medicine_unresolved");

            List<LoadoutItem> components = new List<LoadoutItem>();
            if (stage.Level >= SettlementTechStageLevel.IndustrialEarly && catalog.IsLegal("ComponentIndustrial", stage))
                components.Add(new LoadoutItem("ComponentIndustrial", null, null, generators + ComponentIndustrialSpare, UnitValue(catalog, "ComponentIndustrial", null, null)));
            if (stage.Level >= SettlementTechStageLevel.Spacer && catalog.IsLegal("ComponentSpacer", stage))
                components.Add(new LoadoutItem("ComponentSpacer", null, null, ComponentSpacerStock, UnitValue(catalog, "ComponentSpacer", null, null)));

            // Budget: degrade quality one step, then drop the armoury, then utility items, then reject.
            List<string> degradations = new List<string>();
            int limit = requirements.EquipmentValueLimit;
            int qualityShift = 0;
            bool keepArmoury = true, keepUtility = true;
            Func<SettlementLoadout> assemble = () => new SettlementLoadout(stage,
                plans.Select(p => p.Realize(catalog, qualityShift, keepUtility)),
                keepArmoury ? Merge(armoury.Select(a => a.Realize(catalog, qualityShift))) : Enumerable.Empty<LoadoutItem>(),
                medicine, components, limit, degradations, diagnostics, null);
            SettlementLoadout result = assemble();
            if (result.EquipmentValue > limit) { qualityShift = -1; degradations.Add("quality_degraded"); result = assemble(); }
            if (result.EquipmentValue > limit) { keepArmoury = false; degradations.Add("armoury_dropped"); result = assemble(); }
            if (result.EquipmentValue > limit && requirements.LogisticsUtilityValueLimit > 0)
            {
                int smokeValue = result.Residents.Where(r => r.Utility?.DefName == "Apparel_SmokepopBelt").Sum(r => r.Utility.Value);
                int topUp = Math.Min(requirements.LogisticsUtilityValueLimit, smokeValue);
                if (topUp > 0 && result.EquipmentValue <= limit + topUp)
                {
                    limit += topUp;
                    diagnostics.Add("logistics_utility_topup=" + topUp + " rosterUnchanged=True");
                    result = assemble();
                }
            }
            if (result.EquipmentValue > limit) { keepUtility = false; degradations.Add("utility_dropped"); result = assemble(); }
            if (result.EquipmentValue > limit)
                result = new SettlementLoadout(stage, result.Residents, result.Armoury, result.Medicine, result.Components, limit, degradations,
                    diagnostics, EquipmentBudgetExceeded);
            return result;
        }

        // Deterministic role assignment by resident index: specialists first, the line fills the rest.
        public static List<LoadoutRole> Roles(int residents, SettlementTechStageLevel level, bool mortarCrew)
        {
            Func<int, int> share = percent => (int)Math.Round(residents * percent / 100.0, MidpointRounding.AwayFromZero);
            Dictionary<LoadoutRole, int> counts = new Dictionary<LoadoutRole, int>
            {
                { LoadoutRole.Marksman, share(MarksmanPercent) },
                { LoadoutRole.CloseDefense, share(CloseDefensePercent) },
                { LoadoutRole.Heavy, level >= SettlementTechStageLevel.IndustrialMid ? share(HeavyPercent) : 0 },
                { LoadoutRole.MeleeReserve, share(MeleeReservePercent) },
                { LoadoutRole.Support, Math.Max(share(SupportPercent), mortarCrew ? 1 : 0) }
            };
            LoadoutRole[] trimOrder = { LoadoutRole.CloseDefense, LoadoutRole.MeleeReserve, LoadoutRole.Marksman, LoadoutRole.Heavy, LoadoutRole.Support };
            while (counts.Values.Sum() > residents - 1)
            {
                LoadoutRole largest = trimOrder.Where(r => counts[r] > 0).OrderByDescending(r => counts[r]).First();
                counts[largest]--;
            }
            List<LoadoutRole> result = new List<LoadoutRole>();
            int line = residents - counts.Values.Sum();
            result.AddRange(Enumerable.Repeat(LoadoutRole.RangedLine, line));
            foreach (LoadoutRole role in new[] { LoadoutRole.Marksman, LoadoutRole.CloseDefense, LoadoutRole.Heavy, LoadoutRole.MeleeReserve, LoadoutRole.Support })
                result.AddRange(Enumerable.Repeat(role, counts[role]));
            return result;
        }

        // Vanilla StatWorker_MarketValue.CalculatedBaseMarketValue (statBases MarketValue wins
        // when present) times the provisional quality factor.
        public static int UnitValue(TechCatalog catalog, string defName, string stuff, LoadoutQuality? quality)
        {
            CatalogDef def;
            if (!catalog.TryGet(defName, out def)) return 0;
            double value = def.MarketValue;
            if (value <= 0)
            {
                foreach (KeyValuePair<string, int> cost in def.CostList)
                {
                    CatalogDef part;
                    if (catalog.TryGet(cost.Key, out part)) value += cost.Value * part.MarketValue;
                }
                CatalogDef stuffDef;
                if (def.CostStuffCount > 0) value += stuff != null && catalog.TryGet(stuff, out stuffDef) ? def.CostStuffCount * stuffDef.MarketValue : def.CostStuffCount * 2.0;
                if (def.WorkToMake > 2) value += def.WorkToMake * ValuePerWork;
            }
            if (quality.HasValue) value *= QualityValueFactors[(int)quality.Value];
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        private static IEnumerable<LoadoutItem> Merge(IEnumerable<LoadoutItem> items) =>
            items.GroupBy(i => i.DefName + "|" + i.Stuff + "|" + i.Quality)
                .Select(g => new LoadoutItem(g.First().DefName, g.First().Stuff, g.First().Quality, g.Sum(i => i.Count), g.First().UnitValue))
                .OrderBy(i => i.DefName, StringComparer.Ordinal).ThenBy(i => i.Stuff, StringComparer.Ordinal).ThenBy(i => i.Quality);

        private sealed class PlannedItem
        {
            public string DefName;
            public string Stuff;
            public LoadoutQuality Quality;

            public LoadoutItem Realize(TechCatalog catalog, int shift)
            {
                LoadoutQuality? quality = catalog.TryGet(DefName, out CatalogDef def) && def.SupportsQuality
                    ? (LoadoutQuality?)Math.Max(0, Math.Min((int)LoadoutQuality.Legendary, (int)Quality + shift)) : null;
                return new LoadoutItem(DefName, Stuff, quality, 1, UnitValue(catalog, DefName, Stuff, quality));
            }
        }

        private sealed class ResidentPlan
        {
            public int Index;
            public LoadoutRole Role;
            public bool Elite;
            public PlannedItem Weapon;
            public List<PlannedItem> Apparel;
            public PlannedItem Utility;

            public ResidentLoadout Realize(TechCatalog catalog, int shift, bool keepUtility) =>
                new ResidentLoadout(Index, Role, Elite, Weapon == null ? null : Weapon.Realize(catalog, shift),
                    Apparel.Select(a => a.Realize(catalog, shift)), keepUtility && Utility != null ? Utility.Realize(catalog, shift) : null);
        }

        private sealed class Builder
        {
            private readonly TechCatalog catalog;
            private readonly SettlementTechStage stage;
            private readonly LayoutClimate climate;
            private readonly ulong seed;
            private readonly List<string> diagnostics;

            public Builder(TechCatalog catalog, SettlementTechStage stage, LayoutClimate climate, ulong seed, List<string> diagnostics)
            {
                this.catalog = catalog;
                this.stage = stage;
                this.climate = climate;
                this.seed = seed;
                this.diagnostics = diagnostics;
            }

            private bool Legal(string defName) => catalog.IsLegal(defName, stage);
            private bool Cold => climate.HeatingRequired && !climate.CoolingRequired;
            private SettlementTechStageLevel Level => stage.Level;

            public ResidentPlan Resident(int index, LoadoutRole role, bool elite)
            {
                PlannedItem weapon = Weapon(index, role, elite);
                if (weapon == null) diagnostics.Add("loadout_weapon_unresolved:" + index + ":" + role);
                List<PlannedItem> apparel = Apparel(index, role, elite);
                PlannedItem utility = null;
                if (Level >= SettlementTechStageLevel.Spacer && weapon != null && IsMelee(weapon.DefName) &&
                    (role == LoadoutRole.MeleeReserve || role == LoadoutRole.CloseDefense) && Legal("Apparel_ShieldBelt"))
                    utility = Item("Apparel_ShieldBelt", index, 90, elite);
                else if (role == LoadoutRole.Support && Level >= SettlementTechStageLevel.IndustrialMid)
                {
                    string belt = new[] { "Apparel_SmokepopBelt", "Apparel_FirefoampopPack" }.FirstOrDefault(Legal);
                    if (belt != null) utility = Item(belt, index, 90, elite);
                }
                return new ResidentPlan { Index = index, Role = role, Elite = elite, Weapon = weapon, Apparel = apparel, Utility = utility };
            }

            // The role's first legal, tier-acceptable weapon; then the line table, then the melee table.
            public PlannedItem Weapon(int index, LoadoutRole role, bool elite)
            {
                foreach (LoadoutRole table in new[] { role, LoadoutRole.RangedLine, LoadoutRole.MeleeReserve }.Distinct())
                    foreach (RoleOption option in WeaponOptions(table))
                        if (option.AcceptableAt(Level) && Legal(option.DefName)) return Item(option.DefName, index, 0, elite);
                return null;
            }

            // Wish list best first (armour, then shells, then base clothes); an item that shares a
            // layer and a body part with an earlier one is skipped.
            public List<PlannedItem> Apparel(int index, LoadoutRole role, bool elite)
            {
                bool combat = IsCombat(role);
                List<string> wishes = new List<string>();
                if (Level == SettlementTechStageLevel.Spacer && combat)
                    wishes.AddRange(role == LoadoutRole.Heavy || elite
                        ? new[] { "Apparel_PowerArmor", "Apparel_PowerArmorHelmet" } : new[] { "Apparel_ArmorRecon", "Apparel_ArmorHelmetRecon" });
                if (Level >= SettlementTechStageLevel.IndustrialLate && combat)
                {
                    if (!Cold) wishes.Add("Apparel_FlakJacket");
                    wishes.Add("Apparel_AdvancedHelmet");
                }
                if (Level >= SettlementTechStageLevel.IndustrialMid && combat) wishes.AddRange(new[] { "Apparel_FlakVest", "Apparel_FlakPants" });
                if (Level >= SettlementTechStageLevel.IndustrialEarly && combat) wishes.Add("Apparel_SimpleHelmet");
                if (Level == SettlementTechStageLevel.Medieval && IsFrontLine(role)) wishes.Add("Apparel_PlateArmor");
                if (Cold) wishes.Add(Level == SettlementTechStageLevel.Medieval ? "Apparel_Duster" : "Apparel_Parka");
                if (Level == SettlementTechStageLevel.Neolithic)
                {
                    wishes.Add("Apparel_TribalA");
                    if (combat) wishes.Add("Apparel_WarMask");
                }
                else wishes.AddRange(new[] { elite ? "Apparel_CollarShirt" : "Apparel_BasicShirt", "Apparel_Pants" });

                List<PlannedItem> result = new List<PlannedItem>();
                List<CatalogDef> worn = new List<CatalogDef>();
                int slot = 1;
                foreach (string wish in wishes)
                {
                    CatalogDef def;
                    if (!catalog.TryGet(wish, out def) || !catalog.IsLegal(def, stage)) continue;
                    if (worn.Any(w => Conflicts(w, def))) continue;
                    worn.Add(def);
                    result.Add(Item(wish, index, slot++, elite));
                }
                return result;
            }

            private PlannedItem Item(string defName, int index, int slot, bool elite)
            {
                CatalogDef def;
                catalog.TryGet(defName, out def);
                int roll = LayoutSeed.Roll(LayoutSeed.Key(LayoutSeed.Sub(seed, "quality"), index, slot), "q", 100);
                IReadOnlyList<int> weights = QualityWeights(Level);
                int quality = 0;
                for (int q = 0; q < weights.Count; q++)
                {
                    if (roll < weights[q]) { quality = q; break; }
                    roll -= weights[q];
                }
                if (elite) quality = Math.Min(quality + 1, (int)LoadoutQuality.Legendary);
                return new PlannedItem { DefName = defName, Stuff = StuffFor(def), Quality = (LoadoutQuality)quality };
            }

            // Metal from Medieval on (steel, bought in), wood before; fabric by stage: leather for
            // tribes, cloth for Medieval and IndustrialEarly, synthread from IndustrialMid.
            private string StuffFor(CatalogDef def)
            {
                if (def == null || def.StuffCategories.Count == 0) return null;
                bool metal = def.StuffCategories.Contains("Metallic") && Legal(Steel);
                bool wood = def.StuffCategories.Contains("Woody") && Legal(Wood);
                if (metal && (Level >= SettlementTechStageLevel.Medieval || !wood)) return Steel;
                if (wood) return Wood;
                if (def.StuffCategories.Contains("Fabric") || def.StuffCategories.Contains("Leathery"))
                {
                    if (Level >= SettlementTechStageLevel.IndustrialMid && def.StuffCategories.Contains("Fabric") && Legal("Synthread")) return "Synthread";
                    if (Level == SettlementTechStageLevel.Neolithic && def.StuffCategories.Contains("Leathery") && Legal("Leather_Plain")) return "Leather_Plain";
                    if (def.StuffCategories.Contains("Fabric") && Legal("Cloth")) return "Cloth";
                    if (def.StuffCategories.Contains("Leathery") && Legal("Leather_Plain")) return "Leather_Plain";
                }
                return null;
            }
        }

        // Same layer and overlapping body parts. Vanilla compares actual parts; the only nested
        // Core groups relevant here are FullHead (covers UpperHead and Eyes) and UpperHead (covers Eyes).
        public static bool Conflicts(CatalogDef a, CatalogDef b) =>
            a.ApparelLayers.Intersect(b.ApparelLayers).Any() && Parts(a).Intersect(Parts(b)).Any();

        private static IEnumerable<string> Parts(CatalogDef def)
        {
            foreach (string group in def.ApparelBodyParts)
            {
                yield return group;
                if (group == "FullHead") { yield return "UpperHead"; yield return "Eyes"; }
                if (group == "UpperHead") yield return "Eyes";
            }
        }
    }
}
