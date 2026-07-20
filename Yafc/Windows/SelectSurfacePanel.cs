using System;
using System.Linq;
using Yafc.I18n;
using Yafc.Model;
using Yafc.UI;

namespace Yafc.Windows;

/// <summary>
/// A pseudoscreen for selecting the surface assigned to a particular table.
/// </summary>
internal class SelectSurfaceScreen : PseudoScreenWithResult<SelectedSurface> {
    private SelectSurfaceScreen(SelectedSurface selected, LocalizableString0 clearSelection) {
        platformPanel = new(20, new System.Numerics.Vector2(40, 2.5f), BuildPlatformItem, collapsible: true) {
            data = [null, .. Database.locations.all.OfType<Surface>().Where(s => s.factorioType is "surface")]
        };
        planetPanel = new(30, new System.Numerics.Vector2(40, 2.5f), BuildPlanetItem, collapsible: true) {
            data = [.. Database.locations.all.Where(l => l.factorioType is not "surface" && l.name != "space-location-unknown")]
        };
        selectedSurface = selected with { }; // Clone
        this.clearSelection = clearSelection;
    }

    private readonly VirtualScrollList<Surface?> platformPanel;
    private readonly VirtualScrollList<Location> planetPanel;
    // The text for the 'clear' button.
    private readonly string clearSelection;
    // The active selection, initialized to a copy of the value from the model.
    private readonly SelectedSurface selectedSurface;

    private bool CanSave => selectedSurface.planet != null && (selectedSurface.platform != null || selectedSurface.planet.factorioType is "planet");

    /// <summary>
    /// Show a panel that allows the user to select a location, for the purposes of crafting limitations and solar panel calculations.
    /// </summary>
    /// <param name="selected">The currently selected value, to initialize the UI</param>
    /// <param name="clearSelection"><see cref="LSs.ClearRootSurfaceSelection"/> or <see cref="LSs.ClearChildSurfaceSelection"/>, as appropriate to
    /// the data being edited.</param>
    /// <param name="callback">A callback called with the selected value when the user confirms the dialog.</param>
    public static void Show(SelectedSurface selected, LocalizableString0 clearSelection, Action<SelectedSurface> callback)
        => MainScreen.Instance.ShowPseudoScreen(new SelectSurfaceScreen(selected, clearSelection) { completionCallback = callback });

    public override void Build(ImGui gui) {
        gui.BuildText(LSs.SetIngameLocation, TextBlockDisplayStyle.Centered with { Font = Font.header });
        platformPanel.Build(gui);
        var barRect = gui.AllocateRect(gui.width - 2, 0.5f, RectAlignment.Middle);
        barRect = barRect.LeftPart(barRect.Width - 1);
        gui.DrawRectangle(barRect, SchemeColor.PureForeground);
        gui.AllocateSpacing();
        planetPanel.Build(gui);

        gui.allocator = RectAllocator.Center;
        if (gui.BuildRedButton(clearSelection)) {
            CloseWithResult(new());
        }

        if (CanSave) {
            if (gui.BuildButton(LSs.Done)) {
                CloseWithResult(selectedSurface);
            }
        }
        else if (selectedSurface.planet is null) {
            gui.BuildButton(LSs.NoPlanetSelected, active: false);
        }
        else {
            gui.BuildButton(LSs.CannotLandHere.L(selectedSurface.planet.locName), active: false);
        }
    }

    public void BuildPlatformItem(ImGui gui, Surface? surface, int _) {
        if (BuildButton(gui, selectedSurface.platform, surface)) {
            selectedSurface.platform = surface;
        }
    }

    public void BuildPlanetItem(ImGui gui, Location planet, int _) {
        if (BuildButton(gui, selectedSurface.planet, planet)) {
            selectedSurface.planet = planet;
        }
    }

    private bool BuildButton(ImGui gui, Location? selected, Location? toDraw) {
        SchemeColor color = SchemeColor.None;
        if (selectedSurface != null && selected == toDraw) {
            color = SchemeColor.Primary;
        }

        using (gui.EnterRow()) {
            gui.AllocateSpacing();
            if (toDraw is null) {
                gui.BuildIcon(Database.planetSurface.GetIcon(), 2);
                gui.AllocateSpacing();
                gui.allocator = RectAllocator.RemainingRow;
                gui.BuildText(LSs.SelectOnSurface);
            }
            else {
                gui.BuildFactorioObjectIcon(toDraw);
                gui.AllocateSpacing();
                gui.allocator = RectAllocator.RemainingRow;
                if (toDraw.factorioType is "surface") {
                    gui.BuildText(LSs.SelectInOrbit.L(toDraw.locName));
                }
                else {
                    gui.BuildText(toDraw.locName);
                }
            }
        }

        if (gui.BuildButton(gui.lastRect, color, SchemeColor.Grey)) {
            Rebuild();
            return true;
        }
        return false;
    }

    protected override void ReturnPressed() {
        if (CanSave) {
            CloseWithResult(selectedSurface);
        }
    }
}
