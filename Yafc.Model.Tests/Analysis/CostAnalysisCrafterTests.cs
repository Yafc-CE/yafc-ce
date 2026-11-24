using System;
using System.Linq;
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
        var costs = CostAnalysis.Instance;

        // The wood that the furnace burns is added to the recipe, so the recipe itself has no energy cost.
        Assert.Equal(SmallCrafterCost, costs.recipeCost[GetRecipe("modest")], 1e-3f);
    }

    [Fact]
    public void CostAnalysis_MultiFuelCrafter_PaysForTheCheapestFuel() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");
        var costs = CostAnalysis.Instance;
        float cheapestFuel = MathF.Min(costs.cost[GetItem("wood")], costs.cost[GetItem("peat")]);

        // The flexible furnace can burn wood or peat, which cost more than the flat price. Both have 2 MJ, so the 500 MJ per craft take
        // 250 of whichever is cheaper.
        Assert.Equal(SmallCrafterCost + (250 * cheapestFuel), costs.cost[GetRecipe("flexible")], 1e-1f);
        // Like a single fuel, the cheapest fuel is not part of the recipe's own cost, so the recipe has no energy cost.
        Assert.Equal(SmallCrafterCost, costs.recipeCost[GetRecipe("flexible")], 1e-3f);
    }

    [Fact]
    public void CostAnalysis_MultiFuelDrill_ComparesTheFuelWithTheFlatPriceAndThePenalty() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");
        var mining = (Recipe)Database.objectsByTypeName["Mechanics.mining.choice-solid.choice-ore"];
        RecipeCostBreakdown breakdown = CostAnalysis.Instance.recipeCostBreakdown[mining];
        float cheapestFuel = MathF.Min(CostAnalysis.Instance.cost[GetItem("wood")], CostAnalysis.Instance.cost[GetItem("peat")]) / 2;

        // Wood and peat cost more than the flat price per MJ, but less than the flat price with the penalty for mining rare ore. So the
        // drill keeps the flat price, which costs less with the penalty than the fuel would.
        Assert.InRange(cheapestFuel, 0.1f, 0.1f * breakdown.MiningPenalty);
        Assert.Null(breakdown.Fuel);
        Assert.Equal(0.1f * 2, breakdown.EnergyCost, 1e-3f);
    }

    [Fact]
    public void CostAnalysis_DrillsWithDifferentFuels_PayTheFlatPrice() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");
        var mining = (Recipe)Database.objectsByTypeName["Mechanics.mining.shared-solid.shared-ore"];
        RecipeCostBreakdown breakdown = CostAnalysis.Instance.recipeCostBreakdown[mining];

        // One drill burns peat or nuggets, the other only nuggets. Nuggets are the only fuel that both burn, but they aren't treated as a
        // single fuel, since the first drill has a choice. So the recipe pays the flat price for its 2 MJ per ore, instead of the cheap nuggets.
        Assert.Null(breakdown.Fuel);
        Assert.Equal(0.1f * 2, breakdown.EnergyCost, 1e-3f);
    }

    [Fact]
    public void CostAnalysis_FluidBurnerWithSeveralFuels_PaysTheFlatPrice() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");
        RecipeCostBreakdown breakdown = CostAnalysis.Instance.recipeCostBreakdown[GetRecipe("gassed")];

        // The gas furnace burns light or heavy gas, which both cost more than the flat price per MJ. It still keeps the flat price, since a
        // fluid burner can usually burn any fluid with a fuel value, including byproducts that cost almost nothing.
        Assert.True(CostAnalysis.Instance.cost[(Goods)Database.objectsByTypeName["Fluid.light-gas"]] > 0.1f);
        Assert.Null(breakdown.Fuel);
        Assert.Equal(0.1f * 500, breakdown.EnergyCost, 1e-3f);
    }

    [Fact]
    public void CostAnalysis_MultiFuelCrafter_PaysAtLeastTheFlatPrice() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");
        var costs = CostAnalysis.Instance.cost;

        // Nuggets cost far less than the flat price per MJ, so the frugal furnace, which can also burn peat, pays the flat price instead.
        Assert.Equal(SmallCrafterCost + (0.1f * 500), costs[GetRecipe("frugal")], 1e-1f);
        // A furnace that burns only nuggets still pays for the 5 nuggets it burns.
        Assert.Equal(SmallCrafterCost + (5 * costs[GetItem("nugget")]), costs[GetRecipe("nuggeted")], 1e-1f);
    }

    [Fact]
    public void CostAnalysis_CountsCraftersBiggerThan15Tiles() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");

        Assert.Equal((0.1f * 20 * 1.2f) + 0.02f, CostAnalysis.Instance.recipeCost[GetRecipe("huge")], 1e-3f);
    }

    [Fact]
    public void CostAnalysis_SingleFuelCrafter_RecipeCostIncludesTheFuel() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");
        var costs = CostAnalysis.Instance.cost;

        // The furnace burns only wood (2 MJ), so the 500 MJ per craft take 250 wood, just like an ingredient would.
        Assert.Equal(SmallCrafterCost + (250 * costs[GetItem("wood")]), costs[GetRecipe("modest")], 1e-1f);
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
    public void CostAnalysis_Mining_DoesNotApplyTheRarityPenaltyToTheFuel() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");
        var mining = (Recipe)Database.objectsByTypeName["Mechanics.mining.rare-solid.ore"];
        var costs = CostAnalysis.Instance;

        // The drill burns one wood per ore. The penalty for mining rare ore applies to the rest of the cost, but not to that wood, since
        // the drill burns the same amount however rare the ore is.
        Assert.Equal(costs.cost[GetItem("wood")], costs.cost[mining] - costs.recipeCost[mining], 1e-3f);
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

    [Fact]
    public void CostAnalysis_RecipesInUse_HaveNoWaste() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");

        foreach (var analysis in new[] { CostAnalysis.Instance, CostAnalysis.InstanceAtMilestones }) {
            var usedRecipes = Database.recipes.all.Where(r => analysis.flow[r] > 0f).ToList();

            Assert.NotEmpty(usedRecipes);
            Assert.All(usedRecipes, r => Assert.Equal((r.name, 0f), (r.name, analysis.recipeWastePercentage[r])));
        }
    }

    [Fact]
    public void CostAnalysis_CostBreakdown_SplitsTheRecipeCost() {
        Project project = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Analysis.CostAnalysisCrafterTests.lua");
        project.settings.PollutionCostModifier = 1;
        Analysis.Do<CostAnalysis>(project);
        var breakdowns = CostAnalysis.Instance.recipeCostBreakdown;

        // The furnace burns only wood (2 MJ), so the 500 MJ per craft take 250 wood instead of an energy cost.
        RecipeCostBreakdown modest = breakdowns[GetRecipe("modest")];
        Assert.Equal(GetItem("wood"), modest.Fuel);
        Assert.Equal(250f, modest.FuelAmount, 1e-3f);
        Assert.Equal(0f, modest.EnergyCost);
        Assert.Equal(0.1f * 3 * 1.2f, modest.TimeSizeCost, 1e-3f);
        Assert.Equal(0.02f, modest.ItemTransportCost, 1e-3f);
        Assert.Equal(1f, modest.MiningPenalty);

        // The flexible furnace can burn wood or peat (2 MJ each), which cost more than the flat price, so it burns 250 of the cheaper one.
        RecipeCostBreakdown flexible = breakdowns[GetRecipe("flexible")];
        Goods cheaperFuel = CostAnalysis.Instance.cost[GetItem("wood")] <= CostAnalysis.Instance.cost[GetItem("peat")] ? GetItem("wood") : GetItem("peat");
        Assert.Equal(cheaperFuel, flexible.Fuel);
        Assert.Equal(250f, flexible.FuelAmount, 1e-3f);
        Assert.Equal(0f, flexible.EnergyCost);

        // The frugal furnace can burn peat or nuggets, and nuggets cost less than the flat price, so it pays the flat price as energy.
        RecipeCostBreakdown frugal = breakdowns[GetRecipe("frugal")];
        Assert.Null(frugal.Fuel);
        Assert.Equal(0.1f * 500, frugal.EnergyCost, 1e-3f);

        Assert.Equal(5 * 0.01f, breakdowns[GetRecipe("dirty")].PollutionCost, 1e-3f);

        RecipeCostBreakdown speedy = breakdowns[GetRecipe("speedy")];
        Assert.Equal("b-fast-assembler", speedy.Crafter?.name);
        Assert.Equal(2f, speedy.CrafterSpeed);
        // Mining doesn't use a single building.
        Assert.Null(breakdowns[(Recipe)Database.objectsByTypeName["Mechanics.mining.basic-solid.stone"]].Crafter);

        foreach (Recipe recipe in Database.recipes.all.Where(r => r.IsAutomatable())) {
            Assert.Equal((recipe.name, CostAnalysis.Instance.recipeCost[recipe]), (recipe.name, breakdowns[recipe].TotalCost));
        }
    }
}
