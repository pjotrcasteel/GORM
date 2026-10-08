namespace Gorm.Application.Temporal.Simulation.MonteCarlo;

/// <summary>
/// Provides deterministic random input isolated to one Monte Carlo run.
/// </summary>
public sealed class GraphMonteCarloRunContext
{
    private readonly GraphDeterministicRandom _random;

    internal GraphMonteCarloRunContext(int runIndex, ulong seed)
    {
        RunIndex = runIndex;
        Seed = seed;
        _random = new GraphDeterministicRandom(seed);
    }

    /// <summary>
    /// Gets the zero-based run index.
    /// </summary>
    public int RunIndex { get; }

    /// <summary>
    /// Gets the derived seed unique to this run.
    /// </summary>
    public ulong Seed { get; }

    /// <summary>
    /// Returns a reproducible value greater than or equal to zero and less than one.
    /// </summary>
    public double NextDouble() => _random.NextDouble();

    /// <summary>
    /// Returns a reproducible integer greater than or equal to zero and less than <paramref name="exclusiveMaximum"/>.
    /// </summary>
    public int NextInt32(int exclusiveMaximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveMaximum);

        return (int)(_random.NextUInt64() % (uint)exclusiveMaximum);
    }

    /// <summary>
    /// Returns a reproducible Bernoulli outcome with the supplied probability.
    /// </summary>
    public bool NextBoolean(double probability)
    {
        if (!double.IsFinite(probability) || probability < 0 || probability > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(probability));
        }

        return probability >= 1 || (probability > 0 && NextDouble() < probability);
    }
}