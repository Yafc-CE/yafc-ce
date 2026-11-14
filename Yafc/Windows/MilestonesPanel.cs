using System.Numerics;
using SDL2;
using Yafc.I18n;
using Yafc.Model;
using Yafc.UI;

namespace Yafc;

public class MilestonesWidget : SearchableList<FactorioObject> {
    public MilestonesWidget() : base(30f, new Vector2(3f, 3f), MilestoneDrawer, (element, query) => element.Match(query))
        => data = Project.current.settings.milestones;

    private static void MilestoneDrawer(ImGui gui, FactorioObject element, int index) {
        var settings = Project.current.settings;
        bool unlocked = settings.Flags(element).HasFlags(ProjectPerItemFlags.MilestoneUnlocked);
        if (gui.BuildFactorioObjectButton(element, ButtonDisplayStyle.Milestone(unlocked ? SchemeColor.Primary : SchemeColor.None)) == Click.Left) {
            if (!unlocked) {
                var massUnlock = Milestones.Instance.GetMilestoneResult(element);
                int subIndex = 0;
                settings.SetFlag(element, ProjectPerItemFlags.MilestoneUnlocked, true);
                foreach (var milestone in settings.milestones) {
                    subIndex++;
                    if (massUnlock[subIndex]) {
                        settings.SetFlag(milestone, ProjectPerItemFlags.MilestoneUnlocked, true);
                    }
                }
            }
            else {
                settings.SetFlag(element, ProjectPerItemFlags.MilestoneUnlocked, false);
            }
        }
        if (unlocked && gui.isBuilding) {
            gui.DrawIcon(gui.lastRect, Icon.Check, SchemeColor.Error);
        }
    }
}

public class MilestonesPanel : PseudoScreen {
    private readonly MilestonesWidget milestonesWidget = new();

    public override void Build(ImGui gui) {
        gui.spacing = 1f;
        BuildHeader(gui, LSs.Milestones);
        gui.BuildText(LSs.MilestonesHeader);
        gui.AllocateSpacing(2f);
        milestonesWidget.BuildSearchBox(gui);
        milestonesWidget.Build(gui);
        gui.AllocateSpacing(2f);
        gui.BuildText(LSs.MilestonesDescription, TextBlockDisplayStyle.WrappedText);
        using (gui.EnterRow()) {
            if (gui.BuildButton(LSs.MilestonesEdit, SchemeColor.Grey)) {
                MilestonesEditor.Show();
            }

            if (gui.BuildButton(LSs.MilestonesEditSettings)) {
                Close();
                PreferencesScreen.ShowProgression();
            }

            if (gui.RemainingRow().BuildButton(LSs.Done)) {
                Close();
            }
        }
    }

    protected override void ReturnPressed() => Close();

    public override bool KeyDown(SDL.SDL_Keysym key) {
        milestonesWidget.FocusSearchBox();
        return base.KeyDown(key);
    }
}
