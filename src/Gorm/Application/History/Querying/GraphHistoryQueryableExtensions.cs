using Gorm.Application.Context;
using Gorm.Application.History.Envelopes;
using Gorm.Application.History.Recording;
using Gorm.Application.History.Storage;
using Gorm.Core.Primitives;

namespace Gorm.Application.History.Querying;

/// <summary>
/// Provides history query extensions for graph contexts.
/// </summary>
public static class GraphHistoryQueryableExtensions
{
    /// <summary>
    /// Gets the history queryable for the specified node type.
    /// </summary>
    /// <typeparam name="TNode">The node type.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <returns>The history queryable.</returns>
    public static IQueryable<GraphHistoryEnvelope> History<TNode>(this GraphContext context)
        where TNode : Node
    {
        ArgumentNullException.ThrowIfNull(context);

        if (TryGetInMemoryHistory(context, out var inMemoryHistory))
        {
            return inMemoryHistory.Entries.Where(x => !x.IsEdge && x.EntityType == typeof(TNode)).AsQueryable();
        }

        if (SqlServerGraphHistoryReaderRegistry.TryGet(context, out var sqlReader))
        {
            var entries = sqlReader.ReadNodeHistory<TNode>();

            return entries.AsQueryable();
        }

        throw new NotSupportedException("History queries are supported only when using InMemoryGraphHistoryRecorder or UseSqlServerHistory(...).");
    }

    /// <summary>
    /// Gets the history queryable for the specified edge type.
    /// </summary>
    /// <typeparam name="TEdge">The edge type.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <returns>The history queryable.</returns>
    public static IQueryable<GraphHistoryEnvelope> EdgeHistory<TEdge>(this GraphContext context)
        where TEdge : Edge
    {
        ArgumentNullException.ThrowIfNull(context);

        if (TryGetInMemoryHistory(context, out var inMemoryHistory))
        {
            return inMemoryHistory.Entries.Where(x => x.IsEdge && x.EntityType == typeof(TEdge)).AsQueryable();
        }

        if (SqlServerGraphHistoryReaderRegistry.TryGet(context, out var sqlReader))
        {
            var entries = sqlReader.ReadEdgeHistory<TEdge>();

            return entries.AsQueryable();
        }

        throw new NotSupportedException("History queries are supported only when using InMemoryGraphHistoryRecorder or UseSqlServerHistory(...).");
    }

    /// <summary>
    /// Gets typed node snapshots from history entries.
    /// </summary>
    /// <typeparam name="TNode">The node type.</typeparam>
    /// <param name="history">The history queryable.</param>
    /// <returns>The node snapshots.</returns>
    public static IQueryable<TNode> SelectNodeSnapshot<TNode>(this IQueryable<GraphHistoryEnvelope> history)
        where TNode : Node
    {
        ArgumentNullException.ThrowIfNull(history);

        return history.Select(x => (TNode)x.Snapshot);
    }

    /// <summary>
    /// Gets typed edge snapshots from history entries.
    /// </summary>
    /// <typeparam name="TEdge">The edge type.</typeparam>
    /// <param name="history">The history queryable.</param>
    /// <returns>The edge snapshots.</returns>
    public static IQueryable<TEdge> SelectEdgeSnapshot<TEdge>(this IQueryable<GraphHistoryEnvelope> history)
        where TEdge : Edge
    {
        ArgumentNullException.ThrowIfNull(history);

        return history.Select(x => (TEdge)x.Snapshot);
    }

    private static bool TryGetInMemoryHistory(GraphContext context, out InMemoryGraphHistoryStore historyStore)
    {
        if (context.HistoryRecorder is InMemoryGraphHistoryRecorder recorder)
        {
            historyStore = recorder.HistoryStore;
            return true;
        }

        historyStore = null!;
        return false;
    }
}