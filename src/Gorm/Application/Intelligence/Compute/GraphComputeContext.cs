using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Application.Intelligence.Projection;
using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Compute;

/// <summary>
/// Provides node state, topology and message operations to a vertex compute program.
/// </summary>
public sealed class GraphComputeContext<TState, TMessage>
{
    private readonly GraphProjection _projection;
    private readonly int _nodeIndex;
    private readonly ICollection<GraphComputeMessage<TMessage>> _messages;

    internal GraphComputeContext(GraphProjection projection, int nodeIndex, TState state, int superstep, ICollection<GraphComputeMessage<TMessage>> messages)
    {
        _projection = projection;
        _nodeIndex = nodeIndex;
        State = state;
        Superstep = superstep;
        _messages = messages;
    }

    /// <summary>
    /// Gets the node being computed.
    /// </summary>
    public Node Node => _projection.GetNode(_nodeIndex);

    /// <summary>
    /// Gets the zero-based synchronized compute superstep.
    /// </summary>
    public int Superstep { get; }

    /// <summary>
    /// Gets the current node state.
    /// </summary>
    public TState State { get; private set; }

    internal bool VotedToHalt { get; private set; }

    /// <summary>
    /// Replaces the node state.
    /// </summary>
    public void SetState(TState state) => State = state;

    /// <summary>
    /// Sends a message to any node in the projection.
    /// </summary>
    public void SendTo(Guid targetNodeId, TMessage message) =>
        _messages.Add(new GraphComputeMessage<TMessage>(_projection.GetNodeIndex(targetNodeId), message));

    /// <summary>
    /// Sends a generated message across every outgoing edge.
    /// </summary>
    public void SendToOutgoing(Func<Edge, Node, TMessage> messageFactory)
    {
        ArgumentNullException.ThrowIfNull(messageFactory);
        SendToArcs(_projection.GetOutgoingArcs(_nodeIndex), messageFactory);
    }

    /// <summary>
    /// Sends a generated message across every incoming edge.
    /// </summary>
    public void SendToIncoming(Func<Edge, Node, TMessage> messageFactory)
    {
        ArgumentNullException.ThrowIfNull(messageFactory);
        SendToArcs(_projection.GetIncomingArcs(_nodeIndex), messageFactory);
    }

    /// <summary>
    /// Sends a generated message in one or both directions.
    /// </summary>
    public void SendToNeighbors(GraphAlgorithmTraversalDirection direction, Func<Edge, Node, TMessage> messageFactory)
    {
        ArgumentNullException.ThrowIfNull(messageFactory);

        if (direction is GraphAlgorithmTraversalDirection.Outgoing or GraphAlgorithmTraversalDirection.Both)
        {
            SendToArcs(_projection.GetOutgoingArcs(_nodeIndex), messageFactory);
        }

        if (direction is GraphAlgorithmTraversalDirection.Incoming or GraphAlgorithmTraversalDirection.Both)
        {
            SendToArcs(_projection.GetIncomingArcs(_nodeIndex), messageFactory);
        }
    }

    /// <summary>
    /// Marks the node inactive until it receives another message.
    /// </summary>
    public void VoteToHalt() => VotedToHalt = true;

    private void SendToArcs(ReadOnlySpan<GraphProjectionArc> arcs, Func<Edge, Node, TMessage> messageFactory)
    {
        foreach (var arc in arcs)
        {
            var edge = _projection.GetEdge(arc.EdgeIndex);
            var target = _projection.GetNode(arc.NodeIndex);
            _messages.Add(new GraphComputeMessage<TMessage>(arc.NodeIndex, messageFactory(edge, target)));
        }
    }
}