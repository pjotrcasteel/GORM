namespace Gorm.Core.Visualization;

/// <summary>
/// Represents graph document.
/// </summary>
public sealed class GraphDocument
{
    /// <summary>
    /// Gets or sets the root node id.
    /// </summary>
    public string? RootNodeId { get; init; }

    /// <summary>
    /// Gets or sets the nodes.
    /// </summary>
    public required IReadOnlyList<GraphDocumentNode> Nodes { get; init; }

    /// <summary>
    /// Gets or sets the edges.
    /// </summary>
    public required IReadOnlyList<GraphDocumentEdge> Edges { get; init; }

    /// <summary>
    /// Metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Metadata { get; init; } = new Dictionary<string, object?>();
}