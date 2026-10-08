using Gorm.Application.Intelligence.Diagnostics;

namespace Gorm.Application.Intelligence.Benchmarking;

/// <summary>
/// Configures reproducible in-process intelligence benchmarks without an external benchmark package.
/// </summary>
public sealed class GraphIntelligenceBenchmarkOptions
{
    /// <summary>
    /// Gets or sets warmup executions per operation.
    /// </summary>
    public int WarmupIterations { get; set; } = 1;

    /// <summary>
    /// Gets or sets measured executions per operation.
    /// </summary>
    public int MeasuredIterations { get; set; } = 5;

    /// <summary>
    /// Gets or sets PageRank iterations used by the baseline.
    /// </summary>
    public int PageRankIterations { get; set; } = 20;

    /// <summary>
    /// Gets or sets an optional operation-level progress sink.
    /// </summary>
    public IProgress<GraphAlgorithmProgress>? Progress { get; set; }
}