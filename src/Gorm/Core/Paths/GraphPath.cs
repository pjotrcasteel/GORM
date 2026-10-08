using Gorm.Core.Primitives;

namespace Gorm.Core.Paths;

/// <summary>
/// Represents graph path.
/// </summary>
public static class GraphPath
{
    /// <summary>
    /// Executes from.
    /// </summary>
    /// <typeparam name="TRoot">The type of t root.</typeparam>
    /// <returns>The value.</returns>
    public static GraphPath<TRoot, TRoot> From<TRoot>()
        where TRoot : Node => new([], distinctResults: false);
}