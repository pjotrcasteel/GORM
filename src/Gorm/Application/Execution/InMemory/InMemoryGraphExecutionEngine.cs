using System.Linq.Expressions;
using System.Reflection;
using Gorm.Application.Context;
using Gorm.Application.Execution;
using Gorm.Application.Execution.Abstraction;
using Gorm.Application.Execution.InMemory.Visitors;
using Gorm.Application.Querying.Models;
using Gorm.Application.Tracking;
using Gorm.Core.Primitives;

namespace Gorm.Application.Execution.InMemory;

/// <summary>
/// Represents an in-memory graph execution engine.
/// </summary>
public sealed class InMemoryGraphExecutionEngine : IGraphExecutionEngine
{
    private static readonly MethodInfo CreateNodeQueryableBridgeMethod =
        typeof(InMemoryGraphExecutionEngine)
            .GetMethod(nameof(CreateNodeQueryableBridge), BindingFlags.Instance | BindingFlags.Public)
        ?? throw new InvalidOperationException($"The method '{nameof(CreateNodeQueryableBridge)}' could not be found.");

    private static readonly MethodInfo CreateEdgeQueryableBridgeMethod =
        typeof(InMemoryGraphExecutionEngine)
            .GetMethod(nameof(CreateEdgeQueryableBridge), BindingFlags.Instance | BindingFlags.Public)
        ?? throw new InvalidOperationException($"The method '{nameof(CreateEdgeQueryableBridge)}' could not be found.");

    private static readonly MethodInfo ExecuteTraversalBridgeMethod =
        typeof(InMemoryGraphExecutionEngine)
            .GetMethod(nameof(ExecuteTraversalBridge), BindingFlags.Instance | BindingFlags.Public)
        ?? throw new InvalidOperationException($"The method '{nameof(ExecuteTraversalBridge)}' could not be found.");

    private static readonly MethodInfo ProjectSelectWithEdgeBridgeMethod =
        typeof(InMemoryGraphExecutionEngine)
            .GetMethod(nameof(ProjectSelectWithEdgeBridge), BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: [typeof(IQueryable), typeof(LambdaExpression)],
                modifiers: null)
        ?? throw new InvalidOperationException($"The method '{nameof(ProjectSelectWithEdgeBridge)}' could not be found.");

    private static readonly MethodInfo ExecuteTraversalWithEdgesBridgeMethod =
        typeof(InMemoryGraphExecutionEngine)
            .GetMethod(nameof(ExecuteTraversalWithEdgesBridge), BindingFlags.Instance | BindingFlags.Public)
        ?? throw new InvalidOperationException($"The method '{nameof(ExecuteTraversalWithEdgesBridge)}' could not be found.");

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryGraphExecutionEngine"/> class.
    /// </summary>
    /// <param name="store">The in-memory graph store.</param>
    public InMemoryGraphExecutionEngine(InMemoryGraphStore store)
    {
        Store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// Gets the backing in-memory graph store.
    /// </summary>
    public InMemoryGraphStore Store { get; }

    /// <summary>
    /// Executes the query using the specified execution request.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="query">The query to execute.</param>
    /// <param name="request">The execution request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<List<T>> ExecuteAsync<T>(GraphContext context, IQueryable<T> query, GraphQueryExecutionRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Execute(context, query, request));
    }

    internal List<T> Execute<T>(GraphContext context, IQueryable<T> query, GraphQueryExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(request);

        var executableQuery = RewriteToLinqToObjectsQuery(query);
        var take = request.GetEffectiveTake(null);

        if (take is not null)
        {
            executableQuery = executableQuery.Take(take.Value);
        }

        return [.. executableQuery];
    }

    /// <summary>
    /// Executes the query and materializes the results as a list.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<List<T>> ExecuteListAsync<T>(GraphContext context, IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ExecuteList(context, query));
    }

    internal List<T> ExecuteList<T>(GraphContext context, IQueryable<T> query)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);

        var executableQuery = RewriteToLinqToObjectsQuery(query);
        return [.. executableQuery];
    }

    /// <summary>
    /// Executes the query and returns whether any results exist.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<bool> ExecuteAnyAsync<T>(GraphContext context, IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ExecuteAny(context, query));
    }

    internal bool ExecuteAny<T>(GraphContext context, IQueryable<T> query)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);

        var executableQuery = RewriteToLinqToObjectsQuery(query);
        return ((IEnumerable<T>)executableQuery).Any();
    }

    /// <summary>
    /// Executes the query and returns the number of results.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<int> ExecuteCountAsync<T>(GraphContext context, IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);

        var executableQuery = RewriteToLinqToObjectsQuery(query);
        return Task.FromResult(executableQuery.Count());
    }

    /// <summary>
    /// Executes the query and returns the long count of results.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<long> ExecuteLongCountAsync<T>(GraphContext context, IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);

        var executableQuery = RewriteToLinqToObjectsQuery(query);
        return Task.FromResult(executableQuery.LongCount());
    }

    /// <summary>
    /// Executes the pending changes in the change tracker.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="changeTracker">The change tracker.</param>
    /// <param name="acceptAllChangesOnSuccess">Indicates whether changes should be accepted after a successful save.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<int> SaveChangesAsync(GraphContext context, GraphChangeTracker changeTracker, bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(changeTracker);

        context.DetectChanges();

        var affectedRows = 0;

        affectedRows += PrepareAddedEntities(changeTracker);
        affectedRows += ProcessPendingEdgeConnections(changeTracker);
        affectedRows += ProcessPendingEdgeDisconnections(changeTracker);
        affectedRows += ProcessModifiedEntities(changeTracker);
        affectedRows += ProcessDeletedEntities(changeTracker);

        if (acceptAllChangesOnSuccess)
        {
            changeTracker.AcceptAllChanges(context.Model);
        }

        return Task.FromResult(affectedRows);
    }

    internal IQueryable<T> RewriteToLinqToObjectsQuery<T>(IQueryable<T> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rewrittenExpression = new InMemoryQueryExpressionVisitor(this).Visit(query.Expression)
            ?? throw new InvalidOperationException("Failed to rewrite the in-memory query expression.");

        var root = FindRoot(query.Expression);
        var rootQueryable = CreateRootQueryable(root);

        return rootQueryable.Provider.CreateQuery<T>(rewrittenExpression);
    }

    internal IQueryable CreateRootQueryable(GraphQueryRootExpression root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (root.ElementKind == GraphQueryElementKind.Node)
        {
            if (!typeof(Node).IsAssignableFrom(root.ElementType))
            {
                throw new NotSupportedException($"The root element type '{root.ElementType.FullName}' is not a node type.");
            }

            return CreateNodeQueryable(root.ElementType);
        }

        if (root.ElementKind == GraphQueryElementKind.Edge)
        {
            if (!typeof(Edge).IsAssignableFrom(root.ElementType))
            {
                throw new NotSupportedException($"The root element type '{root.ElementType.FullName}' is not an edge type.");
            }

            return CreateEdgeQueryable(root.ElementType);
        }

        throw new NotSupportedException($"The graph root kind '{root.ElementKind}' is not supported by in-memory query execution.");
    }

    internal IQueryable ExecuteTraversal(
        IQueryable source,
        Type edgeType,
        Type nodeType,
        GraphTraversalDirection direction,
        LambdaExpression? predicate,
        GraphTraversalSafeties safety)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(edgeType);
        ArgumentNullException.ThrowIfNull(nodeType);

        if (safety != GraphTraversalSafeties.None)
        {
            throw new NotSupportedException($"In-memory traversal {nameof(safety)} flags are not supported yet.");
        }

        var genericMethod = ExecuteTraversalBridgeMethod.MakeGenericMethod(edgeType, nodeType);
        var result = genericMethod.Invoke(this, [source, direction, predicate]);

        return (IQueryable)(result ?? throw new InvalidOperationException("Failed to execute the in-memory traversal."));
    }

    internal static IQueryable ProjectSelectWithEdge(IQueryable source, Type edgeType, Type nodeType, Type resultType, LambdaExpression selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(edgeType);
        ArgumentNullException.ThrowIfNull(nodeType);
        ArgumentNullException.ThrowIfNull(resultType);
        ArgumentNullException.ThrowIfNull(selector);

        var genericMethod = ProjectSelectWithEdgeBridgeMethod.MakeGenericMethod(edgeType, nodeType, resultType);
        var result = genericMethod.Invoke(null, [source, selector]);

        return (IQueryable)(result ?? throw new InvalidOperationException("Failed to project the SelectWithEdge query."));
    }

    public static IQueryable<TResult> ProjectSelectWithEdgeBridge<TEdge, TNode, TResult>(IQueryable source, LambdaExpression selector)
        where TEdge : Edge
        where TNode : Node
    {
        return ProjectSelectWithEdgeCore<TEdge, TNode, TResult>(source, selector);
    }

    private static IQueryable<TResult> ProjectSelectWithEdgeCore<TEdge, TNode, TResult>(IQueryable source, LambdaExpression selector)
        where TEdge : Edge
        where TNode : Node
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var compiled = (Func<TNode, TEdge, TResult>)selector.Compile();

        return source.Cast<InMemoryTraversalPair<TEdge, TNode>>().Select(x => compiled(x.Node, x.Edge)).AsQueryable();
    }

    private static GraphQueryRootExpression FindRoot(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var finder = new RootFindingExpressionVisitor();
        finder.Visit(expression);

        return finder.Root ?? throw new NotSupportedException("In-memory query execution requires a graph root expression.");
    }

    private IQueryable CreateNodeQueryable(Type nodeType)
    {
        ArgumentNullException.ThrowIfNull(nodeType);

        var genericMethod = CreateNodeQueryableBridgeMethod.MakeGenericMethod(nodeType);
        var result = genericMethod.Invoke(this, []);

        return (IQueryable)(result ?? throw new InvalidOperationException("Failed to create the in-memory node queryable."));
    }

    public IQueryable<TNode> CreateNodeQueryableBridge<TNode>()
        where TNode : Node
    {
        return CreateNodeQueryableCore<TNode>();
    }

    private IQueryable<TNode> CreateNodeQueryableCore<TNode>()
        where TNode : Node
    {
        return Store.GetNodes<TNode>().AsQueryable();
    }

    private IQueryable CreateEdgeQueryable(Type edgeType)
    {
        ArgumentNullException.ThrowIfNull(edgeType);

        var genericMethod = CreateEdgeQueryableBridgeMethod.MakeGenericMethod(edgeType);
        var result = genericMethod.Invoke(this, []);

        return (IQueryable)(result ?? throw new InvalidOperationException("Failed to create the in-memory edge queryable."));
    }

    public IQueryable<TEdge> CreateEdgeQueryableBridge<TEdge>()
        where TEdge : Edge
    {
        return Store.GetEdges<TEdge>().AsQueryable();
    }

    public IQueryable<TNode> ExecuteTraversalBridge<TEdge, TNode>(IQueryable source, GraphTraversalDirection direction, LambdaExpression? predicate)
        where TEdge : Edge
        where TNode : Node
    {
        return ExecuteTraversalCore<TEdge, TNode>(source, direction, predicate);
    }

    private IQueryable<TNode> ExecuteTraversalCore<TEdge, TNode>(IQueryable source, GraphTraversalDirection direction, LambdaExpression? predicate)
        where TEdge : Edge
        where TNode : Node
    {
        return ExecuteTraversalWithEdgesCore<TEdge, TNode>(source, direction, predicate).Select(x => x.Node).AsQueryable();
    }

    internal IQueryable ExecuteTraversalWithEdges(
        IQueryable source,
        Type edgeType,
        Type nodeType,
        GraphTraversalDirection direction,
        LambdaExpression? predicate,
        GraphTraversalSafeties safety)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(edgeType);
        ArgumentNullException.ThrowIfNull(nodeType);

        if (safety != GraphTraversalSafeties.None)
        {
            throw new NotSupportedException($"In-memory traversal {nameof(safety)} flags are not supported yet.");
        }

        var genericMethod = ExecuteTraversalWithEdgesBridgeMethod.MakeGenericMethod(edgeType, nodeType);
        var result = genericMethod.Invoke(this, [source, direction, predicate]);

        return (IQueryable)(result ?? throw new InvalidOperationException("Failed to execute the in-memory traversal with edges."));
    }

    public IQueryable ExecuteTraversalWithEdgesBridge<TEdge, TNode>(IQueryable source, GraphTraversalDirection direction, LambdaExpression? predicate)
        where TEdge : Edge
        where TNode : Node
    {
        return ExecuteTraversalWithEdgesCore<TEdge, TNode>(source, direction, predicate);
    }

    private IQueryable<InMemoryTraversalPair<TEdge, TNode>> ExecuteTraversalWithEdgesCore<TEdge, TNode>(
        IQueryable source,
        GraphTraversalDirection direction,
        LambdaExpression? predicate)
        where TEdge : Edge
        where TNode : Node
    {
        ArgumentNullException.ThrowIfNull(source);

        ValidateTraversalSource(source);

        var sourceNodeIds = source.Cast<Node>().Select(x => x.Id).ToHashSet();

        var targetNodes = Store.GetNodes<TNode>().ToDictionary(x => x.Id);

        var results = GetTraversalPairs(Store.GetEdges<TEdge>(), sourceNodeIds, targetNodes, direction, predicate);

        return results.AsQueryable();
    }

    private static void ValidateTraversalSource(IQueryable source)
    {
        if (!typeof(Node).IsAssignableFrom(source.ElementType))
        {
            throw new NotSupportedException($"Traversal {nameof(source)} element type '{source.ElementType.FullName}' is not a node type.");
        }
    }

    private static List<InMemoryTraversalPair<TEdge, TNode>> GetTraversalPairs<TEdge, TNode>(
        IEnumerable<TEdge> edges,
        HashSet<Guid> sourceNodeIds,
        Dictionary<Guid, TNode> targetNodes,
        GraphTraversalDirection direction,
        LambdaExpression? predicate)
        where TEdge : Edge
        where TNode : Node
    {
        var filteredEdges = ApplyPredicate(edges, predicate);
        var results = new List<InMemoryTraversalPair<TEdge, TNode>>();

        foreach (var edge in filteredEdges)
        {
            var sourceId = direction == GraphTraversalDirection.Outgoing ? edge.FromId : edge.ToId;

            if (!sourceNodeIds.Contains(sourceId))
            {
                continue;
            }

            var targetId = direction == GraphTraversalDirection.Outgoing ? edge.ToId : edge.FromId;

            if (!targetNodes.TryGetValue(targetId, out var targetNode))
            {
                continue;
            }

            results.Add(new InMemoryTraversalPair<TEdge, TNode> { Edge = edge, Node = targetNode });
        }

        return results;
    }

    private static List<TEdge> ApplyPredicate<TEdge>(IEnumerable<TEdge> edges, LambdaExpression? predicate)
        where TEdge : Edge
    {
        if (predicate is null)
        {
            return [.. edges];
        }

        var compiledPredicate = (Func<TEdge, bool>)predicate.Compile();

        return [.. edges.Where(compiledPredicate)];
    }

    private static int PrepareAddedEntities(GraphChangeTracker changeTracker)
    {
        var affectedRows = 0;

        foreach (var entry in changeTracker.Entries.Where(x => x.State == EntityState.Added))
        {
            switch (entry.Entity)
            {
                case Node node:
                    if (node.Id == Guid.Empty)
                    {
                        node.Id = Guid.NewGuid();
                    }

                    SetInitialConcurrencyTokenIfNeeded(node);
                    affectedRows++;
                    break;

                case Edge edge:
                    PrepareAddedEdge(edge, changeTracker);
                    break;
            }
        }

        return affectedRows;
    }

    private int ProcessPendingEdgeConnections(GraphChangeTracker changeTracker)
    {
        var affectedRows = 0;

        foreach (var pendingConnection in changeTracker.PendingEdgeConnections)
        {
            EnsureNodeIdIsSet(pendingConnection.FromNode, "from");
            EnsureNodeIdIsSet(pendingConnection.ToNode, "to");

            var edge = pendingConnection.Edge;

            if (edge.Id == Guid.Empty)
            {
                edge.Id = Guid.NewGuid();
            }

            edge.FromId = pendingConnection.FromNode.Id;
            edge.ToId = pendingConnection.ToNode.Id;

            SetInitialConcurrencyTokenIfNeeded(edge);
            Store.UpsertEdge(edge);

            affectedRows++;
        }

        foreach (var entry in changeTracker.Entries.Where(x => x.State == EntityState.Added && x.Entity is Node))
        {
            Store.UpsertNode((Node)entry.Entity);
        }

        return affectedRows;
    }

    private int ProcessPendingEdgeDisconnections(GraphChangeTracker changeTracker)
    {
        var affectedRows = 0;

        foreach (var pendingDisconnection in changeTracker.PendingEdgeDisconnections)
        {
            EnsureNodeIdIsSet(pendingDisconnection.FromNode, "from");
            EnsureNodeIdIsSet(pendingDisconnection.ToNode, "to");

            affectedRows += Store.RemoveEdgeConnections(pendingDisconnection.EdgeType, pendingDisconnection.FromNode.Id, pendingDisconnection.ToNode.Id);
        }

        return affectedRows;
    }

    private int ProcessModifiedEntities(GraphChangeTracker changeTracker)
    {
        var affectedRows = 0;

        foreach (var entry in changeTracker.Entries.Where(x => x.State == EntityState.Modified))
        {
            switch (entry.Entity)
            {
                case Node node:
                    ApplyConcurrencyForUpdate(entry, node);
                    Store.UpsertNode(node);
                    affectedRows++;
                    break;

                case Edge edge:
                    ApplyConcurrencyForUpdate(entry, edge);
                    Store.UpsertEdge(edge);
                    affectedRows++;
                    break;
            }
        }

        return affectedRows;
    }

    private int ProcessDeletedEntities(GraphChangeTracker changeTracker)
    {
        var affectedRows = 0;

        foreach (var entry in changeTracker.Entries.Where(x => x.State == EntityState.Deleted))
        {
            switch (entry.Entity)
            {
                case Node node:
                    EnsureDeleteConcurrency(entry, node);
                    Store.RemoveConnectedEdges(node);

                    if (Store.RemoveNode(node))
                    {
                        affectedRows++;
                    }

                    break;

                case Edge edge:
                    EnsureDeleteConcurrency(entry, edge);

                    if (Store.RemoveEdge(edge))
                    {
                        affectedRows++;
                    }

                    break;
            }
        }

        return affectedRows;
    }

    private static void PrepareAddedEdge(Edge edge, GraphChangeTracker changeTracker)
    {
        ArgumentNullException.ThrowIfNull(edge);
        ArgumentNullException.ThrowIfNull(changeTracker);

        if (edge.Id == Guid.Empty)
        {
            edge.Id = Guid.NewGuid();
        }

        var hasConnection = changeTracker.PendingEdgeConnections.Any(x => ReferenceEquals(x.Edge, edge));

        if (!hasConnection)
        {
            throw new NotSupportedException("Adding standalone edges without endpoints is not supported; use AddEdge(...) or Connect(...).");
        }

        SetInitialConcurrencyTokenIfNeeded(edge);
    }

    private void ApplyConcurrencyForUpdate(GraphEntityEntry entry, object entity)
    {
        var storedEntity = FindStoredEntity(entity);

        if (entity is not IHasConcurrencyToken concurrencyTracked)
        {
            return;
        }

        if (storedEntity is not IHasConcurrencyToken storedConcurrencyTracked)
        {
            return;
        }

        var originalVersion = GetOriginalVersion(entry, concurrencyTracked.Version);

        if (storedConcurrencyTracked.Version != originalVersion)
        {
            throw new GraphConcurrencyException($"Concurrency conflict while updating '{entity.GetType().Name}' with key '{GetEntityKey(entity)}'.");
        }

        concurrencyTracked.Version = originalVersion + 1;
    }

    private void EnsureDeleteConcurrency(GraphEntityEntry entry, object entity)
    {
        var storedEntity = FindStoredEntity(entity);

        if (entity is not IHasConcurrencyToken concurrencyTracked)
        {
            return;
        }

        if (storedEntity is not IHasConcurrencyToken storedConcurrencyTracked)
        {
            return;
        }

        var originalVersion = GetOriginalVersion(entry, concurrencyTracked.Version);

        if (storedConcurrencyTracked.Version != originalVersion)
        {
            throw new GraphConcurrencyException($"Concurrency conflict while deleting '{entity.GetType().Name}' with key '{GetEntityKey(entity)}'.");
        }
    }

    private object? FindStoredEntity(object entity)
    {
        switch (entity)
        {
            case Node node:
                if (Store.NodesByType.TryGetValue(entity.GetType(), out var nodeBucket) &&
                    nodeBucket.TryGetValue(node.Id, out var storedNode))
                {
                    return storedNode;
                }

                break;

            case Edge edge:
                if (Store.EdgesByType.TryGetValue(entity.GetType(), out var edgeBucket) &&
                    edgeBucket.TryGetValue(edge.Id, out var storedEdge))
                {
                    return storedEdge;
                }

                break;
        }

        return null;
    }

    private static int GetOriginalVersion(GraphEntityEntry entry, int fallbackVersion)
    {
        if (entry.TryGetOriginalValue(nameof(IHasConcurrencyToken.Version), out var originalValue) &&
            originalValue is int originalVersion)
        {
            return originalVersion;
        }

        return fallbackVersion;
    }

    private static object? GetEntityKey(object entity) =>
        entity switch
        {
            Node node => node.Id,
            Edge edge => edge.Id,
            _ => null
        };

    private static void EnsureNodeIdIsSet(Node node, string role)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        if (node.Id == Guid.Empty)
        {
            throw new InvalidOperationException($"The {role} node '{node.GetType().Name}' must have a non-empty Id before connecting or disconnecting edges.");
        }
    }

    private static void SetInitialConcurrencyTokenIfNeeded(object entity)
    {
        if (entity is not IHasConcurrencyToken concurrencyTracked)
        {
            return;
        }

        if (concurrencyTracked.Version <= 0)
        {
            concurrencyTracked.Version = 1;
        }
    }
}