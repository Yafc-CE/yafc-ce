using System;
using System.Threading.Tasks;
using Yafc.Model;

namespace Yafc;

/// <summary>
/// Orchestrates AutoPlanner solves for the UI application.
/// </summary>
/// <remarks>
/// This implementation composes the shared AutoPlanner solve pipeline with UI scheduling delegates: foreground
/// dispatch for capture and commit, and <see cref="Task.Run"/> for compute.
/// </remarks>
public sealed class UiAutoPlannerSolveOrchestrator : IAutoPlannerSolveOrchestrator {
    /// <summary>
    /// Gets the shared UI orchestrator instance.
    /// </summary>
    public static UiAutoPlannerSolveOrchestrator Instance { get; } = new();

    private UiAutoPlannerSolveOrchestrator() { }

    /// <inheritdoc/>
    public Task<string?> SolveAsync(ProjectPage page, AutoPlanner planner) {
        var threadSwitcher = page.owner.modelThreadSwitcher;
        var pipeline = new AutoPlannerSolvePipeline(
            capture => RunOnForegroundAsync(threadSwitcher, capture),
            static compute => new ValueTask<AutoPlannerSolveResult>(Task.Run(compute)),
            commit => RunOnForegroundAsync(threadSwitcher, commit));

        return pipeline.SolveAsync(planner);
    }

    private static async ValueTask<T> RunOnForegroundAsync<T>(IModelThreadSwitcher threadSwitcher, Func<T> action) {
        await threadSwitcher.SwitchToForeground();
        return action();
    }
}
