namespace Gorm.Application.Querying.Models;

/// <summary>
/// Represents graph scalar projection.
/// </summary>
public sealed class GraphScalarProjection : GraphQueryProjection
{
    /// <summary>
    /// Gets or sets the source property name.
    /// </summary>
    public required string SourcePropertyName { get; init; }
    /// <summary>
    /// Gets or sets the source kind.
    /// </summary>
    public required GraphProjectionSourceKind SourceKind { get; init; }
}