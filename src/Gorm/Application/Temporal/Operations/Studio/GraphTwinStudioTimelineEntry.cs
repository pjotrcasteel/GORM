using Gorm.Application.Temporal.Operations.Drift;

namespace Gorm.Application.Temporal.Operations.Studio;

/// <summary>
/// Desktop timeline row produced without a web dependency.
/// </summary>
public sealed class GraphTwinStudioTimelineEntry
{
    /// <summary>
    /// Gets observation sequence.
    /// </summary>
    public required long Sequence { get; init; }

    /// <summary>
    /// Gets observation time.
    /// </summary>
    public required DateTimeOffset ObservedAt { get; init; }

    /// <summary>
    /// Gets drift severity.
    /// </summary>
    public required GraphTwinDriftSeverity Severity { get; init; }

    /// <summary>
    /// Gets added count.
    /// </summary>
    public required int Added { get; init; }

    /// <summary>
    /// Gets removed count.
    /// </summary>
    public required int Removed { get; init; }

    /// <summary>
    /// Gets modified count.
    /// </summary>
    public required int Modified { get; init; }

    /// <summary>
    /// Gets triggered warning count.
    /// </summary>
    public required int WarningCount { get; init; }
}