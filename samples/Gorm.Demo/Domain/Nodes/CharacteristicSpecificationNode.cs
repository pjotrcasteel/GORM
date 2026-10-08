using Gorm.Core.Primitives;

namespace Gorm.Demo.Domain.Nodes;

/// <summary>
/// Represents characteristic specification node.
/// </summary>
public sealed class CharacteristicSpecificationNode : Node
{
    /// <summary>
    /// Gets empty.
    /// </summary>
    /// <returns>The value.</returns>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the payload.
    /// </summary>
    public string Payload { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the maps into.
    /// </summary>
    public ICollection<CharacteristicSpecificationNode> MapsInto { get; } = [];

    /// <summary>
    /// Gets or sets the mapped from.
    /// </summary>
    public ICollection<CharacteristicSpecificationNode> MappedFrom { get; } = [];
}