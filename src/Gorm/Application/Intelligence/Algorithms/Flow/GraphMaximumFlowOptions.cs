using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Flow;

/// <summary>
/// Configures directed maximum-flow/minimum-cut analysis.
/// </summary>
public sealed class GraphMaximumFlowOptions
{
    /// <summary>
    /// Gets or sets an optional non-negative directed edge-capacity selector. Every selected edge has capacity 1 when omitted.
    /// </summary>
    public Func<Edge, double>? CapacitySelector { get; set; }

    /// <summary>
    /// Gets or sets an optional edge availability predicate.
    /// </summary>
    public Func<Edge, bool>? EdgePredicate { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of augmenting paths.
    /// </summary>
    public int MaximumAugmentations { get; set; } = 1_000_000;

    /// <summary>
    /// Gets or sets the finite positive residual-capacity tolerance.
    /// </summary>
    public double Epsilon { get; set; } = 1e-9;
}