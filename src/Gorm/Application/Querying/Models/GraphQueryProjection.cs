namespace Gorm.Application.Querying.Models;

/// <summary>
/// Represents graph query projection.
/// </summary>
public abstract class GraphQueryProjection
{
    /// <summary>
    /// Gets the CLR type of the projected result.
    /// </summary>
    public required Type ResultType { get; init; }
}