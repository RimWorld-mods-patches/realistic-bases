using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RealisticBases.AI;
using RealisticBases.Core;
using RimWorld;
using Verse;

namespace RealisticBases.Diagnostics
{
    public sealed class P5TendingProbe : MapComponent
    {
        private Pawn patient;
        private int deadline, medicineBefore;
        private readonly HashSet<int> tendJobs = new HashSet<int>();
        public P5TendingProbe(Map map) : base(map) { }
        private int Medicine()
        {
            var c = map.GetComponent<P3GarrisonController>();
            return c.P5OwnedThings.Concat(c.P5Residents.SelectMany(p => p.inventory.innerContainer.Concat(
                    p.carryTracker.CarriedThing == null ? Enumerable.Empty<Thing>() : new[] { p.carryTracker.CarriedThing })))
                .Distinct().Where(t => !t.Destroyed && t.def.IsMedicine).Sum(t => t.stackCount);
        }
        public void Start()
        {
            P5DebugActions.CurrentSettlement();
            if (deadline != 0) throw new InvalidOperationException("Tending probe already active");
            var c = map.GetComponent<P3GarrisonController>();
            patient = c.P5Residents.OrderByDescending(p => p.thingIDNumber).FirstOrDefault()
                ?? throw new InvalidOperationException("No funded resident");
            if (patient.health.HasHediffsNeedingTend()) throw new InvalidOperationException("Require a healthy initial patient");
            medicineBefore = Medicine();
            if (medicineBefore < 1) throw new InvalidOperationException("No finite operating medicine");
            GameDataSaveLoader.SaveGame("RealisticBases-P5-before-tending");
            patient.TakeDamage(new DamageInfo(DamageDefOf.Cut, 8, armorPenetration: 99,
                hitPart: patient.RaceProps.body.corePart));
            if (!patient.health.HasHediffsNeedingTend()) throw new InvalidOperationException("Native injury did not require tending");
            deadline = GenTicks.TicksGame + 10000; tendJobs.Clear();
            P0Log.Message("P5_TENDING_PROBE_START", "map=" + map.uniqueID + " patient=" + patient.ThingID +
                " medicine=" + medicineBefore + " deadline=" + deadline + " npcOrdersIssued=0 extraMedicine=0");
        }
        public override void MapComponentTick()
        {
            if (deadline == 0 || GenTicks.TicksGame % 15 != 0) return;
            foreach (Pawn pawn in map.GetComponent<P3GarrisonController>().P5Residents)
                if (pawn.CurJobDef == JobDefOf.TendPatient && pawn.CurJob.targetA.Thing == patient)
                    tendJobs.Add(pawn.CurJob.loadID);
            int remaining = Medicine();
            bool passed = patient != null && patient.Spawned && !patient.Dead && !patient.health.HasHediffsNeedingTend() &&
                remaining < medicineBefore && tendJobs.Count > 0;
            if (!passed && patient != null && !patient.Dead && GenTicks.TicksGame < deadline) return;
            P0Log.Message("P5_TENDING_PROBE_COMPLETE", "map=" + map.uniqueID + " passed=" + passed +
                " patient=" + patient?.ThingID + " alive=" + (patient != null && !patient.Dead) +
                " nativeTendJobs=" + tendJobs.Count + " medicineBefore=" + medicineBefore + " medicineAfter=" + remaining +
                " physicallyConsumed=" + (medicineBefore - remaining) + " npcOrdersIssued=0 extraMedicine=0");
            deadline = 0; Find.TickManager.Pause();
        }
    }
    public static class P5TendingDebugAction
    {
        [DebugAction("Realistic Bases P5", "Start P5 native tending probe", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Start() => P5DebugActions.CurrentSettlement().Map.GetComponent<P5TendingProbe>().Start();
    }
}
