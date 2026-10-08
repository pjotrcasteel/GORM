using Gorm.Core.Primitives;

namespace Gorm.Application.History.Querying;

/// <summary>
/// Represents a temporal traversal result containing both the related node and edge snapshot.
/// </summary>
/// <typeparam name="TEdge">The edge type.</typeparam>
/// <typeparam name="TNode">The node type.</typeparam>
public sealed class GraphTemporalTraversalResult<TEdge, TNode>
    where TEdge : Edge
    where TNode : Node
{
    /// <summary>
    /// Gets or sets the related node snapshot.
    /// </summary>
    public required TNode Node { get; init; }

    /// <summary>
    /// Gets or sets the edge snapshot.
    /// </summary>
    public required TEdge Edge { get; init; }
}