using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Algorithms.Centrality;

/// <summary>
/// Calculates exact betweenness centrality using Brandes' algorithm.
/// </summary>
public sealed class GraphBetweennessCentralityAlgorithm : IGraphAlgorithm<GraphBetweennessCentralityResult>
{
    private readonly GraphBetweennessCentralityOptions _options;

    /// <summary>
    /// Initializes a betweenness-centrality algorithm.
    /// </summary>
    public GraphBetweennessCentralityAlgorithm(GraphBetweennessCentralityOptions? options = null)
    {
        _options = options ?? new GraphBetweennessCentralityOptions();

        if (!Enum.IsDefined(_options.Direction))
        {
            throw new ArgumentOutOfRangeException(nameof(options), _options.Direction, "Unknown traversal direction.");
        }
    }

    /// <inheritdoc />
    public GraphBetweennessCentralityResult Execute(GraphProjection projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var nodeCount = projection.Statistics.NodeCount;
        var scores = new double[nodeCount];
        var includedEdges = SelectEdges(projection, cancellationToken);
        var weights = _options.WeightSelector is null ? null : CreateWeights(projection, includedEdges, cancellationToken);

        for (var sourceIndex = 0; sourceIndex < nodeCount; sourceIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AccumulateFromSource(projection, sourceIndex, includedEdges, weights, scores, cancellationToken);
        }

        var treatsGraphAsUndirected = _options.Direction == GraphAlgorithmTraversalDirection.Both;
        if (treatsGraphAsUndirected)
        {
            for (var nodeIndex = 0; nodeIndex < scores.Length; nodeIndex++)
            {
                scores[nodeIndex] /= 2;
            }
        }

        var normalization = 0d;
        if (nodeCount >= 3)
        {
            normalization = (nodeCount - 1d) * (nodeCount - 2d);
            if (treatsGraphAsUndirected)
            {
                normalization /= 2d;
            }
        }

        var orderedScores = Enumerable.Range(0, nodeCount)
            .Select(nodeIndex => new GraphBetweennessCentralityScore
            {
                Node = projection.GetNode(nodeIndex),
                Rank = 0,
                Score = scores[nodeIndex],
                NormalizedScore = GraphAlgorithmNumeric.IsZero(normalization)
                    ? 0
                    : scores[nodeIndex] / normalization
            })
            .OrderByDescending(score => score.NormalizedScore)
            .ThenBy(score => score.Node.Id)
            .Select((score, rank) => new GraphBetweennessCentralityScore { Node = score.Node, Rank = rank, Score = score.Score, NormalizedScore = score.NormalizedScore })
            .ToArray();

        return new GraphBetweennessCentralityResult(orderedScores);
    }

    private bool[] SelectEdges(GraphProjection projection, CancellationToken cancellationToken)
    {
        var included = new bool[projection.Statistics.EdgeCount];

        for (var edgeIndex = 0; edgeIndex < included.Length; edgeIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var edge = projection.GetEdge(edgeIndex);
            included[edgeIndex] = _options.EdgePredicate?.Invoke(edge) ?? true;
        }

        return included;
    }

    private double[] CreateWeights(GraphProjection projection, bool[] includedEdges, CancellationToken cancellationToken)
    {
        var weights = new double[projection.Statistics.EdgeCount];

        for (var edgeIndex = 0; edgeIndex < weights.Length; edgeIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!includedEdges[edgeIndex])
            {
                continue;
            }

            var edge = projection.GetEdge(edgeIndex);
            var weight = _options.WeightSelector!(edge);
            if (!double.IsFinite(weight) || weight <= 0)
            {
                throw new InvalidOperationException($"The betweenness weight for edge '{edge.Id}' must be finite and greater than zero but was '{weight}'.");
            }

            weights[edgeIndex] = weight;
        }

        return weights;
    }

    private void AccumulateFromSource(
        GraphProjection projection,
        int sourceIndex,
        bool[] includedEdges,
        double[]? weights,
        double[] centrality,
        CancellationToken cancellationToken)
    {
        var nodeCount = projection.Statistics.NodeCount;
        var predecessors = new List<int>?[nodeCount];
        var shortestPathCounts = new double[nodeCount];
        var distances = new double[nodeCount];

        Array.Fill(distances, double.PositiveInfinity);
        shortestPathCounts[sourceIndex] = 1;
        distances[sourceIndex] = 0;

        List<int> visitOrder = weights is null
            ? TraverseUnweighted(
                new TraverseUnweightedParameters
                {
                    Projection = projection,
                    SourceIndex = sourceIndex,
                    IncludedEdges = includedEdges,
                    Predecessors = predecessors,
                    ShortestPathCounts = shortestPathCounts,
                    Distances = distances,
                    CancellationToken = cancellationToken
                })
            : TraverseWeighted(
                new TraverseWeightedParameters
                {
                    Projection = projection,
                    SourceIndex = sourceIndex,
                    IncludedEdges = includedEdges,
                    Weights = weights,
                    Predecessors = predecessors,
                    ShortestPathCounts = shortestPathCounts,
                    Distances = distances,
                    CancellationToken = cancellationToken
                });

        var dependencies = new double[nodeCount];
        for (var orderIndex = visitOrder.Count; orderIndex > 0; orderIndex--)
        {
            var nodeIndex = visitOrder[orderIndex - 1];
            var nodePredecessors = predecessors[nodeIndex];

            if (nodePredecessors is not null && shortestPathCounts[nodeIndex] > 0)
            {
                foreach (var predecessorIndex in nodePredecessors)
                {
                    dependencies[predecessorIndex] += shortestPathCounts[predecessorIndex] / shortestPathCounts[nodeIndex] * (1 + dependencies[nodeIndex]);
                }
            }

            if (nodeIndex != sourceIndex)
            {
                centrality[nodeIndex] += dependencies[nodeIndex];
            }
        }
    }

    private List<int> TraverseUnweighted(TraverseUnweightedParameters inputs)
    {
        var projection = inputs.Projection;
        var sourceIndex = inputs.SourceIndex;
        var includedEdges = inputs.IncludedEdges;
        var predecessors = inputs.Predecessors;
        var shortestPathCounts = inputs.ShortestPathCounts;
        var distances = inputs.Distances;
        var cancellationToken = inputs.CancellationToken;

        var visitOrder = new List<int>(projection.Statistics.NodeCount);
        var pending = new Queue<int>();
        pending.Enqueue(sourceIndex);

        while (pending.TryDequeue(out var currentIndex))
        {
            cancellationToken.ThrowIfCancellationRequested();
            visitOrder.Add(currentIndex);

            if (_options.Direction is GraphAlgorithmTraversalDirection.Outgoing or GraphAlgorithmTraversalDirection.Both)
            {
                var outgoingArcs = projection.GetOutgoingArcs(currentIndex);
                VisitUnweightedArcs(
                    new VisitUnweightedArcsParameters
                    {
                        Arcs = outgoingArcs,
                        CurrentIndex = currentIndex,
                        IncludedEdges = includedEdges,
                        Predecessors = predecessors,
                        ShortestPathCounts = shortestPathCounts,
                        Distances = distances,
                        Pending = pending
                    });
            }

            if (_options.Direction is GraphAlgorithmTraversalDirection.Incoming or GraphAlgorithmTraversalDirection.Both)
            {
                var incomingArcs = projection.GetIncomingArcs(currentIndex);
                VisitUnweightedArcs(
                    new VisitUnweightedArcsParameters
                    {
                        Arcs = incomingArcs,
                        CurrentIndex = currentIndex,
                        IncludedEdges = includedEdges,
                        Predecessors = predecessors,
                        ShortestPathCounts = shortestPathCounts,
                        Distances = distances,
                        Pending = pending
                    });
            }
        }

        return visitOrder;
    }

    private static void VisitUnweightedArcs(VisitUnweightedArcsParameters inputs)
    {
        var arcs = inputs.Arcs;
        var currentIndex = inputs.CurrentIndex;
        var includedEdges = inputs.IncludedEdges;
        var predecessors = inputs.Predecessors;
        var shortestPathCounts = inputs.ShortestPathCounts;
        var distances = inputs.Distances;
        var pending = inputs.Pending;

        foreach (var arc in arcs)
        {
            if (!includedEdges[arc.EdgeIndex])
            {
                continue;
            }

            var candidateDistance = distances[currentIndex] + 1;
            if (double.IsPositiveInfinity(distances[arc.NodeIndex]))
            {
                distances[arc.NodeIndex] = candidateDistance;
                pending.Enqueue(arc.NodeIndex);
            }

            if (!GraphAlgorithmNumeric.AreEqual(distances[arc.NodeIndex], candidateDistance))
            {
                continue;
            }

            shortestPathCounts[arc.NodeIndex] += shortestPathCounts[currentIndex];
            (predecessors[arc.NodeIndex] ??= []).Add(currentIndex);
        }
    }

    private List<int> TraverseWeighted(TraverseWeightedParameters inputs)
    {
        var projection = inputs.Projection;
        var sourceIndex = inputs.SourceIndex;
        var includedEdges = inputs.IncludedEdges;
        var weights = inputs.Weights;
        var predecessors = inputs.Predecessors;
        var shortestPathCounts = inputs.ShortestPathCounts;
        var distances = inputs.Distances;
        var cancellationToken = inputs.CancellationToken;

        var visitOrder = new List<int>(projection.Statistics.NodeCount);
        var finalized = new bool[projection.Statistics.NodeCount];
        var pending = new PriorityQueue<int, (double Distance, int NodeIndex)>();
        pending.Enqueue(sourceIndex, (0, sourceIndex));

        while (pending.TryDequeue(out var currentIndex, out var priority))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (finalized[currentIndex] || priority.Distance > distances[currentIndex])
            {
                continue;
            }

            finalized[currentIndex] = true;
            visitOrder.Add(currentIndex);

            if (_options.Direction is GraphAlgorithmTraversalDirection.Outgoing or GraphAlgorithmTraversalDirection.Both)
            {
                var outgoingArcs = projection.GetOutgoingArcs(currentIndex);
                VisitWeightedArcs(
                    new VisitWeightedArcsParameters
                    {
                        Arcs = outgoingArcs,
                        CurrentIndex = currentIndex,
                        IncludedEdges = includedEdges,
                        Weights = weights,
                        Predecessors = predecessors,
                        ShortestPathCounts = shortestPathCounts,
                        Distances = distances,
                        Pending = pending
                    });
            }

            if (_options.Direction is GraphAlgorithmTraversalDirection.Incoming or GraphAlgorithmTraversalDirection.Both)
            {
                var incomingArcs = projection.GetIncomingArcs(currentIndex);
                VisitWeightedArcs(
                    new VisitWeightedArcsParameters
                    {
                        Arcs = incomingArcs,
                        CurrentIndex = currentIndex,
                        IncludedEdges = includedEdges,
                        Weights = weights,
                        Predecessors = predecessors,
                        ShortestPathCounts = shortestPathCounts,
                        Distances = distances,
                        Pending = pending
                    });
            }
        }

        return visitOrder;
    }

    private static void VisitWeightedArcs(VisitWeightedArcsParameters inputs)
    {
        var arcs = inputs.Arcs;
        var currentIndex = inputs.CurrentIndex;
        var includedEdges = inputs.IncludedEdges;
        var weights = inputs.Weights;
        var predecessors = inputs.Predecessors;
        var shortestPathCounts = inputs.ShortestPathCounts;
        var distances = inputs.Distances;
        var pending = inputs.Pending;

        foreach (var arc in arcs)
        {
            if (!includedEdges[arc.EdgeIndex])
            {
                continue;
            }

            var candidateDistance = distances[currentIndex] + weights[arc.EdgeIndex];
            if (candidateDistance < distances[arc.NodeIndex])
            {
                distances[arc.NodeIndex] = candidateDistance;
                shortestPathCounts[arc.NodeIndex] = shortestPathCounts[currentIndex];
                predecessors[arc.NodeIndex] = [currentIndex];
                pending.Enqueue(arc.NodeIndex, (candidateDistance, arc.NodeIndex));
            }
            else if (GraphAlgorithmNumeric.AreEqual(candidateDistance, distances[arc.NodeIndex]))
            {
                shortestPathCounts[arc.NodeIndex] += shortestPathCounts[currentIndex];
                (predecessors[arc.NodeIndex] ??= []).Add(currentIndex);
            }
        }
    }

    private sealed class TraverseUnweightedParameters
    {
        public required GraphProjection Projection { get; init; }

        public required int SourceIndex { get; init; }

        public required bool[] IncludedEdges { get; init; }

        public required List<int>?[] Predecessors { get; init; }

        public required double[] ShortestPathCounts { get; init; }

        public required double[] Distances { get; init; }

        public required CancellationToken CancellationToken { get; init; }
    }

    private readonly ref struct VisitUnweightedArcsParameters
    {
        public required ReadOnlySpan<GraphProjectionArc> Arcs { get; init; }

        public required int CurrentIndex { get; init; }

        public required bool[] IncludedEdges { get; init; }

        public required List<int>?[] Predecessors { get; init; }

        public required double[] ShortestPathCounts { get; init; }

        public required double[] Distances { get; init; }

        public required Queue<int> Pending { get; init; }
    }

    private sealed class TraverseWeightedParameters
    {
        public required GraphProjection Projection { get; init; }

        public required int SourceIndex { get; init; }

        public required bool[] IncludedEdges { get; init; }

        public required double[] Weights { get; init; }

        public required List<int>?[] Predecessors { get; init; }

        public required double[] ShortestPathCounts { get; init; }

        public required double[] Distances { get; init; }

        public required CancellationToken CancellationToken { get; init; }
    }

    private readonly ref struct VisitWeightedArcsParameters
    {
        public required ReadOnlySpan<GraphProjectionArc> Arcs { get; init; }

        public required int CurrentIndex { get; init; }

        public required bool[] IncludedEdges { get; init; }

        public required double[] Weights { get; init; }

        public required List<int>?[] Predecessors { get; init; }

        public required double[] ShortestPathCounts { get; init; }

        public required double[] Distances { get; init; }

        public required PriorityQueue<int, (double Distance, int NodeIndex)> Pending { get; init; }
    }
}