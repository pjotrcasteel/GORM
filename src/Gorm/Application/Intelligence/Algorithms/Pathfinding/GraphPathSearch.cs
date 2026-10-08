using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Application.Intelligence.Projection;
using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Pathfinding;

internal static class GraphPathSearch
{
    public static EdgeConfiguration CreateEdgeConfiguration(
        GraphProjection projection,
        Func<Edge, double>? costSelector,
        Func<Edge, bool>? edgePredicate,
        CancellationToken cancellationToken)
    {
        var included = new bool[projection.Statistics.EdgeCount];
        var costs = new double[projection.Statistics.EdgeCount];

        for (var edgeIndex = 0; edgeIndex < included.Length; edgeIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var edge = projection.GetEdge(edgeIndex);
            if (edgePredicate is not null && !edgePredicate(edge))
            {
                continue;
            }

            var cost = costSelector?.Invoke(edge) ?? 1;
            if (!double.IsFinite(cost) || cost < 0)
            {
                throw new InvalidOperationException($"The route cost for edge '{edge.Id}' must be finite and non-negative but was '{cost}'.");
            }

            included[edgeIndex] = true;
            costs[edgeIndex] = cost;
        }

        return new EdgeConfiguration(included, costs);
    }

    public static SearchPath? FindShortestPath(FindShortestPathParameters inputs, out int exploredNodeCount)
    {
        var projection = inputs.Projection;
        var startIndex = inputs.StartIndex;
        var destinationIndex = inputs.DestinationIndex;
        var direction = inputs.Direction;
        var edges = inputs.Edges;
        var heuristic = inputs.Heuristic;
        var bannedNodes = inputs.BannedNodes;
        var bannedEdges = inputs.BannedEdges;
        var cancellationToken = inputs.CancellationToken;

        ValidateDirection(direction);
        exploredNodeCount = 0;

        if (bannedNodes?.Contains(startIndex) == true || bannedNodes?.Contains(destinationIndex) == true)
        {
            return null;
        }

        if (startIndex == destinationIndex)
        {
            return new SearchPath([startIndex], [], 0);
        }

        var buffers = CreateSearchBuffers(projection.Statistics.NodeCount, startIndex);
        var distances = buffers.Distances;
        var previousNodes = buffers.PreviousNodes;
        var previousEdges = buffers.PreviousEdges;

        var destination = projection.GetNode(destinationIndex);
        var initialEstimate = GetHeuristic(projection.GetNode(startIndex), destination, heuristic);
        var pending = new PriorityQueue<SearchState, (double Estimate, double Cost, int Node)>();
        pending.Enqueue(new SearchState(startIndex, 0), (initialEstimate, 0, startIndex));

        while (pending.TryDequeue(out var state, out _))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (state.Cost > distances[state.NodeIndex])
            {
                continue;
            }

            exploredNodeCount++;
            if (state.NodeIndex == destinationIndex)
            {
                return Reconstruct(destinationIndex, distances, previousNodes, previousEdges);
            }

            if (direction is GraphAlgorithmTraversalDirection.Outgoing or GraphAlgorithmTraversalDirection.Both)
            {
                Relax(
                    new RelaxParameters
                    {
                        Projection = projection,
                        Arcs = projection.GetOutgoingArcs(state.NodeIndex),
                        CurrentIndex = state.NodeIndex,
                        Destination = destination,
                        Edges = edges,
                        Heuristic = heuristic,
                        BannedNodes = bannedNodes,
                        BannedEdges = bannedEdges,
                        Distances = distances,
                        PreviousNodes = previousNodes,
                        PreviousEdges = previousEdges,
                        Pending = pending
                    });
            }

            if (direction is GraphAlgorithmTraversalDirection.Incoming or GraphAlgorithmTraversalDirection.Both)
            {
                Relax(
                    new RelaxParameters
                    {
                        Projection = projection,
                        Arcs = projection.GetIncomingArcs(state.NodeIndex),
                        CurrentIndex = state.NodeIndex,
                        Destination = destination,
                        Edges = edges,
                        Heuristic = heuristic,
                        BannedNodes = bannedNodes,
                        BannedEdges = bannedEdges,
                        Distances = distances,
                        PreviousNodes = previousNodes,
                        PreviousEdges = previousEdges,
                        Pending = pending
                    });
            }
        }

        return null;
    }

    private static SearchBuffers CreateSearchBuffers(int nodeCount, int startIndex)
    {
        var distances = new double[nodeCount];
        var previousNodes = new int[nodeCount];
        var previousEdges = new int[nodeCount];
        Array.Fill(distances, double.PositiveInfinity);
        Array.Fill(previousNodes, -1);
        Array.Fill(previousEdges, -1);
        distances[startIndex] = 0;
        return new SearchBuffers(distances, previousNodes, previousEdges);
    }

    public static GraphRoutePath ToPublicPath(GraphProjection projection, SearchPath path, string explanation) =>
        new()
        {
            Nodes = [.. path.NodeIndices.Select(projection.GetNode)],
            Edges = [.. path.EdgeIndices.Select(projection.GetEdge)],
            TotalCost = path.TotalCost,
            Explanation = explanation
        };

    public static string Signature(SearchPath path) =>
        string.Join("/", path.NodeIndices) + "|" + string.Join("/", path.EdgeIndices);

    private static void Relax(RelaxParameters inputs)
    {
        var projection = inputs.Projection;
        var arcs = inputs.Arcs;
        var currentIndex = inputs.CurrentIndex;
        var destination = inputs.Destination;
        var edges = inputs.Edges;
        var heuristic = inputs.Heuristic;
        var bannedNodes = inputs.BannedNodes;
        var bannedEdges = inputs.BannedEdges;
        var distances = inputs.Distances;
        var previousNodes = inputs.PreviousNodes;
        var previousEdges = inputs.PreviousEdges;
        var pending = inputs.Pending;

        foreach (var arc in arcs)
        {
            if (!edges.Included[arc.EdgeIndex] ||
                bannedEdges?.Contains(arc.EdgeIndex) == true ||
                bannedNodes?.Contains(arc.NodeIndex) == true)
            {
                continue;
            }

            var candidateCost = distances[currentIndex] + edges.Costs[arc.EdgeIndex];
            if (candidateCost >= distances[arc.NodeIndex])
            {
                continue;
            }

            distances[arc.NodeIndex] = candidateCost;
            previousNodes[arc.NodeIndex] = currentIndex;
            previousEdges[arc.NodeIndex] = arc.EdgeIndex;
            var estimate = candidateCost + GetHeuristic(projection.GetNode(arc.NodeIndex), destination, heuristic);
            pending.Enqueue(new SearchState(arc.NodeIndex, candidateCost), (estimate, candidateCost, arc.NodeIndex));
        }
    }

    private static double GetHeuristic(Node current, Node destination, Func<Node, Node, double>? heuristic)
    {
        var estimate = heuristic?.Invoke(current, destination) ?? 0;
        if (!double.IsFinite(estimate) || estimate < 0)
        {
            throw new InvalidOperationException($"The A* {nameof(heuristic)} for node '{current.Id}' must be finite and non-negative but was '{estimate}'.");
        }

        return estimate;
    }

    private static SearchPath Reconstruct(int destinationIndex, double[] distances, int[] previousNodes, int[] previousEdges)
    {
        var nodes = new List<int>();
        var edges = new List<int>();
        var currentIndex = destinationIndex;

        while (currentIndex >= 0)
        {
            nodes.Add(currentIndex);
            if (previousEdges[currentIndex] >= 0)
            {
                edges.Add(previousEdges[currentIndex]);
            }

            currentIndex = previousNodes[currentIndex];
        }

        nodes.Reverse();
        edges.Reverse();
        return new SearchPath([.. nodes], [.. edges], distances[destinationIndex]);
    }

    public static void ValidateDirection(GraphAlgorithmTraversalDirection direction)
    {
        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown traversal direction.");
        }
    }

    internal sealed record EdgeConfiguration(bool[] Included, double[] Costs);

    internal sealed record SearchPath(int[] NodeIndices, int[] EdgeIndices, double TotalCost);

    private readonly record struct SearchState(int NodeIndex, double Cost);

    private sealed record SearchBuffers(double[] Distances, int[] PreviousNodes, int[] PreviousEdges);

    /// <summary>
    /// Groups the inputs for FindShortestPath.
    /// </summary>
    public sealed class FindShortestPathParameters
    {
        /// <summary>
        /// Gets or initializes projection.
        /// </summary>
        public required GraphProjection Projection { get; init; }

        /// <summary>
        /// Gets or initializes startIndex.
        /// </summary>
        public required int StartIndex { get; init; }

        /// <summary>
        /// Gets or initializes destinationIndex.
        /// </summary>
        public required int DestinationIndex { get; init; }

        /// <summary>
        /// Gets or initializes direction.
        /// </summary>
        public required GraphAlgorithmTraversalDirection Direction { get; init; }

        /// <summary>
        /// Gets or initializes edges.
        /// </summary>
        public required EdgeConfiguration Edges { get; init; }

        /// <summary>
        /// Gets or initializes heuristic.
        /// </summary>
        public required Func<Node, Node, double>? Heuristic { get; init; }

        /// <summary>
        /// Gets or initializes bannedNodes.
        /// </summary>
        public required IReadOnlySet<int>? BannedNodes { get; init; }

        /// <summary>
        /// Gets or initializes bannedEdges.
        /// </summary>
        public required IReadOnlySet<int>? BannedEdges { get; init; }

        /// <summary>
        /// Gets or initializes cancellationToken.
        /// </summary>
        public required CancellationToken CancellationToken { get; init; }
    }

    private readonly ref struct RelaxParameters
    {
        public required GraphProjection Projection { get; init; }

        public required ReadOnlySpan<GraphProjectionArc> Arcs { get; init; }

        public required int CurrentIndex { get; init; }

        public required Node Destination { get; init; }

        public required EdgeConfiguration Edges { get; init; }

        public required Func<Node, Node, double>? Heuristic { get; init; }

        public required IReadOnlySet<int>? BannedNodes { get; init; }

        public required IReadOnlySet<int>? BannedEdges { get; init; }

        public required double[] Distances { get; init; }

        public required int[] PreviousNodes { get; init; }

        public required int[] PreviousEdges { get; init; }

        public required PriorityQueue<SearchState, (double Estimate, double Cost, int Node)> Pending { get; init; }
    }
}