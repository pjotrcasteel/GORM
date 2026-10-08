using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using Gorm.Application.Execution;
using Gorm.Application.Execution.Abstraction;
using Gorm.Application.History.Abstractions;
using Gorm.Application.History.Envelopes;
using Gorm.Application.History.Recording;
using Gorm.Application.History.Resolvers;
using Gorm.Application.Mutations;
using Gorm.Application.Mutations.Edges;
using Gorm.Application.Mutations.Nodes;
using Gorm.Application.Mutations.Options;
using Gorm.Application.Mutations.Validations;
using Gorm.Application.Querying;
using Gorm.Application.Querying.Models;
using Gorm.Application.Tracking;
using Gorm.Core.Configuration;
using Gorm.Core.Loading;
using Gorm.Core.Metadata;
using Gorm.Core.Models;
using Gorm.Core.Primitives;
using Gorm.Core.Sets;
using Gorm.Infrastructure.Persistence.Connections;
using Gorm.Infrastructure.Providers.Abstractions;
using Gorm.Infrastructure.Providers.SqlServer;

namespace Gorm.Application.Context;

public abstract class GraphContext
{
    private readonly Lazy<GraphModel> _model;
    private readonly GraphRelationshipState _relationshipState = new();
    private readonly GraphContextTransactionCoordinator _transactionCoordinator = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphContext"/> class.
    /// </summary>
    protected GraphContext()
    {
        _model = new Lazy<GraphModel>(BuildModel);
        ChangeTracker = new GraphChangeTracker();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphContext"/> class.
    /// </summary>
    /// <param name="connectionFactory">The connection factory.</param>
    protected GraphContext(IGormDbConnectionFactory connectionFactory)
        : this()
    {
        ConnectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// Executes new.
    /// </summary>
    /// <returns>The value.</returns>
    internal GraphRelationshipFixupStore RelationshipFixupStore => _relationshipState.RelationshipFixupStore;

    /// <summary>
    /// Gets value.
    /// </summary>
    /// <returns>The value.</returns>
    public GraphModel Model => _model.Value;

    /// <summary>
    /// Gets the change tracker for entities attached to the context.
    /// </summary>
    public GraphChangeTracker ChangeTracker { get; }

    /// <summary>
    /// Gets entries.
    /// </summary>
    /// <returns>The items.</returns>
    public IEnumerable<GraphEntityEntry> Entries => ChangeTracker.Entries;

    /// <summary>
    /// Gets the connection factory used for query and save operations.
    /// </summary>
    public IGormDbConnectionFactory? ConnectionFactory { get; private set; }

    /// <summary>
    /// Gets instance.
    /// </summary>
    /// <returns>The value.</returns>
    public IGraphProvider Provider { get; private set; } = SqlServerGraphProvider.Instance;

    /// <summary>
    /// Gets the configured history recorder.
    /// </summary>
    public IGraphHistoryRecorder HistoryRecorder { get; private set; } = NullGraphHistoryRecorder.Instance;

    /// <summary>
    /// Gets the configured history time provider.
    /// </summary>
    public TimeProvider HistoryTimeProvider { get; private set; } = TimeProvider.System;

    /// <summary>
    /// Gets the configured temporal resolver.
    /// </summary>
    public IGraphTemporalResolver TemporalResolver { get; private set; } = new DefaultGraphTemporalResolver();

    /// <summary>
    /// Gets the execution engine used for query and save operations.
    /// </summary>
    public IGraphExecutionEngine ExecutionEngine { get; private set; } = new SqlServerGraphExecutionEngine();

    /// <summary>
    /// Executes use provider.
    /// </summary>
    /// <param name="provider">The provider.</param>
    public void UseProvider(IGraphProvider provider) =>
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));

    /// <summary>
    /// Configures the history recorder.
    /// </summary>
    /// <param name="historyRecorder">The history recorder.</param>
    public void UseHistoryRecorder(IGraphHistoryRecorder historyRecorder) =>
        HistoryRecorder = historyRecorder ?? throw new ArgumentNullException(nameof(historyRecorder));

    /// <summary>
    /// Configures the temporal resolver.
    /// </summary>
    /// <param name="temporalResolver">The temporal resolver.</param>
    public void UseTemporalResolver(IGraphTemporalResolver temporalResolver) =>
        TemporalResolver = temporalResolver ?? throw new ArgumentNullException(nameof(temporalResolver));

    /// <summary>
    /// Configures the history time provider.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    public void UseTimeProvider(TimeProvider timeProvider) =>
        HistoryTimeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>
    /// Executes use execution engine.
    /// </summary>
    /// <param name="executionEngine">The execution engine.</param>
    public void UseExecutionEngine(IGraphExecutionEngine executionEngine) =>
        ExecutionEngine = executionEngine ?? throw new ArgumentNullException(nameof(executionEngine));

    /// <summary>
    /// Executes use connection factory.
    /// </summary>
    /// <param name="connectionFactory">The connection factory.</param>
    public void UseConnectionFactory(IGormDbConnectionFactory connectionFactory) =>
        ConnectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    /// <summary>
    /// Creates the set for nodes.
    /// </summary>
    public GraphSet<TNode> Set<TNode>()
        where TNode : Node => CreateSet(() => Model.GetNode(typeof(TNode)), mapping => new GraphSet<TNode>(this, mapping));

    /// <summary>
    /// Creates the set for edges.
    /// </summary>
    public GraphEdgeSet<TEdge> EdgeSet<TEdge>()
        where TEdge : Edge => CreateSet(() => Model.GetEdge(typeof(TEdge)), mapping => new GraphEdgeSet<TEdge>(this, mapping));

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <returns>The items.</returns>
    public IReadOnlyList<GraphRelationshipMapping> GetRelationships<TNode>()
        where TNode : Node => Model.GetRelationships(typeof(TNode));

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <typeparam name="TOwner">The type of t owner.</typeparam>
    /// <param name="relationshipName">The relationship name.</param>
    /// <returns>The value.</returns>
    public GraphRelationshipMapping GetRelationship<TOwner>(string relationshipName)
        where TOwner : Node => Model.GetRelationship<TOwner>(relationshipName);

    /// <summary>
    /// Executes entry.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <returns>The entry.</returns>
    public GraphEntityEntry Entry(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return ChangeTracker.Entry(entity);
    }

    /// <summary>
    /// Executes detect changes.
    /// </summary>
    public void DetectChanges() => ChangeTracker.DetectChanges(Model);

    /// <summary>
    /// Adds the item.
    /// </summary>
    /// <param name="entity">The entity.</param>
    public void Add(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureTrackableEntity(entity);
        EnsureEntityId(entity);
        ChangeTracker.Add(entity);
        InvalidateRelationshipState();
    }

    /// <summary>
    /// Adds the item.
    /// </summary>
    /// <param name="entities">The entities.</param>
    public void AddRange(params object[] entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        AddRange((IEnumerable<object>)entities);
    }

    /// <summary>
    /// Adds the item.
    /// </summary>
    /// <param name="entities">The entities.</param>
    public void AddRange(IEnumerable<object> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        foreach (var entity in entities)
        {
            Add(entity);
        }
    }

    /// <summary>
    /// Adds graph nodes and edge connections to the change tracker.
    /// </summary>
    /// <param name="nodes">The nodes to add.</param>
    /// <param name="edgeConnections">The edge connections to add.</param>
    public void AddGraph(IEnumerable<Node> nodes, IEnumerable<GraphEdgeBatchItem> edgeConnections)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edgeConnections);

        foreach (var node in nodes)
        {
            Add(node);
        }

        AddEdges(edgeConnections);
    }

    /// <summary>
    /// Adds edge connections to the change tracker.
    /// </summary>
    /// <param name="edgeConnections">The edge connections to add.</param>
    public void AddEdges(IEnumerable<GraphEdgeBatchItem> edgeConnections)
    {
        ArgumentNullException.ThrowIfNull(edgeConnections);

        foreach (var edgeConnection in edgeConnections)
        {
            AddEdge(edgeConnection.From, edgeConnection.To, edgeConnection.Edge);
        }
    }

    /// <summary>
    /// Adds graph nodes and edge connections and saves them in one graph save transaction.
    /// </summary>
    /// <param name="nodes">The nodes to add.</param>
    /// <param name="edgeConnections">The edge connections to add.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<int> StoreGraphAsync(IEnumerable<Node> nodes, IEnumerable<GraphEdgeBatchItem> edgeConnections, CancellationToken cancellationToken = default)
    {
        AddGraph(nodes, edgeConnections);
        return SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Adds graph nodes and edge connections and saves them in one graph save transaction.
    /// </summary>
    /// <param name="connectionFactory">The connection factory.</param>
    /// <param name="nodes">The nodes to add.</param>
    /// <param name="edgeConnections">The edge connections to add.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<int> StoreGraphAsync(
        IGormDbConnectionFactory connectionFactory,
        IEnumerable<Node> nodes,
        IEnumerable<GraphEdgeBatchItem> edgeConnections,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);

        AddGraph(nodes, edgeConnections);
        return SaveChangesAsync(connectionFactory, cancellationToken);
    }

    /// <summary>
    /// Plans a graph mutation without touching the change tracker.
    /// </summary>
    /// <param name="mutation">The graph mutation.</param>
    /// <param name="options">The mutation options.</param>
    /// <returns>The mutation plan.</returns>
    public GraphMutationPlan PlanMutation(GraphMutation mutation, GraphMutationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        options ??= GraphMutationOptions.Default;

        var errors = new List<GraphMutationValidationError>();
        var nodePlans = new List<GraphMutationNodePlanItem>();
        var edgePlans = new List<GraphMutationEdgePlanItem>();

        ValidateMutationNodes(mutation, errors);
        ValidateMutationEdges(mutation, errors);

        foreach (var node in mutation.Nodes)
        {
            nodePlans.Add(new GraphMutationNodePlanItem { Node = node.Node, Operation = node.Operation, Action = DetermineNodeAction(node, options), Key = node.Key });
        }

        foreach (var edge in mutation.Edges)
        {
            edgePlans.Add(new GraphMutationEdgePlanItem
            {
                Edge = edge.Edge,
                From = edge.From,
                To = edge.To,
                Operation = edge.Operation,
                Action = DetermineEdgeAction(edge, options),
                Key = edge.Key
            });
        }

        return new GraphMutationPlan
        {
            Name = mutation.Name,
            CorrelationId = mutation.CorrelationId,
            SourceEventId = mutation.SourceEventId,
            Validation = new GraphMutationValidationResult(errors),
            Nodes = nodePlans,
            Edges = edgePlans
        };
    }

    /// <summary>
    /// Applies a graph mutation to the change tracker.
    /// </summary>
    /// <param name="mutation">The graph mutation.</param>
    /// <param name="options">The mutation options.</param>
    /// <returns>The mutation plan.</returns>
    public GraphMutationPlan ApplyMutation(GraphMutation mutation, GraphMutationOptions? options = null)
    {
        var plan = PlanMutation(mutation, options);
        ExecuteMutationPlan(plan, options);
        return plan;
    }

    /// <summary>
    /// Applies and saves a graph mutation.
    /// </summary>
    /// <param name="mutation">The graph mutation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of persisted changes.</returns>
    public Task<int> ApplyMutationAsync(GraphMutation mutation, CancellationToken cancellationToken = default) =>
        ApplyMutationAsync(mutation, GraphMutationOptions.Default, cancellationToken);

    /// <summary>
    /// Applies and saves a graph mutation.
    /// </summary>
    /// <param name="mutation">The graph mutation.</param>
    /// <param name="options">The mutation options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of persisted changes.</returns>
    public async Task<int> ApplyMutationAsync(GraphMutation mutation, GraphMutationOptions? options, CancellationToken cancellationToken = default)
    {
        ApplyMutation(mutation, options);
        return await SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Applies and saves a graph mutation with the supplied connection factory.
    /// </summary>
    /// <param name="connectionFactory">The connection factory.</param>
    /// <param name="mutation">The graph mutation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of persisted changes.</returns>
    public Task<int> ApplyMutationAsync(IGormDbConnectionFactory connectionFactory, GraphMutation mutation, CancellationToken cancellationToken = default) =>
        ApplyMutationAsync(connectionFactory, mutation, GraphMutationOptions.Default, cancellationToken);

    /// <summary>
    /// Applies and saves a graph mutation with the supplied connection factory.
    /// </summary>
    /// <param name="connectionFactory">The connection factory.</param>
    /// <param name="mutation">The graph mutation.</param>
    /// <param name="options">The mutation options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of persisted changes.</returns>
    public async Task<int> ApplyMutationAsync(
        IGormDbConnectionFactory connectionFactory,
        GraphMutation mutation,
        GraphMutationOptions? options,
        CancellationToken cancellationToken = default)
    {
        ApplyMutation(mutation, options);
        return await SaveChangesAsync(connectionFactory, cancellationToken);
    }

    /// <summary>
    /// Executes a graph mutation plan.
    /// </summary>
    /// <param name="plan">The mutation plan.</param>
    /// <param name="options">The mutation options.</param>
    public void ExecuteMutationPlan(GraphMutationPlan plan, GraphMutationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(plan);

        options ??= GraphMutationOptions.Default;

        if (!plan.Validation.IsValid && options.ThrowOnValidationError)
        {
            throw new InvalidOperationException(plan.Validation.ToMessage());
        }

        if (!plan.Validation.IsValid)
        {
            return;
        }

        foreach (var node in plan.Nodes)
        {
            ExecuteNodePlanItem(node);
        }

        foreach (var edge in plan.Edges)
        {
            ExecuteEdgePlanItem(edge);
        }
    }

    /// <summary>
    /// Attaches the entity.
    /// </summary>
    /// <param name="entity">The entity.</param>
    public void Attach(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureTrackableEntity(entity);
        ChangeTracker.Attach(entity, Model);
    }

    /// <summary>
    /// Attaches the entity.
    /// </summary>
    /// <param name="entities">The entities.</param>
    public void AttachRange(IEnumerable<object> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        foreach (var entity in entities)
        {
            Attach(entity);
        }
    }

    /// <summary>
    /// Removes the item.
    /// </summary>
    /// <param name="entity">The entity.</param>
    public void Remove(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureTrackableEntity(entity);
        ChangeTracker.Remove(entity);
        InvalidateRelationshipState();
    }

    /// <summary>
    /// Removes the item.
    /// </summary>
    /// <param name="entities">The entities.</param>
    public void RemoveRange(IEnumerable<object> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        foreach (var entity in entities)
        {
            Remove(entity);
        }
    }

    /// <summary>
    /// Executes mark modified.
    /// </summary>
    /// <param name="entity">The entity.</param>
    public void MarkModified(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureTrackableEntity(entity);
        ChangeTracker.MarkModified(entity);
        InvalidateRelationshipState();
    }

    /// <summary>
    /// Updates the entity.
    /// </summary>
    /// <param name="entity">The entity.</param>
    public void Update(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureTrackableEntity(entity);

        var entry = ChangeTracker.Entry(entity);

        if (entry.State == EntityState.Detached)
        {
            ChangeTracker.Attach(entity, Model);
        }

        ChangeTracker.MarkModified(entity);
        InvalidateRelationshipState();
    }

    /// <summary>
    /// Updates the entity.
    /// </summary>
    /// <param name="entities">The entities.</param>
    public void UpdateRange(IEnumerable<object> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        foreach (var entity in entities)
        {
            Update(entity);
        }
    }

    /// <summary>
    /// Executes has changes.
    /// </summary>
    /// <returns>True when successful; otherwise, false.</returns>
    public bool HasChanges()
    {
        DetectChanges();
        return ChangeTracker.HasChanges();
    }

    /// <summary>
    /// Executes accept all changes.
    /// </summary>
    public void AcceptAllChanges()
    {
        ChangeTracker.AcceptAllChanges(Model);
        InvalidateRelationshipState();
    }

    /// <summary>
    /// Executes clear tracking.
    /// </summary>
    public void ClearTracking()
    {
        ChangeTracker.Clear();
        InvalidateRelationshipState();
    }

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <returns>The value.</returns>
    public string ChangeTrackerDebugView => GraphChangeTrackerDebugView.Format(ChangeTracker);

    /// <summary>
    /// Executes connect.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TFrom">The type of t from.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="from">The source node.</param>
    /// <param name="to">The target node.</param>
    /// <param name="configureEdge">The edge configuration.</param>
    /// <returns>The value.</returns>
    public TEdge Connect<TEdge, TFrom, TTo>(TFrom from, TTo to, Action<TEdge>? configureEdge = null)
        where TEdge : Edge, new()
        where TFrom : Node
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var edge = new TEdge();
        configureEdge?.Invoke(edge);

        return AddEdge(from, to, edge);
    }

    /// <summary>
    /// Adds the item.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TFrom">The type of t from.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="from">The source node.</param>
    /// <param name="to">The target node.</param>
    /// <param name="edge">The edge instance.</param>
    /// <returns>The value.</returns>
    public TEdge AddEdge<TEdge, TFrom, TTo>(TFrom from, TTo to, TEdge edge)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node =>
        (TEdge)AddEdgeCore(edge, from, to);

    /// <summary>
    /// Adds the edge connection to the change tracker.
    /// </summary>
    /// <param name="from">The source node.</param>
    /// <param name="to">The target node.</param>
    /// <param name="edge">The edge instance.</param>
    /// <returns>The edge instance.</returns>
    public Edge AddEdge(Node from, Node to, Edge edge) =>
        AddEdgeCore(edge, from, to);

    private Edge AddEdgeCore(Edge edge, Node from, Node to)
    {
        ArgumentNullException.ThrowIfNull(edge);
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        EnsureEdgeConnectionIsValid(edge, from, to);
        EnsurePendingEdgeInstanceIsNotAlreadyConnected(edge);

        if (edge.Id == Guid.Empty)
        {
            edge.Id = Guid.NewGuid();
        }

        ChangeTracker.Add(edge);
        ChangeTracker.AddEdgeConnection(new PendingEdgeConnection { Edge = edge, FromNode = from, ToNode = to });

        InvalidateRelationshipState();
        return edge;
    }

    /// <summary>
    /// Executes disconnect.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TFrom">The type of t from.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="from">The source node.</param>
    /// <param name="to">The target node.</param>
    public void Disconnect<TEdge, TFrom, TTo>(TFrom from, TTo to)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        ChangeTracker.AddEdgeDisconnection(new PendingEdgeDisconnection { EdgeType = typeof(TEdge), FromNode = from, ToNode = to });

        InvalidateRelationshipState();
    }

    /// <summary>
    /// Executes is outgoing loaded.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TFrom">The type of t from.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="from">The source node.</param>
    /// <returns>True when successful; otherwise, false.</returns>
    public bool IsOutgoingLoaded<TEdge, TFrom, TTo>(TFrom from)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(from);
        return _relationshipState.Contains(from, GraphTraversalDirection.Outgoing, typeof(TEdge), typeof(TTo), includeEdge: false);
    }

    /// <summary>
    /// Executes is incoming loaded.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TFrom">The type of t from.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="to">The target node.</param>
    /// <returns>True when successful; otherwise, false.</returns>
    public bool IsIncomingLoaded<TEdge, TFrom, TTo>(TTo to)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(to);
        return _relationshipState.Contains(to, GraphTraversalDirection.Incoming, typeof(TEdge), typeof(TFrom), includeEdge: false);
    }

    /// <summary>
    /// Executes is outgoing with edges loaded.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TFrom">The type of t from.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="from">The source node.</param>
    /// <returns>True when successful; otherwise, false.</returns>
    public bool IsOutgoingWithEdgesLoaded<TEdge, TFrom, TTo>(TFrom from)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(from);
        return _relationshipState.Contains(from, GraphTraversalDirection.Outgoing, typeof(TEdge), typeof(TTo), includeEdge: true);
    }

    /// <summary>
    /// Executes is incoming with edges loaded.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TFrom">The type of t from.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="to">The target node.</param>
    /// <returns>True when successful; otherwise, false.</returns>
    public bool IsIncomingWithEdgesLoaded<TEdge, TFrom, TTo>(TTo to)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(to);
        return _relationshipState.Contains(to, GraphTraversalDirection.Incoming, typeof(TEdge), typeof(TFrom), includeEdge: true);
    }

    /// <summary>
    /// Executes try get loaded outgoing.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TFrom">The type of t from.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="from">The source node.</param>
    /// <param name="related">The related node.</param>
    /// <returns>True when successful; otherwise, false.</returns>
    public bool TryGetLoadedOutgoing<TEdge, TFrom, TTo>(TFrom from, out IReadOnlyList<TTo>? related)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(from);

        if (_relationshipState.TryGet(
            GraphRelationshipState.CreateKey(from, GraphTraversalDirection.Outgoing, typeof(TEdge), typeof(TTo), includeEdge: false),
            out List<TTo>? result))
        {
            related = result;
            return true;
        }

        related = null;
        return false;
    }

    /// <summary>
    /// Executes try get loaded incoming.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TFrom">The type of t from.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="to">The target node.</param>
    /// <param name="related">The related node.</param>
    /// <returns>True when successful; otherwise, false.</returns>
    public bool TryGetLoadedIncoming<TEdge, TFrom, TTo>(TTo to, out IReadOnlyList<TFrom>? related)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(to);

        if (_relationshipState.TryGet(
            GraphRelationshipState.CreateKey(to, GraphTraversalDirection.Incoming, typeof(TEdge), typeof(TFrom), includeEdge: false),
            out List<TFrom>? result))
        {
            related = result;
            return true;
        }

        related = null;
        return false;
    }

    /// <summary>
    /// Executes try get loaded outgoing with edges.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TFrom">The type of t from.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="from">The source node.</param>
    /// <param name="related">The related node.</param>
    /// <returns>True when successful; otherwise, false.</returns>
    public bool TryGetLoadedOutgoingWithEdges<TEdge, TFrom, TTo>(TFrom from, out IReadOnlyList<GraphRelatedEdgeResult<TEdge, TTo>>? related)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(from);

        if (_relationshipState.TryGet(
            GraphRelationshipState.CreateKey(from, GraphTraversalDirection.Outgoing, typeof(TEdge), typeof(TTo), includeEdge: true),
            out List<GraphRelatedEdgeResult<TEdge, TTo>>? result))
        {
            related = result;
            return true;
        }

        related = null;
        return false;
    }

    /// <summary>
    /// Executes try get loaded incoming with edges.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TFrom">The type of t from.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="to">The target node.</param>
    /// <param name="related">The related node.</param>
    /// <returns>True when successful; otherwise, false.</returns>
    public bool TryGetLoadedIncomingWithEdges<TEdge, TFrom, TTo>(TTo to, out IReadOnlyList<GraphRelatedEdgeResult<TEdge, TFrom>>? related)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(to);

        if (_relationshipState.TryGet(
            GraphRelationshipState.CreateKey(to, GraphTraversalDirection.Incoming, typeof(TEdge), typeof(TFrom), includeEdge: true),
            out List<GraphRelatedEdgeResult<TEdge, TFrom>>? result))
        {
            related = result;
            return true;
        }

        related = null;
        return false;
    }

    /// <summary>
    /// Loads the specified relationship.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TFrom">The type of t from.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="from">The source node.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<List<TTo>?> LoadOutgoingAsync<TEdge, TFrom, TTo>(TFrom from, CancellationToken cancellationToken = default)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(from);

        var key = GraphRelationshipState.CreateKey(from, GraphTraversalDirection.Outgoing, typeof(TEdge), typeof(TTo), includeEdge: false);

        if (_relationshipState.TryGet(key, out List<TTo>? cached))
        {
            return Task.FromResult(cached);
        }

        return LoadOutgoingCoreAsync<TEdge, TFrom, TTo>(from, key, cancellationToken);
    }

    /// <summary>
    /// Loads the specified relationship.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TFrom">The type of t from.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="from">The source node.</param>
    /// <param name="edgePredicate">The edge predicate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<List<TTo>> LoadOutgoingAsync<TEdge, TFrom, TTo>(TFrom from, Expression<Func<TEdge, bool>> edgePredicate, CancellationToken cancellationToken = default)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(edgePredicate);

        var query = Set<TFrom>().Where(BuildKeyEqualsExpression<TFrom>(from)).Outgoing<TEdge, TTo>(edgePredicate);

        return query.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Loads the specified relationship.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TFrom">The type of t from.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="to">The target node.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<List<TFrom>?> LoadIncomingAsync<TEdge, TFrom, TTo>(TTo to, CancellationToken cancellationToken = default)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(to);

        var key = GraphRelationshipState.CreateKey(to, GraphTraversalDirection.Incoming, typeof(TEdge), typeof(TFrom), includeEdge: false);

        if (_relationshipState.TryGet(key, out List<TFrom>? cached))
        {
            return Task.FromResult(cached);
        }

        return LoadIncomingCoreAsync<TEdge, TFrom, TTo>(to, key, cancellationToken);
    }

    /// <summary>
    /// Loads the specified relationship.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TFrom">The type of t from.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="to">The target node.</param>
    /// <param name="edgePredicate">The edge predicate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<List<TFrom>> LoadIncomingAsync<TEdge, TFrom, TTo>(TTo to, Expression<Func<TEdge, bool>> edgePredicate, CancellationToken cancellationToken = default)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(edgePredicate);

        var query = Set<TTo>().Where(BuildKeyEqualsExpression<TTo>(to)).Incoming<TEdge, TFrom>(edgePredicate);

        return query.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Loads the specified relationship.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TFrom">The type of t from.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="from">The source node.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<List<GraphRelatedEdgeResult<TEdge, TTo>>?> LoadOutgoingWithEdgesAsync<TEdge, TFrom, TTo>(TFrom from, CancellationToken cancellationToken = default)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(from);

        var key = GraphRelationshipState.CreateKey(from, GraphTraversalDirection.Outgoing, typeof(TEdge), typeof(TTo), includeEdge: true);

        if (_relationshipState.TryGet(key, out List<GraphRelatedEdgeResult<TEdge, TTo>>? cached))
        {
            return Task.FromResult(cached);
        }

        return LoadOutgoingWithEdgesCoreAsync<TEdge, TFrom, TTo>(from, key, cancellationToken);
    }

    /// <summary>
    /// Loads the specified relationship.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TFrom">The type of t from.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="from">The source node.</param>
    /// <param name="edgePredicate">The edge predicate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<List<GraphRelatedEdgeResult<TEdge, TTo>>> LoadOutgoingWithEdgesAsync<TEdge, TFrom, TTo>(
        TFrom from,
        Expression<Func<TEdge, bool>> edgePredicate,
        CancellationToken cancellationToken = default)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(edgePredicate);

        var query = Set<TFrom>()
            .Where(BuildKeyEqualsExpression<TFrom>(from))
            .Outgoing<TEdge, TTo>(edgePredicate)
            .SelectWithEdge<TEdge, TTo, GraphRelatedEdgeResult<TEdge, TTo>>((node, edge) => new GraphRelatedEdgeResult<TEdge, TTo> { Node = node, Edge = edge });

        return query.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Loads the specified relationship.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TFrom">The type of t from.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="to">The target node.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<List<GraphRelatedEdgeResult<TEdge, TFrom>>?> LoadIncomingWithEdgesAsync<TEdge, TFrom, TTo>(TTo to, CancellationToken cancellationToken = default)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(to);

        var key = GraphRelationshipState.CreateKey(to, GraphTraversalDirection.Incoming, typeof(TEdge), typeof(TFrom), includeEdge: true);

        if (_relationshipState.TryGet(key, out List<GraphRelatedEdgeResult<TEdge, TFrom>>? cached))
        {
            return Task.FromResult(cached);
        }

        return LoadIncomingWithEdgesCoreAsync<TEdge, TFrom, TTo>(to, key, cancellationToken);
    }

    /// <summary>
    /// Loads the specified relationship.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TFrom">The type of t from.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="to">The target node.</param>
    /// <param name="edgePredicate">The edge predicate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<List<GraphRelatedEdgeResult<TEdge, TFrom>>> LoadIncomingWithEdgesAsync<TEdge, TFrom, TTo>(
        TTo to,
        Expression<Func<TEdge, bool>> edgePredicate,
        CancellationToken cancellationToken = default)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(edgePredicate);

        var query = Set<TTo>()
            .Where(BuildKeyEqualsExpression<TTo>(to))
            .Incoming<TEdge, TFrom>(edgePredicate)
            .SelectWithEdge<TEdge, TFrom, GraphRelatedEdgeResult<TEdge, TFrom>>((node, edge) => new GraphRelatedEdgeResult<TEdge, TFrom> { Node = node, Edge = edge });

        return query.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Loads outgoing relationships and their edges for multiple source nodes in a single query.
    /// </summary>
    /// <typeparam name="TSource">The source node type.</typeparam>
    /// <typeparam name="TEdge">The edge type.</typeparam>
    /// <typeparam name="TNode">The related node type.</typeparam>
    /// <param name="sourceNodeIds">The source node identifiers.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The related nodes and edges grouped by source node identifier.</returns>
    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<(TNode Node, TEdge Edge)>>> LoadOutgoingWithEdgesBySourceAsync<TSource, TEdge, TNode>(
        IReadOnlyCollection<Guid> sourceNodeIds,
        CancellationToken cancellationToken = default)
        where TSource : Node
        where TEdge : Edge
        where TNode : Node
    {
        ArgumentNullException.ThrowIfNull(sourceNodeIds);

        if (sourceNodeIds.Count == 0)
        {
            return Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<(TNode Node, TEdge Edge)>>>(
                new Dictionary<Guid, IReadOnlyList<(TNode Node, TEdge Edge)>>());
        }

        return LoadOutgoingWithEdgesBySourceCoreAsync<TSource, TEdge, TNode>(sourceNodeIds, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<(TNode Node, TEdge Edge)>>> LoadOutgoingWithEdgesBySourceCoreAsync<TSource, TEdge, TNode>(
        IReadOnlyCollection<Guid> sourceNodeIds,
        CancellationToken cancellationToken)
        where TSource : Node
        where TEdge : Edge
        where TNode : Node
    {
        var relationships = await Set<TSource>()
            .WhereIds(sourceNodeIds)
            .Outgoing<TEdge, TNode>()
            .SelectWithEdge<TEdge, TNode, GraphRelatedEdgeResult<TEdge, TNode>>(
                (node, edge) => new GraphRelatedEdgeResult<TEdge, TNode>
                {
                    Node = node,
                    Edge = edge
                })
            .ToListAsync(cancellationToken);

        return relationships
            .GroupBy(relationship => relationship.Edge.FromId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<(TNode Node, TEdge Edge)>)[.. group.Select(relationship => (relationship.Node, relationship.Edge))]);
    }

    /// <summary>
    /// Saves pending changes.
    /// </summary>
    /// <param name="connectionFactory">The connection factory.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<int> SaveChangesAsync(
        IGormDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default) => SaveChangesAsync(connectionFactory, acceptAllChangesOnSuccess: true, cancellationToken);

    /// <summary>
    /// Saves pending changes.
    /// </summary>
    /// <param name="connectionFactory">The connection factory.</param>
    /// <param name="acceptAllChangesOnSuccess">The accept all changes on success.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<int> SaveChangesAsync(IGormDbConnectionFactory connectionFactory, bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        return SaveChangesCoreAsync(connectionFactory, acceptAllChangesOnSuccess, cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

    /// <summary>
    /// Saves pending changes.
    /// </summary>
    /// <param name="acceptAllChangesOnSuccess">The accept all changes on success.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        if (ExecutionEngine is SqlServerGraphExecutionEngine)
        {
            return await ExecutionEngine.SaveChangesAsync(this, ChangeTracker, acceptAllChangesOnSuccess, cancellationToken);
        }

        DetectChanges();

        var historyCapture = await PrepareHistoryCaptureAsync(cancellationToken);
        var result = await ExecutionEngine.SaveChangesAsync(this, ChangeTracker, acceptAllChangesOnSuccess, cancellationToken);

        await historyCapture.PersistAfterSaveAsync(CancellationToken.None);

        InvalidateRelationshipState();

        return result;
    }

    private async Task<int> SaveChangesCoreAsync(IGormDbConnectionFactory connectionFactory, bool acceptAllChangesOnSuccess, CancellationToken cancellationToken)
    {
        DetectChanges();

        var historyCapture = await PrepareHistoryCaptureAsync(cancellationToken, deferSqlBatchCapture: true);

        if (AfterHistoryPreparationForTesting is { } afterHistoryPreparation)
        {
            await afterHistoryPreparation(cancellationToken);
        }

        var result = await _transactionCoordinator.SaveChangesAsync(
            this,
            ChangeTracker,
            connectionFactory,
            acceptAllChangesOnSuccess,
            historyCapture.CaptureWithinTransactionAsync,
            cancellationToken);

        InvalidateRelationshipState();
        return result;
    }

    private async Task<PreparedHistoryCapture> PrepareHistoryCaptureAsync(CancellationToken cancellationToken, bool deferSqlBatchCapture = false)
    {
        if (deferSqlBatchCapture && HistoryRecorder is IGraphHistoryBatchRecorder sqlBatchRecorder)
        {
            return new PreparedHistoryCapture(
                (connection, transaction, token) =>
                {
                    // Defer until SaveChanges has acquired node locks and removed incident edges.
                    // A competing edge writer that commits first is now included in the terminal history.
                    var capturedAtUtc = HistoryTimeProvider.GetUtcNow().UtcDateTime;
                    var envelopes = GraphHistoryEnvelopeCollector.Collect(this, ChangeTracker, capturedAtUtc);
                    return sqlBatchRecorder.PersistAsync(envelopes, connection, transaction, token);
                },
                token => Task.CompletedTask);
        }

        var capturedAtUtc = HistoryTimeProvider.GetUtcNow().UtcDateTime;

        if (HistoryRecorder is IGraphHistoryBatchRecorder batchRecorder)
        {
            var envelopes = GraphHistoryEnvelopeCollector.Collect(this, ChangeTracker, capturedAtUtc);

            return new PreparedHistoryCapture(
                (connection, transaction, token) => batchRecorder.PersistAsync(envelopes, connection, transaction, token),
                token => batchRecorder.PersistAsync(envelopes, connection: null, transaction: null, token));
        }

        await HistoryRecorder.CaptureAsync(this, ChangeTracker, capturedAtUtc, cancellationToken);
        return PreparedHistoryCapture.Empty;
    }

    private sealed class PreparedHistoryCapture(
        Func<DbConnection, DbTransaction, CancellationToken, Task>? captureWithinTransactionAsync,
        Func<CancellationToken, Task> persistAfterSaveAsync)
    {
        public static PreparedHistoryCapture Empty { get; } = new(
            captureWithinTransactionAsync: null,
            persistAfterSaveAsync: _ => Task.CompletedTask);

        public Func<DbConnection, DbTransaction, CancellationToken, Task>? CaptureWithinTransactionAsync { get; } = captureWithinTransactionAsync;

        public Func<CancellationToken, Task> PersistAfterSaveAsync { get; } = persistAfterSaveAsync;
    }

    /// <summary>
    /// Executes generate create script.
    /// </summary>
    /// <returns>The value.</returns>
    public string GenerateCreateScript() => Provider.SchemaGenerator.GenerateCreateScript(Model);

    /// <summary>
    /// Executes ensure created async.
    /// </summary>
    /// <param name="connectionFactory">The connection factory.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Obsolete("GORM is database-first. Provision schema outside GORM and use ValidateSchemaAsync() to detect drift.")]
    public Task EnsureCreatedAsync(IGormDbConnectionFactory connectionFactory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);

        throw new NotSupportedException(
            "GORM is database-first and does not create or migrate databases. " +
            "Provision SQL Server Graph objects through your normal database deployment process and use ValidateSchemaAsync() to detect drift.");
    }

    /// <summary>
    /// Executes ensure created async.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Obsolete("GORM is database-first. Provision schema outside GORM and use ValidateSchemaAsync() to detect drift.")]
    public Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        if (ConnectionFactory is null)
        {
            throw new InvalidOperationException(
                $"No {nameof(IGormDbConnectionFactory)} is configured on the current {nameof(GraphContext)}. " +
                $"Call {nameof(UseConnectionFactory)}(...) first or construct the context with a connection factory.");
        }

        throw new NotSupportedException(
            "GORM is database-first and does not create or migrate databases. " +
            "Provision SQL Server Graph objects through your normal database deployment process and use ValidateSchemaAsync() to detect drift.");
    }

    /// <summary>
    /// Executes try get related.
    /// </summary>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="owner">The owner node.</param>
    /// <param name="relationshipName">The relationship name.</param>
    /// <param name="related">The related node.</param>
    /// <returns>True when successful; otherwise, false.</returns>
    public bool TryGetRelated<TRelated>(Node owner, string relationshipName, out IReadOnlyList<TRelated>? related)
        where TRelated : Node
    {
        ArgumentNullException.ThrowIfNull(owner);
        return RelationshipFixupStore.TryGetRelated(owner, relationshipName, out related);
    }

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="owner">The owner node.</param>
    /// <param name="relationshipName">The relationship name.</param>
    /// <returns>The items.</returns>
    public IReadOnlyList<TRelated> GetRelated<TRelated>(Node owner, string relationshipName)
        where TRelated : Node
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (RelationshipFixupStore.TryGetRelated(owner, relationshipName, out IReadOnlyList<TRelated>? related))
        {
            return related ?? [];
        }

        return [];
    }

    /// <summary>
    /// Executes begin transaction async.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<GraphTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (ConnectionFactory is null)
        {
            throw new InvalidOperationException(
                $"No {nameof(IGormDbConnectionFactory)} is configured on the current {nameof(GraphContext)}. " +
                $"Call {nameof(UseConnectionFactory)}(...) first or construct the context with a connection factory.");
        }

        return _transactionCoordinator.BeginTransactionAsync(this, ConnectionFactory, cancellationToken);
    }

    /// <summary>
    /// Saves pending changes.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public async Task<int> SaveChangesInTransactionAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var result = await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// Executes clear current transaction.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    internal void ClearCurrentTransaction(GraphTransaction transaction)
    {
        _transactionCoordinator.ClearCurrentTransaction(transaction);
    }

    /// <summary>
    /// Invalidates snapshots when a transaction or savepoint rolls back.
    /// Original entity object values are not restored.
    /// </summary>
    internal void InvalidateTrackedStateAfterRollback()
    {
        ChangeTracker.Clear();
        InvalidateRelationshipState();
    }

    // Internal, per-context deterministic interleaving points used only by SQL Server integration tests.
    internal Func<CancellationToken, Task>? AfterIncidentEdgeCleanupForTesting { get; set; }

    internal Func<CancellationToken, Task>? BeforeEdgeEndpointLookupForTesting { get; set; }

    internal Func<CancellationToken, Task>? AfterHistoryPreparationForTesting { get; set; }

    /// <summary>
    /// Executes try get current transaction.
    /// </summary>
    /// <param name="connection">The active transaction connection.</param>
    /// <param name="transaction">The active transaction.</param>
    /// <returns>True when an active transaction exists; otherwise, false.</returns>
    internal bool TryGetCurrentTransaction(out DbConnection connection, out DbTransaction transaction) =>
        _transactionCoordinator.TryGetCurrentTransaction(out connection, out transaction);

    /// <summary>
    /// Executes try get related with edges.
    /// </summary>
    /// <param name="owner">The owner node.</param>
    /// <param name="relationshipName">The relationship name.</param>
    /// <param name="related">The related node.</param>
    /// <returns>True when successful; otherwise, false.</returns>
    public bool TryGetRelatedWithEdges(Node owner, string relationshipName, out IReadOnlyList<object>? related)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return RelationshipFixupStore.TryGetRelatedWithEdges(owner, relationshipName, out related);
    }

    /// <summary>
    /// Executes on model creating.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected abstract void OnModelCreating(GraphModelBuilder modelBuilder);

    private static TSet CreateSet<TSet, TMapping>(Func<TMapping> mappingFactory, Func<TMapping, TSet> setFactory)
        where TMapping : class
    {
        ArgumentNullException.ThrowIfNull(mappingFactory);
        ArgumentNullException.ThrowIfNull(setFactory);

        var mapping = mappingFactory();
        return setFactory(mapping);
    }

    private async Task<List<TTo>?> LoadOutgoingCoreAsync<TEdge, TFrom, TTo>(TFrom from, GraphLoadedRelationshipKey key, CancellationToken cancellationToken)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node
    {
        var query = Set<TFrom>().Where(BuildKeyEqualsExpression<TFrom>(from)).Outgoing<TEdge, TTo>();

        var result = await query.ToListAsync(cancellationToken);
        _relationshipState.Set(key, result);
        return result;
    }

    private async Task<List<TFrom>?> LoadIncomingCoreAsync<TEdge, TFrom, TTo>(TTo to, GraphLoadedRelationshipKey key, CancellationToken cancellationToken)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node
    {
        var query = Set<TTo>().Where(BuildKeyEqualsExpression<TTo>(to)).Incoming<TEdge, TFrom>();

        var result = await query.ToListAsync(cancellationToken);
        _relationshipState.Set(key, result);
        return result;
    }

    private async Task<List<GraphRelatedEdgeResult<TEdge, TTo>>?> LoadOutgoingWithEdgesCoreAsync<TEdge, TFrom, TTo>(
        TFrom from,
        GraphLoadedRelationshipKey key,
        CancellationToken cancellationToken)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node
    {
        var query = Set<TFrom>()
            .Where(BuildKeyEqualsExpression<TFrom>(from))
            .Outgoing<TEdge, TTo>()
            .SelectWithEdge<TEdge, TTo, GraphRelatedEdgeResult<TEdge, TTo>>((node, edge) => new GraphRelatedEdgeResult<TEdge, TTo> { Node = node, Edge = edge });

        var result = await query.ToListAsync(cancellationToken);
        _relationshipState.Set(key, result);
        return result;
    }

    private async Task<List<GraphRelatedEdgeResult<TEdge, TFrom>>?> LoadIncomingWithEdgesCoreAsync<TEdge, TFrom, TTo>(
        TTo to,
        GraphLoadedRelationshipKey key,
        CancellationToken cancellationToken)
        where TEdge : Edge
        where TFrom : Node
        where TTo : Node
    {
        var query = Set<TTo>()
            .Where(BuildKeyEqualsExpression<TTo>(to))
            .Incoming<TEdge, TFrom>()
            .SelectWithEdge<TEdge, TFrom, GraphRelatedEdgeResult<TEdge, TFrom>>((node, edge) => new GraphRelatedEdgeResult<TEdge, TFrom> { Node = node, Edge = edge });

        var result = await query.ToListAsync(cancellationToken);
        _relationshipState.Set(key, result);
        return result;
    }

    private void InvalidateRelationshipState()
    {
        _relationshipState.Clear();
    }

    private GraphModel BuildModel()
    {
        var builder = new GraphModelBuilder();
        OnModelCreating(builder);
        return builder.Build();
    }

    private void EnsurePendingEdgeInstanceIsNotAlreadyConnected(Edge edge)
    {
        var duplicate = ChangeTracker.PendingEdgeConnections.Any(x => ReferenceEquals(x.Edge, edge));

        if (duplicate)
        {
            throw new InvalidOperationException($"The edge instance '{edge.GetType().FullName}' is already used by another pending edge connection.");
        }
    }

    private void EnsureEdgeConnectionIsValid(Edge edge, Node from, Node to)
    {
        if (from.Id == Guid.Empty)
        {
            throw new InvalidOperationException($"The source node '{from.GetType().FullName}' has no key. Add it to the graph batch first or set its Id before connecting it.");
        }

        if (to.Id == Guid.Empty)
        {
            throw new InvalidOperationException($"The target node '{to.GetType().FullName}' has no key. Add it to the graph batch first or set its Id before connecting it.");
        }

        var edgeMapping = Model.GetEdge(edge.GetType());

        if (!edgeMapping.FromNodeType.IsInstanceOfType(from))
        {
            throw new InvalidOperationException(
                $"Edge '{edge.GetType().FullName}' expects source node type '{edgeMapping.FromNodeType.FullName}', but got '{from.GetType().FullName}'.");
        }

        if (!edgeMapping.ToNodeType.IsInstanceOfType(to))
        {
            throw new InvalidOperationException(
                $"Edge '{edge.GetType().FullName}' expects target node type '{edgeMapping.ToNodeType.FullName}', but got '{to.GetType().FullName}'.");
        }
    }

    private static void EnsureEntityId(object entity)
    {
        switch (entity)
        {
            case Node node when node.Id == Guid.Empty:
                node.Id = Guid.NewGuid();
                break;
            case Edge edge when edge.Id == Guid.Empty:
                edge.Id = Guid.NewGuid();
                break;
        }
    }

    private static void EnsureTrackableEntity(object entity)
    {
        if (entity is Node || entity is Edge)
        {
            return;
        }

        throw new InvalidOperationException($"Only {nameof(Node)} and {nameof(Edge)} instances can be tracked.");
    }

    private Expression<Func<TNode, bool>> BuildKeyEqualsExpression<TNode>(TNode entity)
        where TNode : Node
    {
        var mapping = Model.GetNode(typeof(TNode));
        var keyProperty = GetRequiredProperty(typeof(TNode), mapping.KeyPropertyName);
        var keyValue = keyProperty.GetValue(entity) ?? throw new InvalidOperationException(
            $"Entity '{typeof(TNode).FullName}' has a null key value for '{mapping.KeyPropertyName}'.");

        var parameter = Expression.Parameter(typeof(TNode), "x");
        var propertyAccess = Expression.Property(parameter, keyProperty);
        var constant = Expression.Constant(keyValue, keyProperty.PropertyType);
        var equals = Expression.Equal(propertyAccess, constant);

        return Expression.Lambda<Func<TNode, bool>>(equals, parameter);
    }

    private static PropertyInfo GetRequiredProperty(Type clrType, string propertyName)
    {
        var property = clrType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) ?? throw new InvalidOperationException(
            $"Property '{propertyName}' was not found on '{clrType.FullName}'.");

        return property;
    }

    private void ExecuteNodePlanItem(GraphMutationNodePlanItem node)
    {
        switch (node.Action)
        {
            case GraphMutationPlanAction.Insert:
                Add(node.Node);
                break;

            case GraphMutationPlanAction.Attach:
                Attach(node.Node);
                break;

            case GraphMutationPlanAction.Update:
                Update(node.Node);
                break;

            case GraphMutationPlanAction.Delete:
                Remove(node.Node);
                break;

            case GraphMutationPlanAction.NoOp:
                break;

            default:
                throw new InvalidOperationException($"Unsupported graph mutation node action '{node.Action}'.");
        }
    }

    private void ExecuteEdgePlanItem(GraphMutationEdgePlanItem edge)
    {
        switch (edge.Action)
        {
            case GraphMutationPlanAction.Insert:
                AddEdge(edge.From, edge.To, edge.Edge);
                break;

            case GraphMutationPlanAction.Update:
                Update(edge.Edge);
                break;

            case GraphMutationPlanAction.Delete:
                AddEdgeDisconnection(edge.Edge.GetType(), edge.From, edge.To);
                break;

            case GraphMutationPlanAction.Attach:
            case GraphMutationPlanAction.NoOp:
                break;

            default:
                throw new InvalidOperationException($"Unsupported graph mutation edge action '{edge.Action}'.");
        }
    }

    private void AddEdgeDisconnection(Type edgeType, Node from, Node to)
    {
        ChangeTracker.AddEdgeDisconnection(new PendingEdgeDisconnection { EdgeType = edgeType, FromNode = from, ToNode = to });

        InvalidateRelationshipState();
    }

    private static GraphMutationPlanAction DetermineNodeAction(GraphMutationNode node, GraphMutationOptions options)
    {
        return node.Operation switch
        {
            GraphMutationNodeOperation.Add => GraphMutationPlanAction.Insert,
            GraphMutationNodeOperation.Attach => GraphMutationPlanAction.Attach,
            GraphMutationNodeOperation.Update => GraphMutationPlanAction.Update,
            GraphMutationNodeOperation.Remove => GraphMutationPlanAction.Delete,
            GraphMutationNodeOperation.Upsert when node.Node.Id == Guid.Empty && options.InsertUpsertNodesWithEmptyId => GraphMutationPlanAction.Insert,
            GraphMutationNodeOperation.Upsert when node.Node.Id != Guid.Empty && options.UpdateUpsertNodesWithExistingId => GraphMutationPlanAction.Update,
            GraphMutationNodeOperation.Upsert => GraphMutationPlanAction.NoOp,
            _ => throw new InvalidOperationException($"Unsupported graph mutation node operation '{node.Operation}'.")
        };
    }

    private static GraphMutationPlanAction DetermineEdgeAction(GraphMutationEdge edge, GraphMutationOptions options)
    {
        return edge.Operation switch
        {
            GraphMutationEdgeOperation.Add => GraphMutationPlanAction.Insert,
            GraphMutationEdgeOperation.Remove => GraphMutationPlanAction.Delete,
            GraphMutationEdgeOperation.Upsert when edge.Edge.Id == Guid.Empty && options.InsertUpsertEdgesWithEmptyId => GraphMutationPlanAction.Insert,
            GraphMutationEdgeOperation.Upsert when edge.Edge.Id != Guid.Empty && options.UpdateUpsertEdgesWithExistingId => GraphMutationPlanAction.Update,
            GraphMutationEdgeOperation.Upsert => GraphMutationPlanAction.NoOp,
            _ => throw new InvalidOperationException($"Unsupported graph mutation edge operation '{edge.Operation}'.")
        };
    }

    private void ValidateMutationNodes(GraphMutation mutation, List<GraphMutationValidationError> errors)
    {
        for (var index = 0; index < mutation.Nodes.Count; index++)
        {
            var node = mutation.Nodes[index];
            var path = $"nodes[{index}]";

            if (!Model.Nodes.Any(x => x.ClrType == node.Node.GetType()))
            {
                errors.Add(new GraphMutationValidationError
                {
                    Code = "GraphMutation.Node.UnknownType",
                    Message = $"Node type '{node.Node.GetType().FullName}' is not registered in the graph model.",
                    Path = path,
                    Key = node.Key
                });
            }

            if (node.Operation is GraphMutationNodeOperation.Attach or GraphMutationNodeOperation.Update or GraphMutationNodeOperation.Remove && node.Node.Id == Guid.Empty)
            {
                errors.Add(new GraphMutationValidationError
                {
                    Code = "GraphMutation.Node.MissingId",
                    Message = $"Node operation '{node.Operation}' requires a non-empty node Id.",
                    Path = path,
                    Key = node.Key
                });
            }
        }

        var duplicateKeys = mutation.Nodes
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => $"{x.Node.GetType().FullName}:{x.Key}", StringComparer.Ordinal)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToArray();

        foreach (var duplicateKey in duplicateKeys)
        {
            errors.Add(new GraphMutationValidationError
            {
                Code = "GraphMutation.Node.DuplicateKey",
                Message = $"Node mutation key '{duplicateKey}' is used more than once.",
                Key = duplicateKey
            });
        }

        var duplicateIds = mutation.Nodes
            .Where(x => x.Node.Id != Guid.Empty)
            .GroupBy(x => $"{x.Node.GetType().FullName}:{x.Node.Id}", StringComparer.Ordinal)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToArray();

        foreach (var duplicateId in duplicateIds)
        {
            errors.Add(new GraphMutationValidationError
            {
                Code = "GraphMutation.Node.DuplicateId",
                Message = $"Node id '{duplicateId}' is used more than once in the same mutation.",
                Key = duplicateId
            });
        }
    }

    private void ValidateMutationEdges(GraphMutation mutation, List<GraphMutationValidationError> errors)
    {
        for (var index = 0; index < mutation.Edges.Count; index++)
        {
            var edge = mutation.Edges[index];
            var path = $"edges[{index}]";
            var edgeMapping = Model.Edges.FirstOrDefault(x => x.ClrType == edge.Edge.GetType());

            if (edgeMapping is null)
            {
                errors.Add(new GraphMutationValidationError
                {
                    Code = "GraphMutation.Edge.UnknownType",
                    Message = $"Edge type '{edge.Edge.GetType().FullName}' is not registered in the graph model.",
                    Path = path,
                    Key = edge.Key
                });

                continue;
            }

            if (!edgeMapping.FromNodeType.IsInstanceOfType(edge.From))
            {
                errors.Add(new GraphMutationValidationError
                {
                    Code = "GraphMutation.Edge.InvalidSourceType",
                    Message = $"Edge '{edge.Edge.GetType().FullName}' expects source node type '{edgeMapping.FromNodeType.FullName}', but got '{edge.From.GetType().FullName}'.",
                    Path = path,
                    Key = edge.Key
                });
            }

            if (!edgeMapping.ToNodeType.IsInstanceOfType(edge.To))
            {
                errors.Add(new GraphMutationValidationError
                {
                    Code = "GraphMutation.Edge.InvalidTargetType",
                    Message = $"Edge '{edge.Edge.GetType().FullName}' expects target node type '{edgeMapping.ToNodeType.FullName}', but got '{edge.To.GetType().FullName}'.",
                    Path = path,
                    Key = edge.Key
                });
            }

            if (edge.Operation == GraphMutationEdgeOperation.Remove && (edge.From.Id == Guid.Empty || edge.To.Id == Guid.Empty))
            {
                errors.Add(new GraphMutationValidationError
                {
                    Code = "GraphMutation.Edge.RemoveMissingEndpointId",
                    Message = "Removing an edge requires non-empty source and target node ids.",
                    Path = path,
                    Key = edge.Key
                });
            }
        }

        var duplicateEdgeInstances = mutation.Edges
            .GroupBy(x => x.Edge, ReferenceEqualityComparer<Edge>.Instance)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key.GetType().FullName)
            .ToArray();

        foreach (var duplicateEdgeInstance in duplicateEdgeInstances)
        {
            errors.Add(new GraphMutationValidationError
            {
                Code = "GraphMutation.Edge.DuplicateInstance",
                Message = $"The same edge instance '{duplicateEdgeInstance}' is used more than once in the same mutation."
            });
        }

        var duplicateUpsertIdentities = mutation.Edges
            .Where(x => x.Operation == GraphMutationEdgeOperation.Upsert)
            .Select(x => CreateUpsertEdgeIdentity(mutation, x))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x!, StringComparer.Ordinal)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToArray();

        foreach (var duplicateUpsertIdentity in duplicateUpsertIdentities)
        {
            errors.Add(new GraphMutationValidationError
            {
                Code = "GraphMutation.Edge.DuplicateUpsertIdentity",
                Message = $"Upsert edge identity '{duplicateUpsertIdentity}' is used more than once.",
                Key = duplicateUpsertIdentity
            });
        }
    }

    private static string? CreateUpsertEdgeIdentity(GraphMutation mutation, GraphMutationEdge edge)
    {
        var fromKey = ResolveNodeMutationKey(mutation, edge.From);
        var toKey = ResolveNodeMutationKey(mutation, edge.To);

        if (string.IsNullOrWhiteSpace(fromKey) || string.IsNullOrWhiteSpace(toKey))
        {
            return null;
        }

        return $"{edge.Edge.GetType().FullName}:{fromKey}->{toKey}:{edge.Key ?? "-"}";
    }

    private static string? ResolveNodeMutationKey(GraphMutation mutation, Node node)
    {
        if (node.Id != Guid.Empty)
        {
            return $"{node.GetType().FullName}:{node.Id}";
        }

        var mutationNode = mutation.Nodes.FirstOrDefault(x => ReferenceEquals(x.Node, node));

        if (!string.IsNullOrWhiteSpace(mutationNode?.Key))
        {
            return $"{node.GetType().FullName}:{mutationNode.Key}";
        }

        return null;
    }

    private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public static ReferenceEqualityComparer<T> Instance { get; } = new();

        public bool Equals(T? x, T? y) =>
            ReferenceEquals(x, y);

        public int GetHashCode(T obj) =>
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}