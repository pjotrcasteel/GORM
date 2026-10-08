using Gorm.Application.Context;
using Gorm.Application.History.Envelopes;
using Gorm.Application.History.Recording;
using Gorm.Core.Primitives;

namespace Gorm.Application.History.Querying;

/// <summary>
/// Provides temporal graph traversal extensions over historical snapshots.
/// </summary>
public static class GraphTemporalTraversalExtensions
{
    public static IQueryable<TNode> TemporalOutgoing<TEdge, TNode>(this IQueryable<GraphHistoryEnvelope> history, GraphContext context)
        where TEdge : Edge
        where TNode : Node
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(context);

        return TraverseWithEdges<TEdge, TNode>(history, context, outgoing: true).Select(x => x.Node).AsQueryable();
    }

    public static IQueryable<TNode> TemporalIncoming<TEdge, TNode>(this IQueryable<GraphHistoryEnvelope> history, GraphContext context)
        where TEdge : Edge
        where TNode : Node
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(context);

        return TraverseWithEdges<TEdge, TNode>(history, context, outgoing: false).Select(x => x.Node).AsQueryable();
    }

    public static IQueryable<TResult> TemporalSelectWithEdge<TEdge, TNode, TResult>(
        this IQueryable<GraphHistoryEnvelope> history,
        GraphContext context,
        Func<TNode, TEdge, TResult> selector)
        where TEdge : Edge
        where TNode : Node
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(selector);

        return TraverseWithEdges<TEdge, TNode>(history, context, outgoing: true).Select(x => selector(x.Node, x.Edge)).AsQueryable();
    }

    public static IQueryable<TResult> TemporalSelectIncomingWithEdge<TEdge, TNode, TResult>(
        this IQueryable<GraphHistoryEnvelope> history,
        GraphContext context,
        Func<TNode, TEdge, TResult> selector)
        where TEdge : Edge
        where TNode : Node
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(selector);

        return TraverseWithEdges<TEdge, TNode>(history, context, outgoing: false).Select(x => selector(x.Node, x.Edge)).AsQueryable();
    }

    private static IQueryable<GraphTemporalTraversalResult<TEdge, TNode>> TraverseWithEdges<TEdge, TNode>(
        IQueryable<GraphHistoryEnvelope> sourceHistory,
        GraphContext context,
        bool outgoing)
        where TEdge : Edge
        where TNode : Node
    {
        var sourceEntries = sourceHistory.ToList();

        if (sourceEntries.Count == 0)
        {
            return Enumerable.Empty<GraphTemporalTraversalResult<TEdge, TNode>>().AsQueryable();
        }

        EnsureInMemoryHistory(context);

        var edgeHistory = context.EdgeHistory<TEdge>().ToList();
        var nodeHistory = context.History<TNode>().ToList();

        var results = new List<GraphTemporalTraversalResult<TEdge, TNode>>();

        foreach (var sourceEntry in sourceEntries)
        {
            var sourceNode = sourceEntry.GetSnapshotOfType<Node>();
            var sourceInstant = sourceEntry.QueryAsOfUtc ?? sourceEntry.CapturedAtUtc;

            var activeEdges = context.TemporalResolver.ResolveActiveEdgesAtInstant<TEdge>(edgeHistory, sourceInstant, sourceNode.Id, outgoing);

            foreach (var edge in activeEdges)
            {
                var targetNodeId = outgoing ? edge.ToId : edge.FromId;

                var targetEntry = context.TemporalResolver.ResolveNodeAtInstant<TNode>(nodeHistory, targetNodeId, sourceInstant);

                if (targetEntry is null)
                {
                    continue;
                }

                results.Add(new GraphTemporalTraversalResult<TEdge, TNode>
                {
                    Node = targetEntry.GetSnapshotOfType<TNode>(),
                    Edge = edge
                });
            }
        }

        return results.AsQueryable();
    }

    private static void EnsureInMemoryHistory(GraphContext context)
    {
        if (context.HistoryRecorder is not InMemoryGraphHistoryRecorder)
        {
            throw new NotSupportedException("TemporalOutgoing/TemporalIncoming/TemporalSelectWithEdge currently support only in-memory history.");
        }
    }
}