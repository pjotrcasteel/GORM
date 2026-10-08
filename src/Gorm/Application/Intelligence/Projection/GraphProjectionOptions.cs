namespace Gorm.Application.Intelligence.Projection;

/// <summary>
/// Configures the construction of a graph projection.
/// </summary>
public sealed class GraphProjectionOptions
{
    /// <summary>
    /// Gets or sets how edges with endpoints outside the projection are handled.
    /// </summary>
    public GraphOrphanedEdgeBehavior OrphanedEdgeBehavior { get; set; } = GraphOrphanedEdgeBehavior.Throw;
}