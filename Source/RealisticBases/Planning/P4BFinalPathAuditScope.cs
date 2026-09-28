using System;

namespace RealisticBases.Planning
{
    // Generation batches path-grid mutations. A synchronous audit must flush
    // them, then restore the caller's batching mode even when it fails.
    public sealed class P4BFinalPathAuditScope : IDisposable
    {
        private Action restore;

        public P4BFinalPathAuditScope(bool batchingDisabledUpdates, Action flushAndEnable, Action restoreBatching)
        {
            if (!batchingDisabledUpdates) return;
            if (flushAndEnable == null || restoreBatching == null) throw new ArgumentNullException();
            restore = restoreBatching;
            try { flushAndEnable(); }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            Action action = restore;
            restore = null;
            action?.Invoke();
        }
    }
}
