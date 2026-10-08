using System.Globalization;
using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Algorithms.Flow;

/// <summary>
/// Calculates directed maximum flow and a corresponding minimum cut using bounded Edmonds-Karp augmentation.
/// </summary>
public sealed class GraphMaximumFlowAlgorithm : IGraphAlgorithm<GraphMaximumFlowResult>
{
    private readonly Guid _sourceNodeId;
    private readonly Guid _destinationNodeId;
    private readonly GraphMaximumFlowOptions _options;

    /// <summary>
    /// Initializes a maximum-flow/minimum-cut algorithm.
    /// </summary>
    public GraphMaximumFlowAlgorithm(Guid sourceNodeId, Guid destinationNodeId, GraphMaximumFlowOptions? options = null)
    {
        if (sourceNodeId == destinationNodeId)
        {
            throw new ArgumentException("Maximum-flow source and destination must be different nodes.", nameof(destinationNodeId));
        }

        _sourceNodeId = sourceNodeId;
        _destinationNodeId = destinationNodeId;
        _options = options ?? new GraphMaximumFlowOptions();

        if (_options.MaximumAugmentations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum augmentations must be greater than zero.");
        }

        if (!double.IsFinite(_options.Epsilon) || _options.Epsilon <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Residual epsilon must be finite and greater than zero.");
        }
    }

    /// <inheritdoc />
    public GraphMaximumFlowResult Execute(GraphProjection projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var sourceIndex = projection.GetNodeIndex(_sourceNodeId);
        var destinationIndex = projection.GetNodeIndex(_destinationNodeId);
        var network = CreateResidualNetwork(projection, cancellationToken);
        var flow = CalculateMaximumFlow(network.Graph, sourceIndex, destinationIndex, cancellationToken);
        return CreateResult(projection, network, sourceIndex, flow.MaximumFlow, flow.Augmentations, cancellationToken);
    }

    private ResidualNetwork CreateResidualNetwork(GraphProjection projection, CancellationToken cancellationToken)
    {
        var capacities = new double[projection.Statistics.EdgeCount];
        var included = new bool[projection.Statistics.EdgeCount];
        var graph = Enumerable.Range(0, projection.Statistics.NodeCount).Select(_ => new List<ResidualArc>()).ToArray();
        var forwardArcs = new ResidualArc?[projection.Statistics.EdgeCount];

        for (var edgeIndex = 0; edgeIndex < projection.Statistics.EdgeCount; edgeIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var edge = projection.GetEdge(edgeIndex);
            if (_options.EdgePredicate is not null && !_options.EdgePredicate(edge))
            {
                continue;
            }

            var capacity = _options.CapacitySelector?.Invoke(edge) ?? 1;
            if (!double.IsFinite(capacity) || capacity < 0)
            {
                throw new InvalidOperationException($"The flow capacity for edge '{edge.Id}' must be finite and non-negative but was '{capacity}'.");
            }

            included[edgeIndex] = true;
            capacities[edgeIndex] = capacity;
            var fromIndex = projection.GetNodeIndex(edge.FromId);
            var toIndex = projection.GetNodeIndex(edge.ToId);
            forwardArcs[edgeIndex] = AddResidualEdge(graph, fromIndex, toIndex, capacity, edgeIndex);
        }

        return new ResidualNetwork(graph, capacities, included, forwardArcs);
    }

    private FlowCalculation CalculateMaximumFlow(List<ResidualArc>[] graph, int sourceIndex, int destinationIndex, CancellationToken cancellationToken)
    {
        var maximumFlow = 0d;
        var augmentations = 0;
        while (TryFindAugmentingPath(graph, sourceIndex, destinationIndex, cancellationToken, out var parents))
        {
            if (augmentations >= _options.MaximumAugmentations)
            {
                throw new GraphFlowAugmentationLimitException(_options.MaximumAugmentations);
            }

            var bottleneck = FindBottleneck(parents, sourceIndex, destinationIndex);
            AugmentPath(parents, sourceIndex, destinationIndex, bottleneck);
            maximumFlow += bottleneck;
            augmentations++;
        }

        return new FlowCalculation(maximumFlow, augmentations);
    }

    private static double FindBottleneck(ParentArc[] parents, int sourceIndex, int destinationIndex)
    {
        var bottleneck = double.PositiveInfinity;
        for (var nodeIndex = destinationIndex; nodeIndex != sourceIndex;)
        {
            var parent = parents[nodeIndex];
            bottleneck = Math.Min(bottleneck, parent.Arc.Capacity - parent.Arc.Flow);
            nodeIndex = parent.FromIndex;
        }

        return bottleneck;
    }

    private static void AugmentPath(ParentArc[] parents, int sourceIndex, int destinationIndex, double bottleneck)
    {
        for (var nodeIndex = destinationIndex; nodeIndex != sourceIndex;)
        {
            var parent = parents[nodeIndex];
            parent.Arc.Flow += bottleneck;
            parent.Arc.Reverse.Flow -= bottleneck;
            nodeIndex = parent.FromIndex;
        }
    }

    private GraphMaximumFlowResult CreateResult(
        GraphProjection projection,
        ResidualNetwork network,
        int sourceIndex,
        double maximumFlow,
        int augmentations,
        CancellationToken cancellationToken)
    {
        var sourceSide = FindResidualReachable(network.Graph, sourceIndex, cancellationToken);
        var edgeFlows = Enumerable.Range(0, projection.Statistics.EdgeCount)
            .Where(edgeIndex => network.Included[edgeIndex])
            .Select(edgeIndex =>
            {
                var flow = Math.Max(0, network.ForwardArcs[edgeIndex]!.Flow);
                var capacity = network.Capacities[edgeIndex];
                return new GraphEdgeFlow
                {
                    Edge = projection.GetEdge(edgeIndex),
                    Capacity = capacity,
                    Flow = flow,
                    Utilization = GraphAlgorithmNumeric.IsZero(capacity) ? 0 : flow / capacity,
                    IsSaturated = capacity - flow <= _options.Epsilon
                };
            })
            .ToArray();

        var cutEdges = edgeFlows
            .Where(edgeFlow =>
            {
                var fromIndex = projection.GetNodeIndex(edgeFlow.Edge.FromId);
                var toIndex = projection.GetNodeIndex(edgeFlow.Edge.ToId);
                return sourceSide[fromIndex] && !sourceSide[toIndex];
            })
            .OrderBy(edgeFlow => edgeFlow.Edge.Id)
            .ToArray();

        var minimumCutCapacity = cutEdges.Sum(edgeFlow => edgeFlow.Capacity);
        var bottleneckEdges = edgeFlows
            .Where(edgeFlow => edgeFlow.Flow > _options.Epsilon && edgeFlow.IsSaturated)
            .OrderByDescending(edgeFlow => edgeFlow.Utilization)
            .ThenBy(edgeFlow => edgeFlow.Capacity)
            .ThenBy(edgeFlow => edgeFlow.Edge.Id)
            .ToArray();

        return new GraphMaximumFlowResult
        {
            MaximumFlow = maximumFlow,
            MinimumCutCapacity = minimumCutCapacity,
            EdgeFlows = edgeFlows,
            SourceSideNodes = [.. Enumerable.Range(0, projection.Statistics.NodeCount)
                .Where(nodeIndex => sourceSide[nodeIndex])
                .Select(projection.GetNode)
                .OrderBy(node => node.Id)],
            DestinationSideNodes = [.. Enumerable.Range(0, projection.Statistics.NodeCount)
                .Where(nodeIndex => !sourceSide[nodeIndex])
                .Select(projection.GetNode)
                .OrderBy(node => node.Id)],
            CutEdges = cutEdges,
            BottleneckEdges = bottleneckEdges,
            Augmentations = augmentations,
            Explanation = string.Format(
                CultureInfo.InvariantCulture,
                "Maximum flow {0:F3} equals minimum-cut capacity {1:F3}; the cut contains {2} edge(s) and {3} saturated positive-flow bottleneck(s).",
                maximumFlow,
                minimumCutCapacity,
                cutEdges.Length,
                bottleneckEdges.Length)
        };
    }

    private bool TryFindAugmentingPath(List<ResidualArc>[] graph, int sourceIndex, int destinationIndex, CancellationToken cancellationToken, out ParentArc[] parents)
    {
        parents = new ParentArc[graph.Length];
        var visited = new bool[graph.Length];
        var pending = new Queue<int>();
        visited[sourceIndex] = true;
        pending.Enqueue(sourceIndex);

        while (pending.TryDequeue(out var currentIndex))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var arc in graph[currentIndex])
            {
                if (visited[arc.ToIndex] || arc.Capacity - arc.Flow <= _options.Epsilon)
                {
                    continue;
                }

                visited[arc.ToIndex] = true;
                parents[arc.ToIndex] = new ParentArc(currentIndex, arc);
                if (arc.ToIndex == destinationIndex)
                {
                    return true;
                }

                pending.Enqueue(arc.ToIndex);
            }
        }

        return false;
    }

    private bool[] FindResidualReachable(List<ResidualArc>[] graph, int sourceIndex, CancellationToken cancellationToken)
    {
        var reachable = new bool[graph.Length];
        var pending = new Queue<int>();
        reachable[sourceIndex] = true;
        pending.Enqueue(sourceIndex);

        while (pending.TryDequeue(out var currentIndex))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var arc in graph[currentIndex])
            {
                if (!reachable[arc.ToIndex] && arc.Capacity - arc.Flow > _options.Epsilon)
                {
                    reachable[arc.ToIndex] = true;
                    pending.Enqueue(arc.ToIndex);
                }
            }
        }

        return reachable;
    }

    private static ResidualArc AddResidualEdge(List<ResidualArc>[] graph, int fromIndex, int toIndex, double capacity, int edgeIndex)
    {
        var forward = new ResidualArc(toIndex, capacity, edgeIndex);
        var reverse = new ResidualArc(fromIndex, 0, edgeIndex);
        forward.Reverse = reverse;
        reverse.Reverse = forward;
        graph[fromIndex].Add(forward);
        graph[toIndex].Add(reverse);
        return forward;
    }

    private sealed class ResidualArc(int toIndex, double capacity, int edgeIndex)
    {
        public int ToIndex { get; } = toIndex;

        public double Capacity { get; } = capacity;

        public int EdgeIndex { get; } = edgeIndex;

        public double Flow { get; set; }

        public ResidualArc Reverse { get; set; } = null!;
    }

    private sealed record ResidualNetwork(List<ResidualArc>[] Graph, double[] Capacities, bool[] Included, ResidualArc?[] ForwardArcs);

    private readonly record struct FlowCalculation(double MaximumFlow, int Augmentations);

    private readonly record struct ParentArc(int FromIndex, ResidualArc Arc);
}