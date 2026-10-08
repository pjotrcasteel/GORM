using Gorm.Core.Metadata;

namespace Gorm.Core.Configuration.Interfaces;

/// <summary>
/// Defines i node type builder.
/// </summary>
internal interface INodeTypeBuilder
{
    /// <summary>
    /// Builds the result.
    /// </summary>
    /// <returns>The value.</returns>
    internal NodeTypeMapping BuildMapping();
}