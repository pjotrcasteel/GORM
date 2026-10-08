using Gorm.Core.Metadata;

namespace Gorm.Core.Models;

/// <summary>
/// Represents graph model navigation extensions.
/// </summary>
public static class GraphModelNavigationExtensions
{
    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="ownerNodeType">The owner node type.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The value.</returns>
    public static GraphNavigationMapping GetNavigation(this GraphModel model, Type ownerNodeType, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(model);
        return model.GetNavigationCore(ownerNodeType, propertyName);
    }
}