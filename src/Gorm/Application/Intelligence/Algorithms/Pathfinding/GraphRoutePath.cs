using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Pathfinding;

/// <summary>
/// Represents an ordered costed route through a graph projection.
/// </summary>
public sealed class GraphRoutePath
{
    /// <summary>
    /// Gets ordered nodes from source through destination.
    /// </summary>
    public required IReadOnlyList<Node> Nodes { get; init; }

    /// <summary>
    /// Gets ordered traversed edges.
    /// </summary>
    public required IReadOnlyList<Edge> Edges { get; init; }

    /// <summary>
    /// Gets total additive route cost.
    /// </summary>
    public required double TotalCost { get; init; }

    /// <summary>
    /// Gets the number of traversed edges.
    /// </summary>
    public int HopCount => Edges.Count;

    /// <summary>
    /// Gets a human-readable explanation of why this route was returned.
    /// </summary>
    public required string Explanation { get; init; }
}