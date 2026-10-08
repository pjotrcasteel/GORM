using System.Collections.ObjectModel;
using Gorm.Application.Temporal.Scenarios;

namespace Gorm.Application.Temporal.Simulation.Failures;

/// <summary>
/// Contains cascade rounds, capacity evidence and the final isolated failure scenario.
/// </summary>
public sealed class GraphCascadingFailureResult
{
    internal GraphCascadingFailureResult(
        GraphScenario scenario,
        GraphCascadeRound[] rounds,
        GraphCascadeNodeStatus[] nodeStatuses,
        double? baselineMaximumFlow,
        double? survivingMaximumFlow,
        bool truncated)
    {
        Scenario = scenario;
        Rounds = Array.AsReadOnly(rounds);
        NodeStatuses = Array.AsReadOnly(nodeStatuses);
        FailedNodeIds = Array.AsReadOnly(nodeStatuses.Where(status => status.FailedRound is not null).Select(status => status.NodeId).Order().ToArray());
        BaselineMaximumFlow = baselineMaximumFlow;
        SurvivingMaximumFlow = survivingMaximumFlow;
        Truncated = truncated;
        Explanation =
            $"Cascade failed {FailedNodeIds.Count} nodes across {rounds.Length} rounds; " +
            $"propagation{(truncated ? " did" : " did not")} reach the configured round limit.";
    }

    /// <summary>
    /// Gets the final isolated failure scenario.
    /// </summary>
    public GraphScenario Scenario { get; }

    /// <summary>
    /// Gets initial and propagated failure rounds.
    /// </summary>
    public ReadOnlyCollection<GraphCascadeRound> Rounds { get; }

    /// <summary>
    /// Gets status evidence for every baseline node.
    /// </summary>
    public ReadOnlyCollection<GraphCascadeNodeStatus> NodeStatuses { get; }

    /// <summary>
    /// Gets all failed node identifiers.
    /// </summary>
    public ReadOnlyCollection<Guid> FailedNodeIds { get; }

    /// <summary>
    /// Gets baseline maximum flow when source/destination were configured.
    /// </summary>
    public double? BaselineMaximumFlow { get; }

    /// <summary>
    /// Gets surviving maximum flow, or zero when a configured endpoint failed.
    /// </summary>
    public double? SurvivingMaximumFlow { get; }

    /// <summary>
    /// Gets baseline minus surviving maximum flow when calculated.
    /// </summary>
    public double? CapacityLoss => BaselineMaximumFlow - SurvivingMaximumFlow;

    /// <summary>
    /// Gets whether propagation may remain after the configured round limit.
    /// </summary>
    public bool Truncated { get; }

    /// <summary>
    /// Gets a reproducible cascade explanation.
    /// </summary>
    public string Explanation { get; }
}