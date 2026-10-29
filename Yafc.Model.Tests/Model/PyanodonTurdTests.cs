using System.Linq;
using Xunit;

namespace Yafc.Model.Tests.Model;

[Collection("LuaDependentTests")]
public class PyanodonTurdTests {
    private static Module GetModule(string name) => Database.allModules.Single(m => m.name == name);
    private static EntityCrafter GetCrafter(string name) => (EntityCrafter)Database.objectsByTypeName["Entity." + name];
    private static Recipe GetRecipe(string name) => Database.recipes.all.Single(r => r.name == name);

    [Fact]
    public void IsTurdModule_RecognisesTurdModulesByName_WhetherCraftableOrNot() {
        _ = LuaDependentTestHelper.GetProjectForLua();

        // Craftable, as pypostprocessing intends once pybugreports#1632 is fixed.
        Assert.True(GetModule("carbon-capture-module-mk01").IsAutomatable());
        Assert.True(PyanodonTurd.IsTurdModule(GetModule("carbon-capture-module-mk01")));
        // Uncraftable, as today.
        Assert.False(GetModule("carbon-capture-module-mk02").IsAutomatable());
        Assert.True(PyanodonTurd.IsTurdModule(GetModule("carbon-capture-module-mk02")));
        Assert.True(PyanodonTurd.IsTurdModule(GetModule("fish-upgrade-module")));

        Assert.False(PyanodonTurd.IsTurdModule(GetModule("moondrop-mk01")));
        // Named like a TURD module, but there is no turd-select-lonely technology.
        Assert.False(PyanodonTurd.IsTurdModule(GetModule("lonely-module")));
    }

    [Fact]
    public void GetSingleCategoryModule_UsesTheFarmModule_AndNeverATurdModule() {
        _ = LuaDependentTestHelper.GetProjectForLua();

        Assert.True(GetModule("carbon-capture-module-mk01").IsAccessibleWithCurrentMilestones());
        Assert.Equal(GetModule("moondrop-mk01"), DefaultModules.GetSingleCategoryModule(GetCrafter("moondrop-greenhouse-mk01"), GetRecipe("moondrop")));

        // The only accessible module in the fish category is a TURD module.
        Assert.True(GetModule("fish-upgrade-module").IsAccessibleWithCurrentMilestones());
        Assert.Null(DefaultModules.GetSingleCategoryModule(GetCrafter("fish-farm-mk01"), GetRecipe("fish")));
    }
}
