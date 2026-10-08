namespace Gorm.Application.Temporal.Simulation.MonteCarlo;

/// <summary>
/// Represents a two-sided confidence interval for a sample mean.
/// </summary>
public sealed class GraphConfidenceInterval
{
    /// <summary>
    /// Gets the configured confidence level.
    /// </summary>
    public required double Level { get; init; }

    /// <summary>
    /// Gets the inclusive lower estimate.
    /// </summary>
    public required double Lower { get; init; }

    /// <summary>
    /// Gets the inclusive upper estimate.
    /// </summary>
    public required double Upper { get; init; }

    /// <summary>
    /// Gets the normal-distribution critical value used.
    /// </summary>
    public required double CriticalValue { get; init; }
}