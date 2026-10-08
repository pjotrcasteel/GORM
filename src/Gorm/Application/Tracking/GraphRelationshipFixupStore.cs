using Gorm.Application.Context;
using Gorm.Core.Primitives;

namespace Gorm.Application.Tracking;

/// <summary>
/// Represents graph relationship fixup store.
/// </summary>
internal sealed class GraphRelationshipFixupStore
{
    private readonly Dictionary<GraphRelationshipFixupKey, RelationshipFixupBucket> _relationships = [];

    /// <summary>
    /// Executes clear.
    /// </summary>
    public void Clear() => _relationships.Clear();

    /// <summary>
    /// Sets the value.
    /// </summary>
    /// <param name="owner">The owner node.</param>
    /// <param name="relationshipName">The relationship name.</param>
    /// <param name="includeEdge">The include edge.</param>
    /// <param name="items">The items.</param>
    public void Set(Node owner, string relationshipName, bool includeEdge, IEnumerable<object> items)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(items);

        var key = CreateKey(owner, relationshipName, includeEdge);
        var bucket = new RelationshipFixupBucket();

        foreach (var item in items.Where(i => i is not null))
        {
            bucket.Add(item);
        }

        _relationships[key] = bucket;
    }

    /// <summary>
    /// Adds the item.
    /// </summary>
    /// <param name="owner">The owner node.</param>
    /// <param name="relationshipName">The relationship name.</param>
    /// <param name="includeEdge">The include edge.</param>
    /// <param name="item">The item.</param>
    public void Add(Node owner, string relationshipName, bool includeEdge, object item)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(item);

        var key = CreateKey(owner, relationshipName, includeEdge);

        if (!_relationships.TryGetValue(key, out var bucket))
        {
            bucket = new RelationshipFixupBucket();
            _relationships[key] = bucket;
        }

        bucket.Add(item);
    }

    /// <summary>
    /// Removes the item.
    /// </summary>
    /// <param name="owner">The owner node.</param>
    /// <param name="relationshipName">The relationship name.</param>
    /// <param name="includeEdge">The include edge.</param>
    /// <param name="item">The item.</param>
    public void Remove(Node owner, string relationshipName, bool includeEdge, object item)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(item);

        var key = CreateKey(owner, relationshipName, includeEdge);

        if (_relationships.TryGetValue(key, out var bucket))
        {
            bucket.Remove(item);
        }
    }

    /// <summary>
    /// Executes try get related.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <param name="owner">The owner node.</param>
    /// <param name="relationshipName">The relationship name.</param>
    /// <param name="related">The related node.</param>
    /// <returns>True when successful; otherwise, false.</returns>
    public bool TryGetRelated<TNode>(Node owner, string relationshipName, out IReadOnlyList<TNode>? related)
        where TNode : Node
    {
        ArgumentNullException.ThrowIfNull(owner);

        var key = CreateKey(owner, relationshipName, includeEdge: false);

        if (_relationships.TryGetValue(key, out var bucket))
        {
            related = bucket.OfType<TNode>();
            return true;
        }

        related = null;
        return false;
    }

    /// <summary>
    /// Executes try get related with edges.
    /// </summary>
    /// <param name="owner">The owner node.</param>
    /// <param name="relationshipName">The relationship name.</param>
    /// <param name="related">The related node.</param>
    /// <returns>True when successful; otherwise, false.</returns>
    public bool TryGetRelatedWithEdges(Node owner, string relationshipName, out IReadOnlyList<object>? related)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var key = CreateKey(owner, relationshipName, includeEdge: true);

        if (_relationships.TryGetValue(key, out var bucket))
        {
            related = bucket.ToArray();
            return true;
        }

        related = null;
        return false;
    }

    private static GraphRelationshipFixupKey CreateKey(Node owner, string relationshipName, bool includeEdge)
    {
        if (string.IsNullOrWhiteSpace(relationshipName))
        {
            throw new ArgumentException("Relationship name cannot be null or whitespace.", nameof(relationshipName));
        }

        return new GraphRelationshipFixupKey(owner.GetType(), owner.Id, relationshipName, includeEdge);
    }

    private sealed class RelationshipFixupBucket
    {
        private readonly List<object> _items = [];
        private readonly HashSet<Guid> _entityIds = [];

        public void Add(object item)
        {
            var itemId = GraphRelationshipRuntimeCache.TryGetEntityId(item);

            if (itemId is Guid id)
            {
                if (_entityIds.Add(id))
                {
                    _items.Add(item);
                }

                return;
            }

            if (!_items.Contains(item))
            {
                _items.Add(item);
            }
        }

        public void Remove(object item)
        {
            var itemId = GraphRelationshipRuntimeCache.TryGetEntityId(item);

            if (itemId is null)
            {
                _items.RemoveAll(existing => ReferenceEquals(existing, item));
                return;
            }

            var id = itemId.Value;

            for (var i = _items.Count - 1; i >= 0; i--)
            {
                if (GraphRelationshipRuntimeCache.TryGetEntityId(_items[i]) == id)
                {
                    _items.RemoveAt(i);
                }
            }

            _entityIds.Remove(id);
        }

        public List<TNode> OfType<TNode>()
            where TNode : Node
        {
            var result = new List<TNode>(_items.Count);

            for (var i = 0; i < _items.Count; i++)
            {
                if (_items[i] is TNode node)
                {
                    result.Add(node);
                }
            }

            return result;
        }

        public IReadOnlyList<object> ToArray() => [.. _items];
    }
}