using Gorm.Application.Context;
using Gorm.Application.Execution.InMemory;
using Gorm.Application.History.Storage;
using Gorm.Application.Tracking;
using Gorm.Core.Primitives;

namespace Gorm.Application.History.Envelopes;

/// <summary>
/// Collects history envelopes from the current graph change tracker.
/// </summary>
internal static class GraphHistoryEnvelopeCollector
{
    /// <summary>
    /// Collects history envelopes for the current pending changes.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="changeTracker">The change tracker.</param>
    /// <param name="capturedAtUtc">The capture timestamp.</param>
    /// <returns>The collected history envelopes.</returns>
    public static IReadOnlyList<GraphHistoryEnvelope> Collect(GraphContext context, GraphChangeTracker changeTracker, DateTime capturedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(changeTracker);

        var entries = new List<GraphHistoryEnvelope>();

        entries.AddRange(CaptureEntityEntries(context, changeTracker, capturedAtUtc));
        entries.AddRange(CapturePendingConnections(context, changeTracker, capturedAtUtc));
        entries.AddRange(CapturePendingDisconnections(context, changeTracker, capturedAtUtc));

        return entries;
    }

    private static IEnumerable<GraphHistoryEnvelope> CaptureEntityEntries(GraphContext context, GraphChangeTracker changeTracker, DateTime capturedAtUtc)
    {
        foreach (var entry in changeTracker.Entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    foreach (var envelope in CaptureAddedEntry(context, entry, capturedAtUtc))
                    {
                        yield return envelope;
                    }

                    break;

                case EntityState.Modified:
                    foreach (var envelope in CaptureModifiedEntry(context, entry, capturedAtUtc))
                    {
                        yield return envelope;
                    }

                    break;

                case EntityState.Deleted:
                    foreach (var envelope in CaptureDeletedEntry(context, entry, capturedAtUtc))
                    {
                        yield return envelope;
                    }

                    break;
            }
        }
    }

    private static IEnumerable<GraphHistoryEnvelope> CaptureAddedEntry(GraphContext context, GraphEntityEntry entry, DateTime capturedAtUtc)
    {
        switch (entry.Entity)
        {
            case Node node:
                yield return GraphHistoryEnvelope.ForNode(CloneNode(context, node), GraphHistoryOperationKind.Created, capturedAtUtc, capturedAtUtc, null);
                break;

            case Edge edge:
                if (edge.FromId != Guid.Empty && edge.ToId != Guid.Empty)
                {
                    yield return GraphHistoryEnvelope.ForEdge(CloneEdge(context, edge), GraphHistoryOperationKind.Created, capturedAtUtc, capturedAtUtc, null);
                }

                break;
        }
    }

    private static IEnumerable<GraphHistoryEnvelope> CaptureModifiedEntry(GraphContext context, GraphEntityEntry entry, DateTime capturedAtUtc)
    {
        var storedEntity = FindStoredEntity(context, entry.Entity);

        switch (entry.Entity)
        {
            case Node node:
                if (storedEntity is Node storedNode)
                {
                    yield return GraphHistoryEnvelope.ForNode(CloneNode(context, storedNode), GraphHistoryOperationKind.Updated, capturedAtUtc, capturedAtUtc, capturedAtUtc);
                }

                yield return GraphHistoryEnvelope.ForNode(CloneNode(context, node), GraphHistoryOperationKind.Updated, capturedAtUtc, capturedAtUtc, null);
                break;

            case Edge edge:
                if (storedEntity is Edge storedEdge)
                {
                    yield return GraphHistoryEnvelope.ForEdge(CloneEdge(context, storedEdge), GraphHistoryOperationKind.Updated, capturedAtUtc, capturedAtUtc, capturedAtUtc);
                }

                yield return GraphHistoryEnvelope.ForEdge(CloneEdge(context, edge), GraphHistoryOperationKind.Updated, capturedAtUtc, capturedAtUtc, null);
                break;
        }
    }

    private static IEnumerable<GraphHistoryEnvelope> CaptureDeletedEntry(GraphContext context, GraphEntityEntry entry, DateTime capturedAtUtc)
    {
        var storedEntity = FindStoredEntity(context, entry.Entity);

        switch (entry.Entity)
        {
            case Node node:
                yield return GraphHistoryEnvelope.ForNode(
                    storedEntity as Node is { } storedNode ? CloneNode(context, storedNode) : CloneNode(context, node),
                    GraphHistoryOperationKind.Deleted,
                    capturedAtUtc,
                    capturedAtUtc,
                    capturedAtUtc);
                break;

            case Edge edge:
                var storedEdge = storedEntity as Edge ?? FindRecordedEdge(context, edge);
                var snapshot = CloneEdge(context, storedEdge ?? edge);
                if (snapshot.FromId != Guid.Empty && snapshot.ToId != Guid.Empty)
                {
                    yield return GraphHistoryEnvelope.ForEdge(snapshot, GraphHistoryOperationKind.Deleted, capturedAtUtc, capturedAtUtc, capturedAtUtc);
                }
                break;
        }
    }

    private static IEnumerable<GraphHistoryEnvelope> CapturePendingConnections(GraphContext context, GraphChangeTracker changeTracker, DateTime capturedAtUtc)
    {
        foreach (var pendingConnection in changeTracker.PendingEdgeConnections)
        {
            var edge = CloneEdge(context, pendingConnection.Edge);

            if (edge.Id == Guid.Empty)
            {
                edge.Id = Guid.NewGuid();
            }

            edge.FromId = pendingConnection.FromNode.Id;
            edge.ToId = pendingConnection.ToNode.Id;

            yield return GraphHistoryEnvelope.ForEdge(edge, GraphHistoryOperationKind.Connected, capturedAtUtc, capturedAtUtc, null);
        }
    }

    private static IEnumerable<GraphHistoryEnvelope> CapturePendingDisconnections(GraphContext context, GraphChangeTracker changeTracker, DateTime capturedAtUtc)
    {
        foreach (var pendingDisconnection in changeTracker.PendingEdgeDisconnections)
        {
            foreach (var edge in FindMatchingEdges(context, pendingDisconnection.EdgeType, pendingDisconnection.FromNode.Id, pendingDisconnection.ToNode.Id))
            {
                yield return GraphHistoryEnvelope.ForEdge(CloneEdge(context, edge), GraphHistoryOperationKind.Disconnected, capturedAtUtc, capturedAtUtc, capturedAtUtc);
            }
        }
    }

    private static IEnumerable<Edge> FindMatchingEdges(GraphContext context, Type edgeType, Guid fromId, Guid toId)
    {
        if (context.ExecutionEngine is InMemoryGraphExecutionEngine engine)
        {
            if (engine.Store.EdgesByType.TryGetValue(edgeType, out var bucket))
            {
                foreach (var edge in bucket.Values)
                {
                    if (edge.FromId == fromId && edge.ToId == toId) yield return edge;
                }
            }
            yield break;
        }

        if (SqlServerGraphHistoryReaderRegistry.TryGet(context, out var reader))
        {
            foreach (var state in reader.ReadActiveEdgeStates(edgeType, fromId, toId))
            {
                if (state.Snapshot is Edge edge) yield return edge;
            }
        }
    }

    private static Edge? FindRecordedEdge(GraphContext context, Edge edge)
    {
        if (edge.Id == Guid.Empty || !SqlServerGraphHistoryReaderRegistry.TryGet(context, out var reader)) return null;
        return reader.ReadActiveEdgeStates(edge.GetType(), entityId: edge.Id).SingleOrDefault()?.Snapshot as Edge;
    }

    private static object? FindStoredEntity(GraphContext context, object entity)
    {
        if (context.ExecutionEngine is not InMemoryGraphExecutionEngine inMemoryExecutionEngine)
        {
            return null;
        }

        return entity switch
        {
            Node node when inMemoryExecutionEngine.Store.NodesByType.TryGetValue(entity.GetType(), out var nodeBucket) &&
                           nodeBucket.TryGetValue(node.Id, out var storedNode) => storedNode,
            Edge edge when inMemoryExecutionEngine.Store.EdgesByType.TryGetValue(entity.GetType(), out var edgeBucket) &&
                           edgeBucket.TryGetValue(edge.Id, out var storedEdge) => storedEdge,
            _ => null
        };
    }

    private static Node CloneNode(GraphContext context, Node node)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(node);
        var mapping = context.Model.GetNode(node.GetType());
        return (Node)GraphHistorySnapshotCloner.Clone(node, node.GetType(), mapping.Properties);
    }

    private static Edge CloneEdge(GraphContext context, Edge edge)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(edge);
        var mapping = context.Model.GetEdge(edge.GetType());
        return (Edge)GraphHistorySnapshotCloner.Clone(edge, edge.GetType(), mapping.Properties);
    }
}