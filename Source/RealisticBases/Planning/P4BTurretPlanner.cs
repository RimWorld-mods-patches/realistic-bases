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
            GridPoint? outward = null, string side = null, IEnumerable<P4BTurretPlan> additionalGroups = null)
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
            internal string Side;
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
        {
            if (input == null || layout?.FieldWall == null || turret == null) return new[] { "missing_plan" };
            List<string> errors = new List<string>();
            List<P4BTurretPlan> guns = turret.All.ToList();
            if (turret.AdditionalGroups.Any(g => g.AdditionalGroups.Count > 0) ||
                guns.Any(g => g.Support?.AdditionalGroups.Count > 0)) errors.Add("nested_groups");
            foreach (P4BTurretPlan group in turret.Groups)
            {
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
                            Distance(c, d) < Math.Max(a.ExplosionRadius, b.ExplosionRadius) + 2)))
                        errors.Add("network_collision_or_blast");
                }
            if (BaseMaterialValue(input, layout) + turret.ImportValue > input.Requirements.ImportValueLimit)
                errors.Add("network_import_budget");
            if (layout.Power == null || AvailableWatts(layout) < turret.TotalWatts) errors.Add("network_power_budget");
            if (!PowerTerminalsConnected(input, layout, turret)) errors.Add("network_power_access");
            HashSet<GridPoint> finalOpaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
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
            IEnumerable<GridPoint> posts = (layout.FieldWall.UsablePosts ?? new GridPoint[0])
                .Concat(layout.ApproachDefense?.AllPosts ?? new GridPoint[0]).Distinct();
            foreach (P4BTurretPlan group in turret.Groups)
            {
                // A projecting group must share actual reachable approach targets with another
                // gun or an adjacent-cover pawn post. Proximity alone is not mutual support.
                int sharedShots = ApproachTargets(input, layout).Count(c => CanShoot(group, c, finalOpaque) &&
                    (guns.Any(t => !ReferenceEquals(t, group) && CanShoot(t, c, finalOpaque)) ||
                     posts.Any(p => P4BFireLane.CanEngage(p, c, 0, 25, finalOpaque))));
                if (sharedShots < 4) errors.Add("network_isolated_group");
            }
            // Interlocking gun fire is not enough: attackers can close on an
            // isolated emplacement while its defenders stand out of range.
            // Require each physical gun (including a heavy gun's mini-turret)
            // to be near a post that can shoot edge-reachable ground around it.
            if (layout.ApproachDefense != null && guns.Any(g =>
                !NearbyPawnSupport(layout.ApproachDefense, g, finalOpaque)))
                errors.Add("turret_without_nearby_pawn_support");
            if (layout.ApproachDefense != null && guns.Any(g =>
            {
                int threats, answered;
                SniperGuardCoverage(layout.ApproachDefense, g, finalOpaque, out threats, out answered, out _);
                return threats == 0 || answered * 10 < threats * 9;
            })) errors.Add("turret_exposed_to_unanswered_sniper_fire");
            if (layout.ApproachDefense != null)
            {
                P4BStaffingAudit staffing = P4BStaffing.Audit(input, layout, turret, loadout);
                if (staffing.GuardedGuns < staffing.Guns) errors.Add("network_concurrent_sniper_staffing");
                if (staffing.CloseSupportedGuns < staffing.Guns) errors.Add("network_concurrent_close_staffing");
            }
            return errors.Distinct().ToList();
        }

        internal static bool NearbyPawnSupport(P4BApproachDefensePlan defense,
            P4BTurretPlan gun, HashSet<GridPoint> opaque)
        {
            P4BAttackSide side = defense.Sides.FirstOrDefault(s => s.Id == gun.Side && s.Exposed);
            return side != null && side.Positions.Any(post => gun.Footprint.Min(c => Distance(post, c)) <= 14 &&
                side.Samples.Count(target => gun.Footprint.Min(c => Distance(c, target)) <= 18 &&
                    P4BFireLane.CanEngage(post, target, 0, 25, opaque)) >= 2);
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
            if (input == null || layout?.FieldWall == null || turret == null)
                return new[] { "missing_plan" };
            P4BFieldWallPlan fieldWall = layout.FieldWall;
            if (!fieldWall.InnerGate.HasValue) return new[] { "missing_inner_gate" };
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
                .Concat(fieldWall.Walls).Concat(fieldWall.Fields).Concat(fieldWall.ExteriorPosts)
                .Concat(fieldWall.CoverCells));
            occupied.UnionWith(layout.ApproachDefense?.CoverCells ?? new GridPoint[0]);
            occupied.UnionWith(layout.ApproachDefense?.AllPosts ?? new GridPoint[0]);
            occupied.UnionWith(layout.ExtraObstacles);
            occupied.UnionWith(additionalBlocked ?? Enumerable.Empty<GridPoint>());
            HashSet<GridPoint> opaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
            opaque.UnionWith(otherGuns.SelectMany(t => t.Footprint));
            opaque.UnionWith(turret.Support?.Footprint ?? Enumerable.Empty<GridPoint>());
            occupied.UnionWith(opaque.Except(P4BApproachDefensePlanner.AccessGates(layout))
                .Where(c => !c.Equals(fieldWall.InnerGate.Value)));
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
            if (turret.ServiceRoute.Count < 2 || turret.ServiceRoute.Count > 160 ||
                !turret.ServiceRoute[0].Equals(accessGate) ||
                !turret.ServiceRoute.Last().Equals(turret.ServiceCell) ||
                turret.ServiceRoute.Skip(1).Any(c => occupied.Contains(c) || footprint.Contains(c) || !Walkable(input.Terrain, c)) ||
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
            if (DefendedAssets(layout)
                .Any(c => footprint.Any(f => Distance(f, c) < blastClearance))) errors.Add("blast_clearance");
            int usefulTargets = ApproachTargets(input, layout).Count(c => P4BFireLane.CanEngage(turret.Origin, c,
                (int)Math.Ceiling(def.MinimumRange), (int)Math.Floor(def.MaximumRange), opaque));
            if (usefulTargets < 4) errors.Add("fire_lane");
            if (layout.Power == null ||
                layout.Power.Generators * layout.Power.GeneratorWatts - layout.Power.CriticalWatts < turret.TotalWatts ||
                layout.Power.Generators * layout.Power.GeneratorWatts + layout.Power.RenewableWatts -
                    layout.Power.CriticalWatts - layout.Power.NonCriticalWatts < turret.TotalWatts ||
                turret.Watts != def.BasePowerConsumption) errors.Add("power_budget");
            if (turret.LoadedFuelUnits != (int)Math.Ceiling(def.FuelCapacity * def.InitialFuelPercent / def.FuelMultiplier) ||
                turret.ReserveFuelUnits != (int)Math.Ceiling(def.FuelCapacity / def.FuelMultiplier) ||
                turret.ImportValue != ImportValue(input.Catalog, def, fuel,
                    turret.LoadedFuelUnits + turret.ReserveFuelUnits) +
                    CoverImportValue(input, fieldWall.CoverDefName, turret.CoverCells.Count) +
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
            if (!garrison.Success || !garrison.Residents.Any(r => r.Weapon?.DefName == "Gun_SniperRifle"))
            {
                diagnostic = "unplaced: no_equipped_sniper_guard";
                return null;
            }
            List<P4BTurretPlan> groups = new List<P4BTurretPlan>();
            List<string> decisions = new List<string>();
            int fronts = layout.ApproachDefense?.Sides.Count(s => s.Exposed) ?? 1;
            // The exposed-face count bounds group count; no fixed turret count or random field.
            for (int i = 0; i < Math.Min(4, fronts); i++)
            {
                P4BTurretPlan next = PlanGroup(input, layout, fieldWall, occupied, groups, garrison, out string reason);
                decisions.Add(reason);
                if (next == null) break;
                groups.Add(next);
            }
            P4BTurretPlan network = Combine(input.Catalog, groups);
            HashSet<GridPoint> finalOpaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
            finalOpaque.UnionWith(network?.All.SelectMany(t => t.Footprint) ?? Enumerable.Empty<GridPoint>());
            List<int> coverage = ApproachTargets(input, layout).Select(c => network?.All.Count(t =>
                CanShoot(t, c, finalOpaque)) ?? 0).ToList();
            P4BStaffingAudit staffed = network == null ? null : P4BStaffing.Audit(input, layout, network, garrison);
            diagnostic = (network == null ? "unplaced" : "accepted") + " groups=" + groups.Count +
                " turrets=" + (network?.All.Count() ?? 0) + " watts=" + (network?.TotalWatts ?? 0) +
                " value=" + (network?.ImportValue ?? 0) + " uncovered=" + coverage.Count(n => n == 0) +
                " single=" + coverage.Count(n => n == 1) + " overlapping=" + coverage.Count(n => n >= 2) +
                " staffedGuards=" + (staffed?.GuardedGuns ?? 0) + "/" + (staffed?.Guns ?? 0) +
                " staffedClose=" + (staffed?.CloseSupportedGuns ?? 0) + "/" + (staffed?.Guns ?? 0) +
                " sniperPosts=" + (staffed?.SniperPosts.Count ?? 0) +
                " linePosts=" + (staffed?.LinePosts.Count ?? 0) +
                " mortarCrew=" + (staffed?.MortarCrew ?? 0) + " mobileReserve=" + (staffed?.MobileReserve ?? 0) +
                " decisions=[" + string.Join(";", decisions) + "]";
            return network;
        }

        private static P4BTurretPlan PlanGroup(ProceduralLayoutInput input, ProceduralLayout layout,
            P4BFieldWallPlan fieldWall, IEnumerable<GridPoint> occupied, List<P4BTurretPlan> groups,
            SettlementLoadout garrison, out string diagnostic)
        {
            if (input == null || layout == null || fieldWall == null) throw new ArgumentNullException();
            if (!fieldWall.InnerGate.HasValue)
            { diagnostic = "field_gate_unavailable"; return null; }
            if (layout.Power == null || input.Equipment.Kit.Stage.Level < SettlementTechStageLevel.IndustrialMid)
            { diagnostic = "stage_or_power_unavailable"; return null; }
            HashSet<GridPoint> blocked = new HashSet<GridPoint>(occupied ?? Enumerable.Empty<GridPoint>());
            List<P4BTurretPlan> previous = groups.SelectMany(g => g.All).ToList();
            int spentValue = groups.Sum(g => g.ImportValue), spentWatts = previous.Sum(g => g.Watts);
            int baseValue = BaseMaterialValue(input, layout);
            blocked.UnionWith(previous.SelectMany(t => t.Footprint.Concat(t.CoverCells)));
            blocked.UnionWith(fieldWall.Walls);
            blocked.UnionWith(fieldWall.CoverCells);
            blocked.UnionWith(fieldWall.ExteriorPosts);
            blocked.UnionWith(fieldWall.Fields);
            blocked.UnionWith(layout.ApproachDefense?.CoverCells ?? new GridPoint[0]);
            blocked.UnionWith(layout.ApproachDefense?.AllPosts ?? new GridPoint[0]);
            HashSet<GridPoint> opaque = P4BApproachDefensePlanner.Opaque(layout, input.Terrain);
            opaque.UnionWith(previous.SelectMany(t => t.Footprint));
            // A service route may pass a door, but cannot pass through either
            // perimeter's wall even if the caller omitted wall cells from its
            // occupancy set (as the joint-layout packer deliberately does).
            blocked.UnionWith(opaque.Except(P4BApproachDefensePlanner.AccessGates(layout))
                .Where(c => !c.Equals(fieldWall.InnerGate.Value)));
            GridPoint inner = fieldWall.InnerGate.Value, outer = fieldWall.OuterGate;
            GridPoint outward = new GridPoint(Math.Sign(outer.X - inner.X), Math.Sign(outer.Z - inner.Z));
            GridPoint tangent = new GridPoint(-outward.Z, outward.X);
            List<Frame> frames = new List<Frame>();
            string fieldSide = outward.X != 0 ? (outward.X < 0 ? "west" : "east") : (outward.Z < 0 ? "south" : "north");
            if (layout.ApproachDefense == null)
                frames.Add(new Frame { Anchor = outer, Gate = outer, Outward = outward, Side = fieldSide });
            if (layout.ApproachDefense != null)
                frames.AddRange(layout.ApproachDefense.Sides.Where(s => s.Exposed).SelectMany(s => s.Positions.Select(post =>
                {
                    P4BPerimeterPost built = layout.ApproachDefense.Posts.FirstOrDefault(p => p.Cell.Equals(post));
                    return new Frame { Anchor = post, Gate = built?.AccessGate ?? fieldWall.OuterGate,
                        Outward = s.Outward, Side = s.Id };
                })));
            List<GridPoint> targets = ApproachTargets(input, layout).ToList();
            Dictionary<GridPoint, int> coverage = targets.ToDictionary(c => c, c => previous.Count(t =>
                CanShoot(t, c, opaque)));
            P4BStaffingAudit currentStaffing = groups.Count == 0 ? null :
                P4BStaffing.Audit(input, layout, Combine(input.Catalog, groups), garrison);
            List<GridPoint> staffedPosts = currentStaffing == null ? new List<GridPoint>() :
                currentStaffing.SniperPosts.Concat(currentStaffing.LinePosts).ToList();
            Dictionary<string, int> rejected = new Dictionary<string, int>(StringComparer.Ordinal);
            input.Catalog.TryGet("Turret_Autocannon", out CatalogDef autocannon);
            input.Catalog.TryGet("Turret_Sniper", out CatalogDef slug);
            IReadOnlyList<string> order = HeavyWeaponOrder(targets, frames.Select(f => f.Anchor),
                previous.Select(g => g.DefName), autocannon?.MaximumRange ?? 0, slug?.MaximumRange ?? 0);
            foreach (string name in order)
            {
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
                int reserveUnits = (int)Math.Ceiling(def.FuelCapacity / def.FuelMultiplier);
                int value = ImportValue(input.Catalog, def, fuel, loadedUnits + reserveUnits);
                if (value == int.MaxValue) { Reject("construction_cost_unresolved"); continue; }
                if (baseValue + spentValue + value > input.Requirements.ImportValueLimit)
                { Reject("import_budget"); continue; }
                // Score all eligible fronts before route/support construction.
                // A deterministic bounded candidate set replaces the old
                // field-gate-first rule. No gun is needed on every side.
                var candidates = frames.SelectMany(frame => new[] { -1, 1 }.SelectMany(side =>
                    new[] { 1, 2, 3, 4, 5, 7, 9, 11, 13, 15 }.SelectMany(forward => new[] { 8, 11, 14, 17, 20 }.Select(lateral => new
                    { Frame = frame, Side = side, Origin = At(frame.Anchor, frame.Outward,
                        new GridPoint(-frame.Outward.Z, frame.Outward.X), forward, side * lateral) }))))
                    .Select(c => new { c.Frame, c.Side, c.Origin,
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
                    .Where(c => c.Shots >= 4 && c.FrontShots >= 4 && c.Gain >= 4)
                    .Where(c => layout.ApproachDefense == null || layout.ApproachDefense.Sides.Any(s =>
                        s.Id == c.Frame.Side && s.Positions.Any(p => Distance(p, c.Origin) <= 14)))
                    .OrderByDescending(c => c.Gain + c.MutualShots * 3 + c.FrontOpenGround / 8 -
                        Manhattan(c.Origin, c.Frame.Gate) / 4)
                    .ThenByDescending(c => c.FrontShots).ThenByDescending(c => c.Gain)
                    .ThenByDescending(c => c.FrontExposure)
                    .ThenByDescending(c => c.Sides).ThenByDescending(c => c.Shots)
                    .ThenByDescending(c => c.NewFront)
                    .ThenBy(c => Manhattan(c.Origin, c.Frame.Gate))
                    .ThenBy(c => c.Frame.Side, StringComparer.Ordinal).ThenBy(c => c.Origin).ThenBy(c => c.Side);
                foreach (var candidate in candidates)
                        {
                            Frame frame = candidate.Frame;
                            GridPoint origin = candidate.Origin;
                            int side = candidate.Side;
                            GridPoint facing = frame.Outward, across = new GridPoint(-facing.Z, facing.X);
                            List<GridPoint> footprint = Enumerable.Range(0, def.SizeX).SelectMany(x =>
                                Enumerable.Range(0, def.SizeZ).Select(z =>
                                    new GridPoint(origin.X + x, origin.Z + z))).ToList();
                            if (footprint.Any(c => blocked.Contains(c) || opaque.Contains(c) ||
                                Arrival(layout).Contains(c) || !Buildable(input.Terrain, c) ||
                                previous.Any(t => t.ServiceRoute.Contains(c)) ||
                                (layout.ApproachDefense?.Posts.Any(p => p.Route.Contains(c)) ?? false)))
                            { Reject("footprint"); continue; }
                            double blastClearance = def.ExplosionRadius + 2;
                            if (DefendedAssets(layout)
                                .Any(c => footprint.Any(f => Distance(f, c) < blastClearance)) ||
                                previous.Any(t => footprint.Any(f => t.Footprint.Any(c =>
                                    Distance(f, c) < Math.Max(def.ExplosionRadius, t.ExplosionRadius) + 2))))
                            { Reject("blast_clearance"); continue; }
                            foreach (GridPoint service in footprint.SelectMany(c => Cardinal.Select(d =>
                                new GridPoint(c.X + d.X, c.Z + d.Z))).Distinct()
                                .OrderBy(c => Manhattan(c, frame.Gate)).ThenBy(c => c))
                            {
                                if (footprint.Contains(service) || blocked.Contains(service) || !Walkable(input.Terrain, service))
                                    continue;
                                List<GridPoint> route = Route(input.Terrain, frame.Gate, service, blocked, footprint);
                                if (route == null) continue;
                                List<GridPoint> cover = CoverFor(input.Terrain, layout, footprint, route,
                                    blocked, opaque, facing, across, side);
                                if (cover == null || previous.Any(t => t.ServiceRoute.Intersect(cover).Any()))
                                { Reject("turret_cover"); continue; }
                                int coverValue = CoverImportValue(input, fieldWall.CoverDefName, cover.Count);
                                if (coverValue == int.MaxValue || baseValue + spentValue + value + coverValue >
                                    input.Requirements.ImportValueLimit) { Reject("cover_budget"); continue; }
                                P4BTurretPlan support = null;
                                if (def.MinimumRange > 0)
                                {
                                    support = PlanCloseSupport(input, layout, fieldWall, blocked, opaque,
                                        footprint, route, cover, def, value + coverValue, side, frame, spentValue, spentWatts, previous);
                                    if (support == null) { Reject("blind_zone_support"); continue; }
                                }
                                P4BTurretPlan proposed = new P4BTurretPlan(def, origin, footprint, service, route, fuel.DefName,
                                    loadedUnits, reserveUnits, value + coverValue, support, cover,
                                    frame.Gate, facing, frame.Side);
                                P4BTurretPlan network = Combine(input.Catalog, groups.Concat(new[] { proposed }));
                                IReadOnlyList<string> networkErrors = Validate(input, layout, network, null, garrison);
                                if (networkErrors.Count > 0) { Reject("network_" + networkErrors[0]); continue; }
                                ProceduralLayout armed = layout.WithTurret(input, network);
                                if (P4BApproachDefensePlanner.Validate(input, armed).Count > 0)
                                { Reject("post_lane_obstructed"); continue; }
                                diagnostic = "accepted " + name + " side=" + frame.Side + " gain=" + candidate.Gain +
                                    " staffedMutual=" + candidate.MutualShots + " approachSides=" + candidate.Sides +
                                    " approachSamples=" + candidate.Shots + " at=" + origin + " value=" + value +
                                    " loaded=" + loadedUnits + " reserve=" + reserveUnits +
                                    (support == null ? string.Empty : " supportedBy=" + support.DefName);
                                return proposed;
                            }
                            Reject("service_route");
                        }
            }
            diagnostic = string.Join(",", rejected.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => kv.Key + ":" + kv.Value));
            if (diagnostic.Length == 0) diagnostic = "no_useful_unsaturated_approach";
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
            IReadOnlyList<P4BTurretPlan> previous)
        {
            if (!input.Catalog.TryGet("Turret_MiniTurret", out CatalogDef mini) ||
                !input.Catalog.IsLegal(mini, input.Equipment.Kit.Stage) || !mini.AutonomousTurret ||
                mini.FuelCapacity <= 0 || mini.FuelMultiplier <= 0 || mini.FuelPerShot <= 0 ||
                mini.Fuel.Count != 1 ||
                !input.Catalog.TryGet(mini.Fuel[0], out CatalogDef fuel) ||
                !input.Catalog.IsLegal(fuel, input.Equipment.Kit.Stage)) return null;
            int loaded = (int)Math.Ceiling(mini.FuelCapacity * mini.InitialFuelPercent / mini.FuelMultiplier);
            int reserve = (int)Math.Ceiling(mini.FuelCapacity / mini.FuelMultiplier);
            int value = ImportValue(input.Catalog, mini, fuel, loaded + reserve);
            int baseValue = BaseMaterialValue(input, layout);
            if (value == int.MaxValue || baseValue + spentValue + heavyValue + value >
                input.Requirements.ImportValueLimit) return null;
            int watts = spentWatts + heavyDef.BasePowerConsumption + mini.BasePowerConsumption;
            if (layout.Power.Generators * layout.Power.GeneratorWatts - layout.Power.CriticalWatts < watts ||
                layout.Power.Generators * layout.Power.GeneratorWatts + layout.Power.RenewableWatts -
                    layout.Power.CriticalWatts - layout.Power.NonCriticalWatts < watts) return null;
            GridPoint outer = frame.Anchor, outward = frame.Outward,
                tangent = new GridPoint(-outward.Z, outward.X);
            HashSet<GridPoint> supportBlocked = new HashSet<GridPoint>(blocked);
            supportBlocked.UnionWith(heavy);
            supportBlocked.UnionWith(heavyCover);
            foreach (int forward in new[] { 6, 7, 5, 8, 4, 3 })
                for (int lateral = 5; lateral <= 19; lateral += 2)
                {
                    GridPoint origin = At(outer, outward, tangent, forward, -heavySide * lateral);
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
                    if (DefendedAssets(layout)
                        .Any(c => footprint.Any(f => Distance(c, f) < mini.ExplosionRadius + 2)) ||
                        footprint.Any(f => heavy.Any(h => Distance(f, h) <
                            Math.Max(heavyDef.ExplosionRadius, mini.ExplosionRadius) + 2)) ||
                        previous.Any(t => footprint.Any(f => t.Footprint.Any(c =>
                            Distance(f, c) < Math.Max(mini.ExplosionRadius, t.ExplosionRadius) + 2)))) continue;
                    if (!CloseSupport(input.Terrain, layout, heavy, heavyDef.MinimumRange,
                        footprint, mini.MaximumRange, opaque)) continue;
                    foreach (GridPoint service in footprint.SelectMany(c => Cardinal.Select(d =>
                        new GridPoint(c.X + d.X, c.Z + d.Z))).Distinct()
                        .OrderBy(c => Manhattan(c, frame.Gate)).ThenBy(c => c))
                    {
                        if (footprint.Contains(service) || supportBlocked.Contains(service) ||
                            !Walkable(input.Terrain, service)) continue;
                        List<GridPoint> route = Route(input.Terrain, frame.Gate, service, supportBlocked, footprint);
                        if (route == null || route.Intersect(heavy).Any()) continue;
                        List<GridPoint> cover = CoverFor(input.Terrain, layout, footprint, route,
                            supportBlocked, opaque, outward, tangent, -heavySide);
                        if (cover == null || cover.Intersect(heavyRoute).Any() ||
                            previous.Any(t => t.ServiceRoute.Intersect(cover).Any())) continue;
                        int coverValue = CoverImportValue(input, fieldWall.CoverDefName, cover.Count);
                        if (coverValue == int.MaxValue || baseValue + spentValue + heavyValue + value +
                            coverValue > input.Requirements.ImportValueLimit) continue;
                        return new P4BTurretPlan(mini, origin, footprint, service, route,
                            fuel.DefName, loaded, reserve, value + coverValue, coverCells: cover,
                            accessGate: frame.Gate, outward: outward, side: frame.Side);
                    }
                }
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
            IEnumerable<GridPoint> posts = (layout.FieldWall?.UsablePosts ?? new GridPoint[0])
                .Concat(layout.ApproachDefense?.AllPosts ?? new GridPoint[0]).Distinct();
            int pawnSupport = posts.Count(post => blind.Count(target =>
                P4BFireLane.CanEngage(post, target, 0, 25, blockers)) >= 2);
            return smallShots >= 4 && pawnSupport >= (layout.ApproachDefense == null ? 2 : 1);
        }

        private static List<GridPoint> CoverFor(TerrainSnapshot terrain, ProceduralLayout layout,
            IReadOnlyCollection<GridPoint> footprint, IReadOnlyCollection<GridPoint> serviceRoute,
            HashSet<GridPoint> blocked, HashSet<GridPoint> opaque,
            GridPoint outward, GridPoint tangent, int side)
        {
            P4BFieldWallPlan fieldWall = layout.FieldWall;
            List<GridPoint> front = footprint.Where(c => !footprint.Contains(new GridPoint(
                c.X + outward.X, c.Z + outward.Z))).Select(c => new GridPoint(
                c.X + outward.X, c.Z + outward.Z)).Distinct().ToList();
            bool legal(GridPoint c) => !footprint.Contains(c) && !blocked.Contains(c) &&
                !opaque.Contains(c) && !serviceRoute.Contains(c) && !Arrival(layout).Contains(c) &&
                !fieldWall.ExteriorPosts.Contains(c) && Buildable(terrain, c);
            if (front.Count == 0 || front.Any(c => !legal(c))) return null;
            List<GridPoint> cover = new List<GridPoint>(front);
            foreach (int sign in new[] { side, -side })
                foreach (GridPoint flank in footprint.Where(c => !footprint.Contains(new GridPoint(
                    c.X + sign * tangent.X, c.Z + sign * tangent.Z))).Select(c => new GridPoint(
                    c.X + sign * tangent.X, c.Z + sign * tangent.Z)))
                {
                    if (cover.Contains(flank)) continue;
                    if (!legal(flank)) return null;
                    cover.Add(flank);
                }
            return cover;
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

        private static List<GridPoint> Route(TerrainSnapshot terrain, GridPoint start, GridPoint goal,
            HashSet<GridPoint> blocked, IReadOnlyCollection<GridPoint> footprint)
        {
            Queue<GridPoint> queue = new Queue<GridPoint>();
            Dictionary<GridPoint, GridPoint> previous = new Dictionary<GridPoint, GridPoint>();
            queue.Enqueue(start); previous[start] = start;
            while (queue.Count > 0 && previous.Count < 4096)
            {
                GridPoint here = queue.Dequeue();
                if (here.Equals(goal))
                {
                    List<GridPoint> route = new List<GridPoint>();
                    for (GridPoint c = goal; !c.Equals(start); c = previous[c]) route.Add(c);
                    route.Add(start); route.Reverse();
                    return route.Count <= 160 ? route : null;
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
            return null;
        }

        private static IEnumerable<GridPoint> Arrival(ProceduralLayout layout) => layout.Gates.SelectMany(g => g.ExteriorRoute);
        private static IEnumerable<GridPoint> DefendedAssets(ProceduralLayout layout) =>
            layout.FieldWall.ExteriorPosts.Concat(layout.FieldWall.CoverCells)
                .Concat(P4BApproachDefensePlanner.AccessGates(layout))
                .Concat(layout.ApproachDefense?.AllPosts ?? new GridPoint[0])
                .Concat(layout.ApproachDefense?.CoverCells ?? new GridPoint[0]);
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
        private static int AvailableWatts(ProceduralLayout layout) => layout.Power == null ? 0 : Math.Min(
            layout.Power.Generators * layout.Power.GeneratorWatts - layout.Power.CriticalWatts,
            layout.Power.Generators * layout.Power.GeneratorWatts + layout.Power.RenewableWatts -
                layout.Power.CriticalWatts - layout.Power.NonCriticalWatts);

        // Shared wiring reservation: the field's inner gate/lane is a real
        // connection, not whichever prefix the civilian arrival route happens to use.
        public static HashSet<GridPoint> PowerAccessCells(ProceduralLayout layout, P4BTurretPlan network = null) => new HashSet<GridPoint>(layout.Enclosure
            .Concat(layout.ApproachDefense?.SallyPorts.Select(p => p.Cell) ?? new GridPoint[0])
            .Concat(layout.ApproachDefense?.SallyPorts.SelectMany(p => p.InteriorRoute) ?? new GridPoint[0])
            .Concat(layout.Gates.Select(g => g.Cell))
            .Concat(layout.Gates.SelectMany(g => g.BandDoors.Concat(g.OuterVestibule)))
            .Concat(layout.FieldWall == null ? Enumerable.Empty<GridPoint>() : layout.FieldWall.Access
                .Concat(new[] { layout.FieldWall.InnerGate.Value, layout.FieldWall.OuterGate })
                .Concat(layout.FieldWall.ArrivalRoute.TakeWhile(c => !c.Equals(layout.FieldWall.OuterGate))))
            .Concat((network ?? layout.Turret)?.All.SelectMany(t => t.ServiceRoute.Concat(t.Footprint)) ?? Enumerable.Empty<GridPoint>()));

        public static bool PowerTerminalsConnected(ProceduralLayoutInput input, ProceduralLayout layout, P4BTurretPlan network)
        {
            GridPoint[] roots = layout.AllFixtures.Where(f => f.Watts != 0 ||
                f.Kind == LayoutFixtureKind.Generator || f.Kind == LayoutFixtureKind.Battery).Select(f => f.Cells[0]).ToArray();
            if (roots.Length == 0) return false;
            HashSet<GridPoint> allowed = PowerAccessCells(layout, network);
            HashSet<GridPoint> natural = new HashSet<GridPoint>(layout.NaturalWalls.Concat(layout.Rooms.SelectMany(r => r.NaturalWalls)));
            bool Legal(GridPoint c) => allowed.Contains(c) && input.Terrain.InBounds(c) && !natural.Contains(c) &&
                input.Terrain[c].Has(TerrainTraits.Buildable) && !input.Terrain[c].Has(TerrainTraits.Water) &&
                (!input.Terrain[c].Has(TerrainTraits.NaturalRock) || layout.Excavation.Contains(c));
            Queue<GridPoint> queue = new Queue<GridPoint>();
            HashSet<GridPoint> reached = new HashSet<GridPoint> { roots[0] };
            queue.Enqueue(roots[0]);
            while (queue.Count > 0)
            {
                GridPoint here = queue.Dequeue();
                foreach (GridPoint d in Cardinal)
                {
                    GridPoint next = new GridPoint(here.X + d.X, here.Z + d.Z);
                    if (Legal(next) && reached.Add(next)) queue.Enqueue(next);
                }
            }
            return network.All.All(t => reached.Contains(t.Origin));
        }

        private static int BaseMaterialValue(ProceduralLayoutInput input, ProceduralLayout layout) =>
            P4BApproachDefensePlanner.Materials(input, layout, layout.ApproachDefense?.CoverCells.Count() ?? 0).ImportValue;

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
                first.AccessGate, first.Outward, first.Side, list.Skip(1));
        }
        private static double Distance(GridPoint a, GridPoint b) =>
            Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
    }
}
