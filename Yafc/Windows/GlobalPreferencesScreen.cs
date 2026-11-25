using System;
using System.IO;
using SDL2;
using Yafc.I18n;
using Yafc.UI;

namespace Yafc;

public class GlobalPreferencesScreen : WindowUtility, IKeyboardFocus {
    internal readonly ScrollArea languageScroll = new ScrollArea(20f, Language.LanguageSelection, collapsible: true);
    private bool isOpenedFromWelcomeScreen;

    internal GlobalPreferencesScreen(Window parent, bool isOpenedFromWelcomeScreen) : base(ImGuiUtils.DefaultScreenPadding) {
        this.isOpenedFromWelcomeScreen = isOpenedFromWelcomeScreen;
        Create(LSs.GlobalPreferences, 50, parent);
        _ = InputSystem.Instance.SetDefaultKeyboardFocus(this);
    }
    private bool IsValidFontFileExtension(string filepath) => filepath.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || filepath.EndsWith(".otf", StringComparison.OrdinalIgnoreCase);

    private async void SelectFont() {
        // Use the YAFC/Data directory as the default folder because that is where YAFC has it's existing fonts as well as where it downloads additional ones.
        // Additionally it only shows (and allows selection of) .ttf and .otf files. This matches the code below making that assumption.
        string? result = await new FilesystemScreen(LSs.OverrideFont, LSs.OverrideFontLong, LSs.Ok, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data"), FilesystemScreen.Mode.SelectFile, null, this, IsValidFontFileExtension, null);
        if (result == null) {
            return;
        }

        if (SDL_ttf.TTF_OpenFont(result, 16) != IntPtr.Zero) {
            ApplyFont(result);
        }
    }

    private void ApplyFont(string? result) {
        Preferences.Instance.overrideFont = result;
        Preferences.Instance.Save();
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
                string lang = Preferences.Instance.language;
                if (Language.LanguageMapping.TryGetValue(Preferences.Instance.language, out LanguageInfo? mapped)) {
                    lang = mapped.NativeName;
                }

                if (gui.BuildLink(lang)) {
                    gui.ShowDropDown(languageScroll.Build);
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
                if (gui.BuildCheckBox(LSs.WelcomeSoftwareRender, forceSoftwareRenderer, out forceSoftwareRenderer, SchemeColor.None, RectAllocator.LeftRow, null, isOpenedFromWelcomeScreen)) {
                    Preferences.Instance.forceSoftwareRenderer = forceSoftwareRenderer;
                    Preferences.Instance.Save();
                }
            }

            if (gui.BuildCheckBox(LSs.PrefsAutosave, Preferences.Instance.autosaveEnabled, out bool autosaveEnabled)) {
                Preferences.Instance.autosaveEnabled = autosaveEnabled;
                Preferences.Instance.Save();
            }

            if (gui.BuildCheckBox(LSs.PrefsExportEntitiesWithFuelFilterSet, Preferences.Instance.exportEntitiesWithFuelFilter, out bool exportEntitiesWithFuelFilter)) {
                Preferences.Instance.exportEntitiesWithFuelFilter = exportEntitiesWithFuelFilter;
                Preferences.Instance.Save();
            }

            gui.spacing = 1.5f;
            if (gui.BuildCheckBox(LSs.PrefsDarkMode, Preferences.Instance.darkMode, out bool newValue)) {
                Preferences.Instance.darkMode = newValue;
                Preferences.Instance.Save();
                RenderingUtils.SetColorScheme(newValue);
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
