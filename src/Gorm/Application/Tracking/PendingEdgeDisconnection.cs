using Gorm.Core.Primitives;

namespace Gorm.Application.Tracking;

/// <summary>
/// Represents pending edge disconnection.
/// </summary>
public sealed class PendingEdgeDisconnection
{
    /// <summary>
    /// Gets or sets the edge type.
    /// </summary>
    public required Type EdgeType { get; init; }
    /// <summary>
    /// Gets or sets the from node.
    /// </summary>
    public required Node FromNode { get; init; }
    /// <summary>
    /// Gets or sets the to node.
    /// </summary>
    public required Node ToNode { get; init; }
}