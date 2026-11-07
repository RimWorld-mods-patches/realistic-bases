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

        [DebugAction(Category, "Spawn well-equipped player pawn at cell", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnWellEquippedPlayerPawnAtCell()
        {
            if (!P4DQuestDebugActions.Disposable)
            {
                Messages.Message("Interior breach test pawns can only be spawned in the disposable developer profile.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            Map map = Find.CurrentMap;
            Settlement settlement = map?.Parent as Settlement;
            P3GarrisonController controller = map?.GetComponent<P3GarrisonController>();
            if (settlement?.Faction == null || controller == null)
            {
                Messages.Message("Open a generated Realistic Bases settlement before spawning the test pawn.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            IntVec3 spawnCell = UI.MouseCell();
            if (!spawnCell.InBounds(map) || !spawnCell.Standable(map) || spawnCell.GetEdifice(map) != null ||
                spawnCell.GetThingList(map).Any(t => t is Pawn))
            {
                Messages.Message("Choose a clear, standable cell for the test pawn.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            PawnKindDef kind = PawnKindDefOf.Colonist;
            Pawn attacker = null;
            try
            {
                attacker = PawnGenerator.GeneratePawn(kind, Faction.OfPlayer, map.Tile);
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

                GenSpawn.Spawn(attacker, spawnCell, map);

                Log.Message("[RealisticBases:P3] event=INTERIOR_TEST_PLAYER_PAWN_SPAWNED map=" + map.uniqueID +
                    " faction=" + Faction.OfPlayer.def.defName + " pawn=" + attacker.thingIDNumber + " kind=" + kind.defName +
                    " weapon=" + (attacker.equipment?.Primary?.def.defName ?? "faction_loadout") +
                    " cell=" + spawnCell + " hostileToSettlement=" + Faction.OfPlayer.HostileTo(settlement.Faction));
                Messages.Message("A well-equipped player pawn was spawned at " + spawnCell +
                    ". The pawn is selectable and can be drafted for the interior attack test.",
                    MessageTypeDefOf.TaskCompletion, true);
            }
            catch (Exception ex)
            {
                if (attacker != null && !attacker.Destroyed) attacker.Destroy(DestroyMode.Vanish);
                Log.Error("[RealisticBases:P3] event=INTERIOR_TEST_PLAYER_PAWN_FAILED map=" + map.uniqueID + " exception=" + ex);
                Messages.Message("The interior breach test pawn could not be spawned. See the game log.",
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
