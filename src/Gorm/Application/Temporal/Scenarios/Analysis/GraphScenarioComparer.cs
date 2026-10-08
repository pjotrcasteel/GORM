using Gorm.Application.Intelligence.Algorithms.Communities;
using Gorm.Application.Intelligence.Algorithms.Connectivity;
using Gorm.Application.Intelligence.Algorithms.Planning;
using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Temporal.Diff;

namespace Gorm.Application.Temporal.Scenarios.Analysis;

/// <summary>
/// Runs deterministic topology and Intelligence comparisons between a scenario and its baseline.
/// </summary>
public static class GraphScenarioComparer
{
    /// <summary>
    /// Compares the current scenario revision with its unchanged baseline.
    /// </summary>
    /// <param name="scenario">Scenario to compare.</param>
    /// <param name="options">Built-in and domain comparison options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Topology, Intelligence and metric deltas.</returns>
    public static GraphScenarioComparisonResult Compare(GraphScenario scenario, GraphScenarioComparisonOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        options ??= new GraphScenarioComparisonOptions();
        ValidateOptions(options);

        var baselineProjection = scenario.Baseline.CreateProjection(cancellationToken);
        var scenarioProjection = scenario.Current.CreateProjection(cancellationToken);
        var baselineEvidence = Analyze(baselineProjection, options, cancellationToken);
        var scenarioEvidence = Analyze(scenarioProjection, options, cancellationToken);
        var metrics = CreateMetricDeltas(baselineProjection, scenarioProjection, baselineEvidence, scenarioEvidence, options, cancellationToken);

        return new GraphScenarioComparisonResult(
            scenario.Id,
            GraphWorldSnapshotDiffer.Compare(scenario.Baseline, scenario.Current, cancellationToken: cancellationToken),
            baselineEvidence,
            scenarioEvidence,
            metrics);
    }

    private static GraphScenarioIntelligenceSnapshot Analyze(GraphProjection projection, GraphScenarioComparisonOptions options, CancellationToken cancellationToken)
    {
        var components = projection.Run(new GraphConnectedComponentsAlgorithm(), cancellationToken);
        double? criticalPathDuration = null;
        int? criticalNodeCount = null;
        string? criticalPathUnavailableReason = null;
        if (options.IncludeCriticalPath)
        {
            try
            {
                var criticalPath = projection.Run(new GraphCriticalPathAlgorithm(options.CriticalPathOptions), cancellationToken);
                criticalPathDuration = criticalPath.ProjectDuration;
                criticalNodeCount = criticalPath.CriticalNodes.Count;
            }
            catch (GraphCycleDetectedException exception)
            {
                criticalPathUnavailableReason = exception.Message;
            }
        }

        int? communityCount = null;
        double? modularity = null;
        if (options.IncludeCommunities)
        {
            var communities = projection.Run(new GraphLeidenCommunityAlgorithm(options.CommunityOptions), cancellationToken);
            communityCount = communities.Communities.Count;
            modularity = communities.Modularity;
        }

        int? reachableCount = null;
        bool? impactSourceAvailable = null;
        if (options.ImpactSourceNodeId is Guid sourceNodeId)
        {
            impactSourceAvailable = projection.ContainsNode(sourceNodeId);
            reachableCount = impactSourceAvailable.Value ? CountOutgoingReachable(projection, sourceNodeId, options.MaximumImpactNodes, cancellationToken) : 0;
        }

        return new GraphScenarioIntelligenceSnapshot
        {
            NodeCount = projection.Statistics.NodeCount,
            EdgeCount = projection.Statistics.EdgeCount,
            Density = projection.Statistics.Density,
            WeakComponentCount = components.Components.Count,
            CriticalPathDuration = criticalPathDuration,
            CriticalNodeCount = criticalNodeCount,
            CriticalPathUnavailableReason = criticalPathUnavailableReason,
            CommunityCount = communityCount,
            Modularity = modularity,
            ImpactReachableNodeCount = reachableCount,
            ImpactSourceAvailable = impactSourceAvailable
        };
    }

    private static GraphScenarioMetricDelta[] CreateMetricDeltas(
        GraphProjection baselineProjection,
        GraphProjection scenarioProjection,
        GraphScenarioIntelligenceSnapshot baseline,
        GraphScenarioIntelligenceSnapshot scenario,
        GraphScenarioComparisonOptions options,
        CancellationToken cancellationToken)
    {
        var metrics = new List<GraphScenarioMetricDelta>
        {
            Metric("topology.nodes", "nodes", baseline.NodeCount, scenario.NodeCount),
            Metric("topology.edges", "edges", baseline.EdgeCount, scenario.EdgeCount),
            Metric("topology.density", "ratio", baseline.Density, scenario.Density),
            Metric("topology.weak-components", "components", baseline.WeakComponentCount, scenario.WeakComponentCount)
        };
        AddOptional(metrics, "critical-path.duration", "domain-time", baseline.CriticalPathDuration, scenario.CriticalPathDuration);
        AddOptional(metrics, "critical-path.nodes", "nodes", baseline.CriticalNodeCount, scenario.CriticalNodeCount);
        AddOptional(metrics, "community.count", "communities", baseline.CommunityCount, scenario.CommunityCount);
        AddOptional(metrics, "community.modularity", "score", baseline.Modularity, scenario.Modularity);
        AddOptional(metrics, "impact.reachable-nodes", "nodes", baseline.ImpactReachableNodeCount, scenario.ImpactReachableNodeCount);

        foreach (var definition in options.AdditionalMetrics.OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var baselineValue = definition.Selector(baselineProjection);
            var scenarioValue = definition.Selector(scenarioProjection);
            if (!double.IsFinite(baselineValue) || !double.IsFinite(scenarioValue))
            {
                throw new InvalidOperationException($"Scenario metric '{definition.Name}' returned a non-finite value.");
            }

            metrics.Add(Metric(definition.Name, definition.Unit, baselineValue, scenarioValue));
        }

        return [.. metrics.OrderBy(item => item.Name, StringComparer.Ordinal)];
    }

    private static int CountOutgoingReachable(GraphProjection projection, Guid sourceNodeId, int maximumNodes, CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid> { sourceNodeId };
        var pending = new Queue<Guid>();
        pending.Enqueue(sourceNodeId);
        while (pending.TryDequeue(out var current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var neighborId in projection.GetOutgoingNeighbors(current).OrderBy(node => node.Id).Select(neighbor => neighbor.Id))
            {
                if (!visited.Add(neighborId))
                {
                    continue;
                }

                if (visited.Count > maximumNodes)
                {
                    throw new InvalidOperationException(
                        $"Impact reach exceeded MaximumImpactNodes ({maximumNodes}).");
                }

                pending.Enqueue(neighborId);
            }
        }

        return visited.Count;
    }

    private static void AddOptional(List<GraphScenarioMetricDelta> metrics, string name, string unit, double? baseline, double? scenario)
    {
        if (baseline is not null && scenario is not null)
        {
            metrics.Add(Metric(name, unit, baseline.Value, scenario.Value));
        }
    }

    private static GraphScenarioMetricDelta Metric(string name, string unit, double baseline, double scenario) =>
        new()
        {
            Name = name,
            Unit = unit,
            BaselineValue = baseline,
            ScenarioValue = scenario
        };

    private static void ValidateOptions(GraphScenarioComparisonOptions options)
    {
        if (options.MaximumImpactNodes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaximumImpactNodes must be positive.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in options.AdditionalMetrics)
        {
            ArgumentNullException.ThrowIfNull(definition);
            if (string.IsNullOrWhiteSpace(definition.Name) || !names.Add(definition.Name))
            {
                throw new ArgumentException("Additional scenario metric names must be non-empty and unique.", nameof(options));
            }

            ArgumentNullException.ThrowIfNull(definition.Selector);
        }
    }
}