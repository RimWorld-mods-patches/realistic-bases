using System;
using System.Collections.Generic;
using System.Linq;
using RealisticBases.AI;
using RealisticBases.Integration;
using RealisticBases.Core;
using RealisticBases.Persistence;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealisticBases.Diagnostics
{
    // Bounded disposable native firing/service checks, never shipped stocks or
    // additional residents. A pre-probe save restores all deliberate mutations.
    public sealed class P4ETurretProbe : MapComponent
    {
        private Building_TurretGun gun;
        private Pawn visitor;
        private IntVec3 firingCell = IntVec3.Invalid;
        private int phase, deadline, started;
        private float initialFuel;
        private int initialSteel, servicePawn = -1, pawnShots, gunShots;
        private readonly HashSet<int> projectiles = new HashSet<int>();
        private int smokeObserveTick, smokePawn;
        private IntVec3 smokeCell;
        public P4ETurretProbe(Map map) : base(map) { }

        public void ObserveSmoke(Pawn pawn)
        {
            smokePawn = pawn.thingIDNumber; smokeCell = pawn.Position;
            smokeObserveTick = Find.TickManager.TicksGame + 15;
        }

        private void Resolve()
        {
            if (!P4DQuestDebugActions.Disposable || !(map.Parent is Settlement) || !map.GetComponent<P0MapState>().Active)
                throw new InvalidOperationException("Requires disposable ordinary procedural settlement");
            if (phase != 0) throw new InvalidOperationException("Probe already active");
            gun = map.listerThings.AllThings.OfType<Building_TurretGun>()
                .Where(t => t.def.defName == "Turret_Autocannon").OrderBy(t => t.thingIDNumber).FirstOrDefault() ??
                throw new InvalidOperationException("No actual autocannon");
            visitor = map.mapPawns.FreeColonistsSpawned.FirstOrDefault() ??
                throw new InvalidOperationException("No disposable arrival colonist");
            // Isolate direct-fire service from long-range shelling. Explicitly
            // consume, never add, the disposable encounter's artillery stocks.
            map.GetComponent<P3GarrisonController>().DebugExhaustMortarAmmunition();
            projectiles.Clear();
            foreach (Projectile existing in map.listerThings.AllThings.OfType<Projectile>()) projectiles.Add(existing.thingIDNumber);
            pawnShots = gunShots = 0; servicePawn = -1;
            started = Find.TickManager.TicksGame;
        }

        private void MoveVisitor(IntVec3 cell)
        {
            visitor.jobs.StopAll(); visitor.DeSpawn(); GenSpawn.Spawn(visitor, cell, map); visitor.drafter.Drafted = true;
        }

        public void StartFiring()
        {
            Resolve();
            CompRefuelable fuel = gun.TryGetComp<CompRefuelable>();
            initialFuel = fuel.Fuel;
            if (initialFuel <= 0 || gun.TryGetComp<CompPowerTrader>()?.PowerOn != true)
                throw new InvalidOperationException("Requires genuinely fueled and powered autocannon");
            var cells = map.AllCells.Where(c => c.Standable(map) && c.GetEdifice(map) == null &&
                c.InHorDistOf(gun.Position, 25f) && !c.InHorDistOf(gun.Position, 16f) &&
                GenSight.LineOfSight(gun.Position, c, map))
                .OrderBy(c => Math.Abs(c.DistanceTo(gun.Position) - 20f)).ThenBy(c => c.x).ThenBy(c => c.z).ToArray();
            if (cells.Length == 0) throw new InvalidOperationException("No clear bounded autocannon target cell");
            firingCell = cells[0]; MoveVisitor(firingCell);
            phase = 1; deadline = started + 5000;
            P2Log.Message("P4E_AUTOCANNON_PROBE_STARTED", "map=" + map.uniqueID + " phase=firing gun=" + gun.thingIDNumber +
                " visitor=" + visitor.thingIDNumber + " target=" + firingCell + " fuel=" + initialFuel + " deadline=" + deadline);
        }

        public void StartRearm()
        {
            Resolve();
            initialSteel = map.GetComponent<P3GarrisonController>().DebugOperatingStock("Steel");
            if (initialSteel <= 0) throw new InvalidOperationException("No finite operating steel reserve");
            var threats = map.mapPawns.AllPawnsSpawned.Where(p => map.GetComponent<P3GarrisonController>().Controls(p))
                .Select(p => p.Position).Concat(map.listerThings.AllThings.OfType<Building_TurretGun>().Select(t => t.Position)).ToArray();
            MoveVisitor(map.AllCells.Where(c => c.OnEdge(map) && c.Standable(map))
                .OrderByDescending(c => threats.Min(p => c.DistanceToSquared(p))).First());
            CompRefuelable fuel = gun.TryGetComp<CompRefuelable>();
            initialFuel = fuel.Fuel;
            fuel.ConsumeFuel(initialFuel); // Explicit accelerated depletion, no refill.
            phase = 2; deadline = started + 10000;
            P2Log.Message("P4E_AUTOCANNON_PROBE_STARTED", "map=" + map.uniqueID + " phase=rearm gun=" + gun.thingIDNumber +
                " explicitlyDrainedFuel=" + initialFuel + " reserveSteel=" + initialSteel + " deadline=" + deadline);
        }

        public void StartPowerLoss()
        {
            Resolve();
            if (!firingCell.IsValid) firingCell = map.AllCells.Where(c => c.Standable(map) && c.GetEdifice(map) == null &&
                c.InHorDistOf(gun.Position, 25f) && !c.InHorDistOf(gun.Position, 16f) &&
                GenSight.LineOfSight(gun.Position, c, map)).First();
            initialFuel = gun.TryGetComp<CompRefuelable>().Fuel;
            if (initialFuel <= 0) throw new InvalidOperationException("Requires fueled gun before power loss");
            MoveVisitor(firingCell);
            int removed = map.GetComponent<P3GarrisonController>().DebugDisconnectConduits();
            map.powerNetManager.UpdatePowerNetsAndConnections_First();
            phase = 3; deadline = started + 300;
            P2Log.Message("P4E_AUTOCANNON_PROBE_STARTED", "map=" + map.uniqueID + " phase=powerLoss gun=" + gun.thingIDNumber +
                " conduitsRemoved=" + removed + " fuel=" + initialFuel + " deadline=" + deadline);
        }

        public void StartCloseSupport()
        {
            Resolve();
            map.GetComponent<P3GarrisonController>().DebugDisconnectConduits();
            map.powerNetManager.UpdatePowerNetsAndConnections_First();
            if (!firingCell.IsValid) firingCell = map.AllCells.Where(c => c.Standable(map) && c.GetEdifice(map) == null &&
                c.InHorDistOf(gun.Position, 25f) && !c.InHorDistOf(gun.Position, 16f) &&
                GenSight.LineOfSight(gun.Position, c, map)).First();
            MoveVisitor(firingCell);
            initialFuel = gun.TryGetComp<CompRefuelable>().Fuel;
            phase = 4; deadline = started + 6000;
            P2Log.Message("P4E_AUTOCANNON_PROBE_STARTED", "map=" + map.uniqueID + " phase=closeSupport gun=" + gun.thingIDNumber +
                " target=" + firingCell + " deadline=" + deadline);
        }

        public override void MapComponentTick()
        {
            if (smokeObserveTick != 0 && Find.TickManager.TicksGame >= smokeObserveTick)
            {
                P2Log.Message("P4E_LOGISTICS_SMOKE_OBSERVED", "map=" + map.uniqueID + " pawn=" + smokePawn +
                    " originalCell=" + smokeCell + " blindSmoke=" + smokeCell.AnyGas(map, GasType.BlindSmoke));
                smokeObserveTick = 0;
                if (phase == 0) Find.TickManager.Pause();
            }
            if (phase == 0) return;
            foreach (Projectile projectile in map.listerThings.AllThings.OfType<Projectile>())
            {
                if (!projectiles.Add(projectile.thingIDNumber)) continue;
                if (projectile.Launcher == gun) gunShots++;
                if (projectile.Launcher is Pawn p && map.GetComponent<P3GarrisonController>().Controls(p) &&
                    (phase != 4 || p.Position.InHorDistOf(gun.Position, 20f)))
                {
                    pawnShots++;
                    if (phase == 4) P2Log.Message("P4E_AUTOCANNON_SUPPORT_SHOT", "map=" + map.uniqueID + " pawn=" +
                        p.thingIDNumber + " at=" + p.Position + " gun=" + gun.Position + " projectile=" + projectile.def.defName);
                }
            }
            CompRefuelable fuel = gun?.TryGetComp<CompRefuelable>();
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                if (map.GetComponent<P3GarrisonController>().Controls(pawn) &&
                    (pawn.CurJobDef == JobDefOf.Refuel || pawn.CurJobDef == JobDefOf.RearmTurret || pawn.CurJobDef == JobDefOf.RearmTurretAtomic ||
                        pawn.CurJobDef == RBDefOf.RB_P3_BoundedRearm) &&
                    pawn.CurJob.targetA.Thing == gun) servicePawn = pawn.thingIDNumber;
            int steel = map.GetComponent<P3GarrisonController>().DebugOperatingStock("Steel");
            bool passed = phase == 1 ? gunShots > 0 && fuel.Fuel < initialFuel :
                phase == 2 ? fuel.Fuel > 0 && steel < initialSteel && servicePawn >= 0 :
                phase == 4 ? pawnShots > 0 :
                Find.TickManager.TicksGame >= deadline && gun.TryGetComp<CompPowerTrader>()?.PowerOn == false &&
                    fuel.Fuel == initialFuel && gunShots == 0;
            if (!passed && Find.TickManager.TicksGame < deadline && visitor != null && !visitor.Dead && !visitor.Downed) return;
            P2Log.Message("P4E_AUTOCANNON_PROBE_COMPLETE", "map=" + map.uniqueID + " phase=" + phase + " passed=" + passed +
                " elapsed=" + (Find.TickManager.TicksGame - started) + " gun=" + gun.thingIDNumber + " gunShots=" + gunShots +
                " pawnShots=" + pawnShots + " fuel=" + fuel.Fuel + "/" + initialFuel + " reserveSteel=" + steel + "/" + initialSteel +
                " servicePawn=" + servicePawn + " powerOn=" + gun.TryGetComp<CompPowerTrader>()?.PowerOn);
            phase = 0; Find.TickManager.Pause();
        }
    }
}
