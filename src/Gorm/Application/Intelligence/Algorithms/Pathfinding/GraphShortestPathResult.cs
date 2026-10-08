using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Pathfinding;

/// <summary>
/// Represents a shortest path through a graph projection.
/// </summary>
public sealed class GraphShortestPathResult
{
    /// <summary>
    /// Gets path nodes ordered from the requested start node to the destination node.
    /// </summary>
    public required IReadOnlyList<Node> Nodes { get; init; }

    /// <summary>
    /// Gets path edges in traversal order.
    /// </summary>
    public required IReadOnlyList<Edge> Edges { get; init; }

    /// <summary>
    /// Gets the sum of edge weights.
    /// </summary>
    public required double TotalWeight { get; init; }

    /// <summary>
    /// Gets the number of traversed edges.
    /// </summary>
    public int HopCount => Edges.Count;
}