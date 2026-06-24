using System.Threading.Tasks;
using Yafc.Model;

namespace Yafc;

/// <summary>
/// Orchestrates AutoPlanner solves for the UI application.
/// </summary>
/// <remarks>
/// Capture and commit run on the foreground model context, while compute runs on a background thread.
/// </remarks>
public sealed class UiAutoPlannerSolveOrchestrator : IAutoPlannerSolveOrchestrator {
    /// <summary>
    /// Gets the shared UI orchestrator instance.
    /// </summary>
    public static UiAutoPlannerSolveOrchestrator Instance { get; } = new();

    private UiAutoPlannerSolveOrchestrator() { }

    /// <inheritdoc/>
    public async Task<string?> SolveAsync(ProjectPage page, AutoPlanner planner) {
        var threadSwitcher = page.owner.modelThreadSwitcher;

        // Capture and commit touch model state; compute only uses captured snapshots.
        await threadSwitcher.SwitchToForeground();
        var input = planner.CaptureSolveInput();

        var result = await Task.Run(() => AutoPlanner.ComputeSolveResult(input)).ConfigureAwait(false);

        await threadSwitcher.SwitchToForeground();
        return planner.CommitSolveResult(result);
    }
}
