namespace Gorm.Application.Querying.Models;

/// <summary>
/// Defines graph projection source kind values.
/// </summary>
public enum GraphProjectionSourceKind
{
    /// <summary>
    /// The source is a node.
    /// </summary>
    Node = 0,
    /// <summary>
    /// The source is an edge.
    /// </summary>
    Edge = 1
}