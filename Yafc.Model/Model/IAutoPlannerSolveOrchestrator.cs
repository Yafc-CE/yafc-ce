using System.Threading.Tasks;

namespace Yafc.Model;

/// <summary>
/// Coordinates the AutoPlanner capture, compute, and commit pipeline.
/// </summary>
/// <remarks>
/// Implementations compose <see cref="AutoPlannerSolvePipeline"/> to keep solve phases consistent while varying only
/// scheduling decisions such as foreground dispatch or background computation.
/// </remarks>
public interface IAutoPlannerSolveOrchestrator {
    /// <summary>
    /// Solves <paramref name="planner"/> for <paramref name="page"/> and commits the result.
    /// </summary>
    /// <returns>A localized error message when solving fails; otherwise, <see langword="null"/>.</returns>
    Task<string?> SolveAsync(ProjectPage page, AutoPlanner planner);
}
