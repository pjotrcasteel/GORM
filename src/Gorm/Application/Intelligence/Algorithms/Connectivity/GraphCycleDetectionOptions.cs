using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Connectivity;

/// <summary>
/// Configures bounded directed-cycle detection.
/// </summary>
public sealed class GraphCycleDetectionOptions
{
    /// <summary>
    /// Gets or sets the maximum number of cycles returned before the search stops.
    /// </summary>
    public int MaximumCycles { get; set; } = 1_000;

    /// <summary>
    /// Gets or sets the maximum number of nodes in a detected simple cycle.
    /// </summary>
    public int MaximumDepth { get; set; } = 100;

    /// <summary>
    /// Gets or sets an optional edge predicate used to constrain cycle detection.
    /// </summary>
    public Func<Edge, bool>? EdgePredicate { get; set; }
}