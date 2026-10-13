using RealisticBases.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace RealisticBases.AI
{
    // Sentries can eat available food, but cannot bypass the relief/bed path by
    // sleeping at the gun or post. Sleeping requires an explicit relief role.
    public sealed class ThinkNode_P5AwakePostNeeds : ThinkNode_Priority
    {
        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams)
        {
            ThinkResult result = base.TryIssueJobPackage(pawn, jobParams);
            if (result.Job?.def != JobDefOf.LayDown) return result;
            var controller = pawn.Map?.GetComponent<P3GarrisonController>();
            if (controller == null || !controller.Controls(pawn) || controller.P5NeedsRelief(pawn)) return result;
            JobMaker.ReturnToPool(result.Job);
            return ThinkResult.NoJob;
        }
    }
    // Native needs selection with a whole-route settlement bound, not an
    // arbitrary radius around a post that cannot reach the food store/barracks.
    public sealed class ThinkNode_P5BoundedNeeds : ThinkNode_Priority
    {
        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams)
        {
            var controller = pawn.Map?.GetComponent<P3GarrisonController>();
            if (controller == null || !controller.Controls(pawn) || !controller.P5NeedsRelief(pawn)) return ThinkResult.NoJob;
            float oldRadius = pawn.mindState.maxDistToSquadFlag;
            ThinkResult result;
            try
            {
                pawn.mindState.maxDistToSquadFlag = -1;
                result = base.TryIssueJobPackage(pawn, jobParams);
            }
            finally { pawn.mindState.maxDistToSquadFlag = oldRadius; }
            if (!result.IsValid) return result;
            Thing target = result.Job.targetA.Thing;
            bool carried = target != null && !target.Spawned && (pawn.inventory.innerContainer.Contains(target) ||
                pawn.carryTracker.CarriedThing == target);
            bool stationary = !result.Job.targetA.IsValid;
            if (carried || stationary || controller.RouteAllowed(pawn, result.Job.targetA.Cell, 160)) return result;
            P0Log.Message("P5_NEEDS_ROUTE_REJECT", "map=" + pawn.Map.uniqueID + " pawn=" + pawn.ThingID +
                " job=" + result.Job.def.defName + " target=" + result.Job.targetA);
            JobMaker.ReturnToPool(result.Job);
            return ThinkResult.NoJob;
        }
    }
    // Vanilla doctors tend humanlike patients only in bed, and the relief duty
    // has no patient-go-to-bed branch. Keep an injured resident in its own
    // native bed until tended; the relief role ends with the need.
    public sealed class JobGiver_P5PatientBedRest : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var controller = pawn.Map?.GetComponent<P3GarrisonController>();
            if (controller == null || !controller.Controls(pawn) || !controller.P5NeedsRelief(pawn) ||
                !pawn.health.HasHediffsNeedingTend() || RestUtility.DisturbancePreventsLyingDown(pawn)) return null;
            if (pawn.Downed && (!pawn.health.CanCrawl || pawn.GetPosture().InBed())) return null;
            if (SelfTendDue(pawn))
            {
                // Vanilla JobGiver_SelfTend, once the doctors leave a bleeding
                // patient waiting: no medicine, a single tend.
                Job self = JobMaker.MakeJob(JobDefOf.TendPatient, pawn);
                self.endAfterTendedOnce = true;
                return self;
            }
            Building_Bed bed = pawn.CurrentBed() ?? RestUtility.FindBedFor(pawn, pawn, checkSocialProperness: false);
            if (bed == null) return null;
            Job job = JobMaker.MakeJob(JobDefOf.LayDown, bed);
            job.checkOverrideOnExpire = true;
            return job;
        }

        private const int SelfTendWaitTicks = 1250;

        private static bool SelfTendDue(Pawn pawn) => pawn.InBed() && pawn.health.hediffSet.BleedRateTotal > 0 &&
            pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) &&
            pawn.health.hediffSet.hediffs.Any(h => h.TendableNow() && h.Bleeding && h.ageTicks >= SelfTendWaitTicks) &&
            pawn.Map.reservationManager.CanReserve(pawn, pawn);
    }
}
