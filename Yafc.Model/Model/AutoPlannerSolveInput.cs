namespace Yafc.Model;

/// <summary>
/// Describes the result state produced by AutoPlanner computation.
/// </summary>
public enum AutoPlannerSolveStatus {
    /// <summary>
    /// A feasible or optimal recipe-flow solution was found, and planner tiers are available.
    /// </summary>
    Success,

    /// <summary>
    /// No optimal or feasible recipe-flow solution was found for the captured input.
    /// </summary>
    NoSolution
}

/// <summary>
/// Captures one requested AutoPlanner output for computation.
/// </summary>
/// <param name="item">The requested goods item.</param>
/// <param name="amount">The requested item rate.</param>
public readonly record struct AutoPlannerGoalSnapshot(Goods item, float amount);

/// <summary>
/// Captures recipe state needed by AutoPlanner computation.
/// </summary>
/// <param name="recipe">The recipe to consider.</param>
/// <param name="isAccessible">Whether the recipe is available for the current milestones.</param>
/// <param name="baseCost">The recipe's base cost at capture time.</param>
public readonly record struct AutoPlannerRecipeSnapshot(Recipe recipe, bool isAccessible, float baseCost);

/// <summary>
/// Contains the captured state required to compute an AutoPlanner result.
/// </summary>
/// <param name="goals">The requested AutoPlanner outputs.</param>
/// <param name="roots">The goods that should be treated as available roots.</param>
/// <param name="recipes">The recipe availability and cost state to use during computation.</param>
public sealed record AutoPlannerSolveInput(
    AutoPlannerGoalSnapshot[] goals,
    Goods[] roots,
    AutoPlannerRecipeSnapshot[] recipes);

/// <summary>
/// Contains the computed AutoPlanner result before it is committed to a planner.
/// </summary>
/// <param name="status">The computation outcome.</param>
/// <param name="tiers">The computed tier layout when <paramref name="status"/> is <see cref="AutoPlannerSolveStatus.Success"/>.</param>
public sealed record AutoPlannerSolveResult(AutoPlannerSolveStatus status, AutoPlannerRecipe[][]? tiers) {
    /// <summary>
    /// Creates a successful solve result with the computed tier layout.
    /// </summary>
    /// <param name="tiers">The tier layout to commit to the planner.</param>
    public static AutoPlannerSolveResult Success(AutoPlannerRecipe[][] tiers) => new(AutoPlannerSolveStatus.Success, tiers);

    /// <summary>
    /// Gets the shared result used when computation cannot find an optimal or feasible recipe-flow solution.
    /// </summary>
    public static AutoPlannerSolveResult NoSolution { get; } = new(AutoPlannerSolveStatus.NoSolution, null);
}
