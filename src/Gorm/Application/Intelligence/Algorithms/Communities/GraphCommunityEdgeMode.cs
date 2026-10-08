namespace Gorm.Application.Intelligence.Algorithms.Communities;

/// <summary>
/// Defines how projected edges contribute to community detection.
/// </summary>
public enum GraphCommunityEdgeMode
{
    /// <summary>
    /// Treats every selected edge as a bidirectional weighted relationship.
    /// </summary>
    Undirected = 0,

    /// <summary>
    /// Preserves the projected source-to-destination direction.
    /// </summary>
    Directed = 1
}