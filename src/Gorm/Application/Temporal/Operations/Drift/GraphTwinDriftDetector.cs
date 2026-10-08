using Gorm.Application.Temporal.Diff;
using Gorm.Application.Temporal.Snapshots;

namespace Gorm.Application.Temporal.Operations.Drift;

/// <summary>
/// Compares an expected immutable twin with an independently observed immutable world.
/// </summary>
public static class GraphTwinDriftDetector
{
    /// <summary>
    /// Produces exact entity evidence and configured materiality without writing either world.
    /// </summary>
    public static GraphTwinDriftReport Compare(
        GraphWorldSnapshot expected,
        GraphWorldSnapshot observed,
        GraphTwinDriftOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(observed);
        options ??= new GraphTwinDriftOptions();
        ValidateOptions(options);
        var difference = GraphWorldSnapshotDiffer.Compare(
            expected,
            observed,
            new GraphWorldDiffOptions { MaximumChanges = options.MaximumChanges },
            cancellationToken);
        var worldSize = Math.Max(1, Math.Max(expected.Nodes.Count + expected.Edges.Count, observed.Nodes.Count + observed.Edges.Count));
        var ratio = (double)difference.Changes.Count / worldSize;
        GraphTwinDriftSeverity severity;
        if (difference.Changes.Count == 0)
        {
            severity = GraphTwinDriftSeverity.None;
        }
        else if (ratio >= options.CriticalChangeRatio)
        {
            severity = GraphTwinDriftSeverity.Critical;
        }
        else if (difference.TopologyChanged || ratio >= options.HighChangeRatio)
        {
            severity = GraphTwinDriftSeverity.High;
        }
        else
        {
            severity = GraphTwinDriftSeverity.Low;
        }
        return new GraphTwinDriftReport(expected.Identity, observed.Identity, difference, ratio, severity);
    }

    internal static void ValidateOptions(GraphTwinDriftOptions options)
    {
        if (options.MaximumChanges <= 0 || options.MaximumObservations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Drift limits must be positive.");
        }

        if (!double.IsFinite(options.HighChangeRatio) ||
            !double.IsFinite(options.CriticalChangeRatio) ||
            options.HighChangeRatio <= 0 ||
            options.HighChangeRatio > 1 ||
            options.CriticalChangeRatio <= options.HighChangeRatio ||
            options.CriticalChangeRatio > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Drift ratios must satisfy 0 < HighChangeRatio < CriticalChangeRatio <= 1.");
        }
    }
}