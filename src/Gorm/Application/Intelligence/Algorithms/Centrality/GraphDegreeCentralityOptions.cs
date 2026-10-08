using Gorm.Application.Intelligence.Diagnostics;

namespace Gorm.Application.Intelligence.Algorithms.Centrality;

/// <summary>
/// Configures degree centrality execution.
/// </summary>
public sealed class GraphDegreeCentralityOptions
{
    /// <summary>
    /// Gets or sets the maximum number of deterministic work partitions executed concurrently.
    /// </summary>
    public int DegreeOfParallelism { get; set; } = 1;

    /// <summary>
    /// Gets or sets the minimum number of nodes assigned to each work partition.
    /// </summary>
    public int MinimumNodesPerPartition { get; set; } = 256;

    /// <summary>
    /// Gets or sets an optional completion progress sink.
    /// </summary>
    public IProgress<GraphAlgorithmProgress>? Progress { get; set; }
}