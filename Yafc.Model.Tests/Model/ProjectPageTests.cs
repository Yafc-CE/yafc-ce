using System;
using System.Threading.Tasks;
using Yafc.I18n;
using Xunit;

namespace Yafc.Model.Tests;

public class ProjectPageTests {
    [Fact]
    public async Task AutoPlannerSolve_DelegatesToProjectOrchestrator() {
        Project project = new();
        RecordingAutoPlannerSolveOrchestrator orchestrator = new("planned");
        project.autoPlannerSolveOrchestrator = orchestrator;
        ProjectPage page = new(project, typeof(AutoPlanner));
        var planner = Assert.IsType<AutoPlanner>(page.content);

        var error = await planner.Solve(page);

        Assert.Equal("planned", error);
        Assert.Equal(1, orchestrator.calls);
        Assert.Same(page, orchestrator.page);
        Assert.Same(planner, orchestrator.planner);
    }

    [Fact]
    public async Task RunSolveJob_AutoPlannerOrchestratorError_SetsModelErrorAndNotifiesContentChanged() {
        Project project = new();
        project.autoPlannerSolveOrchestrator = new RecordingAutoPlannerSolveOrchestrator("solve failed");
        ProjectPage page = new(project, typeof(AutoPlanner));
        int changeCount = 0;
        bool? visualOnly = null;
        page.contentChanged += value => {
            changeCount++;
            visualOnly = value;
        };

        await page.RunSolveJob();

        Assert.Equal("solve failed", page.modelError);
        Assert.Equal(1, changeCount);
        Assert.False(visualOnly);
    }

    [Fact]
    public void CommitSolveResult_NoSolution_ClearsTiersAndReturnsNoSolutionMessage() {
        ProjectPage page = new(new Project(), typeof(AutoPlanner));
        var planner = Assert.IsType<AutoPlanner>(page.content);
        planner.CommitSolveResult(AutoPlannerSolveResult.Success(CreateTiers()));

        var error = planner.CommitSolveResult(AutoPlannerSolveResult.NoSolution);

        Assert.Equal(LSs.AutoPlannerNoSolution, error);
        Assert.Null(planner.tiers);
    }

    [Fact]
    public void CommitSolveResult_Success_SetsTiersAndReturnsNull() {
        ProjectPage page = new(new Project(), typeof(AutoPlanner));
        var planner = Assert.IsType<AutoPlanner>(page.content);
        var tiers = CreateTiers();

        var error = planner.CommitSolveResult(AutoPlannerSolveResult.Success(tiers));

        Assert.Null(error);
        Assert.Same(tiers, planner.tiers);
    }

    [Fact]
    public void StagedSolveResult_DoesNotMutateTiersBeforeCommit() {
        ProjectPage page = new(new Project(), typeof(AutoPlanner));
        var planner = Assert.IsType<AutoPlanner>(page.content);
        var originalTiers = CreateTiers();
        var replacementTiers = CreateTiers();
        planner.CommitSolveResult(AutoPlannerSolveResult.Success(originalTiers));
        var stagedResult = AutoPlannerSolveResult.Success(replacementTiers);

        Assert.Same(originalTiers, planner.tiers);

        planner.CommitSolveResult(stagedResult);

        Assert.Same(replacementTiers, planner.tiers);
    }

    [Fact]
    public async Task ExternalSolve_DefaultThreadSwitcher_CompletesWithoutUiInitialization() {
        ProjectPage page = new(new Project(), typeof(Summary));

        var error = await page.ExternalSolve();

        Assert.Null(error);
        Assert.False(page.IsSolutionStale());
    }

    [Fact]
    public async Task ExternalSolve_UsesProjectThreadSwitcher() {
        Project project = new();
        CountingModelThreadSwitcher switcher = new();
        project.modelThreadSwitcher = switcher;
        ProjectPage page = new(project, typeof(Summary));

        _ = await page.ExternalSolve();

        Assert.Equal(0, switcher.backgroundSwitches);
        Assert.Equal(2, switcher.foregroundSwitches);
    }

    [Fact]
    public async Task ExternalSolve_ForegroundContinuationRunsInsideSwitcherDispatch() {
        Project project = new();
        GuardedModelThreadSwitcher switcher = new();
        project.modelThreadSwitcher = switcher;
        ProjectPage page = new(project, typeof(Summary));

        _ = await page.ExternalSolve();

        Assert.Equal(2, switcher.foregroundSwitches);
        Assert.False(page.IsSolutionStale());
    }

    private static AutoPlannerRecipe[][] CreateTiers() => [[new AutoPlannerRecipe {
        tier = 0,
        recipesPerSecond = 1f,
        downstream = [],
        upstream = []
    }]];

    private sealed class RecordingAutoPlannerSolveOrchestrator(string result) : IAutoPlannerSolveOrchestrator {
        public int calls { get; private set; }
        public ProjectPage page { get; private set; }
        public AutoPlanner planner { get; private set; }

        public Task<string> SolveAsync(ProjectPage page, AutoPlanner planner) {
            calls++;
            this.page = page;
            this.planner = planner;
            return Task.FromResult(result);
        }
    }

    private sealed class GuardedModelThreadSwitcher : IModelThreadSwitcher {
        private bool inForegroundDispatch;

        public int foregroundSwitches { get; private set; }

        public ModelThreadSwitch SwitchToBackground() => default;

        public ModelThreadSwitch SwitchToForeground() {
            foregroundSwitches++;
            return new ModelThreadSwitch(new GuardedAwaitable(this));
        }

        private sealed class GuardedAwaitable(GuardedModelThreadSwitcher owner) : IModelThreadSwitchAwaitable, IModelThreadSwitchAwaiter {
            public IModelThreadSwitchAwaiter GetAwaiter() => this;
            public bool IsCompleted => false;

            public void GetResult() {
                if (!owner.inForegroundDispatch) {
                    throw new InvalidOperationException("Continuation did not run inside the foreground dispatch.");
                }
            }

            public void OnCompleted(Action continuation) {
                owner.inForegroundDispatch = true;
                try {
                    continuation();
                }
                finally {
                    owner.inForegroundDispatch = false;
                }
            }
        }
    }
}
