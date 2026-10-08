using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Connectivity;

/// <summary>
/// Represents one simple directed cycle.
/// </summary>
public sealed class GraphCycle
{
    /// <summary>
    /// Gets the deterministic zero-based cycle identifier within the result.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// Gets the ordered nodes. The first node is not repeated at the end.
    /// </summary>
    public required IReadOnlyList<Node> Nodes { get; init; }

    /// <summary>
    /// Gets the ordered edges, including the edge returning to the first node.
    /// </summary>
    public required IReadOnlyList<Edge> Edges { get; init; }

    /// <summary>
    /// Gets the number of edges in the cycle.
    /// </summary>
    public int Length => Edges.Count;
}