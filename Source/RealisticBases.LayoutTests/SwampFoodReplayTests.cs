using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

internal static partial class Program
{
    private static ProceduralLayoutInput Swamp15155Input()
    {
        var terrain = CapturedTerrainWithDefinitionIds("swamp-candidate-15155-terrain.rle", 200,
            "1.7.4990 rev1069;generator=RB_P0_Base_Faction");
        Equal("498B952A3DB250C6", terrain.Digest, "preserve the original swamp terrain/roof/trait records");
        var stage = TechStageModel.Create(SettlementTechStageLevel.IndustrialLate);
        var requirements = new LayoutRequirements(residents: 10, capabilities: (SettlementCapability)255, housing: (HousingStandard)0, climate: new LayoutClimate(-13, 28, outdoorGrowingDays: 30), mortarBiasPercent: 50, excavationBudgetCells: 760, fieldCells: 308, capacityFloors: new KeyValuePair<LayoutRoomKind,int>[] {new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)2,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)3,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)4,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)5,24),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)7,4),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)8,1),new KeyValuePair<LayoutRoomKind,int>((LayoutRoomKind)9,24)}, stage: stage, importValueLimit: 101063, equipmentValueLimit: 30240, stockpileRoomCapacityTarget: 120, compactQuestCamp: false, defenseReserveWatts: 4000, logisticsUtilityValueLimit: 300, saleStorageStacks: 126, salePerishableStacks: 27, retainedStorageStacks: 0, retainedPerishableStacks: 0, questArtillery: 0, defenseImportReserveValue: 90750, baseImportValueLimit: 10313, dailyNutritionPerResident: 1.6d, firstHarvestDays: 8);
        var resources = new LayoutLocalResources(localWoodYield: 208113, localStoneStuffs: new string[] {"BlocksGranite","BlocksMarble"}, riverCells: new GridPoint[] {}, steamGeyserCells: new GridPoint[] {new GridPoint(92, 25),new GridPoint(120, 30),new GridPoint(51, 37)});
        return new ProceduralLayoutInput(requirements, terrain, 0x6337E99F7C5DBEF7UL,
            CapturedFoodGrowthCatalog(RuntimeFlatCatalogFixture.Build()), new LayoutFortificationReservation(1),
            new LayoutSearchBudget(maxSites: 10, attemptsPerSite: 3, maxEvaluations: 2500000,
                maxCandidatesPerRoom: 3000, maxRepairsPerAttempt: 20, maxRouteNodes: 40000), resources);
    }

    private static void Swamp15155ConnectedSites()
    {
        var input = Swamp15155Input();
        int area = input.Equipment.Demand.MinimumRoomArea * 3 / 2;
        var sites = P4BJointSitePlanner.Find(input.Terrain, input.Requirements.FieldCells, area,
            input.Fortification.PlacementMargin, LayoutSeed.Sub(input.Seed, "joint-site"),
            input.Budget.MaxSites, input.Budget.MaxSites * 1024, input.Requirements.Residents,
            attachmentDepth: P4BJointSitePlanner.AttachmentDepth(input.Fortification),
            attachmentHalfTip: P4BJointSitePlanner.AttachmentHalfTip(input.Fortification));
        Check(sites);
        var partial = P4BJointSitePlanner.Find(input.Terrain, input.Requirements.FieldCells, area,
            input.Fortification.PlacementMargin, LayoutSeed.Sub(input.Seed, "joint-site"),
            input.Budget.MaxSites, input.Budget.MaxSites * 1024, input.Requirements.Residents,
            attachmentDepth: P4BJointSitePlanner.AttachmentDepth(input.Fortification),
            attachmentHalfTip: P4BJointSitePlanner.AttachmentHalfTip(input.Fortification),
            refineFieldOrigins: true, minimumCrops: 0);
        True(partial.Candidates.Count > 0, "bounded smaller footprints find a connected field/compound pair");
        True(partial.Candidates.All(s => s.HasMainField && s.Crops.Count >= P4BJointSite.MinimumMainFieldCells),
            "use the demonstrated natural field instead of omitting it");
        True(partial.Evaluated <= 3 * input.Budget.MaxSites * 1024, "smaller shapes share the original pair-evaluation cap");
        Check(partial);
        var parallel = P4BJointSitePlanner.Find(input.Terrain, input.Requirements.FieldCells, area,
            input.Fortification.PlacementMargin, LayoutSeed.Sub(input.Seed, "joint-site"),
            input.Budget.MaxSites, input.Budget.MaxSites * 1024, input.Requirements.Residents,
            attachmentDepth: P4BJointSitePlanner.AttachmentDepth(input.Fortification),
            attachmentHalfTip: P4BJointSitePlanner.AttachmentHalfTip(input.Fortification),
            refineFieldOrigins: true, minimumCrops: 0, plannerDegree: 4);
        True(partial.Candidates.Select(s => s.Digest).SequenceEqual(parallel.Candidates.Select(s => s.Digest)),
            "connected smaller-field sites retain seeded ordering under parallel search");
        Equal(partial.Evaluated, parallel.Evaluated, "parallel fallback consumes the same pair allowance");
        Console.WriteLine("  swamp connected preflight sites="+sites.Candidates.Count+" partial="+partial.Candidates.Count+
            " minimumArea="+area+" evaluated="+partial.Evaluated);

        void Check(P4BJointSiteResult candidates)
        {
            foreach (var site in candidates.Candidates)
            {
                var grid = LayoutTerrainGrid.Build(input.Terrain,input.Fortification.PlacementMargin).RestrictTo(site.Compound);
                foreach(var option in site.Interfaces)
                {
                    int depth=P4BJointSitePlanner.AttachmentDepth(input.Fortification);
                    var terminal=new GridPoint(option.InnerGate.X-Math.Sign(option.OuterGate.X-option.InnerGate.X)*depth,
                        option.InnerGate.Z-Math.Sign(option.OuterGate.Z-option.InnerGate.Z)*depth);
                    var seen=new HashSet<GridPoint>{terminal};var queue=new Queue<GridPoint>();queue.Enqueue(terminal);
                    while(queue.Count>0)
                    {
                        var here=queue.Dequeue();
                        foreach(var step in ProceduralLayoutPlanner.Cardinal)
                        {
                            var next=new GridPoint(here.X+step.X,here.Z+step.Z);
                            if(grid.CanPlace(next)&&seen.Add(next))queue.Enqueue(next);
                        }
                    }
                    True(seen.Contains(site.Anchor),"swamp field terminal must share the chosen street anchor's buildable region");
                    True(seen.Count>=area,"capacity in a disconnected patch cannot justify this field gate");
                }
            }
        }
    }

    private static void Swamp15155Settlement()
    {
        var input=Swamp15155Input();string before=input.Terrain.Digest;
        var result=ProceduralLayoutPlanner.Plan(input,true);
        Accepted(result);
        var layout=result.Layout;
        Equal(10,input.Requirements.Residents,"keep all ten funded residents");
        Equal(1,P4BPerimeterDepth.Minimum(input.Terrain,layout),"retain the requested wall layer");
        True(layout.FoodSupply!=null && layout.FoodSupply.Covered,"fund the full original food demand");
        True(layout.FoodSupply.NaturalCells>=P4BJointSite.MinimumMainFieldCells &&
            layout.FoodSupply.NaturalCells<input.Requirements.FieldCells,"retain a useful smaller natural field");
        Equal(178,layout.FoodSupply.TotalFoodUnits,"stock the whole original first-harvest horizon once");
        True(layout.Power.FuelDays>=8,"retain cultivation and ordinary power funding through the first harvest");
        True(ProceduralLayoutValidator.HousingFloorCells(input,layout).Count>=10,"reserve distinct resident spawn positions");
        True(layout.Materials.OrdinaryImportValue<=input.Requirements.BaseImportValueLimit,"retain the captured ordinary allowance");
        True(result.Stats.Evaluations<=input.Budget.MaxEvaluations,"search never exceeds the original evaluation cap");
        True(RealisticBases.Generation.P4BProceduralSpawnPreflight.Evaluate(input,layout).Accepted,"retain full offline spawn preflight");
        Equal(before,input.Terrain.Digest,"planning leaves the captured terrain unchanged");
        Console.WriteLine("  swamp settlement layout="+layout.Digest+" natural="+layout.FoodSupply.NaturalCells+
            " basins="+layout.AllFixtures.Count(f=>f.Kind==LayoutFixtureKind.HydroponicsBasin)+" imports="+
            layout.Materials.OrdinaryImportValue+"/"+input.Requirements.BaseImportValueLimit+" "+result.Stats);
    }
}
