using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Projection;

/// <summary>
/// Safely applies ordered graph deltas to immutable projection snapshots.
/// </summary>
public static class GraphProjectionUpdater
{
    /// <summary>
    /// Applies a compatible delta and returns a new snapshot without modifying the previous one.
    /// </summary>
    public static GraphProjectionUpdateResult Apply(GraphProjectionSnapshot snapshot, GraphProjectionDelta delta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(delta);
        cancellationToken.ThrowIfCancellationRequested();

        EnsureCompatible(snapshot, delta);
        var update = BuildIncrementalProjection(snapshot.Projection, delta, cancellationToken);

        return new GraphProjectionUpdateResult
        {
            BaseVersion = snapshot.Version,
            Mode = GraphProjectionUpdateMode.Incremental,
            Snapshot = new GraphProjectionSnapshot(
                snapshot.Key,
                delta.TargetVersion,
                update.Projection,
                GraphProjectionSnapshotMetadata.Create(snapshot.Key, delta.TargetVersion, update.Projection, origin: "delta", parentVersion: snapshot.Version)),
            AffectedNodeIds = update.AffectedNodeIds,
            AffectedEdgeIds = update.AffectedEdgeIds,
            Explanation =
                $"Applied projection delta '{delta.Key}' from version {delta.BaseVersion} to {delta.TargetVersion}; " +
                $"{update.AffectedNodeIds.Length} node(s) and {update.AffectedEdgeIds.Length} edge(s) require invalidation."
        };
    }

    /// <summary>
    /// Applies a compatible delta, or explicitly rebuilds when the delta is unsafe for the supplied snapshot.
    /// </summary>
    public static Task<GraphProjectionUpdateResult> ApplyOrRebuildAsync(
        GraphProjectionSnapshot snapshot,
        GraphProjectionDelta delta,
        Func<CancellationToken, Task<GraphProjectionSnapshot>> rebuildFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(delta);
        ArgumentNullException.ThrowIfNull(rebuildFactory);
        return ApplyOrRebuildCoreAsync(snapshot, delta, rebuildFactory, cancellationToken);
    }

    private static async Task<GraphProjectionUpdateResult> ApplyOrRebuildCoreAsync(
        GraphProjectionSnapshot snapshot,
        GraphProjectionDelta delta,
        Func<CancellationToken, Task<GraphProjectionSnapshot>> rebuildFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            return Apply(snapshot, delta, cancellationToken);
        }
        catch (GraphProjectionDeltaException rejection)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rebuilt = await rebuildFactory(cancellationToken).ConfigureAwait(false);
            ValidateRebuiltSnapshot(delta, rebuilt, rejection);

            return new GraphProjectionUpdateResult
            {
                BaseVersion = snapshot.Version,
                Mode = GraphProjectionUpdateMode.FullRebuild,
                Snapshot = rebuilt,
                AffectedNodeIds = [.. rebuilt.Projection.Nodes.Select(node => node.Id).Order()],
                AffectedEdgeIds = [.. rebuilt.Projection.Edges.Select(edge => edge.Id).Order()],
                Explanation =
                    $"Rejected incremental delta '{delta.Key}' because {rejection.Message} " +
                    $"Used an explicit full rebuild at version {rebuilt.Version}."
            };
        }
    }

    private static void EnsureCompatible(GraphProjectionSnapshot snapshot, GraphProjectionDelta delta)
    {
        if (snapshot.Key != delta.Key)
        {
            throw new GraphProjectionDeltaException(
                GraphProjectionDeltaFailureReason.ProjectionKeyMismatch,
                $"it targets key '{delta.Key}', while the {nameof(snapshot)} key is '{snapshot.Key}'.");
        }

        if (snapshot.Version != delta.BaseVersion)
        {
            throw new GraphProjectionDeltaException(
                GraphProjectionDeltaFailureReason.VersionMismatch,
                $"it requires base version {delta.BaseVersion}, while the {nameof(snapshot)} version is {snapshot.Version}.");
        }
    }

    private static IncrementalProjection BuildIncrementalProjection(GraphProjection projection, GraphProjectionDelta delta, CancellationToken cancellationToken)
    {
        var currentNodes = projection.Nodes.ToDictionary(node => node.Id);
        var currentEdges = projection.Edges.ToDictionary(edge => edge.Id);
        ValidateEntityStates(currentNodes, currentEdges, delta);

        var nodeUpdate = BuildNodes(projection, delta, currentNodes.Count, cancellationToken);
        var affectedEdgeIds = new HashSet<Guid>(delta.RemovedEdgeIds);
        var edges = BuildEdges(projection, delta, currentEdges.Count, nodeUpdate, affectedEdgeIds, cancellationToken);
        return CreateIncrementalProjection(nodeUpdate.Nodes, edges, nodeUpdate.AffectedNodeIds, affectedEdgeIds);
    }

    private static NodeUpdate BuildNodes(GraphProjection projection, GraphProjectionDelta delta, int currentNodeCount, CancellationToken cancellationToken)
    {
        var updatedNodes = delta.UpdatedNodes.ToDictionary(node => node.Id);
        var removedNodeIds = delta.RemovedNodeIds.ToHashSet();
        var nodes = new List<Node>(currentNodeCount - removedNodeIds.Count + delta.AddedNodes.Count);
        var affectedNodeIds = new HashSet<Guid>(removedNodeIds);

        foreach (var node in projection.Nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (removedNodeIds.Contains(node.Id))
            {
                continue;
            }

            if (updatedNodes.TryGetValue(node.Id, out var replacement))
            {
                nodes.Add(replacement);
                affectedNodeIds.Add(node.Id);
            }
            else
            {
                nodes.Add(node);
            }
        }

        foreach (var node in delta.AddedNodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            nodes.Add(node);
            affectedNodeIds.Add(node.Id);
        }

        return new NodeUpdate(nodes, [.. nodes.Select(node => node.Id)], removedNodeIds, affectedNodeIds);
    }

    private static List<Edge> BuildEdges(
        GraphProjection projection,
        GraphProjectionDelta delta,
        int currentEdgeCount,
        NodeUpdate nodeUpdate,
        HashSet<Guid> affectedEdgeIds,
        CancellationToken cancellationToken)
    {
        var updatedEdges = delta.UpdatedEdges.ToDictionary(edge => edge.Id);
        var explicitlyRemovedEdgeIds = delta.RemovedEdgeIds.ToHashSet();
        var edges = new List<Edge>(currentEdgeCount + delta.AddedEdges.Count);

        foreach (var edge in projection.Edges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProcessExistingEdge(edge, updatedEdges, explicitlyRemovedEdgeIds, nodeUpdate, affectedEdgeIds, edges);
        }

        foreach (var edge in delta.AddedEdges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureEndpointsExist(edge, nodeUpdate.FinalNodeIds);
            edges.Add(edge);
            MarkAffectedEdge(edge, affectedEdgeIds, nodeUpdate.AffectedNodeIds);
        }

        return edges;
    }

    private static void ProcessExistingEdge(
        Edge edge,
        Dictionary<Guid, Edge> updatedEdges,
        HashSet<Guid> explicitlyRemovedEdgeIds,
        NodeUpdate nodeUpdate,
        HashSet<Guid> affectedEdgeIds,
        List<Edge> edges)
    {
        if (explicitlyRemovedEdgeIds.Contains(edge.Id))
        {
            MarkAffectedEdge(edge, affectedEdgeIds, nodeUpdate.AffectedNodeIds);
            return;
        }

        if (updatedEdges.TryGetValue(edge.Id, out var replacement))
        {
            EnsureEndpointsExist(replacement, nodeUpdate.FinalNodeIds);
            edges.Add(replacement);
            MarkAffectedEdge(edge, affectedEdgeIds, nodeUpdate.AffectedNodeIds);
            nodeUpdate.AffectedNodeIds.Add(replacement.FromId);
            nodeUpdate.AffectedNodeIds.Add(replacement.ToId);
            return;
        }

        if (nodeUpdate.RemovedNodeIds.Contains(edge.FromId) ||
            nodeUpdate.RemovedNodeIds.Contains(edge.ToId))
        {
            MarkAffectedEdge(edge, affectedEdgeIds, nodeUpdate.AffectedNodeIds);
            return;
        }

        edges.Add(edge);
    }

    private static void MarkAffectedEdge(Edge edge, HashSet<Guid> affectedEdgeIds, HashSet<Guid> affectedNodeIds)
    {
        affectedEdgeIds.Add(edge.Id);
        affectedNodeIds.Add(edge.FromId);
        affectedNodeIds.Add(edge.ToId);
    }

    private static IncrementalProjection CreateIncrementalProjection(List<Node> nodes, List<Edge> edges, HashSet<Guid> affectedNodeIds, HashSet<Guid> affectedEdgeIds)
    {
        try
        {
            var updatedProjection = GraphProjection.Create(nodes, edges);
            return new IncrementalProjection(updatedProjection, [.. affectedNodeIds.Order()], [.. affectedEdgeIds.Order()]);
        }
        catch (ArgumentException exception)
        {
            throw new GraphProjectionDeltaException(
                GraphProjectionDeltaFailureReason.EntityStateMismatch,
                "the resulting graph projection failed structural validation.",
                exception);
        }
    }

    private static void ValidateEntityStates(IReadOnlyDictionary<Guid, Node> currentNodes, IReadOnlyDictionary<Guid, Edge> currentEdges, GraphProjectionDelta delta)
    {
        foreach (var node in delta.AddedNodes)
        {
            EnsureEntityAbsent(currentNodes, node.Id, "node", "add");
        }

        foreach (var node in delta.UpdatedNodes)
        {
            EnsureEntityPresent(currentNodes, node.Id, "node", "update");
        }

        foreach (var nodeId in delta.RemovedNodeIds)
        {
            EnsureEntityPresent(currentNodes, nodeId, "node", "remove");
        }

        foreach (var edge in delta.AddedEdges)
        {
            EnsureEntityAbsent(currentEdges, edge.Id, "edge", "add");
        }

        foreach (var edge in delta.UpdatedEdges)
        {
            EnsureEntityPresent(currentEdges, edge.Id, "edge", "update");
        }

        foreach (var edgeId in delta.RemovedEdgeIds)
        {
            EnsureEntityPresent(currentEdges, edgeId, "edge", "remove");
        }
    }

    private static void EnsureEntityPresent<TEntity>(IReadOnlyDictionary<Guid, TEntity> entities, Guid identifier, string entityKind, string operation)
    {
        if (!entities.ContainsKey(identifier))
        {
            throw new GraphProjectionDeltaException(
                GraphProjectionDeltaFailureReason.EntityStateMismatch,
                $"it cannot {operation} {entityKind} '{identifier}' because that {nameof(identifier)} is absent.");
        }
    }

    private static void EnsureEntityAbsent<TEntity>(IReadOnlyDictionary<Guid, TEntity> entities, Guid identifier, string entityKind, string operation)
    {
        if (entities.ContainsKey(identifier))
        {
            throw new GraphProjectionDeltaException(
                GraphProjectionDeltaFailureReason.EntityStateMismatch,
                $"it cannot {operation} {entityKind} '{identifier}' because that {nameof(identifier)} already exists.");
        }
    }

    private static void EnsureEndpointsExist(Edge edge, HashSet<Guid> nodeIds)
    {
        if (!nodeIds.Contains(edge.FromId) || !nodeIds.Contains(edge.ToId))
        {
            var missingEndpoint = nodeIds.Contains(edge.FromId) ? edge.ToId : edge.FromId;
            throw new GraphProjectionDeltaException(
                GraphProjectionDeltaFailureReason.InvalidEndpoint,
                $"edge '{edge.Id}' refers to missing endpoint '{missingEndpoint}' after applying the delta.");
        }
    }

    private static void ValidateRebuiltSnapshot(GraphProjectionDelta delta, GraphProjectionSnapshot? rebuilt, GraphProjectionDeltaException rejection)
    {
        if (rebuilt is null)
        {
            throw new GraphProjectionDeltaException(GraphProjectionDeltaFailureReason.RebuildRejected, "The full-rebuild factory returned no projection snapshot.", rejection);
        }

        if (rebuilt.Key != delta.Key)
        {
            throw new GraphProjectionDeltaException(
                GraphProjectionDeltaFailureReason.RebuildRejected,
                $"The full-rebuild snapshot key '{rebuilt.Key}' does not match delta key '{delta.Key}'.",
                rejection);
        }

        if (rebuilt.Version < delta.TargetVersion)
        {
            throw new GraphProjectionDeltaException(
                GraphProjectionDeltaFailureReason.RebuildRejected,
                $"The full-rebuild version {rebuilt.Version} is older than required target version {delta.TargetVersion}.",
                rejection);
        }
    }

    private sealed record NodeUpdate(List<Node> Nodes, HashSet<Guid> FinalNodeIds, HashSet<Guid> RemovedNodeIds, HashSet<Guid> AffectedNodeIds);

    private sealed record IncrementalProjection(GraphProjection Projection, Guid[] AffectedNodeIds, Guid[] AffectedEdgeIds);
}