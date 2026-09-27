using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    // Tech-stage model for settlement equipment (pure, engine independent).
    // See docs/plans/2026-09-23-tech-stage-equipment.md. Nothing in the live
    // generator references these types yet.
    public static class TechStageVersions
    {
        public const int Schema = 1;
    }

    // Mirrors the order of Verse.TechLevel without referencing the engine; the
    // runtime adapter maps by name.
    public enum TechTier
    {
        Undefined,
        Animal,
        Neolithic,
        Medieval,
        Industrial,
        Spacer,
        Ultra,
        Archotech
    }

    public enum SettlementTechStageLevel
    {
        Neolithic,
        Medieval,
        IndustrialEarly,
        IndustrialMid,
        IndustrialLate,
        Spacer
    }

    // Research granted to one settlement beyond its stage, with the recorded reason
    // (for example Stonecutting for a wood-poor tribal site).
    public sealed class TechAdaptation
    {
        public TechAdaptation(string researchDefName, string reason)
        {
            if (string.IsNullOrEmpty(researchDefName)) throw new ArgumentNullException(nameof(researchDefName));
            if (string.IsNullOrEmpty(reason)) throw new ArgumentNullException(nameof(reason));
            ResearchDefName = researchDefName;
            Reason = reason;
        }

        public string ResearchDefName { get; }
        public string Reason { get; }
        public override string ToString() => ResearchDefName + "(" + Reason + ")";
    }

    // Immutable: a cumulative research set plus a tech ceiling, and per-settlement
    // adaptations. Adaptations only add research; they never raise the ceiling.
    public sealed class SettlementTechStage
    {
        private readonly HashSet<string> research;

        internal SettlementTechStage(SettlementTechStageLevel level, IEnumerable<TechAdaptation> adaptations)
        {
            Level = level;
            Ceiling = TechStageModel.CeilingOf(level);
            BaseResearch = new ReadOnlyCollection<string>(TechStageModel.CumulativeResearch(level).ToList());
            HashSet<string> baseSet = new HashSet<string>(BaseResearch, StringComparer.Ordinal);
            List<TechAdaptation> kept = new List<TechAdaptation>();
            foreach (TechAdaptation adaptation in (adaptations ?? Enumerable.Empty<TechAdaptation>()).OrderBy(a => a.ResearchDefName, StringComparer.Ordinal))
                if (!kept.Any(a => a.ResearchDefName == adaptation.ResearchDefName)) kept.Add(adaptation);
            Adaptations = new ReadOnlyCollection<TechAdaptation>(kept);
            research = new HashSet<string>(baseSet, StringComparer.Ordinal);
            foreach (TechAdaptation adaptation in kept) research.Add(adaptation.ResearchDefName);
            Research = new ReadOnlyCollection<string>(research.OrderBy(r => r, StringComparer.Ordinal).ToList());
            Digest = ComputeDigest();
        }

        public int SchemaVersion => TechStageVersions.Schema;
        public SettlementTechStageLevel Level { get; }
        public TechTier Ceiling { get; }
        // Sorted (ordinal) cumulative research of the stage alone.
        public IReadOnlyList<string> BaseResearch { get; }
        public IReadOnlyList<TechAdaptation> Adaptations { get; }
        // Sorted (ordinal) union of BaseResearch and adaptations.
        public IReadOnlyList<string> Research { get; }
        public string Digest { get; }

        public bool HasResearch(string defName) => defName != null && research.Contains(defName);

        // Returns this instance when the research is already present.
        public SettlementTechStage WithAdaptation(string researchDefName, string reason)
        {
            TechAdaptation adaptation = new TechAdaptation(researchDefName, reason);
            if (HasResearch(researchDefName)) return this;
            return new SettlementTechStage(Level, Adaptations.Concat(new[] { adaptation }));
        }

        // One stage lower, keeping the settlement's adaptations. False at Neolithic.
        public bool TryStepDown(out SettlementTechStage lower)
        {
            if (Level == SettlementTechStageLevel.Neolithic)
            {
                lower = null;
                return false;
            }
            lower = new SettlementTechStage(Level - 1, Adaptations);
            return true;
        }

        public override string ToString() => Level + "/" + Ceiling + (Adaptations.Count == 0 ? string.Empty : "+" + string.Join(",", Adaptations));

        private string ComputeDigest()
        {
            ulong hash = StableHash.Start;
            hash = StableHash.Add(hash, "p4a-tech-stage");
            hash = StableHash.Add(hash, TechStageVersions.Schema);
            hash = StableHash.Add(hash, (int)Level);
            hash = StableHash.Add(hash, (int)Ceiling);
            hash = StableHash.Add(hash, BaseResearch.Count);
            foreach (string r in BaseResearch) hash = StableHash.Add(hash, r);
            hash = StableHash.Add(hash, Adaptations.Count);
            foreach (TechAdaptation a in Adaptations)
            {
                hash = StableHash.Add(hash, a.ResearchDefName);
                hash = StableHash.Add(hash, a.Reason);
            }
            return StableHash.Hex(hash);
        }
    }

    public static class TechStageModel
    {
        public const string UnsupportedTechLevel = "faction_tech_level_unsupported";

        // Band weights in percent. PROVISIONAL: need live calibration.
        public const int NeolithicMedievalChancePercent = 10;
        public const int IndustrialEarlyWeight = 30;
        public const int IndustrialMidWeight = 50;
        public const int IndustrialLateWeight = 20;
        // Vanilla pirates are mostly industrial; only elites carry spacer gear.
        public const int SpacerBandMidWeight = 30;
        public const int SpacerBandLateWeight = 45;
        public const int SpacerBandSpacerWeight = 25;

        private static readonly string[] Neolithic = { "Pemmican", "TreeSowing", "PsychoidBrewing", "PassiveCooler", "RecurveBow" };
        private static readonly string[] Medieval = { "ComplexFurniture", "ComplexClothing", "Stonecutting", "Smithing", "LongBlades", "PlateArmor", "Greatbow", "CarpetMaking" };
        private static readonly string[] IndustrialEarly =
        {
            "ComplexFurniture", "Stonecutting", "ComplexClothing", "Electricity", "AirConditioning", "NutrientPaste", "PassiveCooler",
            "Batteries", "Machining", "Gunsmithing", "Mortars"
        };
        // SterileMaterials: prerequisite of HospitalBed, found by the prerequisite-closure check.
        private static readonly string[] IndustrialMid =
        {
            "BlowbackOperation", "GasOperation", "FlakArmor", "GunTurrets", "MicroelectronicsBasics", "SterileMaterials", "HospitalBed",
            "PrecisionRifling", "DrugProduction", "MedicineProduction", "SolarPanels", "WatermillGenerator", "GeothermalPower",
            "BiofuelRefining", "SmokepopBelt", "Firefoam"
        };
        // Prosthetics: prerequisite of Bionics, found by the prerequisite-closure check.
        private static readonly string[] IndustrialLate = { "MultiAnalyzer", "VitalsMonitor", "MultibarrelWeapons", "HeavyTurrets", "SniperTurret", "Fabrication", "Prosthetics" };
        private static readonly string[] Spacer = { "ShieldBelt", "ChargedShot", "ReconArmor", "PoweredArmor", "Bionics" };

        public static IReadOnlyList<SettlementTechStageLevel> Levels { get; } = new ReadOnlyCollection<SettlementTechStageLevel>(new[]
        {
            SettlementTechStageLevel.Neolithic, SettlementTechStageLevel.Medieval, SettlementTechStageLevel.IndustrialEarly,
            SettlementTechStageLevel.IndustrialMid, SettlementTechStageLevel.IndustrialLate, SettlementTechStageLevel.Spacer
        });

        public static TechTier CeilingOf(SettlementTechStageLevel level)
        {
            switch (level)
            {
                case SettlementTechStageLevel.Neolithic: return TechTier.Neolithic;
                case SettlementTechStageLevel.Medieval: return TechTier.Medieval;
                case SettlementTechStageLevel.Spacer: return TechTier.Spacer;
                default: return TechTier.Industrial;
            }
        }

        // Research the stage adds on top of the previous one, in table order.
        public static IReadOnlyList<string> AddedResearch(SettlementTechStageLevel level)
        {
            switch (level)
            {
                case SettlementTechStageLevel.Neolithic: return Array.AsReadOnly(Neolithic);
                case SettlementTechStageLevel.Medieval: return Array.AsReadOnly(Medieval);
                case SettlementTechStageLevel.IndustrialEarly: return Array.AsReadOnly(IndustrialEarly);
                case SettlementTechStageLevel.IndustrialMid: return Array.AsReadOnly(IndustrialMid);
                case SettlementTechStageLevel.IndustrialLate: return Array.AsReadOnly(IndustrialLate);
                case SettlementTechStageLevel.Spacer: return Array.AsReadOnly(Spacer);
                default: throw new ArgumentOutOfRangeException(nameof(level));
            }
        }

        // Sorted (ordinal), duplicate-free union of all stages up to and including level.
        public static IReadOnlyList<string> CumulativeResearch(SettlementTechStageLevel level)
        {
            SortedSet<string> result = new SortedSet<string>(StringComparer.Ordinal);
            foreach (SettlementTechStageLevel l in Levels)
            {
                if (l > level) break;
                foreach (string r in AddedResearch(l)) result.Add(r);
            }
            return new ReadOnlyCollection<string>(result.ToList());
        }

        public static SettlementTechStage Create(SettlementTechStageLevel level) => new SettlementTechStage(level, null);

        // Stage weights (percent, summing to 100) for a faction tech level; empty when unsupported.
        public static IReadOnlyList<KeyValuePair<SettlementTechStageLevel, int>> Band(TechTier factionTechLevel)
        {
            List<KeyValuePair<SettlementTechStageLevel, int>> band = new List<KeyValuePair<SettlementTechStageLevel, int>>();
            switch (factionTechLevel)
            {
                case TechTier.Neolithic:
                    band.Add(Weight(SettlementTechStageLevel.Neolithic, 100 - NeolithicMedievalChancePercent));
                    band.Add(Weight(SettlementTechStageLevel.Medieval, NeolithicMedievalChancePercent));
                    break;
                case TechTier.Medieval:
                    band.Add(Weight(SettlementTechStageLevel.Medieval, 100));
                    break;
                case TechTier.Industrial:
                    band.Add(Weight(SettlementTechStageLevel.IndustrialEarly, IndustrialEarlyWeight));
                    band.Add(Weight(SettlementTechStageLevel.IndustrialMid, IndustrialMidWeight));
                    band.Add(Weight(SettlementTechStageLevel.IndustrialLate, IndustrialLateWeight));
                    break;
                case TechTier.Spacer:
                case TechTier.Ultra:
                case TechTier.Archotech:
                    band.Add(Weight(SettlementTechStageLevel.IndustrialMid, SpacerBandMidWeight));
                    band.Add(Weight(SettlementTechStageLevel.IndustrialLate, SpacerBandLateWeight));
                    band.Add(Weight(SettlementTechStageLevel.Spacer, SpacerBandSpacerWeight));
                    break;
            }
            return new ReadOnlyCollection<KeyValuePair<SettlementTechStageLevel, int>>(band);
        }

        // Picks the faction's stage from its band. The faction seed comes from
        // LayoutSeed.DeriveFaction, so every settlement of a faction gets the same
        // stage. Integration must persist the resolved stage per faction so that a
        // later schema or weight change never re-rolls an existing faction.
        public static bool TryResolve(TechTier factionTechLevel, ulong factionSeed, out SettlementTechStage stage, out string diagnostic)
        {
            IReadOnlyList<KeyValuePair<SettlementTechStageLevel, int>> band = Band(factionTechLevel);
            if (band.Count == 0)
            {
                stage = null;
                diagnostic = UnsupportedTechLevel + ":" + factionTechLevel;
                return false;
            }
            int total = band.Sum(b => b.Value);
            int roll = LayoutSeed.Roll(LayoutSeed.Sub(factionSeed, "tech-stage|" + TechStageVersions.Schema), "band", total);
            SettlementTechStageLevel chosen = band[band.Count - 1].Key;
            foreach (KeyValuePair<SettlementTechStageLevel, int> entry in band)
            {
                if (roll < entry.Value)
                {
                    chosen = entry.Key;
                    break;
                }
                roll -= entry.Value;
            }
            stage = Create(chosen);
            diagnostic = null;
            return true;
        }

        private static KeyValuePair<SettlementTechStageLevel, int> Weight(SettlementTechStageLevel level, int weight) =>
            new KeyValuePair<SettlementTechStageLevel, int>(level, weight);
    }
}
