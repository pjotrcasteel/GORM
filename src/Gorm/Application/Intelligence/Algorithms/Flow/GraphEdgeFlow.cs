using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Flow;

/// <summary>
/// Represents calculated flow and utilization for one selected projected edge.
/// </summary>
public sealed class GraphEdgeFlow
{
    /// <summary>
    /// Gets the projected edge.
    /// </summary>
    public required Edge Edge { get; init; }

    /// <summary>
    /// Gets configured directed capacity.
    /// </summary>
    public required double Capacity { get; init; }

    /// <summary>
    /// Gets calculated directed flow.
    /// </summary>
    public required double Flow { get; init; }

    /// <summary>
    /// Gets flow divided by capacity, or zero for a zero-capacity edge.
    /// </summary>
    public required double Utilization { get; init; }

    /// <summary>
    /// Gets a value indicating whether residual capacity is within the configured tolerance.
    /// </summary>
    public required bool IsSaturated { get; init; }
}