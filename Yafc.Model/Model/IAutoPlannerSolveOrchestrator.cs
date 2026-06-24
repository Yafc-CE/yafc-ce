using System.Threading.Tasks;

namespace Yafc.Model;

public interface IAutoPlannerSolveOrchestrator {
    Task<string?> SolveAsync(ProjectPage page, AutoPlanner planner);
}
