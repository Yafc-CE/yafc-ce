namespace Yafc.Model;

/// <summary>
/// Chooses the module for buildings that accept only one module category, such as Pyanodon's farm buildings, which are designed to run
/// with their own modules.
/// </summary>
public static class DefaultModules {
    /// <summary>
    /// Finds the module that fills a building that accepts only one module category. Only modules that are accessible with the current
    /// milestones are considered, so players decide which modules count by adding them as milestones.
    /// No secondary sort on prod/speed to keep the code simpler.
    /// </summary>
    /// <returns>The accepted module that unlocks latest according to the milestones, or <see langword="null"/> if the building does not
    /// accept exactly one module category or no module qualifies.</returns>
    public static Module? GetSingleCategoryModule(EntityCrafter crafter, RecipeOrTechnology recipe) {
        if (crafter.moduleSlots <= 0 || crafter.allowedModuleCategories is not [string moduleCategory]) {
            return null;
        }

        Module? bestModule = null;
        Bits bestUnlockOrder = default;

        //Loop over all modules finding the one that is usable, unlocked, and unlocks latest according to milestones
        foreach (Module module in Database.allModules) {
            //Check module is in the building's allowed category
            if (module.moduleSpecification.category != moduleCategory) {
                continue;
            }

            //Check the recipe allows this module
            if (!crafter.CanAcceptModule(module.moduleSpecification) || !recipe.CanAcceptModule(module)) {
                continue;
            }

            //Check if this module is currently available with the active milestones
            if (!module.IsAccessibleWithCurrentMilestones()) {
                continue;
            }

            //TURD modules use the farm module category, but must never fill the module slots
            if (PyanodonTurd.IsTurdModule(module)) {
                continue;
            }

            //Update if this module is better (milestone order)
            Bits moduleMilestoneOrder = Milestones.Instance.GetMilestoneResult(module.id);
            if (bestModule == null || moduleMilestoneOrder > bestUnlockOrder) {
                bestModule = module;
                bestUnlockOrder = moduleMilestoneOrder;
            }
        }

        return bestModule;
    }
}
