namespace Gorm.Application.Intelligence.Algorithms.Connectivity;

/// <summary>
/// Defines connected-component semantics.
/// </summary>
public enum GraphComponentKind
{
    /// <summary>
    /// Treats every edge as bidirectional.
    /// </summary>
    Weak = 0,

    /// <summary>
    /// Requires every node in a component to be reachable from every other node.
    /// </summary>
    Strong = 1
}