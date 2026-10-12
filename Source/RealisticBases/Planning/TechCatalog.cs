using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    // Immutable snapshot of the defs the settlement equipment model may use
    // (pure, engine independent). The runtime adapter builds it from
    // DefDatabase; tests use a synthetic Core fixture. The pure planner never
    // reads defs or files itself.
    public enum CatalogCategory
    {
        Building,
        Weapon,
        Apparel,
        Item
    }

    public enum CatalogPassability
    {
        Standable,
        PassThroughOnly,
        Impassable
    }

    public sealed class CatalogDef
    {
        public CatalogDef(string defName, CatalogCategory category, TechTier techLevel = TechTier.Undefined,
            IEnumerable<string> research = null, int sizeX = 1, int sizeZ = 1, IEnumerable<string> stuffCategories = null,
            int costStuffCount = 0, IEnumerable<KeyValuePair<string, int>> costList = null, IEnumerable<string> stuffProvides = null,
            IEnumerable<string> weaponTags = null, IEnumerable<string> apparelTags = null, IEnumerable<string> apparelLayers = null,
            IEnumerable<string> apparelBodyParts = null, int basePowerConsumption = 0, IEnumerable<string> fuel = null,
            double fuelPerDay = 0, bool hasInteractionCell = false, GridPoint interactionOffset = default(GridPoint),
            CatalogPassability passability = CatalogPassability.Standable, double marketValue = 0, string thingClass = null,
            string designationCategory = null, bool isTrap = false, IEnumerable<string> producedAt = null, bool raw = false, int stackLimit = 1, double workToMake = 0,
            string turretGunDefName = null, double minimumRange = 0, double maximumRange = 0,
            double fuelCapacity = 0, double fuelMultiplier = 0, double initialFuelPercent = 0,
            double fuelPerShot = 0, bool fuelFactorByDifficulty = false, double explosionRadius = 0, double combatPower = 0,
            IEnumerable<string> linkableFacilities = null, double facilityMaxDistance = 0, bool isSittable = false,
            bool supportsSeatedWork = false, bool? supportsQuality = null, int maxItemsInCell = 1)
        {
            if (maxItemsInCell < 1) throw new ArgumentOutOfRangeException(nameof(maxItemsInCell));
            MaxItemsInCell = maxItemsInCell;
            if (string.IsNullOrEmpty(defName)) throw new ArgumentNullException(nameof(defName));
            if (sizeX < 1 || sizeZ < 1) throw new ArgumentOutOfRangeException(nameof(sizeX));
            if (stackLimit < 1) throw new ArgumentOutOfRangeException(nameof(stackLimit));
            if (workToMake < 0) throw new ArgumentOutOfRangeException(nameof(workToMake));
            if (costStuffCount < 0 || fuelPerDay < 0 || marketValue < 0) throw new ArgumentOutOfRangeException(nameof(costStuffCount));
            if (minimumRange < 0 || maximumRange < minimumRange || fuelCapacity < 0 || fuelMultiplier < 0 ||
                initialFuelPercent < 0 || initialFuelPercent > 1 || fuelPerShot < 0 || explosionRadius < 0 || combatPower < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumRange));
            DefName = defName;
            Category = category;
            TechLevel = techLevel;
            ResearchPrerequisites = List(research);
            SizeX = sizeX;
            SizeZ = sizeZ;
            StuffCategories = List(stuffCategories);
            CostStuffCount = costStuffCount;
            Dictionary<string, int> costs = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> cost in costList ?? Enumerable.Empty<KeyValuePair<string, int>>())
            {
                if (cost.Value <= 0) throw new ArgumentOutOfRangeException(nameof(costList));
                int existing;
                costs[cost.Key] = costs.TryGetValue(cost.Key, out existing) ? existing + cost.Value : cost.Value;
            }
            CostList = new ReadOnlyDictionary<string, int>(costs);
            StuffProvides = List(stuffProvides);
            WeaponTags = List(weaponTags);
            ApparelTags = List(apparelTags);
            ApparelLayers = List(apparelLayers);
            ApparelBodyParts = List(apparelBodyParts);
            BasePowerConsumption = basePowerConsumption;
            Fuel = List(fuel);
            FuelPerDay = fuelPerDay;
            HasInteractionCell = hasInteractionCell;
            InteractionOffset = interactionOffset;
            Passability = passability;
            MarketValue = marketValue;
            ThingClass = thingClass ?? string.Empty;
            DesignationCategory = designationCategory ?? string.Empty;
            IsTrap = isTrap;
            ProducedAt = List(producedAt);
            Raw = raw;
            StackLimit = stackLimit;
            WorkToMake = workToMake;
            TurretGunDefName = turretGunDefName ?? string.Empty;
            MinimumRange = minimumRange;
            MaximumRange = maximumRange;
            FuelCapacity = fuelCapacity;
            FuelMultiplier = fuelMultiplier;
            InitialFuelPercent = initialFuelPercent;
            FuelPerShot = fuelPerShot;
            FuelFactorByDifficulty = fuelFactorByDifficulty;
            ExplosionRadius = explosionRadius;
            CombatPower = combatPower;
            LinkableFacilities = List(linkableFacilities);
            FacilityMaxDistance = facilityMaxDistance;
            IsSittable = isSittable;

            SupportsQuality = supportsQuality ?? (category == CatalogCategory.Weapon || category == CatalogCategory.Apparel);
            SupportsSeatedWork = supportsSeatedWork || ThingClass == "Building_WorkTable" ||
                ThingClass == "Building_WorkTable_HeatPush" || ThingClass == "Building_ResearchBench";
        }

        public string DefName { get; }
        public CatalogCategory Category { get; }
        public TechTier TechLevel { get; }
        // Building research plus recipe research (recipeMaker, or the research every
        // producing recipe needs), with ParentName inheritance already applied.
        public IReadOnlyList<string> ResearchPrerequisites { get; }
        public int SizeX { get; }
        public int SizeZ { get; }
        public IReadOnlyList<string> StuffCategories { get; }
        public int CostStuffCount { get; }
        public IReadOnlyDictionary<string, int> CostList { get; }
        // Stuff categories this item provides when used as stuff (stuffProps).
        public IReadOnlyList<string> StuffProvides { get; }
        public IReadOnlyList<string> WeaponTags { get; }
        public IReadOnlyList<string> ApparelTags { get; }
        public IReadOnlyList<string> ApparelLayers { get; }
        public IReadOnlyList<string> ApparelBodyParts { get; }
        // Vanilla sign convention: positive consumes, negative generates (watts).
        public int BasePowerConsumption { get; }
        public IReadOnlyList<string> Fuel { get; }
        // Refuelable consumption per day, in fuel units.
        public double FuelPerDay { get; }
        public bool HasInteractionCell { get; }
        // Vanilla interactionCellOffset (x, z) for the default (north) rotation.
        public GridPoint InteractionOffset { get; }
        public CatalogPassability Passability { get; }
        public double MarketValue { get; }
        public string ThingClass { get; }
        public string DesignationCategory { get; }
        public bool IsTrap { get; }
        // Work benches that can make this def. Only checked for items (materials
        // and stock) that are not raw.
        public IReadOnlyList<string> ProducedAt { get; }
        // Mined, harvested, butchered or salvaged: legal without a local producer.
        public bool Raw { get; }
        // Units per stored stack (vanilla stackLimit, default 1).
        public int StackLimit { get; }
        // max(WorkToMake, WorkToBuild) from statBases; feeds the vanilla market value formula.
        public double WorkToMake { get; }
        // Resolved autonomous turret facts; zero/empty for other defs.
        public string TurretGunDefName { get; }
        public double MinimumRange { get; }
        public double MaximumRange { get; }
        public double FuelCapacity { get; }
        public double FuelMultiplier { get; }
        public double InitialFuelPercent { get; }
        public double FuelPerShot { get; }
        public bool FuelFactorByDifficulty { get; }
        public double ExplosionRadius { get; }
        public double CombatPower { get; }
        public IReadOnlyList<string> LinkableFacilities { get; }
        public double FacilityMaxDistance { get; }
        public bool IsSittable { get; }
        // Storage buildings: stacks one cell holds (vanilla shelves hold 3).
        public int MaxItemsInCell { get; }
        public bool SupportsSeatedWork { get; }
        public bool SupportsQuality { get; }
        public bool AutonomousTurret => TurretGunDefName.Length > 0 && MaximumRange > 0 && BasePowerConsumption > 0;
        public bool GeneratesPower => BasePowerConsumption < 0;
        public bool Blocking => Passability == CatalogPassability.Impassable;

        public override string ToString() => DefName;

        internal ulong AddTo(ulong hash)
        {
            hash = StableHash.Add(hash, DefName);
            if (MaxItemsInCell > 1) hash = StableHash.Add(hash, "maxItemsInCell:" + MaxItemsInCell);
            hash = StableHash.Add(hash, (int)Category);
            hash = StableHash.Add(hash, (int)TechLevel);
            hash = AddList(hash, ResearchPrerequisites);
            hash = StableHash.Add(hash, SizeX);
            hash = StableHash.Add(hash, SizeZ);
            hash = AddList(hash, StuffCategories);
            hash = StableHash.Add(hash, CostStuffCount);
            hash = StableHash.Add(hash, CostList.Count);
            foreach (KeyValuePair<string, int> cost in CostList.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                hash = StableHash.Add(hash, cost.Key);
                hash = StableHash.Add(hash, cost.Value);
            }
            hash = AddList(hash, StuffProvides);
            hash = AddList(hash, WeaponTags);
            hash = AddList(hash, ApparelTags);
            hash = AddList(hash, ApparelLayers);
            hash = AddList(hash, ApparelBodyParts);
            hash = StableHash.Add(hash, BasePowerConsumption);
            hash = AddList(hash, Fuel);
            hash = AddScaled(hash, FuelPerDay);
            hash = StableHash.Add(hash, HasInteractionCell ? 1 : 0);
            hash = StableHash.Add(hash, InteractionOffset.X);
            hash = StableHash.Add(hash, InteractionOffset.Z);
            hash = StableHash.Add(hash, (int)Passability);
            hash = AddScaled(hash, MarketValue);
            hash = StableHash.Add(hash, ThingClass);
            hash = StableHash.Add(hash, DesignationCategory);
            hash = StableHash.Add(hash, IsTrap ? 1 : 0);
            hash = AddList(hash, ProducedAt);
            hash = StableHash.Add(hash, Raw ? 1 : 0);
            hash = StableHash.Add(hash, StackLimit);
            hash = AddScaled(hash, WorkToMake);
            hash = StableHash.Add(hash, TurretGunDefName);
            hash = AddList(hash, LinkableFacilities);
            hash = AddScaled(hash, FacilityMaxDistance);
            hash = StableHash.Add(hash, IsSittable ? 1 : 0);
            hash = StableHash.Add(hash, SupportsSeatedWork ? 1 : 0);
            hash = StableHash.Add(hash, SupportsQuality ? 1 : 0);
            foreach (double value in new[] { MinimumRange, MaximumRange, FuelCapacity, FuelMultiplier,
                InitialFuelPercent, FuelPerShot, ExplosionRadius, CombatPower }) hash = AddScaled(hash, value);
            return StableHash.Add(hash, FuelFactorByDifficulty ? 1 : 0);
        }

        internal static ulong AddList(ulong hash, IReadOnlyList<string> values)
        {
            hash = StableHash.Add(hash, values.Count);
            foreach (string value in values) hash = StableHash.Add(hash, value);
            return hash;
        }

        private static ulong AddScaled(ulong hash, double value) => StableHash.Add(hash, unchecked((ulong)(long)Math.Round(value * 1000.0)));

        private static IReadOnlyList<string> List(IEnumerable<string> values) =>
            new ReadOnlyCollection<string>((values ?? Enumerable.Empty<string>()).Where(v => !string.IsNullOrEmpty(v)).Distinct(StringComparer.Ordinal).ToList());
    }

    // One research project and its prerequisites (prerequisites plus hiddenPrerequisites).
    public sealed class CatalogResearch
    {
        public CatalogResearch(string defName, TechTier techLevel, IEnumerable<string> prerequisites = null)
        {
            if (string.IsNullOrEmpty(defName)) throw new ArgumentNullException(nameof(defName));
            DefName = defName;
            TechLevel = techLevel;
            Prerequisites = new ReadOnlyCollection<string>((prerequisites ?? Enumerable.Empty<string>())
                .Where(p => !string.IsNullOrEmpty(p)).Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToList());
        }

        public string DefName { get; }
        public TechTier TechLevel { get; }
        public IReadOnlyList<string> Prerequisites { get; }
    }

    // Traps and IEDs are never generated. This filter runs before legality.
    public static class ProhibitedContent
    {
        public static IReadOnlyList<string> NameMarkers { get; } = new ReadOnlyCollection<string>(new[] { "Trap", "IED" });
        public static IReadOnlyList<string> TrapClasses { get; } = new ReadOnlyCollection<string>(new[]
        {
            "Building_Trap", "Building_TrapDamager", "Building_TrapExplosive", "Building_TrapRearmable"
        });
        public static IReadOnlyList<string> Research { get; } = new ReadOnlyCollection<string>(new[] { "IEDs" });

        public static bool IsProhibited(CatalogDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            return def.IsTrap || NameMarkers.Any(m => def.DefName.IndexOf(m, StringComparison.Ordinal) >= 0) ||
                TrapClasses.Contains(def.ThingClass) || def.ResearchPrerequisites.Any(IsProhibitedResearch);
        }

        public static bool IsProhibitedResearch(string research) => research != null && Research.Contains(research);
    }

    public sealed class CatalogLegality
    {
        public const string UnknownDef = "unknown_def";
        public const string Prohibited = "prohibited";
        public const string ResearchMissing = "research_missing";
        public const string AboveCeiling = "above_ceiling";
        public const string ProducerIllegal = "producer_illegal";

        private CatalogLegality(bool legal, string reason)
        {
            Legal = legal;
            Reason = reason;
        }

        public bool Legal { get; }
        // Null when legal; otherwise "<code>" or "<code>:<detail>".
        public string Reason { get; }
        public string Code => Reason == null ? null : Reason.Split(':')[0];
        public override string ToString() => Legal ? "legal" : Reason;

        internal static readonly CatalogLegality Yes = new CatalogLegality(true, null);
        internal static CatalogLegality No(string code, string detail = null) => new CatalogLegality(false, detail == null ? code : code + ":" + detail);
    }

    public sealed class TechCatalog
    {
        private readonly Dictionary<string, CatalogDef> defs;
        private readonly Dictionary<string, CatalogResearch> research;

        public TechCatalog(string source, IEnumerable<CatalogDef> defs, IEnumerable<CatalogResearch> research)
        {
            Source = source ?? string.Empty;
            this.defs = new Dictionary<string, CatalogDef>(StringComparer.Ordinal);
            foreach (CatalogDef def in defs ?? Enumerable.Empty<CatalogDef>())
            {
                if (def == null) throw new ArgumentNullException(nameof(defs));
                if (this.defs.ContainsKey(def.DefName)) throw new ArgumentException("duplicate def " + def.DefName, nameof(defs));
                this.defs.Add(def.DefName, def);
            }
            this.research = new Dictionary<string, CatalogResearch>(StringComparer.Ordinal);
            foreach (CatalogResearch project in research ?? Enumerable.Empty<CatalogResearch>())
            {
                if (project == null) throw new ArgumentNullException(nameof(research));
                if (this.research.ContainsKey(project.DefName)) throw new ArgumentException("duplicate research " + project.DefName, nameof(research));
                this.research.Add(project.DefName, project);
            }
            Defs = new ReadOnlyCollection<CatalogDef>(this.defs.Values.OrderBy(d => d.DefName, StringComparer.Ordinal).ToList());
            Research = new ReadOnlyCollection<CatalogResearch>(this.research.Values.OrderBy(r => r.DefName, StringComparer.Ordinal).ToList());
            Digest = ComputeDigest();
        }

        // Minimum tech tier for exotic raw items whose vanilla techLevel is Undefined
        // (salvaged or traded, never locally produced). The one override table,
        // shared by the fixture and the runtime adapter. PROVISIONAL.
        public static IReadOnlyDictionary<string, TechTier> RawMinimumTiers { get; } = new ReadOnlyDictionary<string, TechTier>(
            new Dictionary<string, TechTier>(StringComparer.Ordinal)
            {
                { "Synthread", TechTier.Industrial },
                { "Plasteel", TechTier.Industrial },
                { "Uranium", TechTier.Industrial },
                { "ComponentIndustrial", TechTier.Industrial },
                { "Hyperweave", TechTier.Spacer },
                { "ComponentSpacer", TechTier.Spacer }
            });

        // The def's techLevel, raised by RawMinimumTiers for raw items with an Undefined techLevel.
        public static TechTier EffectiveTechLevel(CatalogDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            TechTier minimum;
            return def.Raw && def.TechLevel == TechTier.Undefined && RawMinimumTiers.TryGetValue(def.DefName, out minimum) ? minimum : def.TechLevel;
        }

        public string Source { get; }
        // Sorted (ordinal) by defName.
        public IReadOnlyList<CatalogDef> Defs { get; }
        public IReadOnlyList<CatalogResearch> Research { get; }
        public string Digest { get; }

        public bool TryGet(string defName, out CatalogDef def)
        {
            def = null;
            return defName != null && defs.TryGetValue(defName, out def);
        }

        public bool TryGetResearch(string defName, out CatalogResearch project)
        {
            project = null;
            return defName != null && research.TryGetValue(defName, out project);
        }

        public bool IsLegal(string defName, SettlementTechStage stage) => Evaluate(defName, stage).Legal;
        public bool IsLegal(CatalogDef def, SettlementTechStage stage) => Evaluate(def, stage).Legal;

        public CatalogLegality Evaluate(string defName, SettlementTechStage stage)
        {
            CatalogDef def;
            if (!TryGet(defName, out def)) return CatalogLegality.No(CatalogLegality.UnknownDef, defName);
            return Evaluate(def, stage);
        }

        // Order: prohibited content, research, tech ceiling, then (items only) producer.
        public CatalogLegality Evaluate(CatalogDef def, SettlementTechStage stage)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (stage == null) throw new ArgumentNullException(nameof(stage));
            if (ProhibitedContent.IsProhibited(def)) return CatalogLegality.No(CatalogLegality.Prohibited);
            List<string> missing = def.ResearchPrerequisites.Where(r => !stage.HasResearch(r)).ToList();
            if (missing.Count > 0) return CatalogLegality.No(CatalogLegality.ResearchMissing, string.Join(",", missing));
            TechTier tier = EffectiveTechLevel(def);
            if (tier > stage.Ceiling) return CatalogLegality.No(CatalogLegality.AboveCeiling, tier + ">" + stage.Ceiling);
            if (def.Category == CatalogCategory.Item && !def.Raw && def.ProducedAt.Count > 0)
            {
                // Producers are buildings, which have no producer check, so this never recurses further.
                bool produced = def.ProducedAt.Any(p =>
                {
                    CatalogDef producer;
                    return TryGet(p, out producer) && producer.Category != CatalogCategory.Item && Evaluate(producer, stage).Legal;
                });
                if (!produced) return CatalogLegality.No(CatalogLegality.ProducerIllegal, string.Join(",", def.ProducedAt));
            }
            return CatalogLegality.Yes;
        }

        // Sorted (ordinal) legal defs for a stage; prohibited content is never included.
        public IReadOnlyList<CatalogDef> LegalDefs(SettlementTechStage stage) =>
            new ReadOnlyCollection<CatalogDef>(Defs.Where(d => Evaluate(d, stage).Legal).ToList());

        // Diagnostics for a research set against the catalog's research graph:
        // "unknown_research:X", "prohibited_research:X" and "missing_prerequisite:X->Y".
        public IReadOnlyList<string> ResearchSetGaps(IEnumerable<string> researchSet)
        {
            HashSet<string> set = new HashSet<string>(researchSet ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            List<string> gaps = new List<string>();
            foreach (string name in set.OrderBy(r => r, StringComparer.Ordinal))
            {
                if (ProhibitedContent.IsProhibitedResearch(name)) gaps.Add("prohibited_research:" + name);
                CatalogResearch project;
                if (!TryGetResearch(name, out project))
                {
                    gaps.Add("unknown_research:" + name);
                    continue;
                }
                foreach (string prerequisite in project.Prerequisites)
                    if (!set.Contains(prerequisite)) gaps.Add("missing_prerequisite:" + name + "->" + prerequisite);
            }
            return new ReadOnlyCollection<string>(gaps);
        }

        private string ComputeDigest()
        {
            ulong hash = StableHash.Start;
            hash = StableHash.Add(hash, "p4a-tech-catalog");
            hash = StableHash.Add(hash, Source);
            hash = StableHash.Add(hash, Defs.Count);
            foreach (CatalogDef def in Defs) hash = def.AddTo(hash);
            hash = StableHash.Add(hash, RawMinimumTiers.Count);
            foreach (KeyValuePair<string, TechTier> minimum in RawMinimumTiers.OrderBy(m => m.Key, StringComparer.Ordinal))
            {
                hash = StableHash.Add(hash, minimum.Key);
                hash = StableHash.Add(hash, (int)minimum.Value);
            }
            hash = StableHash.Add(hash, Research.Count);
            foreach (CatalogResearch project in Research)
            {
                hash = StableHash.Add(hash, project.DefName);
                hash = StableHash.Add(hash, (int)project.TechLevel);
                hash = CatalogDef.AddList(hash, project.Prerequisites);
            }
            return StableHash.Hex(hash);
        }
    }
}
