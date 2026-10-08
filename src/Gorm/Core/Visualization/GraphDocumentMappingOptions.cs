namespace Gorm.Core.Visualization;

public sealed class GraphDocumentMappingOptions<TNode, TEdge>
{
    public Func<TNode, string>? NodeId { get; init; }

    public Func<TNode, string>? NodeLabel { get; init; }

    public Func<TNode, string>? NodeType { get; init; }

    public Func<TEdge, string>? EdgeId { get; init; }

    public Func<TEdge, string>? EdgeFrom { get; init; }

    public Func<TEdge, string>? EdgeTo { get; init; }

    public Func<TEdge, string>? EdgeType { get; init; }

    public Func<TEdge, string?>? EdgeLabel { get; init; }
}