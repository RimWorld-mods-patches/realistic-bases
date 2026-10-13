using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealisticBases.Planning
{
    // P5 operating-reserve persistence across map unload/re-entry: what was
    // left of each generated reserve when the encounter map was removed.
    // Re-entry spawns at most that remainder; off-map balances stay frozen,
    // so leaving and returning is never a refill. Keys are def or def|stuff.
    public sealed class P5ReserveLedger
    {
        private readonly Dictionary<string, int> remaining;

        public P5ReserveLedger(IEnumerable<KeyValuePair<string, int>> remaining)
        {
            this.remaining = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> entry in remaining ?? Enumerable.Empty<KeyValuePair<string, int>>())
            {
                if (string.IsNullOrEmpty(entry.Key) || entry.Value < 0 || this.remaining.ContainsKey(entry.Key))
                    throw new ArgumentException("Invalid reserve ledger entry " + entry.Key);
                this.remaining.Add(entry.Key, entry.Value);
            }
            Remaining = new ReadOnlyDictionary<string, int>(this.remaining);
        }

        public IReadOnlyDictionary<string, int> Remaining { get; }

        public static string Key(string def, string stuff = null) =>
            string.IsNullOrEmpty(stuff) ? def : def + "|" + stuff;

        // A reserve absent from the ledger was not generated at the last visit
        // (for example a new stage or layout); it keeps its planned amount.
        public int Cap(string key, int planned)
        {
            if (planned < 0) throw new ArgumentOutOfRangeException(nameof(planned));
            int left;
            return remaining.TryGetValue(key, out left) ? Math.Min(planned, left) : planned;
        }

        // Several planned stocks can share a key (generator fuel and fuel wood
        // are both WoodLog). The remainder is one allowance: each grant reduces
        // what later stocks of the same key may receive within one generation.
        public int Grant(string key, int planned, IDictionary<string, int> granted)
        {
            if (granted == null) throw new ArgumentNullException(nameof(granted));
            if (planned < 0) throw new ArgumentOutOfRangeException(nameof(planned));
            int left;
            if (!remaining.TryGetValue(key, out left)) return planned;
            int already;
            granted.TryGetValue(key, out already);
            int grant = Math.Max(0, Math.Min(planned, left - already));
            granted[key] = already + grant;
            return grant;
        }

        // Unload accounting: the observed remainder, never above what was
        // generated for that key (stray same-def items do not become reserves).
        public static P5ReserveLedger Observe(IEnumerable<KeyValuePair<string, int>> planned, Func<string, int> observed)
        {
            if (observed == null) throw new ArgumentNullException(nameof(observed));
            return new P5ReserveLedger((planned ?? Enumerable.Empty<KeyValuePair<string, int>>())
                .GroupBy(p => p.Key, StringComparer.Ordinal)
                .Select(g => new KeyValuePair<string, int>(g.Key, Math.Max(0, Math.Min(g.Sum(p => p.Value), observed(g.Key))))));
        }
    }
}
