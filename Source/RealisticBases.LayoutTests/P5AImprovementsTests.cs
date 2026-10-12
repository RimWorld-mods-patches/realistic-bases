using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.Planning;

// P5A additional improvements: generator vents, shelf orientation, mortar
// shells and premium perimeter walls.
internal static partial class Program
{
    private static void GeneratorRoomVents()
    {
        TerrainSnapshot mountain = Mountain(130, 110);
        foreach (var c in new[] { new { Terrain = Plains(130), Result = Mid(1) }, new { Terrain = Plains(130), Result = Early(2) },
            new { Terrain = mountain, Result = PlanWith(mountain, new LayoutRequirements(8, Industrial, HousingStandard.Barracks, Temperate, 0, 700, 186, null, MidStage), 4, null) } })
        {
            ProceduralLayoutResult result = c.Result;
            Accepted(result);
            LayoutRoom power = result.Layout.Rooms.Single(r => r.Kind == LayoutRoomKind.Power);
            LayoutFixture[] vents = power.Fixtures.Where(f => f.Kind == LayoutFixtureKind.WallVent).ToArray();
            Equal((result.Layout.Power.Generators + 1) / 2, vents.Length, "one vent per two fueled generators");
            foreach (LayoutFixture vent in vents)
            {
                True(power.IsEdge(vent.Cells[0]), "vent sits in the power-room wall");
                True(power.Interior.Contains(vent.ColdCell.Value) && !power.Bounds.Contains(vent.HotCell.Value),
                    "vent joins the room to the outside");
            }
            LayoutRoomPartition partition = LayoutRoomPartition.Build(c.Terrain, result.Layout);
            True(vents.All(v => partition.IsOutdoor(v.HotCell.Value)), "vent exhausts to outdoor air");
        }
    }

    private static void ShelvesFaceTheRoom()
    {
        foreach (ProceduralLayoutResult result in new[] { Mid(1), V2Plan(P2ProfileCatalog.IndustrialTemperate, 3) })
        {
            Accepted(result);
            foreach (LayoutRoom room in result.Layout.Rooms)
            {
                foreach (LayoutFixture shelf in room.Fixtures.Where(f => f.Kind == LayoutFixtureKind.Shelf))
                {
                    GridPoint front = ProceduralLayoutValidator.VanillaNorthRotatedBy(shelf.Rotation);
                    foreach (GridPoint c in shelf.Cells)
                    {
                        GridPoint ahead = new GridPoint(c.X + front.X, c.Z + front.Z);
                        if (room.Kind == LayoutRoomKind.MortarMagazine)
                            Equal(room.Door, ahead, "magazine shelf faces its door");
                        else
                            True(room.Interior.Contains(ahead),
                                "shelf " + room.Id + "@" + c + " " + shelf.Rotation + " faces into the room, not a wall");
                    }
                }
            }
        }
    }

    private static ProceduralLayoutResult V2Plan(PhysicalSettlementProfile profile, ulong seed) =>
        ProceduralLayoutPlanner.Plan(new ProceduralLayoutInput(
            V2Funded(profile, SettlementTechStageLevel.IndustrialLate, Temperate, 100), Plains(180), seed, CoreCatalog));

    private static void PremiumPerimeterStuff()
    {
        SettlementTechStage spacer = TechStageModel.Create(SettlementTechStageLevel.Spacer);
        SettlementTechStage late = TechStageModel.Create(SettlementTechStageLevel.IndustrialLate);
        LayoutEquipment Equip(int residents, SettlementTechStage stage, ulong seed) => LayoutEquipment.Resolve(
            LayoutRequirements.FromPopulation(residents, Industrial, Temperate, stage: stage), CoreCatalog, null, 2, seed);
        HashSet<string> chosen = new HashSet<string>(Enumerable.Range(1, 12).Select(s => Equip(12, spacer, (ulong)s).PerimeterStuff));
        True(chosen.All(LayoutEquipment.PremiumPerimeterStuffs.Contains), "wealthiest spacers: plasteel or uranium perimeter " + string.Join(",", chosen));
        Equal(2, chosen.Count, "both premium metals occur across seeds");
        Equal(null, Equip(8, spacer, 1).PerimeterStuff, "an ordinary spacer settlement keeps local stone");
        Equal(null, Equip(12, late, 1).PerimeterStuff, "industrial settlements never get premium walls");
        Equal(Equip(12, spacer, 5).Digest, Equip(12, spacer, 5).Digest, "deterministic");
    }
}
