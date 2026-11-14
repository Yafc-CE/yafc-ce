using System.Linq;
using System.Numerics;
using SDL2;
using Yafc.I18n;
using Yafc.Model;
using Yafc.UI;

namespace Yafc;

public class MilestonesEditor : PseudoScreen {
    private readonly SearchableList<FactorioObject> milestoneList;

    private MilestonesEditor() : base(50) {
        milestoneList = new SearchableList<FactorioObject>(30f, new Vector2(float.PositiveInfinity, 3f), MilestoneDrawer,
            (element, query) => element.Match(query)) {

            data = Project.current.settings.milestones
        };

        // Recalculate the project with the possibly-modified milestones.
        cleanupCallback = () => Milestones.Instance.Compute(Project.current, new());
    }

    public static void Show() => MainScreen.Instance.ShowPseudoScreen(new MilestonesEditor());

    private void MilestoneDrawer(ImGui gui, FactorioObject element, int index) {
        using (gui.EnterRow()) {
            var settings = Project.current.settings;
            gui.BuildFactorioObjectIcon(element, new IconDisplayStyle(3f, MilestoneDisplay.None, false));
            gui.BuildText(element.locName, maxWidth: width - 16.6f); // Experimentally determined width of the non-text parts of the editor.
            if (gui.BuildButton(Icon.Close, size: 1f)) {
                _ = settings.RecordUndo().milestones.Remove(element);
                Rebuild();
            }
        }

        // Only reorder the full list: in a filtered list, the index is not the position in the milestones, and a move would
        // also shift the hidden milestones in between.
        if (milestoneList.filter.empty && gui.DoListReordering(gui.lastRect, gui.lastRect, index, out int moveFrom)) {
            Project.current.settings.RecordUndo().milestones.MoveListElementIndex(moveFrom, index);
        }
    }

    public override bool KeyDown(SDL.SDL_Keysym key) {
        milestoneList.FocusSearchBox();
        return base.KeyDown(key);
    }

    public override void Build(ImGui gui) {
        BuildHeader(gui, LSs.MilestoneEditor);
        milestoneList.BuildSearchBox(gui);
        milestoneList.Build(gui);

        gui.BuildText(LSs.MilestoneDescription, TextBlockDisplayStyle.WrappedText with { Color = SchemeColor.BackgroundTextFaint });

        using (gui.EnterRow()) {
            if (gui.BuildButton(LSs.MilestoneAutoSort, SchemeColor.Grey)) {
                ErrorCollector collector = new ErrorCollector();
                Milestones.Instance.ComputeWithParameters(Project.current, collector, [.. Project.current.settings.milestones], true);

                if (collector.severity > ErrorSeverity.None) {
                    ErrorListPanel.Show(collector);
                }

                milestoneList.RebuildContents();
            }
            if (gui.BuildButton(LSs.MilestoneAdd)) {
                SelectMultiObjectPanel.Select(Database.objects.explorable.Except(Project.current.settings.milestones), new(LSs.MilestoneAddNew, Multiple: true), AddMilestone);
            }
        }
    }

    private void AddMilestone(FactorioObject obj) {
        var settings = Project.current.settings;

        if (settings.milestones.Contains(obj)) {
            MessageBox.Show(LSs.MilestoneCannotAdd, LSs.MilestoneCannotAddAlreadyExists, LSs.Ok);
            return;
        }

        var lockedMask = Milestones.Instance.GetMilestoneResult(obj);

        if (lockedMask.IsClear()) {
            settings.RecordUndo().milestones.Add(obj);
        }
        else {
            int bestIndex = 0;
            for (int i = 1; i < lockedMask.length; i++) {
                if (lockedMask[i]) {
                    lockedMask[i] = false;
                    var milestone = Milestones.Instance.currentMilestones[i - 1];
                    int index = settings.milestones.IndexOf(milestone);

                    if (index >= bestIndex) {
                        bestIndex = index + 1;
                    }

                    if (lockedMask.IsClear()) {
                        break;
                    }
                }
            }
            settings.RecordUndo().milestones.Insert(bestIndex, obj);
        }

        // Do not hide the new milestone, or it looks like adding it failed.
        if (!obj.Match(milestoneList.filter)) {
            milestoneList.filter = default;
        }

        Rebuild();
    }
}
