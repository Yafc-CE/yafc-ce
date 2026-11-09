using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SDL2;
using Serilog;
using Yafc.Core;
using Yafc.I18n;
using Yafc.Model;
using Yafc.Parser;
using Yafc.UI;

namespace Yafc;


/// <summary>
/// Contains information about a language supported by Factorio.
/// </summary>
/// <param name="NativeName">The name of the language, expressed in its language. To select this language, the current font must contain glyphs
/// for all characters in this string.</param>
/// <param name="LatinName"><paramref name="NativeName"/>, if that uses only well-supported Latin characters, or the English name of the
/// language.</param>
/// <param name="BaseFontName">The base name of the default font to use for this language.</param>
internal sealed record LanguageInfo(string LatinName, string NativeName, string BaseFontName) {
    public LanguageInfo(string latinName, string nativeName) : this(latinName, nativeName, "Roboto") { }
    public LanguageInfo(string name) : this(name, name, "Roboto") { }

    /// <summary>
    /// If not <see langword="null"/>, the current font must also have glyphs for these characters. Some fonts have enough glyphs for the
    /// language name, but don't have glyphs for all characters used by the language. (e.g. Some fonts have glyphs for türkmençe but are missing
    /// the Turkish ı.)
    /// </summary>
    public string? CheckExtraCharacters { get; set; }
}

public class GlobalPreferencesScreen : WindowUtility, IKeyboardFocus
{
    internal GlobalPreferencesScreen(Window parent) : base(ImGuiUtils.DefaultScreenPadding) {
        languageScroll = new ScrollArea(20f, LanguageSelection, collapsible: true);
        Create(LSs.AboutYafc, 50, parent);
        _ = InputSystem.Instance.SetDefaultKeyboardFocus(this);
    } 
    private readonly ScrollArea languageScroll;
    internal static bool downloading;

    internal static readonly SortedList<string, LanguageInfo> languageMapping = new()
    {
        {"en", new("English")},
        {"af", new("Afrikaans")},
        {"ar", new("Arabic", "العربية", "noto-sans-arabic")},
        {"be", new("Belarusian", "беларуская")},
        {"bg", new("Bulgarian", "български")},
        {"ca", new("català")},
        {"cs", new("Czech", "čeština")},
        {"da", new("dansk")},
        {"de", new("Deutsch")},
        {"el", new("Greek", "Ελληνικά")},
        {"es-ES", new("español")},
        {"et", new("eesti") },
        {"eu", new("euskara") },
        {"fi", new("suomi")},
        {"fil", new("Filipino")},
        {"fr", new("français")},
        {"fy-NL", new("Frysk")},
        {"ga-IE", new("Gaeilge")},
        {"he", new("Hebrew", "עברית", "noto-sans-hebrew")},
        {"hr", new("hrvatski")},
        {"hu", new("magyar")},
        {"id", new("Bahasa Indonesia")},
        {"is", new("íslenska")},
        {"it", new("italiano")},
        {"ja", new("Japanese", "日本語", "noto-sans-jp") { CheckExtraCharacters = "軽気処鉱砕" }},
        {"ka", new("Georgian", "ქართული", "noto-sans-georgian")},
        {"kk", new("Kazakh", "Қазақша")},
        {"ko", new("Korean", "한국어", "noto-sans-kr")},
        {"lt", new("Lithuanian", "lietuvių")},
        {"lv", new("Latvian", "latviešu")},
        {"nl", new("Nederlands")},
        {"no", new("norsk")},
        {"pl", new("polski")},
        {"pt-PT", new("português")},
        {"pt-BR", new("português (Brazil)")},
        {"ro", new("română")},
        {"ru", new("Russian", "русский")},
        {"sk", new("Slovak", "slovenčina")},
        {"sl", new("slovenski")},
        {"sq", new("shqip")},
        {"sr", new("Serbian", "српски")},
        {"sv-SE", new("svenska")},
        {"th", new("Thai", "ไทย", "noto-sans-thai")},
        {"tr", new("Turkish","türkmençe") { CheckExtraCharacters = "ığş" }},
        {"uk", new("Ukranian", "українська")},
        {"vi", new("Vietnamese", "Tiếng Việt")},
        {"zh-CN", new("Chinese (Simplified)", "汉语", "noto-sans-sc")},
        {"zh-TW", new("Chinese (Traditional)", "漢語", "noto-sans-tc") { CheckExtraCharacters = "鈾礦" }},
    };

    internal ScrollArea LanguageScroll => languageScroll;

    internal static void LanguageSelection(ImGui gui) {
        gui.spacing = 0f;
        gui.allocator = RectAllocator.LeftAlign;
        gui.BuildText(LSs.WelcomeAlertEnglishFallback, TextBlockDisplayStyle.WrappedText);
        gui.AllocateSpacing(0.5f);

        DoLanguageList(gui, languageMapping, true);
        if (!Program.hasOverriddenFont) {
            gui.AllocateSpacing(0.5f);

            gui.BuildText(LSs.WelcomeAlertNeedADifferentFont, TextBlockDisplayStyle.WrappedText);
            gui.AllocateSpacing(0.5f);
        }
        DoLanguageList(gui, languageMapping, false);
    }

    private static void DoLanguageList(ImGui gui, SortedList<string, LanguageInfo> list, bool listFontSupported) {
        foreach (var (k, v) in list) {
            if (!Font.text.CanDraw(v.NativeName + v.CheckExtraCharacters) && !listFontSupported) {
                bool result;
                if (Font.text.CanDraw(v.NativeName)) {
                    result = gui.BuildLink($"{v.NativeName} ({k})");
                }
                else {
                    result = gui.BuildLink($"{v.LatinName} ({k})");
                }

                if (result) {
                    if (Font.FilesExist(v.BaseFontName) || Program.hasOverriddenFont) {
                        Preferences.Instance.language = k;
                        Preferences.Instance.Save();
                        gui.CloseDropdown();
                        restartIfNecessary();
                    }
                    else {
                        gui.ShowDropDown(async gui => {
                            gui.BuildText(LSs.WelcomeAlertDownloadFont, TextBlockDisplayStyle.WrappedText);
                            gui.allocator = RectAllocator.Center;
                            if (gui.BuildButton(LSs.Confirm)) {
                                gui.CloseDropdown();
                                downloading = true;
                                // Jump through several hoops to download an appropriate Noto Sans font.
                                // Google Webfonts Helper is the first place I found that would (eventually) let me directly download ttf files
                                // for the static fonts. It can also provide links to Google, but that would take three requests, instead of two.
                                // Variable fonts can be acquired from github, but SDL_ttf doesn't support those yet.
                                // See https://gwfh.mranftl.com/fonts, https://github.com/majodev/google-webfonts-helper, and
                                // https://github.com/libsdl-org/SDL_ttf/pull/506
                                HttpClient client = new();
                                // Get the character subsets supported by this font
                                string query = "https://gwfh.mranftl.com/api/fonts/" + v.BaseFontName;
                                dynamic response = JObject.Parse(await client.GetStringAsync(query));
                                // Request a zip containing ttf files and all character subsets
                                query += "?variants=300,regular&download=zip&formats=ttf&subsets=";
                                foreach (string item in response["subsets"]) {
                                    query += item + ",";
                                }
                                ZipArchive archive = new(await client.GetStreamAsync(query));
                                // Extract the two ttf files into the expected locations
                                foreach (var entry in archive.Entries) {
                                    if (entry.Name.Contains("300")) {
                                        entry.ExtractToFile($"Data/{v.BaseFontName}-Light.ttf");
                                    }
                                    else {
                                        entry.ExtractToFile($"Data/{v.BaseFontName}-Regular.ttf");
                                    }
                                }
                                // Save and restart
                                Preferences.Instance.language = k;
                                Preferences.Instance.Save();
                                restartIfNecessary();
                            }
                        });
                    }
                }
            }
            else if (Font.text.CanDraw(v.NativeName + v.CheckExtraCharacters) && listFontSupported && gui.BuildLink(v.NativeName + " (" + k + ")")) {
                Preferences.Instance.language = k;
                Preferences.Instance.Save();
                _ = gui.CloseDropdown();
            }
        }

        static void restartIfNecessary() {
            if (!Program.hasOverriddenFont) {
                Process.Start("dotnet", Assembly.GetEntryAssembly()!.Location);
                Environment.Exit(0);
            }
        }
    }

    private async void SelectFont() {
        string? result = await new FilesystemScreen(LSs.OverrideFont, LSs.OverrideFontLong, LSs.Ok, null, FilesystemScreen.Mode.SelectFile, null, this, null, null);
        if (result == null) {
            return;
        }

        if (SDL_ttf.TTF_OpenFont(result, 16) != IntPtr.Zero) {
            Preferences.Instance.overrideFont = result;
            languageScroll.RebuildContents();
            Preferences.Instance.Save();
        }
    }

    protected override void BuildContents(ImGui gui) {
        gui.spacing = 1.5f;
        gui.BuildText(LSs.FullName, new TextBlockDisplayStyle(Font.header, Alignment: RectAlignment.Middle));
        if (downloading) {
            gui.BuildText(LSs.PleaseWait, TextBlockDisplayStyle.Centered);
            gui.BuildText(LSs.DownloadingFonts, TextBlockDisplayStyle.Centered);
        }
        else {
            using (gui.EnterRow()) {
                gui.BuildText(LSs.WelcomeLanguageHeader);
                //gui.allocator = RectAllocator.RightRow;
                string lang = Preferences.Instance.language;
                if (languageMapping.TryGetValue(Preferences.Instance.language, out LanguageInfo? mapped)) {
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
                    Preferences.Instance.overrideFont = null;
                    languageScroll.RebuildContents();
                    Preferences.Instance.Save();
                }
            }
            gui.BuildText(LSs.WelcomeResetFontRestart, TextBlockDisplayStyle.WrappedText);
            gui.AllocateSpacing(0.5f);

            gui.spacing = .5f;

            using (gui.EnterRowWithHelpIcon(LSs.WelcomeLoadAutosaveHint, false)) {
                if (gui.BuildCheckBox(LSs.WelcomeLoadAutosave, Preferences.Instance.useMostRecentSave,
                        out bool useMostRecentSave)) {
                    Preferences.Instance.useMostRecentSave = useMostRecentSave;
                    Preferences.Instance.Save();
                }
            }

            using (gui.EnterRowWithHelpIcon(LSs.WelcomeSoftwareRenderHint, false)) {
                bool forceSoftwareRenderer = Preferences.Instance.forceSoftwareRenderer;
                _ = gui.BuildCheckBox(LSs.WelcomeSoftwareRender, forceSoftwareRenderer, out forceSoftwareRenderer);

                if (forceSoftwareRenderer != Preferences.Instance.forceSoftwareRenderer) {
                    Preferences.Instance.forceSoftwareRenderer = forceSoftwareRenderer;
                    Preferences.Instance.Save();
                }
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
