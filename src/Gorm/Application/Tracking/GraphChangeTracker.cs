using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Gorm.Core.Metadata;
using Gorm.Core.Models;
using Gorm.Core.Primitives;

namespace Gorm.Application.Tracking;

/// <summary>
/// Represents graph change tracker.
/// </summary>
public sealed class GraphChangeTracker
{
    private readonly Dictionary<object, GraphEntityEntry> _entriesByReference =
        new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<Type, List<GraphEntityEntry>> _entriesByType = [];
    private readonly Dictionary<TrackedEntityKey, GraphEntityEntry> _entriesByKey = [];
    private readonly Dictionary<Type, PropertyInfo> _keyPropertiesByType = [];
    private readonly Dictionary<Type, GraphSnapshotPropertyPlan[]> _snapshotPropertiesByType = [];
    private readonly HashSet<Type> _keyIndexInitializedTypes = [];
    private readonly HashSet<Type> _keyIndexDirtyTypes = [];

    private readonly List<PendingEdgeConnection> _pendingEdgeConnections = [];
    private readonly List<PendingEdgeDisconnection> _pendingEdgeDisconnections = [];
    private readonly HashSet<object> _pendingEdgeConnectionsByEdge = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<PendingEdgeKey, List<PendingEdgeConnection>> _pendingEdgeConnectionsByKey = [];
    private readonly Dictionary<PendingEdgeKey, PendingEdgeDisconnection> _pendingEdgeDisconnectionsByKey = [];

    /// <summary>
    /// Gets values.
    /// </summary>
    /// <returns>The entry.</returns>
    public IReadOnlyCollection<GraphEntityEntry> Entries => _entriesByReference.Values;

    /// <summary>
    /// Gets pending edge connections.
    /// </summary>
    /// <returns>The items.</returns>
    public IReadOnlyList<PendingEdgeConnection> PendingEdgeConnections => _pendingEdgeConnections;

    /// <summary>
    /// Gets pending edge disconnections.
    /// </summary>
    /// <returns>The items.</returns>
    public IReadOnlyList<PendingEdgeDisconnection> PendingEdgeDisconnections => _pendingEdgeDisconnections;

    /// <summary>
    /// Executes has changes.
    /// </summary>
    /// <returns>True when successful; otherwise, false.</returns>
    public bool HasChanges() => _entriesByReference.Values.Any(x => x.State != EntityState.Unchanged && x.State != EntityState.Detached) ||
        _pendingEdgeConnections.Count > 0 ||
        _pendingEdgeDisconnections.Count > 0;

    /// <summary>
    /// Executes entries for.
    /// </summary>
    /// <param name="clrType">The clr type.</param>
    /// <returns>The items.</returns>
    public IEnumerable<GraphEntityEntry> EntriesFor(Type clrType)
    {
        ArgumentNullException.ThrowIfNull(clrType);
        return _entriesByType.TryGetValue(clrType, out var entries) ? entries : Array.Empty<GraphEntityEntry>();
    }

    /// <summary>
    /// Executes entries for.
    /// </summary>
    /// <typeparam name="TEntity">The type of t entity.</typeparam>
    /// <returns>The items.</returns>
    public IEnumerable<GraphEntityEntry> EntriesFor<TEntity>() => EntriesFor(typeof(TEntity));

    /// <summary>
    /// Adds the item.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <returns>The entry.</returns>
    public GraphEntityEntry Add(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var entry = GetOrCreateEntry(entity);
        entry.State = EntityState.Added;
        _keyIndexDirtyTypes.Add(entry.ClrType);

        return entry;
    }

    /// <summary>
    /// Attaches the entity.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="model">The model.</param>
    /// <returns>The entry.</returns>
    public GraphEntityEntry Attach(object entity, GraphModel model)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(model);

        var entry = GetOrCreateEntry(entity);

        if (entry.State == EntityState.Detached)
        {
            entry.State = EntityState.Unchanged;
        }

        CaptureSnapshot(entry, model);
        IndexEntry(model, entry);

        return entry;
    }

    /// <summary>
    /// Removes the item.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <returns>The entry.</returns>
    public GraphEntityEntry Remove(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var entry = GetOrCreateEntry(entity);

        if (entry.State == EntityState.Added)
        {
            RemoveEntry(entry);

            return new GraphEntityEntry
            {
                Entity = entity,
                ClrType = entity.GetType(),
                State = EntityState.Detached
            };
        }

        entry.State = EntityState.Deleted;
        RemoveKeyIndexes(entry);

        return entry;
    }

    /// <summary>
    /// Executes mark modified.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <returns>The entry.</returns>
    public GraphEntityEntry MarkModified(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var entry = GetOrCreateEntry(entity);

        if (entry.State == EntityState.Unchanged)
        {
            entry.State = EntityState.Modified;
        }

        return entry;
    }

    /// <summary>
    /// Executes entry.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <returns>The entry.</returns>
    public GraphEntityEntry Entry(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (_entriesByReference.TryGetValue(entity, out var entry))
        {
            return entry;
        }

        return new GraphEntityEntry
        {
            Entity = entity,
            ClrType = entity.GetType(),
            State = EntityState.Detached
        };
    }

    /// <summary>
    /// Executes try get tracked entity by key.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="clrType">The clr type.</param>
    /// <param name="keyValue">The key value.</param>
    /// <returns>The value.</returns>
    public object? TryGetTrackedEntityByKey(GraphModel model, Type clrType, object keyValue)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(clrType);
        ArgumentNullException.ThrowIfNull(keyValue);

        var lookupKey = new TrackedEntityKey(clrType, keyValue);

        if (TryGetIndexedEntity(lookupKey, out var indexedEntity))
        {
            return indexedEntity;
        }

        if (!_keyIndexInitializedTypes.Contains(clrType) || _keyIndexDirtyTypes.Contains(clrType))
        {
            RebuildKeyIndexForType(model, clrType);

            if (TryGetIndexedEntity(lookupKey, out indexedEntity))
            {
                return indexedEntity;
            }
        }

        return null;
    }

    /// <summary>
    /// Executes detect changes.
    /// </summary>
    /// <param name="model">The model.</param>
    public void DetectChanges(GraphModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        foreach (var entry in _entriesByReference.Values)
        {
            if (entry.State != EntityState.Unchanged)
            {
                continue;
            }

            var originalState = entry.State;
            var properties = GetSnapshotProperties(entry.ClrType, model);

            entry.DetectChanges(properties);

            if (entry.State != originalState)
            {
                _keyIndexDirtyTypes.Add(entry.ClrType);
            }
        }
    }

    /// <summary>
    /// Adds the item.
    /// </summary>
    /// <param name="connection">The connection.</param>
    public void AddEdgeConnection(PendingEdgeConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var key = PendingEdgeKey.Create(connection.Edge.GetType(), connection.FromNode, connection.ToNode);

        if (_pendingEdgeDisconnectionsByKey.Remove(key, out var cancelledDisconnection))
        {
            _pendingEdgeDisconnections.Remove(cancelledDisconnection);
        }

        if (!_pendingEdgeConnectionsByEdge.Add(connection.Edge))
        {
            throw new InvalidOperationException("The same edge instance is already connected. Use a separate edge instance for every graph edge row.");
        }

        _pendingEdgeConnections.Add(connection);

        if (!_pendingEdgeConnectionsByKey.TryGetValue(key, out var connectionsForKey))
        {
            connectionsForKey = [];
            _pendingEdgeConnectionsByKey[key] = connectionsForKey;
        }

        connectionsForKey.Add(connection);
    }

    /// <summary>
    /// Adds the item.
    /// </summary>
    /// <param name="disconnection">The disconnection.</param>
    public void AddEdgeDisconnection(PendingEdgeDisconnection disconnection)
    {
        ArgumentNullException.ThrowIfNull(disconnection);

        var key = PendingEdgeKey.Create(disconnection.EdgeType, disconnection.FromNode, disconnection.ToNode);

        if (_pendingEdgeConnectionsByKey.Remove(key, out var cancelledConnections))
        {
            for (var i = 0; i < cancelledConnections.Count; i++)
            {
                var cancelledConnection = cancelledConnections[i];

                _pendingEdgeConnections.Remove(cancelledConnection);
                _pendingEdgeConnectionsByEdge.Remove(cancelledConnection.Edge);
                DetachIfAdded(cancelledConnection.Edge);
            }

            return;
        }

        if (_pendingEdgeDisconnectionsByKey.TryAdd(key, disconnection))
        {
            _pendingEdgeDisconnections.Add(disconnection);
        }
    }

    /// <summary>
    /// Executes accept all changes.
    /// </summary>
    /// <param name="model">The model.</param>
    public void AcceptAllChanges(GraphModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<GraphEntityEntry>? deletedEntries = null;

        foreach (var entry in _entriesByReference.Values)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                case EntityState.Modified:
                    entry.State = EntityState.Unchanged;
                    CaptureSnapshot(entry, model);
                    IndexEntry(model, entry);
                    break;

                case EntityState.Deleted:
                    deletedEntries ??= [];
                    deletedEntries.Add(entry);
                    break;
            }
        }

        if (deletedEntries is not null)
        {
            for (var i = 0; i < deletedEntries.Count; i++)
            {
                RemoveEntry(deletedEntries[i]);
            }
        }

        ClearPendingEdges();
    }

    /// <summary>
    /// Executes clear.
    /// </summary>
    public void Clear()
    {
        _entriesByReference.Clear();
        _entriesByType.Clear();
        _entriesByKey.Clear();
        _keyIndexInitializedTypes.Clear();
        _keyIndexDirtyTypes.Clear();
        ClearPendingEdges();
    }

    private void CaptureSnapshot(GraphEntityEntry entry, GraphModel model)
    {
        var properties = GetSnapshotProperties(entry.ClrType, model);
        entry.CaptureSnapshot(properties);
    }

    private void DetachIfAdded(object entity)
    {
        if (!_entriesByReference.TryGetValue(entity, out var entry) || entry.State != EntityState.Added)
        {
            return;
        }

        RemoveEntry(entry);
    }

    private GraphSnapshotPropertyPlan[] GetSnapshotProperties(Type clrType, GraphModel model)
    {
        if (_snapshotPropertiesByType.TryGetValue(clrType, out var cached))
        {
            return cached;
        }

        var created = CreateSnapshotProperties(clrType, model);
        _snapshotPropertiesByType[clrType] = created;

        return created;
    }

    private static GraphSnapshotPropertyPlan[] CreateSnapshotProperties(Type clrType, GraphModel model)
    {
        if (typeof(Node).IsAssignableFrom(clrType))
        {
            var mapping = model.GetNode(clrType);
            return CreateSnapshotProperties(mapping.ClrType, mapping.KeyPropertyName, mapping.Properties);
        }

        if (typeof(Edge).IsAssignableFrom(clrType))
        {
            var mapping = model.GetEdge(clrType);
            return CreateSnapshotProperties(mapping.ClrType, mapping.KeyPropertyName, mapping.Properties);
        }

        throw new InvalidOperationException(
            $"Type '{clrType.FullName}' is not a supported tracked entity type.");
    }

    private static GraphSnapshotPropertyPlan[] CreateSnapshotProperties(Type clrType, string keyPropertyName, IReadOnlyList<PropertyMapping> mappedProperties)
    {
        var result = new List<GraphSnapshotPropertyPlan>
        {
            CreateSnapshotPropertyPlan(GetRequiredProperty(clrType, keyPropertyName))
        };

        for (var i = 0; i < mappedProperties.Count; i++)
        {
            var propertyName = mappedProperties[i].PropertyName;

            if (string.Equals(propertyName, keyPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(CreateSnapshotPropertyPlan(GetRequiredProperty(clrType, propertyName)));
        }

        return [.. result];
    }

    private GraphEntityEntry GetOrCreateEntry(object entity)
    {
        if (_entriesByReference.TryGetValue(entity, out var existing))
        {
            return existing;
        }

        var entry = new GraphEntityEntry
        {
            Entity = entity,
            ClrType = entity.GetType(),
            State = EntityState.Detached
        };

        _entriesByReference[entity] = entry;

        if (!_entriesByType.TryGetValue(entry.ClrType, out var entriesForType))
        {
            entriesForType = [];
            _entriesByType[entry.ClrType] = entriesForType;
        }

        entriesForType.Add(entry);
        _keyIndexDirtyTypes.Add(entry.ClrType);

        return entry;
    }

    private bool TryGetIndexedEntity(TrackedEntityKey key, out object? entity)
    {
        if (!_entriesByKey.TryGetValue(key, out var entry))
        {
            entity = null;
            return false;
        }

        if (entry.State is EntityState.Detached or EntityState.Deleted)
        {
            _entriesByKey.Remove(key);
            entity = null;
            return false;
        }

        entity = entry.Entity;
        return true;
    }

    private void RebuildKeyIndexForType(GraphModel model, Type clrType)
    {
        RemoveKeyIndexesForType(clrType);

        var keyProperty = GetKeyProperty(model, clrType);

        if (_entriesByType.TryGetValue(clrType, out var entries))
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                if (entry.State is EntityState.Detached or EntityState.Deleted)
                {
                    continue;
                }

                var keyValue = keyProperty.GetValue(entry.Entity);

                if (keyValue is not null)
                {
                    _entriesByKey[new TrackedEntityKey(clrType, keyValue)] = entry;
                }
            }
        }

        _keyIndexInitializedTypes.Add(clrType);
        _keyIndexDirtyTypes.Remove(clrType);
    }

    private void IndexEntry(GraphModel model, GraphEntityEntry entry)
    {
        if (entry.State is EntityState.Detached or EntityState.Deleted)
        {
            RemoveKeyIndexes(entry);
            return;
        }

        var keyProperty = GetKeyProperty(model, entry.ClrType);
        var keyValue = keyProperty.GetValue(entry.Entity);

        if (keyValue is not null)
        {
            _entriesByKey[new TrackedEntityKey(entry.ClrType, keyValue)] = entry;
        }
    }

    private void RemoveEntry(GraphEntityEntry entry)
    {
        _entriesByReference.Remove(entry.Entity);
        RemoveKeyIndexes(entry);

        if (_entriesByType.TryGetValue(entry.ClrType, out var entries))
        {
            entries.Remove(entry);

            if (entries.Count == 0)
            {
                _entriesByType.Remove(entry.ClrType);
            }
        }

        _keyIndexDirtyTypes.Add(entry.ClrType);
    }

    private void RemoveKeyIndexes(GraphEntityEntry entry)
    {
        List<TrackedEntityKey>? keysToRemove = null;

        foreach (var item in _entriesByKey.Where(x => ReferenceEquals(x.Value, entry)))
        {
            keysToRemove ??= [];
            keysToRemove.Add(item.Key);
        }

        if (keysToRemove is null)
        {
            return;
        }

        for (var i = 0; i < keysToRemove.Count; i++)
        {
            _entriesByKey.Remove(keysToRemove[i]);
        }
    }

    private void RemoveKeyIndexesForType(Type clrType)
    {
        List<TrackedEntityKey>? keysToRemove = null;

        foreach (var item in _entriesByKey.Select(e => e.Key))
        {
            if (item.ClrType == clrType)
            {
                keysToRemove ??= [];
                keysToRemove.Add(item);
            }
        }

        if (keysToRemove is null)
        {
            return;
        }

        for (var i = 0; i < keysToRemove.Count; i++)
        {
            _entriesByKey.Remove(keysToRemove[i]);
        }
    }

    private PropertyInfo GetKeyProperty(GraphModel model, Type clrType)
    {
        if (_keyPropertiesByType.TryGetValue(clrType, out var cached))
        {
            return cached;
        }

        var property = CreateKeyProperty(model, clrType);
        _keyPropertiesByType[clrType] = property;

        return property;
    }

    private static PropertyInfo CreateKeyProperty(GraphModel model, Type clrType)
    {
        if (typeof(Node).IsAssignableFrom(clrType))
        {
            var mapping = model.GetNode(clrType);
            return GetRequiredProperty(mapping.ClrType, mapping.KeyPropertyName);
        }

        if (typeof(Edge).IsAssignableFrom(clrType))
        {
            var mapping = model.GetEdge(clrType);
            return GetRequiredProperty(mapping.ClrType, mapping.KeyPropertyName);
        }

        throw new InvalidOperationException(
            $"Type '{clrType.FullName}' is not a supported tracked entity type.");
    }

    private static GraphSnapshotPropertyPlan CreateSnapshotPropertyPlan(PropertyInfo property)
    {
        var target = Expression.Parameter(typeof(object), "target");
        var access = Expression.Property(Expression.Convert(target, property.DeclaringType!), property);
        var box = Expression.Convert(access, typeof(object));
        var getter = Expression.Lambda<Func<object, object?>>(box, target).Compile();

        return new GraphSnapshotPropertyPlan(property.Name, getter);
    }

    private static PropertyInfo GetRequiredProperty(Type clrType, string propertyName)
    {
        var property = clrType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) ?? throw new InvalidOperationException(
                $"Property '{propertyName}' was not found on type '{clrType.FullName}'.");

        return property;
    }

    private void ClearPendingEdges()
    {
        _pendingEdgeConnections.Clear();
        _pendingEdgeDisconnections.Clear();
        _pendingEdgeConnectionsByEdge.Clear();
        _pendingEdgeConnectionsByKey.Clear();
        _pendingEdgeDisconnectionsByKey.Clear();
    }

    private readonly record struct TrackedEntityKey(
        Type ClrType,
        object KeyValue);

    private readonly struct PendingEdgeKey : IEquatable<PendingEdgeKey>
    {
        private readonly Type _edgeType;
        private readonly object _fromNode;
        private readonly object _toNode;

        private PendingEdgeKey(Type edgeType, object fromNode, object toNode)
        {
            _edgeType = edgeType;
            _fromNode = fromNode;
            _toNode = toNode;
        }

        public static PendingEdgeKey Create(Type edgeType, object fromNode, object toNode)
        {
            ArgumentNullException.ThrowIfNull(edgeType);
            ArgumentNullException.ThrowIfNull(fromNode);
            ArgumentNullException.ThrowIfNull(toNode);

            return new PendingEdgeKey(edgeType, fromNode, toNode);
        }

        public bool Equals(PendingEdgeKey other) =>
            _edgeType == other._edgeType &&
            ReferenceEquals(_fromNode, other._fromNode) &&
            ReferenceEquals(_toNode, other._toNode);

        public override bool Equals(object? obj) => obj is PendingEdgeKey other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(_edgeType, RuntimeHelpers.GetHashCode(_fromNode), RuntimeHelpers.GetHashCode(_toNode));
    }

    /// <summary>
    /// Represents reference equality comparer.
    /// </summary>
    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        /// <summary>
        /// Executes new.
        /// </summary>
        /// <returns>The value.</returns>
        public static ReferenceEqualityComparer Instance { get; } = new();

        /// <summary>
        /// Executes equals.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>True when successful; otherwise, false.</returns>
        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <param name="obj">The obj.</param>
        /// <returns>The value.</returns>
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}