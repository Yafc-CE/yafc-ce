using System.Threading.Tasks;

namespace Yafc.Model;

/// <summary>
/// Coordinates AutoPlanner solve scheduling.
/// </summary>
/// <remarks>
/// Implementations explicitly choose where capture, compute, and commit execute, such as foreground dispatch or
/// background computation.
/// </remarks>
public interface IAutoPlannerSolveOrchestrator {
    /// <summary>
    /// Solves <paramref name="planner"/> for <paramref name="page"/> and commits the result.
    /// </summary>
    /// <returns>A localized error message when solving fails; otherwise, <see langword="null"/>.</returns>
    Task<string?> SolveAsync(ProjectPage page, AutoPlanner planner);
}
