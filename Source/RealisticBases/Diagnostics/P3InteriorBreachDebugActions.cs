using System;
using System.Linq;
using LudeonTK;
using RealisticBases.AI;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace RealisticBases.Diagnostics
{
    public static class P3InteriorBreachDebugActions
    {
        private const string Category = "Realistic Bases P3";

        [DebugAction(Category, "Spawn well-equipped raider in warehouse", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnWellEquippedRaiderInWarehouse()
        {
            if (!P4DQuestDebugActions.Disposable)
            {
                Messages.Message("Interior breach attackers can only be spawned in the disposable developer profile.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            Map map = Find.CurrentMap;
            Settlement settlement = map?.Parent as Settlement;
            P3GarrisonController controller = map?.GetComponent<P3GarrisonController>();
            if (settlement?.Faction == null || controller == null)
            {
                Messages.Message("Open a generated Realistic Bases settlement before spawning the attacker.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            Faction raiders = Find.FactionManager.AllFactionsListForReading
                .Where(f => f != null && !f.IsPlayer && !f.Hidden && !f.defeated && f.def.humanlikeFaction &&
                    f != settlement.Faction && f.HostileTo(settlement.Faction) &&
                    f.def.pawnGroupMakers?.Any(g => g.kindDef == PawnGroupKindDefOf.Combat) == true)
                .OrderBy(f => f.loadID).FirstOrDefault();
            if (raiders == null)
            {
                Messages.Message("No active hostile combat faction is available for this settlement.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            Room warehouse = map.listerThings.AllThings.OfType<Building_Storage>()
                .Where(s => s.Spawned && s.Faction == settlement.Faction)
                .Select(s => s.GetRoom()).Where(r => r != null).Distinct()
                .OrderByDescending(r => map.listerThings.AllThings.Count(t => t.Spawned && t.Position.GetRoom(map) == r))
                .ThenByDescending(r => r.CellCount).FirstOrDefault();
            IntVec3? spawnCell = warehouse == null ? null : map.AllCells
                .Where(c => c.GetRoom(map) == warehouse && c.Standable(map) && c.GetEdifice(map) == null &&
                    !c.GetThingList(map).Any(t => t is Pawn))
                .OrderBy(c => c.DistanceToSquared(controller.SupportCenter)).Cast<IntVec3?>().FirstOrDefault();
            if (!spawnCell.HasValue && controller.SupportCenter.InBounds(map) && controller.SupportCenter.Standable(map) &&
                controller.SupportCenter.GetEdifice(map) == null && !controller.SupportCenter.GetThingList(map).Any(t => t is Pawn))
                spawnCell = controller.SupportCenter;
            if (!spawnCell.HasValue)
            {
                Messages.Message("No clear interior cell is available for the test attacker.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            PawnKindDef kind = raiders.def.basicMemberKind ?? PawnKindDefOf.Pirate;
            Pawn attacker = null;
            try
            {
                attacker = PawnGenerator.GeneratePawn(kind, raiders, map.Tile);
                attacker.skills?.GetSkill(SkillDefOf.Shooting)?.EnsureMinLevelWithMargin(12);
                attacker.skills?.GetSkill(SkillDefOf.Melee)?.EnsureMinLevelWithMargin(8);

                ThingDef rifleDef = DefDatabase<ThingDef>.GetNamedSilentFail("Gun_AssaultRifle") ??
                    DefDatabase<ThingDef>.GetNamedSilentFail("Gun_LMG");
                if (rifleDef != null && attacker.equipment != null)
                {
                    if (attacker.equipment.Primary != null)
                        attacker.equipment.DestroyEquipment(attacker.equipment.Primary);
                    attacker.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(rifleDef));
                }
                WearIfAvailable(attacker, "Apparel_FlakVest");
                WearIfAvailable(attacker, "Apparel_FlakHelmet");
                WearIfAvailable(attacker, "Apparel_FlakPants");

                GenSpawn.Spawn(attacker, spawnCell.Value, map);
                LordMaker.MakeNewLord(raiders, new LordJob_AssaultColony(raiders,
                    canKidnap: false, canTimeoutOrFlee: false, canSteal: true), map, new[] { attacker });

                string roomName = warehouse?.Role?.label ?? "interior fallback area";
                Log.Message("[RealisticBases:P3] event=INTERIOR_TEST_ATTACKER_SPAWNED map=" + map.uniqueID +
                    " faction=" + raiders.def.defName + " pawn=" + attacker.thingIDNumber + " kind=" + kind.defName +
                    " weapon=" + (attacker.equipment?.Primary?.def.defName ?? "faction_loadout") +
                    " room=" + roomName + " cell=" + spawnCell.Value);
                Messages.Message("A well-equipped hostile raider was spawned inside " + roomName +
                    ". The raider is ordered to assault the settlement and may steal supplies.",
                    MessageTypeDefOf.NegativeEvent, true);
            }
            catch (Exception ex)
            {
                if (attacker != null && !attacker.Destroyed) attacker.Destroy(DestroyMode.Vanish);
                Log.Error("[RealisticBases:P3] event=INTERIOR_TEST_ATTACKER_FAILED map=" + map.uniqueID + " exception=" + ex);
                Messages.Message("The interior breach attacker could not be spawned. See the game log.",
                    MessageTypeDefOf.NegativeEvent, false);
            }
        }

        private static void WearIfAvailable(Pawn pawn, string defName)
        {
            ThingDef apparelDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (apparelDef == null || !apparelDef.IsApparel) return;
            pawn.apparel.Wear((Apparel)ThingMaker.MakeThing(apparelDef), dropReplacedApparel: true);
        }
    }
}
