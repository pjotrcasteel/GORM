using Gorm.Application.Intelligence.Algorithms.Flow;
using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Temporal.Scenarios;
using Gorm.Application.Temporal.Snapshots;
using Gorm.Core.Primitives;

namespace Gorm.Application.Temporal.Simulation.Failures;

/// <summary>
/// Simulates simultaneous capacity-shortfall cascades on an isolated temporal world.
/// </summary>
public static class GraphCascadingFailureSimulator
{
    /// <summary>
    /// Runs one deterministic cascading-failure and optional source-to-destination capacity simulation.
    /// </summary>
    /// <param name="baseline">Immutable initial world.</param>
    /// <param name="scenarioId">Isolated failure scenario id.</param>
    /// <param name="simulatedAt">Valid and recorded time assigned to failure mutations.</param>
    /// <param name="options">Demand, capacity, flow and safety options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cascade, node, flow and final-world evidence.</returns>
    public static GraphCascadingFailureResult Run(
        GraphWorldSnapshot baseline,
        GraphScenarioId scenarioId,
        DateTimeOffset simulatedAt,
        GraphCascadingFailureOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(scenarioId);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);
        var projection = baseline.CreateProjection(cancellationToken);
        var demands = projection.Nodes.ToDictionary(node => node.Id, node => ValidateNonNegative(options.DemandSelector(node), "demand", node.Id));
        var capacities = projection.Edges.ToDictionary(edge => edge.Id, edge => ValidateNonNegative(options.CapacitySelector(edge), "capacity", edge.Id));
        var selectedEdges = projection.Edges.Where(edge => options.EdgePredicate?.Invoke(edge) ?? true).OrderBy(edge => edge.Id).ToArray();
        var baselineIncomingCounts = projection.Nodes.ToDictionary(node => node.Id, node => selectedEdges.Count(edge => edge.ToId == node.Id));
        var active = projection.Nodes.Select(node => node.Id).ToHashSet();
        var failedRoundByNode = new Dictionary<Guid, int>();
        var availableByNode = projection.Nodes.ToDictionary(node => node.Id, _ => 0d);
        var rounds = new List<GraphCascadeRound>();
        var initial = options.InitiallyFailedNodeIds.Distinct().Order().ToArray();
        foreach (var nodeId in initial)
        {
            if (!active.Remove(nodeId))
            {
                throw new ArgumentException($"Initial failed node '{nodeId}' is not part of the baseline.", nameof(options));
            }

            failedRoundByNode.Add(nodeId, 0);
        }

        EnsureFailureLimit(failedRoundByNode.Count, options.MaximumFailures);
        if (initial.Length > 0)
        {
            rounds.Add(new GraphCascadeRound { Round = 0, FailedNodeIds = initial });
        }

        var failureInputs = new FindFailuresParameters
        {
            Active = active,
            Demands = demands,
            Capacities = capacities,
            SelectedEdges = selectedEdges,
            BaselineIncomingCounts = baselineIncomingCounts,
            AvailableByNode = availableByNode,
            Options = options,
            CancellationToken = cancellationToken
        };
        var truncated = RunCascade(active, failedRoundByNode, rounds, failureInputs);

        UpdateAvailableCapacities(active, capacities, selectedEdges, availableByNode, cancellationToken);
        var scenario = GraphScenario.Fork(
            baseline,
            scenarioId,
            new GraphScenarioOptions { MaximumRevisions = options.MaximumFailures });
        foreach (var nodeId in failedRoundByNode.OrderBy(item => item.Value).ThenBy(item => item.Key).Select(item => item.Key))
        {
            scenario = scenario.Apply(GraphScenarioMutation.RemoveNode(nodeId, simulatedAt, simulatedAt, "Capacity cascade failure"), cancellationToken).Scenario;
        }

        var statuses = projection.Nodes
            .OrderBy(node => node.Id)
            .Select(node => new GraphCascadeNodeStatus
            {
                NodeId = node.Id,
                Demand = demands[node.Id],
                AvailableIncomingCapacity = availableByNode[node.Id],
                IsSource = baselineIncomingCounts[node.Id] == 0,
                FailedRound = failedRoundByNode.GetValueOrDefault(node.Id, -1) is var failedRound && failedRound >= 0
                    ? failedRound
                    : null
            })
            .ToArray();
        var (baselineFlow, survivingFlow) = CalculateFlows(projection, scenario.Current.CreateProjection(cancellationToken), options, cancellationToken);
        return new GraphCascadingFailureResult(scenario, [.. rounds], statuses, baselineFlow, survivingFlow, truncated);
    }

    private static bool RunCascade(
        ISet<Guid> active,
        IDictionary<Guid, int> failedRoundByNode,
        ICollection<GraphCascadeRound> rounds,
        FindFailuresParameters inputs)
    {
        for (var round = 1; round <= inputs.Options.MaximumRounds; round++)
        {
            inputs.CancellationToken.ThrowIfCancellationRequested();
            var failures = FindFailures(inputs);
            if (failures.Length == 0)
            {
                return false;
            }

            EnsureFailureLimit(failedRoundByNode.Count + failures.Length, inputs.Options.MaximumFailures);
            foreach (var nodeId in failures)
            {
                active.Remove(nodeId);
                failedRoundByNode.Add(nodeId, round);
            }

            rounds.Add(new GraphCascadeRound { Round = round, FailedNodeIds = failures });
            if (round == inputs.Options.MaximumRounds)
            {
                return FindFailures(inputs).Length > 0;
            }
        }

        return false;
    }

    private static Guid[] FindFailures(FindFailuresParameters inputs)
    {
        var active = inputs.Active;
        var demands = inputs.Demands;
        var capacities = inputs.Capacities;
        var selectedEdges = inputs.SelectedEdges;
        var baselineIncomingCounts = inputs.BaselineIncomingCounts;
        var availableByNode = inputs.AvailableByNode;
        var options = inputs.Options;
        var cancellationToken = inputs.CancellationToken;

        UpdateAvailableCapacities(active, capacities, selectedEdges, availableByNode, cancellationToken);
        return
        [
            .. active
                .Where(nodeId => !options.ProtectSourceNodes || baselineIncomingCounts[nodeId] > 0)
                .Where(nodeId => availableByNode[nodeId] + options.Epsilon < demands[nodeId])
                .Order()
        ];
    }

    private static void UpdateAvailableCapacities(
        IReadOnlySet<Guid> active,
        IReadOnlyDictionary<Guid, double> capacities,
        IReadOnlyList<Edge> selectedEdges,
        IDictionary<Guid, double> availableByNode,
        CancellationToken cancellationToken)
    {
        foreach (var nodeId in availableByNode.Keys.ToArray())
        {
            availableByNode[nodeId] = 0;
        }

        foreach (var edge in selectedEdges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (active.Contains(edge.FromId) && active.Contains(edge.ToId))
            {
                availableByNode[edge.ToId] += capacities[edge.Id];
            }
        }
    }

    private static (double? Baseline, double? Surviving) CalculateFlows(
        GraphProjection baseline,
        GraphProjection surviving,
        GraphCascadingFailureOptions options,
        CancellationToken cancellationToken)
    {
        if (options.FlowSourceNodeId is not Guid source || options.FlowDestinationNodeId is not Guid destination)
        {
            return (null, null);
        }

        var flowOptions = new GraphMaximumFlowOptions
        {
            CapacitySelector = options.CapacitySelector,
            EdgePredicate = options.EdgePredicate,
            MaximumAugmentations = options.MaximumFlowAugmentations,
            Epsilon = Math.Max(options.Epsilon, 1e-12)
        };
        var baselineFlow = baseline.Run(new GraphMaximumFlowAlgorithm(source, destination, flowOptions), cancellationToken).MaximumFlow;
        var survivingFlow = surviving.ContainsNode(source) && surviving.ContainsNode(destination)
            ? surviving.Run(new GraphMaximumFlowAlgorithm(source, destination, flowOptions), cancellationToken).MaximumFlow
            : 0;
        return (baselineFlow, survivingFlow);
    }

    private static double ValidateNonNegative(double value, string kind, Guid entityId)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new InvalidOperationException(
                $"Cascade {kind} for entity '{entityId}' must be finite and non-negative but was {value}.");
        }

        return value;
    }

    private static void EnsureFailureLimit(int count, int maximumFailures)
    {
        if (count > maximumFailures)
        {
            throw new InvalidOperationException($"Cascade exceeded MaximumFailures ({maximumFailures}).");
        }
    }

    private static void ValidateOptions(GraphCascadingFailureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.DemandSelector);
        ArgumentNullException.ThrowIfNull(options.CapacitySelector);
        if (options.MaximumRounds <= 0 || options.MaximumFailures <= 0 || options.MaximumFlowAugmentations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Cascade limits must be positive.");
        }

        if (!double.IsFinite(options.Epsilon) || options.Epsilon < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Epsilon must be finite and non-negative.");
        }

        if ((options.FlowSourceNodeId is null) != (options.FlowDestinationNodeId is null))
        {
            throw new ArgumentException("Flow source and destination must be configured together.", nameof(options));
        }
    }

    private sealed class FindFailuresParameters
    {
        public required IReadOnlySet<Guid> Active { get; init; }

        public required IReadOnlyDictionary<Guid, double> Demands { get; init; }

        public required IReadOnlyDictionary<Guid, double> Capacities { get; init; }

        public required IReadOnlyList<Edge> SelectedEdges { get; init; }

        public required IReadOnlyDictionary<Guid, int> BaselineIncomingCounts { get; init; }

        public required IDictionary<Guid, double> AvailableByNode { get; init; }

        public required GraphCascadingFailureOptions Options { get; init; }

        public required CancellationToken CancellationToken { get; init; }
    }
}