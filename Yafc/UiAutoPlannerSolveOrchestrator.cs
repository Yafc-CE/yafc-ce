using System.Threading.Tasks;
using Yafc.Model;

namespace Yafc;

/// <summary>
/// Orchestrates AutoPlanner solves for the UI application.
/// </summary>
/// <remarks>
/// This implementation captures and commits on the foreground model thread while scheduling compute work on a
/// background task.
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

        await threadSwitcher.SwitchToForeground();
        var input = planner.CaptureSolveInput();

        var result = await Task.Run(() => AutoPlanner.ComputeSolveResult(input)).ConfigureAwait(false);

        await threadSwitcher.SwitchToForeground();
        return planner.CommitSolveResult(result);
    }
}
