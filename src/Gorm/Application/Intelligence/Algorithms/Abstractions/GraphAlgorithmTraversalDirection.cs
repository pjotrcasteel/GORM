namespace Gorm.Application.Intelligence.Algorithms.Abstractions;

/// <summary>
/// Defines edge directions available to in-memory graph algorithms.
/// </summary>
public enum GraphAlgorithmTraversalDirection
{
    /// <summary>
    /// Traverses edges from source to destination.
    /// </summary>
    Outgoing = 0,

    /// <summary>
    /// Traverses edges from destination to source.
    /// </summary>
    Incoming = 1,

    /// <summary>
    /// Treats edges as bidirectional.
    /// </summary>
    Both = 2
}