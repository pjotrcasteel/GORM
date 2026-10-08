using Gorm.Core.Primitives;

namespace Gorm.Application.Tracking;

/// <summary>
/// Represents pending edge connection.
/// </summary>
public sealed class PendingEdgeConnection
{
    /// <summary>
    /// Gets or sets the edge.
    /// </summary>
    public required Edge Edge { get; init; }
    /// <summary>
    /// Gets or sets the from node.
    /// </summary>
    public required Node FromNode { get; init; }
    /// <summary>
    /// Gets or sets the to node.
    /// </summary>
    public required Node ToNode { get; init; }
}