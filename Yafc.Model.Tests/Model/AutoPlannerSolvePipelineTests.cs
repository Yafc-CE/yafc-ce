using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Yafc.Model.Tests.Model;

[Collection("LuaDependentTests")]
public class AutoPlannerSolvePipelineTests {
    [Fact]
    public async Task SolveAsync_UsesSharedCaptureComputeCommitOrder() {
        var planner = CreatePlanner();
        var goal = Database.items.all.First();
        planner.goals.Add(new AutoPlannerGoal { item = goal, amount = 3f });
        var tiers = CreateTiers();
        List<string> events = [];

        var pipeline = new AutoPlannerSolvePipeline(
            capture => {
                events.Add("run-capture");
                var input = capture();
                events.Add("captured");
                return ValueTask.FromResult(input);
            },
            input => {
                events.Add("compute");
                var capturedGoal = Assert.Single(input.goals);
                Assert.Same(goal, capturedGoal.item);
                Assert.Equal(3f, capturedGoal.amount);
                Assert.Null(planner.tiers);
                return ValueTask.FromResult(AutoPlannerSolveResult.Success(tiers));
            },
            commit => {
                events.Add("run-commit");
                Assert.Null(planner.tiers);
                var error = commit();
                events.Add("committed");
                return ValueTask.FromResult(error);
            });

        var solveTask = pipeline.SolveAsync(planner);

        Assert.True(solveTask.IsCompleted);
        Assert.Null(await solveTask);
        Assert.Equal(["run-capture", "captured", "compute", "run-commit", "committed"], events);
        Assert.Same(tiers, planner.tiers);
    }

    [Fact]
    public async Task SolveAsync_AwaitsComputeBeforeRunningCommit() {
        var planner = CreatePlanner();
        var tiers = CreateTiers();
        var resultSource = new TaskCompletionSource<AutoPlannerSolveResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> events = [];

        var pipeline = new AutoPlannerSolvePipeline(
            capture => {
                events.Add("capture");
                return ValueTask.FromResult(capture());
            },
            (AutoPlannerSolveInput _) => {
                events.Add("compute");
                return new ValueTask<AutoPlannerSolveResult>(resultSource.Task);
            },
            commit => {
                events.Add("commit");
                return ValueTask.FromResult(commit());
            });

        var solveTask = pipeline.SolveAsync(planner);

        Assert.False(solveTask.IsCompleted);
        Assert.Equal(["capture", "compute"], events);

        resultSource.SetResult(AutoPlannerSolveResult.Success(tiers));

        Assert.Null(await solveTask);
        Assert.Equal(["capture", "compute", "commit"], events);
        Assert.Same(tiers, planner.tiers);
    }

    [Fact]
    public async Task SolveAsync_RunsCaptureAndCommitInsideScheduledActions() {
        var planner = CreatePlanner();
        var goal = Database.items.all.First();
        planner.goals.Add(new AutoPlannerGoal { item = goal, amount = 1f });
        var tiers = CreateTiers();
        var scheduler = new GuardedActionScheduler();
        List<string> events = [];

        var pipeline = new AutoPlannerSolvePipeline(
            capture => scheduler.RunAsync(() => {
                scheduler.ThrowIfNotInsideScheduledAction();
                events.Add("capture");
                planner.goals[0].amount = 2f;
                return capture();
            }),
            input => {
                events.Add("compute");
                var capturedGoal = Assert.Single(input.goals);
                Assert.Same(goal, capturedGoal.item);
                Assert.Equal(2f, capturedGoal.amount);
                return ValueTask.FromResult(AutoPlannerSolveResult.Success(tiers));
            },
            commit => scheduler.RunAsync(() => {
                scheduler.ThrowIfNotInsideScheduledAction();
                events.Add("commit");
                return commit();
            }));

        Assert.Null(await pipeline.SolveAsync(planner));

        Assert.Equal(["capture", "compute", "commit"], events);
        Assert.Equal(2, scheduler.calls);
        Assert.Same(tiers, planner.tiers);
    }

    private static AutoPlanner CreatePlanner() {
        var project = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.BareMinimumLuaData.lua");
        var page = new ProjectPage(project, typeof(AutoPlanner));

        return Assert.IsType<AutoPlanner>(page.content);
    }

    private static AutoPlannerRecipe[][] CreateTiers() => [[new AutoPlannerRecipe {
        tier = 0,
        recipesPerSecond = 1f,
        downstream = [],
        upstream = []
    }]];

    private sealed class GuardedActionScheduler {
        private bool insideScheduledAction;

        public int calls { get; private set; }

        public ValueTask<T> RunAsync<T>(Func<T> action) {
            calls++;
            var source = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task.Run(() => {
                insideScheduledAction = true;
                try {
                    source.SetResult(action());
                }
                catch (Exception ex) {
                    source.SetException(ex);
                }
                finally {
                    insideScheduledAction = false;
                }
            });

            return new ValueTask<T>(source.Task);
        }

        public void ThrowIfNotInsideScheduledAction() {
            if (!insideScheduledAction) {
                throw new InvalidOperationException("Action did not run inside the scheduled context.");
            }
        }
    }
}
