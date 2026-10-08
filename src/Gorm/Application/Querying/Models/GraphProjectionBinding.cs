namespace Gorm.Application.Querying.Models;

/// <summary>
/// Represents graph projection binding.
/// </summary>
public sealed class GraphProjectionBinding
{
    /// <summary>
    /// Gets the name of the target member to assign to.
    /// </summary>
    public required string TargetMemberName { get; init; }
    /// <summary>
    /// Gets the source property name, or <see langword="null"/> when projecting a whole entity.
    /// </summary>
    public string? SourcePropertyName { get; init; }
    /// <summary>
    /// Gets whether the source is a node or an edge.
    /// </summary>
    public required GraphProjectionSourceKind SourceKind { get; init; }
    /// <summary>
    /// Gets whether the entire source entity is assigned rather than a single property.
    /// </summary>
    public bool IsWholeEntity { get; init; }
}