using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    // Local material availability around the site (pure input). TerrainSnapshot
    // stays owned by P1/P4; the runtime adapter fills this separately.
    public sealed class LayoutLocalResources
    {
        public LayoutLocalResources(int localWoodYield, IEnumerable<string> localStoneStuffs = null, IEnumerable<GridPoint> riverCells = null,
            IEnumerable<GridPoint> steamGeyserCells = null)
        {
            if (localWoodYield < 0) throw new ArgumentOutOfRangeException(nameof(localWoodYield));
            LocalWoodYield = localWoodYield;
            LocalStoneStuffs = new ReadOnlyCollection<string>((localStoneStuffs ?? Enumerable.Empty<string>())
                .Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList());
            RiverCells = new ReadOnlyCollection<GridPoint>((riverCells ?? Enumerable.Empty<GridPoint>()).Distinct().OrderBy(c => c).ToList());
            SteamGeyserCells = new ReadOnlyCollection<GridPoint>((steamGeyserCells ?? Enumerable.Empty<GridPoint>()).Distinct().OrderBy(c => c).ToList());
            Digest = ComputeDigest();
        }

        // Moving-water cells (watermill candidates), sorted.
        public IReadOnlyList<GridPoint> RiverCells { get; }
        // Steam geyser positions (the vanilla 2x2 geyser occupies [x..x+1] x [z..z+1]), sorted.
        public IReadOnlyList<GridPoint> SteamGeyserCells { get; }

        // The 2x2 cells of every listed geyser (vanilla GenAdj.OccupiedRect for size 2x2).
        public IEnumerable<GridPoint> GeyserFootprints() =>
            SteamGeyserCells.SelectMany(g => new[] { g, new GridPoint(g.X + 1, g.Z), new GridPoint(g.X, g.Z + 1), new GridPoint(g.X + 1, g.Z + 1) }).Distinct();

        // Harvestable wood (WoodLog units) within reach of the site.
        public int LocalWoodYield { get; }
        // Stone block defs whose chunks are local (for example BlocksGranite), sorted ordinal.
        public IReadOnlyList<string> LocalStoneStuffs { get; }
        public string Digest { get; }

        // Unconstrained wood and no known stone: the default when the caller has no data.
        public static LayoutLocalResources Default { get; } = new LayoutLocalResources(int.MaxValue);

        private string ComputeDigest()
        {
            ulong hash = StableHash.Start;
            hash = StableHash.Add(hash, "p4a-local-resources");
            hash = StableHash.Add(hash, LocalWoodYield);
            hash = CatalogDef.AddList(hash, LocalStoneStuffs);
            foreach (IReadOnlyList<GridPoint> cells in new[] { RiverCells, SteamGeyserCells })
            {
                hash = StableHash.Add(hash, cells.Count);
                foreach (GridPoint c in cells)
                {
                    hash = StableHash.Add(hash, c.X);
                    hash = StableHash.Add(hash, c.Z);
                }
            }
            return StableHash.Hex(hash);
        }
    }

    // The resolved equipment kit for one settlement: roles at the final stage plus
    // the facts furnishing needs. Built only through LayoutEquipment.
    public sealed class LayoutKit
    {
        public const string BatteryDefName = "Battery";
        public const string MortarDefName = "Turret_Mortar";
        public const string CoolerThingClass = "Building_Cooler";

        // Roles whose fixtures the layout places; only these decide whether it needs power.
        public static IReadOnlyList<SettlementRole> FurnishingRoles { get; } = new ReadOnlyCollection<SettlementRole>(new[]
        {
            SettlementRole.Sleep, SettlementRole.MedicalBed, SettlementRole.MedicalMonitor, SettlementRole.Seat, SettlementRole.Table,
            SettlementRole.Light, SettlementRole.Heat, SettlementRole.Cool, SettlementRole.Cooking, SettlementRole.CraftSmithing,
            SettlementRole.CraftTailoring, SettlementRole.CraftMachining, SettlementRole.CraftFabrication, SettlementRole.CraftStonecutting,
            SettlementRole.Storage, SettlementRole.Recreation
        });

        // Workshop bench roles in placement order.
        public static IReadOnlyList<SettlementRole> CraftRoles { get; } = new ReadOnlyCollection<SettlementRole>(new[]
        {
            SettlementRole.CraftSmithing, SettlementRole.CraftTailoring, SettlementRole.CraftMachining, SettlementRole.CraftFabrication,
            SettlementRole.CraftStonecutting
        });

        internal LayoutKit(LayoutRequirements requirements, TechCatalog catalog, RoleResolutionSet roles, string generatorDefName = null)
        {
            Catalog = catalog;
            Roles = roles;
            Powered = FurnishingRoles.Select(DefFor).Any(d => d != null && d.BasePowerConsumption > 0);
            CatalogDef cool = DefFor(SettlementRole.Cool);
            Refrigerated = requirements.Refrigerated && cool != null && cool.ThingClass == CoolerThingClass;
            MortarCapable = requirements.Electric && catalog.IsLegal(MortarDefName, roles.Stage);
            List<CatalogDef> workshop = new List<CatalogDef>();
            foreach (SettlementRole role in CraftRoles)
            {
                CatalogDef def = DefFor(role);
                if (def != null && !workshop.Any(d => d.DefName == def.DefName)) workshop.Add(def);
            }
            WorkshopDefs = new ReadOnlyCollection<CatalogDef>(workshop);
            CatalogDef generator, battery;
            DispatchableOptions = new ReadOnlyCollection<CatalogDef>(!Powered ? new List<CatalogDef>() : LayoutPowerModel.DispatchableOrder
                .Select(n => catalog.TryGet(n, out generator) && catalog.IsLegal(generator, roles.Stage) && FuelOf(catalog, generator, roles.Stage) != null ? generator : null)
                .Where(d => d != null).ToList());
            Generator = DispatchableOptions.FirstOrDefault(d => generatorDefName == null || d.DefName == generatorDefName);
            GeneratorFuel = Generator == null ? null : FuelOf(catalog, Generator, roles.Stage);
            Battery = Powered && catalog.TryGet(BatteryDefName, out battery) && catalog.IsLegal(BatteryDefName, roles.Stage) ? battery : null;
            List<string> notes = new List<string>();
            if (requirements.Electric && !Powered) notes.Add("capability_exceeds_stage:Electricity");
            if (requirements.Refrigerated && !Refrigerated) notes.Add("refrigeration_unavailable_at_stage");
            Notes = new ReadOnlyCollection<string>(notes);
        }

        public TechCatalog Catalog { get; }
        public RoleResolutionSet Roles { get; }
        public SettlementTechStage Stage => Roles.Stage;
        public bool Powered { get; }
        public bool Refrigerated { get; }
        public bool MortarCapable { get; }
        // Distinct workshop bench defs (craft roles deduplicated), in CraftRoles order.
        public IReadOnlyList<CatalogDef> WorkshopDefs { get; }
        // Legal dispatchable generators whose fuel is also legal, in LayoutPowerModel.DispatchableOrder.
        public IReadOnlyList<CatalogDef> DispatchableOptions { get; }
        // The chosen dispatchable generator and its fuel item.
        public CatalogDef Generator { get; }
        public CatalogDef GeneratorFuel { get; }
        public CatalogDef Battery { get; }
        public int GeneratorWatts => Generator == null ? 0 : -Generator.BasePowerConsumption;
        public IReadOnlyList<string> Notes { get; }

        private static CatalogDef FuelOf(TechCatalog catalog, CatalogDef generator, SettlementTechStage stage)
        {
            foreach (string fuel in generator.Fuel)
            {
                CatalogDef def;
                if (catalog.TryGet(fuel, out def) && catalog.IsLegal(def, stage)) return def;
            }
            return null;
        }

        // The chosen catalog def of a role; null when unresolved or virtual (a zone).
        public CatalogDef DefFor(SettlementRole role)
        {
            RoleResolution resolution = Roles.Get(role);
            if (resolution == null || !resolution.Resolved || resolution.Chosen.Virtual) return null;
            CatalogDef def;
            return Catalog.TryGet(resolution.ChosenDefName, out def) ? def : null;
        }

        public bool Resolved(SettlementRole role)
        {
            RoleResolution resolution = Roles.Get(role);
            return resolution != null && resolution.Resolved;
        }
    }

    // Construction and fuel materials of a layout (or of the pre-search estimate).
    // Only wood and stone are accounted; CostList items are summarized, not charged (step 6).
    public sealed class LayoutMaterialLedger
    {
        internal LayoutMaterialLedger(int wallCells, int doors, IDictionary<string, int> stuff, int constructionWood, int fuelWood,
            int localWoodUsed, int importedWood, int importValue, IEnumerable<string> unaccounted, IDictionary<string, int> costList,
            string generatorFuelDef, int generatorFuel, int generatorFuelImported)
        {
            GeneratorFuelDef = generatorFuelDef ?? string.Empty;
            GeneratorFuel = generatorFuel;
            GeneratorFuelImported = generatorFuelImported;
            WallCells = wallCells;
            Doors = doors;
            Stuff = new ReadOnlyDictionary<string, int>(new SortedDictionary<string, int>(stuff, StringComparer.Ordinal));
            ConstructionWood = constructionWood;
            FuelWood = fuelWood;
            LocalWoodUsed = localWoodUsed;
            ImportedWood = importedWood;
            ImportValue = importValue;
            UnaccountedStuffDefs = new ReadOnlyCollection<string>(unaccounted.Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList());
            CostListSummary = new ReadOnlyDictionary<string, int>(new SortedDictionary<string, int>(costList, StringComparer.Ordinal));
        }

        public int WallCells { get; }
        public int Doors { get; }
        // Stuff def -> units for walls, doors and stuffable furniture.
        public IReadOnlyDictionary<string, int> Stuff { get; }
        public int ConstructionWood { get; }
        // Campfire, torch and passive-cooler fuel over LayoutEquipment.FuelHorizonDays.
        public int FuelWood { get; }
        // Generator fuel stock (units of GeneratorFuelDef) for the horizon, stored physically.
        public string GeneratorFuelDef { get; }
        public int GeneratorFuel { get; }
        // Imported part of the generator fuel (all chemfuel; wood beyond the local yield).
        public int GeneratorFuelImported { get; }
        public int LocalWoodUsed { get; }
        // All imported wood, including wood generator fuel.
        public int ImportedWood { get; }
        // Market value of imported wood plus imported non-wood generator fuel.
        public int ImportValue { get; }
        // Stuffable defs whose stuff is neither the construction stuff nor wood (not charged here).
        public IReadOnlyList<string> UnaccountedStuffDefs { get; }
        // Fixed CostList totals (Steel, components, ...); recorded, charged in step 6.
        public IReadOnlyDictionary<string, int> CostListSummary { get; }

        public override string ToString() => "walls=" + WallCells + " doors=" + Doors + " stuff=" +
            string.Join(",", Stuff.Select(s => s.Key + ":" + s.Value)) + " constructionWood=" + ConstructionWood + " fuelWood=" + FuelWood +
            " generatorFuel=" + GeneratorFuelDef + ":" + GeneratorFuel + "/imported " + GeneratorFuelImported +
            " localWood=" + LocalWoodUsed + " importedWood=" + ImportedWood + " importValue=" + ImportValue;

        internal ulong AddTo(ulong hash)
        {
            hash = StableHash.Add(hash, WallCells);
            hash = StableHash.Add(hash, Doors);
            foreach (KeyValuePair<string, int> s in Stuff)
            {
                hash = StableHash.Add(hash, s.Key);
                hash = StableHash.Add(hash, s.Value);
            }
            hash = StableHash.Add(hash, ConstructionWood);
            hash = StableHash.Add(hash, FuelWood);
            hash = StableHash.Add(hash, GeneratorFuelDef);
            hash = StableHash.Add(hash, GeneratorFuel);
            hash = StableHash.Add(hash, GeneratorFuelImported);
            hash = StableHash.Add(hash, ImportedWood);
            return StableHash.Add(hash, ImportValue);
        }
    }

    // Stage kit, adaptations, construction stuff, demand and material accounting,
    // computed once per input and shared by the planner and the validator.
    public sealed class LayoutEquipment
    {
        public const string Wood = "WoodLog";
        // Walls have no role: the plain vanilla wall is the only built wall.
        public const string WallDefName = "Wall";
        public const string StonecuttingResearch = "Stonecutting";
        public const string StuffWoody = "Woody";
        public const string StuffStony = "Stony";
        // Stonecutting adaptation when local wood < construction need x 1.25. PROVISIONAL.
        public const int WoodScarcityNumerator = 5;
        public const int WoodScarcityDenominator = 4;
        // Days of fire-pit, torch and passive-cooler fuel charged as stock. PROVISIONAL.
        public const int FuelHorizonDays = 3;
        // Pre-search estimate of the outline wall: factor x sqrt(area factor x room bounds area). PROVISIONAL.
        public const double EnclosureOutlineFactor = 4.0;
        public const double EnclosureAreaFactor = 1.5;
        public const string MaterialBudgetExceeded = "material_budget_exceeded";
        public const string ClimateControlUnresolved = "climate_control_unresolved";
        public const string PowerEquipmentUnresolved = "power_equipment_unresolved";
        public const string InteractionOffsetUnsupported = "interaction_offset_unsupported";

        private LayoutEquipment(LayoutKit kit, LayoutDemandSet demand, LayoutLocalResources resources, string constructionStuff,
            bool adapted, LayoutMaterialLedger estimate, string rejection, IEnumerable<string> notes)
        {
            Kit = kit;
            Demand = demand;
            Resources = resources;
            ConstructionStuff = constructionStuff;
            StonecuttingAdapted = adapted;
            Estimate = estimate;
            Rejection = rejection;
            Notes = new ReadOnlyCollection<string>(notes.ToList());
            Digest = ComputeDigest();
        }

        public LayoutKit Kit { get; }
        public LayoutDemandSet Demand { get; }
        public LayoutLocalResources Resources { get; }
        // Wall, door and furniture stuff: WoodLog or a local stone block def.
        public string ConstructionStuff { get; }
        public bool StonecuttingAdapted { get; }
        public LayoutMaterialLedger Estimate { get; }
        // Pre-search rejection code, or null.
        public string Rejection { get; }
        public IReadOnlyList<string> Notes { get; }
        public string Digest { get; }
        public bool StoneConstruction => ConstructionStuff != Wood;

        public static LayoutEquipment Resolve(ProceduralLayoutInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            return Resolve(input.Requirements, input.Catalog, input.Resources, input.Fortification.DesiredGates, input.Seed);
        }

        public static LayoutEquipment Resolve(LayoutRequirements requirements, TechCatalog catalog, LayoutLocalResources resources,
            int desiredGates, ulong seed)
        {
            if (requirements == null) throw new ArgumentNullException(nameof(requirements));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            resources = resources ?? LayoutLocalResources.Default;
            ulong roomSeed = LayoutSeed.Sub(seed, "rooms");
            List<string> notes = new List<string>();
            SettlementTechStage stage = requirements.Stage;
            LayoutKit kit = new LayoutKit(requirements, catalog, RoleResolver.Resolve(catalog, stage));
            LayoutDemandSet demand = LayoutDemandModel.Derive(requirements, kit, roomSeed);
            string generatorChoice = null;
            bool adapted = false;
            if (kit.Stage.Level == SettlementTechStageLevel.Neolithic && resources.LocalStoneStuffs.Count > 0 && demand.Rejections.Count == 0 &&
                !kit.Stage.HasResearch(StonecuttingResearch))
            {
                LayoutMaterialLedger woodOnly = EstimateLedger(kit, requirements, demand, resources, Wood, false, desiredGates, 0);
                long need = woodOnly.ConstructionWood;
                if ((long)resources.LocalWoodYield * WoodScarcityDenominator < need * WoodScarcityNumerator)
                {
                    stage = stage.WithAdaptation(StonecuttingResearch, "wood " + resources.LocalWoodYield + "/" + need);
                    kit = new LayoutKit(requirements, catalog, RoleResolver.Resolve(catalog, stage));
                    demand = LayoutDemandModel.Derive(requirements, kit, roomSeed);
                    adapted = true;
                    notes.Add("adaptation:" + StonecuttingResearch + ":wood " + resources.LocalWoodYield + "/" + need);
                }
            }
            string stuff = ChooseConstructionStuff(kit, resources, seed);
            if (kit.Powered && kit.DispatchableOptions.Count > 0 && demand.Rejections.Count == 0)
            {
                // Dispatchable fuel: wood when the local yield covers the stock after construction and
                // other fuel, otherwise chemfuel when legal (imported), otherwise imported wood.
                LayoutMaterialLedger withoutGenerators = EstimateLedger(kit, requirements, demand, resources, stuff, adapted, desiredGates, 0);
                CatalogDef wood = kit.DispatchableOptions.FirstOrDefault(d => d.Fuel.Contains(Wood));
                CatalogDef other = kit.DispatchableOptions.FirstOrDefault(d => !d.Fuel.Contains(Wood));
                CatalogDef chosen = wood ?? other;
                if (wood != null && other != null)
                {
                    long spare = (long)resources.LocalWoodYield - withoutGenerators.ConstructionWood - withoutGenerators.FuelWood;
                    if (spare < LayoutPowerModel.FuelStock(wood, demand.PowerGenerators)) chosen = other;
                }
                generatorChoice = chosen.DefName;
                if (kit.Generator == null || kit.Generator.DefName != generatorChoice)
                {
                    kit = new LayoutKit(requirements, catalog, kit.Roles, generatorChoice);
                    demand = LayoutDemandModel.Derive(requirements, kit, roomSeed);
                }
                int stock = LayoutPowerModel.FuelStock(kit.Generator, demand.PowerGenerators);
                int stackLimit = kit.GeneratorFuel == null ? 1 : kit.GeneratorFuel.StackLimit;
                int cells = (stock + stackLimit - 1) / stackLimit;
                if (cells > 0) demand = LayoutDemandModel.Derive(requirements, kit, roomSeed, cells);
                notes.Add("dispatchable:" + kit.Generator.DefName + " fuel=" + kit.GeneratorFuel.DefName + " worstCaseStock=" + stock + " storageCells=" + cells);
            }
            LayoutMaterialLedger estimate = EstimateLedger(kit, requirements, demand, resources, stuff, adapted, desiredGates, demand.PowerGenerators);
            notes.AddRange(kit.Notes);
            notes.Add("construction_stuff:" + stuff);
            string offset = UnsupportedInteractionOffset(kit);

            string rejection = null;
            if (!kit.Roles.Success) rejection = RoleResolver.EssentialUnresolved;
            else if (requirements.Climate.HeatingRequired && !kit.Resolved(SettlementRole.Heat)) rejection = ClimateControlUnresolved + ":Heat";
            else if (requirements.Climate.CoolingRequired && !kit.Resolved(SettlementRole.Cool)) rejection = ClimateControlUnresolved + ":Cool";
            else if (offset != null) rejection = InteractionOffsetUnsupported + ":" + offset;
            else if (kit.Powered && (kit.Generator == null || kit.Battery == null)) rejection = PowerEquipmentUnresolved;
            else if (estimate.ImportValue > requirements.ImportValueLimit) rejection = MaterialBudgetExceeded;
            return new LayoutEquipment(kit, demand, resources, stuff, adapted, estimate, rejection, notes);
        }

        // Every Core bench uses the straight-ahead offset (0,0,-1); other offsets are not modelled.
        private static string UnsupportedInteractionOffset(LayoutKit kit)
        {
            IEnumerable<CatalogDef> defs = LayoutKit.FurnishingRoles.Select(kit.DefFor).Concat(kit.WorkshopDefs).Concat(new[] { kit.Generator, kit.Battery });
            CatalogDef bad = defs.FirstOrDefault(d => d != null && d.HasInteractionCell && (d.InteractionOffset.X != 0 || d.InteractionOffset.Z >= 0));
            return bad == null ? null : bad.DefName;
        }

        private readonly Dictionary<long, LayoutRoomDemand> powerRooms = new Dictionary<long, LayoutRoomDemand>();

        // The power room demand for a per-attempt generator and battery count (cached); null when no legal size exists.
        public LayoutRoomDemand PowerRoom(LayoutRequirements requirements, int generators, int batteries, ulong seed)
        {
            long key = (long)generators * 1000 + batteries;
            LayoutRoomDemand demand;
            if (!powerRooms.TryGetValue(key, out demand))
            {
                demand = generators < 1 || generators > LayoutDemandModel.MaxSplit ? null
                    : LayoutDemandModel.BuildPowerRoom(requirements, Kit, generators, batteries, LayoutSeed.Sub(seed, "rooms"));
                powerRooms[key] = demand;
            }
            return demand;
        }

        // Stuff for a stuffable def: the construction stone when accepted, otherwise wood when accepted, otherwise null.
        public string StuffFor(CatalogDef def)
        {
            if (def == null || def.StuffCategories.Count == 0) return null;
            return StuffFor(def, ConstructionStuff);
        }

        private static string StuffFor(CatalogDef def, string construction)
        {
            if (def.StuffCategories.Count == 0) return null;
            if (construction != Wood && def.StuffCategories.Contains(StuffStony)) return construction;
            return def.StuffCategories.Contains(StuffWoody) ? Wood : null;
        }

        private static string ChooseConstructionStuff(LayoutKit kit, LayoutLocalResources resources, ulong seed)
        {
            if (kit.Stage.Level == SettlementTechStageLevel.Neolithic && !kit.Stage.HasResearch(StonecuttingResearch)) return Wood;
            List<string> stones = resources.LocalStoneStuffs.Where(s => kit.Catalog.IsLegal(s, kit.Stage)).ToList();
            if (stones.Count == 0) return Wood;
            return stones[LayoutSeed.Roll(seed, "stuff", stones.Count)];
        }

        // Pre-search estimate from the smallest candidate size of every room.
        private static LayoutMaterialLedger EstimateLedger(LayoutKit kit, LayoutRequirements requirements, LayoutDemandSet demand, LayoutLocalResources resources,
            string stuff, bool adapted, int desiredGates, int generators)
        {
            int wallCells = 0;
            double boundsArea = 0;
            List<LayoutLocalFixture> fixtures = new List<LayoutLocalFixture>();
            foreach (LayoutRoomDemand room in demand.Rooms)
            {
                GridRect size = room.CandidateSizes.OrderBy(s => s.Area).ThenBy(s => Math.Abs(s.Width - s.Height)).ThenBy(s => s.Width).FirstOrDefault();
                if (size.Width == 0) continue;
                int bw = size.Width + 2, bh = size.Height + 2;
                wallCells += 2 * bw + 2 * bh - 4 - 1;
                boundsArea += bw * bh;
                LayoutFurnishResult furnish = LayoutFurnishing.Furnish(room.Kind, size.Width, size.Height, (size.Width + 1) / 2, room.Capacity,
                    LayoutDemandModel.FurnishContext(kit, requirements, false, null));
                if (furnish.Success) fixtures.AddRange(furnish.Fixtures);
            }
            wallCells += (int)Math.Ceiling(EnclosureOutlineFactor * Math.Sqrt(EnclosureAreaFactor * boundsArea));
            int doors = demand.Rooms.Count + desiredGates;
            return Ledger(kit, resources, stuff, adapted, wallCells, doors, fixtures.Select(f => new KeyValuePair<string, SettlementRole?>(f.DefName, f.Role)),
                LayoutPowerModel.FuelStock(kit.Generator, generators));
        }

        // Exact ledger of a planned layout: room walls (minus doors, natural rock and cooler
        // cells), the outline wall, room doors and gate doors, and every placed fixture.
        public LayoutMaterialLedger Count(IEnumerable<LayoutRoom> rooms, IEnumerable<GridPoint> builtWalls, int gates,
            LayoutPowerPlan power, IEnumerable<string> defenseFixtures = null)
        {
            List<LayoutRoom> list = rooms.ToList();
            int wallCells = builtWalls.Count();
            foreach (LayoutRoom room in list)
            {
                HashSet<GridPoint> excluded = new HashSet<GridPoint>(room.NaturalWalls) { room.Door };
                foreach (LayoutFixture f in room.Fixtures.Where(f => f.Kind == LayoutFixtureKind.WallCooler)) excluded.UnionWith(f.Cells);
                wallCells += room.Bounds.Points().Count(p => room.IsEdge(p) && !excluded.Contains(p));
            }
            int doors = list.Count + gates;
            IEnumerable<KeyValuePair<string, SettlementRole?>> fixtures = list.SelectMany(r => r.Fixtures)
                .Select(f => new KeyValuePair<string, SettlementRole?>(f.DefName, f.Role))
                .Concat((power == null ? Enumerable.Empty<LayoutPowerYard>() : power.Yards).Select(y => new KeyValuePair<string, SettlementRole?>(y.DefName, null)))
                .Concat((defenseFixtures ?? Enumerable.Empty<string>()).Select(name =>
                    new KeyValuePair<string, SettlementRole?>(name, SettlementRole.Defense)));
            return Ledger(Kit, Resources, ConstructionStuff, StonecuttingAdapted, wallCells, doors, fixtures, power == null ? 0 : power.FuelStock);
        }

        private static LayoutMaterialLedger Ledger(LayoutKit kit, LayoutLocalResources resources, string stuff, bool adapted, int wallCells,
            int doors, IEnumerable<KeyValuePair<string, SettlementRole?>> fixtures, int generatorFuel)
        {
            TechCatalog catalog = kit.Catalog;
            Dictionary<string, int> stuffs = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, int> costs = new Dictionary<string, int>(StringComparer.Ordinal);
            List<string> unaccounted = new List<string>();
            double fuel = 0;
            Action<CatalogDef, int> charge = (def, count) =>
            {
                if (def == null || count <= 0) return;
                if (def.CostStuffCount > 0)
                {
                    string s = StuffFor(def, stuff);
                    if (s == null) unaccounted.Add(def.DefName);
                    else Add(stuffs, s, def.CostStuffCount * count);
                }
                foreach (KeyValuePair<string, int> cost in def.CostList) Add(costs, cost.Key, cost.Value * count);
            };
            CatalogDef wall;
            catalog.TryGet(WallDefName, out wall);
            CatalogDef door = kit.DefFor(SettlementRole.Door);
            charge(wall, wallCells);
            charge(door, doors);
            foreach (KeyValuePair<string, SettlementRole?> fixture in fixtures)
            {
                CatalogDef def;
                if (!catalog.TryGet(fixture.Key, out def)) continue;
                charge(def, 1);
                if (CountsFuel(def, fixture.Value)) fuel += def.FuelPerDay * FuelHorizonDays;
            }
            int constructionWood;
            stuffs.TryGetValue(Wood, out constructionWood);
            int fuelWood = (int)Math.Ceiling(fuel);
            CatalogDef generatorFuelDef = generatorFuel > 0 ? kit.GeneratorFuel : null;
            bool woodGenerator = generatorFuelDef != null && generatorFuelDef.DefName == Wood;
            long allFuelWood = fuelWood + (woodGenerator ? generatorFuel : 0);
            long yield = resources.LocalWoodYield;
            long imported = adapted ? allFuelWood + Math.Max(0, constructionWood - yield) : Math.Max(0, constructionWood + allFuelWood - yield);
            int otherFuelImported = generatorFuelDef != null && !woodGenerator ? generatorFuel : 0;
            int generatorImported = woodGenerator ? (int)Math.Min(generatorFuel, imported) : otherFuelImported;
            CatalogDef woodDef;
            double woodValue = catalog.TryGet(Wood, out woodDef) ? woodDef.MarketValue : 0;
            int importValue = (int)Math.Ceiling(imported * woodValue + otherFuelImported * (generatorFuelDef == null ? 0 : generatorFuelDef.MarketValue));
            return new LayoutMaterialLedger(wallCells, doors, stuffs, constructionWood, fuelWood,
                (int)(constructionWood + allFuelWood - imported), (int)imported, importValue, unaccounted, costs,
                generatorFuelDef == null ? null : generatorFuelDef.DefName, generatorFuel, generatorImported);
        }

        // Fuel horizon covers fire pits (1x1 cooking campfires), torches, heat campfires and
        // passive coolers; fueled stoves, smithies and generators wait for step 5's fuel model.
        private static bool CountsFuel(CatalogDef def, SettlementRole? role)
        {
            if (!def.Fuel.Contains(Wood) || def.FuelPerDay <= 0 || !role.HasValue) return false;
            switch (role.Value)
            {
                case SettlementRole.Light:
                case SettlementRole.Heat:
                case SettlementRole.Cool:
                    return true;
                case SettlementRole.Cooking:
                    return def.SizeX * def.SizeZ == 1;
                default:
                    return false;
            }
        }

        private static void Add(Dictionary<string, int> map, string key, int value)
        {
            int existing;
            map[key] = map.TryGetValue(key, out existing) ? existing + value : value;
        }

        private string ComputeDigest()
        {
            ulong hash = StableHash.Start;
            hash = StableHash.Add(hash, "p4a-equipment");
            hash = StableHash.Add(hash, Kit.Roles.Digest);
            hash = StableHash.Add(hash, Resources.Digest);
            hash = StableHash.Add(hash, ConstructionStuff);
            hash = StableHash.Add(hash, StonecuttingAdapted ? 1 : 0);
            hash = Estimate.AddTo(hash);
            hash = StableHash.Add(hash, Rejection ?? string.Empty);
            foreach (string note in Notes) hash = StableHash.Add(hash, note);
            return StableHash.Hex(hash);
        }
    }
}
