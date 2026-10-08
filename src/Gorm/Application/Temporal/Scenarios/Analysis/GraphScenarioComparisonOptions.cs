using Gorm.Application.Intelligence.Algorithms.Communities;
using Gorm.Application.Intelligence.Algorithms.Planning;

namespace Gorm.Application.Temporal.Scenarios.Analysis;

/// <summary>
/// Controls built-in and domain-specific scenario Intelligence comparison.
/// </summary>
public sealed class GraphScenarioComparisonOptions
{
    /// <summary>
    /// Gets or sets whether Critical Path Method metrics are calculated.
    /// </summary>
    public bool IncludeCriticalPath { get; init; }

    /// <summary>
    /// Gets or sets Critical Path Method options.
    /// </summary>
    public GraphCriticalPathOptions? CriticalPathOptions { get; init; }

    /// <summary>
    /// Gets or sets whether deterministic Leiden community metrics are calculated.
    /// </summary>
    public bool IncludeCommunities { get; init; }

    /// <summary>
    /// Gets or sets deterministic Leiden community options.
    /// </summary>
    public GraphCommunityDetectionOptions? CommunityOptions { get; init; }

    /// <summary>
    /// Gets or sets an optional node from which outgoing impact reach is measured.
    /// </summary>
    public Guid? ImpactSourceNodeId { get; init; }

    /// <summary>
    /// Gets or sets the maximum number of nodes explored by each impact reach traversal.
    /// </summary>
    public int MaximumImpactNodes { get; init; } = 1_000_000;

    /// <summary>
    /// Gets or sets additional pure projection metrics.
    /// </summary>
    public IReadOnlyList<GraphScenarioMetricDefinition> AdditionalMetrics { get; init; } = [];
}