using Gorm.Application.History.Envelopes;
using Gorm.Core.Primitives;

namespace Gorm.Application.History.Abstractions;

/// <summary>
/// Resolves temporal graph state from history envelopes.
/// </summary>
public interface IGraphTemporalResolver
{
    /// <summary>
    /// Resolves the latest active history envelopes at the specified instant.
    /// </summary>
    /// <param name="history">The history envelopes.</param>
    /// <param name="instantUtc">The instant in UTC.</param>
    /// <returns>The resolved envelopes.</returns>
    public IReadOnlyList<GraphHistoryEnvelope> ResolveAsOf(IEnumerable<GraphHistoryEnvelope> history, DateTime instantUtc);

    /// <summary>
    /// Resolves the current active history envelopes.
    /// </summary>
    /// <param name="history">The history envelopes.</param>
    /// <returns>The resolved envelopes.</returns>
    public IReadOnlyList<GraphHistoryEnvelope> ResolveCurrent(IEnumerable<GraphHistoryEnvelope> history);

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
        where TEdge : Edge;

    /// <summary>
    /// Resolves the latest active node state at the specified instant.
    /// </summary>
    /// <typeparam name="TNode">The node type.</typeparam>
    /// <param name="nodeHistory">The node history.</param>
    /// <param name="nodeId">The node identifier.</param>
    /// <param name="instantUtc">The instant in UTC.</param>
    /// <returns>The resolved envelope, or null when no active state exists.</returns>
    public GraphHistoryEnvelope? ResolveNodeAtInstant<TNode>(IEnumerable<GraphHistoryEnvelope> nodeHistory, Guid nodeId, DateTime instantUtc)
        where TNode : Node;
}