using System;
using System.Threading.Tasks;

namespace Yafc.Model;

/// <summary>
/// Runs the shared AutoPlanner capture, compute, and commit business pipeline.
/// </summary>
/// <remarks>
/// Orchestrators compose this pipeline with scheduling delegates so every host uses the same solve phases while
/// varying only where those phases execute.
/// </remarks>
public sealed class AutoPlannerSolvePipeline {
    private readonly Func<Func<AutoPlannerSolveInput>, ValueTask<AutoPlannerSolveInput>> runCapture;
    private readonly Func<AutoPlannerSolveInput, ValueTask<AutoPlannerSolveResult>> compute;
    private readonly Func<Func<string?>, ValueTask<string?>> runCommit;

    /// <summary>
    /// Initializes a new AutoPlanner solve pipeline.
    /// </summary>
    /// <param name="runCapture">Runs the supplied model-owned planner state capture action in the required context.</param>
    /// <param name="scheduleCompute">Schedules the supplied AutoPlanner computation and returns its result.</param>
    /// <param name="runCommit">Runs the supplied model-owned planner state commit action in the required context.</param>
    public AutoPlannerSolvePipeline(
        Func<Func<AutoPlannerSolveInput>, ValueTask<AutoPlannerSolveInput>> runCapture,
        Func<Func<AutoPlannerSolveResult>, ValueTask<AutoPlannerSolveResult>> scheduleCompute,
        Func<Func<string?>, ValueTask<string?>> runCommit) {
        this.runCapture = runCapture ?? throw new ArgumentNullException(nameof(runCapture));
        ArgumentNullException.ThrowIfNull(scheduleCompute);
        compute = input => scheduleCompute(() => AutoPlanner.ComputeSolveResult(input));
        this.runCommit = runCommit ?? throw new ArgumentNullException(nameof(runCommit));
    }

    internal AutoPlannerSolvePipeline(
        Func<Func<AutoPlannerSolveInput>, ValueTask<AutoPlannerSolveInput>> runCapture,
        Func<AutoPlannerSolveInput, ValueTask<AutoPlannerSolveResult>> compute,
        Func<Func<string?>, ValueTask<string?>> runCommit) {
        this.runCapture = runCapture ?? throw new ArgumentNullException(nameof(runCapture));
        this.compute = compute ?? throw new ArgumentNullException(nameof(compute));
        this.runCommit = runCommit ?? throw new ArgumentNullException(nameof(runCommit));
    }

    /// <summary>
    /// Captures solve input, computes a result, and commits it to <paramref name="planner"/>.
    /// </summary>
    /// <param name="planner">The planner to solve.</param>
    /// <returns>A localized error message when solving fails; otherwise, <see langword="null"/>.</returns>
    public Task<string?> SolveAsync(AutoPlanner planner) {
        ArgumentNullException.ThrowIfNull(planner);

        var input = runCapture(planner.CaptureSolveInput);
        if (!input.IsCompletedSuccessfully) {
            return AwaitCaptureAsync(planner, input);
        }

        var result = compute(input.GetAwaiter().GetResult());
        if (!result.IsCompletedSuccessfully) {
            return AwaitComputeAsync(planner, result);
        }

        var commit = runCommit(() => planner.CommitSolveResult(result.GetAwaiter().GetResult()));
        if (!commit.IsCompletedSuccessfully) {
            return commit.AsTask();
        }

        return Task.FromResult(commit.GetAwaiter().GetResult());
    }

    private async Task<string?> AwaitCaptureAsync(
        AutoPlanner planner,
        ValueTask<AutoPlannerSolveInput> capture) {
        var input = await capture.ConfigureAwait(false);
        var result = await compute(input).ConfigureAwait(false);
        return await runCommit(() => planner.CommitSolveResult(result)).ConfigureAwait(false);
    }

    private async Task<string?> AwaitComputeAsync(AutoPlanner planner, ValueTask<AutoPlannerSolveResult> result) {
        var solved = await result.ConfigureAwait(false);
        return await runCommit(() => planner.CommitSolveResult(solved)).ConfigureAwait(false);
    }
}
