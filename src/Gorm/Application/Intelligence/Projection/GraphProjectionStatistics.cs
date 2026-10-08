namespace Gorm.Application.Intelligence.Projection;

/// <summary>
/// Describes the shape of a graph projection.
/// </summary>
public sealed class GraphProjectionStatistics
{
    /// <summary>
    /// Gets the number of projected nodes.
    /// </summary>
    public required int NodeCount { get; init; }

    /// <summary>
    /// Gets the number of projected edges.
    /// </summary>
    public required int EdgeCount { get; init; }

    /// <summary>
    /// Gets the number of ignored edges whose endpoints were not projected.
    /// </summary>
    public required int IgnoredEdgeCount { get; init; }

    /// <summary>
    /// Gets the number of nodes without incoming or outgoing edges.
    /// </summary>
    public required int IsolatedNodeCount { get; init; }

    /// <summary>
    /// Gets the directed graph density. Parallel edges can produce a value greater than 1.
    /// </summary>
    public required double Density { get; init; }
}