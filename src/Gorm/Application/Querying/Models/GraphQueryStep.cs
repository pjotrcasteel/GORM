namespace Gorm.Application.Querying.Models;

/// <summary>
/// Represents graph query step.
/// </summary>
public abstract class GraphQueryStep
{
    /// <summary>
    /// Gets the CLR type of the input element entering this step.
    /// </summary>
    public required Type InputElementType { get; init; }
    /// <summary>
    /// Gets the kind (node or edge) of the input element.
    /// </summary>
    public required GraphQueryElementKind InputElementKind { get; init; }
    /// <summary>
    /// Gets the CLR type of the output element leaving this step.
    /// </summary>
    public required Type OutputElementType { get; init; }
    /// <summary>
    /// Gets the kind (node or edge) of the output element.
    /// </summary>
    public required GraphQueryElementKind OutputElementKind { get; init; }
}