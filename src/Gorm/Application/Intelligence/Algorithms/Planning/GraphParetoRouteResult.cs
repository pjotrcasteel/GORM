using Gorm.Application.Intelligence.Algorithms.Pathfinding;

namespace Gorm.Application.Intelligence.Algorithms.Planning;

/// <summary>
/// Represents bounded multi-criteria route decisioning.
/// </summary>
public sealed class GraphParetoRouteResult
{
    /// <summary>
    /// Gets ranked non-dominated routes.
    /// </summary>
    public required IReadOnlyList<GraphParetoRoute> Routes { get; init; }

    /// <summary>
    /// Gets bounded explanations for complete rejected alternatives.
    /// </summary>
    public required IReadOnlyList<GraphRejectedRoute> RejectedAlternatives { get; init; }

    /// <summary>
    /// Gets the number of expanded search labels.
    /// </summary>
    public required int ExpandedLabels { get; init; }

    /// <summary>
    /// Gets a value indicating whether a configured search/retention limit may have hidden frontier routes.
    /// </summary>
    public required bool Truncated { get; init; }

    /// <summary>
    /// Gets an overall decision explanation.
    /// </summary>
    public required string Explanation { get; init; }
}