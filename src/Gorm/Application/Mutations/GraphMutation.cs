using Gorm.Application.Mutations.Edges;
using Gorm.Application.Mutations.Nodes;
using Gorm.Application.Mutations.Options;
using Gorm.Core.Primitives;

namespace Gorm.Application.Mutations;

/// <summary>
/// Represents an intent-based graph mutation.
/// </summary>
public sealed class GraphMutation
{
    private readonly List<GraphMutationNode> _nodes = [];
    private readonly List<GraphMutationEdge> _edges = [];

    private GraphMutation(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>
    /// Gets the mutation name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the optional correlation id.
    /// </summary>
    public string? CorrelationId { get; private set; }

    /// <summary>
    /// Gets the optional source event id.
    /// </summary>
    public string? SourceEventId { get; private set; }

    /// <summary>
    /// Gets the node mutation items.
    /// </summary>
    public IReadOnlyList<GraphMutationNode> Nodes => _nodes;

    /// <summary>
    /// Gets the edge mutation items.
    /// </summary>
    public IReadOnlyList<GraphMutationEdge> Edges => _edges;

    /// <summary>
    /// Creates a graph mutation.
    /// </summary>
    /// <param name="name">The mutation name.</param>
    /// <returns>The graph mutation.</returns>
    public static GraphMutation Create(string name = "graph-mutation") =>
        new(name);

    /// <summary>
    /// Sets the mutation correlation id.
    /// </summary>
    /// <param name="correlationId">The correlation id.</param>
    /// <returns>The graph mutation.</returns>
    public GraphMutation WithCorrelationId(string? correlationId)
    {
        CorrelationId = correlationId;
        return this;
    }

    /// <summary>
    /// Sets the mutation source event id.
    /// </summary>
    /// <param name="sourceEventId">The source event id.</param>
    /// <returns>The graph mutation.</returns>
    public GraphMutation WithSourceEventId(string? sourceEventId)
    {
        SourceEventId = sourceEventId;
        return this;
    }

    /// <summary>
    /// Adds a new node.
    /// </summary>
    public GraphMutation AddNode<TNode>(TNode node, Action<GraphMutationNodeOptions>? configure = null)
        where TNode : Node =>
        AddNode(node, GraphMutationNodeOperation.Add, configure);

    /// <summary>
    /// Attaches an existing unchanged node.
    /// </summary>
    public GraphMutation AttachNode<TNode>(TNode node, Action<GraphMutationNodeOptions>? configure = null)
        where TNode : Node =>
        AddNode(node, GraphMutationNodeOperation.Attach, configure);

    /// <summary>
    /// Updates an existing node.
    /// </summary>
    public GraphMutation UpdateNode<TNode>(TNode node, Action<GraphMutationNodeOptions>? configure = null)
        where TNode : Node =>
        AddNode(node, GraphMutationNodeOperation.Update, configure);

    /// <summary>
    /// Adds or updates a node based on its identity.
    /// </summary>
    public GraphMutation UpsertNode<TNode>(TNode node, Action<GraphMutationNodeOptions>? configure = null)
        where TNode : Node =>
        AddNode(node, GraphMutationNodeOperation.Upsert, configure);

    /// <summary>
    /// Removes an existing node.
    /// </summary>
    public GraphMutation RemoveNode<TNode>(TNode node, Action<GraphMutationNodeOptions>? configure = null)
        where TNode : Node =>
        AddNode(node, GraphMutationNodeOperation.Remove, configure);

    /// <summary>
    /// Adds a new edge.
    /// </summary>
    public GraphMutation AddEdge<TEdge, TFrom, TTo>(TFrom from, TTo to, TEdge edge, Action<GraphMutationEdgeOptions>? configure = null)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node =>
        AddEdge(from, to, edge, GraphMutationEdgeOperation.Add, configure);

    /// <summary>
    /// Adds a new edge.
    /// </summary>
    public GraphMutation AddEdge<TEdge, TFrom, TTo>(TFrom from, TTo to, Action<TEdge>? configureEdge = null, Action<GraphMutationEdgeOptions>? configure = null)
        where TEdge : Edge, new()
        where TFrom : Node
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var edge = new TEdge();
        configureEdge?.Invoke(edge);

        return AddEdge(from, to, edge, GraphMutationEdgeOperation.Add, configure);
    }

    /// <summary>
    /// Adds or updates an edge based on its identity.
    /// </summary>
    public GraphMutation UpsertEdge<TEdge, TFrom, TTo>(TFrom from, TTo to, TEdge edge, Action<GraphMutationEdgeOptions>? configure = null)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node =>
        AddEdge(from, to, edge, GraphMutationEdgeOperation.Upsert, configure);

    /// <summary>
    /// Adds or updates an edge based on its identity.
    /// </summary>
    public GraphMutation UpsertEdge<TEdge, TFrom, TTo>(TFrom from, TTo to, Action<TEdge>? configureEdge = null, Action<GraphMutationEdgeOptions>? configure = null)
        where TEdge : Edge, new()
        where TFrom : Node
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var edge = new TEdge();
        configureEdge?.Invoke(edge);

        return AddEdge(from, to, edge, GraphMutationEdgeOperation.Upsert, configure);
    }

    /// <summary>
    /// Removes an edge between the source and target nodes.
    /// </summary>
    public GraphMutation RemoveEdge<TEdge, TFrom, TTo>(TFrom from, TTo to, Action<GraphMutationEdgeOptions>? configure = null)
        where TEdge : Edge, new()
        where TFrom : Node
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        return AddEdge(from, to, new TEdge(), GraphMutationEdgeOperation.Remove, configure);
    }

    private GraphMutation AddNode<TNode>(TNode node, GraphMutationNodeOperation operation, Action<GraphMutationNodeOptions>? configure)
        where TNode : Node
    {
        ArgumentNullException.ThrowIfNull(node);

        var options = new GraphMutationNodeOptions();
        configure?.Invoke(options);

        var existing = _nodes.FirstOrDefault(x => ReferenceEquals(x.Node, node));

        if (existing is not null)
        {
            if (existing.Operation == operation && string.Equals(existing.Key, options.KeyValue, StringComparison.Ordinal))
            {
                return this;
            }

            throw new InvalidOperationException(
                $"The node instance '{node.GetType().FullName}' is already part of this graph mutation with {nameof(operation)} '{existing.Operation}'. " +
                $"It cannot also be added with {nameof(operation)} '{operation}'.");
        }

        _nodes.Add(new GraphMutationNode
        {
            Node = node,
            Operation = operation,
            Key = options.KeyValue
        });

        return this;
    }

    private GraphMutation AddEdge<TEdge, TFrom, TTo>(TFrom from, TTo to, TEdge edge, GraphMutationEdgeOperation operation, Action<GraphMutationEdgeOptions>? configure)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(edge);

        var options = new GraphMutationEdgeOptions();
        configure?.Invoke(options);

        if (_edges.Any(x => ReferenceEquals(x.Edge, edge)))
        {
            throw new InvalidOperationException("The same edge instance is already part of this graph mutation. Use a separate edge instance for every graph edge row.");
        }

        _edges.Add(new GraphMutationEdge
        {
            Edge = edge,
            From = from,
            To = to,
            Operation = operation,
            Key = options.KeyValue
        });

        return this;
    }
}