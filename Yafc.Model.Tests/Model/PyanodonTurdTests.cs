using System.Linq;
using Xunit;

namespace Yafc.Model.Tests.Model;

[Collection("LuaDependentTests")]
public class PyanodonTurdTests {
    private static Module GetModule(string name) => Database.allModules.Single(m => m.name == name);
    private static EntityCrafter GetCrafter(string name) => (EntityCrafter)Database.objectsByTypeName["Entity." + name];
    private static Recipe GetRecipe(string name) => Database.recipes.all.Single(r => r.name == name);

    [Fact]
    public void IsTurdModule_RecognizesTurdModulesByName_WhetherCraftableOrNot() {
        _ = LuaDependentTestHelper.GetProjectForLua();

        // Craftable, as pypostprocessing intends once pybugreports#1632 is fixed.
        Assert.True(GetModule("carbon-capture-module-mk01").IsAutomatable());
        Assert.True(PyanodonTurd.IsTurdModule(GetModule("carbon-capture-module-mk01")));
        // Uncraftable, as today.
        Assert.False(GetModule("carbon-capture-module-mk02").IsAutomatable());
        Assert.True(PyanodonTurd.IsTurdModule(GetModule("carbon-capture-module-mk02")));
        Assert.True(PyanodonTurd.IsTurdModule(GetModule("turd-only-upgrade-module")));

        Assert.False(PyanodonTurd.IsTurdModule(GetModule("moondrop-mk01")));
        // Named like a TURD module, but there is no turd-select-lonely technology.
        Assert.False(PyanodonTurd.IsTurdModule(GetModule("lonely-module")));
    }

    [Fact]
    public void GetSingleCategoryModule_UsesTheFarmModule_AndNeverATurdModule() {
        _ = LuaDependentTestHelper.GetProjectForLua();

        Assert.True(GetModule("carbon-capture-module-mk01").IsAccessibleWithCurrentMilestones());
        Assert.Equal(GetModule("moondrop-mk01"), DefaultModules.GetSingleCategoryModule(GetCrafter("moondrop-greenhouse-mk01"), GetRecipe("moondrop")));

        // The only accessible module of the TURD-only farm is a TURD module.
        Assert.True(GetModule("turd-only-upgrade-module").IsAccessibleWithCurrentMilestones());
        Assert.Null(DefaultModules.GetSingleCategoryModule(GetCrafter("turd-only-farm-mk01"), GetRecipe("turd-only-crop")));
    }

    [Fact]
    public void GetSelectedBonus_AppliesOnlyOnceSelected_AndMatchesTheBuildingTier() {
        Project project = LuaDependentTestHelper.GetProjectForLua();
        Assert.Null(PyanodonTurd.GetSelectedBonus(GetCrafter("moondrop-greenhouse-mk01")));

        SelectTurd(project, "carbon-capture");
        SelectTurd(project, "hh");

        Assert.Equal((0.5f, -0.5f), PyanodonTurd.GetSelectedBonus(GetCrafter("moondrop-greenhouse-mk01")));
        Assert.Equal((1f, -0.5f), PyanodonTurd.GetSelectedBonus(GetCrafter("moondrop-greenhouse-mk02")));
        // A missing tier suffix means mk01.
        Assert.Equal((0.5f, 0f), PyanodonTurd.GetSelectedBonus(GetCrafter("guar-gum-plantation")));
        // Accepts the TURD modules of several upgrades, so it is not their target.
        Assert.Null(PyanodonTurd.GetSelectedBonus(GetCrafter("reproductive-complex")));
        // Not selected.
        Assert.Null(PyanodonTurd.GetSelectedBonus(GetCrafter("turd-only-farm-mk01")));
    }

    [Fact]
    public void CostAnalysis_UsesTheDefaultFarmModule() {
        _ = LuaDependentTestHelper.GetProjectForLua();
        var costs = CostAnalysis.Instance.recipeCost;

        // Both farms are built the same way and both crops take the same time, but only the moondrop greenhouse has a farm module.
        Assert.True(costs[GetRecipe("moondrop")] < costs[GetRecipe("turd-only-crop")]);
    }

    [Fact]
    public void CostAnalysis_UsesTheSelectedTurd() {
        Project project = LuaDependentTestHelper.GetProjectForLua();
        float moondrop = CostAnalysis.Instance.recipeCost[GetRecipe("moondrop")];

        SelectTurd(project, "carbon-capture");
        Assert.True(CostAnalysis.Instance.recipeCost[GetRecipe("moondrop")] < moondrop);
    }

    [Fact]
    public void CostAnalysis_IncludesTheBuildingsBuiltInSpeed() {
        _ = LuaDependentTestHelper.GetProjectForLua();
        var costs = CostAnalysis.Instance.recipeCost;

        // The two greenhouses and their modules are the same, except that the moondrop greenhouse is built with -100% speed.
        Assert.True(costs[GetRecipe("plain-crop")] < costs[GetRecipe("moondrop")]);
    }

    private static void SelectTurd(Project project, string subtech) {
        project.settings.SetFlag(Database.objectsByTypeName["Technology.turd-select-" + subtech], ProjectPerItemFlags.MarkedAccessible, true);
        Analysis.ProcessAnalyses(new System.Progress<(string, string)>(), project, new ErrorCollector());
    }
}
