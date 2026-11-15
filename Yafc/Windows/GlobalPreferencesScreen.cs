using System;
using SDL2;
using Yafc.I18n;
using Yafc.UI;

namespace Yafc;

public class GlobalPreferencesScreen : WindowUtility, IKeyboardFocus {

    internal GlobalPreferencesScreen(Window parent) : base(ImGuiUtils.DefaultScreenPadding) {
        Create(LSs.GlobalPreferences, 50, parent);
        _ = InputSystem.Instance.SetDefaultKeyboardFocus(this);
    }
    private bool IsValidFontFileExtension(string filepath) => filepath.ToLowerInvariant().EndsWith(".ttf") || filepath.ToLowerInvariant().EndsWith(".otf");

    private async void SelectFont() {
        // Use the YAFC/Data directory as the default folder because that is where YAFC has it's existing fonts as well as where it downloads additional ones.
        // Additionally it only shows (and allows selection of) .ttf files, or truetype fonts. This matches the code below making that assumption.
        string? result = await new FilesystemScreen(LSs.OverrideFont, LSs.OverrideFontLong, LSs.Ok, AppDomain.CurrentDomain.BaseDirectory + "Data", FilesystemScreen.Mode.SelectFile, null, this, IsValidFontFileExtension, null);
        if (result == null) {
            return;
        }

        if (SDL_ttf.TTF_OpenFont(result, 16) != IntPtr.Zero) {
            ApplyFont(result);
        }
    }

    private static void ApplyFont(string? result) {
        Preferences.Instance.overrideFont = result;
        Language.languageScroll.RebuildContents();
        Preferences.Instance.Save();
        Program.SetFont();
        // All windows need to be forcibly updated when font changes, as different fonts effectively have different (effective) sizes.
        Ui.UpdateWindowSizes();
    }

    protected override void BuildContents(ImGui gui) {
        gui.spacing = 1.5f;
        gui.BuildText(LSs.FullName, new TextBlockDisplayStyle(Font.header, Alignment: RectAlignment.Middle));
        if (Language.downloading) {
            gui.BuildText(LSs.PleaseWait, TextBlockDisplayStyle.Centered);
            gui.BuildText(LSs.DownloadingFonts, TextBlockDisplayStyle.Centered);
        }
        else {
            using (gui.EnterRow()) {
                gui.BuildText(LSs.WelcomeLanguageHeader);
                //gui.allocator = RectAllocator.RightRow;
                string lang = Preferences.Instance.language;
                if (Language.LanguageMapping.TryGetValue(Preferences.Instance.language, out LanguageInfo? mapped)) {
                    lang = mapped.NativeName;
                }

                if (gui.BuildLink(lang)) {
                    gui.ShowDropDown(Language.languageScroll.Build);
                }
            }

            gui.AllocateSpacing(0.5f);
            if (gui.BuildButton(LSs.WelcomeSelectFont)) {
                SelectFont();
            }

            if (Preferences.Instance.overrideFont != null) {
                gui.BuildText(Preferences.Instance.overrideFont, TextBlockDisplayStyle.WrappedText);
                if (gui.BuildLink(LSs.WelcomeResetFont)) {
                    ApplyFont(null);
                }
            }
            gui.AllocateSpacing(0.5f);

            gui.spacing = .5f;

            using (gui.EnterRowWithHelpIcon(LSs.WelcomeLoadAutosaveHint, false)) {
                if (gui.BuildCheckBox(LSs.WelcomeLoadAutosave, Preferences.Instance.useMostRecentSave,
                        out bool useMostRecentSave)) {
                    Preferences.Instance.useMostRecentSave = useMostRecentSave;
                    Preferences.Instance.Save();
                }
            }

            // If the player can reach the option from the Main Menu, they should not need to change this option to begin with.
            using (gui.EnterRowWithHelpIcon(LSs.WelcomeSoftwareRenderHint, false)) {
                bool forceSoftwareRenderer = Preferences.Instance.forceSoftwareRenderer;
                // Treat this as "disabled" if opened from the Main Screen, as changing it would have no effect until a restart.
                //  Additionally disabling it could make the Main Screen unusable / crash for some users.
                _ = gui.BuildCheckBox(LSs.WelcomeSoftwareRender, forceSoftwareRenderer, out forceSoftwareRenderer, (parent is MainScreen) ? SchemeColor.Grey : SchemeColor.None, RectAllocator.LeftRow, null, (parent is not MainScreen));
                Preferences.Instance.forceSoftwareRenderer = forceSoftwareRenderer;
                Preferences.Instance.Save();
            }

            if (gui.BuildCheckBox(LSs.PrefsAutosave, Preferences.Instance.autosaveEnabled, out bool autosaveEnabled)) {
                Preferences.Instance.autosaveEnabled = autosaveEnabled;
            }

            if (gui.BuildCheckBox(LSs.PrefsExportEntitiesWithFuelFilterSet, Preferences.Instance.exportEntitiesWithFuelFilter, out bool exportEntitiesWithFuelFilter)) {
                Preferences.Instance.exportEntitiesWithFuelFilter = exportEntitiesWithFuelFilter;
            }

            gui.spacing = 1.5f;
            using (gui.EnterRow()) {
                if (gui.BuildButton(Icon.DarkMode)) {
                    Preferences.Instance.darkMode = !Preferences.Instance.darkMode;
                    RenderingUtils.SetColorScheme(Preferences.Instance.darkMode);
                    Preferences.Instance.Save();
                }
                gui.BuildText(LSs.ToggleDarkMode, TextBlockDisplayStyle.WrappedText);
            }
        }
    }


    public bool KeyDown(SDL.SDL_Keysym key) {
        if (key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE) {
            Close();
            return true;
        }
        return false;
    }
    public bool TextInput(string input) => false;
    public bool KeyUp(SDL.SDL_Keysym key) => false;
    public void FocusChanged(bool focused) { }

}
