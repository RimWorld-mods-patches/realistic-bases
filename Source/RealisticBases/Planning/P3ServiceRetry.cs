using System.Collections.Generic;
using System.Linq;

namespace RealisticBases.Planning
{
    // Backoff: a failed service must not re-enter the same job in the same
    // tick. Other targets and workers remain available. Pending entries are
    // saved with the controller so a reload keeps the same decisions.
    public sealed class P3ServiceRetry
    {
        public const int Delay = 300;
        private readonly Dictionary<long, int> until = new Dictionary<long, int>();
        private static long Key(int pawn, int target) => ((long)pawn << 32) | (uint)target;
        public bool Allowed(int pawn, int target, int tick) =>
            !until.TryGetValue(Key(pawn, target), out int expiry) || tick >= expiry;
        public void Failed(int pawn, int target, int tick) => until[Key(pawn, target)] = tick + Delay;

        // Entries still blocking at tick, ordered by key.
        public List<KeyValuePair<long, int>> Pending(int tick) =>
            until.Where(e => tick < e.Value).OrderBy(e => e.Key).ToList();

        public void Restore(IList<long> keys, IList<int> expiries)
        {
            until.Clear();
            for (int i = 0; i < keys.Count && i < expiries.Count; i++) until[keys[i]] = expiries[i];
        }
    }
}
