using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Pathfinding;

/// <summary>
/// Represents a complete route excluded from the selected Pareto results.
/// </summary>
public sealed class GraphRejectedRoute
{
    /// <summary>
    /// Gets ordered route nodes.
    /// </summary>
    public required IReadOnlyList<Node> Nodes { get; init; }

    /// <summary>
    /// Gets ordered route edges.
    /// </summary>
    public required IReadOnlyList<Edge> Edges { get; init; }

    /// <summary>
    /// Gets accumulated raw metric values.
    /// </summary>
    public required IReadOnlyDictionary<string, double> Metrics { get; init; }

    /// <summary>
    /// Gets the explicit reason the alternative was rejected.
    /// </summary>
    public required string Reason { get; init; }
}