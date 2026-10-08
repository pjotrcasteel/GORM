namespace Gorm.Application.Temporal.Operations.Prediction;

/// <summary>
/// Represents one finite time-series metric observation.
/// </summary>
public sealed class GraphTwinMetricSample
{
    /// <summary>
    /// Gets the sample time.
    /// </summary>
    public required DateTimeOffset At { get; init; }

    /// <summary>
    /// Gets the finite metric value.
    /// </summary>
    public required double Value { get; init; }
}