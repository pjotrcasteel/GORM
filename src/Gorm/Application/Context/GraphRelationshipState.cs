using Gorm.Application.Querying.Models;
using Gorm.Application.Tracking;
using Gorm.Core.Loading;

namespace Gorm.Application.Context;

internal sealed class GraphRelationshipState
{
    private readonly Dictionary<GraphLoadedRelationshipKey, object> _loadedRelationships = [];

    public GraphRelationshipFixupStore RelationshipFixupStore { get; } = new();

    public bool Contains(object owner, GraphTraversalDirection direction, Type edgeType, Type nodeType, bool includeEdge)
    {
        var key = CreateRelationshipKey(owner, direction, edgeType, nodeType, includeEdge);
        return _loadedRelationships.ContainsKey(key);
    }

    public bool TryGet<T>(object owner, GraphTraversalDirection direction, Type edgeType, Type nodeType, bool includeEdge, out T? value)
        where T : class
    {
        var key = CreateRelationshipKey(owner, direction, edgeType, nodeType, includeEdge);
        return TryGet(key, out value);
    }

    public bool TryGet<T>(GraphLoadedRelationshipKey key, out T? value)
        where T : class
    {
        if (_loadedRelationships.TryGetValue(key, out var existing) &&
            existing is T typed)
        {
            value = typed;
            return true;
        }

        value = null;
        return false;
    }

    public static GraphLoadedRelationshipKey CreateKey(object owner, GraphTraversalDirection direction, Type edgeType, Type nodeType, bool includeEdge)
    {
        return CreateRelationshipKey(owner, direction, edgeType, nodeType, includeEdge);
    }

    public void Set(GraphLoadedRelationshipKey key, object value)
    {
        _loadedRelationships[key] = value;
    }

    public void Clear()
    {
        _loadedRelationships.Clear();
        RelationshipFixupStore.Clear();
    }

    private static GraphLoadedRelationshipKey CreateRelationshipKey(object owner, GraphTraversalDirection direction, Type edgeType, Type nodeType, bool includeEdge)
    {
        return new GraphLoadedRelationshipKey(owner, direction, edgeType, nodeType, includeEdge);
    }
}