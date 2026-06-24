namespace Yafc.Model;

public enum AutoPlannerSolveStatus {
    Success,
    NoSolution
}

public readonly record struct AutoPlannerGoalSnapshot(Goods item, float amount);

public readonly record struct AutoPlannerRecipeSnapshot(Recipe recipe, bool isAccessible, float baseCost);

public sealed record AutoPlannerSolveInput(
    AutoPlannerGoalSnapshot[] goals,
    Goods[] roots,
    AutoPlannerRecipeSnapshot[] recipes);

public sealed record AutoPlannerSolveResult(AutoPlannerSolveStatus status, AutoPlannerRecipe[][]? tiers) {
    public static AutoPlannerSolveResult Success(AutoPlannerRecipe[][] tiers) => new(AutoPlannerSolveStatus.Success, tiers);

    public static AutoPlannerSolveResult NoSolution { get; } = new(AutoPlannerSolveStatus.NoSolution, null);
}
