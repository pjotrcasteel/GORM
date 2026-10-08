namespace Gorm.Application.Temporal.Operations.Prediction;

/// <summary>
/// Bounds deterministic linear threshold prediction.
/// </summary>
public sealed class GraphThresholdPredictionOptions
{
    /// <summary>
    /// Gets or sets the maximum number of samples.
    /// </summary>
    public int MaximumSamples { get; init; } = 100_000;

    /// <summary>
    /// Gets or sets the maximum accepted prediction horizon.
    /// </summary>
    public TimeSpan MaximumHorizon { get; init; } = TimeSpan.FromDays(3_650);
}