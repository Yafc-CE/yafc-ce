using System.Threading.Tasks;

namespace Yafc.Model;

/// <summary>
/// Provides synchronous, headless AutoPlanner solve orchestration.
/// </summary>
/// <remarks>
/// Headless and test callers run capture, compute, and commit inline without background scheduling.
/// Keep the capture/compute/commit sequence in sync with UiAutoPlannerSolveOrchestrator; only scheduling differs.
/// </remarks>
public sealed class DirectAutoPlannerSolveOrchestrator : IAutoPlannerSolveOrchestrator {
    /// <summary>
    /// Gets the shared direct orchestrator instance.
    /// </summary>
    public static DirectAutoPlannerSolveOrchestrator Instance { get; } = new();

    private DirectAutoPlannerSolveOrchestrator() { }

    /// <inheritdoc/>
    public Task<string?> SolveAsync(ProjectPage page, AutoPlanner planner) {
        var input = planner.CaptureSolveInput();
        var result = AutoPlanner.ComputeSolveResult(input);
        return Task.FromResult(planner.CommitSolveResult(result));
    }
}
