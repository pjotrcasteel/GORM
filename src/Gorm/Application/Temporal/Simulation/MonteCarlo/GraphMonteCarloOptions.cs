namespace Gorm.Application.Temporal.Simulation.MonteCarlo;

/// <summary>
/// Configures a bounded reproducible Monte Carlo experiment.
/// </summary>
public sealed class GraphMonteCarloOptions
{
    /// <summary>
    /// Gets or sets the number of independent runs.
    /// </summary>
    public int Runs { get; init; } = 1_000;

    /// <summary>
    /// Gets or sets the explicit experiment seed.
    /// </summary>
    public ulong Seed { get; init; }

    /// <summary>
    /// Gets or sets the two-sided normal-approximation confidence level.
    /// </summary>
    public double ConfidenceLevel { get; init; } = 0.95;

    /// <summary>
    /// Gets or sets the hard run safety limit.
    /// </summary>
    public int MaximumRuns { get; init; } = 1_000_000;
}