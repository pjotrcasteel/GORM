using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Communities;

/// <summary>
/// Represents a selected edge crossing the final community partition.
/// </summary>
public sealed class GraphCommunityBoundaryEdge
{
    /// <summary>
    /// Gets the projected edge.
    /// </summary>
    public required Edge Edge { get; init; }

    /// <summary>
    /// Gets the source community identifier.
    /// </summary>
    public required int FromCommunityId { get; init; }

    /// <summary>
    /// Gets the destination community identifier.
    /// </summary>
    public required int ToCommunityId { get; init; }

    /// <summary>
    /// Gets the configured edge weight.
    /// </summary>
    public required double Weight { get; init; }

    /// <summary>
    /// Gets an explainable structural-surprise score in the range zero through one.
    /// </summary>
    public required double AnomalyScore { get; init; }

    /// <summary>
    /// Gets a value indicating whether the score meets the configured anomaly threshold.
    /// </summary>
    public required bool IsAnomalous { get; init; }

    /// <summary>
    /// Gets a human-readable explanation of the anomaly score.
    /// </summary>
    public required string Explanation { get; init; }
}