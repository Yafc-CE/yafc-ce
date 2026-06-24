using System.Threading.Tasks;

namespace Yafc.Model;

/// <summary>
/// Provides the synchronous, headless AutoPlanner solve orchestration.
/// </summary>
/// <remarks>
/// This default implementation captures input, computes the result, and commits it on the caller's flow without
/// adding background scheduling.
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
