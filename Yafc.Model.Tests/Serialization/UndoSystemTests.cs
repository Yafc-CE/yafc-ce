using System;
using System.Collections.Generic;
using System.Threading;
using Xunit;

namespace Yafc.Model.Tests;

public class UndoSystemTests {
    [Fact]
    public void DefaultImmediateSchedulers_DoNotSharePendingCallbacks() {
        IUndoBatchScheduler previousDefaultScheduler = UndoSystem.DefaultScheduler;
        UndoSystem.DefaultScheduler = new ImmediateUndoBatchScheduler();

        try {
            var first = new Project();
            var second = new Project();
            int firstChanges = 0;
            int secondChanges = 0;
            first.settings.changed += _ => firstChanges++;
            second.settings.changed += _ => secondChanges++;

            first.settings.RecordUndo().miningProductivity = 1f;
            second.settings.RecordUndo().miningProductivity = 2f;

            Assert.True(second.undo.CanUndo);

            Assert.Equal(0, firstChanges);
            Assert.Equal(1, secondChanges);

            first.undo.FlushPendingChanges();

            Assert.Equal(1, firstChanges);
            Assert.True(first.undo.CanUndo);
        }
        finally {
            UndoSystem.DefaultScheduler = previousDefaultScheduler;
        }
    }

    [Fact]
    public void ImmediateScheduler_FlushCommitsAfterMutation() {
        var project = new Project(new ImmediateUndoBatchScheduler());
        float changedValue = -1f;
        project.settings.changed += _ => changedValue = project.settings.miningProductivity;

        project.settings.RecordUndo().miningProductivity = 1.25f;
        project.undo.FlushPendingChanges();

        Assert.Equal(1.25f, changedValue);
        Assert.True(project.undo.CanUndo);
    }

    [Fact]
    public void ImmediateScheduler_CanUndoFlushesAfterMutation() {
        var project = new Project(new ImmediateUndoBatchScheduler());

        project.RecordUndo().yafcVersion = "immediate";

        Assert.True(project.undo.CanUndo);
        Assert.False(project.undo.HasChangesPending(project));
    }

    [Fact]
    public void ScheduledCommit_CompletesPendingBatch() {
        var scheduler = new DeferredUndoBatchScheduler();
        var project = new Project(scheduler);

        project.RecordUndo().yafcVersion = "scheduled";

        Assert.False(project.undo.CanUndo);
        Assert.True(project.undo.HasChangesPending(project));

        scheduler.RunNext();

        Assert.True(project.undo.CanUndo);
        Assert.False(project.undo.HasChangesPending(project));

        project.undo.PerformUndo();
        Assert.Null(project.yafcVersion);
    }

    [Fact]
    public void FlushPendingChanges_InvalidatesPreviouslyScheduledCommit() {
        var scheduler = new DeferredUndoBatchScheduler();
        var project = new Project(scheduler);

        project.RecordUndo().yafcVersion = "flushed";
        project.undo.FlushPendingChanges();
        project.RecordUndo().yafcVersion = "scheduled";

        Assert.Equal(2, scheduler.PendingCount);
        Assert.False(project.undo.CanUndo);
        Assert.True(project.undo.HasChangesPending(project));

        scheduler.RunNext();

        Assert.False(project.undo.CanUndo);
        Assert.True(project.undo.HasChangesPending(project));

        scheduler.RunNext();

        Assert.True(project.undo.CanUndo);
        Assert.False(project.undo.HasChangesPending(project));

        project.undo.PerformUndo();
        Assert.Equal("flushed", project.yafcVersion);
    }

    private sealed class DeferredUndoBatchScheduler : IUndoBatchScheduler {
        private readonly Queue<Action> callbacks = [];

        public int PendingCount => callbacks.Count;

        public void ScheduleOnGestureFinish(SendOrPostCallback callback, object state)
            => callbacks.Enqueue(() => callback(state));

        public void RunNext() => callbacks.Dequeue()();
    }
}
