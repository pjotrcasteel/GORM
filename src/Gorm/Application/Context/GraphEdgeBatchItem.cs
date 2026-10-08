using Gorm.Core.Primitives;

namespace Gorm.Application.Context;

/// <summary>
/// Represents an edge batch item containing the edge instance and its source and target nodes.
/// </summary>
public sealed class GraphEdgeBatchItem
{
    /// <summary>
    /// Gets or sets the edge to persist.
    /// </summary>
    public required Edge Edge { get; init; }

    /// <summary>
    /// Gets or sets the source node.
    /// </summary>
    public required Node From { get; init; }

    /// <summary>
    /// Gets or sets the target node.
    /// </summary>
    public required Node To { get; init; }

    /// <summary>
    /// Creates an edge batch item.
    /// </summary>
    /// <param name="from">The source node.</param>
    /// <param name="to">The target node.</param>
    /// <param name="edge">The edge to persist.</param>
    /// <returns>The edge batch item.</returns>
    public static GraphEdgeBatchItem Create(Node from, Node to, Edge edge)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(edge);

        return new GraphEdgeBatchItem
        {
            Edge = edge,
            From = from,
            To = to
        };
    }

    /// <summary>
    /// Creates an edge batch item.
    /// </summary>
    /// <typeparam name="TEdge">The edge type.</typeparam>
    /// <typeparam name="TFrom">The source node type.</typeparam>
    /// <typeparam name="TTo">The target node type.</typeparam>
    /// <param name="from">The source node.</param>
    /// <param name="to">The target node.</param>
    /// <param name="edge">The edge to persist.</param>
    /// <returns>The edge batch item.</returns>
    public static GraphEdgeBatchItem Create<TEdge, TFrom, TTo>(TFrom from, TTo to, TEdge edge)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(edge);

        return new GraphEdgeBatchItem
        {
            Edge = edge,
            From = from,
            To = to
        };
    }
}