using System.Threading.Tasks;

namespace Yafc.Model;

public sealed class DirectAutoPlannerSolveOrchestrator : IAutoPlannerSolveOrchestrator {
    public static DirectAutoPlannerSolveOrchestrator Instance { get; } = new();

    private DirectAutoPlannerSolveOrchestrator() { }

    public Task<string?> SolveAsync(ProjectPage page, AutoPlanner planner) {
        var input = planner.CaptureSolveInput();
        var result = AutoPlanner.ComputeSolveResult(input);
        return Task.FromResult(planner.CommitSolveResult(result));
    }
}
