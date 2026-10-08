using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Projection;

/// <summary>
/// Builds a validated graph projection delta while preserving concrete GORM node and edge types.
/// </summary>
public sealed class GraphProjectionDeltaBuilder
{
    private readonly Dictionary<Guid, Node> _addedNodes = [];
    private readonly Dictionary<Guid, Node> _updatedNodes = [];
    private readonly HashSet<Guid> _removedNodeIds = [];
    private readonly HashSet<Guid> _changedNodeIds = [];
    private readonly Dictionary<Guid, Edge> _addedEdges = [];
    private readonly Dictionary<Guid, Edge> _updatedEdges = [];
    private readonly HashSet<Guid> _removedEdgeIds = [];
    private readonly HashSet<Guid> _changedEdgeIds = [];

    /// <summary>
    /// Initializes a delta builder for one logical projection version transition.
    /// </summary>
    public GraphProjectionDeltaBuilder(GraphProjectionKey key, long baseVersion, long targetVersion)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (baseVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baseVersion), baseVersion, "A base version cannot be negative.");
        }

        if (targetVersion <= baseVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(targetVersion), targetVersion, "A target version must be greater than its base version.");
        }

        Key = key;
        BaseVersion = baseVersion;
        TargetVersion = targetVersion;
    }

    /// <summary>
    /// Gets the logical projection key.
    /// </summary>
    public GraphProjectionKey Key { get; }

    /// <summary>
    /// Gets the required base version.
    /// </summary>
    public long BaseVersion { get; }

    /// <summary>
    /// Gets the resulting target version.
    /// </summary>
    public long TargetVersion { get; }

    /// <summary>
    /// Adds new nodes to the delta.
    /// </summary>
    public GraphProjectionDeltaBuilder AddNodes<TNode>(IEnumerable<TNode> nodes)
        where TNode : Node
    {
        ArgumentNullException.ThrowIfNull(nodes);
        foreach (var node in nodes)
        {
            RegisterNode(node, _addedNodes, "add");
        }

        return this;
    }

    /// <summary>
    /// Adds replacement values for existing nodes to the delta.
    /// </summary>
    public GraphProjectionDeltaBuilder UpdateNodes<TNode>(IEnumerable<TNode> nodes)
        where TNode : Node
    {
        ArgumentNullException.ThrowIfNull(nodes);
        foreach (var node in nodes)
        {
            RegisterNode(node, _updatedNodes, "update");
        }

        return this;
    }

    /// <summary>
    /// Adds existing node identifiers to remove. Incident edges are removed automatically.
    /// </summary>
    public GraphProjectionDeltaBuilder RemoveNodes(IEnumerable<Guid> nodeIds)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);
        foreach (var nodeId in nodeIds)
        {
            RegisterIdentifier(nodeId, _changedNodeIds, _removedNodeIds, "node", "remove");
        }

        return this;
    }

    /// <summary>
    /// Adds new edges to the delta.
    /// </summary>
    public GraphProjectionDeltaBuilder AddEdges<TEdge>(IEnumerable<TEdge> edges)
        where TEdge : Edge
    {
        ArgumentNullException.ThrowIfNull(edges);
        foreach (var edge in edges)
        {
            RegisterEdge(edge, _addedEdges, "add");
        }

        return this;
    }

    /// <summary>
    /// Adds replacement values for existing edges to the delta.
    /// </summary>
    public GraphProjectionDeltaBuilder UpdateEdges<TEdge>(IEnumerable<TEdge> edges)
        where TEdge : Edge
    {
        ArgumentNullException.ThrowIfNull(edges);
        foreach (var edge in edges)
        {
            RegisterEdge(edge, _updatedEdges, "update");
        }

        return this;
    }

    /// <summary>
    /// Adds existing edge identifiers to remove.
    /// </summary>
    public GraphProjectionDeltaBuilder RemoveEdges(IEnumerable<Guid> edgeIds)
    {
        ArgumentNullException.ThrowIfNull(edgeIds);
        foreach (var edgeId in edgeIds)
        {
            RegisterIdentifier(edgeId, _changedEdgeIds, _removedEdgeIds, "edge", "remove");
        }

        return this;
    }

    /// <summary>
    /// Builds an immutable, identifier-ordered delta.
    /// </summary>
    public GraphProjectionDelta Build() => new(
        new GraphProjectionDelta.GraphProjectionDeltaParameters
        {
            Key = Key,
            BaseVersion = BaseVersion,
            TargetVersion = TargetVersion,
            AddedNodes = [.. _addedNodes.Values.OrderBy(node => node.Id)],
            UpdatedNodes = [.. _updatedNodes.Values.OrderBy(node => node.Id)],
            RemovedNodeIds = [.. _removedNodeIds.Order()],
            AddedEdges = [.. _addedEdges.Values.OrderBy(edge => edge.Id)],
            UpdatedEdges = [.. _updatedEdges.Values.OrderBy(edge => edge.Id)],
            RemovedEdgeIds = [.. _removedEdgeIds.Order()]
        });

    private void RegisterNode(Node? node, IDictionary<Guid, Node> destination, string operation)
    {
        ArgumentNullException.ThrowIfNull(node);
        RegisterIdentifier(node.Id, _changedNodeIds, destination, node, "node", operation);
    }

    private void RegisterEdge(Edge? edge, IDictionary<Guid, Edge> destination, string operation)
    {
        ArgumentNullException.ThrowIfNull(edge);

        if (edge.FromId == Guid.Empty || edge.ToId == Guid.Empty)
        {
            throw new ArgumentException($"Edge '{edge.Id}' must have non-empty endpoint identifiers.", nameof(edge));
        }

        RegisterIdentifier(edge.Id, _changedEdgeIds, destination, edge, "edge", operation);
    }

    private static void RegisterIdentifier(Guid identifier, ISet<Guid> changedIdentifiers, HashSet<Guid> destination, string entityKind, string operation)
    {
        EnsureIdentifier(identifier, entityKind);
        EnsureUniqueOperation(identifier, changedIdentifiers, entityKind, operation);
        destination.Add(identifier);
    }

    private static void RegisterIdentifier<TEntity>(
        Guid identifier,
        ISet<Guid> changedIdentifiers,
        IDictionary<Guid, TEntity> destination,
        TEntity entity,
        string entityKind,
        string operation)
    {
        EnsureIdentifier(identifier, entityKind);
        EnsureUniqueOperation(identifier, changedIdentifiers, entityKind, operation);
        destination.Add(identifier, entity);
    }

    private static void EnsureIdentifier(Guid identifier, string entityKind)
    {
        if (identifier == Guid.Empty)
        {
            throw new ArgumentException($"A changed {entityKind} must have a non-empty identifier.", nameof(identifier));
        }
    }

    private static void EnsureUniqueOperation(Guid identifier, ISet<Guid> changedIdentifiers, string entityKind, string operation)
    {
        if (!changedIdentifiers.Add(identifier))
        {
            throw new ArgumentException($"The delta already contains an operation for {entityKind} '{identifier}' and cannot also {operation} it.", nameof(operation));
        }
    }
}