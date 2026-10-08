using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Application.Intelligence.Algorithms.Pathfinding;
using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Algorithms.Planning;

/// <summary>
/// Finds a bounded Pareto frontier using multi-objective label setting over loopless paths.
/// </summary>
public sealed class GraphParetoRouteAlgorithm : IGraphAlgorithm<GraphParetoRouteResult>
{
    private readonly Guid _sourceNodeId;
    private readonly Guid _destinationNodeId;
    private readonly GraphParetoRouteOptions _options;

    /// <summary>
    /// Initializes a multi-criteria Pareto route algorithm.
    /// </summary>
    public GraphParetoRouteAlgorithm(Guid sourceNodeId, Guid destinationNodeId, GraphParetoRouteOptions options)
    {
        _sourceNodeId = sourceNodeId;
        _destinationNodeId = destinationNodeId;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ValidateOptions(_options);
    }

    /// <inheritdoc />
    public GraphParetoRouteResult Execute(GraphProjection projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var sourceIndex = projection.GetNodeIndex(_sourceNodeId);
        var destinationIndex = projection.GetNodeIndex(_destinationNodeId);
        var edgeMetrics = CreateEdgeMetrics(projection, cancellationToken);
        var labelsByNode = Enumerable.Range(0, projection.Statistics.NodeCount).Select(_ => new List<RouteLabel>()).ToArray();
        var initial = new RouteLabel(sourceIndex, [sourceIndex], [], new double[_options.Criteria.Count], new HashSet<int> { sourceIndex });
        var pending = new PriorityQueue<RouteLabel, (int Hops, double Score, string Signature)>();
        var rejected = new List<RejectedLabel>();
        var expandedLabels = 0;
        var truncated = false;

        labelsByNode[sourceIndex].Add(initial);
        pending.Enqueue(initial, CreatePriority(initial));

        while (pending.TryDequeue(out var label, out _))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!labelsByNode[label.NodeIndex].Contains(label))
            {
                continue;
            }

            if (expandedLabels >= _options.MaximumExpandedLabels)
            {
                truncated = true;
                break;
            }

            expandedLabels++;
            if (label.NodeIndex == destinationIndex)
            {
                continue;
            }

            if (_options.Direction is GraphAlgorithmTraversalDirection.Outgoing or GraphAlgorithmTraversalDirection.Both)
            {
                ExpandArcs(
                    new ExpandArcsParameters
                    {
                        Arcs = projection.GetOutgoingArcs(label.NodeIndex),
                        Label = label,
                        DestinationIndex = destinationIndex,
                        EdgeMetrics = edgeMetrics,
                        LabelsByNode = labelsByNode,
                        Pending = pending,
                        Rejected = rejected
                    },
                    ref truncated
                );
            }

            if (_options.Direction is GraphAlgorithmTraversalDirection.Incoming or GraphAlgorithmTraversalDirection.Both)
            {
                ExpandArcs(
                    new ExpandArcsParameters
                    {
                        Arcs = projection.GetIncomingArcs(label.NodeIndex),
                        Label = label,
                        DestinationIndex = destinationIndex,
                        EdgeMetrics = edgeMetrics,
                        LabelsByNode = labelsByNode,
                        Pending = pending,
                        Rejected = rejected
                    },
                    ref truncated
                );
            }
        }

        var destinationLabels = labelsByNode[destinationIndex].Where(label => label.NodeIndex == destinationIndex).ToArray();

        var ranked = RankFrontier(destinationLabels);
        foreach (var omitted in ranked.Skip(_options.MaximumResults))
        {
            AddRejected(rejected, omitted.Label, "Non-dominated, but outside MaximumResults after normalized weighted ranking.");
        }

        var routes = ranked.Take(_options.MaximumResults).Select((item, rank) => CreatePublicRoute(projection, item.Label, item.Score, rank)).ToArray();

        return CreateResult(projection, routes, rejected, expandedLabels, truncated);
    }

    private EdgeMetricConfiguration CreateEdgeMetrics(GraphProjection projection, CancellationToken cancellationToken)
    {
        var included = new bool[projection.Statistics.EdgeCount];
        var values = new double[projection.Statistics.EdgeCount][];

        for (var edgeIndex = 0; edgeIndex < projection.Statistics.EdgeCount; edgeIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var edge = projection.GetEdge(edgeIndex);
            if (_options.EdgePredicate is not null && !_options.EdgePredicate(edge))
            {
                values[edgeIndex] = new double[_options.Criteria.Count];
                continue;
            }

            var metrics = new double[_options.Criteria.Count];
            for (var criterionIndex = 0; criterionIndex < _options.Criteria.Count; criterionIndex++)
            {
                var value = _options.Criteria[criterionIndex].Selector(edge);
                if (!double.IsFinite(value) || value < 0)
                {
                    throw new InvalidOperationException(
                        $"Route criterion '{_options.Criteria[criterionIndex].Name}' for edge '{edge.Id}' must be finite and non-negative but was '{value}'.");
                }

                metrics[criterionIndex] = value;
            }

            included[edgeIndex] = true;
            values[edgeIndex] = metrics;
        }

        return new EdgeMetricConfiguration(included, values);
    }

    private void ExpandArcs(ExpandArcsParameters inputs, ref bool truncated)
    {
        var arcs = inputs.Arcs;
        var label = inputs.Label;
        var destinationIndex = inputs.DestinationIndex;
        var edgeMetrics = inputs.EdgeMetrics;
        var labelsByNode = inputs.LabelsByNode;
        var pending = inputs.Pending;
        var rejected = inputs.Rejected;
        var context = new RouteExpansionContext(destinationIndex, edgeMetrics, labelsByNode, pending, rejected);

        foreach (var arc in arcs)
        {
            ExpandArc(arc, label, context, ref truncated);
        }
    }

    private void ExpandArc(GraphProjectionArc arc, RouteLabel label, RouteExpansionContext context, ref bool truncated)
    {
        if (!context.EdgeMetrics.Included[arc.EdgeIndex] || label.VisitedNodes.Contains(arc.NodeIndex))
        {
            return;
        }

        var candidate = CreateCandidate(label, arc, context.EdgeMetrics);
        var existingLabels = context.LabelsByNode[arc.NodeIndex];
        var isDestination = arc.NodeIndex == context.DestinationIndex;
        if (RejectDominatedCandidate(existingLabels, candidate, isDestination, context.Rejected) ||
            RejectLaterEquivalent(existingLabels, candidate, isDestination, context.Rejected))
        {
            return;
        }

        RemoveDominatedLabels(existingLabels, candidate, isDestination, context.Rejected);
        existingLabels.Add(candidate);
        context.Pending.Enqueue(candidate, CreatePriority(candidate));
        TrimOverflow(existingLabels, isDestination, context.Rejected, ref truncated);
    }

    private RouteLabel CreateCandidate(RouteLabel label, GraphProjectionArc arc, EdgeMetricConfiguration edgeMetrics)
    {
        var metrics = new double[_options.Criteria.Count];
        for (var criterionIndex = 0; criterionIndex < metrics.Length; criterionIndex++)
        {
            metrics[criterionIndex] =
                label.Metrics[criterionIndex] +
                edgeMetrics.Values[arc.EdgeIndex][criterionIndex];
        }

        return new RouteLabel(
            arc.NodeIndex,
            [.. label.NodeIndices, arc.NodeIndex],
            [.. label.EdgeIndices, arc.EdgeIndex],
            metrics,
            new HashSet<int>(label.VisitedNodes) { arc.NodeIndex });
    }

    private bool RejectDominatedCandidate(List<RouteLabel> existingLabels, RouteLabel candidate, bool isDestination, List<RejectedLabel> rejected)
    {
        var dominating = existingLabels.FirstOrDefault(existing => Dominates(existing, candidate, isDestination));
        if (dominating is null)
        {
            return false;
        }

        if (isDestination)
        {
            AddRejected(rejected, candidate, ExplainDominance(dominating, candidate));
        }

        return true;
    }

    private bool RejectLaterEquivalent(List<RouteLabel> existingLabels, RouteLabel candidate, bool isDestination, List<RejectedLabel> rejected)
    {
        var equivalent = existingLabels.FirstOrDefault(existing => MetricsEqual(existing, candidate));
        if (equivalent is null)
        {
            return false;
        }

        if (string.CompareOrdinal(Signature(candidate), Signature(equivalent)) >= 0)
        {
            if (isDestination)
            {
                AddRejected(rejected, candidate, "Equal metrics to an already retained deterministic route.");
            }

            return true;
        }

        existingLabels.Remove(equivalent);
        if (isDestination)
        {
            AddRejected(rejected, equivalent, "Equal metrics; deterministic topology signature ranked later.");
        }

        return false;
    }

    private void RemoveDominatedLabels(List<RouteLabel> existingLabels, RouteLabel candidate, bool isDestination, List<RejectedLabel> rejected)
    {
        var dominated = existingLabels
            .Where(existing => Dominates(candidate, existing, isDestination))
            .ToArray();
        foreach (var removed in dominated)
        {
            existingLabels.Remove(removed);
            if (isDestination)
            {
                AddRejected(rejected, removed, ExplainDominance(candidate, removed));
            }
        }
    }

    private void TrimOverflow(List<RouteLabel> existingLabels, bool isDestination, List<RejectedLabel> rejected, ref bool truncated)
    {
        if (existingLabels.Count <= _options.MaximumLabelsPerNode)
        {
            return;
        }

        truncated = true;
        var removed = existingLabels
            .OrderBy(ScalarScore)
            .ThenBy(Signature, StringComparer.Ordinal)
            .Skip(_options.MaximumLabelsPerNode)
            .ToArray();
        foreach (var overflow in removed)
        {
            existingLabels.Remove(overflow);
            if (isDestination)
            {
                AddRejected(rejected, overflow, "Removed by MaximumLabelsPerNode safety limit.");
            }
        }
    }

    private bool Dominates(RouteLabel left, RouteLabel right, bool destination)
    {
        if (!destination && !left.VisitedNodes.IsSubsetOf(right.VisitedNodes))
        {
            return false;
        }

        var strictlyBetter = false;
        for (var criterionIndex = 0; criterionIndex < _options.Criteria.Count; criterionIndex++)
        {
            var comparison = left.Metrics[criterionIndex].CompareTo(right.Metrics[criterionIndex]);
            if (_options.Criteria[criterionIndex].Goal == GraphOptimizationGoal.Minimize)
            {
                if (comparison > 0)
                {
                    return false;
                }

                strictlyBetter |= comparison < 0;
            }
            else
            {
                if (comparison < 0)
                {
                    return false;
                }

                strictlyBetter |= comparison > 0;
            }
        }

        return strictlyBetter;
    }

    private static bool MetricsEqual(RouteLabel left, RouteLabel right) =>
        left.Metrics.SequenceEqual(right.Metrics);

    private (int Hops, double Score, string Signature) CreatePriority(RouteLabel label) =>
        (label.EdgeIndices.Length, ScalarScore(label), Signature(label));

    private double ScalarScore(RouteLabel label)
    {
        var score = 0d;
        for (var criterionIndex = 0; criterionIndex < _options.Criteria.Count; criterionIndex++)
        {
            var direction = _options.Criteria[criterionIndex].Goal == GraphOptimizationGoal.Minimize ? 1 : -1;
            score += direction * label.Metrics[criterionIndex] * _options.Criteria[criterionIndex].RankingWeight;
        }

        return score;
    }

    private RankedLabel[] RankFrontier(RouteLabel[] labels)
    {
        if (labels.Length == 0)
        {
            return [];
        }

        var minimums = Enumerable.Range(0, _options.Criteria.Count).Select(index => labels.Min(label => label.Metrics[index])).ToArray();
        var maximums = Enumerable.Range(0, _options.Criteria.Count).Select(index => labels.Max(label => label.Metrics[index])).ToArray();
        var totalWeight = _options.Criteria.Sum(criterion => criterion.RankingWeight);

        return [.. labels
            .Select(label =>
            {
                var weightedLoss = 0d;
                for (var criterionIndex = 0; criterionIndex < _options.Criteria.Count; criterionIndex++)
                {
                    var range = maximums[criterionIndex] - minimums[criterionIndex];
                    var normalizedLoss = 0d;
                    if (!GraphAlgorithmNumeric.IsZero(range))
                    {
                        normalizedLoss = _options.Criteria[criterionIndex].Goal == GraphOptimizationGoal.Minimize
                            ? (label.Metrics[criterionIndex] - minimums[criterionIndex]) / range
                            : (maximums[criterionIndex] - label.Metrics[criterionIndex]) / range;
                    }
                    weightedLoss += normalizedLoss * _options.Criteria[criterionIndex].RankingWeight;
                }

                return new RankedLabel(label, weightedLoss / totalWeight);
            })
            .OrderBy(item => item.Score)
            .ThenBy(item => Signature(item.Label), StringComparer.Ordinal)];
    }

    private GraphParetoRoute CreatePublicRoute(GraphProjection projection, RouteLabel label, double score, int rank)
    {
        var metrics = CreateMetricDictionary(label);
        return new GraphParetoRoute
        {
            Rank = rank,
            Nodes = [.. label.NodeIndices.Select(projection.GetNode)],
            Edges = [.. label.EdgeIndices.Select(projection.GetEdge)],
            Metrics = metrics,
            RankingScore = score,
            Explanation =
                $"Non-dominated across {_options.Criteria.Count} criterion/criteria; normalized weighted loss {score:F3}. " +
                string.Join(", ", metrics.Select(metric => $"{metric.Key}={metric.Value:F3}"))
        };
    }

    private GraphParetoRouteResult CreateResult(GraphProjection projection, GraphParetoRoute[] routes, List<RejectedLabel> rejected, int expandedLabels, bool truncated) =>
        new()
        {
            Routes = routes,
            RejectedAlternatives = [.. rejected
                .Take(_options.MaximumRejectedAlternatives)
                .Select(item => CreateRejectedRoute(projection, item))],
            ExpandedLabels = expandedLabels,
            Truncated = truncated,
            Explanation = truncated
                ? $"Returned {routes.Length} currently non-dominated route(s); a safety limit was reached, so the frontier may be incomplete."
                : $"Returned {routes.Length} non-dominated route(s), ranked only after Pareto filtering; lower RankingScore is preferred."
        };

    private GraphRejectedRoute CreateRejectedRoute(GraphProjection projection, RejectedLabel rejected) =>
        new()
        {
            Nodes = [.. rejected.Label.NodeIndices.Select(projection.GetNode)],
            Edges = [.. rejected.Label.EdgeIndices.Select(projection.GetEdge)],
            Metrics = CreateMetricDictionary(rejected.Label),
            Reason = rejected.Reason
        };

    private Dictionary<string, double> CreateMetricDictionary(RouteLabel label) =>
        Enumerable.Range(0, _options.Criteria.Count)
            .ToDictionary(index => _options.Criteria[index].Name, index => label.Metrics[index]);

    private string ExplainDominance(RouteLabel dominating, RouteLabel rejected) =>
        "Dominated by another complete route: " +
        string.Join(", ", Enumerable.Range(0, _options.Criteria.Count).Select(index =>
            $"{_options.Criteria[index].Name} {dominating.Metrics[index]:F3} vs {rejected.Metrics[index]:F3}"));

    private void AddRejected(List<RejectedLabel> rejected, RouteLabel label, string reason)
    {
        if (rejected.Count < _options.MaximumRejectedAlternatives)
        {
            rejected.Add(new RejectedLabel(label, reason));
        }
    }

    private static string Signature(RouteLabel label) =>
        string.Join("/", label.NodeIndices) + "|" + string.Join("/", label.EdgeIndices);

    private static void ValidateOptions(GraphParetoRouteOptions options)
    {
        GraphPathSearch.ValidateDirection(options.Direction);
        if (options.Criteria.Count == 0)
        {
            throw new ArgumentException("At least one route criterion is required.", nameof(options));
        }

        if (options.Criteria.Any(criterion => criterion is null))
        {
            throw new ArgumentException("Route criteria cannot contain null.", nameof(options));
        }

        if (options.Criteria.Any(criterion => criterion.Selector is null))
        {
            throw new ArgumentException("Every route criterion needs a selector.", nameof(options));
        }

        if (options.Criteria.Any(criterion => string.IsNullOrWhiteSpace(criterion.Name)))
        {
            throw new ArgumentException("Every route criterion needs a non-empty name.", nameof(options));
        }

        if (options.Criteria.Select(criterion => criterion.Name).Distinct(StringComparer.Ordinal).Count() !=
            options.Criteria.Count)
        {
            throw new ArgumentException("Route criterion names must be unique.", nameof(options));
        }

        if (options.Criteria.Any(criterion => !Enum.IsDefined(criterion.Goal)))
        {
            throw new ArgumentException("A route criterion contains an unknown optimization goal.", nameof(options));
        }

        if (options.Criteria.Any(criterion => !double.IsFinite(criterion.RankingWeight) || criterion.RankingWeight < 0) ||
            options.Criteria.Sum(criterion => criterion.RankingWeight) <= 0)
        {
            throw new ArgumentException("Ranking weights must be finite and non-negative, with at least one weight greater than zero.", nameof(options));
        }

        if (options.MaximumResults <= 0 ||
            options.MaximumLabelsPerNode <= 0 ||
            options.MaximumExpandedLabels <= 0 ||
            options.MaximumRejectedAlternatives < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Pareto search and result limits are invalid.");
        }
    }

    private sealed record EdgeMetricConfiguration(bool[] Included, double[][] Values);

    private sealed record RouteExpansionContext(
        int DestinationIndex,
        EdgeMetricConfiguration EdgeMetrics,
        List<RouteLabel>[] LabelsByNode,
        PriorityQueue<RouteLabel, (int Hops, double Score, string Signature)> Pending,
        List<RejectedLabel> Rejected);

    private sealed record RouteLabel(int NodeIndex, int[] NodeIndices, int[] EdgeIndices, double[] Metrics, HashSet<int> VisitedNodes);

    private sealed record RejectedLabel(RouteLabel Label, string Reason);

    private sealed record RankedLabel(RouteLabel Label, double Score);

    private readonly ref struct ExpandArcsParameters
    {
        public required ReadOnlySpan<GraphProjectionArc> Arcs { get; init; }

        public required RouteLabel Label { get; init; }

        public required int DestinationIndex { get; init; }

        public required EdgeMetricConfiguration EdgeMetrics { get; init; }

        public required List<RouteLabel>[] LabelsByNode { get; init; }

        public required PriorityQueue<RouteLabel, (int Hops, double Score, string Signature)> Pending { get; init; }

        public required List<RejectedLabel> Rejected { get; init; }
    }
}