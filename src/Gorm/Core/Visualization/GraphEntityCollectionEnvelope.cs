namespace Gorm.Core.Visualization;

/// <summary>
/// Represents graph entity collection envelope.
/// </summary>
public sealed class GraphEntityCollectionEnvelope<TNode, TEdge>
{
    /// <summary>
    /// Gets or sets the nodes.
    /// </summary>
    public required IReadOnlyList<TNode> Nodes { get; init; }

    /// <summary>
    /// Gets or sets the edges.
    /// </summary>
    public required IReadOnlyList<TEdge> Edges { get; init; }

    /// <summary>
    /// Gets or sets the root node id.
    /// </summary>
    public string? RootNodeId { get; init; }
}