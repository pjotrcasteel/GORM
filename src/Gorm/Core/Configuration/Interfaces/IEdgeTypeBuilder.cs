using Gorm.Core.Metadata;

namespace Gorm.Core.Configuration.Interfaces;

/// <summary>
/// Defines i edge type builder.
/// </summary>
internal interface IEdgeTypeBuilder
{
    /// <summary>
    /// Builds the result.
    /// </summary>
    /// <returns>The value.</returns>
    internal EdgeTypeMapping BuildMapping();
}