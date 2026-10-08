using System.Diagnostics;
using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Application.Intelligence.Diagnostics;
using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Projection;

/// <summary>
/// Represents a detached, dense in-memory projection used by graph intelligence algorithms.
/// </summary>
public sealed class GraphProjection
{
    private readonly Node[] _nodes;
    private readonly Edge[] _edges;
    private readonly Dictionary<Guid, int> _nodeIndices;
    private readonly int[] _outgoingOffsets;
    private readonly GraphProjectionArc[] _outgoingArcs;
    private readonly int[] _incomingOffsets;
    private readonly GraphProjectionArc[] _incomingArcs;

    internal GraphProjection(GraphProjectionParameters inputs)
    {
        var nodes = inputs.Nodes;
        var edges = inputs.Edges;
        var nodeIndices = inputs.NodeIndices;
        var outgoingOffsets = inputs.OutgoingOffsets;
        var outgoingArcs = inputs.OutgoingArcs;
        var incomingOffsets = inputs.IncomingOffsets;
        var incomingArcs = inputs.IncomingArcs;
        var ignoredEdgeCount = inputs.IgnoredEdgeCount;

        _nodes = nodes;
        _edges = edges;
        Nodes = Array.AsReadOnly(nodes);
        Edges = Array.AsReadOnly(edges);
        _nodeIndices = nodeIndices;
        _outgoingOffsets = outgoingOffsets;
        _outgoingArcs = outgoingArcs;
        _incomingOffsets = incomingOffsets;
        _incomingArcs = incomingArcs;

        var isolatedNodeCount = 0;
        for (var index = 0; index < nodes.Length; index++)
        {
            if (GetOutgoingDegree(index) == 0 && GetIncomingDegree(index) == 0)
            {
                isolatedNodeCount++;
            }
        }

        Statistics = new GraphProjectionStatistics
        {
            NodeCount = nodes.Length,
            EdgeCount = edges.Length,
            IgnoredEdgeCount = ignoredEdgeCount,
            IsolatedNodeCount = isolatedNodeCount,
            Density = CalculateDensity(nodes.Length, edges.Length)
        };
    }

    /// <summary>
    /// Gets all projected nodes.
    /// </summary>
    public IReadOnlyList<Node> Nodes { get; }

    /// <summary>
    /// Gets all projected edges.
    /// </summary>
    public IReadOnlyList<Edge> Edges { get; }

    /// <summary>
    /// Gets projection statistics.
    /// </summary>
    public GraphProjectionStatistics Statistics { get; }

    /// <summary>
    /// Creates a projection from materialized nodes and edges.
    /// </summary>
    /// <param name="nodes">The nodes to project.</param>
    /// <param name="edges">The edges to project.</param>
    /// <param name="options">Projection options.</param>
    /// <returns>The detached graph projection.</returns>
    public static GraphProjection Create(IEnumerable<Node> nodes, IEnumerable<Edge> edges, GraphProjectionOptions? options = null) =>
        new GraphProjectionBuilder(options)
            .AddNodes(nodes)
            .AddEdges(edges)
            .Build();

    /// <summary>
    /// Determines whether the projection contains a node identifier.
    /// </summary>
    public bool ContainsNode(Guid nodeId) => _nodeIndices.ContainsKey(nodeId);

    /// <summary>
    /// Gets a projected node by identifier.
    /// </summary>
    public Node GetNode(Guid nodeId) => _nodes[GetNodeIndex(nodeId)];

    /// <summary>
    /// Gets the outgoing edges for a node.
    /// </summary>
    public IReadOnlyList<Edge> GetOutgoingEdges(Guid nodeId) => GetEdges(GetOutgoingArcs(GetNodeIndex(nodeId)));

    /// <summary>
    /// Gets the incoming edges for a node.
    /// </summary>
    public IReadOnlyList<Edge> GetIncomingEdges(Guid nodeId) => GetEdges(GetIncomingArcs(GetNodeIndex(nodeId)));

    /// <summary>
    /// Gets the outgoing neighbouring nodes for a node.
    /// </summary>
    public IReadOnlyList<Node> GetOutgoingNeighbors(Guid nodeId) => GetNodes(GetOutgoingArcs(GetNodeIndex(nodeId)));

    /// <summary>
    /// Gets the incoming neighbouring nodes for a node.
    /// </summary>
    public IReadOnlyList<Node> GetIncomingNeighbors(Guid nodeId) => GetNodes(GetIncomingArcs(GetNodeIndex(nodeId)));

    /// <summary>
    /// Executes an intelligence algorithm against this projection.
    /// </summary>
    public TResult Run<TResult>(IGraphAlgorithm<TResult> algorithm, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(algorithm);
        var startedAt = Stopwatch.GetTimestamp();
        using var activity = GormIntelligenceDiagnostics.StartAlgorithmActivity(algorithm.GetType(), this);
        try
        {
            var result = algorithm.Execute(this, cancellationToken);
            GormIntelligenceDiagnostics.RecordSuccess(activity, startedAt, GormIntelligenceDiagnostics.ActivityNames.Algorithm);
            return result;
        }
        catch (Exception exception)
        {
            GormIntelligenceDiagnostics.RecordFailure(activity, startedAt, GormIntelligenceDiagnostics.ActivityNames.Algorithm, exception);
            throw;
        }
    }

    internal Node GetNode(int nodeIndex) => _nodes[nodeIndex];

    internal Edge GetEdge(int edgeIndex) => _edges[edgeIndex];

    internal int GetNodeIndex(Guid nodeId) =>
        _nodeIndices.TryGetValue(nodeId, out var index)
            ? index
            : throw new KeyNotFoundException($"Node '{nodeId}' is not part of this graph projection.");

    internal int GetOutgoingDegree(int nodeIndex) => _outgoingOffsets[nodeIndex + 1] - _outgoingOffsets[nodeIndex];

    internal int GetIncomingDegree(int nodeIndex) => _incomingOffsets[nodeIndex + 1] - _incomingOffsets[nodeIndex];

    internal ReadOnlySpan<GraphProjectionArc> GetOutgoingArcs(int nodeIndex) =>
        _outgoingArcs.AsSpan(_outgoingOffsets[nodeIndex], GetOutgoingDegree(nodeIndex));

    internal ReadOnlySpan<GraphProjectionArc> GetIncomingArcs(int nodeIndex) =>
        _incomingArcs.AsSpan(_incomingOffsets[nodeIndex], GetIncomingDegree(nodeIndex));

    private Edge[] GetEdges(ReadOnlySpan<GraphProjectionArc> arcs)
    {
        var result = new Edge[arcs.Length];
        for (var index = 0; index < arcs.Length; index++)
        {
            result[index] = _edges[arcs[index].EdgeIndex];
        }

        return result;
    }

    private Node[] GetNodes(ReadOnlySpan<GraphProjectionArc> arcs)
    {
        var result = new Node[arcs.Length];
        for (var index = 0; index < arcs.Length; index++)
        {
            result[index] = _nodes[arcs[index].NodeIndex];
        }

        return result;
    }

    private static double CalculateDensity(int nodeCount, int edgeCount) =>
        nodeCount < 2 ? 0 : edgeCount / ((double)nodeCount * (nodeCount - 1));

    internal sealed class GraphProjectionParameters
    {
        public required Node[] Nodes { get; init; }

        public required Edge[] Edges { get; init; }

        public required Dictionary<Guid, int> NodeIndices { get; init; }

        public required int[] OutgoingOffsets { get; init; }

        public required GraphProjectionArc[] OutgoingArcs { get; init; }

        public required int[] IncomingOffsets { get; init; }

        public required GraphProjectionArc[] IncomingArcs { get; init; }

        public required int IgnoredEdgeCount { get; init; }
    }
}