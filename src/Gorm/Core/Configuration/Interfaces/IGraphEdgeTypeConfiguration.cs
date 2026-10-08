using Gorm.Core.Primitives;

namespace Gorm.Core.Configuration.Interfaces;

/// <summary>
/// Configures a graph edge type.
/// </summary>
/// <typeparam name="TEdge">The edge type.</typeparam>
public interface IGraphEdgeTypeConfiguration<TEdge> where TEdge : Edge
{
    /// <summary>
    /// Configures the edge type.
    /// </summary>
    /// <param name="builder">The edge type builder.</param>
    public void Configure(EdgeTypeBuilder<TEdge> builder);
}