using Gorm.Core.Primitives;

namespace Gorm.Demo.Domain.Edges;

/// <summary>
/// Represents characteristic specification map edge.
/// </summary>
public sealed class CharacteristicSpecificationMapEdge : Edge
{
    /// <summary>
    /// Gets or sets the payload.
    /// </summary>
    public string Payload { get; set; } = "{}";
}