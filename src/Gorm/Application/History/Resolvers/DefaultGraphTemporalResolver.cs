using Gorm.Application.History.Abstractions;
using Gorm.Application.History.Configuration;
using Gorm.Application.History.Envelopes;
using Gorm.Core.Primitives;

namespace Gorm.Application.History.Resolvers;

/// <summary>
/// Default implementation of temporal graph state resolution.
/// </summary>
public sealed class DefaultGraphTemporalResolver : IGraphTemporalResolver
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultGraphTemporalResolver"/> class.
    /// </summary>
    /// <param name="options">The history options.</param>
    public DefaultGraphTemporalResolver(GraphHistoryOptions? options = null)
    {
        Options = options ?? new GraphHistoryOptions();
    }

    /// <summary>
    /// Gets the configured history options.
    /// </summary>
    public GraphHistoryOptions Options { get; }

    /// <summary>
    /// Resolves the latest active history envelopes at the specified instant.
    /// </summary>
    /// <param name="history">The history envelopes.</param>
    /// <param name="instantUtc">The instant in UTC.</param>
    /// <returns>The resolved envelopes.</returns>
    public IReadOnlyList<GraphHistoryEnvelope> ResolveAsOf(IEnumerable<GraphHistoryEnvelope> history, DateTime instantUtc)
    {
        var utcInstant = EnsureUtc(instantUtc);

        return
        [
            .. history
                .Where(x => x.CapturedAtUtc <= utcInstant)
                .GroupBy(GetLogicalGroupKey)
                .Select(SelectLatest)
                .Where(x => x is not null)
                .Select(x => CopyWithQueryAsOfUtc(x!, utcInstant))
        ];
    }

    /// <summary>
    /// Resolves the current active history envelopes.
    /// </summary>
    /// <param name="history">The history envelopes.</param>
    /// <returns>The resolved envelopes.</returns>
    public IReadOnlyList<GraphHistoryEnvelope> ResolveCurrent(IEnumerable<GraphHistoryEnvelope> history) =>
    [
        .. history
            .GroupBy(GetLogicalGroupKey)
            .Select(SelectLatest)
            .Where(x => x is not null)!
    ];

    /// <summary>
    /// Resolves the latest active edge states at the specified instant for the given source node.
    /// </summary>
    /// <typeparam name="TEdge">The edge type.</typeparam>
    /// <param name="edgeHistory">The edge history.</param>
    /// <param name="instantUtc">The instant in UTC.</param>
    /// <param name="sourceNodeId">The source node identifier.</param>
    /// <param name="outgoing">True for outgoing traversal; otherwise false.</param>
    /// <returns>The resolved edge snapshots.</returns>
    public IReadOnlyList<TEdge> ResolveActiveEdgesAtInstant<TEdge>(IEnumerable<GraphHistoryEnvelope> edgeHistory, DateTime instantUtc, Guid sourceNodeId, bool outgoing)
        where TEdge : Edge
    {
        var utcInstant = EnsureUtc(instantUtc);

        return
        [
            .. edgeHistory
                .Where(x => x.IsEdge && x.EntityType == typeof(TEdge) && x.CapturedAtUtc <= utcInstant &&(outgoing ? x.FromId == sourceNodeId : x.ToId == sourceNodeId))
                .GroupBy(x => new { x.EntityType, x.EntityId })
                .Select(SelectLatest)
                .Where(x => x is not null && IsEdgeActive(x))
                .Select(x => x!.GetSnapshotOfType<TEdge>())
        ];
    }

    /// <summary>
    /// Resolves the latest active node state at the specified instant.
    /// </summary>
    /// <typeparam name="TNode">The node type.</typeparam>
    /// <param name="nodeHistory">The node history.</param>
    /// <param name="nodeId">The node identifier.</param>
    /// <param name="instantUtc">The instant in UTC.</param>
    /// <returns>The resolved envelope, or null when no active state exists.</returns>
    public GraphHistoryEnvelope? ResolveNodeAtInstant<TNode>(IEnumerable<GraphHistoryEnvelope> nodeHistory, Guid nodeId, DateTime instantUtc)
        where TNode : Node
    {
        var utcInstant = EnsureUtc(instantUtc);

        var latest = nodeHistory
            .Where(x => !x.IsEdge && x.EntityType == typeof(TNode) && x.EntityId == nodeId && x.CapturedAtUtc <= utcInstant)
            .OrderByDescending(x => x.CapturedAtUtc)
            .ThenBy(x => x.ValidToUtc.HasValue)
            .ThenByDescending(x => x.ValidFromUtc)
            .FirstOrDefault();

        return latest is null || !IsNodeActive(latest) ? null : CopyWithQueryAsOfUtc(latest, utcInstant);
    }

    private GraphHistoryEnvelope? SelectLatest(IEnumerable<GraphHistoryEnvelope> group)
    {
        var latest = group.OrderByDescending(x => x.CapturedAtUtc).ThenBy(x => x.ValidToUtc.HasValue).ThenByDescending(x => x.ValidFromUtc).FirstOrDefault();

        if (latest is null)
        {
            return null;
        }

        if (latest.IsEdge)
        {
            return IsEdgeActive(latest) ? latest : null;
        }

        return IsNodeActive(latest) ? latest : null;
    }

    private bool IsEdgeActive(GraphHistoryEnvelope envelope) =>
        envelope.OperationKind switch
        {
            GraphHistoryOperationKind.Disconnected when Options.DisconnectedEdgesAreInactive => false,
            GraphHistoryOperationKind.Deleted when Options.DeletedEdgesAreInactive => false,
            _ => true
        };

    private bool IsNodeActive(GraphHistoryEnvelope envelope) =>
        !Options.DeletedNodesAreInactive || envelope.OperationKind is not GraphHistoryOperationKind.Deleted;

    private static object GetLogicalGroupKey(GraphHistoryEnvelope envelope) =>
        new { envelope.EntityType, envelope.EntityId };

    private static GraphHistoryEnvelope CopyWithQueryAsOfUtc(GraphHistoryEnvelope envelope, DateTime queryAsOfUtc) =>
        new()
        {
            EntityType = envelope.EntityType,
            EntityId = envelope.EntityId,
            OperationKind = envelope.OperationKind,
            CapturedAtUtc = envelope.CapturedAtUtc,
            ValidFromUtc = envelope.ValidFromUtc,
            ValidToUtc = envelope.ValidToUtc,
            IsEdge = envelope.IsEdge,
            FromId = envelope.FromId,
            ToId = envelope.ToId,
            Snapshot = envelope.Snapshot,
            QueryAsOfUtc = queryAsOfUtc
        };

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}