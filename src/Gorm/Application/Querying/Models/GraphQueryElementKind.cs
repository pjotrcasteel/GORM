namespace Gorm.Application.Querying.Models;

/// <summary>
/// Defines graph query element kind values.
/// </summary>
public enum GraphQueryElementKind
{
    /// <summary>
    /// The element is a graph node.
    /// </summary>
    Node = 0,
    /// <summary>
    /// The element is a graph edge.
    /// </summary>
    Edge = 1
}