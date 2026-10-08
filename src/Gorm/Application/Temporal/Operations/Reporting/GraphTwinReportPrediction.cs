using Gorm.Application.Temporal.Operations.Prediction;

namespace Gorm.Application.Temporal.Operations.Reporting;

/// <summary>
/// Serializable predicted-threshold evidence.
/// </summary>
public sealed class GraphTwinReportPrediction
{
    /// <summary>
    /// Gets threshold.
    /// </summary>
    public required double Threshold { get; init; }

    /// <summary>
    /// Gets direction.
    /// </summary>
    public required GraphThresholdDirection Direction { get; init; }

    /// <summary>
    /// Gets trend slope per second.
    /// </summary>
    public required double SlopePerSecond { get; init; }

    /// <summary>
    /// Gets fit R-squared.
    /// </summary>
    public required double RSquared { get; init; }

    /// <summary>
    /// Gets whether threshold is already breached.
    /// </summary>
    public required bool AlreadyBreached { get; init; }

    /// <summary>
    /// Gets predicted crossing time.
    /// </summary>
    public DateTimeOffset? PredictedBreachAt { get; init; }

    /// <summary>
    /// Gets whether breach is inside horizon.
    /// </summary>
    public required bool WithinHorizon { get; init; }

    /// <summary>
    /// Gets explanation.
    /// </summary>
    public required string Explanation { get; init; }
}