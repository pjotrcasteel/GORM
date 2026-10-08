using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Connectivity;

/// <summary>
/// Represents a connected component.
/// </summary>
public sealed class GraphConnectedComponent
{
    /// <summary>
    /// Gets the zero-based component identifier.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// Gets the nodes in the component.
    /// </summary>
    public required IReadOnlyList<Node> Nodes { get; init; }
}