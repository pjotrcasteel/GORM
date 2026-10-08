using System.Reflection;

namespace Gorm.Core.Visualization;

/// <summary>
/// Represents graph document builder.
/// </summary>
public static class GraphDocumentBuilder
{
    /// <summary>
    /// Executes from envelope.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <param name="envelope">The envelope.</param>
    /// <param name="nodeId">The node id.</param>
    /// <param name="nodeLabel">The node label.</param>
    /// <param name="nodeType">The node type.</param>
    /// <param name="edgeId">The edge id.</param>
    /// <param name="edgeFrom">The edge from.</param>
    /// <param name="edgeTo">The edge to.</param>
    /// <param name="edgeType">The edge type.</param>
    /// <param name="edgeLabel">The edge label.</param>
    /// <returns>The value.</returns>
    public static GraphDocument FromEnvelope<TNode, TEdge>(GraphEntityCollectionEnvelope<TNode, TEdge> envelope, GraphDocumentMappingOptions<TNode, TEdge>? options = null)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        options ??= new GraphDocumentMappingOptions<TNode, TEdge>();

        var nodeId = options.NodeId;
        var nodeLabel = options.NodeLabel;
        var nodeType = options.NodeType;
        var edgeId = options.EdgeId;
        var edgeFrom = options.EdgeFrom;
        var edgeTo = options.EdgeTo;
        var edgeType = options.EdgeType;
        var edgeLabel = options.EdgeLabel;

        nodeId ??= x => ReadStringProperty(x, "Id");
        nodeLabel ??= x => TryReadStringProperty(x, "Name") ?? nodeId(x);
        nodeType ??= x => typeof(TNode).Name;

        edgeId ??= x => ReadStringProperty(x, "Id");
        edgeFrom ??= x => ReadStringProperty(x, "FromId");
        edgeTo ??= x => ReadStringProperty(x, "ToId");
        edgeType ??= x => typeof(TEdge).Name;
        edgeLabel ??= _ => null;

        return new GraphDocument
        {
            RootNodeId = envelope.RootNodeId,
            Nodes = [.. envelope.Nodes
                .Select(x => new GraphDocumentNode
                {
                    Id = nodeId(x),
                    Label = nodeLabel(x),
                    Type = nodeType(x),
                    IsRoot = string.Equals(nodeId(x), envelope.RootNodeId, StringComparison.OrdinalIgnoreCase),
                    Properties = SnapshotProperties(x)
                })],
            Edges = [.. envelope.Edges
                .Select(x => new GraphDocumentEdge
                {
                    Id = edgeId(x),
                    From = edgeFrom(x),
                    To = edgeTo(x),
                    Type = edgeType(x),
                    Label = edgeLabel(x),
                    Properties = SnapshotProperties(x)
                })]
        };
    }

    private static Dictionary<string, object?> SnapshotProperties<T>(T value) =>
        value switch
        {
            null => [],
            _ => value.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.CanRead)
            .ToDictionary(x => x.Name, x => x.GetValue(value))
        };

    private static string ReadStringProperty<T>(T value, string name)
    {
        var result = TryReadStringProperty(value, name);
        if (string.IsNullOrWhiteSpace(result))
        {
            throw new InvalidOperationException($"Property '{name}' is required on '{typeof(T).FullName}' to build a GraphDocument.");
        }

        return result;
    }

    private static string? TryReadStringProperty<T>(T value, string name)
    {
        if (value is null)
        {
            return null;
        }

        var property = value.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        var propertyValue = property?.GetValue(value);
        return propertyValue?.ToString();
    }
}