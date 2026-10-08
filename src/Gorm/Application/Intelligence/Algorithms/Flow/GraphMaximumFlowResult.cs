using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Flow;

/// <summary>
/// Represents maximum flow, minimum cut and edge bottlenecks.
/// </summary>
public sealed class GraphMaximumFlowResult
{
    /// <summary>
    /// Gets maximum source-to-destination flow.
    /// </summary>
    public required double MaximumFlow { get; init; }

    /// <summary>
    /// Gets the capacity of the returned minimum cut.
    /// </summary>
    public required double MinimumCutCapacity { get; init; }

    /// <summary>
    /// Gets flow information for every selected edge.
    /// </summary>
    public required IReadOnlyList<GraphEdgeFlow> EdgeFlows { get; init; }

    /// <summary>
    /// Gets nodes reachable from the source in the final residual graph.
    /// </summary>
    public required IReadOnlyList<Node> SourceSideNodes { get; init; }

    /// <summary>
    /// Gets nodes on the destination side of the minimum cut.
    /// </summary>
    public required IReadOnlyList<Node> DestinationSideNodes { get; init; }

    /// <summary>
    /// Gets selected directed edges crossing the minimum cut.
    /// </summary>
    public required IReadOnlyList<GraphEdgeFlow> CutEdges { get; init; }

    /// <summary>
    /// Gets saturated positive-flow edges ordered by utilization and capacity.
    /// </summary>
    public required IReadOnlyList<GraphEdgeFlow> BottleneckEdges { get; init; }

    /// <summary>
    /// Gets the number of augmenting paths used.
    /// </summary>
    public required int Augmentations { get; init; }

    /// <summary>
    /// Gets an explanation of flow, cut and bottleneck meaning.
    /// </summary>
    public required string Explanation { get; init; }
}