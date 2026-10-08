using Gorm.Application.Temporal.Operations.Drift;

namespace Gorm.Application.Temporal.Operations.Benchmarking;

/// <summary>
/// Configures a bounded dependency-free Twin operations benchmark.
/// </summary>
public sealed class GraphTwinBenchmarkOptions
{
    /// <summary>
    /// Gets or sets unmeasured warm-up iterations.
    /// </summary>
    public int WarmupIterations { get; init; } = 10;

    /// <summary>
    /// Gets or sets measured iterations.
    /// </summary>
    public int Iterations { get; init; } = 1_000;

    /// <summary>
    /// Gets or sets the hard iteration limit.
    /// </summary>
    public int MaximumIterations { get; init; } = 1_000_000;

    /// <summary>
    /// Gets or sets drift comparison options.
    /// </summary>
    public GraphTwinDriftOptions DriftOptions { get; init; } = new();
}