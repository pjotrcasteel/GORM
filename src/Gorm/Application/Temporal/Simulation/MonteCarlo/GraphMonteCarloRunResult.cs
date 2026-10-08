namespace Gorm.Application.Temporal.Simulation.MonteCarlo;

/// <summary>
/// Contains the reproducible seed and scalar outcome of one run.
/// </summary>
public sealed class GraphMonteCarloRunResult
{
    /// <summary>
    /// Gets the zero-based run index.
    /// </summary>
    public required int RunIndex { get; init; }

    /// <summary>
    /// Gets the run seed.
    /// </summary>
    public required ulong Seed { get; init; }

    /// <summary>
    /// Gets the finite simulation outcome.
    /// </summary>
    public required double Value { get; init; }
}