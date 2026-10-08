using Gorm.Application.Temporal.Diff;

namespace Gorm.Application.Temporal.Scenarios;

/// <summary>
/// Contains the next immutable scenario revision and its exact world difference.
/// </summary>
public sealed class GraphScenarioApplyResult
{
    /// <summary>
    /// Gets the next immutable scenario revision.
    /// </summary>
    public required GraphScenario Scenario { get; init; }

    /// <summary>
    /// Gets the world difference produced by the mutation.
    /// </summary>
    public required GraphWorldSnapshotDiff Difference { get; init; }

    /// <summary>
    /// Gets the number of incident edges removed automatically with a node.
    /// </summary>
    public required int CascadedEdgeRemovals { get; init; }
}