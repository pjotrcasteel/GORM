namespace Gorm.Application.Querying.Models;

/// <summary>
/// Represents graph constructor projection parameter.
/// </summary>
public sealed class GraphConstructorProjectionParameter
{
    /// <summary>
    /// Gets the constructor parameter name.
    /// </summary>
    public required string ParameterName { get; init; }
    /// <summary>
    /// Gets the constructor parameter CLR type.
    /// </summary>
    public required Type ParameterType { get; init; }
    /// <summary>
    /// Gets the source property name, or <see langword="null"/> when projecting a whole entity.
    /// </summary>
    public string? SourcePropertyName { get; init; }
    /// <summary>
    /// Gets whether the source is a node or an edge.
    /// </summary>
    public required GraphProjectionSourceKind SourceKind { get; init; }
    /// <summary>
    /// Gets whether the entire source entity is passed rather than a single property.
    /// </summary>
    public bool IsWholeEntity { get; init; }
}