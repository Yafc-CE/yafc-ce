using Xunit;

namespace Yafc.Model.Tests;

[Collection("LuaDependentTests")]
public class CostAnalysisCrafterTests {
    private static Recipe GetRecipe(string name) => (Recipe)Database.objectsByTypeName["Recipe." + name];

    // Each test recipe takes 1 second, has no ingredients and makes one item: the size cost of a 3x3 crafter plus the item cost.
    private const float SmallCrafterCost = (0.1f * 3 * 1.2f) + 0.02f;

    [Fact]
    public void CostAnalysis_CountsEnergyAbove1000MJ() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");
        var costs = CostAnalysis.Instance.recipeCost;

        Assert.Equal(SmallCrafterCost + (0.1f * 500), costs[GetRecipe("modest")], 1e-3f);
        Assert.Equal(SmallCrafterCost + (0.1f * 2000), costs[GetRecipe("hungry")], 1e-3f);
    }

    [Fact]
    public void CostAnalysis_CountsCraftersBiggerThan15Tiles() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");

        Assert.Equal((0.1f * 20 * 1.2f) + 0.02f, CostAnalysis.Instance.recipeCost[GetRecipe("huge")], 1e-3f);
    }

    [Fact]
    public void CostAnalysis_CraftersWithoutEmissions_HaveNoPollutionCost() {
        Project project = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");
        project.settings.PollutionCostModifier = 1;
        Analysis.Do<CostAnalysis>(project);
        var costs = CostAnalysis.Instance.recipeCost;

        Assert.Equal(SmallCrafterCost, costs[GetRecipe("clean")], 1e-3f);
        Assert.Equal(SmallCrafterCost + (0.1f * 1) + (5 * 0.01f), costs[GetRecipe("dirty")], 1e-3f);
    }

    [Fact]
    public void CostAnalysis_HeatPoweredCrafter_UsesItsOwnEnergyAndSize() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");

        // The crafter is 3 wide and uses 1 kW, which is 0.001 MJ per craft.
        Assert.Equal(SmallCrafterCost + (0.1f * 0.001f), CostAnalysis.Instance.recipeCost[GetRecipe("heated")], 1e-3f);
    }

    [Fact]
    public void CostAnalysis_CraftersWithDifferentFuels_DontHideLaterCrafters() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");

        // The small assembler comes after two furnaces with different fuels, and its size and energy are the lowest.
        Assert.Equal(SmallCrafterCost + (0.1f * 0.001f), CostAnalysis.Instance.recipeCost[GetRecipe("mixed")], 1e-3f);
    }

    [Fact]
    public void CostAnalysis_IgnoresTheCharacter_WhenABuildingCanMakeTheRecipe() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");

        // The press is 5 wide and uses 500 MJ per craft. The character uses no energy, but doesn't count.
        Assert.Equal((0.1f * 5 * 1.2f) + (0.1f * 500) + 0.02f, CostAnalysis.Instance.recipeCost[GetRecipe("pressed")], 1e-3f);
    }

    [Fact]
    public void CostAnalysis_IgnoresLockedBuildings() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");

        // The locked assembler is 1 wide, the unlocked one 3 wide.
        Assert.Equal(SmallCrafterCost + (0.1f * 0.001f), CostAnalysis.Instance.recipeCost[GetRecipe("locked-or-not")], 1e-3f);
    }

    [Fact]
    public void CostAnalysis_IgnoresBuildingsBehindALockedMilestone_InBothCosts() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");
        var milestoneAssembler = (EntityCrafter)Database.objectsByTypeName["Entity.a-milestone-assembler"];

        Assert.True(milestoneAssembler.IsAccessible());
        Assert.False(milestoneAssembler.IsAccessibleWithCurrentMilestones());

        // The milestone assembler is 1 wide, the basic one 3 wide. The all-milestones cost uses the basic one too.
        Assert.Equal(SmallCrafterCost + (0.1f * 0.001f), CostAnalysis.Instance.recipeCost[GetRecipe("milestone-or-not")], 1e-3f);
        Assert.Equal(SmallCrafterCost + (0.1f * 0.001f), CostAnalysis.InstanceAtMilestones.recipeCost[GetRecipe("milestone-or-not")], 1e-3f);
    }

    [Fact]
    public void CostAnalysis_IgnoresBuildingsPlacedWithAnotherBuildingsItem() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");

        // The variant is 1 wide, the base assembler 3 wide.
        Assert.Equal(SmallCrafterCost + (0.1f * 0.001f), CostAnalysis.Instance.recipeCost[GetRecipe("varied")], 1e-3f);
    }
}
