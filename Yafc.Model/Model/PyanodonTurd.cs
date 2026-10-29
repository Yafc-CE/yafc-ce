using System.Linq;
using System.Text.RegularExpressions;

namespace Yafc.Model;

/// <summary>
/// Support for Pyanodon's TURD upgrades. Each TURD choice acts as a hidden module in the buildings it upgrades. pypostprocessing exposes
/// each choice to Yafc as a "turd-select-&lt;subtech&gt;" technology that the player marks as accessible, and the module is named
/// "&lt;subtech&gt;-module", or "&lt;subtech&gt;-module-mk0N" when each building tier gets its own variant.
/// </summary>
/// <remarks>
/// TURD modules are recognised by these names instead of by being uncraftable: pypostprocessing intends to make them craftable for Yafc,
/// but does not yet, because of a naming bug (https://github.com/pyanodon/pybugreports/issues/1632).
/// </remarks>
public static partial class PyanodonTurd {
    /// <summary>
    /// Checks whether <paramref name="module"/> is the hidden module of a TURD choice. TURD modules share their category with the farm
    /// modules of the buildings they upgrade, but have hidden-beacon sized effects, so they must never be put in module slots.
    /// </summary>
    public static bool IsTurdModule(Module module) => GetSelectionTechnology(module) != null;

    /// <summary>
    /// Finds the TURD upgrade that the player selected for <paramref name="crafter"/>. A tiered TURD module only applies to the building
    /// tier with the same -mk0N suffix. A building that accepts the TURD modules of several upgrades (e.g. Pyanodon's Reproductive Complex)
    /// is not the target of any of them.
    /// </summary>
    /// <returns>The speed and consumption bonus of the selected TURD module, or <see langword="null"/> if none is selected.</returns>
    public static (float speed, float consumption)? GetSelectedBonus(EntityCrafter crafter) {
        var turdModules = Database.allModules.Where(m => crafter.CanAcceptModule(m.moduleSpecification) && IsTurdModule(m)).ToList();

        if (turdModules.Select(m => m.moduleSpecification.category).Distinct().Count() != 1) {
            return null;
        }

        // Some tier-1 buildings have no suffix (e.g. guar-gum-plantation), so a missing suffix means mk01.
        Match crafterTier = BuildingTierSuffix().Match(crafter.name);
        string crafterSuffix = crafterTier.Success ? crafterTier.Value : "-mk01";

        foreach (var module in turdModules) {
            Match name = TurdModuleName().Match(module.name);

            if (name.Groups["tier"].Success && name.Groups["tier"].Value != crafterSuffix) {
                continue;
            }

            if (GetSelectionTechnology(module)!.IsAccessible()) {
                return (module.moduleSpecification.baseSpeed, module.moduleSpecification.baseConsumption);
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the "turd-select-&lt;subtech&gt;" technology that belongs to <paramref name="module"/>.
    /// </summary>
    /// <returns>The technology, or <see langword="null"/> if <paramref name="module"/> is not a TURD module.</returns>
    private static FactorioObject? GetSelectionTechnology(Module module) {
        Match name = TurdModuleName().Match(module.name);

        if (name.Success && Database.objectsByTypeName.TryGetValue("Technology.turd-select-" + name.Groups["subtech"].Value, out var selection)) {
            return selection;
        }

        return null;
    }

    [GeneratedRegex(@"^(?<subtech>.+)-module(?<tier>-mk0[1-9])?$")]
    private static partial Regex TurdModuleName();

    [GeneratedRegex(@"-mk0[1-9]$")]
    private static partial Regex BuildingTierSuffix();
}
