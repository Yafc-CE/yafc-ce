using System.Threading.Tasks;

namespace Yafc.Model;

/// <summary>
/// Coordinates the AutoPlanner capture, compute, and commit pipeline.
/// </summary>
/// <remarks>
/// Implementations own where each phase runs. UI and app layers should use this seam to schedule background
/// computation and foreground commits without changing the planner's solve logic.
/// </remarks>
public interface IAutoPlannerSolveOrchestrator {
    /// <summary>
    /// Solves <paramref name="planner"/> for <paramref name="page"/> and commits the result.
    /// </summary>
    /// <returns>A localized error message when solving fails; otherwise, <see langword="null"/>.</returns>
    Task<string?> SolveAsync(ProjectPage page, AutoPlanner planner);
}
