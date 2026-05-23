using System.Collections.Generic;
using System.Threading;

namespace Yafc.Model;

/// <summary>
/// An <see cref="IUndoBatchScheduler"/> that queues callbacks until the owning
/// <see cref="UndoSystem"/> reaches a headless flush point. This preserves the UI scheduler's
/// post-mutation commit semantics without requiring a UI event loop.
/// </summary>
public sealed class ImmediateUndoBatchScheduler : IUndoBatchScheduler {
    private readonly Queue<(SendOrPostCallback Callback, object State)> callbacks = [];
    private bool isDraining;

    /// <inheritdoc />
    public void ScheduleOnGestureFinish(SendOrPostCallback callback, object state) => callbacks.Enqueue((callback, state));

    internal void RunPendingCallbacks() {
        if (isDraining) {
            return;
        }

        isDraining = true;
        try {
            while (callbacks.Count > 0) {
                var (callback, state) = callbacks.Dequeue();
                callback(state);
            }
        }
        finally {
            isDraining = false;
        }
    }
}
