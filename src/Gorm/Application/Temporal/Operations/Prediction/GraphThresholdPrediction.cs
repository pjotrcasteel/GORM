namespace Gorm.Application.Temporal.Operations.Prediction;

/// <summary>
/// Contains trend, fit and predicted breach evidence.
/// </summary>
public sealed class GraphThresholdPrediction
{
    /// <summary>
    /// Gets the sample count.
    /// </summary>
    public required int SampleCount { get; init; }

    /// <summary>
    /// Gets the fitted value change per second.
    /// </summary>
    public required double SlopePerSecond { get; init; }

    /// <summary>
    /// Gets the coefficient of determination from zero through one.
    /// </summary>
    public required double RSquared { get; init; }

    /// <summary>
    /// Gets the requested threshold.
    /// </summary>
    public required double Threshold { get; init; }

    /// <summary>
    /// Gets breach direction.
    /// </summary>
    public required GraphThresholdDirection Direction { get; init; }

    /// <summary>
    /// Gets whether the latest actual sample already breaches the threshold.
    /// </summary>
    public required bool AlreadyBreached { get; init; }

    /// <summary>
    /// Gets the predicted crossing instant when trend points toward a future breach.
    /// </summary>
    public DateTimeOffset? PredictedBreachAt { get; init; }

    /// <summary>
    /// Gets whether crossing is actual/current or falls inside the requested future horizon.
    /// </summary>
    public required bool WithinHorizon { get; init; }

    /// <summary>
    /// Gets explicit linear-model evidence.
    /// </summary>
    public required string Explanation { get; init; }
}