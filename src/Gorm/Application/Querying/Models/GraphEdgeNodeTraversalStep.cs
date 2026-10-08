namespace Gorm.Application.Querying.Models;

/// <summary>
/// Represents graph edge node traversal step.
/// </summary>
public sealed class GraphEdgeNodeTraversalStep : GraphQueryStep
{
    /// <summary>
    /// Gets which endpoint (<see cref="GraphEdgeEndpoint.From"/> or <see cref="GraphEdgeEndpoint.To"/>) to resolve.
    /// </summary>
    public required GraphEdgeEndpoint Endpoint { get; init; }
}