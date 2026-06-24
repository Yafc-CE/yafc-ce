using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Yafc.Model.Tests.Model;

[Collection("LuaDependentTests")]
public class DirectAutoPlannerSolveOrchestratorTests {
    [Fact]
    public async Task SolveAsync_WithNoGoals_CommitsEmptyResultSynchronously() {
        var page = CreatePage("Yafc.Model.Tests.BareMinimumLuaData.lua");
        var planner = Assert.IsType<AutoPlanner>(page.content);

        var solveTask = DirectAutoPlannerSolveOrchestrator.Instance.SolveAsync(page, planner);

        Assert.True(solveTask.IsCompletedSuccessfully);
        Assert.Null(await solveTask);
        var tiers = Assert.IsType<AutoPlannerRecipe[][]>(planner.tiers);
        Assert.Empty(tiers);
    }

    [Fact]
    public async Task SolveAsync_WithSolvableGoal_CommitsComputedTiersSynchronously() {
        var page = CreatePage("Yafc.Model.Tests.Model.DirectAutoPlannerSolveOrchestratorTests.lua");
        var planner = Assert.IsType<AutoPlanner>(page.content);
        var raw = Database.goods.all.Single(g => g.name == "raw");
        var product = Database.goods.all.Single(g => g.name == "product");
        var makeIntermediate = Assert.IsType<Recipe>(Database.recipes.all.Single(r => r.name == "make-intermediate"));
        var makeProduct = Assert.IsType<Recipe>(Database.recipes.all.Single(r => r.name == "make-product"));
        MarkAccessible(page.owner, makeIntermediate, makeProduct);
        Analysis.Do<Milestones>(page.owner);
        CostAnalysis.Instance.recipeCost[makeIntermediate] = 1f;
        CostAnalysis.Instance.recipeCost[makeProduct] = 1f;
        planner.roots.Add(raw);
        planner.goals.Add(new AutoPlannerGoal {
            item = product,
            amount = 1f,
        });

        var solveTask = DirectAutoPlannerSolveOrchestrator.Instance.SolveAsync(page, planner);

        Assert.True(solveTask.IsCompletedSuccessfully);
        Assert.Null(await solveTask);
        var tiers = Assert.IsType<AutoPlannerRecipe[][]>(planner.tiers);
        var committedRecipes = tiers.SelectMany(tier => tier).ToArray();
        Assert.Equal([makeIntermediate, makeProduct], committedRecipes.Select(recipe => recipe.recipe));
        Assert.All(committedRecipes, recipe => Assert.InRange(recipe.recipesPerSecond, 0.999f, 1.001f));
        Assert.Collection(tiers,
            tier => {
                var recipe = Assert.Single(tier);
                Assert.Same(makeIntermediate, recipe.recipe);
                Assert.Equal(0, recipe.tier);
            },
            tier => {
                var recipe = Assert.Single(tier);
                Assert.Same(makeProduct, recipe.recipe);
                Assert.Equal(1, recipe.tier);
            });
    }

    private static ProjectPage CreatePage(string luaResourceName) {
        var project = LuaDependentTestHelper.GetProjectForLua(luaResourceName);

        return new ProjectPage(project, typeof(AutoPlanner));
    }

    private static void MarkAccessible(Project project, params FactorioObject[] objects) {
        foreach (var obj in objects) {
            project.settings.SetFlag(obj, ProjectPerItemFlags.MarkedAccessible, true);
        }
    }
}
