using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RealisticBases.AI
{
    // Retain native reservations, pickup, duplicate pickup, 240-tick service
    // and final fuel consumption. Only movement receives gateway waypoints.
    public sealed class JobDriver_P3BoundedRearm : JobDriver_Refuel
    {
        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFinishAction(condition =>
            {
                if (condition == JobCondition.Incompletable || condition == JobCondition.Errored)
                    pawn.Map?.GetComponent<P3GarrisonController>()?.ServiceFailed(pawn, job, CurToilString);
            });
            int index = 0;
            foreach (Toil native in base.MakeNewToils())
            {
                // Verified Core 1.6.4871 sequence: fuel goto=2, turret goto=5.
                if (index == 2 || index == 5)
                {
                    TargetIndex targetIndex = index == 2 ? TargetIndex.B : TargetIndex.A;
                    PathEndMode mode = index == 2 ? PathEndMode.ClosestTouch : PathEndMode.Touch;
                    Toil select = ToilMaker.MakeToil("P3SelectBoundedServiceWaypoint");
                    select.initAction = () =>
                    {
                        Thing target = job.GetTarget(targetIndex).Thing;
                        if (target == null) { EndJobWith(JobCondition.Incompletable); return; }
                        if (pawn.CanReachImmediate(target, mode)) { JumpToToil(native); return; }
                        IntVec3 next = pawn.Map.GetComponent<P3GarrisonController>().ServiceWaypoint(pawn, target, mode);
                        // GotoCell on the current cell completes synchronously;
                        // its jump back would recurse until the native stack
                        // overflows. Every service leg must make progress.
                        if (!next.IsValid || next == pawn.Position) { EndJobWith(JobCondition.Incompletable); return; }
                        job.SetTarget(TargetIndex.C, next);
                    };
                    yield return select;
                    yield return Toils_Goto.GotoCell(TargetIndex.C, PathEndMode.OnCell);
                    yield return Toils_Jump.Jump(select);
                }
                yield return native;
                index++;
            }
        }
    }
}
