using System;
using System.Collections.Generic;
using System.Linq;

namespace RealisticBases.Planning
{
    // P5.4: what an ordinary settlement keeps between visits. The roster and
    // supported asset state are recorded before native teardown and restored
    // on re-entry; nothing here grants a replacement or a fresh initial fill.
    public enum P5ResidentFate
    {
        Restored,
        Dead,
        Captured,
        Departed,
        Missing,
    }

    public struct P5ResidentStatus
    {
        public P5ResidentStatus(bool found, bool dead, bool sameFaction, bool captured)
        {
            Found = found;
            Dead = dead;
            SameFaction = sameFaction;
            Captured = captured;
        }

        public bool Found { get; }
        public bool Dead { get; }
        public bool SameFaction { get; }
        public bool Captured { get; }
    }

    public static class P5RosterRestore
    {
        // Survivors return in their archived order. Casualties, captives and
        // residents who left the faction are not replaced.
        public static IReadOnlyList<KeyValuePair<int, P5ResidentFate>> Resolve(IEnumerable<int> archived,
            Func<int, P5ResidentStatus> status)
        {
            if (status == null) throw new ArgumentNullException(nameof(status));
            var result = new List<KeyValuePair<int, P5ResidentFate>>();
            foreach (int id in (archived ?? Enumerable.Empty<int>()).Distinct())
            {
                P5ResidentStatus s = status(id);
                P5ResidentFate fate = !s.Found ? P5ResidentFate.Missing
                    : s.Dead ? P5ResidentFate.Dead
                    : s.Captured ? P5ResidentFate.Captured
                    : !s.SameFaction ? P5ResidentFate.Departed
                    : P5ResidentFate.Restored;
                result.Add(new KeyValuePair<int, P5ResidentFate>(id, fate));
            }
            return result;
        }
    }

    // One building's supported state. Negative values mean "not applicable".
    public sealed class P5AssetSnapshot
    {
        public P5AssetSnapshot(string key, int hitPoints, float fuel, float storedEnergy, string shellDef, int shellCount)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Missing asset key");
            Key = key;
            HitPoints = hitPoints;
            Fuel = fuel;
            StoredEnergy = storedEnergy;
            ShellDef = string.IsNullOrEmpty(shellDef) || shellCount <= 0 ? null : shellDef;
            ShellCount = ShellDef == null ? 0 : shellCount;
        }

        public string Key { get; }
        public int HitPoints { get; }
        public float Fuel { get; }
        public float StoredEnergy { get; }
        public string ShellDef { get; }
        public int ShellCount { get; }

        // Layout positions are deterministic for one layout digest, so the
        // definition and footprint origin identify the regenerated asset.
        public static string KeyFor(string defName, int x, int z) => defName + "@" + x + "," + z;

        // Restoring never exceeds what the asset can hold, and never raises
        // it above the archived amount: damage, depletion and discharge stay.
        public int RestoredHitPoints(int maxHitPoints) =>
            HitPoints < 0 ? maxHitPoints : Math.Max(1, Math.Min(maxHitPoints, HitPoints));
        public float RestoredFuel(float capacity) => Fuel < 0 ? -1 : Math.Max(0, Math.Min(capacity, Fuel));
        public float RestoredEnergy(float capacity) => StoredEnergy < 0 ? -1 : Math.Max(0, Math.Min(capacity, StoredEnergy));
    }
}
