using System.Collections.Generic;

namespace RealisticBases.Planning
{
    // Runtime-only backoff: a failed service must not re-enter the same job
    // in the same tick. Other targets and workers remain available.
    public sealed class P3ServiceRetry
    {
        public const int Delay = 300;
        private readonly Dictionary<long, int> until = new Dictionary<long, int>();
        private static long Key(int pawn, int target) => ((long)pawn << 32) | (uint)target;
        public bool Allowed(int pawn, int target, int tick) =>
            !until.TryGetValue(Key(pawn, target), out int expiry) || tick >= expiry;
        public void Failed(int pawn, int target, int tick) => until[Key(pawn, target)] = tick + Delay;
    }
}
