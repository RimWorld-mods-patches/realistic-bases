using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    public sealed class P4BTurretPlan
    {
        internal P4BTurretPlan(CatalogDef def, GridPoint origin, IEnumerable<GridPoint> footprint,
            GridPoint serviceCell, IEnumerable<GridPoint> serviceRoute, string fuelDefName,
            int loadedFuelUnits, int reserveFuelUnits, int importValue, P4BTurretPlan support = null,
            IEnumerable<GridPoint> coverCells = null, GridPoint? accessGate = null,
            GridPoint? outward = null, string side = null, IEnumerable<P4BTurretPlan> additionalGroups = null,
            bool betweenNests = false, int spacingLevel = 0)
        {
            DefName = def.DefName;
            Origin = origin;
            Footprint = new ReadOnlyCollection<GridPoint>(footprint.Distinct().OrderBy(c => c).ToList());
            ServiceCell = serviceCell;
            ServiceRoute = new ReadOnlyCollection<GridPoint>(serviceRoute.ToList());
            CoverCells = new ReadOnlyCollection<GridPoint>((coverCells ?? Enumerable.Empty<GridPoint>())
                .Distinct().OrderBy(c => c).ToList());
            FuelDefName = fuelDefName;
            LoadedFuelUnits = loadedFuelUnits;
            ReserveFuelUnits = reserveFuelUnits;
            Watts = def.BasePowerConsumption;
            MinimumRange = def.MinimumRange;
            MaximumRange = def.MaximumRange;
            ExplosionRadius = def.ExplosionRadius;
            Support = support;
            AdditionalGroups = new ReadOnlyCollection<P4BTurretPlan>((additionalGroups ??
                Enumerable.Empty<P4BTurretPlan>()).ToList());
            AccessGate = accessGate; Outward = outward; Side = side ?? string.Empty;
            BetweenNests = betweenNests;
            SpacingLevel = spacingLevel;
            ImportValue = importValue + (support?.ImportValue ?? 0) + AdditionalGroups.Sum(g => g.ImportValue);
            ulong hash = StableHash.Add(StableHash.Start, "p4b-turret-v1");
            hash = StableHash.Add(hash, DefName);
            hash = StableHash.Add(hash, Origin.X); hash = StableHash.Add(hash, Origin.Z);
            hash = StableHash.Add(hash, ServiceCell.X); hash = StableHash.Add(hash, ServiceCell.Z);
            foreach (GridPoint c in Footprint.Concat(ServiceRoute).Concat(CoverCells))
            { hash = StableHash.Add(hash, c.X); hash = StableHash.Add(hash, c.Z); }
            hash = StableHash.Add(hash, FuelDefName);
            hash = StableHash.Add(hash, LoadedFuelUnits);
            hash = StableHash.Add(hash, ReserveFuelUnits);
            hash = StableHash.Add(hash, Watts);
            hash = StableHash.Add(hash, ImportValue);
            hash = StableHash.Add(hash, Support?.Digest ?? string.Empty);
            foreach (P4BTurretPlan group in AdditionalGroups) hash = StableHash.Add(hash, group.Digest);
            hash = StableHash.Add(hash, Side);
            hash = StableHash.Add(hash, SpacingLevel);
            foreach (GridPoint c in new[] { accessGate ?? new GridPoint(-1, -1), outward ?? new GridPoint(0, 0) })
            { hash = StableHash.Add(hash, c.X); hash = StableHash.Add(hash, c.Z); }
            Digest = hash.ToString("X16");
        }

        public string DefName { get; }
        public GridPoint Origin { get; }
        public IReadOnlyList<GridPoint> Footprint { get; }
        public GridPoint ServiceCell { get; }
        // From the selected perimeter access gate to a legal refuel/repair cell; part of the
        // defended and conduit-access reservation, not a claim of a pawn job.
        public IReadOnlyList<GridPoint> ServiceRoute { get; }
        public IReadOnlyList<GridPoint> CoverCells { get; }
        public string FuelDefName { get; }
        // Equivalent physical item units, including the turret's initial load.
        public int LoadedFuelUnits { get; }
        public int ReserveFuelUnits { get; }
        public int Watts { get; }
        public double MinimumRange { get; }
        public double MaximumRange { get; }
        public double ExplosionRadius { get; }
        public int ImportValue { get; }
        // A large gun is never an isolated emplacement: its close-blind zone
        // is assigned a separate mini-turret and useful pawn support at that
        // front. Mini-turret-only plans have no support child.
        public P4BTurretPlan Support { get; }
        // Only the network root owns peers; support remains a local blind-zone gun.
        public IReadOnlyList<P4BTurretPlan> AdditionalGroups { get; }
        public IEnumerable<P4BTurretPlan> Groups => new[] { this }.Concat(AdditionalGroups);
        public GridPoint? AccessGate { get; }
        public GridPoint? Outward { get; }
        public string Side { get; }
        public bool BetweenNests { get; }
        // 0: blast radius + 2; 1: radius; 2: mini-to-mini below radius.
        public int SpacingLevel { get; }
        public IEnumerable<P4BTurretPlan> All => Groups.SelectMany(g => new[] { g }.Concat(
            g.Support == null ? Enumerable.Empty<P4BTurretPlan>() : new[] { g.Support }));
        public int TotalWatts => All.Sum(t => t.Watts);
        public string Digest { get; }
    }

    // A bounded, incrementally approach-ranked defense network, not a raid simulator.
    // Large guns require a mini-turret and staffed crossfire in their dead ground.
    // Failure leaves the legal field enclosure intact and reports why.
    public static class P4BTurretPlanner
    {
        private sealed class Frame
        {
            internal GridPoint Anchor, Gate, Outward;
            internal GridPoint? FirstNest, PartnerNest;
            internal string Side;
            internal bool BetweenNests;
        }
        // Kept for parsing older T5g diagnostics; no current planner path creates these.
        private sealed class BetweenCandidateDiagnostic
        {
            internal string Type, Stage, Rejection;
            internal GridPoint Gate, NestA, NestB, StretchCell, Origin;
            internal GridPoint? Blocker;
        }
        private static readonly GridPoint[] Cardinal =
        { new GridPoint(0, -1), new GridPoint(-1, 0), new GridPoint(1, 0), new GridPoint(0, 1) };

        public static IReadOnlyList<string> HeavyWeaponOrder(IEnumerable<GridPoint> targets, IEnumerable<GridPoint> positions,
            IEnumerable<string> previousGuns, double autocannonRange, double slugRange)
        {
            var posts = positions.Distinct().ToArray();
            int near = 0, longOnly = 0;
            foreach (GridPoint target in targets.Distinct())
            {
                double distance = posts.Length == 0 ? double.MaxValue : posts.Min(p => Distance(p, target));
                if (distance <= autocannonRange) near++;
                else if (distance <= slugRange) longOnly++;
            }
            var previous = new HashSet<string>(previousGuns, StringComparer.Ordinal);
            bool preferAuto = near >= longOnly;
            if (previous.Contains("Turret_Sniper") && !previous.Contains("Turret_Autocannon") && near > 0) preferAuto = true;
            if (previous.Contains("Turret_Autocannon") && !previous.Contains("Turret_Sniper") && longOnly > 0) preferAuto = false;
            return preferAuto ? new[] { "Turret_Autocannon", "Turret_Sniper", "Turret_MiniTurret" } :
                new[] { "Turret_Sniper", "Turret_Autocannon", "Turret_MiniTurret" };
        }

        public static IReadOnlyList<string> Validate(ProceduralLayoutInput input, ProceduralLayout layout,
            P4BTurretPlan turret, IEnumerable<GridPoint> additionalBlocked = null,
            SettlementLoadout loadout = null)
            => ValidateInternal(input, layout, turret, additionalBlocked, loadout,
                checkCrossingExposure: false);

        private static IReadOnlyList<string> ValidateInternal(ProceduralLayoutInput input, ProceduralLayout layout,
            P4BTurretPlan turret, IEnumerable<GridPoint> additionalBlocked,
            SettlementLoadout loadout, P4BTurretPlan onlyGroup = null,
            bool includeNetworkChecks = true, bool checkCrossingExposure = true,
            CellSet sharedBaseOpaque = null)
        {
            if (input == null || layout == null || turret == null ||
                layout.FieldWall == null && input.QuestContract == null) return new[] { "missing_plan" };
            List<string> errors = new List<string>();
            List<P4BTurretPlan> guns = turret.All.ToList();
            if (turret.AdditionalGroups.Any(g => g.AdditionalGroups.Count > 0) ||
                guns.Any(g => g.Support?.AdditionalGroups.Count > 0)) errors.Add("nested_groups");
            foreach (P4BTurretPlan group in turret.Groups)
            {
                if (onlyGroup != null && !ReferenceEquals(group, onlyGroup) &&
                    (group.DefName != onlyGroup.DefName || !group.Origin.Equals(onlyGroup.Origin) ||
                     !Nullable.Equals(group.AccessGate, onlyGroup.AccessGate))) continue;
                List<P4BTurretPlan> others = guns.Except(new[] { group, group.Support }).ToList();
                errors.AddRange(ValidateGroup(input, layout, group,
                    (additionalBlocked ?? Enumerable.Empty<GridPoint>()).Concat(others.SelectMany(t =>
                        t.Footprint.Concat(t.CoverCells))), others));
            }
            for (int i = 0; i < guns.Count; i++)
                for (int j = i + 1; j < guns.Count; j++)
                {
                    P4BTurretPlan a = guns[i], b = guns[j];
                    if (a.Footprint.Concat(a.CoverCells).Intersect(b.Footprint.Concat(b.CoverCells)).Any() ||
                        a.ServiceRoute.Intersect(b.Footprint.Concat(b.CoverCells)).Any() ||
                        b.ServiceRoute.Intersect(a.Footprint.Concat(a.CoverCells)).Any() ||
                        a.Footprint.Any(c => b.Footprint.Any(d =>
                            Distance(c, d) < RequiredTurretSpacing(a, b))))
                        errors.Add("network_collision_or_blast");
                }
            if (BaseMaterialValue(input, layout) + turret.ImportValue > input.Requirements.ImportValueLimit)
                errors.Add("network_import_budget");
            if (layout.Power == null || AvailableWatts(layout) < turret.TotalWatts) errors.Add("network_power_budget");
            if (!PowerTerminalsConnected(input, layout, turret)) errors.Add("network_power_access");
            HashSet<GridPoint> finalOpaque = sharedBaseOpaque == null
                ? P4BApproachDefensePlanner.Opaque(layout, input.Terrain)
                : sharedBaseOpaque.ToHashSet();
            if (layout.ApproachDefense != null)
            {
                HashSet<GridPoint> beforeGuns = new HashSet<GridPoint>(finalOpaque);
                finalOpaque.UnionWith(guns.SelectMany(t => t.Footprint));
                if (layout.ApproachDefense.Sides.Any(s => s.Exposed &&
                    P4BApproachDefensePlanner.CoveredSamples(s, beforeGuns) >= P4BApproachDefensePlanner.UsefulArc(s.Samples.Count) &&
                    P4BApproachDefensePlanner.CoveredSamples(s, finalOpaque) < P4BApproachDefensePlanner.UsefulArc(s.Samples.Count)))
                    errors.Add("turret_obstructs_defender_frontage");
            }
            finalOpaque.UnionWith(guns.SelectMany(t => t.Footprint));
            if (!includeNetworkChecks) return errors.Distinct().ToList();
            // A quest camp's guns are the native advertised threat: its one guard
            // cannot staff them, so the garrison support rules below are for
            // settlements only. Geometry, cover, service and power still apply.
            if (input.QuestContract != null) return errors.Distinct().ToList();
            IEnumerable<GridPoint> posts = layout.ApproachDefense?.AllPosts ?? new GridPoint[0];
            List<GridPoint> approachTargets = ApproachTargets(input, layout).ToList();
            Dictionary<P4BTurretPlan, HashSet<GridPoint>> gunShots = guns.Distinct().ToDictionary(g => g,
                g => new HashSet<GridPoint>(approachTargets.Where(c => CanShoot(g, c, finalOpaque))));
            HashSet<GridPoint> postShots = new HashSet<GridPoint>(approachTargets.Where(c =>
                posts.Any(p => P4BFireLane.CanEngage(p, c, 0, 25, finalOpaque))));
            foreach (P4BTurretPlan group in turret.Groups)
            {
                // A projecting group must share actual reachable approach targets with another
                // gun or an adjacent-cover pawn post. Proximity alone is not mutual support.
                HashSet<GridPoint> groupShots = gunShots[group];
                int sharedShots = groupShots.Count(c => postShots.Contains(c) ||
                    guns.Any(t => !ReferenceEquals(t, group) && gunShots[t].Contains(c)));
                if (sharedShots < 4) errors.Add("network_isolated_group");
            }
            // Interlocking gun fire is not enough: attackers can close on an
            // isolated emplacement while its defenders stand out of range.
            // Require each physical gun (including a heavy gun's mini-turret)
            // to be near a post that can shoot edge-reachable ground around it.
            if (layout.ApproachDefense != null)
            {
                int standardRifleRange = StandardRifleRange(input);
                if (guns.Any(g => !NearbyPawnSupport(layout.ApproachDefense, g, finalOpaque,
                    standardRifleRange))) errors.Add("turret_without_nearby_pawn_support");
                if (checkCrossingExposure)
                foreach (IGrouping<GridPoint?, P4BTurretPlan> crossing in turret.Groups
                    .GroupBy(g => g.AccessGate).OrderBy(g => g.Key))
                {
                    List<P4BTurretPlan> battery = crossing.SelectMany(g => g.All).ToList();
                    bool slugSelected = battery.Any(g => g.DefName == "Turret_Sniper");
                    bool changedCrossing = onlyGroup == null || Nullable.Equals(crossing.Key, onlyGroup.AccessGate);
                    if (changedCrossing && slugSelected &&
                        battery.Count(g => g.DefName == "Turret_MiniTurret") >= 3)
                    {
                        ExposureCoverage(layout, input.Terrain, battery, guns, new GridPoint[0],
                            out int threats, out int answered);
                        if (threats == 0 || answered * 100 < threats * ExposureRequiredPercent())
                            errors.Add("crossing_hard_exposure_coverage");
                    }
                }
            }
            return errors.Distinct().ToList();
        }

        internal static bool NearbyPawnSupport(P4BApproachDefensePlan defense,
            P4BTurretPlan gun, HashSet<GridPoint> opaque, int defenderWeaponRange = 36)
        {
            P4BAttackSide side = defense.Sides.FirstOrDefault(s => s.Id == gun.Side && s.Exposed);
            return side != null && side.Positions.Any(post => gun.Footprint.Min(c => Distance(post, c)) <= 14 &&
                side.Samples.Count(target => gun.Footprint.Min(c => Distance(c, target)) <= 18 &&
                    P4BFireLane.CanEngage(post, target, 0, defenderWeaponRange, opaque)) >= 2);
        }

        private static int StandardRifleRange(ProceduralLayoutInput input)
        {
            foreach (string name in new[] { "Gun_BoltActionRifle", "Gun_AssaultRifle", "Gun_ChargeRifle" })
            {
                if (input.Catalog.TryGet(name, out CatalogDef rifle) &&
                    input.Catalog.IsLegal(rifle, input.Equipment.Kit.Stage) && rifle.MaximumRange > 0)
                    return (int)Math.Floor(rifle.MaximumRange);
            }
            return 0;
        }

        private static int ExposureRequiredPercent()
        {
            string value = Environment.GetEnvironmentVariable("T5_EXPOSURE_THRESHOLD_PERCENT");
            return int.TryParse(value, out int percent) && percent >= 90 && percent <= 100 ? percent : 90;
        }

        private static double RequiredTurretSpacing(P4BTurretPlan a, P4BTurretPlan b) =>
            RequiredTurretSpacing(a.DefName, a.ExplosionRadius, a.SpacingLevel, a.AccessGate,
                b.DefName, b.ExplosionRadius, b.SpacingLevel, b.AccessGate);

        private static double RequiredTurretSpacing(CatalogDef a, int aLevel, GridPoint? aGate,
            P4BTurretPlan b) => RequiredTurretSpacing(a.DefName, a.ExplosionRadius, aLevel, aGate,
                b.DefName, b.ExplosionRadius, b.SpacingLevel, b.AccessGate);

        private static double RequiredTurretSpacing(string aName, double aRadius, int aLevel, GridPoint? aGate,
            string bName, double bRadius, int bLevel, GridPoint? bGate)
        {
            bool sameCrossing = aGate.HasValue && bGate.HasValue && aGate.Value.Equals(bGate.Value);
            int level = sameCrossing ? Math.Min(aLevel, bLevel) : 0;
            double radius = Math.Max(aRadius, bRadius);
            if (level == 0) return radius + 2;
            if (level == 1) return radius;
            bool bothMini = aName == "Turret_MiniTurret" && bName == "Turret_MiniTurret";
            return bothMini ? 0 : radius;
        }

        private static bool BetweenNestRifleSupport(ProceduralLayout layout, Frame frame, GridPoint origin,
            int rifleRange, HashSet<GridPoint> opaque)
        {
            if (layout.ApproachDefense == null || !frame.FirstNest.HasValue || !frame.PartnerNest.HasValue)
                return false;
            P4BAttackSide side = layout.ApproachDefense.Sides.FirstOrDefault(s => s.Id == frame.Side && s.Exposed);
            if (side == null || rifleRange <= 0) return false;
            bool lineFrom(GridPoint nest)
            {
                return side.Samples.Count(target => Distance(origin, target) <= 18 &&
                    P4BFireLane.CanEngage(nest, target, 0, rifleRange, opaque)) >= 2;
            }
            GridPoint a = frame.FirstNest.Value, b = frame.PartnerNest.Value;
            return Distance(origin, a) <= 14 && Distance(origin, b) <= rifleRange && lineFrom(b) ||
                Distance(origin, b) <= 14 && Distance(origin, a) <= rifleRange && lineFrom(a);
        }

        private static int BestRosterWeaponRange(ProceduralLayoutInput input, ProceduralLayout layout,
            SettlementLoadout loadout)
        {
            loadout = loadout ?? LayoutLoadoutModel.Build(input, layout);
            if (loadout == null || !loadout.Success) return 0;
            return loadout.Residents.Where(r => r.Weapon != null &&
                    input.Catalog.TryGet(r.Weapon.DefName, out CatalogDef def) &&
                    def.Category == CatalogCategory.Weapon && def.MaximumRange > 0)
                .Select(r => input.Catalog.TryGet(r.Weapon.DefName, out CatalogDef def)
                    ? (int)Math.Floor(def.MaximumRange) : 0).DefaultIfEmpty(0).Max();
        }

        private static void ExposureCoverage(ProceduralLayout layout, TerrainSnapshot terrain,
            IReadOnlyList<P4BTurretPlan> battery, IReadOnlyList<P4BTurretPlan> network,
            IReadOnlyList<GridPoint> guards, out int threats, out int answered)
        {
            threats = 0; answered = 0;
            HashSet<GridPoint> opaque = P4BApproachDefensePlanner.Opaque(layout, terrain);
            P4BTurretPlan[] batterySlugs = battery.Where(g => g.DefName == "Turret_Sniper").ToArray();
            P4BTurretPlan[] batteryMinis = battery.Where(g => g.DefName == "Turret_MiniTurret").ToArray();
            var sniperBlockers = network.Where(g => g.DefName == "Turret_Sniper")
                .Select(slug => new { Gun = slug, Blockers = SniperBlockers(slug) }).ToArray();
            P4BTurretPlan[] networkMinis = network.Where(g => g.DefName == "Turret_MiniTurret").ToArray();
            HashSet<GridPoint> exposure = new HashSet<GridPoint>();
            foreach (P4BTurretPlan gun in battery)
            {
                HashSet<GridPoint> incoming = new HashSet<GridPoint>(opaque);
                incoming.ExceptWith(gun.Footprint);
                for (int x = 0; x < terrain.Width; x += 2)
                    for (int z = 0; z < terrain.Height; z += 2)
                    {
                        GridPoint cell = new GridPoint(x, z);
                        if (layout.ApproachDefense.AttackerCanReach(cell) && Distance(gun.Origin, cell) <= 44.9 &&
                            P4BFireLane.CanEngage(cell, gun.Origin, 0, 45, incoming)) exposure.Add(cell);
                    }
            }
            threats = exposure.Count;
            foreach (GridPoint cell in exposure)
            {
                bool slugBlind = false;
                foreach (P4BTurretPlan slug in batterySlugs)
                    if (Distance(slug.Origin, cell) < slug.MinimumRange) { slugBlind = true; break; }
                if (slugBlind)
                {
                    bool miniAnswers = false;
                    foreach (P4BTurretPlan mini in batteryMinis)
                        if (Distance(mini.Origin, cell) <= mini.MaximumRange &&
                            P4BFireLane.CanEngage(mini.Origin, cell, 0, (int)Math.Floor(mini.MaximumRange), opaque))
                        { miniAnswers = true; break; }
                    if (miniAnswers) answered++;
                    continue;
                }
                bool answeredHere = guards.Any(post => Distance(post, cell) <= 44.9 &&
                    P4BFireLane.CanEngage(post, cell, 0, 45, opaque));
                if (!answeredHere)
                    foreach (var entry in sniperBlockers)
                    {
                        P4BTurretPlan slug = entry.Gun;
                        double distance = Distance(slug.Origin, cell);
                        if (distance >= slug.MinimumRange && distance <= slug.MaximumRange &&
                            P4BFireLane.CanEngage(slug.Origin, cell, 12, 46, entry.Blockers))
                        { answeredHere = true; break; }
                    }
                if (!answeredHere)
                {
                    foreach (P4BTurretPlan mini in networkMinis)
                        if (Distance(mini.Origin, cell) <= mini.MaximumRange &&
                            P4BFireLane.CanEngage(mini.Origin, cell, 0, (int)Math.Floor(mini.MaximumRange), opaque))
                        { answeredHere = true; break; }
                }
                if (answeredHere) answered++;
            }

            HashSet<GridPoint> SniperBlockers(P4BTurretPlan slug)
            {
                HashSet<GridPoint> blockers = new HashSet<GridPoint>(opaque);
                blockers.ExceptWith(slug.Footprint);
                return blockers;
            }
        }

        private static IEnumerable<string> HardExposureFailureDetails(ProceduralLayoutInput input,
            ProceduralLayout layout, P4BTurretPlan network, SettlementLoadout loadout, GridPoint? changedGate = null)
        {
            if (layout.ApproachDefense == null) yield break;
            foreach (IGrouping<GridPoint?, P4BTurretPlan> crossing in network.Groups.GroupBy(g => g.AccessGate))
            {
                if (changedGate.HasValue && !Nullable.Equals(changedGate, crossing.Key)) continue;
                List<P4BTurretPlan> battery = crossing.SelectMany(g => g.All).ToList();
                bool slug = battery.Any(g => g.DefName == "Turret_Sniper");
                if (!slug || battery.Count(g => g.DefName == "Turret_MiniTurret") < 3)
                    continue;
                ExposureCoverage(layout, input.Terrain, battery, network.All.ToList(), new GridPoint[0],
                    out int threats, out int answered);
                if (threats == 0 || answered * 100 < threats * ExposureRequiredPercent())
                    yield return (crossing.Key?.ToString() ?? "none") + ":" + answered + "/" + threats +
                        ":trigger=slug";
            }
        }

        private static int UnansweredExposure(ProceduralLayoutInput input, ProceduralLayout layout,
            GridPoint origin, CatalogDef candidate, IReadOnlyList<P4BTurretPlan> previous,
            IReadOnlyList<GridPoint> staffedPosts, HashSet<GridPoint> opaque,
            Dictionary<GridPoint, bool> existingAnswers)
        {
            if (layout.ApproachDefense == null) return 0;
            int unanswered = 0;
            for (int x = 0; x < input.Terrain.Width; x += 2)
                for (int z = 0; z < input.Terrain.Height; z += 2)
                {
                    GridPoint cell = new GridPoint(x, z);
                    if (!layout.ApproachDefense.AttackerCanReach(cell) || Distance(origin, cell) > 44.9 ||
                        !P4BFireLane.CanEngage(cell, origin, 0, 45, opaque)) continue;
                    if (!existingAnswers.TryGetValue(cell, out bool answered))
                    {
                        answered = staffedPosts.Any(post => Distance(post, cell) <= 44.9 &&
                            P4BFireLane.CanEngage(post, cell, 0, 45, opaque));
                        if (!answered)
                            answered = previous.Any(g =>
                            {
                                double distance = Distance(g.Origin, cell);
                                if (distance < g.MinimumRange || distance > g.MaximumRange) return false;
                                return P4BFireLane.CanEngage(g.Origin, cell,
                                    (int)Math.Ceiling(g.MinimumRange), (int)Math.Floor(g.MaximumRange), opaque);
                            });
                        existingAnswers.Add(cell, answered);
                    }
                    if (!answered && candidate.DefName == "Turret_MiniTurret" &&
                        Distance(origin, cell) <= candidate.MaximumRange)
                        answered = P4BFireLane.CanEngage(origin, cell, 0,
                            (int)Math.Floor(candidate.MaximumRange), opaque);
                    if (!answered) unanswered++;
                }
            return unanswered;
        }

        // Core 1.6 sniper rifles have 44.9 range. The check is geometric: it
        // assumes a sniper is assigned to this post and is verified separately
        // against the realized equipment and P3 assignment.
        internal static void SniperGuardCoverage(P4BApproachDefensePlan defense, P4BTurretPlan gun,
            HashSet<GridPoint> opaque, out int threats, out int answered, out GridPoint? guardPost)
        {
            threats = 0; answered = 0; guardPost = null;
            P4BAttackSide side = defense.Sides.FirstOrDefault(s => s.Id == gun.Side && s.Exposed);
            if (side == null) return;
            HashSet<GridPoint> incoming = new HashSet<GridPoint>(opaque);
            incoming.ExceptWith(gun.Footprint);
            List<GridPoint> threatCells = side.Samples.Where(sample =>
                P4BFireLane.CanEngage(sample, gun.Origin, 0, 44, incoming)).ToList();
            threats = threatCells.Count;
            foreach (GridPoint post in side.Positions.Where(p =>
                gun.Footprint.Min(c => Distance(p, c)) <= 14 &&
                side.Samples.Count(sample => P4BFireLane.CanEngage(p, sample, 8, 44, opaque)) >= 4))
            {
                int shots = threatCells.Count(sample => P4BFireLane.CanEngage(post, sample, 0, 44, opaque));
                if (shots <= answered) continue;
                answered = shots; guardPost = post;
            }
        }

        internal static IReadOnlyCollection<GridPoint> SniperGuardOptions(P4BApproachDefensePlan defense,
            P4BTurretPlan gun, HashSet<GridPoint> opaque)
        {
            P4BAttackSide side = defense?.Sides.FirstOrDefault(s => s.Id == gun.Side && s.Exposed);
            if (side == null) return new GridPoint[0];
            HashSet<GridPoint> incoming = new HashSet<GridPoint>(opaque);
            incoming.ExceptWith(gun.Footprint);
            List<GridPoint> threats = side.Samples.Where(sample =>
                P4BFireLane.CanEngage(sample, gun.Origin, 0, 44, incoming)).ToList();
            if (threats.Count == 0) return new GridPoint[0];
            return side.Positions.Where(p => gun.Footprint.Min(c => Distance(p, c)) <= 14 &&
                side.Samples.Count(sample => P4BFireLane.CanEngage(p, sample, 8, 44, opaque)) >= 4 &&
                threats.Count(sample => P4BFireLane.CanEngage(p, sample, 0, 44, opaque)) * 10 >=
                    threats.Count * 9).Distinct().OrderBy(p => p).ToList();
        }

        private static IReadOnlyList<string> ValidateGroup(ProceduralLayoutInput input, ProceduralLayout layout,
            P4BTurretPlan turret, IEnumerable<GridPoint> additionalBlocked, IEnumerable<P4BTurretPlan> otherGuns)
        {
            List<string> errors = new List<string>();
            if (input == null || layout == null || turret == null ||
                layout.FieldWall == null && input.QuestContract == null)
                return new[] { "missing_plan" };
            // A quest camp has no field: its guns carry their access gate and side.
            P4BFieldWallPlan fieldWall = layout.FieldWall;
            if (fieldWall != null && !fieldWall.InnerGate.HasValue) return new[] { "missing_inner_gate" };
            if (fieldWall == null && (!turret.AccessGate.HasValue || !turret.Outward.HasValue))
                return new[] { "missing_access_or_side" };
            if (!input.Catalog.TryGet(turret.DefName, out CatalogDef def) ||
                !input.Catalog.IsLegal(def, input.Equipment.Kit.Stage) || !def.AutonomousTurret ||
                def.FuelCapacity <= 0 || def.FuelMultiplier <= 0 || def.FuelPerShot <= 0 ||
                def.Fuel.Count != 1 || def.Fuel[0] != turret.FuelDefName ||
                !input.Catalog.TryGet(turret.FuelDefName, out CatalogDef fuel) ||
                !input.Catalog.IsLegal(fuel, input.Equipment.Kit.Stage))
                return new[] { "illegal_def_or_fuel" };
            List<GridPoint> footprint = Enumerable.Range(0, def.SizeX).SelectMany(x =>
                Enumerable.Range(0, def.SizeZ).Select(z =>
                    new GridPoint(turret.Origin.X + x, turret.Origin.Z + z))).ToList();
            HashSet<GridPoint> occupied = new HashSet<GridPoint>(layout.Enclosure
                .Concat(layout.Rooms.SelectMany(r => r.Bounds.Points()))
                .Concat(layout.PowerYards.SelectMany(y => y.Cells))
                .Concat(layout.AllFixtures.SelectMany(f => f.Cells))
                .Concat(layout.OuterSections.SelectMany(s => s.ServiceLane(layout.Gates[s.GateIndex])))
                .Concat(FieldContent(fieldWall)));
            occupied.UnionWith(layout.ApproachDefense?.CoverCells ?? new GridPoint[0]);
            occupied.UnionWith(layout.ApproachDefense?.AllPosts ?? new GridPoint[0]);
            occupied.UnionWith(layout.ExtraObstacles);
            occupied.UnionWith(additionalBlocked ?? Enumerable.Empty<GridPoint>());
            HashSet<GridPoint> opaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
            opaque.UnionWith(otherGuns.SelectMany(t => t.Footprint));
            opaque.UnionWith(turret.Support?.Footprint ?? Enumerable.Empty<GridPoint>());
            occupied.UnionWith(opaque.Except(P4BApproachDefensePlanner.AccessGates(layout))
                .Where(c => fieldWall == null || !c.Equals(fieldWall.InnerGate.Value)));
            GridPoint accessGate = turret.AccessGate ?? fieldWall.OuterGate;
            if (!P4BApproachDefensePlanner.AccessGates(layout).Contains(accessGate)) errors.Add("access_gate");
            if (!footprint.OrderBy(c => c).SequenceEqual(turret.Footprint) || footprint.Any(c =>
                occupied.Contains(c) || opaque.Contains(c) || Arrival(layout).Contains(c) ||
                (layout.ApproachDefense?.Posts.Any(p => p.Route.Contains(c)) ?? false) ||
                !Buildable(input.Terrain, c))) errors.Add("footprint");
            if (!footprint.Any(c => Manhattan(c, turret.ServiceCell) == 1) ||
                occupied.Contains(turret.ServiceCell) || !Walkable(input.Terrain, turret.ServiceCell))
                errors.Add("service_cell");
            // A later group may change the shortest path, but the advertised path must stay legal.
            HashSet<GridPoint> nestCover = new HashSet<GridPoint>(layout.ApproachDefense?.CoverCells ??
                Enumerable.Empty<GridPoint>());
            if (layout.ApproachDefense != null)
                nestCover.UnionWith(layout.ApproachDefense.Posts.SelectMany(p => p.CoverCells));
            HashSet<GridPoint> routeOccupied = new HashSet<GridPoint>(occupied);
            routeOccupied.ExceptWith(nestCover);
            if (turret.ServiceRoute.Count < 2 || turret.ServiceRoute.Count > 160 ||
                !TurretRouteStarts(layout, accessGate, turret.Side).Contains(turret.ServiceRoute[0]) ||
                !turret.ServiceRoute.Last().Equals(turret.ServiceCell) ||
                turret.ServiceRoute.Skip(1).Any(c => routeOccupied.Contains(c) || footprint.Contains(c) || !Walkable(input.Terrain, c)) ||
                turret.ServiceRoute.Zip(turret.ServiceRoute.Skip(1), (a, b) => Manhattan(a, b)).Any(n => n != 1))
                errors.Add("service_route");
            GridPoint outward = turret.Outward ?? new GridPoint(Math.Sign(fieldWall.OuterGate.X - fieldWall.InnerGate.Value.X),
                Math.Sign(fieldWall.OuterGate.Z - fieldWall.InnerGate.Value.Z));
            if (!Cardinal.Contains(outward) || (layout.ApproachDefense != null && turret.Side.Length > 0 &&
                !layout.ApproachDefense.Sides.Any(s => s.Id == turret.Side && s.Exposed && s.Outward.Equals(outward) &&
                    s.Samples.Count(c => P4BFireLane.CanEngage(turret.Origin, c,
                        (int)Math.Ceiling(def.MinimumRange), (int)Math.Floor(def.MaximumRange), opaque)) >= 4)))
                errors.Add("attack_side");
            List<GridPoint> front = footprint.Where(c => !footprint.Contains(new GridPoint(
                c.X + outward.X, c.Z + outward.Z))).Select(c => new GridPoint(
                c.X + outward.X, c.Z + outward.Z)).Distinct().ToList();
            GridPoint tangent = new GridPoint(-outward.Z, outward.X);
            List<GridPoint> flanks = new[] { -1, 1 }.SelectMany(sign => footprint.Where(c =>
                !footprint.Contains(new GridPoint(c.X + sign * tangent.X, c.Z + sign * tangent.Z)))
                .Select(c => new GridPoint(c.X + sign * tangent.X, c.Z + sign * tangent.Z)))
                .Distinct().ToList();
            if (front.Any(c => !turret.CoverCells.Contains(c)) ||
                flanks.Any(c => !turret.CoverCells.Contains(c))) errors.Add("adjacent_flank_cover");
            if (front.Any(c => !turret.CoverCells.Contains(c)) ||
                turret.CoverCells.Any(c => occupied.Contains(c) || opaque.Contains(c) ||
                    footprint.Contains(c) || turret.ServiceRoute.Contains(c) ||
                    Arrival(layout).Contains(c) || !Buildable(input.Terrain, c) ||
                    !footprint.Any(f => Manhattan(f, c) == 1))) errors.Add("adjacent_cover");
            double blastClearance = def.ExplosionRadius + 2;
            if (BlastClearanceAssets(layout)
                .Any(c => footprint.Any(f => Distance(f, c) < blastClearance))) errors.Add("blast_clearance");
            int usefulTargets = ApproachTargets(input, layout).Count(c => P4BFireLane.CanEngage(turret.Origin, c,
                (int)Math.Ceiling(def.MinimumRange), (int)Math.Floor(def.MaximumRange), opaque));
            if (usefulTargets < 4) errors.Add("fire_lane");
            if (layout.Power == null ||
                layout.Power.Generators * layout.Power.GeneratorWatts + layout.Power.FirmWatts - layout.Power.CriticalWatts < turret.TotalWatts ||
                layout.Power.Generators * layout.Power.GeneratorWatts + layout.Power.FirmWatts -
                    layout.Power.CriticalWatts - layout.Power.NonCriticalWatts < turret.TotalWatts ||
                turret.Watts != def.BasePowerConsumption) errors.Add("power_budget");
            if (turret.LoadedFuelUnits != (int)Math.Ceiling(def.FuelCapacity * def.InitialFuelPercent / def.FuelMultiplier) ||
                turret.ReserveFuelUnits != ReserveFuel(input, def) ||
                turret.ImportValue != ImportValue(input.Catalog, def, fuel,
                    turret.LoadedFuelUnits + turret.ReserveFuelUnits) +
                    CoverImportValue(input, P4BCoverNest.CoverDefName(input), turret.CoverCells.Count) +
                    (turret.Support?.ImportValue ?? 0) + turret.AdditionalGroups.Sum(g => g.ImportValue))
                errors.Add("finite_stock_or_import");
            if (turret.MinimumRange != def.MinimumRange || turret.MaximumRange != def.MaximumRange ||
                turret.ExplosionRadius != def.ExplosionRadius) errors.Add("resolved_facts");
            if (def.MinimumRange > 0)
            {
                if (turret.Support == null || turret.Support.DefName != "Turret_MiniTurret")
                    errors.Add("blind_zone_without_small_turret");
                else
                {
                    errors.AddRange(ValidateGroup(input, layout, turret.Support,
                        (additionalBlocked ?? Enumerable.Empty<GridPoint>()).Concat(turret.Footprint).Concat(turret.CoverCells),
                        otherGuns.Concat(new[] { turret }))
                        .Select(e => "support_" + e));
                    if (turret.Footprint.Intersect(turret.Support.Footprint).Any() ||
                        turret.ServiceRoute.Intersect(turret.Support.Footprint).Any() ||
                        turret.Support.ServiceRoute.Intersect(turret.Footprint).Any() ||
                        turret.CoverCells.Intersect(turret.Support.Footprint.Concat(turret.Support.CoverCells)
                            .Concat(turret.Support.ServiceRoute)).Any() ||
                        turret.Support.CoverCells.Intersect(turret.Footprint.Concat(turret.ServiceRoute)).Any() ||
                        turret.Footprint.Any(a => turret.Support.Footprint.Any(b =>
                            Distance(a, b) < Math.Max(turret.ExplosionRadius, turret.Support.ExplosionRadius) + 2)))
                        errors.Add("support_collision_or_blast");
                    if (!CloseSupport(input.Terrain, layout, turret.Footprint,
                        turret.MinimumRange, turret.Support.Footprint, turret.Support.MaximumRange, opaque))
                        errors.Add("blind_zone_not_cross_covered");
                }
            }
            else if (turret.Support != null) errors.Add("unexpected_support");
            return errors;
        }

        public static P4BTurretPlan Plan(ProceduralLayoutInput input, ProceduralLayout layout,
            P4BFieldWallPlan fieldWall, IEnumerable<GridPoint> occupied, out string diagnostic)
        {
            if (input == null || layout == null || fieldWall == null) throw new ArgumentNullException();
            SettlementLoadout garrison = LayoutLoadoutModel.Build(input, layout);
            if (!garrison.Success)
            { diagnostic = "unplaced: garrison_unavailable"; return null; }
            List<P4BTurretPlan> groups = new List<P4BTurretPlan>();
            List<string> decisions = new List<string>();
            CellSet baseOpaque = new CellSet(input.Terrain);
            foreach (GridPoint cell in P4BApproachDefensePlanner.Opaque(layout, input.Terrain))
                baseOpaque.Add(cell);
            CellSet baseBlocked = new CellSet(input.Terrain);
            if (occupied != null)
                foreach (GridPoint cell in occupied) baseBlocked.Add(cell);
            baseBlocked.UnionWith(fieldWall.Walls);
            baseBlocked.UnionWith(fieldWall.ExteriorPosts);
            baseBlocked.UnionWith(fieldWall.Fields);
            if (layout.ApproachDefense != null)
            {
                foreach (GridPoint cell in layout.ApproachDefense.CoverCells) baseBlocked.Add(cell);
                foreach (GridPoint cell in layout.ApproachDefense.AllPosts) baseBlocked.Add(cell);
            }
            HashSet<GridPoint> powerLimitedGates = new HashSet<GridPoint>();
            bool powerHeadroomStopped = false;
            int minimumTurretWatts = input.Catalog.TryGet("Turret_MiniTurret", out CatalogDef minimumTurret)
                ? minimumTurret.BasePowerConsumption : int.MaxValue;
            List<P4BPost> posts = (layout.ApproachDefense?.Posts ?? new P4BPost[0]).ToList();
            var crossings = posts.GroupBy(p => p.AccessGate)
                .Select(g =>
                {
                    P4BAttackSide side = layout.ApproachDefense.Sides.Where(s => g.Any(p => p.Side == s.Id))
                        .OrderByDescending(s => s.Samples.Count).ThenBy(s => s.Id, StringComparer.Ordinal).First();
                    return new { AccessGate = g.Key, Outward = side.Outward, Side = side.Id,
                        Exposure = side.Samples.Count, Posts = g.Count() };
                })
                .OrderByDescending(c => c.Exposure).ThenBy(c => c.AccessGate).ThenBy(c => c.Outward).ToList();
            if (crossings.Count == 0 && layout.ApproachDefense == null)
                crossings.Add(new { AccessGate = fieldWall.OuterGate, Outward = new GridPoint(
                    Math.Sign(fieldWall.OuterGate.X - fieldWall.InnerGate.Value.X),
                    Math.Sign(fieldWall.OuterGate.Z - fieldWall.InnerGate.Value.Z)), Side = "field", Exposure = 1, Posts = 1 });
            bool fullCountDefenseReserve = input.Requirements.DefenseReserveWatts >=
                OrdinarySettlementPopulation.EstimateDefenseReserveWatts(input.Requirements.Stage);
            P4BTurretPlan TryPlanGroup(string side, GridPoint? gate, bool miniOnly, bool heavyOnly,
                bool ignoreGainGate, int spacingLevel, out string reason)
            {
                if (powerHeadroomStopped)
                { reason = "power_headroom_stopped"; return null; }
                P4BTurretPlan result = PlanGroup(input, layout, fieldWall, baseBlocked, baseOpaque, groups, garrison,
                    side, gate, miniOnly, heavyOnly, ignoreGainGate, spacingLevel,
                    fullCountDefenseReserve, out reason);
                if (result == null && reason.IndexOf("critical_power_headroom", StringComparison.Ordinal) >= 0 &&
                    AvailableWatts(layout) - groups.SelectMany(g => g.All).Sum(g => g.Watts) < minimumTurretWatts)
                    powerHeadroomStopped = true;
                return result;
            }
            // Complete minimum batteries before any crossing receives a heavy or extra mini.
            for (int crossingIndex = 0; crossingIndex < crossings.Count; crossingIndex++)
            {
                var crossing = crossings[crossingIndex];
                int crossingStart = groups.Count;
                bool complete = false;
                for (int spacingLevel = 0; spacingLevel <= 2 && !complete; spacingLevel++)
                {
                    if (groups.Count > crossingStart)
                        groups.RemoveRange(crossingStart, groups.Count - crossingStart);
                    int count = 0;
                    while (count < 3)
                    {
                        P4BTurretPlan next = TryPlanGroup(crossing.Side, crossing.AccessGate,
                            true, false, true, spacingLevel, out string reason);
                        decisions.Add(crossing.AccessGate + "/" + crossing.Side +
                            ":spacingLevel=" + spacingLevel + ":" + reason);
                        if (next == null) break;
                        groups.Add(next); count++;
                    }
                    if (powerHeadroomStopped)
                    {
                        int placedBeforeRemoval = count;
                        groups.RemoveRange(crossingStart, groups.Count - crossingStart);
                        for (int i = crossingIndex; i < crossings.Count; i++)
                        {
                            powerLimitedGates.Add(crossings[i].AccessGate);
                            if (i > crossingIndex)
                                decisions.Add(crossings[i].AccessGate + "/" + crossings[i].Side +
                                    ":power_headroom_shortfall=3_unattempted");
                        }
                        decisions.Add(crossing.AccessGate + "/" + crossing.Side +
                            ":power_headroom_incomplete_battery_removed placed=" + placedBeforeRemoval +
                            "/3 final=0/3 shortfall=3");
                        break;
                    }
                    if (count >= 3)
                    {
                        complete = true;
                        decisions.Add(crossing.AccessGate + "/" + crossing.Side +
                            ":spacingFallbackLevel=" + spacingLevel + " battery=complete");
                    }
                    else
                    {
                        decisions.Add(crossing.AccessGate + "/" + crossing.Side +
                            ":spacingLevel=" + spacingLevel + " incomplete_battery_removed count=" + count + "/3");
                    }
                }
                if (!complete && !powerHeadroomStopped)
                {
                    if (groups.Count > crossingStart)
                        groups.RemoveRange(crossingStart, groups.Count - crossingStart);
                    decisions.Add(crossing.AccessGate + "/" + crossing.Side + ":incomplete_battery_removed");
                }
            }
            foreach (var crossing in crossings)
            {
                if (powerHeadroomStopped) break;
                int miniCount = groups.SelectMany(g => g.All).Count(g => g.AccessGate.HasValue &&
                    g.AccessGate.Value.Equals(crossing.AccessGate) && g.DefName == "Turret_MiniTurret");
                if (miniCount < 3) continue;
                for (int heavyIndex = 0; heavyIndex < 3 && miniCount < 6; heavyIndex++)
                {
                    P4BTurretPlan heavy = TryPlanGroup(crossing.Side, crossing.AccessGate,
                        false, true, false, 0, out string reason);
                    decisions.Add(crossing.AccessGate + "/" + crossing.Side + ":heavy:" + reason);
                    if (heavy == null) break;
                    groups.Add(heavy);
                    miniCount++;
                }
            }
            foreach (var crossing in crossings)
            {
                if (powerHeadroomStopped) break;
                int count = groups.SelectMany(g => g.All).Count(g => g.AccessGate.HasValue &&
                    g.AccessGate.Value.Equals(crossing.AccessGate) && g.DefName == "Turret_MiniTurret");
                while (count >= 3 && count < 6)
                {
                    P4BTurretPlan next = TryPlanGroup(crossing.Side, crossing.AccessGate,
                        true, false, false, 0, out string reason);
                    decisions.Add(crossing.AccessGate + "/" + crossing.Side + ":" + reason);
                    if (next == null) break;
                    groups.Add(next); count++;
                }
            }
            P4BTurretPlan network = Combine(input.Catalog, groups);
            while (network != null)
            {
                // The exposure floor belongs to the crossing whose proposal is being
                // validated. A separate crossing's old shortfall must not unwind this one.
                List<string> finalErrors = ValidateInternal(input, layout, network, null, garrison,
                    onlyGroup: groups[groups.Count - 1]).ToList();
                if (finalErrors.Count == 0)
                    finalErrors.AddRange(P4BApproachDefensePlanner.Validate(input,
                        layout.WithTurret(input, network)));
                if (finalErrors.Count == 0) break;
                GridPoint? removedGate = groups[groups.Count - 1].AccessGate;
                bool powerValidationFailure = finalErrors.Any(e => e.Contains("power_budget") ||
                    e.Contains("power_access") || e.Contains("power_generation") ||
                    e.Contains("power_critical") || e.Contains("power_battery"));
                decisions.Add((powerValidationFailure ? "power_validation_removed=" : "final_validation_removed=") +
                    removedGate + ":" + finalErrors[0]);
                if (powerValidationFailure)
                {
                    powerHeadroomStopped = true;
                    if (removedGate.HasValue) powerLimitedGates.Add(removedGate.Value);
                }
                groups.RemoveAt(groups.Count - 1);
                if (removedGate.HasValue && groups.SelectMany(g => g.All).Count(g =>
                    g.AccessGate.HasValue && g.AccessGate.Value.Equals(removedGate.Value) &&
                    g.DefName == "Turret_MiniTurret") < 3 && !powerValidationFailure)
                {
                    groups.RemoveAll(g => g.AccessGate.HasValue && g.AccessGate.Value.Equals(removedGate.Value));
                    decisions.Add("final_validation_removed_incomplete_battery=" + removedGate);
                }
                network = Combine(input.Catalog, groups);
            }
            HashSet<GridPoint> finalOpaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
            finalOpaque.UnionWith(network?.All.SelectMany(t => t.Footprint) ?? Enumerable.Empty<GridPoint>());
            List<int> coverage = ApproachTargets(input, layout).Select(c => network?.All.Count(t =>
                CanShoot(t, c, finalOpaque)) ?? 0).ToList();
            P4BStaffingAudit staffed = network == null ? null : P4BStaffing.Audit(input, layout, network, garrison);
            int powerShortfallMinis = powerLimitedGates.Sum(gate => Math.Max(0, 3 -
                groups.SelectMany(g => g.All).Count(g => g.DefName == "Turret_MiniTurret" &&
                    g.AccessGate.HasValue && g.AccessGate.Value.Equals(gate))));
            int powerShortfallCrossings = powerLimitedGates.Count(gate =>
                groups.SelectMany(g => g.All).Count(g => g.DefName == "Turret_MiniTurret" &&
                    g.AccessGate.HasValue && g.AccessGate.Value.Equals(gate)) < 3);
            diagnostic = (network == null ? "unplaced" : "accepted") + " groups=" + groups.Count +
                " turrets=" + (network?.All.Count() ?? 0) + " watts=" + (network?.TotalWatts ?? 0) +
                " value=" + (network?.ImportValue ?? 0) + " uncovered=" + coverage.Count(n => n == 0) +
                " single=" + coverage.Count(n => n == 1) + " overlapping=" + coverage.Count(n => n >= 2) +
                " staffedGuards=" + (staffed?.GuardedGuns ?? 0) + "/" + (staffed?.Guns ?? 0) +
                " staffedClose=" + (staffed?.CloseSupportedGuns ?? 0) + "/" + (staffed?.Guns ?? 0) +
                " sniperPosts=" + (staffed?.SniperPosts.Count ?? 0) +
                " linePosts=" + (staffed?.LinePosts.Count ?? 0) +
                " mortarCrew=" + (staffed?.MortarCrew ?? 0) + " mobileReserve=" + (staffed?.MobileReserve ?? 0) +
                " powerHeadroomStopped=" + powerHeadroomStopped +
                " powerLimitedCrossings=" + powerShortfallCrossings +
                " batteryMiniShortfall=" + powerShortfallMinis +
                " decisions=[" + string.Join(";", decisions) + "]";
            return network;
        }

        private static P4BTurretPlan PlanGroup(ProceduralLayoutInput input, ProceduralLayout layout,
            P4BFieldWallPlan fieldWall, CellSet baseBlocked, CellSet baseOpaque, List<P4BTurretPlan> groups,
            SettlementLoadout garrison, string onlySide, GridPoint? onlyGate, bool miniOnly, bool heavyOnly,
            bool ignoreGainGate, int spacingLevel, bool fullCountDefenseReserve,
            out string diagnostic)
        {
            if (input == null || layout == null || fieldWall == null) throw new ArgumentNullException();
            if (!fieldWall.InnerGate.HasValue)
            { diagnostic = "field_gate_unavailable"; return null; }
            if (layout.Power == null || input.Equipment.Kit.Stage.Level < SettlementTechStageLevel.IndustrialMid)
            { diagnostic = "stage_or_power_unavailable"; return null; }
            CellSet blocked = new CellSet(input.Terrain);
            blocked.CopyFrom(baseBlocked);
            List<P4BTurretPlan> previous = groups.SelectMany(g => g.All).ToList();
            List<GridPoint> previousFootprintCells = previous.SelectMany(t => t.Footprint).ToList();
            CellSet previousServiceRoutes = new CellSet(input.Terrain);
            foreach (P4BTurretPlan turret in previous) previousServiceRoutes.UnionWith(turret.ServiceRoute);
            CellSet postRoutes = new CellSet(input.Terrain);
            if (layout.ApproachDefense != null)
                foreach (P4BPost post in layout.ApproachDefense.Posts) postRoutes.UnionWith(post.Route);
            CellSet arrivalCells = new CellSet(input.Terrain);
            foreach (GridPoint cell in Arrival(layout)) arrivalCells.Add(cell);
            Dictionary<GridPoint, List<P4BTurretPlan>> previousTurretsByFootprintCell = new Dictionary<GridPoint, List<P4BTurretPlan>>();
            foreach (P4BTurretPlan turret in previous)
                foreach (GridPoint cell in turret.Footprint)
                {
                    if (!previousTurretsByFootprintCell.TryGetValue(cell, out List<P4BTurretPlan> atCell))
                        previousTurretsByFootprintCell.Add(cell, atCell = new List<P4BTurretPlan>());
                    atCell.Add(turret);
                }
            int spentValue = groups.Sum(g => g.ImportValue), spentWatts = previous.Sum(g => g.Watts);
            int baseValue = BaseMaterialValue(input, layout);
            foreach (P4BTurretPlan turret in previous)
            {
                blocked.UnionWith(turret.Footprint);
                blocked.UnionWith(turret.CoverCells);
            }
            HashSet<GridPoint> nestCover = new HashSet<GridPoint>(layout.ApproachDefense?.CoverCells ??
                Enumerable.Empty<GridPoint>());
            if (layout.ApproachDefense != null)
                nestCover.UnionWith(layout.ApproachDefense.Posts.SelectMany(p => p.CoverCells));
            CellSet opaque = new CellSet(input.Terrain);
            opaque.CopyFrom(baseOpaque);
            foreach (P4BTurretPlan turret in previous) opaque.UnionWith(turret.Footprint);
            // A service route may pass a door, but cannot pass through either
            // perimeter's wall even if the caller omitted wall cells from its
            // occupancy set (as the joint-layout packer deliberately does).
            IReadOnlyList<GridPoint> accessGates = P4BApproachDefensePlanner.AccessGates(layout);
            List<GridPoint> opaqueCells = new List<GridPoint>();
            opaque.CopyTo(opaqueCells);
            for (int i = 0; i < opaqueCells.Count; i++)
                if (!accessGates.Contains(opaqueCells[i]) && !opaqueCells[i].Equals(fieldWall.InnerGate.Value))
                    blocked.Add(opaqueCells[i]);
            HashSet<GridPoint> legacyBlocked = blocked.ToHashSet();
            HashSet<GridPoint> legacyOpaque = opaque.ToHashSet();
            GridPoint inner = fieldWall.InnerGate.Value, outer = fieldWall.OuterGate;
            GridPoint outward = new GridPoint(Math.Sign(outer.X - inner.X), Math.Sign(outer.Z - inner.Z));
            GridPoint tangent = new GridPoint(-outward.Z, outward.X);
            List<Frame> frames = new List<Frame>();
            string fieldSide = outward.X != 0 ? (outward.X < 0 ? "west" : "east") : (outward.Z < 0 ? "south" : "north");
            if (layout.ApproachDefense == null)
                frames.Add(new Frame { Anchor = outer, Gate = outer, Outward = outward, Side = fieldSide });
            if (layout.ApproachDefense != null)
                // One frame per post facing a side: its slots stand side by side,
                // so further slots would only repeat the same turret search.
                frames.AddRange(layout.ApproachDefense.Sides.Where(s => s.Exposed &&
                    (onlySide == null || s.Id == onlySide)).SelectMany(s =>
                    layout.ApproachDefense.Posts.Where(p => !onlyGate.HasValue || p.AccessGate.Equals(onlyGate.Value))
                    .Select(p => new { Post = p, Slot = p.Slots.Cast<GridPoint?>()
                        .FirstOrDefault(c => s.PositionSet.Contains(c.Value)) })
                    .Where(x => x.Slot.HasValue).Select(x => new Frame { Anchor = x.Slot.Value,
                        Gate = x.Post.AccessGate, Outward = s.Outward, Side = s.Id })));
            // T5g between-nests candidates were rejected and their full variant is preserved
            // in .runtime/investigations/turret-batteries/between-nests.patch.
            List<GridPoint> targets = ApproachTargets(input, layout).ToList();
            HashSet<GridPoint> opaqueForCoverage = legacyOpaque;
            Dictionary<GridPoint, int> coverage = targets.ToDictionary(c => c, c => previous.Count(t =>
                CanShoot(t, c, opaqueForCoverage)));
            P4BStaffingAudit currentStaffing = groups.Count == 0 ? null :
                P4BStaffing.Audit(input, layout, Combine(input.Catalog, groups), garrison);
            List<GridPoint> staffedPosts = currentStaffing == null ? new List<GridPoint>() :
                currentStaffing.SniperPosts.Concat(currentStaffing.LinePosts).ToList();
            Dictionary<GridPoint, int> exposureScores = new Dictionary<GridPoint, int>();
            Dictionary<GridPoint, bool> existingExposureAnswers = new Dictionary<GridPoint, bool>();
            Dictionary<string, int> rejected = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, int> hardExposureBlockers = new Dictionary<string, int>(StringComparer.Ordinal);
            List<string> turretCoverRejections = new List<string>();
            List<string> serviceRouteFailures = new List<string>();
            int betweenNestsFrontageRejections = 0;
            HashSet<string> nearbyPawnSupportRejectedSides = new HashSet<string>(StringComparer.Ordinal);
            int betweenGenerated = 0, betweenBand = 0, betweenRifleLos = 0, betweenFireLane = 0, betweenGain = 0,
                betweenSide = 0, betweenRanked = 0, betweenPlacementChecks = 0, betweenEndpoints = 0,
                betweenReachableRoutes = 0;
            int betweenPairsFound = frames.Where(f => f.BetweenNests && f.FirstNest.HasValue && f.PartnerNest.HasValue)
                .Select(f => f.Side + "/" + f.FirstNest.Value + "/" + f.PartnerNest.Value).Distinct().Count();
            List<BetweenCandidateDiagnostic> betweenCandidates = new List<BetweenCandidateDiagnostic>();
            Dictionary<string, BetweenCandidateDiagnostic> betweenCandidatesByKey =
                new Dictionary<string, BetweenCandidateDiagnostic>(StringComparer.Ordinal);
            BetweenCandidateDiagnostic currentBetweenPosition = null;
            GridPoint? currentBetweenBlocker = null;
            bool currentBetweenCandidate = false;
            Dictionary<string, int> betweenRejected = new Dictionary<string, int>(StringComparer.Ordinal);
            input.Catalog.TryGet("Turret_Autocannon", out CatalogDef autocannon);
            input.Catalog.TryGet("Turret_Sniper", out CatalogDef slug);
            if (onlySide != null && layout.ApproachDefense != null)
                targets = layout.ApproachDefense.Sides.Where(s => s.Id == onlySide)
                    .SelectMany(s => s.Samples).Distinct().OrderBy(c => c).ToList();
            bool crossingHasStaffedSniper = currentStaffing != null && currentStaffing.SniperPosts.Any(post =>
                layout.ApproachDefense?.Sides.Where(s => s.Id == onlySide).Any(s => s.Positions.Any(p =>
                    Distance(p, post) <= 14 && s.Samples.Count(target =>
                        P4BFireLane.CanEngage(post, target, 8, 44, opaque)) >= 4)) == true);
            IReadOnlyList<string> order = miniOnly ? new[] { "Turret_MiniTurret" } : heavyOnly
                ? (crossingHasStaffedSniper ? new[] { "Turret_Autocannon", "Turret_Sniper" } :
                    new[] { "Turret_Sniper", "Turret_Autocannon" }) : HeavyWeaponOrder(targets, frames.Select(f => f.Anchor),
                previous.Select(g => g.DefName), autocannon?.MaximumRange ?? 0, slug?.MaximumRange ?? 0);
            foreach (string name in order)
            {
                currentBetweenCandidate = false;
                int existingAtCrossing = previous.Count(g => g.AccessGate.HasValue && onlyGate.HasValue &&
                    g.AccessGate.Value.Equals(onlyGate.Value) && g.DefName == name);
                if (heavyOnly && existingAtCrossing >= (name == "Turret_Autocannon" ? 2 : 1)) continue;
                if (!input.Catalog.TryGet(name, out CatalogDef def) ||
                    !input.Catalog.IsLegal(def, input.Equipment.Kit.Stage)) continue;
                if (!def.AutonomousTurret || def.Fuel.Count != 1 || def.FuelCapacity <= 0 ||
                    def.FuelMultiplier <= 0 || def.FuelPerShot <= 0)
                { Reject("incomplete_turret_facts"); continue; }
                if (!input.Catalog.TryGet(def.Fuel[0], out CatalogDef fuel) ||
                    !input.Catalog.IsLegal(fuel, input.Equipment.Kit.Stage))
                { Reject("fuel_illegal"); continue; }
                int availableWatts = AvailableWatts(layout) - spentWatts;
                int totalWatts = availableWatts;
                if (availableWatts < def.BasePowerConsumption || totalWatts < def.BasePowerConsumption)
                { Reject("critical_power_headroom"); continue; }
                int loadedUnits = (int)Math.Ceiling(def.FuelCapacity * def.InitialFuelPercent / def.FuelMultiplier);
                int reserveUnits = P5Readiness.ReloadReserve(def.FuelCapacity, def.InitialFuelPercent, def.FuelPerShot,
                    def.FuelMultiplier, (int)Math.Ceiling(def.FuelCapacity / def.FuelMultiplier));
                int value = ImportValue(input.Catalog, def, fuel, loadedUnits + reserveUnits);
                if (value == int.MaxValue) { Reject("construction_cost_unresolved"); continue; }
                if (baseValue + spentValue + value > input.Requirements.ImportValueLimit)
                { Reject("import_budget"); continue; }
                // Score all eligible fronts before route/support construction.
                // A deterministic bounded candidate set replaces the old
                // field-gate-first rule. No gun is needed on every side.
                int standardRifleRange = StandardRifleRange(input);
                var generatedCandidates = frames.SelectMany(frame => (frame.BetweenNests ? new[] { 0 } : new[] { -1, 1 }).SelectMany(side =>
                    (frame.BetweenNests ? new[] { 2, 3, 4 } : new[] { 1, 2, 3, 4, 5, 7, 9, 11, 13, 15 }).SelectMany(forward =>
                        (frame.BetweenNests ? new[] { 0 } : new[] { 0, 2, 4, 6, 8, 10, 12, 14 }).Select(lateral => new
                    { Frame = frame, Side = side, Origin = At(frame.Anchor, frame.Outward,
                        new GridPoint(-frame.Outward.Z, frame.Outward.X), forward, side * lateral) }))))
                    .ToList();
                List<BetweenCandidateDiagnostic> generatedBetween = new List<BetweenCandidateDiagnostic>();
                foreach (var c in generatedCandidates.Where(c => c.Frame.BetweenNests))
                {
                    string key = BetweenCandidateKey(name, c.Frame, c.Origin);
                    if (!betweenCandidatesByKey.TryGetValue(key, out BetweenCandidateDiagnostic record))
                    {
                        record = new BetweenCandidateDiagnostic { Type = name, Gate = c.Frame.Gate,
                            NestA = c.Frame.FirstNest.Value, NestB = c.Frame.PartnerNest.Value,
                            StretchCell = c.Frame.Anchor, Origin = c.Origin, Stage = "generated" };
                        betweenCandidatesByKey.Add(key, record); betweenCandidates.Add(record);
                    }
                    generatedBetween.Add(record);
                }
                betweenGenerated += generatedCandidates.Where(c => c.Frame.BetweenNests)
                    .Select(c => c.Frame.Gate + "/" + c.Origin).Distinct().Count();
                var distanceCandidates = generatedCandidates.Where(c => c.Frame.BetweenNests
                    ? c.Frame.FirstNest.HasValue && c.Frame.PartnerNest.HasValue &&
                        (Distance(c.Frame.FirstNest.Value, c.Origin) <= 14 ||
                         Distance(c.Frame.PartnerNest.Value, c.Origin) <= 14)
                    : Distance(c.Frame.Anchor, c.Origin) >= def.ExplosionRadius + 2 &&
                        Distance(c.Frame.Anchor, c.Origin) <= 14).ToList();
                betweenBand += distanceCandidates.Count(c => c.Frame.BetweenNests);
                HashSet<string> distanceKeys = new HashSet<string>(distanceCandidates.Where(c => c.Frame.BetweenNests)
                    .Select(c => BetweenCandidateKey(name, c.Frame, c.Origin)), StringComparer.Ordinal);
                foreach (var c in generatedCandidates.Where(c => c.Frame.BetweenNests))
                {
                    BetweenCandidateDiagnostic record = GetBetweenCandidate(name, c.Frame, c.Origin);
                    record.Stage = "within14_one_nest";
                    if (!distanceKeys.Contains(BetweenCandidateKey(name, c.Frame, c.Origin)))
                        record.Rejection = "outside_14_of_either_nest";
                }
                var geometryCandidates = distanceCandidates.Where(c => !c.Frame.BetweenNests ||
                    BetweenNestRifleSupport(layout, c.Frame, c.Origin, standardRifleRange, legacyOpaque)).ToList();
                betweenRifleLos += geometryCandidates.Count(c => c.Frame.BetweenNests);
                HashSet<string> rifleLineKeys = new HashSet<string>(geometryCandidates.Where(c => c.Frame.BetweenNests)
                    .Select(c => BetweenCandidateKey(name, c.Frame, c.Origin)), StringComparer.Ordinal);
                foreach (var c in distanceCandidates.Where(c => c.Frame.BetweenNests))
                {
                    BetweenCandidateDiagnostic record = GetBetweenCandidate(name, c.Frame, c.Origin);
                    record.Stage = "rifle_range_line_of_fire";
                    if (!rifleLineKeys.Contains(BetweenCandidateKey(name, c.Frame, c.Origin)))
                        record.Rejection = "other_nest_outside_rifle_range_or_no_nearby_ground_los";
                }
                var scoredCandidates = geometryCandidates.Select(c => new { c.Frame, c.Side, c.Origin,
                        Gain = targets.Where(t => coverage[t] < 2 && P4BFireLane.CanEngage(c.Origin, t,
                            (int)Math.Ceiling(def.MinimumRange), (int)Math.Floor(def.MaximumRange), opaque))
                            .Sum(t => coverage[t] == 0 ? 4 : 1),
                        NewFront = !groups.Any(g => g.Side == c.Frame.Side), Shots = targets.Count(t =>
                        P4BFireLane.CanEngage(c.Origin, t, (int)Math.Ceiling(def.MinimumRange),
                            (int)Math.Floor(def.MaximumRange), opaque)),
                        MutualShots = targets.Count(t => P4BFireLane.CanEngage(c.Origin, t,
                            (int)Math.Ceiling(def.MinimumRange), (int)Math.Floor(def.MaximumRange), opaque) &&
                            (coverage[t] > 0 || staffedPosts.Any(p =>
                                P4BFireLane.CanEngage(p, t, 0, 25, opaque)))),
                        FrontShots = layout.ApproachDefense == null ? 4 : layout.ApproachDefense.Sides
                            .Where(s => s.Id == c.Frame.Side).SelectMany(s => s.Samples).Count(t =>
                                P4BFireLane.CanEngage(c.Origin, t, (int)Math.Ceiling(def.MinimumRange),
                                    (int)Math.Floor(def.MaximumRange), opaque)),
                        FrontExposure = layout.ApproachDefense == null ? 0 : layout.ApproachDefense.Sides
                            .Where(s => s.Id == c.Frame.Side).Sum(s => s.Samples.Count),
                        FrontOpenGround = layout.ApproachDefense == null ? 0 : layout.ApproachDefense.Sides
                            .Where(s => s.Id == c.Frame.Side).Sum(s => s.OpenGroundArea),
                        Sides = layout.ApproachDefense == null ? 1 : layout.ApproachDefense.Sides.Count(s =>
                            s.Samples.Count(t => P4BFireLane.CanEngage(c.Origin, t,
                                (int)Math.Ceiling(def.MinimumRange), (int)Math.Floor(def.MaximumRange), opaque)) >= 4) })
                    .ToList();
                var fireCandidates = scoredCandidates.Where(c => c.Shots >= 4 && c.FrontShots >= 4).ToList();
                betweenFireLane += fireCandidates.Count(c => c.Frame.BetweenNests);
                HashSet<string> fireKeys = new HashSet<string>(fireCandidates.Where(c => c.Frame.BetweenNests)
                    .Select(c => BetweenCandidateKey(name, c.Frame, c.Origin)), StringComparer.Ordinal);
                foreach (var c in scoredCandidates.Where(c => c.Frame.BetweenNests && !fireKeys.Contains(
                    BetweenCandidateKey(name, c.Frame, c.Origin))))
                { BetweenCandidateDiagnostic rec = GetBetweenCandidate(name, c.Frame, c.Origin); rec.Stage = "fireLane"; rec.Rejection = "fire_lane"; }
                foreach (var c in fireCandidates.Where(c => c.Frame.BetweenNests))
                    GetBetweenCandidate(name, c.Frame, c.Origin).Stage = "fireLane";
                var gainCandidates = (ignoreGainGate ? fireCandidates : fireCandidates.Where(c => c.Gain >= 4)).ToList();
                betweenGain += gainCandidates.Count(c => c.Frame.BetweenNests);
                HashSet<string> gainKeys = new HashSet<string>(gainCandidates.Where(c => c.Frame.BetweenNests)
                    .Select(c => BetweenCandidateKey(name, c.Frame, c.Origin)), StringComparer.Ordinal);
                if (!ignoreGainGate)
                    foreach (var c in fireCandidates.Where(c => c.Frame.BetweenNests && !gainKeys.Contains(
                        BetweenCandidateKey(name, c.Frame, c.Origin))))
                    { BetweenCandidateDiagnostic rec = GetBetweenCandidate(name, c.Frame, c.Origin); rec.Stage = "gain"; rec.Rejection = "gain_gate"; }
                foreach (var c in gainCandidates.Where(c => c.Frame.BetweenNests))
                    GetBetweenCandidate(name, c.Frame, c.Origin).Stage = "gain";
                var sideCandidates = gainCandidates.Where(c => layout.ApproachDefense == null || layout.ApproachDefense.Sides.Any(s =>
                        s.Id == c.Frame.Side && s.Positions.Any(p => Distance(p, c.Origin) <= 14)))
                    .ToList();
                betweenSide += sideCandidates.Count(c => c.Frame.BetweenNests);
                HashSet<string> sideKeys = new HashSet<string>(sideCandidates.Where(c => c.Frame.BetweenNests)
                    .Select(c => BetweenCandidateKey(name, c.Frame, c.Origin)), StringComparer.Ordinal);
                foreach (var c in gainCandidates.Where(c => c.Frame.BetweenNests && !sideKeys.Contains(
                    BetweenCandidateKey(name, c.Frame, c.Origin))))
                { BetweenCandidateDiagnostic rec = GetBetweenCandidate(name, c.Frame, c.Origin); rec.Stage = "side"; rec.Rejection = "side_range"; }
                foreach (var c in sideCandidates.Where(c => c.Frame.BetweenNests))
                    GetBetweenCandidate(name, c.Frame, c.Origin).Stage = "side";
                var candidates = sideCandidates
                    .OrderBy(c =>
                    {
                        if (!exposureScores.TryGetValue(c.Origin, out int score))
                            exposureScores[c.Origin] = score = UnansweredExposure(input, layout, c.Origin,
                                def, previous, staffedPosts, opaqueForCoverage, existingExposureAnswers);
                        return score;
                    })
                    .ThenByDescending(c => c.Gain + c.MutualShots * 3 + c.FrontOpenGround / 8 -
                        Manhattan(c.Origin, c.Frame.Gate) / 4)
                    .ThenByDescending(c => c.FrontShots).ThenByDescending(c => c.Gain)
                    .ThenByDescending(c => c.FrontExposure)
                    .ThenByDescending(c => c.Sides).ThenByDescending(c => c.Shots)
                    .ThenByDescending(c => c.NewFront)
                    .ThenBy(c => Manhattan(c.Origin, c.Frame.Gate))
                    .ThenBy(c => c.Frame.Side, StringComparer.Ordinal).ThenBy(c => c.Origin).ThenBy(c => c.Side).ToList();
                betweenRanked += candidates.Count(c => c.Frame.BetweenNests);
                foreach (var c in candidates.Where(c => c.Frame.BetweenNests))
                    GetBetweenCandidate(name, c.Frame, c.Origin).Stage = "ranked";
                foreach (var candidate in candidates)
                        {
                            Frame frame = candidate.Frame;
                            currentBetweenCandidate = frame.BetweenNests;
                            currentBetweenPosition = frame.BetweenNests ?
                                GetBetweenCandidate(name, frame, candidate.Origin) : null;
                            currentBetweenBlocker = null;
                            if (currentBetweenPosition != null) currentBetweenPosition.Stage = "placement";
                            if (frame.BetweenNests) betweenPlacementChecks++;
                            GridPoint origin = candidate.Origin;
                            int side = candidate.Side;
                            GridPoint facing = frame.Outward, across = new GridPoint(-facing.Z, facing.X);
                            List<GridPoint> footprint = Enumerable.Range(0, def.SizeX).SelectMany(x =>
                                Enumerable.Range(0, def.SizeZ).Select(z =>
                                    new GridPoint(origin.X + x, origin.Z + z))).ToList();
                            GridPoint? footprintBlocker = footprint.Where(c => blocked.Contains(c) || opaque.Contains(c) ||
                                arrivalCells.Contains(c) || !Buildable(input.Terrain, c) ||
                                previousServiceRoutes.Contains(c) || postRoutes.Contains(c))
                                .Select(c => (GridPoint?)c).FirstOrDefault();
                            if (footprintBlocker.HasValue)
                            { currentBetweenBlocker = footprintBlocker; Reject("footprint"); continue; }
                            double blastClearance = def.ExplosionRadius + 2;
                            GridPoint? blastBlocker = BlastClearanceAssets(layout)
                                .Where(c => footprint.Any(f => Distance(f, c) < blastClearance))
                                .Select(c => (GridPoint?)c).FirstOrDefault();
                            if (!blastBlocker.HasValue)
                                blastBlocker = previousFootprintCells.Where(c =>
                                    footprint.Any(f => previousTurretsByFootprintCell.TryGetValue(c, out List<P4BTurretPlan> atCell) &&
                                        atCell.Any(t => Distance(f, c) < RequiredTurretSpacing(def, spacingLevel,
                                            frame.Gate, t))))
                                    .Select(c => (GridPoint?)c).FirstOrDefault();
                            if (blastBlocker.HasValue)
                            { currentBetweenBlocker = blastBlocker; Reject("blast_clearance"); continue; }
                List<GridPoint> cover = CoverFor(input.Terrain, layout, footprint, legacyBlocked, legacyOpaque,
                    facing, across, side, null, out GridPoint? failedCoverCell,
                    out string failedCoverRequirement);
                if (cover == null)
                {
                    string condition = failedCoverCell.HasValue
                        ? failedCoverRequirement + ":" + CoverFailureCondition(input, layout, fieldWall,
                            footprint, legacyBlocked, legacyOpaque, null, previous, failedCoverCell, failedCoverRequirement)
                        : CoverFailureCondition(input, layout, fieldWall, footprint, legacyBlocked, legacyOpaque,
                            null, previous, failedCoverCell, failedCoverRequirement);
                    turretCoverRejections.Add(FormatCoverRejection(origin, failedCoverCell, condition));
                    currentBetweenBlocker = failedCoverCell;
                    Reject("turret_cover"); continue;
                }
                var otherRouteConflict = previous.Select(t => new
                {
                    Turret = t,
                    Cell = t.ServiceRoute.Intersect(cover).Select(c => (GridPoint?)c).FirstOrDefault()
                }).FirstOrDefault(x => x.Cell.HasValue);
                if (otherRouteConflict != null)
                {
                    turretCoverRejections.Add(FormatCoverRejection(origin, otherRouteConflict.Cell,
                        "another_turret_service_route_intersection"));
                    currentBetweenBlocker = otherRouteConflict.Cell;
                    Reject("turret_cover"); continue;
                }
                int coverValue = CoverImportValue(input, P4BCoverNest.CoverDefName(input), cover.Count);
                if (coverValue == int.MaxValue || baseValue + spentValue + value + coverValue >
                    input.Requirements.ImportValueLimit) { Reject("cover_budget"); continue; }
                HashSet<GridPoint> routeEndpointsBlocked = new HashSet<GridPoint>(legacyBlocked);
                routeEndpointsBlocked.UnionWith(cover);
                List<GridPoint> services = TurretServiceCells(input.Terrain, footprint, cover,
                    routeEndpointsBlocked, frame.Gate);
                HashSet<GridPoint> routeBlocked = new HashSet<GridPoint>(routeEndpointsBlocked);
                routeBlocked.ExceptWith(nestCover);
                routeBlocked.ExceptWith(previous.SelectMany(t => t.ServiceRoute));
                if (frame.BetweenNests) betweenEndpoints += services.Count;
                Dictionary<GridPoint, List<GridPoint>> serviceRoutes = TurretRoutes(input.Terrain,
                    layout, frame.Gate, frame.Side, services, routeBlocked, footprint);
                if (frame.BetweenNests) betweenReachableRoutes += serviceRoutes.Count;
                bool anyServiceRoute = false;
                foreach (GridPoint service in services)
                {
                    if (!serviceRoutes.TryGetValue(service, out List<GridPoint> route)) continue;
                    anyServiceRoute = true;
                    P4BTurretPlan support = null;
                    if (def.MinimumRange > 0)
                    {
                        support = PlanCloseSupport(input, layout, fieldWall, legacyBlocked, legacyOpaque,
                            footprint, route, cover, def, value + coverValue, side, frame,
                            spentValue, spentWatts, previous, out string supportFailure,
                            out string supportRouteFailures);
                        if (!string.IsNullOrEmpty(supportRouteFailures))
                            serviceRouteFailures.AddRange(supportRouteFailures.Split('|'));
                        if (support == null) { Reject(supportFailure); continue; }
                    }
                    P4BTurretPlan proposed = new P4BTurretPlan(def, origin, footprint, service, route, fuel.DefName,
                        loadedUnits, reserveUnits, value + coverValue, support, cover,
                        frame.Gate, facing, frame.Side, betweenNests: frame.BetweenNests,
                        spacingLevel: spacingLevel);
                    P4BTurretPlan network = Combine(input.Catalog, groups.Concat(new[] { proposed }));
                    IReadOnlyList<string> networkErrors = ValidateInternal(input, layout, network,
                        null, garrison, onlyGroup: proposed, sharedBaseOpaque: baseOpaque);
                    if (networkErrors.Count > 0)
                    {
                        if (networkErrors.Contains("crossing_hard_exposure_coverage"))
                            foreach (string blocker in HardExposureFailureDetails(input, layout, network, garrison,
                                proposed.AccessGate))
                            {
                                hardExposureBlockers.TryGetValue(blocker, out int attempts);
                                hardExposureBlockers[blocker] = attempts + 1;
                            }
                        if (networkErrors.Contains("turret_without_nearby_pawn_support"))
                        {
                            int range = StandardRifleRange(input);
                            foreach (P4BTurretPlan unsupported in network.All.Where(g =>
                                !NearbyPawnSupport(layout.ApproachDefense, g, legacyOpaque, range)))
                                nearbyPawnSupportRejectedSides.Add(unsupported.Side);
                        }
                        if (frame.BetweenNests && networkErrors.Contains("turret_obstructs_defender_frontage"))
                            betweenNestsFrontageRejections++;
                        Reject("network_" + networkErrors[0]); continue;
                    }
                    if (!P4BApproachDefensePlanner.TurretsPreserveAttackSlots(
                        layout, network.All.SelectMany(g => g.Footprint)))
                    { if (frame.BetweenNests) betweenNestsFrontageRejections++; Reject("post_lane_obstructed"); continue; }
                    if (currentBetweenPosition != null)
                    { currentBetweenPosition.Stage = "placed"; currentBetweenPosition.Rejection = "placed"; }
                    foreach (var skipped in candidates.Where(c => c.Frame.BetweenNests && !ReferenceEquals(c, candidate)))
                    {
                        BetweenCandidateDiagnostic rec = GetBetweenCandidate(name, skipped.Frame, skipped.Origin);
                        if (rec.Rejection == null) rec.Rejection = "not_reached_after_earlier_acceptance";
                    }
                    diagnostic = "accepted " + name + " side=" + frame.Side + " gain=" + candidate.Gain + " gain>=4" +
                        " staffedMutual=" + candidate.MutualShots + " approachSides=" + candidate.Sides +
                        " betweenNests=" + frame.BetweenNests + " betweenNestsFrontageRejects=" + betweenNestsFrontageRejections +
                        " approachSamples=" + candidate.Shots + " at=" + origin + " value=" + value +
                        " loaded=" + loadedUnits + " reserve=" + reserveUnits +
                        (support == null ? string.Empty : " supportedBy=" + support.DefName) +
                        (rejected.Count == 0 ? string.Empty : " rejectedBefore=[" + string.Join(",",
                            rejected.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + ":" + kv.Value)) + "]") +
                            TurretCoverRejectionSuffix() + ServiceRouteFailureSuffix() + BetweenFunnelSuffix() +
                            NearbyPawnSupportSuffix() + HardExposureBlockerSuffix() + BetweenCandidateDetailsSuffix();
                    return proposed;
                }
                if (!anyServiceRoute)
                {
                    string doorCuts = string.Join(";", services.Select(service => RouteCutSummary(input, layout,
                        TurretRouteStarts(layout, frame.Gate, frame.Side), service, routeBlocked, footprint, previous)));
                    serviceRouteFailures.Add(origin + ">" + frame.Gate + ">" +
                        (services.Count == 0 ? "none" : string.Join(",", services)) + ">" +
                        (services.Count == 0 ? "no_legal_non_cover_service_endpoint" :
                            "non_cover_service_endpoints_unreachable_from_route_origin") +
                        (doorCuts.Length == 0 ? string.Empty : ";" + doorCuts));
                    Reject("service_route");
                }
            }
            currentBetweenCandidate = false;
            }
            diagnostic = string.Join(",", rejected.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => kv.Key + ":" + kv.Value));
            if (diagnostic.Length == 0) diagnostic = "no_useful_unsaturated_approach";
            diagnostic += " betweenNestsFrontageRejects=" + betweenNestsFrontageRejections + BetweenFunnelSuffix() +
                NearbyPawnSupportSuffix() + HardExposureBlockerSuffix() + BetweenCandidateDetailsSuffix();
            diagnostic += TurretCoverRejectionSuffix() + ServiceRouteFailureSuffix();
            return null;

            void Reject(string code)
            {
                rejected.TryGetValue(code, out int n); rejected[code] = n + 1;
                if (currentBetweenCandidate)
                {
                    betweenRejected.TryGetValue(code, out int bn); betweenRejected[code] = bn + 1;
                    if (currentBetweenPosition != null)
                    { currentBetweenPosition.Stage = "placement"; currentBetweenPosition.Rejection = code;
                        currentBetweenPosition.Blocker = currentBetweenBlocker; }
                }
            }
            string BetweenCandidateKey(string type, Frame frame, GridPoint origin) => type + "/" + frame.Gate + "/" +
                frame.FirstNest + "/" + frame.PartnerNest + "/" + frame.Anchor + "/" + origin;
            BetweenCandidateDiagnostic GetBetweenCandidate(string type, Frame frame, GridPoint origin) =>
                betweenCandidatesByKey[BetweenCandidateKey(type, frame, origin)];
            string BetweenFunnelSuffix() => " betweenFunnel{pairs=" + betweenPairsFound + ";generated=" + betweenGenerated +
                ";within14OneNest=" + betweenBand + ";rifleRangeAndLoS=" + betweenRifleLos +
                ";fireLane=" + betweenFireLane + ";gain=" + betweenGain +
                ";side=" + betweenSide + ";ranked=" + betweenRanked + ";serviceEndpoints=" +
                betweenEndpoints + ";checked=" + betweenPlacementChecks + ";reachable=" + betweenReachableRoutes +
                ";rejects=" + (betweenRejected.Count == 0 ? "none" : string.Join(",", betweenRejected
                    .OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + ":" + kv.Value))) + "}";
            string HardExposureBlockerSuffix() => hardExposureBlockers.Count == 0 ? string.Empty :
                " hardExposureBlockers=[" + string.Join(",", hardExposureBlockers.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(kv => kv.Key + ":" + kv.Value)) + "]";
            string NearbyPawnSupportSuffix() => " noNearbyNestSides=[" + string.Join(",",
                nearbyPawnSupportRejectedSides.OrderBy(s => s, StringComparer.Ordinal)) + "]";
            string BetweenCandidateDetailsSuffix() => betweenCandidates.Count == 0 ? string.Empty :
                " betweenCandidates{" + string.Join("|", betweenCandidates.Select(c => c.Type + "~" + c.Gate + "~" +
                    c.NestA + "~" + c.NestB + "~" + c.StretchCell + "~" + c.Origin + "~" + c.Stage + "~" +
                    (c.Rejection ?? "none") + "~" + (c.Blocker.HasValue ? c.Blocker.Value.ToString() : "none"))) + "}";
            string TurretCoverRejectionSuffix() => turretCoverRejections.Count == 0 ? string.Empty :
                " coverRejections=[" + string.Join("|", turretCoverRejections) + "]";
            string ServiceRouteFailureSuffix() => serviceRouteFailures.Count == 0 ? string.Empty :
                " serviceRouteFailures=[" + string.Join("|", serviceRouteFailures) + "]";
        }

        // A quest camp's advertised mini turrets: exactly `count`, spread
        // round-robin over the exposed sides (most approach samples first),
        // each with a cover nest and a service route from a camp gate. Loaded
        // as native loads them, with no reserve. Null when the count cannot fit.
        public static P4BTurretPlan PlanQuestGuns(ProceduralLayoutInput input, ProceduralLayout layout, int count,
            out string diagnostic)
        {
            if (input == null || layout == null) throw new ArgumentNullException();
            if (count <= 0) { diagnostic = "none_advertised"; return null; }
            if (layout.ApproachDefense == null || layout.Power == null)
            { diagnostic = "defense_or_power_unavailable"; return null; }
            if (!input.Catalog.TryGet("Turret_MiniTurret", out CatalogDef mini) ||
                !input.Catalog.IsLegal(mini, input.Equipment.Kit.Stage) || !mini.AutonomousTurret ||
                mini.FuelCapacity <= 0 || mini.FuelMultiplier <= 0 || mini.Fuel.Count != 1 ||
                !input.Catalog.TryGet(mini.Fuel[0], out CatalogDef fuel) ||
                !input.Catalog.IsLegal(fuel, input.Equipment.Kit.Stage))
            { diagnostic = "mini_turret_illegal"; return null; }
            int loaded = (int)Math.Ceiling(mini.FuelCapacity * mini.InitialFuelPercent / mini.FuelMultiplier);
            int value = ImportValue(input.Catalog, mini, fuel, loaded);
            List<P4BAttackSide> sides = layout.ApproachDefense.Sides.Where(s => s.Exposed)
                .OrderByDescending(s => s.Samples.Count).ThenBy(s => s.Id, StringComparer.Ordinal).ToList();
            if (sides.Count == 0) { diagnostic = "no_exposed_side"; return null; }
            List<Frame> frames = sides.SelectMany(s => layout.ApproachDefense.Posts
                .Select(p => new { Post = p, Slot = p.Slots.Cast<GridPoint?>().FirstOrDefault(c => s.Positions.Contains(c.Value)) })
                .Where(x => x.Slot.HasValue).Select(x => new Frame { Anchor = x.Slot.Value, Gate = x.Post.AccessGate,
                    Outward = s.Outward, Side = s.Id })).ToList();
            HashSet<GridPoint> baseBlocked = new HashSet<GridPoint>(layout.Enclosure
                .Concat(layout.Rooms.SelectMany(r => r.Bounds.Points()))
                .Concat(layout.PowerYards.SelectMany(y => y.Cells))
                .Concat(layout.AllFixtures.SelectMany(f => f.Cells))
                .Concat(layout.OuterSections.SelectMany(o => o.ServiceLane(layout.Gates[o.GateIndex])))
                .Concat(layout.ApproachDefense.CoverCells).Concat(layout.ApproachDefense.AllPosts));
            HashSet<GridPoint> baseOpaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
            baseBlocked.UnionWith(baseOpaque.Except(P4BApproachDefensePlanner.AccessGates(layout)));
            List<P4BTurretPlan> guns = new List<P4BTurretPlan>();
            Dictionary<string, int> rejected = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
            {
                // The side whose turn it is first, then any other side.
                IEnumerable<P4BAttackSide> order = sides.Skip(i % sides.Count).Concat(sides.Take(i % sides.Count));
                P4BTurretPlan placed = null;
                foreach (P4BAttackSide side in order)
                {
                    placed = PlaceQuestGun(input, layout, mini, fuel, loaded, value, frames.Where(f => f.Side == side.Id),
                        side, guns, baseBlocked, baseOpaque, rejected);
                    if (placed != null) break;
                }
                if (placed == null)
                {
                    diagnostic = "placed=" + guns.Count + "/" + count + " " + string.Join(",", rejected
                        .OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + ":" + kv.Value));
                    return null;
                }
                guns.Add(placed);
            }
            P4BTurretPlan network = Combine(input.Catalog, guns);
            diagnostic = "accepted turrets=" + guns.Count + " sides=" + string.Join(",", guns.GroupBy(g => g.Side)
                .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + ":" + g.Count())) +
                " watts=" + network.TotalWatts + " value=" + network.ImportValue;
            return network;
        }

        private static P4BTurretPlan PlaceQuestGun(ProceduralLayoutInput input, ProceduralLayout layout,
            CatalogDef mini, CatalogDef fuel, int loaded, int value, IEnumerable<Frame> frames, P4BAttackSide side,
            List<P4BTurretPlan> guns, HashSet<GridPoint> baseBlocked, HashSet<GridPoint> baseOpaque,
            Dictionary<string, int> rejected)
        {
            HashSet<GridPoint> blocked = new HashSet<GridPoint>(baseBlocked);
            blocked.UnionWith(guns.SelectMany(t => t.Footprint.Concat(t.CoverCells)));
            HashSet<GridPoint> opaque = new HashSet<GridPoint>(baseOpaque);
            opaque.UnionWith(guns.SelectMany(t => t.Footprint));
            int minimum = (int)Math.Ceiling(mini.MinimumRange), maximum = (int)Math.Floor(mini.MaximumRange);
            var candidates = frames.SelectMany(frame => new[] { 2, 3, 4, 5, 6, 7, 8, 10, 12 }.SelectMany(forward =>
                    new[] { 0, -2, 2, -4, 4, -6, 6, -8, 8, -10, 10, -12, 12, -14, 14 }.Select(lateral => new
                    { Frame = frame, Origin = At(frame.Anchor, frame.Outward,
                        new GridPoint(-frame.Outward.Z, frame.Outward.X), forward, lateral) })))
                .Select(c => new { c.Frame, c.Origin, Shots = side.Samples.Count(t =>
                    P4BFireLane.CanEngage(c.Origin, t, minimum, maximum, opaque)) })
                .Where(c => c.Shots >= 4)
                .OrderByDescending(c => c.Shots).ThenBy(c => Manhattan(c.Origin, c.Frame.Gate)).ThenBy(c => c.Origin);
            foreach (var candidate in candidates)
            {
                Frame frame = candidate.Frame;
                GridPoint facing = frame.Outward, across = new GridPoint(-facing.Z, facing.X);
                List<GridPoint> footprint = Enumerable.Range(0, mini.SizeX).SelectMany(x =>
                    Enumerable.Range(0, mini.SizeZ).Select(z => new GridPoint(candidate.Origin.X + x, candidate.Origin.Z + z))).ToList();
                if (footprint.Any(c => blocked.Contains(c) || opaque.Contains(c) || Arrival(layout).Contains(c) ||
                    !Buildable(input.Terrain, c) || guns.Any(t => t.ServiceRoute.Contains(c)) ||
                    layout.ApproachDefense.Posts.Any(p => p.Route.Contains(c))))
                { Reject("footprint"); continue; }
                if (DefendedAssets(layout).Any(c => footprint.Any(f => Distance(f, c) < mini.ExplosionRadius + 2)) ||
                    guns.Any(t => footprint.Any(f => t.Footprint.Any(c => Distance(f, c) < mini.ExplosionRadius + 2))))
                { Reject("blast_clearance"); continue; }
                List<GridPoint> cover = CoverFor(input.Terrain, layout, footprint, blocked, opaque,
                    facing, across, 1);
                if (cover == null) { Reject("turret_cover"); continue; }
                if (guns.Any(t => t.ServiceRoute.Intersect(cover).Any()))
                { Reject("turret_cover"); continue; }
                HashSet<GridPoint> routeBlocked = new HashSet<GridPoint>(blocked);
                routeBlocked.UnionWith(cover);
                List<GridPoint> services = TurretServiceCells(input.Terrain, footprint, cover,
                    routeBlocked, frame.Gate);
                Dictionary<GridPoint, List<GridPoint>> serviceRoutes = Routes(input.Terrain,
                    frame.Gate, services, routeBlocked, footprint);
                foreach (GridPoint service in services)
                {
                    if (!serviceRoutes.TryGetValue(service, out List<GridPoint> route)) continue;
                    int coverValue = CoverImportValue(input, P4BCoverNest.CoverDefName(input), cover.Count);
                    if (coverValue == int.MaxValue) { Reject("cover_value"); continue; }
                    P4BTurretPlan proposed = new P4BTurretPlan(mini, candidate.Origin, footprint, service, route, fuel.DefName,
                        loaded, 0, value + coverValue, coverCells: cover, accessGate: frame.Gate, outward: facing, side: frame.Side);
                    P4BTurretPlan network = Combine(input.Catalog, guns.Concat(new[] { proposed }));
                    IReadOnlyList<string> errors = Validate(input, layout, network);
                    if (errors.Count > 0) { Reject("network_" + errors[0]); continue; }
                    if (P4BApproachDefensePlanner.Validate(input, layout.WithTurret(input, network)).Count > 0)
                    { Reject("post_lane_obstructed"); continue; }
                    return proposed;
                }
                Reject("service_route");
            }
            return null;

            void Reject(string code) { rejected.TryGetValue(code, out int n); rejected[code] = n + 1; }
        }

        private static GridPoint At(GridPoint origin, GridPoint outward, GridPoint tangent, int forward, int lateral) =>
            new GridPoint(origin.X + outward.X * forward + tangent.X * lateral,
                origin.Z + outward.Z * forward + tangent.Z * lateral);

        private static P4BTurretPlan PlanCloseSupport(ProceduralLayoutInput input, ProceduralLayout layout,
            P4BFieldWallPlan fieldWall, HashSet<GridPoint> blocked, HashSet<GridPoint> opaque,
            IReadOnlyCollection<GridPoint> heavy, IReadOnlyCollection<GridPoint> heavyRoute,
            IReadOnlyCollection<GridPoint> heavyCover,
            CatalogDef heavyDef, int heavyValue, int heavySide, Frame frame, int spentValue, int spentWatts,
            IReadOnlyList<P4BTurretPlan> previous, out string failureCode,
            out string routeFailureDetails)
        {
            failureCode = "blind_zone_support";
            List<string> routeFailures = new List<string>();
            routeFailureDetails = string.Empty;
            if (!input.Catalog.TryGet("Turret_MiniTurret", out CatalogDef mini) ||
                !input.Catalog.IsLegal(mini, input.Equipment.Kit.Stage) || !mini.AutonomousTurret ||
                mini.FuelCapacity <= 0 || mini.FuelMultiplier <= 0 || mini.FuelPerShot <= 0 ||
                mini.Fuel.Count != 1 ||
                !input.Catalog.TryGet(mini.Fuel[0], out CatalogDef fuel) ||
                !input.Catalog.IsLegal(fuel, input.Equipment.Kit.Stage)) return null;
            int loaded = (int)Math.Ceiling(mini.FuelCapacity * mini.InitialFuelPercent / mini.FuelMultiplier);
            int reserve = P5Readiness.ReloadReserve(mini.FuelCapacity, mini.InitialFuelPercent, mini.FuelPerShot,
                mini.FuelMultiplier, (int)Math.Ceiling(mini.FuelCapacity / mini.FuelMultiplier));
            int value = ImportValue(input.Catalog, mini, fuel, loaded + reserve);
            int baseValue = BaseMaterialValue(input, layout);
            if (value == int.MaxValue || baseValue + spentValue + heavyValue + value >
                input.Requirements.ImportValueLimit) { failureCode = "import_budget"; return null; }
            int watts = spentWatts + heavyDef.BasePowerConsumption + mini.BasePowerConsumption;
            if (layout.Power.Generators * layout.Power.GeneratorWatts + layout.Power.FirmWatts - layout.Power.CriticalWatts < watts ||
                layout.Power.Generators * layout.Power.GeneratorWatts + layout.Power.FirmWatts -
                    layout.Power.CriticalWatts - layout.Power.NonCriticalWatts < watts)
            { failureCode = "critical_power_headroom"; return null; }
            GridPoint outer = frame.Anchor, outward = frame.Outward,
                tangent = new GridPoint(-outward.Z, outward.X);
            HashSet<GridPoint> supportBlocked = new HashSet<GridPoint>(blocked);
            supportBlocked.UnionWith(heavy);
            supportBlocked.UnionWith(heavyCover);
            supportBlocked.UnionWith(heavyRoute);
            HashSet<GridPoint> nestCover = new HashSet<GridPoint>(layout.ApproachDefense?.CoverCells ??
                Enumerable.Empty<GridPoint>());
            if (layout.ApproachDefense != null)
                nestCover.UnionWith(layout.ApproachDefense.Posts.SelectMany(p => p.CoverCells));
            foreach (int forward in new[] { 1, 2, 3, 4, 5, 7, 9, 11, 13, 15 })
                foreach (int lateral in new[] { 0, 2, 4, 6, 8, 10, 12, 14 })
                {
                    GridPoint origin = At(outer, outward, tangent, forward, -heavySide * lateral);
                    if (Distance(outer, origin) < mini.ExplosionRadius + 2 || Distance(outer, origin) > 14) continue;
                    List<GridPoint> footprint = Enumerable.Range(0, mini.SizeX).SelectMany(x =>
                        Enumerable.Range(0, mini.SizeZ).Select(z =>
                            new GridPoint(origin.X + x, origin.Z + z))).ToList();
                    if (footprint.Any(c => supportBlocked.Contains(c) || opaque.Contains(c) ||
                        Arrival(layout).Contains(c) || heavyRoute.Contains(c) ||
                        previous.Any(t => t.ServiceRoute.Contains(c)) ||
                        (layout.ApproachDefense?.Posts.Any(p => p.Route.Contains(c)) ?? false) ||
                        !Buildable(input.Terrain, c))) continue;
                    if (layout.ApproachDefense != null && !layout.ApproachDefense.Sides.Any(s =>
                        s.Id == frame.Side && s.Positions.Any(p => footprint.Any(c => Distance(p, c) <= 14))))
                        continue;
                    if (BlastClearanceAssets(layout)
                        .Any(c => footprint.Any(f => Distance(c, f) < mini.ExplosionRadius + 2)) ||
                        footprint.Any(f => heavy.Any(h => Distance(f, h) <
                            Math.Max(heavyDef.ExplosionRadius, mini.ExplosionRadius) + 2)) ||
                        previous.Any(t => footprint.Any(f => t.Footprint.Any(c =>
                            Distance(f, c) < Math.Max(mini.ExplosionRadius, t.ExplosionRadius) + 2)))) continue;
                    if (!CloseSupport(input.Terrain, layout, heavy, heavyDef.MinimumRange,
                        footprint, mini.MaximumRange, opaque)) continue;
                    List<GridPoint> cover = CoverFor(input.Terrain, layout, footprint, supportBlocked,
                        opaque, outward, tangent, -heavySide);
                    if (cover == null || cover.Intersect(heavyRoute).Any() ||
                        previous.Any(t => t.ServiceRoute.Intersect(cover).Any())) continue;
                    HashSet<GridPoint> routeBlocked = new HashSet<GridPoint>(supportBlocked);
                    routeBlocked.UnionWith(cover);
                    List<GridPoint> services = TurretServiceCells(input.Terrain, footprint, cover,
                        routeBlocked, frame.Gate);
                    HashSet<GridPoint> servicePathBlocked = new HashSet<GridPoint>(routeBlocked);
                    servicePathBlocked.ExceptWith(nestCover);
                    servicePathBlocked.ExceptWith(previous.SelectMany(t => t.ServiceRoute));
                    Dictionary<GridPoint, List<GridPoint>> serviceRoutes = TurretRoutes(input.Terrain,
                        layout, frame.Gate, frame.Side, services, servicePathBlocked, footprint);
                    bool anyServiceRoute = false;
                    foreach (GridPoint service in services)
                    {
                        if (!serviceRoutes.TryGetValue(service, out List<GridPoint> route)) continue;
                        anyServiceRoute = true;
                        int coverValue = CoverImportValue(input, P4BCoverNest.CoverDefName(input), cover.Count);
                        if (coverValue == int.MaxValue || baseValue + spentValue + heavyValue + value +
                            coverValue > input.Requirements.ImportValueLimit) continue;
                        routeFailureDetails = string.Join("|", routeFailures);
                        return new P4BTurretPlan(mini, origin, footprint, service, route,
                            fuel.DefName, loaded, reserve, value + coverValue, coverCells: cover,
                            accessGate: frame.Gate, outward: outward, side: frame.Side);
                    }
                    if (!anyServiceRoute)
                    {
                        string doorCuts = string.Join(";", services.Select(service => RouteCutSummary(input, layout,
                            TurretRouteStarts(layout, frame.Gate, frame.Side), service, routeBlocked, footprint, previous)));
                        routeFailures.Add(origin + ">" + frame.Gate + ">" +
                            (services.Count == 0 ? "none" : string.Join(",", services)) + ">" +
                            (services.Count == 0 ? "support_no_legal_non_cover_service_endpoint" :
                                "support_non_cover_service_endpoints_unreachable_from_route_origin") +
                            (doorCuts.Length == 0 ? string.Empty : ";" + doorCuts));
                    }
                }
            if (routeFailures.Count > 0) failureCode = "service_route";
            routeFailureDetails = string.Join("|", routeFailures);
            return null;
        }

        private static bool CloseSupport(TerrainSnapshot terrain, ProceduralLayout layout,
            IReadOnlyCollection<GridPoint> heavy, double heavyMinimum,
            IReadOnlyCollection<GridPoint> small, double smallMaximum, HashSet<GridPoint> opaque)
        {
            GridPoint origin = heavy.OrderBy(c => c).First();
            HashSet<GridPoint> blockers = new HashSet<GridPoint>(opaque);
            blockers.UnionWith(heavy);
            blockers.UnionWith(small);
            List<GridPoint> blind = new List<GridPoint>();
            foreach (int dx in new[] { -4, 0, 4 })
                foreach (int dz in new[] { -4, 0, 4 })
                {
                    GridPoint target = new GridPoint(origin.X + dx, origin.Z + dz);
                    if ((dx == 0 && dz == 0) || !Walkable(terrain, target) || blockers.Contains(target) ||
                        (layout.ApproachDefense != null && !layout.ApproachDefense.AttackerCanReach(target)) ||
                        Distance(origin, target) >= heavyMinimum) continue;
                    blind.Add(target);
                }
            if (blind.Count < 4) return false;
            GridPoint smallOrigin = small.OrderBy(c => c).First();
            HashSet<GridPoint> smallBlockers = new HashSet<GridPoint>(blockers);
            smallBlockers.Remove(smallOrigin);
            int smallShots = blind.Count(target => P4BFireLane.CanEngage(smallOrigin, target,
                0, (int)Math.Floor(smallMaximum), smallBlockers));
            // Require useful pawn support at this actual front, not the two
            // field posts on the other side of an opaque settlement.
            IEnumerable<GridPoint> posts = layout.ApproachDefense?.AllPosts ?? new GridPoint[0];
            int pawnSupport = posts.Count(post => blind.Count(target =>
                P4BFireLane.CanEngage(post, target, 0, 25, blockers)) >= 2);
            return smallShots >= 4 && pawnSupport >= (layout.ApproachDefense == null ? 2 : 1);
        }

        private static List<GridPoint> CoverFor(TerrainSnapshot terrain, ProceduralLayout layout,
            IReadOnlyCollection<GridPoint> footprint, HashSet<GridPoint> blocked, HashSet<GridPoint> opaque,
            GridPoint outward, GridPoint tangent, int side,
            IReadOnlyCollection<GridPoint> serviceRoute = null)
        {
            return CoverFor(terrain, layout, footprint, blocked, opaque, outward, tangent, side,
                serviceRoute, out _, out _);
        }

        private static List<GridPoint> CoverFor(TerrainSnapshot terrain, ProceduralLayout layout,
            IReadOnlyCollection<GridPoint> footprint, HashSet<GridPoint> blocked, HashSet<GridPoint> opaque,
            GridPoint outward, GridPoint tangent, int side, IReadOnlyCollection<GridPoint> serviceRoute,
            out GridPoint? failureCell, out string failureRequirement)
        {
            failureCell = null;
            failureRequirement = null;
            P4BFieldWallPlan fieldWall = layout.FieldWall;
            List<GridPoint> front = footprint.Where(c => !footprint.Contains(new GridPoint(
                c.X + outward.X, c.Z + outward.Z))).Select(c => new GridPoint(
                c.X + outward.X, c.Z + outward.Z)).Distinct().ToList();
            bool legal(GridPoint c) => !footprint.Contains(c) && !blocked.Contains(c) &&
                !opaque.Contains(c) && (serviceRoute == null || !serviceRoute.Contains(c)) &&
                !Arrival(layout).Contains(c) &&
                (fieldWall == null || !fieldWall.ExteriorPosts.Contains(c)) && Buildable(terrain, c);
            if (front.Count == 0)
            {
                failureRequirement = "missing_front_cover_cell";
                return null;
            }
            GridPoint? failedFront = front.Where(c => !legal(c)).Select(c => (GridPoint?)c).FirstOrDefault();
            if (failedFront.HasValue)
            {
                failureCell = failedFront;
                failureRequirement = "front_cover_cell_illegal";
                return null;
            }
            List<GridPoint> cover = new List<GridPoint>(front);
            foreach (int sign in new[] { side, -side })
                foreach (GridPoint flank in footprint.Where(c => !footprint.Contains(new GridPoint(
                    c.X + sign * tangent.X, c.Z + sign * tangent.Z))).Select(c => new GridPoint(
                    c.X + sign * tangent.X, c.Z + sign * tangent.Z)))
                {
                    if (cover.Contains(flank)) continue;
                    if (!legal(flank))
                    {
                        failureCell = flank;
                        failureRequirement = "flank_cover_cell_illegal";
                        return null;
                    }
                    cover.Add(flank);
                }
            return cover;
        }

        private static string FormatCoverRejection(GridPoint origin, GridPoint? cell, string condition) =>
            origin + ">" + (cell.HasValue ? cell.Value.ToString() : "none") + ">" + condition;

        private static string CoverFailureCondition(ProceduralLayoutInput input, ProceduralLayout layout,
            P4BFieldWallPlan fieldWall, IReadOnlyCollection<GridPoint> footprint,
            HashSet<GridPoint> blocked, HashSet<GridPoint> opaque, IReadOnlyCollection<GridPoint> serviceRoute,
            IReadOnlyList<P4BTurretPlan> previous, GridPoint? cell, string requirement)
        {
            if (!cell.HasValue) return requirement ?? "missing_cover_cell";
            GridPoint c = cell.Value;
            if (footprint.Contains(c)) return "cover_cell_overlaps_own_turret_footprint";
            if (blocked.Contains(c))
            {
                if (layout.ApproachDefense?.Posts.Any(p => p.Slots.Contains(c)) ?? false) return "blocked_by_nest_slot";
                if (layout.ApproachDefense?.Posts.Any(p => p.Route.Contains(c)) ?? false) return "blocked_by_nest_route";
                if ((layout.ApproachDefense?.CoverCells.Contains(c) ?? false) ||
                    (layout.ApproachDefense?.Posts.Any(p => p.CoverCells.Contains(c)) ?? false)) return "blocked_by_nest_cover";
                if (previous.Any(t => t.Footprint.Contains(c))) return "blocked_by_another_turret_footprint";
                if (previous.Any(t => t.CoverCells.Contains(c))) return "blocked_by_another_turret_cover";
                if (previous.Any(t => t.ServiceRoute.Contains(c))) return "blocked_by_another_turret_service_route";
                if (Arrival(layout).Contains(c)) return "blocked_by_arrival_or_exterior_route";
                if (fieldWall?.Fields.Contains(c) ?? false) return "blocked_by_field_crops";
                if (fieldWall?.ExteriorPosts.Contains(c) ?? false) return "blocked_by_field_wall_exterior_post";
                if (fieldWall?.Walls.Contains(c) ?? false) return "blocked_by_field_wall";
                if (layout.PowerYards.Any(y => y.Cells.Contains(c))) return "blocked_by_power_yard";
                if (layout.Enclosure.Contains(c) || layout.BuiltWalls.Contains(c) || layout.OuterWalls.Contains(c))
                    return "blocked_by_compound_wall";
                if (layout.Rooms.Any(r => r.Bounds.Contains(c)) || layout.AllFixtures.Any(f => f.Cells.Contains(c)))
                    return "blocked_by_compound_fixture_or_room";
                if (input.Terrain.InBounds(c))
                {
                    TerrainTraits blockedTraits = input.Terrain[c].Traits;
                    if ((blockedTraits & TerrainTraits.NaturalRock) != 0) return "blocked_by_natural_rock";
                    if ((blockedTraits & TerrainTraits.Water) != 0) return "blocked_by_water";
                    if ((blockedTraits & TerrainTraits.ClearablePlant) != 0) return "blocked_by_clearable_plant";
                    if ((blockedTraits & TerrainTraits.Occupied) != 0) return "blocked_by_terrain_occupancy";
                }
                return "blocked_by_reserved_occupancy";
            }
            if (opaque.Contains(c))
            {
                if (fieldWall?.Walls.Contains(c) ?? false) return "opaque_field_wall";
                if (layout.NaturalWalls.Contains(c) || input.Terrain.InBounds(c) &&
                    input.Terrain[c].Has(TerrainTraits.NaturalRock)) return "opaque_natural_rock";
                if (layout.BuiltWalls.Contains(c) || layout.OuterWalls.Contains(c) || layout.Enclosure.Contains(c))
                    return "opaque_compound_wall";
                return "opaque_cell";
            }
            if (serviceRoute?.Contains(c) ?? false) return "own_service_route";
            if (Arrival(layout).Contains(c)) return "arrival_or_exterior_route";
            if (fieldWall?.ExteriorPosts.Contains(c) ?? false) return "field_wall_exterior_post";
            if (!input.Terrain.InBounds(c)) return "terrain_out_of_bounds";
            TerrainTraits traits = input.Terrain[c].Traits;
            List<string> predicates = new List<string>();
            if ((traits & TerrainTraits.Standable) == 0 && (traits & TerrainTraits.ClearablePlant) == 0)
                predicates.Add("not_standable");
            if ((traits & TerrainTraits.Buildable) == 0) predicates.Add("not_buildable");
            if ((traits & TerrainTraits.Roofed) != 0) predicates.Add("roofed");
            if ((traits & TerrainTraits.Water) != 0) predicates.Add("water");
            if ((traits & TerrainTraits.NaturalRock) != 0) predicates.Add("natural_rock");
            if ((traits & TerrainTraits.Occupied) != 0) predicates.Add("terrain_occupied");
            if ((traits & TerrainTraits.MapEdge) != 0) predicates.Add("map_edge");
            if ((traits & TerrainTraits.ClearablePlant) != 0) predicates.Add("clearable_plant");
            return "terrain_predicate_failed:" + (predicates.Count == 0 ?
                (requirement ?? "not_buildable") : string.Join("+", predicates));
        }

        private static int CoverImportValue(ProceduralLayoutInput input, string defName, int count)
        {
            if (!input.Catalog.TryGet(defName, out CatalogDef def)) return int.MaxValue;
            double cost = 0;
            foreach (KeyValuePair<string, int> item in def.CostList)
            {
                if (!input.Catalog.TryGet(item.Key, out CatalogDef resource)) return int.MaxValue;
                cost += item.Value * resource.MarketValue;
            }
            if (def.CostStuffCount > 0)
            {
                string stuffName = input.Equipment.StuffFor(def);
                if (stuffName == null || !input.Catalog.TryGet(stuffName, out CatalogDef stuff)) return int.MaxValue;
                cost += def.CostStuffCount * stuff.MarketValue;
            }
            return cost * count >= int.MaxValue ? int.MaxValue : (int)Math.Ceiling(cost * count);
        }

        private static bool Walkable(TerrainSnapshot terrain, GridPoint c) => terrain.InBounds(c) &&
            (terrain[c].Has(TerrainTraits.Standable) || terrain[c].Has(TerrainTraits.ClearablePlant)) &&
            !terrain[c].HasAny(TerrainTraits.Water | TerrainTraits.NaturalRock | TerrainTraits.Occupied | TerrainTraits.MapEdge);

        private static bool Buildable(TerrainSnapshot terrain, GridPoint c) => Walkable(terrain, c) &&
            terrain[c].Has(TerrainTraits.Buildable) && !terrain[c].Has(TerrainTraits.Roofed);

        private static List<GridPoint> TurretServiceCells(TerrainSnapshot terrain,
            IReadOnlyCollection<GridPoint> footprint, IReadOnlyCollection<GridPoint> cover,
            HashSet<GridPoint> blocked, GridPoint routeOrigin) => footprint
                .SelectMany(c => Cardinal.Select(d => new GridPoint(c.X + d.X, c.Z + d.Z))).Distinct()
                .Where(service => !footprint.Contains(service) && !cover.Contains(service) &&
                    !blocked.Contains(service) && Walkable(terrain, service))
                .OrderBy(c => Manhattan(c, routeOrigin)).ThenBy(c => c).ToList();

        private static Dictionary<GridPoint, List<GridPoint>> Routes(TerrainSnapshot terrain, GridPoint start,
            IEnumerable<GridPoint> goals, HashSet<GridPoint> blocked, IReadOnlyCollection<GridPoint> footprint)
        {
            HashSet<GridPoint> unresolved = new HashSet<GridPoint>(goals ?? Enumerable.Empty<GridPoint>());
            Dictionary<GridPoint, List<GridPoint>> routes = new Dictionary<GridPoint, List<GridPoint>>();
            if (unresolved.Count == 0) return routes;
            Queue<GridPoint> queue = new Queue<GridPoint>();
            Dictionary<GridPoint, GridPoint> previous = new Dictionary<GridPoint, GridPoint>();
            queue.Enqueue(start); previous[start] = start;
            while (queue.Count > 0 && previous.Count < 4096)
            {
                GridPoint here = queue.Dequeue();
                if (unresolved.Remove(here))
                {
                    List<GridPoint> route = new List<GridPoint>();
                    for (GridPoint c = here; !c.Equals(start); c = previous[c]) route.Add(c);
                    route.Add(start); route.Reverse();
                    if (route.Count <= 160) routes.Add(here, route);
                    if (unresolved.Count == 0) break;
                }
                foreach (GridPoint d in Cardinal)
                {
                    GridPoint next = new GridPoint(here.X + d.X, here.Z + d.Z);
                    if (previous.ContainsKey(next) || footprint.Contains(next) ||
                        (blocked.Contains(next) && !next.Equals(start)) || !Walkable(terrain, next) ||
                        Manhattan(next, start) > 100) continue;
                    previous[next] = here; queue.Enqueue(next);
                }
            }
            return routes;
        }

        private static Dictionary<GridPoint, List<GridPoint>> TurretRoutes(TerrainSnapshot terrain,
            ProceduralLayout layout, GridPoint gate, string side, IEnumerable<GridPoint> goals,
            HashSet<GridPoint> blocked, IReadOnlyCollection<GridPoint> footprint)
        {
            Dictionary<GridPoint, List<GridPoint>> result = new Dictionary<GridPoint, List<GridPoint>>();
            foreach (GridPoint start in TurretRouteStarts(layout, gate, side))
                foreach (KeyValuePair<GridPoint, List<GridPoint>> route in Routes(terrain, start, goals, blocked, footprint))
                    if (!result.ContainsKey(route.Key) || route.Value.Count < result[route.Key].Count)
                        result[route.Key] = route.Value;
            return result;
        }

        private static string RouteCutSummary(ProceduralLayoutInput input, ProceduralLayout layout,
            IEnumerable<GridPoint> routeStarts, GridPoint serviceCell, HashSet<GridPoint> blocked,
            IReadOnlyCollection<GridPoint> footprint, IReadOnlyList<P4BTurretPlan> previous)
        {
            List<string> cuts = new List<string>();
            foreach (GridPoint start in routeStarts.Distinct())
            {
                List<GridPoint> path = LeastBlockedRoute(input.Terrain, start, serviceCell, blocked, footprint);
                if (path == null)
                {
                    string reason = !input.Terrain.InBounds(start) ? "door_out_of_bounds" :
                        !input.Terrain.InBounds(serviceCell) ? "service_out_of_bounds" : "diagnostic_search_exhausted";
                    cuts.Add("door=" + start + ":" + reason); continue;
                }
                List<GridPoint> obstacles = path.Where(c => !c.Equals(start) && IsRouteObstacle(input.Terrain,
                    c, blocked, footprint)).ToList();
                if (obstacles.Count == 0)
                {
                    cuts.Add("door=" + start + ":no_cell_blocker_shortest_path=" +
                        (path.Count - 1 > 160 ? "over_route_length_160" : "blocked_by_search_limit_or_route_condition"));
                    continue;
                }
                cuts.Add("door=" + start + ":" + string.Join(",", obstacles.Select(c =>
                    RouteBlockerCategory(input, layout, previous, blocked, footprint, c) + "@" + c)));
            }
            return "back=" + serviceCell + ";doorCuts=" + (cuts.Count == 0 ? "no_allowed_door" : string.Join(";", cuts));
        }

        private static List<GridPoint> LeastBlockedRoute(TerrainSnapshot terrain, GridPoint start, GridPoint goal,
            HashSet<GridPoint> blocked, IReadOnlyCollection<GridPoint> footprint)
        {
            if (!terrain.InBounds(start) || !terrain.InBounds(goal)) return null;
            LinkedList<GridPoint> queue = new LinkedList<GridPoint>();
            Dictionary<GridPoint, int> cost = new Dictionary<GridPoint, int> { [start] = 0 };
            Dictionary<GridPoint, GridPoint> previous = new Dictionary<GridPoint, GridPoint> { [start] = start };
            queue.AddFirst(start);
            int searchLimit = Math.Max(8192, terrain.Width * terrain.Height);
            while (queue.Count > 0 && previous.Count < searchLimit)
            {
                GridPoint here = queue.First.Value;
                queue.RemoveFirst();
                if (here.Equals(goal)) break;
                foreach (GridPoint d in Cardinal)
                {
                    GridPoint next = new GridPoint(here.X + d.X, here.Z + d.Z);
                    if (!terrain.InBounds(next) || previous.Count >= searchLimit) continue;
                    int nextCost = cost[here] + (IsRouteObstacle(terrain, next, blocked, footprint) &&
                        !next.Equals(start) ? 1 : 0);
                    if (cost.TryGetValue(next, out int oldCost) && oldCost <= nextCost) continue;
                    cost[next] = nextCost; previous[next] = here;
                    if (nextCost == cost[here]) queue.AddFirst(next); else queue.AddLast(next);
                }
            }
            if (!previous.ContainsKey(goal)) return null;
            List<GridPoint> path = new List<GridPoint>();
            for (GridPoint c = goal; !c.Equals(start); c = previous[c]) path.Add(c);
            path.Add(start); path.Reverse();
            return path;
        }

        private static bool IsRouteObstacle(TerrainSnapshot terrain, GridPoint cell,
            HashSet<GridPoint> blocked, IReadOnlyCollection<GridPoint> footprint) =>
            footprint.Contains(cell) || blocked.Contains(cell) || !Walkable(terrain, cell);

        private static string RouteBlockerCategory(ProceduralLayoutInput input, ProceduralLayout layout,
            IReadOnlyList<P4BTurretPlan> previous, HashSet<GridPoint> blocked,
            IReadOnlyCollection<GridPoint> footprint, GridPoint cell)
        {
            if (footprint.Contains(cell)) return "current_turret_footprint";
            if (previous.Any(t => t.Footprint.Contains(cell))) return "another_turret_footprint";
            if (previous.Any(t => t.CoverCells.Contains(cell))) return "another_turret_cover";
            if (previous.Any(t => t.ServiceRoute.Contains(cell))) return "another_turret_service_route";
            if (layout.ApproachDefense?.Posts.Any(p => p.CoverCells.Contains(cell)) ?? false) return "nest_cover";
            if (layout.ApproachDefense?.Posts.Any(p => p.Route.Contains(cell)) ?? false) return "nest_route";
            if (layout.ApproachDefense?.Posts.Any(p => p.Slots.Contains(cell)) ?? false) return "nest_slot";
            if (Arrival(layout).Contains(cell)) return "arrival_or_exterior_route";
            if (layout.OuterSections.Any(s => s.ServiceLane(layout.Gates[s.GateIndex]).Contains(cell)))
                return "outer_section_service_lane";
            if (layout.FieldWall?.Fields.Contains(cell) ?? false) return "field_crop";
            if (layout.FieldWall?.Walls.Contains(cell) ?? false) return "field_wall_band";
            if (layout.FieldWall?.ExteriorPosts.Contains(cell) ?? false) return "field_wall_post";
            if (layout.Gates.Any(g => g.BandWalls.Contains(cell) || g.BandDoors.Contains(cell) ||
                g.OuterVestibule.Contains(cell))) return "compound_wall_band";
            if (layout.Enclosure.Contains(cell) || layout.BuiltWalls.Contains(cell) || layout.OuterWalls.Contains(cell))
                return "compound_wall";
            if (layout.PowerYards.Any(y => y.Cells.Contains(cell))) return "power_yard";
            if (layout.Rooms.Any(r => r.Bounds.Contains(cell)) || layout.AllFixtures.Any(f => f.Cells.Contains(cell)))
                return "compound_room_or_fixture";
            if (input.Terrain.InBounds(cell))
            {
                TerrainTraits traits = input.Terrain[cell].Traits;
                if ((traits & TerrainTraits.NaturalRock) != 0) return "terrain_rock";
                if ((traits & TerrainTraits.Water) != 0) return "terrain_water";
                if ((traits & TerrainTraits.ClearablePlant) != 0) return "terrain_crop";
                if ((traits & TerrainTraits.Occupied) != 0) return "terrain_occupied";
                if ((traits & TerrainTraits.MapEdge) != 0) return "map_edge";
                if ((traits & TerrainTraits.Standable) == 0) return "terrain_not_standable";
            }
            return blocked.Contains(cell) ? "other_reserved_blocker" : "terrain_blocker";
        }

        private static IReadOnlyList<GridPoint> TurretRouteStarts(ProceduralLayout layout, GridPoint gate, string side)
        {
            List<GridPoint> starts = new List<GridPoint> { gate };
            List<P4BSallyPort> ports = (layout.ApproachDefense?.SallyPorts ?? Enumerable.Empty<P4BSallyPort>())
                .Concat(layout.FieldWall?.AccessPorts ?? Enumerable.Empty<P4BSallyPort>()).ToList();
            P4BSallyPort primary = ports.FirstOrDefault(p => p.Outer.Equals(gate));
            if (primary == null) return starts;
            starts.AddRange(primary.Doors);
            P4BSallyPort adjacent = ports.Where(p => !ReferenceEquals(p, primary) && p.Field == primary.Field &&
                p.Outward.Equals(primary.Outward)).OrderBy(p => Math.Abs(TangentProjection(p.Outer, primary.Outward) -
                    TangentProjection(primary.Outer, primary.Outward))).ThenBy(p => p.Outer).FirstOrDefault();
            if (adjacent != null) starts.AddRange(adjacent.Doors);
            return starts.Distinct().ToList();
        }

        private static int TangentProjection(GridPoint point, GridPoint outward) =>
            point.X * -outward.Z + point.Z * outward.X;

        private static int NormalProjection(GridPoint point, GridPoint outward) =>
            point.X * outward.X + point.Z * outward.Z;

        private static List<List<GridPoint>> OuterWallFaceWalks(ProceduralLayout layout, P4BAttackSide side)
        {
            HashSet<GridPoint> cells = new HashSet<GridPoint>(side.Faces);
            foreach (P4BSallyPort port in (layout.ApproachDefense?.SallyPorts ?? Enumerable.Empty<P4BSallyPort>())
                .Concat(layout.FieldWall?.AccessPorts ?? Enumerable.Empty<P4BSallyPort>())
                .Where(p => p.Outward.Equals(side.Outward))) cells.Add(port.Outer);
            if (layout.FieldWall != null && layout.Gates[layout.FieldWall.GateIndex].Outward.Equals(side.Outward))
                cells.Add(layout.FieldWall.OuterGate);
            foreach (LayoutGate gate in layout.Gates.Where(g => g.Outward.Equals(side.Outward)))
                cells.Add(gate.BandDoors.Concat(new[] { gate.Cell })
                    .OrderByDescending(c => NormalProjection(c, side.Outward)).First());

            List<List<GridPoint>> walks = new List<List<GridPoint>>();
            HashSet<GridPoint> remaining = new HashSet<GridPoint>(cells);
            while (remaining.Count > 0)
            {
                GridPoint root = remaining.OrderBy(c => c).First();
                Queue<GridPoint> queue = new Queue<GridPoint>();
                List<GridPoint> component = new List<GridPoint>();
                remaining.Remove(root); queue.Enqueue(root);
                while (queue.Count > 0)
                {
                    GridPoint here = queue.Dequeue(); component.Add(here);
                    foreach (GridPoint d in Cardinal)
                    {
                        GridPoint next = new GridPoint(here.X + d.X, here.Z + d.Z);
                        if (remaining.Remove(next)) queue.Enqueue(next);
                    }
                }
                walks.Add(component.OrderBy(c => TangentProjection(c, side.Outward))
                    .ThenBy(c => NormalProjection(c, side.Outward)).ThenBy(c => c).ToList());
            }
            return walks;
        }

        private static GridPoint OuterWallFace(ProceduralLayout layout, GridPoint gate, GridPoint outward)
        {
            P4BSallyPort port = (layout.ApproachDefense?.SallyPorts ?? Enumerable.Empty<P4BSallyPort>())
                .Concat(layout.FieldWall?.AccessPorts ?? Enumerable.Empty<P4BSallyPort>())
                .FirstOrDefault(p => p.Doors.Contains(gate));
            if (port != null) return port.Outer;
            if (layout.FieldWall != null && layout.FieldWall.OuterGate.Equals(gate)) return gate;
            foreach (LayoutGate perimeterGate in layout.Gates)
            {
                List<GridPoint> doors = perimeterGate.BandDoors.Concat(new[] { perimeterGate.Cell }).ToList();
                if (doors.Contains(gate))
                    return doors.OrderByDescending(c => c.X * outward.X + c.Z * outward.Z).First();
            }
            return gate;
        }

        private static List<GridPoint> Route(TerrainSnapshot terrain, GridPoint start, GridPoint goal,
            HashSet<GridPoint> blocked, IReadOnlyCollection<GridPoint> footprint) =>
            Routes(terrain, start, new[] { goal }, blocked, footprint).TryGetValue(goal, out List<GridPoint> route)
                ? route : null;

        private static IEnumerable<GridPoint> Arrival(ProceduralLayout layout) => layout.Gates.SelectMany(g => g.ExteriorRoute)
            .Concat(layout.FieldWall?.ArrivalRoute ?? Enumerable.Empty<GridPoint>());
        private static IEnumerable<GridPoint> FieldContent(P4BFieldWallPlan fieldWall) => fieldWall == null
            ? Enumerable.Empty<GridPoint>() : fieldWall.Walls.Concat(fieldWall.Fields).Concat(fieldWall.ExteriorPosts);

        // A quest camp's advertised guns carry what native gives them, no
        // reserve; settlement guns keep the reload reserve.
        private static int ReserveFuel(ProceduralLayoutInput input, CatalogDef def) =>
            input.QuestContract != null ? 0 : P5Readiness.ReloadReserve(def.FuelCapacity, def.InitialFuelPercent,
                def.FuelPerShot, def.FuelMultiplier, (int)Math.Ceiling(def.FuelCapacity / def.FuelMultiplier));

        private static IEnumerable<GridPoint> DefendedAssets(ProceduralLayout layout) =>
            (layout.FieldWall?.ExteriorPosts ?? Enumerable.Empty<GridPoint>())
                .Concat(P4BApproachDefensePlanner.AccessGates(layout))
                .Concat(layout.ApproachDefense?.AllPosts ?? new GridPoint[0])
                .Concat(layout.ApproachDefense?.CoverCells ?? new GridPoint[0]);

        private static IEnumerable<GridPoint> BlastClearanceAssets(ProceduralLayout layout) =>
            (layout.FieldWall?.ExteriorPosts ?? Enumerable.Empty<GridPoint>())
                .Concat(P4BApproachDefensePlanner.AccessGates(layout))
                .Concat(layout.ApproachDefense?.AllPosts ?? new GridPoint[0]);
        private static IEnumerable<GridPoint> ApproachTargets(ProceduralLayoutInput input, ProceduralLayout layout)
        {
            if (layout.ApproachDefense != null)
                return layout.ApproachDefense.Sides.Where(s => s.Exposed).SelectMany(s => s.Samples).Distinct();
            P4BFieldWallPlan wall = layout.FieldWall;
            GridPoint outward = layout.Gates[wall.GateIndex].Outward, tangent = new GridPoint(-outward.Z, outward.X);
            return Enumerable.Range(10, 26).SelectMany(k => new[] { -4, 0, 4 }
                .Select(t => At(wall.OuterGate, outward, tangent, k, t))).Where(c => Walkable(input.Terrain, c));
        }

        private static int ImportValue(TechCatalog catalog, CatalogDef turret, CatalogDef fuel, int fuelUnits)
        {
            double total = fuelUnits * fuel.MarketValue;
            foreach (KeyValuePair<string, int> cost in turret.CostList)
            {
                if (!catalog.TryGet(cost.Key, out CatalogDef item)) return int.MaxValue;
                total += cost.Value * item.MarketValue;
            }
            if (turret.CostStuffCount > 0 && catalog.TryGet("Steel", out CatalogDef steel))
                total += turret.CostStuffCount * steel.MarketValue;
            return total >= int.MaxValue ? int.MaxValue : (int)Math.Ceiling(total);
        }

        private static int Manhattan(GridPoint a, GridPoint b) => Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z);
        internal static int AvailableWatts(ProceduralLayout layout) => layout.Power == null ? 0 : Math.Min(
            layout.Power.Generators * layout.Power.GeneratorWatts + layout.Power.FirmWatts - layout.Power.CriticalWatts,
            layout.Power.Generators * layout.Power.GeneratorWatts + layout.Power.FirmWatts -
                layout.Power.CriticalWatts - layout.Power.NonCriticalWatts);

        internal static P4BTurretPlan RemoveLeastValuableCrossing(ProceduralLayoutInput input,
            ProceduralLayout layout, P4BTurretPlan network, out GridPoint? removedGate)
        {
            removedGate = null;
            if (input == null || layout == null || network == null) return network;
            var least = network.Groups.Where(g => g.AccessGate.HasValue)
                .Select(g => new
                {
                    Gate = g.AccessGate.Value,
                    Exposure = (layout.ApproachDefense?.Posts ?? Enumerable.Empty<P4BPost>())
                        .Where(p => p.AccessGate.Equals(g.AccessGate.Value))
                        .SelectMany(p => layout.ApproachDefense.Sides.Where(s => s.Exposed && s.Id == p.Side))
                        .Select(s => s.Samples.Count).DefaultIfEmpty(0).Max()
                })
                .GroupBy(x => x.Gate).Select(g => new { Gate = g.Key, Exposure = g.Max(x => x.Exposure) })
                .OrderBy(x => x.Exposure).ThenBy(x => x.Gate).FirstOrDefault();
            if (least == null) return network;
            removedGate = least.Gate;
            return Combine(input.Catalog, network.Groups.Where(g =>
                !g.AccessGate.HasValue || !g.AccessGate.Value.Equals(least.Gate))
                .Select(g => StandaloneGroup(input.Catalog, g)));
        }

        private static P4BTurretPlan StandaloneGroup(TechCatalog catalog, P4BTurretPlan group)
        {
            catalog.TryGet(group.DefName, out CatalogDef def);
            int value = group.ImportValue - (group.Support?.ImportValue ?? 0) -
                group.AdditionalGroups.Sum(g => g.ImportValue);
            return new P4BTurretPlan(def, group.Origin, group.Footprint, group.ServiceCell,
                group.ServiceRoute, group.FuelDefName, group.LoadedFuelUnits, group.ReserveFuelUnits,
                value, group.Support, group.CoverCells, group.AccessGate, group.Outward, group.Side,
                betweenNests: group.BetweenNests, spacingLevel: group.SpacingLevel);
        }

        // Shared wiring reservation: the field's inner gate/lane is a real
        // connection, not whichever prefix the civilian arrival route happens to use.
        public static HashSet<GridPoint> PowerAccessCells(ProceduralLayout layout, P4BTurretPlan network = null) => new HashSet<GridPoint>(layout.Enclosure
            .Concat(layout.BuiltWalls)
            .Concat(layout.OuterWalls)
            .Concat(layout.Gates.SelectMany(g => g.BandWalls))
            .Concat(layout.FieldWall?.Walls ?? Enumerable.Empty<GridPoint>())
            .Concat(layout.ExhaustPocketWalls)
            .Concat(layout.ApproachDefense?.SallyPorts.SelectMany(p => p.Doors) ?? Enumerable.Empty<GridPoint>())
            .Concat(layout.ApproachDefense?.SallyPorts.SelectMany(p => p.InteriorRoute) ?? new GridPoint[0])
            .Concat(layout.Gates.Select(g => g.Cell))
            .Concat(layout.Gates.SelectMany(g => g.BandDoors.Concat(g.OuterVestibule)))
            .Concat(layout.FieldWall == null ? Enumerable.Empty<GridPoint>() : layout.FieldWall.Access
                .Concat(new[] { layout.FieldWall.InnerGate.Value, layout.FieldWall.OuterGate })
                .Concat(layout.FieldWall.ArrivalRoute.TakeWhile(c => !c.Equals(layout.FieldWall.OuterGate))))
            .Concat((network ?? layout.Turret)?.All.SelectMany(t => t.ServiceRoute.Concat(t.Footprint)) ?? Enumerable.Empty<GridPoint>()));

        public static bool PowerTerminalsConnected(ProceduralLayoutInput input, ProceduralLayout layout, P4BTurretPlan network)
        {
            GridPoint[] terminals = layout.AllFixtures.Where(f => f.Watts != 0 ||
                f.Kind == LayoutFixtureKind.Generator || f.Kind == LayoutFixtureKind.Battery).Select(f => f.Cells[0])
                .Concat(layout.PowerYards.SelectMany(y => y.Cells.Take(1)))
                .Concat(network?.All.SelectMany(t => t.Footprint.Take(1)) ?? Enumerable.Empty<GridPoint>())
                .Distinct().ToArray();
            if (terminals.Length == 0) return false;
            HashSet<GridPoint> allowed = PowerAccessCells(layout, network);
            allowed.UnionWith(layout.PowerYards.SelectMany(y => y.Cells));
            allowed.UnionWith(terminals);
            HashSet<GridPoint> natural = new HashSet<GridPoint>(layout.NaturalWalls.Concat(layout.Rooms.SelectMany(r => r.NaturalWalls)));
            bool Legal(GridPoint c) => allowed.Contains(c) && input.Terrain.InBounds(c) && !natural.Contains(c) &&
                input.Terrain[c].Has(TerrainTraits.Buildable) && !input.Terrain[c].Has(TerrainTraits.Water) &&
                (!input.Terrain[c].Has(TerrainTraits.NaturalRock) || layout.Excavation.Contains(c));
            Queue<GridPoint> queue = new Queue<GridPoint>();
            HashSet<GridPoint> reached = new HashSet<GridPoint> { terminals[0] };
            queue.Enqueue(terminals[0]);
            while (queue.Count > 0)
            {
                GridPoint here = queue.Dequeue();
                foreach (GridPoint d in Cardinal)
                {
                    GridPoint next = new GridPoint(here.X + d.X, here.Z + d.Z);
                    if (Legal(next) && reached.Add(next)) queue.Enqueue(next);
                }
            }
            return terminals.All(reached.Contains);
        }

        private static int BaseMaterialValue(ProceduralLayoutInput input, ProceduralLayout layout) =>
            P4BApproachDefensePlanner.Materials(input, layout).ImportValue;

        private static bool CanShoot(P4BTurretPlan turret, GridPoint target, HashSet<GridPoint> opaque)
        {
            HashSet<GridPoint> blockers = new HashSet<GridPoint>(opaque);
            blockers.ExceptWith(turret.Footprint);
            return P4BFireLane.CanEngage(turret.Origin, target,
                (int)Math.Ceiling(turret.MinimumRange), (int)Math.Floor(turret.MaximumRange), blockers);
        }


        private static P4BTurretPlan Combine(TechCatalog catalog, IEnumerable<P4BTurretPlan> groups)
        {
            List<P4BTurretPlan> list = groups.ToList();
            if (list.Count == 0) return null;
            P4BTurretPlan first = list[0];
            catalog.TryGet(first.DefName, out CatalogDef def);
            return new P4BTurretPlan(def, first.Origin, first.Footprint, first.ServiceCell, first.ServiceRoute,
                first.FuelDefName, first.LoadedFuelUnits, first.ReserveFuelUnits,
                first.ImportValue - (first.Support?.ImportValue ?? 0), first.Support, first.CoverCells,
                first.AccessGate, first.Outward, first.Side, list.Skip(1), first.BetweenNests, first.SpacingLevel);
        }
        private static double Distance(GridPoint a, GridPoint b) =>
            Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
    }
}
