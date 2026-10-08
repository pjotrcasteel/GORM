using Gorm.Core.Metadata;
using Gorm.Core.Models;
using Gorm.Core.Primitives;

namespace Gorm.Core.Configuration;

/// <summary>
/// Represents graph model builder.
/// </summary>
public sealed partial class GraphModelBuilder
{
    private readonly Dictionary<Type, NodeTypeMapping> _nodes = [];
    private readonly Dictionary<Type, EdgeTypeMapping> _edges = [];

    public void Node<TNode>() where TNode : Node =>
        Node<TNode>(static _ => { });

    /// <summary>
    /// Executes node.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <param name="configure">The include configuration.</param>
    public void Node<TNode>(Action<NodeTypeBuilder<TNode>> configure)
        where TNode : Node
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new NodeTypeBuilder<TNode>();
        configure(builder);
        _nodes[typeof(TNode)] = builder.BuildMapping();
    }

    public void Edge<TEdge, TFrom, TTo>() where TEdge : Edge where TFrom : Node where TTo : Node =>
        Edge<TEdge>(static edge => edge.From<TFrom>().To<TTo>());

    /// <summary>
    /// Executes edge.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <param name="configure">The include configuration.</param>
    public void Edge<TEdge>(Action<EdgeTypeBuilder<TEdge>> configure)
        where TEdge : Edge
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new EdgeTypeBuilder<TEdge>();
        configure(builder);
        _edges[typeof(TEdge)] = builder.BuildMapping();
    }

    /// <summary>
    /// Builds the result.
    /// </summary>
    /// <returns>The value.</returns>
    public GraphModel Build() => new([.. _nodes.Values], [.. _edges.Values]);
}