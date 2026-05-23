using System;
using System.Collections.Generic;
using System.IO;

namespace Yafc.Model;

public class UndoSystem {
    /// <summary>
    /// Gets or sets the fallback scheduler used when <see cref="UndoSystem()" /> is called without
    /// an explicit scheduler argument. Set this to a UI-aware implementation (e.g.
    /// <c>GestureFinishUndoBatchScheduler</c>) before creating any <see cref="Project"/> instances
    /// in an interactive session. Defaults to <see cref="ImmediateUndoBatchScheduler"/>.
    /// </summary>
    public static IUndoBatchScheduler DefaultScheduler { get; set; } = new ImmediateUndoBatchScheduler();

    private readonly IUndoBatchScheduler _scheduler;

    /// <summary>Initialises a new <see cref="UndoSystem"/> using <see cref="DefaultScheduler"/>.</summary>
    public UndoSystem() : this(CreateDefaultScheduler()) { }

    /// <summary>Initialises a new <see cref="UndoSystem"/> with an explicit <paramref name="scheduler"/>.</summary>
    public UndoSystem(IUndoBatchScheduler scheduler) {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
    }

    private static IUndoBatchScheduler CreateDefaultScheduler()
        => DefaultScheduler is ImmediateUndoBatchScheduler ? new ImmediateUndoBatchScheduler() : DefaultScheduler;

    public uint version {
        get => _version;
        private set {
            if (_version != value) {
                _version = value;
                versionChanged?.Invoke();
            }
        }
    }
    public event Action? versionChanged;
    private bool undoBatchVisualOnly = true;
    private readonly List<UndoSnapshot> currentUndoBatch = [];
    private readonly List<ModelObject> changedList = [];
    private readonly Stack<UndoBatch> undo = new Stack<UndoBatch>();
    private readonly Stack<UndoBatch> redo = new Stack<UndoBatch>();
    private bool suspended;
    private bool scheduled;
    private uint scheduleGeneration;
    private uint _version = 2;

    internal void CreateUndoSnapshot(ModelObject target, bool visualOnly) {
        if (SerializationMap.IsDeserializing) {
            throw new InvalidOperationException("Do not record an undo event while deserializing.");
        }

        bool isFirstChangeInBatch = changedList.Count == 0;

        if (isFirstChangeInBatch) {
            version++;
        }

        bool shouldSchedule = isFirstChangeInBatch && !suspended && !scheduled;
        undoBatchVisualOnly &= visualOnly;

        if (target.objectVersion == version) {
            return;
        }

        changedList.Add(target);
        target.objectVersion = version;

        if (visualOnly && undo.Count > 0 && undo.Peek().Contains(target)) {
            if (shouldSchedule) {
                Schedule();
            }

            return;
        }

        var builder = target.GetUndoBuilder();
        currentUndoBatch.Add(builder.MakeUndoSnapshot(target));

        if (shouldSchedule) {
            Schedule();
        }
    }

    private static void MakeUndoBatch(object? state) {
        ScheduledUndoBatch scheduledBatch = (ScheduledUndoBatch)state!; // null-forgiving: Only called by Schedule, which passes a state instance.
        UndoSystem system = scheduledBatch.system;

        if (scheduledBatch.generation != system.scheduleGeneration) {
            return;
        }

        system.CommitUndoBatch();
    }

    private void CommitUndoBatch() {
        scheduled = false;
        bool visualOnly = undoBatchVisualOnly;

        for (int i = 0; i < changedList.Count; i++) {
            changedList[i].ThisChanged(visualOnly);
        }

        changedList.Clear();

        if (currentUndoBatch.Count == 0) {
            return;
        }

        UndoBatch batch = new UndoBatch([.. currentUndoBatch], visualOnly);
        undo.Push(batch);
        undoBatchVisualOnly = true;
        redo.Clear();
        currentUndoBatch.Clear();
    }

    private void Schedule() {
        scheduled = true;
        _scheduler.ScheduleOnGestureFinish(MakeUndoBatch, new ScheduledUndoBatch(this, ++scheduleGeneration));
    }

    /// <summary>Commits the current pending undo batch immediately, if one exists.</summary>
    public void FlushPendingChanges() {
        if (changedList.Count == 0) {
            return;
        }

        if (_scheduler is ImmediateUndoBatchScheduler immediateScheduler) {
            immediateScheduler.RunPendingCallbacks();

            if (changedList.Count == 0) {
                return;
            }
        }

        scheduleGeneration++;
        CommitUndoBatch();
    }

    internal void FlushPendingChangesIfImmediateScheduler() {
        if (!suspended && _scheduler is ImmediateUndoBatchScheduler immediateScheduler) {
            immediateScheduler.RunPendingCallbacks();

            if (changedList.Count > 0) {
                scheduleGeneration++;
                CommitUndoBatch();
            }
        }
    }

    private sealed class ScheduledUndoBatch(UndoSystem system, uint generation) {
        public readonly UndoSystem system = system;
        public readonly uint generation = generation;
    }

    public void Suspend() => suspended = true;

    public void Resume() {
        suspended = false;

        if (!scheduled && changedList.Count > 0) {
            Schedule();
            FlushPendingChangesIfImmediateScheduler();
        }
    }

    public void PerformUndo() {
        if (CanUndo) {
            redo.Push(undo.Pop().Restore(++version));
        }
    }

    public bool CanUndo {
        get {
            FlushPendingChangesIfImmediateScheduler();
            return !(undo.Count == 0 || changedList.Count > 0);
        }
    }

    public void PerformRedo() {
        FlushPendingChangesIfImmediateScheduler();

        if (redo.Count == 0 || changedList.Count > 0) {
            return;
        }

        undo.Push(redo.Pop().Restore(++version));
    }

    public void RecordChange() => version++;

    public bool HasChangesPending(ModelObject obj) {
        FlushPendingChangesIfImmediateScheduler();
        return changedList.Contains(obj);
    }
}
internal readonly struct UndoSnapshot(ModelObject target, object?[]? managed, byte[]? unmanaged) {
    internal readonly ModelObject target = target;
    internal readonly object?[]? managedReferences = managed;
    internal readonly byte[]? unmanagedData = unmanaged;

    public UndoSnapshot Restore() {
        var builder = target.GetUndoBuilder();
        var redo = builder.MakeUndoSnapshot(target);
        builder.RevertToUndoSnapshot(target, this);
        return redo;
    }
}

internal readonly struct UndoBatch(UndoSnapshot[] snapshots, bool visualOnly) {
    public readonly UndoSnapshot[] snapshots = snapshots;
    public readonly bool visualOnly = visualOnly;

    public UndoBatch Restore(uint undoState) {
        for (int i = 0; i < snapshots.Length; i++) {
            snapshots[i] = snapshots[i].Restore();
            snapshots[i].target.objectVersion = undoState;
        }

        foreach (var snapshot in snapshots) {
            snapshot.target.AfterDeserialize();
        }

        foreach (var snapshot in snapshots) {
            snapshot.target.ThisChanged(visualOnly);
        }

        return this;
    }

    public bool Contains(ModelObject target) {
        foreach (var snapshot in snapshots) {
            if (snapshot.target == target) {
                return true;
            }
        }

        return false;
    }
}

internal class UndoSnapshotBuilder {
    private readonly MemoryStream stream = new MemoryStream();
    private readonly List<object?> managedRefs = [];
    public readonly BinaryWriter writer;
    private readonly ModelObject currentTarget;

    internal UndoSnapshotBuilder(ModelObject target) {
        writer = new BinaryWriter(stream);
        currentTarget = target;
    }

    internal UndoSnapshot Build() {
        byte[]? buffer = null;

        if (stream.Position > 0) {
            buffer = new byte[stream.Position];
            Array.Copy(stream.GetBuffer(), buffer, stream.Position);
        }

        UndoSnapshot result = new UndoSnapshot(currentTarget, managedRefs.Count > 0 ? [.. managedRefs] : null, buffer);
        stream.Position = 0;
        managedRefs.Clear();

        return result;
    }

    public void WriteManagedReference(object? reference) => managedRefs.Add(reference);

    public void WriteManagedReferences(IEnumerable<object> references) => managedRefs.AddRange(references);
}

internal class UndoSnapshotReader {
    private static readonly BinaryReader NullReader = new BinaryReader(Stream.Null);
    public BinaryReader reader { get; }
    private int refId;
    private readonly object?[]? managed;

    internal UndoSnapshotReader(UndoSnapshot snapshot) {
        if (snapshot.unmanagedData != null) {
            MemoryStream stream = new MemoryStream(snapshot.unmanagedData, false);
            reader = new BinaryReader(stream);
        }
        else {
            reader = NullReader;
        }

        managed = snapshot.managedReferences;
        refId = 0;
    }

    public object? ReadManagedReference() {
        if (managed == null) {
            throw new InvalidOperationException("No managed objects are available to read in this undo snapshot.");
        }

        return managed[refId++];
    }

    public T? ReadOwnedReference<T>(ModelObject owner) where T : ModelObject {
        T? obj = ReadManagedReference() as T;
        if (obj != null && obj.ownerObject != owner) {
            obj.ownerObject = owner;
        }

        return obj;
    }
}
