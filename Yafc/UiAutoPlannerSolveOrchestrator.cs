using System.Threading.Tasks;
using Yafc.Model;

namespace Yafc;

public sealed class UiAutoPlannerSolveOrchestrator : IAutoPlannerSolveOrchestrator {
    public static UiAutoPlannerSolveOrchestrator Instance { get; } = new();

    private UiAutoPlannerSolveOrchestrator() { }

    public async Task<string?> SolveAsync(ProjectPage page, AutoPlanner planner) {
        var threadSwitcher = page.owner.modelThreadSwitcher;

        await threadSwitcher.SwitchToForeground();
        var input = planner.CaptureSolveInput();

        var result = await Task.Run(() => AutoPlanner.ComputeSolveResult(input)).ConfigureAwait(false);

        await threadSwitcher.SwitchToForeground();
        return planner.CommitSolveResult(result);
    }
}
