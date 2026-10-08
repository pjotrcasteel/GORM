namespace Gorm.Core.Primitives;

/// <summary>
/// Represents node.
/// </summary>
public abstract class Node
{
    /// <summary>
    /// Gets or sets the unique identifier of the node.
    /// </summary>
    public Guid Id { get; set; }
}