using Gorm.Application.Intelligence.Diagnostics;

namespace Gorm.Application.Intelligence.Algorithms.Centrality;

/// <summary>
/// Configures the PageRank algorithm.
/// </summary>
public sealed class GraphPageRankOptions
{
    /// <summary>
    /// Gets or sets the damping factor.
    /// </summary>
    public double DampingFactor { get; set; } = 0.85;

    /// <summary>
    /// Gets or sets the maximum number of iterations.
    /// </summary>
    public int MaximumIterations { get; set; } = 100;

    /// <summary>
    /// Gets or sets the convergence tolerance measured as total score delta.
    /// </summary>
    public double Tolerance { get; set; } = 0.00000001;

    /// <summary>
    /// Gets or sets the maximum number of deterministic node partitions executed concurrently.
    /// </summary>
    public int DegreeOfParallelism { get; set; } = 1;

    /// <summary>
    /// Gets or sets the minimum number of nodes assigned to each deterministic work partition.
    /// </summary>
    public int MinimumNodesPerPartition { get; set; } = 256;

    /// <summary>
    /// Gets or sets an optional deterministic iteration progress sink.
    /// </summary>
    public IProgress<GraphAlgorithmProgress>? Progress { get; set; }

    /// <summary>
    /// Gets or sets the number of PageRank iterations between progress reports.
    /// </summary>
    public int ProgressInterval { get; set; } = 1;
}