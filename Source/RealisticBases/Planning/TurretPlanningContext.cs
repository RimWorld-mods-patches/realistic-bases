using System;
using System.Collections.Generic;

namespace RealisticBases.Planning
{
    // Per-layout state shared by the turret planner's crossing/spacing searches.
    // Static geometry is prepared once; dynamic masks and caches are reset and
    // updated as groups are considered.
    internal sealed class TurretPlanningContext
    {
        internal TurretPlanningContext(int width, int height)
        {
            BaseOpaque = new CellSet(width, height);
            StaticBlocked = new CellSet(width, height);
            Opaque = new CellSet(width, height);
            Blocked = new CellSet(width, height);
            NestCover = new CellSet(width, height);
            PostRoutes = new CellSet(width, height);
            ArrivalCells = new CellSet(width, height);
            PreviousFootprints = new CellSet(width, height);
            PreviousCoverCells = new CellSet(width, height);
            PreviousServiceRoutes = new CellSet(width, height);
            Coverage = new CellMap<int>(width, height);
            ExposureScores = new CellMap<int>(width, height);
            ExistingExposureAnswers = new CellMap<bool>(width, height);
            ApproachTargets = Array.Empty<GridPoint>();
            SideSamples = new List<GridPoint[]>();
            Frames = new List<TurretPlanningFrame>();
            PreviousTurrets = new List<P4BTurretPlan>();
            PreviousFootprintCells = new List<GridPoint>();
            StaffedPosts = new List<GridPoint>();
            ScratchCellsA = new List<GridPoint>();
            ScratchCellsB = new List<GridPoint>();
            ScratchCellsC = new List<GridPoint>();
        }

        internal CellSet BaseOpaque { get; }
        internal CellSet StaticBlocked { get; }
        internal CellSet Opaque { get; }
        internal CellSet Blocked { get; }
        internal CellSet NestCover { get; }
        internal CellSet PostRoutes { get; }
        internal CellSet ArrivalCells { get; }
        internal CellSet PreviousFootprints { get; }
        internal CellSet PreviousCoverCells { get; }
        internal CellSet PreviousServiceRoutes { get; }
        internal CellMap<int> Coverage { get; }
        internal CellMap<int> ExposureScores { get; }
        internal CellMap<bool> ExistingExposureAnswers { get; }
        internal GridPoint[] ApproachTargets { get; set; }
        internal List<GridPoint[]> SideSamples { get; }
        internal List<TurretPlanningFrame> Frames { get; }
        internal List<P4BTurretPlan> PreviousTurrets { get; }
        internal List<GridPoint> PreviousFootprintCells { get; }
        internal List<GridPoint> StaffedPosts { get; }
        internal List<GridPoint> ScratchCellsA { get; }
        internal List<GridPoint> ScratchCellsB { get; }
        internal List<GridPoint> ScratchCellsC { get; }

        internal void ResetGroupState()
        {
            Opaque.CopyFrom(BaseOpaque);
            Blocked.CopyFrom(StaticBlocked);
            PreviousFootprints.Clear();
            PreviousCoverCells.Clear();
            PreviousServiceRoutes.Clear();
            Coverage.Clear();
            ExposureScores.Clear();
            ExistingExposureAnswers.Clear();
            PreviousTurrets.Clear();
            PreviousFootprintCells.Clear();
            StaffedPosts.Clear();
            ScratchCellsA.Clear();
            ScratchCellsB.Clear();
            ScratchCellsC.Clear();
        }
    }

    internal struct TurretPlanningFrame
    {
        internal TurretPlanningFrame(GridPoint anchor, GridPoint gate, GridPoint outward,
            GridPoint? firstNest, GridPoint? partnerNest, string side, bool betweenNests)
        {
            Anchor = anchor;
            Gate = gate;
            Outward = outward;
            FirstNest = firstNest;
            PartnerNest = partnerNest;
            Side = side;
            BetweenNests = betweenNests;
        }

        internal GridPoint Anchor { get; }
        internal GridPoint Gate { get; }
        internal GridPoint Outward { get; }
        internal GridPoint? FirstNest { get; }
        internal GridPoint? PartnerNest { get; }
        internal string Side { get; }
        internal bool BetweenNests { get; }
    }
}
