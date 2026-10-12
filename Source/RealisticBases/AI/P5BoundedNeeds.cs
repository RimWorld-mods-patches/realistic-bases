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
}
