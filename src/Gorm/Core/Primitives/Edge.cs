namespace Gorm.Core.Primitives;

/// <summary>
/// Represents edge.
/// </summary>
public abstract class Edge
{
    /// <summary>
    /// Gets or sets the unique identifier of the edge.
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// Gets or sets the source node identifier.
    /// </summary>
    public Guid FromId { get; set; }
    /// <summary>
    /// Gets or sets the destination node identifier.
    /// </summary>
    public Guid ToId { get; set; }
}