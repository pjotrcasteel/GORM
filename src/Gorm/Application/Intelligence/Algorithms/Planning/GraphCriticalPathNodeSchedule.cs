using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Planning;

/// <summary>
/// Represents the calculated Critical Path Method schedule of one node.
/// </summary>
public sealed class GraphCriticalPathNodeSchedule
{
    /// <summary>
    /// Gets the projected node.
    /// </summary>
    public required Node Node { get; init; }

    /// <summary>
    /// Gets the configured node duration.
    /// </summary>
    public required double Duration { get; init; }

    /// <summary>
    /// Gets the earliest time at which the node can start.
    /// </summary>
    public required double EarliestStart { get; init; }

    /// <summary>
    /// Gets the earliest time at which the node can finish.
    /// </summary>
    public required double EarliestFinish { get; init; }

    /// <summary>
    /// Gets the latest start time that does not delay the complete projection.
    /// </summary>
    public required double LatestStart { get; init; }

    /// <summary>
    /// Gets the latest finish time that does not delay the complete projection.
    /// </summary>
    public required double LatestFinish { get; init; }

    /// <summary>
    /// Gets the total scheduling slack.
    /// </summary>
    public required double Slack { get; init; }

    /// <summary>
    /// Gets a value indicating whether the node is critical within the configured tolerance.
    /// </summary>
    public required bool IsCritical { get; init; }
}