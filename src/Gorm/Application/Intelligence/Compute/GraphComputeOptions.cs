using Gorm.Application.Intelligence.Diagnostics;

namespace Gorm.Application.Intelligence.Compute;

/// <summary>
/// Configures vertex-centric graph computation.
/// </summary>
public sealed class GraphComputeOptions
{
    /// <summary>
    /// Gets or sets the maximum number of synchronized compute supersteps.
    /// </summary>
    public int MaximumSupersteps { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum number of messages created in a single superstep.
    /// </summary>
    public int MaximumMessagesPerSuperstep { get; set; } = 1_000_000;

    /// <summary>
    /// Gets or sets the maximum number of nodes computed concurrently. The default value 1 preserves sequential execution.
    /// </summary>
    public int DegreeOfParallelism { get; set; } = 1;

    /// <summary>
    /// Gets or sets the minimum number of nodes in each deterministic compute partition.
    /// </summary>
    public int MinimumNodesPerPartition { get; set; } = 64;

    /// <summary>
    /// Gets or sets an optional deterministic superstep progress sink.
    /// </summary>
    public IProgress<GraphAlgorithmProgress>? Progress { get; set; }

    /// <summary>
    /// Gets or sets the number of supersteps between progress reports.
    /// </summary>
    public int ProgressInterval { get; set; } = 1;
}