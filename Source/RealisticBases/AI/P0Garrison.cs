using System.Collections.Generic;
using System.Linq;
using RealisticBases.Core;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RealisticBases.AI
{
    public sealed class LordJob_P0Garrison : LordJob
    {
        private IntVec3 holdPost;
        private IntVec3 workCenter;

        public LordJob_P0Garrison()
        {
        }

        public LordJob_P0Garrison(IntVec3 holdPost, IntVec3 workCenter)
        {
            this.holdPost = holdPost;
            this.workCenter = workCenter;
        }

        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph();
            graph.StartingToil = new LordToil_P0Garrison(holdPost, workCenter);
            return graph;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref holdPost, "holdPost");
            Scribe_Values.Look(ref workCenter, "workCenter");
        }
    }

    public sealed class LordToil_P0Garrison : LordToil
    {
        private readonly IntVec3 holdPost;
        private readonly IntVec3 workCenter;

        public override IntVec3 FlagLoc => holdPost;

        public LordToil_P0Garrison(IntVec3 holdPost, IntVec3 workCenter)
        {
            this.holdPost = holdPost;
            this.workCenter = workCenter;
        }

        public override void UpdateAllDuties()
        {
            List<Pawn> pawns = lord.ownedPawns.OrderBy(p => p.thingIDNumber).ToList();
            for (int i = 0; i < pawns.Count; i++)
            {
                pawns[i].mindState.duty = i == 0
                    ? new PawnDuty(RBDefOf.RB_P0_HoldPost, holdPost, 4f)
                    : new PawnDuty(RBDefOf.RB_P0_WorkSettlement, workCenter, 12f);
            }
            P0Log.Message("GARRISON_DUTIES", "lord=" + lord.loadID + " pawns=" + pawns.Count +
                " holdPawn=" + (pawns.Count > 0 ? pawns[0].thingIDNumber.ToString() : "none") +
                " workerPawn=" + (pawns.Count > 1 ? pawns[1].thingIDNumber.ToString() : "none"));
        }
    }

    public sealed class JobGiver_P0SettlementWork : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            return pawn.Map?.GetComponent<P0WorkProbe>()?.TryGiveJob(pawn);
        }
    }

    public sealed class P0WorkProbe : MapComponent
    {
        private Pawn holdPawn;
        private Pawn workerPawn;
        private Thing generator;
        private Thing stove;
        private Building_PlantGrower grower;
        private IntVec3 holdPost = IntVec3.Invalid;
        private int baselineMeals;
        private int stage;
        private string lastWorkerJob;
        private float lastHoldHealth = -1f;
        private bool holdDamageReported;

        public P0WorkProbe(Map map) : base(map)
        {
        }

        public void Initialize(Pawn defender, Pawn worker, Thing fuelGenerator, Thing cookingStove,
            Building_PlantGrower plantGrower, IntVec3 post)
        {
            holdPawn = defender;
            workerPawn = worker;
            generator = fuelGenerator;
            stove = cookingStove;
            grower = plantGrower;
            holdPost = post;
            baselineMeals = map.listerThings.ThingsOfDef(ThingDefOf.MealSimple).Sum(t => t.stackCount);
            stage = 0;
            lastHoldHealth = defender.health.summaryHealth.SummaryHealthPercent;
            P0Log.Message("WORK_PROBE_INIT", "map=" + map.uniqueID + " holdPawn=" + defender.thingIDNumber +
                " workerPawn=" + worker.thingIDNumber + " faction=" + worker.Faction?.def?.defName +
                " generator=" + generator.thingIDNumber + " stove=" + stove.thingIDNumber +
                " grower=" + grower.thingIDNumber + " baselineMeals=" + baselineMeals);
        }

        public Job TryGiveJob(Pawn pawn)
        {
            if (pawn != workerPawn || pawn.Downed || pawn.Dead)
            {
                return null;
            }

            WorkGiver_Refuel refuel = DefDatabase<WorkGiverDef>.GetNamed("Refuel").Worker as WorkGiver_Refuel;
            CompRefuelable generatorFuel = generator?.TryGetComp<CompRefuelable>();
            if (stage <= 0)
            {
                if (generatorFuel != null && !generatorFuel.HasFuel)
                {
                    return refuel != null && refuel.HasJobOnThing(pawn, generator)
                        ? refuel.JobOnThing(pawn, generator)
                        : null;
                }
                SetStage(1, "generator_refueled fuel=" + (generatorFuel?.Fuel ?? -1f));
            }

            CompRefuelable stoveFuel = stove?.TryGetComp<CompRefuelable>();
            if (stage <= 1)
            {
                if (stoveFuel != null && !stoveFuel.HasFuel)
                {
                    return refuel != null && refuel.HasJobOnThing(pawn, stove)
                        ? refuel.JobOnThing(pawn, stove)
                        : null;
                }
                SetStage(2, "stove_refueled fuel=" + (stoveFuel?.Fuel ?? -1f));
            }

            int meals = map.listerThings.ThingsOfDef(ThingDefOf.MealSimple).Sum(t => t.stackCount);
            if (stage <= 2)
            {
                if (meals <= baselineMeals)
                {
                    WorkGiver_DoBill cook = DefDatabase<WorkGiverDef>.GetNamed("DoBillsCook").Worker as WorkGiver_DoBill;
                    return cook?.JobOnThing(pawn, stove);
                }
                SetStage(3, "meal_completed delta=" + (meals - baselineMeals));
            }

            if (stage <= 3)
            {
                foreach (IntVec3 cell in grower.OccupiedRect())
                {
                    if (cell.GetPlant(map)?.def.defName == "Plant_Rice")
                    {
                        SetStage(4, "growing_task_completed cell=" + cell);
                        return null;
                    }
                }
                WorkGiver_GrowerSow sow = DefDatabase<WorkGiverDef>.GetNamed("GrowerSow").Worker as WorkGiver_GrowerSow;
                foreach (IntVec3 cell in grower.OccupiedRect())
                {
                    Job job = sow?.JobOnCell(pawn, cell);
                    if (job != null)
                    {
                        return job;
                    }
                }
            }
            return null;
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref holdPawn, "p0HoldPawn");
            Scribe_References.Look(ref workerPawn, "p0WorkerPawn");
            Scribe_References.Look(ref generator, "p0Generator");
            Scribe_References.Look(ref stove, "p0Stove");
            Scribe_References.Look(ref grower, "p0Grower");
            Scribe_Values.Look(ref holdPost, "p0HoldPost", IntVec3.Invalid);
            Scribe_Values.Look(ref baselineMeals, "p0BaselineMeals", 0);
            Scribe_Values.Look(ref stage, "p0WorkStage", 0);
            Scribe_Values.Look(ref lastWorkerJob, "p0LastWorkerJob");
            Scribe_Values.Look(ref lastHoldHealth, "p0LastHoldHealth", -1f);
            Scribe_Values.Look(ref holdDamageReported, "p0HoldDamageReported", false);
        }

        public override void FinalizeInit()
        {
            if (workerPawn != null)
            {
                P0Log.Message("WORK_PROBE_RELOADED", "map=" + map.uniqueID + " stage=" + stage +
                    " worker=" + workerPawn.thingIDNumber + " generatorFuel=" +
                    (generator?.TryGetComp<CompRefuelable>()?.Fuel ?? -1f));
            }
        }

        public override void MapComponentTick()
        {
            if (workerPawn == null || holdPawn == null || (Find.TickManager.TicksGame + map.uniqueID) % 60 != 0)
            {
                return;
            }
            string job = workerPawn.CurJobDef?.defName ?? "none";
            if (job != lastWorkerJob && stage < 4)
            {
                P0Log.Message("WORK_JOB", "pawn=" + workerPawn.thingIDNumber + " stage=" + stage +
                    " previous=" + (lastWorkerJob ?? "none") + " current=" + job);
            }
            lastWorkerJob = job;

            float health = holdPawn.health.summaryHealth.SummaryHealthPercent;
            if (!holdDamageReported && health + 0.001f < lastHoldHealth)
            {
                holdDamageReported = true;
                P0Log.Message("HOLD_UNDER_DAMAGE", "pawn=" + holdPawn.thingIDNumber + " healthBefore=" +
                    lastHoldHealth.ToString("F3") + " healthNow=" + health.ToString("F3") +
                    " duty=" + holdPawn.mindState.duty?.def?.defName + " distanceToPost=" +
                    holdPawn.Position.DistanceTo(holdPost).ToString("F2"));
            }
            lastHoldHealth = health;
        }

        public void DamageHoldPawn()
        {
            if (holdPawn == null || holdPawn.Dead || !holdPawn.Spawned)
            {
                Messages.Message("No active P0 hold pawn on this map.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            holdPawn.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 5f));
            P0Log.Message("HOLD_DAMAGE_APPLIED", "pawn=" + holdPawn.thingIDNumber +
                " duty=" + holdPawn.mindState.duty?.def?.defName + " position=" + holdPawn.Position +
                " post=" + holdPost);
        }

        private void SetStage(int next, string evidence)
        {
            if (next <= stage)
            {
                return;
            }
            stage = next;
            P0Log.Message("WORK_STAGE", "map=" + map.uniqueID + " stage=" + stage + " " + evidence);
        }
    }
}
