using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Application.Intelligence.Projection;
using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Pathfinding;

/// <summary>
/// Finds a weighted shortest path using Dijkstra's algorithm.
/// </summary>
public sealed class GraphShortestPathAlgorithm : IGraphAlgorithm<GraphShortestPathResult?>
{
    private readonly Guid _startNodeId;
    private readonly Guid _destinationNodeId;
    private readonly GraphShortestPathOptions _options;

    /// <summary>
    /// Initializes a shortest-path algorithm.
    /// </summary>
    public GraphShortestPathAlgorithm(Guid startNodeId, Guid destinationNodeId, GraphShortestPathOptions? options = null)
    {
        _startNodeId = startNodeId;
        _destinationNodeId = destinationNodeId;
        _options = options ?? new GraphShortestPathOptions();

        if (!Enum.IsDefined(_options.Direction))
        {
            throw new ArgumentOutOfRangeException(nameof(options), _options.Direction, "Unknown traversal direction.");
        }
    }

    /// <inheritdoc />
    public GraphShortestPathResult? Execute(GraphProjection projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var startIndex = projection.GetNodeIndex(_startNodeId);
        var destinationIndex = projection.GetNodeIndex(_destinationNodeId);

        if (startIndex == destinationIndex)
        {
            return new GraphShortestPathResult
            {
                Nodes = [projection.GetNode(startIndex)],
                Edges = [],
                TotalWeight = 0
            };
        }

        var weights = CreateWeights(projection);
        var nodeCount = projection.Statistics.NodeCount;
        var distances = new double[nodeCount];
        var previousNodes = new int[nodeCount];
        var previousEdges = new int[nodeCount];

        Array.Fill(distances, double.PositiveInfinity);
        Array.Fill(previousNodes, -1);
        Array.Fill(previousEdges, -1);

        var pending = new PriorityQueue<int, double>();
        distances[startIndex] = 0;
        pending.Enqueue(startIndex, 0);

        while (pending.TryDequeue(out var currentIndex, out var queuedDistance))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (queuedDistance > distances[currentIndex])
            {
                continue;
            }

            if (currentIndex == destinationIndex)
            {
                return CreateResult(projection, destinationIndex, distances, previousNodes, previousEdges);
            }

            if (_options.Direction is GraphAlgorithmTraversalDirection.Outgoing or GraphAlgorithmTraversalDirection.Both)
            {
                Relax(
                    new RelaxParameters
                    {
                        Projection = projection,
                        Arcs = projection.GetOutgoingArcs(currentIndex),
                        CurrentIndex = currentIndex,
                        Weights = weights,
                        Distances = distances,
                        PreviousNodes = previousNodes,
                        PreviousEdges = previousEdges,
                        Pending = pending
                    });
            }

            if (_options.Direction is GraphAlgorithmTraversalDirection.Incoming or GraphAlgorithmTraversalDirection.Both)
            {
                Relax(
                    new RelaxParameters
                    {
                        Projection = projection,
                        Arcs = projection.GetIncomingArcs(currentIndex),
                        CurrentIndex = currentIndex,
                        Weights = weights,
                        Distances = distances,
                        PreviousNodes = previousNodes,
                        PreviousEdges = previousEdges,
                        Pending = pending
                    });
            }
        }

        return null;
    }

    private double[] CreateWeights(GraphProjection projection)
    {
        var weights = new double[projection.Statistics.EdgeCount];

        for (var edgeIndex = 0; edgeIndex < weights.Length; edgeIndex++)
        {
            var edge = projection.GetEdge(edgeIndex);
            var weight = _options.WeightSelector?.Invoke(edge) ?? 1;

            if (!double.IsFinite(weight) || weight < 0)
            {
                throw new InvalidOperationException(
                    $"The weight for edge '{edge.Id}' must be a finite, non-negative number but was '{weight}'.");
            }

            weights[edgeIndex] = weight;
        }

        return weights;
    }

    private void Relax(RelaxParameters inputs)
    {
        var projection = inputs.Projection;
        var arcs = inputs.Arcs;
        var currentIndex = inputs.CurrentIndex;
        var weights = inputs.Weights;
        var distances = inputs.Distances;
        var previousNodes = inputs.PreviousNodes;
        var previousEdges = inputs.PreviousEdges;
        var pending = inputs.Pending;

        foreach (var arc in arcs)
        {
            var edge = projection.GetEdge(arc.EdgeIndex);
            if (_options.EdgePredicate is not null && !_options.EdgePredicate(edge))
            {
                continue;
            }

            var candidateDistance = distances[currentIndex] + weights[arc.EdgeIndex];
            if (candidateDistance >= distances[arc.NodeIndex])
            {
                continue;
            }

            distances[arc.NodeIndex] = candidateDistance;
            previousNodes[arc.NodeIndex] = currentIndex;
            previousEdges[arc.NodeIndex] = arc.EdgeIndex;
            pending.Enqueue(arc.NodeIndex, candidateDistance);
        }
    }

    private static GraphShortestPathResult CreateResult(GraphProjection projection, int destinationIndex, double[] distances, int[] previousNodes, int[] previousEdges)
    {
        var nodes = new List<Node>();
        var edges = new List<Edge>();
        var currentIndex = destinationIndex;

        while (currentIndex >= 0)
        {
            nodes.Add(projection.GetNode(currentIndex));

            var edgeIndex = previousEdges[currentIndex];
            if (edgeIndex >= 0)
            {
                edges.Add(projection.GetEdge(edgeIndex));
            }

            currentIndex = previousNodes[currentIndex];
        }

        nodes.Reverse();
        edges.Reverse();

        return new GraphShortestPathResult
        {
            Nodes = nodes,
            Edges = edges,
            TotalWeight = distances[destinationIndex]
        };
    }

    private readonly ref struct RelaxParameters
    {
        public required GraphProjection Projection { get; init; }

        public required ReadOnlySpan<GraphProjectionArc> Arcs { get; init; }

        public required int CurrentIndex { get; init; }

        public required double[] Weights { get; init; }

        public required double[] Distances { get; init; }

        public required int[] PreviousNodes { get; init; }

        public required int[] PreviousEdges { get; init; }

        public required PriorityQueue<int, double> Pending { get; init; }
    }
}