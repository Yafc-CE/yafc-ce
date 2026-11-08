using Xunit;

namespace Yafc.Model.Tests;

[Collection("LuaDependentTests")]
public class CostAnalysisCrafterTests {
    private static Recipe GetRecipe(string name) => (Recipe)Database.objectsByTypeName["Recipe." + name];
    private static Goods GetItem(string name) => (Goods)Database.objectsByTypeName["Item." + name];

    // Each test recipe takes 1 second, has no ingredients and makes one item: the size cost of a 3x3 crafter plus the item cost.
    private const float SmallCrafterCost = (0.1f * 3 * 1.2f) + 0.02f;

    [Fact]
    public void CostAnalysis_CountsEnergyAbove1000MJ() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");
        var costs = CostAnalysis.Instance.cost;
        float woodCost = costs[GetItem("wood")];

        // These furnaces burn only wood (2 MJ), so 500 MJ and 2000 MJ take 250 and 1000 wood.
        Assert.Equal(SmallCrafterCost + (250 * woodCost), costs[GetItem("modest")], 1e-1f);
        Assert.Equal(SmallCrafterCost + (1000 * woodCost), costs[GetItem("hungry")], 1e-1f);
    }

    [Fact]
    public void CostAnalysis_SingleFuelCrafter_PaysForTheFuelOnly() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");
        var costs = CostAnalysis.Instance.recipeCost;

        // The wood that the furnace burns is added to the recipe, so the recipe itself has no energy cost.
        Assert.Equal(SmallCrafterCost, costs[GetRecipe("modest")], 1e-3f);
        // The flexible furnace can burn wood or peat, so its 500 MJ per craft are charged as energy instead.
        Assert.Equal(SmallCrafterCost + (0.1f * 500), costs[GetRecipe("flexible")], 1e-3f);
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
        // The furnace burns only wood, so the recipe has no energy cost, only the pollution.
        Assert.Equal(SmallCrafterCost + (5 * 0.01f), costs[GetRecipe("dirty")], 1e-3f);
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
        var costs = CostAnalysis.Instance.cost;

        // The press is 5 wide and burns only wood (2 MJ), so its 500 MJ per craft take 250 wood. The character uses no energy, but doesn't count.
        Assert.Equal((0.1f * 5 * 1.2f) + 0.02f + (250 * costs[GetItem("wood")]), costs[GetItem("pressed")], 1e-1f);
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

    [Fact]
    public void CostAnalysis_UsesTheFastestBuilding_WithItsSpeedAndSize() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");

        // The fast assembler is 5 wide and twice as fast, so the recipe takes half a second. The slow one is smaller, but isn't used.
        Assert.Equal((0.1f * 0.5f * 5 * 1.2f) + 0.02f, CostAnalysis.Instance.recipeCost[GetRecipe("speedy")], 1e-3f);
    }

    [Fact]
    public void CostAnalysis_UsesTheSlowestBuilding_WhenNoneIsUnlocked() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");

        // Both assemblers need the pack. The slow one is the first that a player unlocks, so its size of 5 is used at speed 1.
        Assert.Equal((0.1f * 5 * 1.2f) + 0.02f, CostAnalysis.Instance.recipeCost[GetRecipe("locked-speeds")], 1e-3f);
    }

    [Fact]
    public void CostAnalysis_BuildingWithNegativeSpeed_UsesTheSlowestModule_WhenNoneIsUnlocked() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");

        // The farm has -100% speed. Its +100% module brings it to speed 1, instead of the 20% minimum, in both costs.
        Assert.Equal(SmallCrafterCost, CostAnalysis.Instance.recipeCost[GetRecipe("farmed")], 1e-3f);
        Assert.Equal(SmallCrafterCost, CostAnalysis.InstanceAtMilestones.recipeCost[GetRecipe("farmed")], 1e-3f);
    }

    [Fact]
    public void CostAnalysis_BuildingWithSeveralModuleCategories_UsesAnUnlockedModule() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");

        // The +300% module is the only unlocked one, so it's used instead of the slower farm modules. The complex runs at speed 3.
        Assert.Equal((0.1f * (1f / 3f) * 3 * 1.2f) + 0.02f, CostAnalysis.Instance.recipeCost[GetRecipe("complexed")], 1e-3f);
    }

    [Fact]
    public void CostAnalysis_Mining_KeepsSpeed1_AndTheSmallestDrill() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");
        var mining = (Recipe)Database.objectsByTypeName["Mechanics.mining.basic-solid.stone"];

        // The size of the 2-wide drill, at speed 1. The 5-wide drill is faster, but mining doesn't use its speed or its size.
        Assert.Equal((0.1f * 2 * 1.2f) + 0.02f, CostAnalysis.Instance.recipeCost[mining], 1e-3f);
    }

    [Fact]
    public void CostAnalysis_Pumping_UsesTheFastestPump_WithItsSpeedAndSize() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");
        var pumping = (Recipe)Database.objectsByTypeName["Mechanics.pump.water.water"];

        // The big pump is 3 wide and twice as fast, so pumping 1200 water takes half a second. The small pump is 1 wide, but isn't used.
        Assert.Equal((0.1f * 0.5f * 3 * 1.2f) + (1200 * 0.0005f), CostAnalysis.Instance.recipeCost[pumping], 1e-3f);
    }

    [Fact]
    public void CostAnalysis_AgriculturalTower_UsesItsPlots_AndTheWidthOfItsField() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");
        var planting = (Recipe)Database.objectsByTypeName["Mechanics.plant.seed"];

        // 48 trees grow at once, so each one takes 80 / 48 seconds. The 7 x 7 grid is 21 tiles wide. The recipe has one ingredient and
        // one product, which are both items.
        Assert.Equal((0.1f * (80f / 48f) * 21 * 1.3f) + (2 * 0.02f), CostAnalysis.Instance.recipeCost[planting], 1e-3f);
    }
}
