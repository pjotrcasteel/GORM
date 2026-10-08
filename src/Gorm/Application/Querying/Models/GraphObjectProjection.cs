namespace Gorm.Application.Querying.Models;

/// <summary>
/// Represents graph object projection.
/// </summary>
public sealed class GraphObjectProjection : GraphQueryProjection
{
    /// <summary>
    /// Gets the collection of member assignment bindings.
    /// </summary>
    public IList<GraphProjectionBinding> Bindings { get; } = [];
}