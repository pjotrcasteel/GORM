namespace Gorm.Application.Intelligence.Projection;

/// <summary>
/// Defines how a graph projection handles an edge whose endpoint is not part of the projection.
/// </summary>
public enum GraphOrphanedEdgeBehavior
{
    /// <summary>
    /// Throws an exception when an edge endpoint is missing.
    /// </summary>
    Throw = 0,

    /// <summary>
    /// Ignores edges with one or more missing endpoints.
    /// </summary>
    Ignore = 1
}