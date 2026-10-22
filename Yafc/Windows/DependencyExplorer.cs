using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SDL2;
using Yafc.I18n;
using Yafc.Model;
using Yafc.UI;

namespace Yafc;

public class DependencyExplorer : PseudoScreen, INavigationTarget {
    private readonly ScrollArea dependencies;
    private readonly ScrollArea dependents;
    private static readonly Padding listPad = new Padding(0.5f);

    public NavigationHelper navigationHelper { get; }

    private FactorioObject current;

    private static readonly Dictionary<DependencyNode.Flags, (LocalizableString1? name, LocalizableString0 missingText)> dependencyListTexts = new()
    {
        {DependencyNode.Flags.Fuel, (LSs.DependencyFuel, LSs.DependencyFuelMissing)},
        {DependencyNode.Flags.Ingredient, (LSs.DependencyIngredient, LSs.DependencyIngredientMissing)},
        {DependencyNode.Flags.IngredientVariant, (LSs.DependencyIngredient, LSs.DependencyIngredientVariantsMissing)},
        {DependencyNode.Flags.CraftingEntity, (LSs.DependencyCrafter, LSs.DependencyCrafterMissing)},
        {DependencyNode.Flags.Source, (LSs.DependencySource, LSs.DependencySourcesMissing)},
        {DependencyNode.Flags.TechnologyUnlock, (LSs.DependencyTechnology, LSs.DependencyTechnologyMissing)},
        {DependencyNode.Flags.TechnologyPrerequisites, (LSs.DependencyTechnology, LSs.DependencyTechnologyNoPrerequisites)},
        {DependencyNode.Flags.ItemToPlace, (LSs.DependencyItem, LSs.DependencyItemMissing)},
        {DependencyNode.Flags.SourceEntity, (LSs.DependencySource, LSs.DependencyMapSourceMissing)},
        {DependencyNode.Flags.Disabled, (null, LSs.DependencyTechnologyDisabled)},
        {DependencyNode.Flags.Location, (LSs.DependencyLocation, LSs.DependencyLocationMissing)},
    };
    public DependencyExplorer(FactorioObject current) : base(60f) {
        // Due to NavigationHelper needing to be accesible to anything using INavigationTarget without knowing what type it's specialized for, 
        //  we need to wrap the Change call in a lambda. 
        navigationHelper = new NavigationHelper { changeDelegate = arg => Change(arg) };
        dependencies = new ScrollArea(30f, DrawDependencies);
        dependents = new ScrollArea(30f, DrawDependents);
        this.current = current;
        navigationHelper.addHistory(current);
    }

    public static void Show(FactorioObject target) => MainScreen.Instance.ShowPseudoScreen(new DependencyExplorer(target));

    private void DrawFactorioObject(ImGui gui, FactorioId id) {
        FactorioObject obj = Database.objects[id];
        using (gui.EnterGroup(listPad, RectAllocator.LeftRow)) {
            gui.BuildFactorioObjectIcon(obj);
            string text = LSs.NameWithType.L(obj.locName, obj.type);
            gui.RemainingRow(0.5f).BuildText(text, TextBlockDisplayStyle.WrappedText with { Color = obj.IsAccessible() ? SchemeColor.BackgroundText : SchemeColor.BackgroundTextFaint });
        }
        if (gui.BuildFactorioObjectButtonBackground(gui.lastRect, obj, tooltipOptions: new() { ShowTypeInHeader = true }) == Click.Left) {
            Change(obj);
        }
    }

    private void DrawDependencies(ImGui gui) {
        gui.spacing = 0f;

        Dependencies.dependencyList[current].Draw(gui, (gui, elements, flags) => {
            string name;
            string missingText;
            if (dependencyListTexts.TryGetValue(flags, out var dependencyType)) {
                name = dependencyType.name?.L(elements.Count) ?? "";
                missingText = dependencyType.missingText;
            }
            else {
                name = flags.ToString();
                missingText = "Missing " + flags;
            }

            if (elements.Count > 0) {
                gui.AllocateSpacing(0.5f);
                if (elements.Count == 1) {
                    gui.BuildText(LSs.DependencyRequireSingle.L(name));
                }
                else if (flags.HasFlags(DependencyNode.Flags.RequireEverything)) {
                    gui.BuildText(LSs.DependencyRequireAll.L(name));
                }
                else {
                    gui.BuildText(LSs.DependencyRequireAny.L(name));
                }

                gui.AllocateSpacing(0.5f);
                foreach (var id in elements.OrderByDescending(x => CostAnalysis.Instance.flow[x])) {
                    DrawFactorioObject(gui, id);
                }
            }
            else {
                if (Database.rootAccessible.Contains(current)) {
                    missingText = LSs.DependencyAccessibleAnyway.L(missingText);
                }
                else {
                    missingText = LSs.DependencyAndNotAccessible.L(missingText);
                }

                gui.BuildText(missingText, TextBlockDisplayStyle.WrappedText);
            }
        });
    }

    private void DrawDependents(ImGui gui) {
        gui.spacing = 0f;
        foreach (var reverseDependency in Dependencies.reverseDependencies[current].OrderByDescending(x => CostAnalysis.Instance.flow[x])) {
            DrawFactorioObject(gui, reverseDependency);
        }
    }

    private void SetFlag(ProjectPerItemFlags flag, bool set) {
        Project.current.settings.SetFlag(current, flag, set);
        Analysis.Do<Milestones>(Project.current);
        Rebuild();
        dependents.Rebuild();
        dependencies.Rebuild();
    }

    public override void Build(ImGui gui) {
        gui.allocator = RectAllocator.Center;
        BuildHeader(gui, LSs.DependencyExplorer);
        using (gui.EnterRow()) {
            gui.BuildText(LSs.DependencyCurrentlyInspecting, Font.subheader);
            if (gui.BuildFactorioObjectButtonWithText(current) == Click.Left) {
                SelectSingleObjectPanel.Select(Database.objects.explorable, new(LSs.DependencySelectSomething), Change);
            }

            gui.DrawText(gui.lastRect, LSs.DependencyClickToChangeHint, RectAlignment.MiddleRight, color: TextBlockDisplayStyle.HintText.Color);
        }
        using (gui.EnterRow()) {
            var settings = Project.current.settings;
            if (current.IsAccessible()) {
                if (current.IsAutomatable()) {
                    gui.BuildText(LSs.DependencyAutomatable);
                }
                else {
                    gui.BuildText(LSs.DependencyAccessible);
                }

                if (settings.Flags(current).HasFlags(ProjectPerItemFlags.MarkedAccessible)) {
                    gui.BuildText(LSs.DependencyMarkedAccessible);
                    if (gui.BuildLink(LSs.DependencyClearMark)) {
                        SetFlag(ProjectPerItemFlags.MarkedAccessible, false);
                        NeverEnoughItemsPanel.Refresh();
                    }
                }
                else {
                    if (gui.BuildLink(LSs.DependencyMarkNotAccessible)) {
                        SetFlag(ProjectPerItemFlags.MarkedInaccessible, true);
                        NeverEnoughItemsPanel.Refresh();
                    }

                    if (gui.BuildLink(LSs.DependencyMarkAccessibleIgnoringMilestones)) {
                        SetFlag(ProjectPerItemFlags.MarkedAccessible, true);
                        NeverEnoughItemsPanel.Refresh();
                    }
                }
            }
            else {
                if (settings.Flags(current).HasFlags(ProjectPerItemFlags.MarkedInaccessible)) {
                    gui.BuildText(LSs.DependencyMarkedNotAccessible);
                    if (gui.BuildLink(LSs.DependencyClearMark)) {
                        SetFlag(ProjectPerItemFlags.MarkedInaccessible, false);
                        NeverEnoughItemsPanel.Refresh();
                    }
                }
                else {
                    gui.BuildText(LSs.DependencyNotAccessible);
                    if (gui.BuildLink(LSs.DependencyMarkAccessible)) {
                        SetFlag(ProjectPerItemFlags.MarkedAccessible, true);
                        NeverEnoughItemsPanel.Refresh();
                    }
                }
            }
        }
        gui.AllocateSpacing(2f);
        using var split = gui.EnterHorizontalSplit(2);
        split.Next();
        gui.BuildText(LSs.DependencyHeaderDependencies.L(Dependencies.dependencyList[current].Count()), Font.subheader);
        dependencies.Build(gui);
        split.Next();
        gui.BuildText(LSs.DependencyHeaderDependents.L(Dependencies.reverseDependencies[current].Count), Font.subheader);
        dependents.Build(gui);
    }

    public void Change(FactorioObject target) {
        navigationHelper.addHistory(target);

        current = target;
        dependents.Rebuild();
        dependencies.Rebuild();
        Rebuild();
    }

    /// <summary>
    /// Handle KeyDown event. 
    /// Forward to the NavigationHelper for navigation key handling.
    /// </summary>
    public override bool KeyDown(SDL.SDL_Keysym key) {
        if (navigationHelper.HandkeKeyDownNavigation(key)) {
            return true;
        }
        return base.KeyDown(key);
    }
}
