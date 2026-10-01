using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    // Equipment roles resolved against a TechCatalog for a settlement tech stage
    // (pure, engine independent). See docs/plans/2026-09-23-tech-stage-equipment.md.
    public enum SettlementRole
    {
        Sleep,
        MedicalBed,
        MedicalMonitor,
        Seat,
        Table,
        Light,
        Heat,
        Cool,
        Cooking,
        CraftSmithing,
        CraftTailoring,
        CraftStonecutting,
        CraftMachining,
        CraftFabrication,
        Storage,
        Recreation,
        Door,
        Defense,
        UtilityBelt,
        Research,
        ResearchAnalyzer,
        MedicalStorage
    }

    // One preference. A virtual option (a zone) needs no def and is always legal.
    // MaxStage caps primitive fallbacks: above it the option is not acceptable for
    // the settlement's tier, even when legal. MinStage keeps an option out of
    // simpler tiers (tribal = simple), even when legal.
    public sealed class RoleOption
    {
        private RoleOption(string defName, bool isVirtual, SettlementTechStageLevel? maxStage, SettlementTechStageLevel? minStage)
        {
            if (string.IsNullOrEmpty(defName)) throw new ArgumentNullException(nameof(defName));
            if (maxStage.HasValue && minStage.HasValue && minStage.Value > maxStage.Value) throw new ArgumentOutOfRangeException(nameof(minStage));
            DefName = defName;
            Virtual = isVirtual;
            MaxStage = maxStage;
            MinStage = minStage;
        }

        public string DefName { get; }
        public bool Virtual { get; }
        public SettlementTechStageLevel? MaxStage { get; }
        public SettlementTechStageLevel? MinStage { get; }

        public static RoleOption Def(string defName, SettlementTechStageLevel? maxStage = null, SettlementTechStageLevel? minStage = null) =>
            new RoleOption(defName, false, maxStage, minStage);
        public static RoleOption Zone(string name, SettlementTechStageLevel? maxStage = null, SettlementTechStageLevel? minStage = null) =>
            new RoleOption(name, true, maxStage, minStage);

        public bool AcceptableAt(SettlementTechStageLevel level) =>
            (!MaxStage.HasValue || level <= MaxStage.Value) && (!MinStage.HasValue || level >= MinStage.Value);
        public override string ToString() => (Virtual ? "zone:" : string.Empty) + DefName +
            (MinStage.HasValue ? "(>=" + MinStage.Value + ")" : string.Empty) + (MaxStage.HasValue ? "(<=" + MaxStage.Value + ")" : string.Empty);
    }

    public sealed class RoleDefinition
    {
        public RoleDefinition(SettlementRole role, bool essential, IEnumerable<RoleOption> options)
        {
            Role = role;
            Essential = essential;
            Options = new ReadOnlyCollection<RoleOption>((options ?? Enumerable.Empty<RoleOption>()).ToList());
            if (Options.Count == 0 || Options.Any(o => o == null)) throw new ArgumentException("role needs options", nameof(options));
        }

        public SettlementRole Role { get; }
        public bool Essential { get; }
        // Best first.
        public IReadOnlyList<RoleOption> Options { get; }
    }

    public sealed class RoleTable
    {
        private readonly Dictionary<SettlementRole, RoleDefinition> byRole;

        public RoleTable(IEnumerable<RoleDefinition> definitions)
        {
            byRole = new Dictionary<SettlementRole, RoleDefinition>();
            foreach (RoleDefinition definition in definitions ?? Enumerable.Empty<RoleDefinition>())
            {
                if (definition == null) throw new ArgumentNullException(nameof(definitions));
                if (byRole.ContainsKey(definition.Role)) throw new ArgumentException("duplicate role " + definition.Role, nameof(definitions));
                byRole.Add(definition.Role, definition);
            }
            Definitions = new ReadOnlyCollection<RoleDefinition>(byRole.Values.OrderBy(d => d.Role).ToList());
            Digest = ComputeDigest();
        }

        public IReadOnlyList<RoleDefinition> Definitions { get; }
        public string Digest { get; }

        public bool TryGet(SettlementRole role, out RoleDefinition definition) => byRole.TryGetValue(role, out definition);

        // The standard preference table: order, essential flags and MaxStage caps.
        // PROVISIONAL: the caps and orders need live calibration.
        public static RoleTable Standard { get; } = BuildStandard();

        private static RoleTable BuildStandard()
        {
            const SettlementTechStageLevel neolithic = SettlementTechStageLevel.Neolithic;
            const SettlementTechStageLevel medieval = SettlementTechStageLevel.Medieval;
            Func<string, RoleOption> d = name => RoleOption.Def(name);
            Func<string, SettlementTechStageLevel, RoleOption> capped = (name, max) => RoleOption.Def(name, max);
            return new RoleTable(new[]
            {
                new RoleDefinition(SettlementRole.Sleep, true, new[] { d("Bed"), capped("Bedroll", medieval), capped("SleepingSpot", neolithic) }),
                new RoleDefinition(SettlementRole.MedicalBed, false, new[] { d("HospitalBed"), d("Bed"), capped("Bedroll", medieval), capped("SleepingSpot", neolithic) }),
                new RoleDefinition(SettlementRole.MedicalMonitor, false, new[] { d("VitalsMonitor") }),
                new RoleDefinition(SettlementRole.Seat, false, new[] { d("DiningChair"), d("Stool") }),
                new RoleDefinition(SettlementRole.Table, false, new[] { d("Table2x2c"), d("Table1x2c") }),
                new RoleDefinition(SettlementRole.Light, true, new[] { d("StandingLamp"), capped("TorchLamp", medieval) }),
                // Heat and Cool are climate dependent; the planner escalates them when the climate demands it.
                new RoleDefinition(SettlementRole.Heat, false, new[] { d("Heater"), capped("Campfire", medieval) }),
                new RoleDefinition(SettlementRole.Cool, false, new[] { d("Cooler"), capped("PassiveCooler", medieval) }),
                new RoleDefinition(SettlementRole.Cooking, true, new[] { d("ElectricStove"), RoleOption.Def("FueledStove", minStage: medieval), capped("Campfire", neolithic) }),
                new RoleDefinition(SettlementRole.CraftSmithing, true, new[] { d("ElectricSmithy"), d("FueledSmithy"), capped("CraftingSpot", neolithic) }),
                new RoleDefinition(SettlementRole.CraftTailoring, true, new[] { d("ElectricTailoringBench"), d("HandTailoringBench"), capped("CraftingSpot", neolithic) }),
                new RoleDefinition(SettlementRole.CraftStonecutting, false, new[] { d("TableStonecutter") }),
                new RoleDefinition(SettlementRole.CraftMachining, false, new[] { d("TableMachining") }),
                new RoleDefinition(SettlementRole.CraftFabrication, false, new[] { d("FabricationBench") }),
                new RoleDefinition(SettlementRole.Research, false, new[] { d("HiTechResearchBench"), d("SimpleResearchBench") }),
                new RoleDefinition(SettlementRole.ResearchAnalyzer, false, new[] { d("MultiAnalyzer") }),
                new RoleDefinition(SettlementRole.Storage, true, new[] { d("Shelf"), RoleOption.Zone("Stockpile") }),
                new RoleDefinition(SettlementRole.MedicalStorage, false, new[] { d("ShelfSmall"), d("Shelf") }),
                new RoleDefinition(SettlementRole.Recreation, false, new[]
                {
                    d("ChessTable"), d("BilliardsTable"), d("PokerTable"), capped("HorseshoesPin", medieval), capped("HoopstoneRing", medieval),
                    capped("GameOfUrBoard", medieval)
                }),
                new RoleDefinition(SettlementRole.Door, true, new[] { d("Door") }),
                new RoleDefinition(SettlementRole.Defense, false, new[] { RoleOption.Def("Turret_Sniper", minStage: SettlementTechStageLevel.IndustrialLate), d("Turret_Autocannon"), d("Turret_MiniTurret"), d("Sandbags"), d("Barricade") }),
                new RoleDefinition(SettlementRole.UtilityBelt, false, new[] { d("Apparel_ShieldBelt"), d("Apparel_SmokepopBelt"), d("Apparel_FirefoampopPack") })
            });
        }

        private string ComputeDigest()
        {
            ulong hash = StableHash.Start;
            hash = StableHash.Add(hash, "p4a-role-table");
            hash = StableHash.Add(hash, Definitions.Count);
            foreach (RoleDefinition definition in Definitions)
            {
                hash = StableHash.Add(hash, (int)definition.Role);
                hash = StableHash.Add(hash, definition.Essential ? 1 : 0);
                hash = StableHash.Add(hash, definition.Options.Count);
                foreach (RoleOption option in definition.Options)
                {
                    hash = StableHash.Add(hash, option.DefName);
                    hash = StableHash.Add(hash, option.Virtual ? 1 : 0);
                    hash = StableHash.Add(hash, option.MaxStage.HasValue ? (int)option.MaxStage.Value : -1);
                    hash = StableHash.Add(hash, option.MinStage.HasValue ? (int)option.MinStage.Value : -1);
                }
            }
            return StableHash.Hex(hash);
        }
    }

    public sealed class RoleResolution
    {
        internal RoleResolution(RoleDefinition definition, IList<RoleOption> candidates, IList<KeyValuePair<string, string>> rejected)
        {
            Role = definition.Role;
            Essential = definition.Essential;
            Candidates = new ReadOnlyCollection<RoleOption>(candidates);
            Rejected = new ReadOnlyCollection<KeyValuePair<string, string>>(rejected);
        }

        public SettlementRole Role { get; }
        public bool Essential { get; }
        // Legal and tier-acceptable options, best first.
        public IReadOnlyList<RoleOption> Candidates { get; }
        // Option name -> legality reason, "above_max_stage:<cap>" or "below_min_stage:<min>".
        public IReadOnlyList<KeyValuePair<string, string>> Rejected { get; }
        public bool Resolved => Candidates.Count > 0;
        public RoleOption Chosen => Resolved ? Candidates[0] : null;
        public string ChosenDefName => Resolved ? Candidates[0].DefName : null;
        public override string ToString() => Role + "=" + (ChosenDefName ?? "-");
    }

    public sealed class RoleResolutionSet
    {
        internal RoleResolutionSet(SettlementTechStage requested, SettlementTechStage stage, IList<RoleResolution> resolutions,
            IList<string> stepDowns, IList<string> diagnostics, bool success, string catalogDigest, string tableDigest)
        {
            RequestedStage = requested;
            Stage = stage;
            Resolutions = new ReadOnlyCollection<RoleResolution>(resolutions);
            StepDowns = new ReadOnlyCollection<string>(stepDowns);
            Diagnostics = new ReadOnlyCollection<string>(diagnostics);
            Success = success;
            Digest = ComputeDigest(catalogDigest, tableDigest);
        }

        public SettlementTechStage RequestedStage { get; }
        // The stage the kit was resolved at (lower than requested after step-downs).
        public SettlementTechStage Stage { get; }
        // Ordered by role.
        public IReadOnlyList<RoleResolution> Resolutions { get; }
        // "stage_step_down:<from>-><to>:essential_role_unresolved:<roles>"
        public IReadOnlyList<string> StepDowns { get; }
        // "role_unresolved:<role>" for optional roles; "essential_role_unresolved:<role>" on failure.
        public IReadOnlyList<string> Diagnostics { get; }
        public bool Success { get; }
        public string Digest { get; }

        public RoleResolution Get(SettlementRole role) => Resolutions.FirstOrDefault(r => r.Role == role);
        public string ChosenDefName(SettlementRole role) => Get(role)?.ChosenDefName;

        private string ComputeDigest(string catalogDigest, string tableDigest)
        {
            ulong hash = StableHash.Start;
            hash = StableHash.Add(hash, "p4a-role-resolution");
            hash = StableHash.Add(hash, catalogDigest);
            hash = StableHash.Add(hash, tableDigest);
            hash = StableHash.Add(hash, RequestedStage.Digest);
            hash = StableHash.Add(hash, Stage.Digest);
            hash = StableHash.Add(hash, Success ? 1 : 0);
            foreach (RoleResolution resolution in Resolutions)
            {
                hash = StableHash.Add(hash, (int)resolution.Role);
                hash = StableHash.Add(hash, resolution.Candidates.Count);
                foreach (RoleOption option in resolution.Candidates) hash = StableHash.Add(hash, option.DefName);
            }
            foreach (string line in StepDowns.Concat(Diagnostics)) hash = StableHash.Add(hash, line);
            return StableHash.Hex(hash);
        }
    }

    public static class RoleResolver
    {
        public const string StepDown = "stage_step_down";
        public const string EssentialUnresolved = "essential_role_unresolved";
        public const string RoleUnresolved = "role_unresolved";
        public const string AboveMaxStage = "above_max_stage";
        public const string BelowMinStage = "below_min_stage";

        // Resolves every role at the stage. When an essential role cannot be
        // resolved, the settlement cannot equip its tier: the stage steps down one
        // level (keeping adaptations) and the whole kit is re-resolved there. At
        // Neolithic the resolution fails.
        public static RoleResolutionSet Resolve(TechCatalog catalog, SettlementTechStage stage, RoleTable table = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (stage == null) throw new ArgumentNullException(nameof(stage));
            table = table ?? RoleTable.Standard;
            List<string> stepDowns = new List<string>();
            SettlementTechStage current = stage;
            while (true)
            {
                List<RoleResolution> resolutions = table.Definitions.Select(d => ResolveRole(catalog, current, d)).ToList();
                List<RoleResolution> missing = resolutions.Where(r => r.Essential && !r.Resolved).ToList();
                List<string> diagnostics = resolutions.Where(r => !r.Essential && !r.Resolved).Select(r => RoleUnresolved + ":" + r.Role).ToList();
                if (missing.Count == 0)
                    return new RoleResolutionSet(stage, current, resolutions, stepDowns, diagnostics, true, catalog.Digest, table.Digest);
                SettlementTechStage lower;
                if (!current.TryStepDown(out lower))
                {
                    diagnostics.InsertRange(0, missing.Select(r => EssentialUnresolved + ":" + r.Role));
                    return new RoleResolutionSet(stage, current, resolutions, stepDowns, diagnostics, false, catalog.Digest, table.Digest);
                }
                stepDowns.Add(StepDown + ":" + current.Level + "->" + lower.Level + ":" + EssentialUnresolved + ":" +
                    string.Join(",", missing.Select(r => r.Role.ToString())));
                current = lower;
            }
        }

        private static RoleResolution ResolveRole(TechCatalog catalog, SettlementTechStage stage, RoleDefinition definition)
        {
            List<RoleOption> candidates = new List<RoleOption>();
            List<KeyValuePair<string, string>> rejected = new List<KeyValuePair<string, string>>();
            foreach (RoleOption option in definition.Options)
            {
                if (!option.AcceptableAt(stage.Level))
                {
                    rejected.Add(new KeyValuePair<string, string>(option.DefName, option.MaxStage.HasValue && stage.Level > option.MaxStage.Value
                        ? AboveMaxStage + ":" + option.MaxStage.Value : BelowMinStage + ":" + option.MinStage.Value));
                    continue;
                }
                if (!option.Virtual)
                {
                    CatalogLegality legality = catalog.Evaluate(option.DefName, stage);
                    if (!legality.Legal)
                    {
                        rejected.Add(new KeyValuePair<string, string>(option.DefName, legality.Reason));
                        continue;
                    }
                }
                candidates.Add(option);
            }
            return new RoleResolution(definition, candidates, rejected);
        }
    }
}
