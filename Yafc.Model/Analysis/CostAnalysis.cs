using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Google.OrTools.LinearSolver;
using Serilog;
using Yafc.Core;
using Yafc.I18n;

namespace Yafc.Model;

public class CostAnalysis(bool onlyCurrentMilestones) : Analysis {
    private readonly ILogger logger = Logging.GetLogger<CostAnalysis>();

    public static readonly CostAnalysis Instance = new CostAnalysis(false);
    public static readonly CostAnalysis InstanceAtMilestones = new CostAnalysis(true);
    public static CostAnalysis Get(bool atCurrentMilestones) => atCurrentMilestones ? InstanceAtMilestones : Instance;

    private const float CostPerSecond = 0.1f;
    private const float CostPerMj = 0.1f;
    private const float CostPerIngredientPerSize = 0.1f;
    private const float CostPerProductPerSize = 0.2f;
    private const float CostPerItem = 0.02f;
    private const float CostPerFluid = 0.0005f;
    private const float CostPerPollution = 0.01f;
    private const float CostLowerLimit = -10f;
    private const float CostLimitWhenGeneratesOnMap = 1e4f;
    private const float MiningPenalty = 1f; // Penalty for any mining
    private const float MiningMaxDensityForPenalty = 30f; // Mining things with less density (resources per tile on a default map; vanilla iron ore is 10) than this gets extra penalty
    private const float MiningMaxExtraPenaltyForRarity = 10f;

    public Mapping<FactorioObject, float> cost;
    public Mapping<Recipe, float> recipeCost;
    public Mapping<RecipeOrTechnology, float> recipeProductCost;
    public Mapping<FactorioObject, float> flow;
    public Mapping<Recipe, float> recipeWastePercentage;
    public Goods[]? importantItems;
    private readonly bool onlyCurrentMilestones = onlyCurrentMilestones;
    private readonly Dictionary<EntityCrafter, (float speed, float consumption)?> turdBonusCache = [];
    private string? itemAmountPrefix;

    private bool ShouldInclude(FactorioObject obj) => onlyCurrentMilestones ? obj.IsAutomatableWithCurrentMilestones() : obj.IsAutomatable();

    public override void Compute(Project project, ErrorCollector warnings) {
        turdBonusCache.Clear();
        var workspaceSolver = DataUtils.CreateSolver();
        var objective = workspaceSolver.Objective();
        objective.SetMaximization();
        Stopwatch time = Stopwatch.StartNew();

        // Find the best accessible container for spoilage cost calculation
        float bestContainerSlotsPerTile = 1f;
        foreach (var container in Database.allContainers) {
            float area = container.width * container.height;
            if (ShouldInclude(container) && area > 0) {
                float slotsPerTile = container.inventorySize / area;
                if (slotsPerTile > bestContainerSlotsPerTile) {
                    bestContainerSlotsPerTile = slotsPerTile;
                }
            }
        }

        var variables = Database.goods.CreateMapping<Variable>();
        var constraints = Database.recipes.CreateMapping<Constraint>();

        Dictionary<Goods, float> sciencePackUsage = [];
        if (!onlyCurrentMilestones && project.preferences.targetTechnology != null) {
            itemAmountPrefix = LSs.CostAnalysisEstimatedAmountFor.L(project.preferences.targetTechnology.locName);

            foreach (var spUsage in TechnologyScienceAnalysis.Instance.allSciencePacks[project.preferences.targetTechnology]) {
                sciencePackUsage[spUsage.goods] = spUsage.amount;
            }
        }
        else {
            itemAmountPrefix = LSs.CostAnalysisEstimatedAmount;

            foreach (Technology technology in Database.technologies.all.ExceptExcluded(this)) {
                if (technology.IsAccessible() && technology.ingredients is not null) {
                    foreach (var ingredient in technology.ingredients) {
                        if (ingredient.goods.IsAutomatable()) {
                            if (onlyCurrentMilestones && !Milestones.Instance.IsAccessibleAtNextMilestone(ingredient.goods)) {
                                continue;
                            }

                            _ = sciencePackUsage.TryGetValue(ingredient.goods, out float prev);
                            sciencePackUsage[ingredient.goods] = prev + (ingredient.amount * technology.count);
                        }
                    }
                }
            }
        }

        foreach (Goods goods in Database.goods.all.ExceptExcluded(this)) {
            if (!ShouldInclude(goods)) {
                continue;
            }

            float mapGeneratedAmount = 0f;

            foreach (var src in goods.miscSources) {
                if (src is Entity ent && ent.mapGenerated) {
                    foreach (var product in ent.loot) {
                        if (product.goods == goods) {
                            mapGeneratedAmount += product.amount;
                        }
                    }
                }
            }

            var variable = workspaceSolver.MakeVar(CostLowerLimit, CostLimitWhenGeneratesOnMap / mapGeneratedAmount, false, goods.name);
            objective.SetCoefficient(variable, 1e-3); // adding small amount to each object cost, so even objects that aren't required for science will get cost calculated
            variables[goods] = variable;
        }

        foreach (var (item, count) in sciencePackUsage) {
            objective.SetCoefficient(variables[item], count / 1000f);
        }

        var export = Database.objects.CreateMapping<float>();
        var recipeProductionCost = Database.recipesAndTechnologies.CreateMapping<float>();
        recipeCost = Database.recipes.CreateMapping<float>();
        flow = Database.objects.CreateMapping<float>();
        var lastVariable = Database.goods.CreateMapping<Variable>();

        foreach (Recipe recipe in Database.recipes.all.ExceptExcluded(this)) {
            if (!ShouldInclude(recipe)) {
                continue;
            }

            if (onlyCurrentMilestones && !recipe.IsAccessibleWithCurrentMilestones()) {
                continue;
            }

            // TODO incorporate fuel selection. Now just select fuel if it only uses 1 fuel
            Goods? singleUsedFuel = null;
            float singleUsedFuelAmount = 0f;
            float emissions = float.PositiveInfinity;
            int buildingSize = int.MaxValue;
            float energyPerCraft = float.PositiveInfinity;
            float craftingTime = recipe.time;
            EntityCrafter[] crafters = GetCraftersForCost(recipe, out bool noneUnlocked);

            if (recipe is Mechanics) {
                // Special recipes, such as mining, pumping and generating, assume speed 1. So their size, energy and pollution are each
                // the lowest among their buildings, chosen separately. Taking them from the fastest building would charge for its size
                // without the benefit of its speed. Mining keeps speed 1 on purpose, since its density penalty was calibrated at speed 1.
                // The other special recipes are planned to use real speed, see #751.
                bool fuelsDiffer = false;

                foreach (var crafter in crafters) {
                    foreach ((_, float e) in crafter.energy.emissions) {
                        emissions = MathF.Min(e, emissions);
                    }

                    if (crafter.size < buildingSize) {
                        buildingSize = crafter.size;
                    }

                    float energy = GetSpeedAndEnergy(recipe, crafter).energyPerCraft;

                    if (energy < energyPerCraft) {
                        energyPerCraft = energy;
                    }

                    // The fuel is only added to the recipe if every crafter burns the same single fuel.
                    if (!fuelsDiffer) {
                        Goods[] fuels = [.. crafter.energy.fuels.Where(ShouldInclude)];

                        if (fuels is [Goods fuel] && fuel.fuelValue > 0f && (singleUsedFuel == null || singleUsedFuel == fuel)) {
                            float amount = energy / fuel.fuelValue;
                            singleUsedFuelAmount = singleUsedFuel == null ? amount : MathF.Min(singleUsedFuelAmount, amount);
                            singleUsedFuel = fuel;
                        }
                        else {
                            fuelsDiffer = true;
                        }
                    }
                }

                if (fuelsDiffer) {
                    singleUsedFuel = null;
                }
            }
            else if (ChooseCrafter(recipe, crafters, noneUnlocked) is (EntityCrafter crafter, float speed, float energy)) {
                // Other recipes are priced with one building, using its speed, size, energy and pollution together.
                buildingSize = crafter.size;
                energyPerCraft = energy;

                if (speed > 0f) {
                    craftingTime = recipe.time / speed;
                }

                foreach ((_, float e) in crafter.energy.emissions) {
                    emissions = MathF.Min(e, emissions);
                }

                Goods[] fuels = [.. crafter.energy.fuels.Where(ShouldInclude)];

                if (fuels is [Goods fuel] && fuel.fuelValue > 0f) {
                    singleUsedFuel = fuel;
                    singleUsedFuelAmount = energy / fuel.fuelValue;
                }
            }

            // A recipe without crafters is charged a fixed size and energy.
            if (buildingSize == int.MaxValue) {
                buildingSize = 15;
            }

            if (float.IsPositiveInfinity(energyPerCraft)) {
                energyPerCraft = 1000f;
            }

            if (energyPerCraft < 0f) {
                energyPerCraft = 0f;
            }

            // Crafters without emissions don't pollute.
            if (float.IsPositiveInfinity(emissions)) {
                emissions = 0f;
            }

            if (singleUsedFuel?.isPower == true) {
                singleUsedFuel = null;
            }

            int size = Math.Max(buildingSize, (recipe.ingredients.Length + recipe.products.Length) / 2);
            float sizeUsage = CostPerSecond * craftingTime * size;
            // When the buildings burn a single fuel, that fuel is added to the recipe below and already pays for the energy.
            float energyCost = singleUsedFuel == null ? CostPerMj * energyPerCraft : 0f;
            float logisticsCost = (sizeUsage * (1f + (CostPerIngredientPerSize * recipe.ingredients.Length) + (CostPerProductPerSize * recipe.products.Length))) + energyCost;

            // Special handling for spoilage recipes: cost depends on container efficiency and stack size
            // Spoilage happens in storage containers where many stacks spoil in parallel
            if (recipe is Mechanics && recipe.name.StartsWith("spoil.") && recipe.ingredients.Length == 1 && recipe.ingredients[0].goods is Item spoilingItem) {
                int stackSize = spoilingItem.stackSize;
                // Cost is based on storage space needed: time / (stackSize * slotsPerTile)
                // This reflects that larger stacks and better containers reduce infrastructure cost
                logisticsCost = CostPerSecond * recipe.time / (stackSize * bestContainerSlotsPerTile);
            }

            var constraint = workspaceSolver.MakeConstraint(double.NegativeInfinity, 0, recipe.name);
            constraints[recipe] = constraint;

            foreach (var product in recipe.products) {
                var var = variables[product.goods];
                float amount = product.amount;
                constraint.SetCoefficientCheck(var, amount, ref lastVariable[product.goods]);

                if (product.goods is Item) {
                    logisticsCost += amount * CostPerItem;
                }
                else if (product.goods is Fluid) {
                    logisticsCost += amount * CostPerFluid;
                }
            }

            if (singleUsedFuel != null) {
                var var = variables[singleUsedFuel];
                constraint.SetCoefficientCheck(var, -singleUsedFuelAmount, ref lastVariable[singleUsedFuel]);
            }

            foreach (var ingredient in recipe.ingredients) {
                var var = variables[ingredient.goods]; // TODO split cost analysis
                constraint.SetCoefficientCheck(var, -ingredient.amount, ref lastVariable[ingredient.goods]);

                if (ingredient.goods is Item) {
                    logisticsCost += ingredient.amount * CostPerItem;
                }
                else if (ingredient.goods is Fluid) {
                    logisticsCost += ingredient.amount * CostPerFluid;
                }
            }

            if (recipe.sourceEntity != null && recipe.sourceEntity.mapGenerated) {
                float totalMining = 0f;

                foreach (var product in recipe.products) {
                    totalMining += product.amount;
                }

                float miningPenalty = MiningPenalty;
                float totalDensity = recipe.sourceEntity.mapGenDensity / totalMining;

                // A density of 0 means the density could not be estimated. Don't treat such resources as the rarest possible.
                if (totalDensity > 0 && totalDensity < MiningMaxDensityForPenalty) {
                    float extraPenalty = MathF.Log(MiningMaxDensityForPenalty / totalDensity);
                    miningPenalty += Math.Min(extraPenalty, MiningMaxExtraPenaltyForRarity);
                }

                logisticsCost *= miningPenalty;
            }

            if (emissions >= 0f) {
                logisticsCost += emissions * CostPerPollution * craftingTime * project.settings.PollutionCostModifier;
            }

            constraint.SetUb(logisticsCost);
            export[recipe] = logisticsCost;
            recipeCost[recipe] = logisticsCost;
        }

        // TODO this is temporary fix for strange item sources (make the cost of item not higher than the cost of its source)
        foreach (Item item in Database.items.all.ExceptExcluded(this)) {
            if (ShouldInclude(item)) {
                foreach (var source in item.miscSources) {
                    if (source is Goods g && ShouldInclude(g)) {
                        var constraint = workspaceSolver.MakeConstraint(double.NegativeInfinity, 0, "source-" + item.locName);
                        constraint.SetCoefficient(variables[g], -1);
                        constraint.SetCoefficient(variables[item], 1);
                    }
                }
            }
        }

        // TODO this is temporary fix for fluid temperatures (make the cost of fluid with lower temp not higher than the cost of fluid with higher temp)
        foreach (var (name, fluids) in Database.fluidVariants) {
            var prev = fluids[0];

            for (int i = 1; i < fluids.Count; i++) {
                var cur = fluids[i];
                var constraint = workspaceSolver.MakeConstraint(double.NegativeInfinity, 0, "fluid-" + name + "-" + prev.temperature);
                constraint.SetCoefficient(variables[prev], 1);
                constraint.SetCoefficient(variables[cur], -1);
                prev = cur;
            }
        }

        if (Database.heatVariants != null) {
            var prev = Database.heatVariants[0];

            for (int i = 1; i < Database.heatVariants.Count; i++) {
                var cur = Database.heatVariants[i];
                var constraint = workspaceSolver.MakeConstraint(double.NegativeInfinity, 0, "heat-" + prev.temperature);
                constraint.SetCoefficient(variables[prev], 1);
                constraint.SetCoefficient(variables[cur], -1);
                prev = cur;
            }
        }

        var result = workspaceSolver.TrySolveWithDifferentSeeds();
        logger.Information("Cost analysis completed in {ElapsedTime}ms with result {result}", time.ElapsedMilliseconds, result);
        float sumImportance = 1f;
        int totalRecipes = 0;

        if (result is Solver.ResultStatus.OPTIMAL or Solver.ResultStatus.FEASIBLE) {
            float objectiveValue = (float)objective.Value();
            logger.Information("Estimated modpack cost: {EstimatedCost}", DataUtils.FormatAmount(objectiveValue * 1000f, UnitOfMeasure.None));
            foreach (Goods g in Database.goods.all.ExceptExcluded(this)) {
                if (variables[g] == null) {
                    continue;
                }

                float value = (float)variables[g].SolutionValue();
                export[g] = value;
            }

            foreach (Recipe recipe in Database.recipes.all.ExceptExcluded(this)) {
                if (constraints[recipe] == null) {
                    continue;
                }

                float recipeFlow = (float)constraints[recipe].DualValue();

                if (recipeFlow > 0f) {
                    totalRecipes++;
                    sumImportance += recipeFlow;
                    flow[recipe] = recipeFlow;
                    foreach (var product in recipe.products) {
                        flow[product.goods] += recipeFlow * product.amount;
                    }
                }
            }
        }
        foreach (FactorioObject o in Database.objects.all.ExceptExcluded(this)) {
            if (!ShouldInclude(o)) {
                export[o] = float.PositiveInfinity;
                continue;
            }

            if (o is RecipeOrTechnology recipe) {
                foreach (var ingredient in recipe.ingredients) // TODO split
{
                    export[o] += export[ingredient.goods] * ingredient.amount;
                }

                foreach (var product in recipe.products) {
                    recipeProductionCost[recipe] += product.amount * export[product.goods];
                }
            }
            else if (o is Entity entity) {
                float minimal = float.PositiveInfinity;

                foreach (var item in entity.itemsToPlace) {
                    if (export[item] < minimal) {
                        minimal = export[item];
                    }
                }
                export[o] = minimal;
            }
        }
        cost = export;
        recipeProductCost = recipeProductionCost;

        recipeWastePercentage = Database.recipes.CreateMapping<float>();
        if (result is Solver.ResultStatus.OPTIMAL or Solver.ResultStatus.FEASIBLE) {
            foreach (var (recipe, constraint) in constraints) {
                if (constraint == null) {
                    continue;
                }

                float productCost = 0f;

                foreach (var product in recipe.products) {
                    productCost += product.amount * export[product.goods];
                }

                recipeWastePercentage[recipe] = 1f - (productCost / export[recipe]);
            }
        }
        else {
            if (!onlyCurrentMilestones) {
                warnings.Error(LSs.CostAnalysisFailed, ErrorSeverity.AnalysisWarning);
            }
        }

        importantItems = [.. Database.goods.all.ExceptExcluded(this).Where(x => x.usages.Length > 1)
            .OrderByDescending(x => flow[x] * cost[x] * x.usages.Count(y => ShouldInclude(y) && recipeWastePercentage[y] == 0f))];

        workspaceSolver.Dispose();
    }

    /// <summary>
    /// Returns the crafters that can set the cost of a recipe. These are the buildings that are unlocked at the current milestones, in
    /// both costs, the same as the modules that the cost analysis uses. If none of them is, all buildings are used, and
    /// <paramref name="noneUnlocked"/> is <see langword="true"/>. The character is only used if nothing else can make the recipe, since it
    /// uses no energy and would make the recipe look free. Buildings that are only placed with another building's item are skipped too,
    /// see <see cref="IsPlacedWithOtherBuildingsItem"/>.
    /// </summary>
    private static EntityCrafter[] GetCraftersForCost(Recipe recipe, out bool noneUnlocked) {
        EntityCrafter[] crafters = [.. recipe.crafters.Where(c => c != Database.character && !IsPlacedWithOtherBuildingsItem(c)
            && c.IsAccessibleWithCurrentMilestones())];
        noneUnlocked = crafters.Length == 0;

        if (crafters.Length == 0) {
            crafters = [.. recipe.crafters.Where(c => c != Database.character)];
        }

        if (crafters.Length == 0) {
            crafters = recipe.crafters;
        }

        return crafters;
    }

    /// <summary>
    /// Some of Pyanodon's TURD upgrades replace a building with a variant, which is placed with the original building's item. So YAFC
    /// considers the variant accessible as soon as the original is, whichever TURD is selected. Pyanodon doesn't pass these
    /// replacements on to YAFC, so YAFC can't tell which variant goes with the selection, and the variants are not used.
    /// </summary>
    private static bool IsPlacedWithOtherBuildingsItem(EntityCrafter crafter)
        => crafter.itemsToPlace.Length > 0 && !crafter.itemsToPlace.Any(i => i.placeResult == crafter);

    /// <summary>
    /// Chooses the building that prices a recipe, which is the fastest of <paramref name="crafters"/>. If speeds tie, the lowest energy
    /// wins, then the smallest size. If none of the buildings is unlocked, the slowest one is used instead, since that's the first one a
    /// player unlocks. Buildings with a crafting speed of 0 are only used if no other building can make the recipe.
    /// </summary>
    /// <returns>The building with its speed and energy per craft, or <see langword="null"/> if there are no crafters.</returns>
    private (EntityCrafter crafter, float speed, float energyPerCraft)? ChooseCrafter(Recipe recipe, EntityCrafter[] crafters, bool noneUnlocked) {
        if (crafters.Any(c => c.baseCraftingSpeed > 0f)) {
            crafters = [.. crafters.Where(c => c.baseCraftingSpeed > 0f)];
        }

        (EntityCrafter crafter, float speed, float energyPerCraft)? best = null;

        foreach (EntityCrafter crafter in crafters) {
            (float speed, float energy) = GetSpeedAndEnergy(recipe, crafter);

            if (best is not (EntityCrafter bestCrafter, float bestSpeed, float bestEnergy)) {
                best = (crafter, speed, energy);
                continue;
            }

            // Speeds within 0.01% of each other count as equal, so that rounding doesn't decide between them.
            int faster = speed > bestSpeed * 1.0001f ? 1 : speed < bestSpeed * 0.9999f ? -1 : 0;

            if (noneUnlocked) {
                faster = -faster;
            }

            if (faster > 0 || (faster == 0 && (energy < bestEnergy || (energy == bestEnergy && crafter.size < bestCrafter.size)))) {
                best = (crafter, speed, energy);
            }
        }

        return best;
    }

    /// <summary>
    /// Calculates the crafting speed and the energy per craft of a building for a recipe. Buildings that are designed around modules, such
    /// as Pyanodon's farm buildings, use this module setup:
    /// <list type="bullet">
    /// <item>The default module from <see cref="DefaultModules.GetSingleCategoryModule"/> in every slot, the same module that production
    /// tables fill in. Other buildings are not given modules, so their cost does not assume any.</item>
    /// <item>Buildings with negative built-in speed that accept several module categories, such as Pyanodon's Reproductive Complex, have no
    /// default module, because the right module depends on the recipe. They use the slowest module that the recipe accepts and that is
    /// unlocked at the current milestones.</item>
    /// <item>If none of its modules is unlocked yet, a building with negative built-in speed uses the slowest module that it and the recipe
    /// accept, among the modules that are accessible at all, instead of running at the minimum speed. TURD modules are never used for this, since they're only meant for buildings
    /// whose TURD the player selected.</item>
    /// <item>The TURD upgrade that the player selected for this building, if any (see <see cref="PyanodonTurd.GetSelectedBonus"/>).</item>
    /// </list>
    /// Like in production tables, the building's built-in effect is added as well. Pyanodon's farm buildings are built with -100% speed,
    /// so that they only work with their modules. A building with negative built-in speed, or one that accepts only one module category,
    /// gets its built-in effect even when none of its modules is available, so that it never looks cheaper without its module than with it.
    /// </summary>
    private (float speed, float energyPerCraft) GetSpeedAndEnergy(Recipe recipe, EntityCrafter crafter) {
        float speed = crafter.baseCraftingSpeed;
        float power = crafter.basePower;

        if (crafter.moduleSlots > 0) {
            if (!turdBonusCache.TryGetValue(crafter, out var turd)) {
                turd = PyanodonTurd.GetSelectedBonus(crafter);
                turdBonusCache[crafter] = turd;
            }

            Module? module = DefaultModules.GetSingleCategoryModule(crafter, recipe);
            Effect builtInEffect = crafter.effectReceiver.baseEffect;
            bool needsRecipeModule = builtInEffect.speed < 0f && crafter.allowedModuleCategories is not [_];

            if (needsRecipeModule || (module is null && builtInEffect.speed < 0f)) {
                Module[] usable = [.. Database.allModules.Where(m => crafter.CanAcceptModule(m.moduleSpecification) && recipe.CanAcceptModule(m)
                    && m.IsAccessible() && !PyanodonTurd.IsTurdModule(m) && m.moduleSpecification.Speed(Quality.Normal) > 0f)];

                if (needsRecipeModule) {
                    module = usable.Where(m => m.IsAccessibleWithCurrentMilestones()).MinBy(m => m.moduleSpecification.Speed(Quality.Normal));
                }

                module ??= usable.MinBy(m => m.moduleSpecification.Speed(Quality.Normal));
            }

            if (module is not null || turd is not null || needsRecipeModule || crafter.allowedModuleCategories is [_]) {
                float speedBonus = builtInEffect.speed + (crafter.moduleSlots * (module?.moduleSpecification.Speed(Quality.Normal) ?? 0f)) + (turd?.speed ?? 0f);
                float consumptionBonus = builtInEffect.consumption + (crafter.moduleSlots * (module?.moduleSpecification.Consumption(Quality.Normal) ?? 0f)) + (turd?.consumption ?? 0f);

                // Factorio limits the combined speed and consumption bonuses to -80%.
                speed = crafter.baseCraftingSpeed * MathF.Max(0.2f, 1f + speedBonus);
                power = crafter.basePower * MathF.Max(0.2f, 1f + consumptionBonus);
            }
        }

        if (crafter.energy.type == EntityEnergyType.Void) {
            return (speed, 0f);
        }

        // A building that can't craft is never the one with the lowest energy.
        float energyPerCraft = speed > 0f ? recipe.time * power / (speed * crafter.energy.effectivity) : float.PositiveInfinity;

        return (speed, energyPerCraft);
    }

    public static string GetDisplayCost(FactorioObject goods) {
        float cost = goods.Cost();
        float costNow = goods.Cost(true);
        if (float.IsPositiveInfinity(cost)) {
            return LSs.AnalysisNotAutomatable;
        }

        float compareCost = cost;
        float compareCostNow = costNow;
        string finalCost;

        if (goods is Fluid) {
            compareCost = cost * 50;
            compareCostNow = costNow * 50;
            finalCost = LSs.CostAnalysisFluidCost.L(DataUtils.FormatAmount(compareCost, UnitOfMeasure.None));
        }
        else if (goods is Item) {
            finalCost = LSs.CostAnalysisItemCost.L(DataUtils.FormatAmount(compareCost, UnitOfMeasure.None));
        }
        else if (goods is Special special && special.isPower) {
            finalCost = LSs.CostAnalysisEnergyCost.L(DataUtils.FormatAmount(compareCost, UnitOfMeasure.None));
        }
        else if (goods is Recipe) {
            finalCost = LSs.CostAnalysisRecipeCost.L(DataUtils.FormatAmount(compareCost, UnitOfMeasure.None));
        }
        else {
            finalCost = LSs.CostAnalysisGenericCost.L(DataUtils.FormatAmount(compareCost, UnitOfMeasure.None));
        }

        if (compareCostNow > compareCost && !float.IsPositiveInfinity(compareCostNow)) {
            return LSs.CostAnalysisWithCurrentCost.L(finalCost, DataUtils.FormatAmount(compareCostNow, UnitOfMeasure.None));
        }

        return finalCost;
    }

    public static float GetBuildingHours(Recipe recipe, float flow) => recipe.time * flow * (1000f / 3600f);

    public string? GetItemAmount(Goods goods) {
        float itemFlow = flow[goods];
        if (itemFlow <= 1f) {
            return null;
        }

        return itemAmountPrefix + DataUtils.FormatAmount(itemFlow * 1000f, UnitOfMeasure.None);
    }
}
