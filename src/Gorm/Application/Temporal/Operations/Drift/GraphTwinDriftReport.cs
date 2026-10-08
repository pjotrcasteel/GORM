using Gorm.Application.Temporal.Diff;
using Gorm.Application.Temporal.Snapshots;

namespace Gorm.Application.Temporal.Operations.Drift;

/// <summary>
/// Contains exact drift evidence between the expected twin and observed world.
/// </summary>
public sealed class GraphTwinDriftReport
{
    internal GraphTwinDriftReport(
        GraphWorldSnapshotIdentity expected,
        GraphWorldSnapshotIdentity observed,
        GraphWorldSnapshotDiff difference,
        double entityChangeRatio,
        GraphTwinDriftSeverity severity)
    {
        Expected = expected;
        Observed = observed;
        Difference = difference;
        EntityChangeRatio = entityChangeRatio;
        Severity = severity;
        Explanation = difference.Changes.Count == 0
            ? $"Observed world '{observed.SnapshotId}' matches expected twin '{expected.SnapshotId}'."
            : $"Observed world has {difference.Changes.Count} drifted entities ({entityChangeRatio:P2}); severity is {severity}.";
    }

    /// <summary>
    /// Gets expected twin identity.
    /// </summary>
    public GraphWorldSnapshotIdentity Expected { get; }

    /// <summary>
    /// Gets observed world identity.
    /// </summary>
    public GraphWorldSnapshotIdentity Observed { get; }

    /// <summary>
    /// Gets deterministic entity-level differences.
    /// </summary>
    public GraphWorldSnapshotDiff Difference { get; }

    /// <summary>
    /// Gets changed entities divided by the larger world size.
    /// </summary>
    public double EntityChangeRatio { get; }

    /// <summary>
    /// Gets materiality classification.
    /// </summary>
    public GraphTwinDriftSeverity Severity { get; }

    /// <summary>
    /// Gets whether any observed state differs.
    /// </summary>
    public bool HasDrift => Difference.Changes.Count > 0;

    /// <summary>
    /// Gets a deterministic summary.
    /// </summary>
    public string Explanation { get; }
}