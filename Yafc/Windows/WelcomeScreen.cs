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

public class WelcomeScreen : WindowUtility, IProgress<(string, string)>, IKeyboardFocus {
    private readonly ILogger logger = Logging.GetLogger<WelcomeScreen>();
    private bool loading;
    private string? currentLoad1, currentLoad2;
    private string path = "", dataPath = "";
    private string modsPath {
        get;
        set {
            field = value;
            FactorioDataSource.ClearDisabledMods();
        }
    } = "";
    private bool expensive;
    private bool netProduction;
    private string createText;
    private bool canCreate;
    private readonly ScrollArea errorScroll;
    private readonly ScrollArea recentProjectScroll;
    private readonly ScrollArea languageScroll;
    private string? errorMod;
    private const string DefinesUpdateCommand = "/command helpers.write_file(\"Defines2.1.lua\", \"return \" .. serpent.line(defines), false)";
    private DefinesUpdateState definesUpdateState;
    private string? errorMessage;
    private string? tip;
    private readonly string[] tips;
    private bool useMostRecentSave = true;

    private enum DefinesUpdateState {
        None,
        WaitingForCopy,
        WaitingForFile,
    }

    private enum EditType {
        Workspace, Factorio, Mods
    }

    public WelcomeScreen(ProjectDefinition? cliProject = null) : base(ImGuiUtils.DefaultScreenPadding) {
        tips = File.ReadAllLines("Data/Tips.txt");

        IconCollection.ClearCustomIcons();
        RenderingUtils.SetColorScheme(Preferences.Instance.darkMode);

        recentProjectScroll = new ScrollArea(20f, BuildRecentProjectList, collapsible: true);
        languageScroll = new ScrollArea(20f, GlobalPreferencesScreen.LanguageSelection, collapsible: true);
        errorScroll = new ScrollArea(20f, BuildRedErrorPanel, collapsible: true);
        Create(LSs.Welcome.L(YafcLib.version.ToString(3)), 45, null);

        if (cliProject != null && !string.IsNullOrEmpty(cliProject.dataPath)) {
            SetProject(cliProject);
            LoadProject();
        }
        else {
            ProjectDefinition? lastProject = Preferences.Instance.recentProjects.FirstOrDefault();
            SetProject(lastProject);
            _ = InputSystem.Instance.SetDefaultKeyboardFocus(this);
        }
    }

    private void BuildRedErrorPanel(ImGui gui) {
        if (errorMod != null) {
            gui.BuildText(LSs.ErrorWhileLoadingMod.L(errorMod), TextBlockDisplayStyle.Centered with { Color = SchemeColor.Error });
        }

        gui.allocator = RectAllocator.Stretch;
        gui.BuildText(errorMessage, TextBlockDisplayStyle.ErrorText with { Color = SchemeColor.ErrorText });
        gui.DrawRectangle(gui.lastRect, SchemeColor.Error);
    }

    protected override void BuildContents(ImGui gui) {
        gui.spacing = 1.5f;
        gui.BuildText(LSs.FullName, new TextBlockDisplayStyle(Font.header, Alignment: RectAlignment.Middle));
        if (loading) {
            gui.BuildText(currentLoad1, TextBlockDisplayStyle.Centered);
            gui.BuildText(currentLoad2, TextBlockDisplayStyle.Centered);
            gui.AllocateSpacing(15f);
            gui.BuildText(tip, new TextBlockDisplayStyle(WrapText: true, Alignment: RectAlignment.Middle));
            gui.SetNextRebuild(Ui.time + 30);
        }
        else if (GlobalPreferencesScreen.downloading) {
            gui.BuildText(LSs.PleaseWait, TextBlockDisplayStyle.Centered);
            gui.BuildText(LSs.DownloadingFonts, TextBlockDisplayStyle.Centered);
        }
        else if (errorMessage != null) {
            errorScroll.Build(gui);
            bool thereIsAModToDisable = (errorMod != null);

            using (gui.EnterRow()) {
                if (thereIsAModToDisable) {
                    gui.BuildWrappedText(LSs.UnableToLoadWithMod);
                }
                else {
                    gui.BuildWrappedText(LSs.UnableToLoad);
                }
            }

            using (gui.EnterRow()) {
                if (gui.BuildLink(LSs.MoreInfo)) {
                    ShowDropDown(gui, gui.lastRect, ProjectErrorMoreInfo, new Padding(0.5f), 30f);
                }
            }

            using (gui.EnterRow()) {
                if (gui.BuildButton(LSs.CopyToClipboard, SchemeColor.Grey)) {
                    _ = SDL.SDL_SetClipboardText(errorMessage);
                }
                if (thereIsAModToDisable && gui.BuildButton(LSs.DisableAndReload).WithTooltip(gui, LSs.DisableAndReloadHint)) {
                    FactorioDataSource.DisableMod(errorMod!);
                    errorMessage = null;
                    LoadProject();
                }
                if (gui.RemainingRow().BuildButton(LSs.BackButton)) {
                    errorMessage = null;
                    Rebuild();
                }
            }
        }
        else if (definesUpdateState != DefinesUpdateState.None) {
            gui.BuildText(LSs.LoadErrorNeedsNewDefines, TextBlockDisplayStyle.WrappedText);
            using (gui.EnterRow()) {
                if (gui.BuildButton(LSs.CopyUpdateDefinesCommand, definesUpdateState == DefinesUpdateState.WaitingForCopy ? SchemeColor.Primary : SchemeColor.Grey)) {
                    SDL.SDL_SetClipboardText(DefinesUpdateCommand);
                    definesUpdateState = DefinesUpdateState.WaitingForFile;
                }
                if (gui.BuildButton(LSs.MenuRunFactorio, definesUpdateState == DefinesUpdateState.WaitingForFile ? SchemeColor.Primary : SchemeColor.Grey)) {
                    MainScreen.StartFactorio();
                }
                if (tryFileCopy()) {
                    definesUpdateState = DefinesUpdateState.None;
                    LoadProject();
                }
                ButtonEvent evt = gui.RemainingRow().BuildButton(LSs.BackButton, SchemeColor.Grey);
                // "C:/<whatever>" looks wrong, but not replacing the \ risks trying to display "\n", which cannot be escaped when localizing.
                // (Factorio treats "\\n" as '\' + '\n', not '\\' + 'n', and we do the same.)
                _ = evt.WithTooltip(gui, LSs.LoadErrorNotYetUpdated.L(Path.Combine(Environment.CurrentDirectory, "Data").Replace('\\', '/')));
                if (evt) {
                    Rebuild();
                    definesUpdateState = DefinesUpdateState.None;
                }
            }
        }
        else {
            BuildPathSelect(gui, path, LSs.WelcomeProjectFileLocation, LSs.WelcomeProjectFileLocationHint, EditType.Workspace, LSs.EnterProjectName);
            BuildPathSelect(gui, dataPath, LSs.WelcomeDataLocation,
                LSs.WelcomeDataLocationHint, EditType.Factorio);
            BuildPathSelect(gui, modsPath, LSs.WelcomeModLocation,
                LSs.WelcomeModLocationHint, EditType.Mods);

            using (gui.EnterRow()) {
                using (gui.EnterRowWithHelpIcon(LSs.WelcomeExpensiveRecipesHint, false)) {
                    _ = gui.BuildCheckBox(LSs.WelcomeExpensiveRecipes, expensive, out expensive);
                }
                gui.allocator = RectAllocator.RightRow;
                string lang = Preferences.Instance.language;
                if (GlobalPreferencesScreen.languageMapping.TryGetValue(Preferences.Instance.language, out LanguageInfo? mapped)) {
                    lang = mapped.NativeName;
                }

                if (gui.BuildLink(lang)) {
                    gui.ShowDropDown(languageScroll.Build);
                }

                gui.BuildText(LSs.WelcomeLanguageHeader);
            }

            gui.spacing = .5f;

            using (gui.EnterRowWithHelpIcon(LSs.WelcomeLoadAutosaveHint, false)) {
                if (gui.BuildCheckBox(LSs.WelcomeLoadAutosave, Preferences.Instance.useMostRecentSave,
                        out useMostRecentSave)) {
                    Preferences.Instance.useMostRecentSave = useMostRecentSave;
                    Preferences.Instance.Save();
                }
            }

            using (gui.EnterRowWithHelpIcon(LSs.WelcomeUseNetProductionHint, false)) {
                _ = gui.BuildCheckBox(LSs.WelcomeUseNetProduction, netProduction, out netProduction);
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
                if (Preferences.Instance.recentProjects.Length > 1) {
                    if (gui.BuildButton(LSs.RecentProjects, SchemeColor.Grey)) {
                        gui.ShowDropDown(BuildRecentProjectsDropdown, 35f);
                    }
                }
                if (gui.BuildButton(Icon.Help).WithTooltip(gui, LSs.AboutYafc)) {
                    _ = new AboutScreen(this);
                }

                if (gui.BuildButton(Icon.DarkMode).WithTooltip(gui, LSs.ToggleDarkMode)) {
                    Preferences.Instance.darkMode = !Preferences.Instance.darkMode;
                    RenderingUtils.SetColorScheme(Preferences.Instance.darkMode);
                    Preferences.Instance.Save();
                }

                //TODO: Fix tooltip, Icon, 
                if (gui.BuildButton(Icon.Settings).WithTooltip(gui, LSs.WelcomeGlobalPreferencesTooltip)) {
                    _ = new GlobalPreferencesScreen(this);
                }
                if (gui.RemainingRow().BuildButton(createText, active: canCreate)) {
                    LoadProject();
                }
            }
        }

        bool tryFileCopy() {
            // Potential locations for the script-output folder:
            string[] factorioUserFolders = [
                modsPath + "/..", // All OS, if the user entered a mod path
                dataPath + "/..", // Portable installation, at least Windows and maybe all OS
                "~/.factorio", // Linux
                "~/.var/app/com.valvesoftware.Steam/.factorio", // Linux with flatpak Steam, if I'm reading the wiki right
                "~/Library/Application Support/factorio", // MacOS
                Environment.ExpandEnvironmentVariables("%appdata%/Factorio"), // Windows
            ];

            foreach (string root in factorioUserFolders) {
                string file = Path.Combine(root, "script-output/Defines2.1.lua");
                if (File.Exists(file)) {
                    File.Move(file, Path.Combine(Environment.CurrentDirectory, "Data/Defines2.1.lua"), true);
                    return true;
                }
            }
            return false;
        }
    }

    private void ProjectErrorMoreInfo(ImGui gui) {

        gui.BuildWrappedText(LSs.LoadErrorAdvice);

        // The whole line is underlined if the allocator is not set to LeftAlign
        gui.allocator = RectAllocator.LeftAlign;
        if (gui.BuildLink(LSs.LoadErrorCreateIssue)) {
            Ui.VisitLink(AboutScreen.Github);
        }

        gui.BuildWrappedText(LSs.LoadErrorCreateIssueWithInformation);
    }

    public void Report((string, string) value) => (currentLoad1, currentLoad2) = value;

    private bool FactorioValid(string factorio) => !string.IsNullOrEmpty(factorio) && Directory.Exists(Path.Combine(factorio, "core"));

    private bool ModsValid(string mods) => string.IsNullOrEmpty(mods) || File.Exists(Path.Combine(mods, "mod-list.json"));

    [MemberNotNull(nameof(createText))]
    private void ValidateSelection() {
        bool factorioValid = FactorioValid(dataPath);
        bool modsValid = ModsValid(modsPath);
        bool projectExists = File.Exists(path);

        if (projectExists) {
            createText = LSs.LoadProjectName.L(Path.GetFileNameWithoutExtension(path));
        }
        else if (path != "") {
            string? directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory)) {
                createText = LSs.WelcomeAlertMissingDirectory;
                canCreate = false;
                return;
            }
            else if (Directory.Exists(path)) {
                createText = LSs.WelcomeAlertPathIsDirectory;
                canCreate = false;
                return;
            }
            createText = LSs.WelcomeCreateProjectName.L(Path.GetFileNameWithoutExtension(path));
        }
        else {
            createText = LSs.WelcomeCreateUnnamedProject;
        }

        canCreate = factorioValid && modsValid;
    }

    private void BuildPathSelect(ImGui gui, string path, string description, string placeholder, EditType editType, string? filePlaceholder = null) {
        gui.BuildText(description, TextBlockDisplayStyle.WrappedText);
        gui.spacing = 0.5f;
        using (gui.EnterGroup(default, RectAllocator.RightRow)) {
            if (gui.BuildButton(LSs.WelcomeBrowseButton)) {
                ShowFileSelect(description, path, editType, filePlaceholder);
            }

            if (gui.RemainingRow(0f).BuildTextInput(path, out path, placeholder)) {
                switch (editType) {
                    case EditType.Workspace:
                        this.path = path;
                        break;
                    case EditType.Factorio:
                        dataPath = path;
                        break;
                    case EditType.Mods:
                        modsPath = path;
                        break;
                }
                ValidateSelection();
            }
        }
        gui.spacing = 1.5f;
    }

    /// <summary>
    /// Initializes different input fields with the supplied project definition. <br/>
    /// If the project is null, the fields are cleared. <br/>
    /// If the user is on Windows, it also tries to infer the installation directory of Factorio.
    /// </summary>
    /// <param name="project">A project definition with paths and options. Can be null.</param>
    [MemberNotNull(nameof(createText))]
    private void SetProject(ProjectDefinition? project) {
        if (project != null) {
            dataPath = project.dataPath;
            modsPath = project.modsPath;
            path = project.path;
            expensive = project.expensive;
            netProduction = project.netProduction;
        }
        else {
            dataPath = "";
            modsPath = "";
            path = "";
            netProduction = false;
        }

        if (dataPath == "" && RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            string possibleDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam/steamApps/common/Factorio/data");
            if (FactorioValid(possibleDataPath)) {
                dataPath = possibleDataPath;
            }
        }

        ValidateSelection();
        rootGui.Rebuild();
    }

    private async void LoadProject() {
        try {
            // TODO (Yafc-CE/yafc-ce/issues/249): Why does WelcomeScreen.cs need the internals of ProjectDefinition?
            // Why not take or copy the whole object? The parts are used only in WelcomeScreen.cs, so I see no reason
            // to disassemble ProjectDefinition and drag it piece by piece.
            var (dataPath, modsPath, projectPath) = (this.dataPath, this.modsPath, path);
            Preferences.Instance.AddProject(dataPath, modsPath, projectPath, expensive, netProduction);
            tip = tips.Length > 0 ? tips[DataUtils.random.Next(tips.Length)] : "";

            loading = true;
            rootGui.Rebuild();

            ErrorCollector collector = new ErrorCollector();
            var project = await Task.Run(() => FactorioDataSource.Parse(
                dataPath,
                modsPath,
                projectPath,
                expensive,
                netProduction,
                this,
                collector,
                Preferences.Instance.language,
                Preferences.Instance.useMostRecentSave));

            logger.Information("Opening main screen");
            _ = new MainScreen(displayIndex, project);

            if (collector.severity > ErrorSeverity.None) {
                ErrorListPanel.Show(collector);
            }

            Close();
            GC.Collect();
            logger.Information("GC: {TotalMemory}", GC.GetTotalMemory(false));
        }
        catch (InvalidOperationException ex) when (FactorioDataSource.CurrentLoadingMod == null && (ex.StackTrace?.Contains("MoveNext") ?? false)) {
            definesUpdateState = DefinesUpdateState.WaitingForCopy;
        }
        catch (Exception ex) {
            while (ex.InnerException != null) {
                ex = ex.InnerException;
            }

            errorScroll.RebuildContents();
            errorMod = FactorioDataSource.CurrentLoadingMod;
            if (ex is LuaException lua) {
                errorMessage = lua.Message;
            }
            else {
                errorMessage = ex.Message + "\n" + ex.StackTrace;
            }
        }
        finally {
            loading = false;
            rootGui.Rebuild();
        }
    }

    private Func<string, bool>? GetFolderFilter(EditType type) => type switch {
        EditType.Mods => ModsValid,
        EditType.Factorio => FactorioValid,
        _ => null,
    };

    private async void ShowFileSelect(string description, string path, EditType type, string? placeholder = null) {
        string buttonText;
        string? location, fileExtension;
        FilesystemScreen.Mode fsMode;

        if (type == EditType.Workspace) {
            buttonText = LSs.Select;
            location = Path.GetDirectoryName(path);
            fsMode = FilesystemScreen.Mode.SelectOrCreateFile;
            fileExtension = "yafc";
        }
        else {
            buttonText = LSs.SelectFolder;
            location = path;
            fsMode = FilesystemScreen.Mode.SelectFolder;
            fileExtension = null;
        }

        string? result = await new FilesystemScreen(LSs.SelectFolder, description, buttonText, location, fsMode, "", this, GetFolderFilter(type), fileExtension, placeholder);

        if (result != null) {
            if (type == EditType.Factorio) {
                dataPath = result;
            }
            else if (type == EditType.Mods) {
                modsPath = result;
            }
            else {
                this.path = result;
            }

            Rebuild();
            ValidateSelection();
        }
    }

    private void BuildRecentProjectsDropdown(ImGui gui) => recentProjectScroll.Build(gui);

    private void BuildRecentProjectList(ImGui gui) {
        gui.spacing = 0f;
        foreach (var project in Preferences.Instance.recentProjects) {
            if (string.IsNullOrEmpty(project.path)) {
                continue;
            }

            using (gui.EnterGroup(new Padding(0.5f, 0.25f), RectAllocator.LeftRow)) {
                gui.BuildIcon(Icon.Settings);
                gui.RemainingRow(0.5f).BuildText(project.path);
            }

            if (gui.BuildButton(gui.lastRect, SchemeColor.None, SchemeColor.Grey)) {
                WelcomeScreen owner = (WelcomeScreen)gui.window!; // null-forgiving: gui.window has been set earlier in the render loop.
                owner.SetProject(project);
                _ = gui.CloseDropdown();
            }
        }
    }

    public bool KeyDown(SDL.SDL_Keysym key) {
        if (canCreate && !loading && errorMessage == null
            && key.scancode is SDL.SDL_Scancode.SDL_SCANCODE_RETURN or SDL.SDL_Scancode.SDL_SCANCODE_RETURN2 or SDL.SDL_Scancode.SDL_SCANCODE_KP_ENTER) {

            LoadProject();
            return true;
        }
        if (errorMessage != null && key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE) {
            errorMessage = null;
            Rebuild();
            return true;
        }
        return false;
    }
    public bool TextInput(string input) => false;
    public bool KeyUp(SDL.SDL_Keysym key) => false;
    public void FocusChanged(bool focused) { }
}
