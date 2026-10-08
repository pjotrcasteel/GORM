using System.Reflection;
using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Projection;

/// <summary>
/// Builds detached graph projections from materialized graph entities.
/// </summary>
public sealed class GraphProjectionBuilder
{
    private static readonly MethodInfo MemberwiseCloneMethod = typeof(object).GetMethod(
        "MemberwiseClone",
        BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException(
        "The runtime did not expose Object.MemberwiseClone.");

    private readonly List<Node> _nodes = [];
    private readonly List<Edge> _edges = [];
    private readonly GraphProjectionOptions _options;

    /// <summary>
    /// Initializes a new graph projection builder.
    /// </summary>
    public GraphProjectionBuilder(GraphProjectionOptions? options = null)
    {
        _options = options ?? new GraphProjectionOptions();
    }

    /// <summary>
    /// Adds nodes to the projection.
    /// </summary>
    public GraphProjectionBuilder AddNodes<TNode>(IEnumerable<TNode> nodes)
        where TNode : Node
    {
        ArgumentNullException.ThrowIfNull(nodes);
        _nodes.AddRange(nodes);
        return this;
    }

    /// <summary>
    /// Adds edges to the projection.
    /// </summary>
    public GraphProjectionBuilder AddEdges<TEdge>(IEnumerable<TEdge> edges)
        where TEdge : Edge
    {
        ArgumentNullException.ThrowIfNull(edges);
        _edges.AddRange(edges);
        return this;
    }

    /// <summary>
    /// Builds a detached projection and its incoming and outgoing adjacency indexes.
    /// </summary>
    public GraphProjection Build()
    {
        var nodes = CloneEntities(_nodes);
        var nodeIndices = IndexNodes(nodes);
        var (edges, ignoredEdgeCount) = SelectEdges(CloneEntities(_edges), nodeIndices, _options.OrphanedEdgeBehavior);
        var outgoingCounts = new int[nodes.Length];
        var incomingCounts = new int[nodes.Length];

        foreach (var edge in edges)
        {
            outgoingCounts[nodeIndices[edge.FromId]]++;
            incomingCounts[nodeIndices[edge.ToId]]++;
        }

        var outgoingOffsets = CreateOffsets(outgoingCounts);
        var incomingOffsets = CreateOffsets(incomingCounts);
        var outgoingArcs = new GraphProjectionArc[edges.Length];
        var incomingArcs = new GraphProjectionArc[edges.Length];
        var outgoingPositions = outgoingOffsets[..^1];
        var incomingPositions = incomingOffsets[..^1];

        for (var edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
        {
            var edge = edges[edgeIndex];
            var fromIndex = nodeIndices[edge.FromId];
            var toIndex = nodeIndices[edge.ToId];

            outgoingArcs[outgoingPositions[fromIndex]++] = new GraphProjectionArc(toIndex, edgeIndex);
            incomingArcs[incomingPositions[toIndex]++] = new GraphProjectionArc(fromIndex, edgeIndex);
        }

        return new GraphProjection(
            new GraphProjection.GraphProjectionParameters
            {
                Nodes = nodes,
                Edges = edges,
                NodeIndices = nodeIndices,
                OutgoingOffsets = outgoingOffsets,
                OutgoingArcs = outgoingArcs,
                IncomingOffsets = incomingOffsets,
                IncomingArcs = incomingArcs,
                IgnoredEdgeCount = ignoredEdgeCount
            });
    }

    private static TEntity[] CloneEntities<TEntity>(IEnumerable<TEntity> entities)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entities);
        return [.. entities.Select(CloneEntity)];
    }

    private static TEntity CloneEntity<TEntity>(TEntity entity)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);

        var runtimeType = entity.GetType();
        var clone = (TEntity)(MemberwiseCloneMethod.Invoke(entity, null) ?? throw new InvalidOperationException(
            $"Failed to clone projection entity type '{runtimeType.FullName}'."));
        var properties = runtimeType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        for (var index = 0; index < properties.Length; index++)
        {
            var property = properties[index];
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length != 0 || property.PropertyType != typeof(byte[]))
            {
                continue;
            }

            if (property.GetValue(clone) is byte[] bytes)
            {
                property.SetValue(clone, bytes.ToArray());
            }
        }

        return clone;
    }

    private static Dictionary<Guid, int> IndexNodes(Node[] nodes)
    {
        var result = new Dictionary<Guid, int>(nodes.Length);

        for (var index = 0; index < nodes.Length; index++)
        {
            var node = nodes[index] ?? throw new ArgumentException("A graph projection cannot contain a null node.", nameof(nodes));

            if (node.Id == Guid.Empty)
            {
                throw new ArgumentException("Every projected node must have a non-empty identifier.", nameof(nodes));
            }

            if (!result.TryAdd(node.Id, index))
            {
                throw new ArgumentException($"The projection contains duplicate node identifier '{node.Id}'.", nameof(nodes));
            }
        }

        return result;
    }

    private static (Edge[] Edges, int IgnoredEdgeCount) SelectEdges(
        IEnumerable<Edge> source,
        Dictionary<Guid, int> nodeIndices,
        GraphOrphanedEdgeBehavior orphanedEdgeBehavior)
    {
        var edges = new List<Edge>();
        var edgeIdentifiers = new HashSet<Guid>();
        var ignoredEdgeCount = 0;

        foreach (var edge in source)
        {
            ArgumentNullException.ThrowIfNull(edge);

            if (edge.Id == Guid.Empty)
            {
                throw new ArgumentException("Every projected edge must have a non-empty identifier.", nameof(source));
            }

            if (!edgeIdentifiers.Add(edge.Id))
            {
                throw new ArgumentException($"The projection contains duplicate edge identifier '{edge.Id}'.", nameof(source));
            }

            var containsFrom = nodeIndices.ContainsKey(edge.FromId);
            var containsTo = nodeIndices.ContainsKey(edge.ToId);

            if (containsFrom && containsTo)
            {
                edges.Add(edge);
                continue;
            }

            if (orphanedEdgeBehavior == GraphOrphanedEdgeBehavior.Throw)
            {
                throw new ArgumentException(
                    $"Edge '{edge.Id}' refers to endpoint '{(containsFrom ? edge.ToId : edge.FromId)}' " +
                    "that is not part of the projection.",
                    nameof(source));
            }

            ignoredEdgeCount++;
        }

        return ([.. edges], ignoredEdgeCount);
    }

    private static int[] CreateOffsets(int[] counts)
    {
        var offsets = new int[counts.Length + 1];
        for (var index = 0; index < counts.Length; index++)
        {
            offsets[index + 1] = offsets[index] + counts[index];
        }

        return offsets;
    }
}