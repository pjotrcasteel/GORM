using Gorm.Core.Primitives;

namespace Gorm.Core.Loading;

/// <summary>
/// Represents graph related edge result.
/// </summary>
public sealed class GraphRelatedEdgeResult<TEdge, TNode>
    where TEdge : Edge
    where TNode : Node
{
    /// <summary>
    /// Gets or sets the node.
    /// </summary>
    public required TNode Node { get; init; }
    /// <summary>
    /// Gets or sets the edge.
    /// </summary>
    public required TEdge Edge { get; init; }
}