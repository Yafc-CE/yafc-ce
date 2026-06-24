using System.Threading.Tasks;

namespace Yafc.Model;

/// <summary>
/// Provides the synchronous, headless AutoPlanner solve orchestration.
/// </summary>
/// <remarks>
/// This default implementation composes the shared AutoPlanner solve pipeline with inline scheduling delegates, so
/// headless and test callers use the same business phases as UI callers without adding background scheduling.
/// </remarks>
public sealed class DirectAutoPlannerSolveOrchestrator : IAutoPlannerSolveOrchestrator {
    private static readonly AutoPlannerSolvePipeline pipeline = new(
        static capture => ValueTask.FromResult(capture()),
        static compute => ValueTask.FromResult(compute()),
        static commit => ValueTask.FromResult(commit()));

    /// <summary>
    /// Gets the shared direct orchestrator instance.
    /// </summary>
    public static DirectAutoPlannerSolveOrchestrator Instance { get; } = new();

    private DirectAutoPlannerSolveOrchestrator() { }

    /// <inheritdoc/>
    public Task<string?> SolveAsync(ProjectPage page, AutoPlanner planner) => pipeline.SolveAsync(planner);
}
