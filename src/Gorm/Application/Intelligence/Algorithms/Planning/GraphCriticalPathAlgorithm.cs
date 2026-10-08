using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Application.Intelligence.Algorithms.Connectivity;
using Gorm.Application.Intelligence.Projection;
using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Planning;

/// <summary>
/// Calculates earliest/latest schedules, slack and critical paths for a directed acyclic graph.
/// </summary>
public sealed class GraphCriticalPathAlgorithm : IGraphAlgorithm<GraphCriticalPathResult>
{
    private readonly GraphCriticalPathOptions _options;

    /// <summary>
    /// Initializes a Critical Path Method algorithm.
    /// </summary>
    public GraphCriticalPathAlgorithm(GraphCriticalPathOptions? options = null)
    {
        _options = options ?? new GraphCriticalPathOptions();

        if (!double.IsFinite(_options.CriticalTolerance) || _options.CriticalTolerance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Critical tolerance must be finite and non-negative.");
        }
    }

    /// <inheritdoc />
    public GraphCriticalPathResult Execute(GraphProjection projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var nodeCount = projection.Statistics.NodeCount;
        if (nodeCount == 0)
        {
            return new GraphCriticalPathResult(0, [], [], [], [], []);
        }

        var durations = CreateDurations(projection, cancellationToken);
        var (includedEdges, lags) = CreateEdgeConfiguration(projection, cancellationToken);
        var topologicalOrder = CreateTopologicalOrder(projection, includedEdges, cancellationToken);
        var earliestStarts = new double[nodeCount];
        var earliestFinishes = new double[nodeCount];
        var primaryPredecessorNodes = new int[nodeCount];
        var primaryPredecessorEdges = new int[nodeCount];

        Array.Fill(primaryPredecessorNodes, -1);
        Array.Fill(primaryPredecessorEdges, -1);

        CalculateEarliestSchedule(
            new CalculateEarliestScheduleParameters
            {
                Projection = projection,
                TopologicalOrder = topologicalOrder,
                IncludedEdges = includedEdges,
                Lags = lags,
                Durations = durations,
                EarliestStarts = earliestStarts,
                EarliestFinishes = earliestFinishes,
                PrimaryPredecessorNodes = primaryPredecessorNodes,
                PrimaryPredecessorEdges = primaryPredecessorEdges,
                CancellationToken = cancellationToken
            });

        var projectDuration = earliestFinishes.Max();
        var latestStarts = new double[nodeCount];
        var latestFinishes = new double[nodeCount];
        CalculateLatestSchedule(
            new CalculateLatestScheduleParameters
            {
                Projection = projection,
                TopologicalOrder = topologicalOrder,
                IncludedEdges = includedEdges,
                Lags = lags,
                Durations = durations,
                ProjectDuration = projectDuration,
                LatestStarts = latestStarts,
                LatestFinishes = latestFinishes,
                CancellationToken = cancellationToken
            });

        var schedulesByIndex = new GraphCriticalPathNodeSchedule[nodeCount];
        for (var nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
        {
            var slack = latestStarts[nodeIndex] - earliestStarts[nodeIndex];
            schedulesByIndex[nodeIndex] = new GraphCriticalPathNodeSchedule
            {
                Node = projection.GetNode(nodeIndex),
                Duration = durations[nodeIndex],
                EarliestStart = earliestStarts[nodeIndex],
                EarliestFinish = earliestFinishes[nodeIndex],
                LatestStart = latestStarts[nodeIndex],
                LatestFinish = latestFinishes[nodeIndex],
                Slack = slack,
                IsCritical = Math.Abs(slack) <= _options.CriticalTolerance
            };
        }

        var criticalNodes = topologicalOrder.Where(nodeIndex => schedulesByIndex[nodeIndex].IsCritical).Select(projection.GetNode).ToArray();

        var criticalEdges = FindCriticalEdges(projection, includedEdges, lags, schedulesByIndex, earliestStarts, earliestFinishes);

        var (primaryNodes, primaryEdges) = CreatePrimaryPath(projection, earliestFinishes, primaryPredecessorNodes, primaryPredecessorEdges);

        return new GraphCriticalPathResult(
            projectDuration,
            [.. topologicalOrder.Select(nodeIndex => schedulesByIndex[nodeIndex])],
            criticalNodes,
            criticalEdges,
            primaryNodes,
            primaryEdges);
    }

    private double[] CreateDurations(GraphProjection projection, CancellationToken cancellationToken)
    {
        var durations = new double[projection.Statistics.NodeCount];
        for (var nodeIndex = 0; nodeIndex < durations.Length; nodeIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var node = projection.GetNode(nodeIndex);
            var duration = _options.DurationSelector?.Invoke(node) ?? 0;
            if (!double.IsFinite(duration) || duration < 0)
            {
                throw new InvalidOperationException($"The duration for node '{node.Id}' must be finite and non-negative but was '{duration}'.");
            }

            durations[nodeIndex] = duration;
        }

        return durations;
    }

    private (bool[] IncludedEdges, double[] Lags) CreateEdgeConfiguration(GraphProjection projection, CancellationToken cancellationToken)
    {
        var includedEdges = new bool[projection.Statistics.EdgeCount];
        var lags = new double[projection.Statistics.EdgeCount];

        for (var edgeIndex = 0; edgeIndex < includedEdges.Length; edgeIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var edge = projection.GetEdge(edgeIndex);
            includedEdges[edgeIndex] = _options.EdgePredicate?.Invoke(edge) ?? true;
            if (!includedEdges[edgeIndex])
            {
                continue;
            }

            var lag = _options.LagSelector?.Invoke(edge) ?? 1;
            if (!double.IsFinite(lag) || lag < 0)
            {
                throw new InvalidOperationException($"The lag for edge '{edge.Id}' must be finite and non-negative but was '{lag}'.");
            }

            lags[edgeIndex] = lag;
        }

        return (includedEdges, lags);
    }

    private static int[] CreateTopologicalOrder(GraphProjection projection, bool[] includedEdges, CancellationToken cancellationToken)
    {
        var nodeCount = projection.Statistics.NodeCount;
        var indegrees = CalculateIndegrees(projection, includedEdges);
        var pending = CreateInitialQueue(indegrees);

        var order = new List<int>(nodeCount);
        while (pending.TryDequeue(out var nodeIndex, out _))
        {
            cancellationToken.ThrowIfCancellationRequested();
            order.Add(nodeIndex);

            EnqueueNewlyAvailableNodes(projection, includedEdges, indegrees, pending, nodeIndex);
        }

        if (order.Count != nodeCount)
        {
            var remainingNodeIds = Enumerable.Range(0, nodeCount)
                .Where(nodeIndex => indegrees[nodeIndex] > 0)
                .Select(nodeIndex => projection.GetNode(nodeIndex).Id)
                .Order()
                .ToArray();
            throw new GraphCycleDetectedException([.. remainingNodeIds]);
        }

        return [.. order];
    }

    private static int[] CalculateIndegrees(GraphProjection projection, bool[] includedEdges)
    {
        var indegrees = new int[projection.Statistics.NodeCount];
        for (var nodeIndex = 0; nodeIndex < indegrees.Length; nodeIndex++)
        {
            foreach (var arc in projection.GetOutgoingArcs(nodeIndex))
            {
                if (includedEdges[arc.EdgeIndex])
                {
                    indegrees[arc.NodeIndex]++;
                }
            }
        }

        return indegrees;
    }

    private static PriorityQueue<int, int> CreateInitialQueue(int[] indegrees)
    {
        var pending = new PriorityQueue<int, int>();
        for (var nodeIndex = 0; nodeIndex < indegrees.Length; nodeIndex++)
        {
            if (indegrees[nodeIndex] == 0)
            {
                pending.Enqueue(nodeIndex, nodeIndex);
            }
        }

        return pending;
    }

    private static void EnqueueNewlyAvailableNodes(GraphProjection projection, bool[] includedEdges, int[] indegrees, PriorityQueue<int, int> pending, int nodeIndex)
    {
        foreach (var arc in projection.GetOutgoingArcs(nodeIndex))
        {
            if (includedEdges[arc.EdgeIndex] && --indegrees[arc.NodeIndex] == 0)
            {
                pending.Enqueue(arc.NodeIndex, arc.NodeIndex);
            }
        }
    }

    private static void CalculateEarliestSchedule(CalculateEarliestScheduleParameters inputs)
    {
        var projection = inputs.Projection;
        var topologicalOrder = inputs.TopologicalOrder;
        var includedEdges = inputs.IncludedEdges;
        var lags = inputs.Lags;
        var durations = inputs.Durations;
        var earliestStarts = inputs.EarliestStarts;
        var earliestFinishes = inputs.EarliestFinishes;
        var primaryPredecessorNodes = inputs.PrimaryPredecessorNodes;
        var primaryPredecessorEdges = inputs.PrimaryPredecessorEdges;
        var cancellationToken = inputs.CancellationToken;

        foreach (var nodeIndex in topologicalOrder)
        {
            cancellationToken.ThrowIfCancellationRequested();
            earliestFinishes[nodeIndex] = earliestStarts[nodeIndex] + durations[nodeIndex];

            foreach (var arc in projection.GetOutgoingArcs(nodeIndex))
            {
                if (!includedEdges[arc.EdgeIndex])
                {
                    continue;
                }

                var candidateStart = earliestFinishes[nodeIndex] + lags[arc.EdgeIndex];
                var isBetter = candidateStart > earliestStarts[arc.NodeIndex];
                var isDeterministicTie =
                    GraphAlgorithmNumeric.AreEqual(candidateStart, earliestStarts[arc.NodeIndex]) &&
                    (primaryPredecessorNodes[arc.NodeIndex] < 0 ||
                     nodeIndex < primaryPredecessorNodes[arc.NodeIndex] ||
                     (nodeIndex == primaryPredecessorNodes[arc.NodeIndex] && arc.EdgeIndex < primaryPredecessorEdges[arc.NodeIndex]));

                if (!isBetter && !isDeterministicTie)
                {
                    continue;
                }

                earliestStarts[arc.NodeIndex] = candidateStart;
                primaryPredecessorNodes[arc.NodeIndex] = nodeIndex;
                primaryPredecessorEdges[arc.NodeIndex] = arc.EdgeIndex;
            }
        }
    }

    private static void CalculateLatestSchedule(CalculateLatestScheduleParameters inputs)
    {
        var projection = inputs.Projection;
        var topologicalOrder = inputs.TopologicalOrder;
        var includedEdges = inputs.IncludedEdges;
        var lags = inputs.Lags;
        var durations = inputs.Durations;
        var projectDuration = inputs.ProjectDuration;
        var latestStarts = inputs.LatestStarts;
        var latestFinishes = inputs.LatestFinishes;
        var cancellationToken = inputs.CancellationToken;

        Array.Fill(latestFinishes, projectDuration);

        for (var orderIndex = topologicalOrder.Length - 1; orderIndex >= 0; orderIndex--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var nodeIndex = topologicalOrder[orderIndex];
            var latestFinish = double.PositiveInfinity;

            foreach (var arc in projection.GetOutgoingArcs(nodeIndex))
            {
                if (includedEdges[arc.EdgeIndex])
                {
                    latestFinish = Math.Min(latestFinish, latestStarts[arc.NodeIndex] - lags[arc.EdgeIndex]);
                }
            }

            if (!double.IsPositiveInfinity(latestFinish))
            {
                latestFinishes[nodeIndex] = latestFinish;
            }

            latestStarts[nodeIndex] = latestFinishes[nodeIndex] - durations[nodeIndex];
        }
    }

    private Edge[] FindCriticalEdges(
        GraphProjection projection,
        bool[] includedEdges,
        double[] lags,
        GraphCriticalPathNodeSchedule[] schedules,
        double[] earliestStarts,
        double[] earliestFinishes)
    {
        var result = new List<Edge>();
        for (var edgeIndex = 0; edgeIndex < includedEdges.Length; edgeIndex++)
        {
            if (!includedEdges[edgeIndex])
            {
                continue;
            }

            var edge = projection.GetEdge(edgeIndex);
            var fromIndex = projection.GetNodeIndex(edge.FromId);
            var toIndex = projection.GetNodeIndex(edge.ToId);
            if (schedules[fromIndex].IsCritical &&
                schedules[toIndex].IsCritical &&
                Math.Abs(earliestStarts[toIndex] - (earliestFinishes[fromIndex] + lags[edgeIndex])) <=
                _options.CriticalTolerance)
            {
                result.Add(edge);
            }
        }

        return [.. result];
    }

    private static (Node[] Nodes, Edge[] Edges) CreatePrimaryPath(GraphProjection projection, double[] earliestFinishes, int[] predecessorNodes, int[] predecessorEdges)
    {
        var destinationIndex = Enumerable.Range(0, earliestFinishes.Length).OrderByDescending(nodeIndex => earliestFinishes[nodeIndex]).ThenBy(nodeIndex => nodeIndex).First();
        var nodes = new List<Node>();
        var edges = new List<Edge>();
        var currentIndex = destinationIndex;

        while (currentIndex >= 0)
        {
            nodes.Add(projection.GetNode(currentIndex));
            var edgeIndex = predecessorEdges[currentIndex];
            if (edgeIndex >= 0)
            {
                edges.Add(projection.GetEdge(edgeIndex));
            }

            currentIndex = predecessorNodes[currentIndex];
        }

        nodes.Reverse();
        edges.Reverse();
        return ([.. nodes], [.. edges]);
    }

    private sealed class CalculateEarliestScheduleParameters
    {
        public required GraphProjection Projection { get; init; }

        public required int[] TopologicalOrder { get; init; }

        public required bool[] IncludedEdges { get; init; }

        public required double[] Lags { get; init; }

        public required double[] Durations { get; init; }

        public required double[] EarliestStarts { get; init; }

        public required double[] EarliestFinishes { get; init; }

        public required int[] PrimaryPredecessorNodes { get; init; }

        public required int[] PrimaryPredecessorEdges { get; init; }

        public required CancellationToken CancellationToken { get; init; }
    }

    private sealed class CalculateLatestScheduleParameters
    {
        public required GraphProjection Projection { get; init; }

        public required int[] TopologicalOrder { get; init; }

        public required bool[] IncludedEdges { get; init; }

        public required double[] Lags { get; init; }

        public required double[] Durations { get; init; }

        public required double ProjectDuration { get; init; }

        public required double[] LatestStarts { get; init; }

        public required double[] LatestFinishes { get; init; }

        public required CancellationToken CancellationToken { get; init; }
    }
}