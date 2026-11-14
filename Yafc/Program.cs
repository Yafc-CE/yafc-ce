using System;
using System.IO;
using Serilog;
using Yafc.Core;
using Yafc.I18n;
using Yafc.Model;
using Yafc.Parser;
using Yafc.UI;

namespace Yafc;

public static class Program {
    private static readonly ILogger logger = Logging.GetLogger(typeof(Program));
    internal static bool hasOverriddenFont { get; private set; }

    private static void Main(string[] args) {
        YafcLib.RegisterDefaultAnalysis();

        // Wire up the UI-aware undo batch scheduler so that undo batches are committed on gesture-finish
        // (mouse-up) rather than immediately. This must be set before any Project is loaded or created,
        // including any that Ui.Start() might trigger during initialization.
        UndoSystem.DefaultScheduler = new GestureFinishUndoBatchScheduler();

        try {
            Ui.Start();
        }
        catch (Exception ex) {
            logger.Fatal(ex, "Failed to initialize the UI");
            throw;
        }

        // This must happen before Preferences.Instance, where we load the prefs file and the requested translation.
        FactorioDataSource.LoadYafcLocale("en");

        ProjectDefinition? cliProject = CommandLineParser.ParseArgs(args);

        if (CommandLineParser.errorOccured || CommandLineParser.helpRequested) {
            Console.WriteLine(LSs.YafcWithVersion.L(YafcLib.version.ToString(3)));
            Console.WriteLine();

            if (CommandLineParser.errorOccured) {
                Console.WriteLine(LSs.CommandLineError.L(CommandLineParser.lastError));
                Console.WriteLine();
                Environment.ExitCode = 1;
            }

            CommandLineParser.PrintHelp();
        }
        else {
            _ = new WelcomeScreen(cliProject);
            Ui.MainLoop();
        }
    }
}
