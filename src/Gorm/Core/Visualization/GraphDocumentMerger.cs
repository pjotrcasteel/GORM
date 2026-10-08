namespace Gorm.Core.Visualization;

/// <summary>
/// Represents graph document merger.
/// </summary>
public static class GraphDocumentMerger
{
    /// <summary>
    /// Executes merge.
    /// </summary>
    /// <param name="documents">The documents.</param>
    /// <returns>The value.</returns>
    public static GraphDocument Merge(params IReadOnlyList<GraphDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var nodes = new Dictionary<string, GraphDocumentNode>(StringComparer.OrdinalIgnoreCase);
        var edges = new Dictionary<string, GraphDocumentEdge>(StringComparer.OrdinalIgnoreCase);
        var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        string? rootNodeId = null;

        foreach (var document in documents.Where(x => x is not null))
        {
            rootNodeId ??= document.RootNodeId;

            foreach (var node in document.Nodes)
            {
                nodes[node.Id] = node;
            }

            foreach (var edge in document.Edges)
            {
                edges[edge.Id] = edge;
            }

            foreach (var pair in document.Metadata)
            {
                metadata[pair.Key] = pair.Value;
            }
        }

        return new GraphDocument
        {
            RootNodeId = rootNodeId ?? nodes.Keys.FirstOrDefault(),
            Nodes = [.. nodes.Values],
            Edges = [.. edges.Values],
            Metadata = metadata
        };
    }
}