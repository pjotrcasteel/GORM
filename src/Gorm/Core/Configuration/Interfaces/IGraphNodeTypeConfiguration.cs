using Gorm.Core.Primitives;

namespace Gorm.Core.Configuration.Interfaces;

/// <summary>
/// Configures a graph node type.
/// </summary>
/// <typeparam name="TNode">The node type.</typeparam>
public interface IGraphNodeTypeConfiguration<TNode> where TNode : Node
{
    /// <summary>
    /// Configures the node type.
    /// </summary>
    /// <param name="builder">The node type builder.</param>
    public void Configure(NodeTypeBuilder<TNode> builder);
}