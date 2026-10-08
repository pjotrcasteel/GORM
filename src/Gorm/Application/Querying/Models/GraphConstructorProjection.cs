using System.Reflection;

namespace Gorm.Application.Querying.Models;

/// <summary>
/// Represents graph constructor projection.
/// </summary>
public sealed class GraphConstructorProjection : GraphQueryProjection
{
    /// <summary>
    /// Gets the constructor used to create each result instance.
    /// </summary>
    public required ConstructorInfo Constructor { get; init; }
    /// <summary>
    /// Gets the ordered list of constructor parameter bindings.
    /// </summary>
    public IList<GraphConstructorProjectionParameter> Parameters { get; } = [];
}