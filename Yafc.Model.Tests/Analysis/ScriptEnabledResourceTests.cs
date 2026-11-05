using Xunit;

namespace Yafc.Model.Tests;

[Collection("LuaDependentTests")]
public class ScriptEnabledResourceTests {
    private const string LuaFile = "Yafc.Model.Tests.Analysis.ScriptEnabledResourceTests.lua";

    private static Recipe GetMiningRecipe(string name) => (Recipe)Database.objectsByTypeName["Mechanics.mining.basic-solid." + name];
    private static Item GetItem(string name) => (Item)Database.objectsByTypeName["Item." + name];

    [Fact]
    public void ScriptEnabledResource_WithoutSpawnLocations_CanBeMined() {
        _ = LuaDependentTestHelper.GetProjectForLua(LuaFile);

        Assert.True(GetMiningRecipe("script-ore").IsAccessible());
        Assert.True(GetItem("script-ore").IsAccessible());
        Assert.Null(GetMiningRecipe("script-ore").craftingSurfaces);
    }

    [Fact]
    public void ResourceWithoutSpawnLocations_StaysInaccessible_WhenNothingEnablesIt() {
        _ = LuaDependentTestHelper.GetProjectForLua(LuaFile);

        Assert.False(GetMiningRecipe("unreachable-ore").IsAccessible());
        Assert.False(GetItem("unreachable-ore").IsAccessible());
        Assert.Empty(GetMiningRecipe("unreachable-ore").craftingSurfaces!);
    }

    [Fact]
    public void ResourceWithSpawnLocations_KeepsItsLocationRequirement() {
        _ = LuaDependentTestHelper.GetProjectForLua(LuaFile);

        Assert.True(GetMiningRecipe("nauvis-ore").IsAccessible());
        Location location = Assert.Single(GetMiningRecipe("nauvis-ore").craftingSurfaces!);
        Assert.Equal("nauvis", location.name);
    }
}
