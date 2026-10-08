namespace Gorm.Application.Temporal.Operations.Drift;

/// <summary>
/// Configures drift materiality and live synchronization bounds.
/// </summary>
public sealed class GraphTwinDriftOptions
{
    /// <summary>
    /// Gets or sets the maximum entity changes in one comparison.
    /// </summary>
    public int MaximumChanges { get; init; } = 1_000_000;

    /// <summary>
    /// Gets or sets the change ratio at which severity becomes high.
    /// </summary>
    public double HighChangeRatio { get; init; } = 0.10;

    /// <summary>
    /// Gets or sets the change ratio at which severity becomes critical.
    /// </summary>
    public double CriticalChangeRatio { get; init; } = 0.30;

    /// <summary>
    /// Gets or sets the maximum observations consumed by one live synchronizer.
    /// </summary>
    public int MaximumObservations { get; init; } = 1_000_000;
}