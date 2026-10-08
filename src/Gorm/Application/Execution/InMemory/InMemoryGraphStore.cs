using Gorm.Application.Execution;
using Gorm.Application.History;
using Gorm.Core.Primitives;

namespace Gorm.Application.Execution.InMemory;

/// <summary>
/// Represents an in-memory graph store for nodes and edges.
/// </summary>
public sealed class InMemoryGraphStore
{
    private readonly Dictionary<Type, Dictionary<Guid, Node>> _nodesByType = [];
    private readonly Dictionary<Type, Dictionary<Guid, Edge>> _edgesByType = [];

    /// <summary>
    /// Gets all node buckets by CLR type.
    /// </summary>
    public IReadOnlyDictionary<Type, Dictionary<Guid, Node>> NodesByType => _nodesByType;

    /// <summary>
    /// Gets all edge buckets by CLR type.
    /// </summary>
    public IReadOnlyDictionary<Type, Dictionary<Guid, Edge>> EdgesByType => _edgesByType;

    /// <summary>
    /// Gets the node bucket for the specified node type.
    /// </summary>
    /// <typeparam name="TNode">The node type.</typeparam>
    /// <returns>The node bucket.</returns>
    public IReadOnlyDictionary<Guid, TNode> GetNodeBucket<TNode>()
        where TNode : Node
    {
        if (!_nodesByType.TryGetValue(typeof(TNode), out var bucket))
        {
            return new Dictionary<Guid, TNode>();
        }

        return bucket.ToDictionary(x => x.Key, x => (TNode)CloneNode(x.Value));
    }

    /// <summary>
    /// Gets the edge bucket for the specified edge type.
    /// </summary>
    /// <typeparam name="TEdge">The edge type.</typeparam>
    /// <returns>The edge bucket.</returns>
    public IReadOnlyDictionary<Guid, TEdge> GetEdgeBucket<TEdge>()
        where TEdge : Edge
    {
        if (!_edgesByType.TryGetValue(typeof(TEdge), out var bucket))
        {
            return new Dictionary<Guid, TEdge>();
        }

        return bucket.ToDictionary(x => x.Key, x => (TEdge)CloneEdge(x.Value));
    }

    /// <summary>
    /// Gets all nodes of the specified type.
    /// </summary>
    /// <typeparam name="TNode">The node type.</typeparam>
    /// <returns>The nodes.</returns>
    public IReadOnlyCollection<TNode> GetNodes<TNode>()
        where TNode : Node
    {
        if (!_nodesByType.TryGetValue(typeof(TNode), out var bucket))
        {
            return [];
        }

        return [.. bucket.Values.Select(x => (TNode)CloneNode(x))];
    }

    /// <summary>
    /// Gets all edges of the specified type.
    /// </summary>
    /// <typeparam name="TEdge">The edge type.</typeparam>
    /// <returns>The edges.</returns>
    public IReadOnlyCollection<TEdge> GetEdges<TEdge>()
        where TEdge : Edge
    {
        if (!_edgesByType.TryGetValue(typeof(TEdge), out var bucket))
        {
            return [];
        }

        return [.. bucket.Values.Select(x => (TEdge)CloneEdge(x))];
    }

    /// <summary>
    /// Adds or replaces the specified node.
    /// </summary>
    /// <param name="node">The node.</param>
    public void UpsertNode(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.Id == Guid.Empty)
        {
            throw new InvalidOperationException("Cannot store a node with an empty Id.");
        }

        var bucket = GetOrCreateNodeBucket(node.GetType());
        bucket[node.Id] = CloneNode(node);
    }

    /// <summary>
    /// Adds or replaces the specified edge.
    /// </summary>
    /// <param name="edge">The edge.</param>
    public void UpsertEdge(Edge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);

        if (edge.Id == Guid.Empty)
        {
            throw new InvalidOperationException("Cannot store an edge with an empty Id.");
        }

        var bucket = GetOrCreateEdgeBucket(edge.GetType());
        bucket[edge.Id] = CloneEdge(edge);
    }

    /// <summary>
    /// Removes the specified node.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>True when removed; otherwise, false.</returns>
    public bool RemoveNode(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (!_nodesByType.TryGetValue(node.GetType(), out var bucket))
        {
            return false;
        }

        return bucket.Remove(node.Id);
    }

    /// <summary>
    /// Removes the specified edge.
    /// </summary>
    /// <param name="edge">The edge.</param>
    /// <returns>True when removed; otherwise, false.</returns>
    public bool RemoveEdge(Edge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);

        if (!_edgesByType.TryGetValue(edge.GetType(), out var bucket))
        {
            return false;
        }

        return bucket.Remove(edge.Id);
    }

    /// <summary>
    /// Removes all edges connected to the specified node.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The number of removed edges.</returns>
    public int RemoveConnectedEdges(Node node) =>
        _edgesByType.Values.Sum(
        bucket =>
        {
            var edgeIds = bucket.Values.Where(x => x.FromId == node.Id || x.ToId == node.Id).Select(x => x.Id).ToArray();

            return edgeIds.Count(bucket.Remove);
        });

    /// <summary>
    /// Removes all edges of the specified type between the specified nodes.
    /// </summary>
    /// <param name="edgeType">The edge CLR type.</param>
    /// <param name="fromId">The source node identifier.</param>
    /// <param name="toId">The target node identifier.</param>
    /// <returns>The number of removed edges.</returns>
    public int RemoveEdgeConnections(Type edgeType, Guid fromId, Guid toId)
    {
        ArgumentNullException.ThrowIfNull(edgeType);

        if (!_edgesByType.TryGetValue(edgeType, out var bucket))
        {
            return 0;
        }

        var edgeIds = bucket.Values.Where(x => x.FromId == fromId && x.ToId == toId).Select(x => x.Id).ToList();

        if (edgeIds.Count > 1)
        {
            throw new InvalidOperationException("Ambiguous GORM edge disconnection matched multiple edge rows. Remove a specific edge entity instead.");
        }

        foreach (var edgeId in edgeIds)
        {
            bucket.Remove(edgeId);
        }

        return edgeIds.Count;
    }

    /// <summary>
    /// Clears the entire store.
    /// </summary>
    public void Clear()
    {
        _nodesByType.Clear();
        _edgesByType.Clear();
    }

    private Dictionary<Guid, Node> GetOrCreateNodeBucket(Type clrType)
    {
        if (!_nodesByType.TryGetValue(clrType, out var bucket))
        {
            bucket = [];
            _nodesByType[clrType] = bucket;
        }

        return bucket;
    }

    private Dictionary<Guid, Edge> GetOrCreateEdgeBucket(Type clrType)
    {
        if (!_edgesByType.TryGetValue(clrType, out var bucket))
        {
            bucket = [];
            _edgesByType[clrType] = bucket;
        }

        return bucket;
    }

    private static Node CloneNode(Node node) =>
        (Node)GraphHistorySnapshotCloner.Clone(node, node.GetType());

    private static Edge CloneEdge(Edge edge) =>
        (Edge)GraphHistorySnapshotCloner.Clone(edge, edge.GetType());
}