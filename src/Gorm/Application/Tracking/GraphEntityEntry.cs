using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Gorm.Application.Tracking;

/// <summary>
/// Represents graph entity entry.
/// </summary>
public sealed class GraphEntityEntry
{
    private static readonly ConcurrentDictionary<Type, Func<object, bool>> IsKeySetAccessors = new();

    private readonly Dictionary<string, object?> _originalValues =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the entity.
    /// </summary>
    public required object Entity { get; init; }

    /// <summary>
    /// Gets or sets the clr type.
    /// </summary>
    public required Type ClrType { get; init; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public EntityState State { get; set; }

    /// <summary>
    /// Gets original values.
    /// </summary>
    /// <returns>The value.</returns>
    public IReadOnlyDictionary<string, object?> OriginalValues => _originalValues;

    /// <summary>
    /// Executes is key set.
    /// </summary>
    /// <returns>True when successful; otherwise, false.</returns>
    public bool IsKeySet() => IsKeySetAccessors.GetOrAdd(ClrType, CreateIsKeySetAccessor)(Entity);

    /// <summary>
    /// Executes capture snapshot.
    /// </summary>
    /// <param name="properties">The properties.</param>
    public void CaptureSnapshot(IEnumerable<PropertyInfo> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        _originalValues.Clear();

        foreach (var property in properties)
        {
            _originalValues[property.Name] = property.GetValue(Entity);
        }
    }

    internal void CaptureSnapshot(IReadOnlyList<GraphSnapshotPropertyPlan> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        _originalValues.Clear();

        for (var i = 0; i < properties.Count; i++)
        {
            var property = properties[i];
            _originalValues[property.Name] = property.Get(Entity);
        }
    }

    /// <summary>
    /// Executes detect changes.
    /// </summary>
    /// <param name="properties">The properties.</param>
    public void DetectChanges(IEnumerable<PropertyInfo> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        foreach (var property in properties)
        {
            var currentValue = property.GetValue(Entity);
            _originalValues.TryGetValue(property.Name, out var originalValue);

            if (!Equals(currentValue, originalValue))
            {
                State = EntityState.Modified;
                return;
            }
        }
    }

    internal void DetectChanges(IReadOnlyList<GraphSnapshotPropertyPlan> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        for (var i = 0; i < properties.Count; i++)
        {
            var property = properties[i];
            var currentValue = property.Get(Entity);
            _originalValues.TryGetValue(property.Name, out var originalValue);

            if (!Equals(currentValue, originalValue))
            {
                State = EntityState.Modified;
                return;
            }
        }
    }

    /// <summary>
    /// Executes try get original value.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The value.</param>
    /// <returns>True when successful; otherwise, false.</returns>
    public bool TryGetOriginalValue(string propertyName, out object? value)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        return _originalValues.TryGetValue(propertyName, out value);
    }

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The value.</returns>
    public object? GetOriginalValue(string propertyName)
    {
        ArgumentNullException.ThrowIfNull(propertyName);

        if (_originalValues.TryGetValue(propertyName, out var value))
        {
            return value;
        }

        throw new InvalidOperationException(
            $"Original value for property '{propertyName}' is not available.");
    }

    private static Func<object, bool> CreateIsKeySetAccessor(Type clrType)
    {
        var idProperty = clrType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);

        if (idProperty is null)
        {
            return _ => true;
        }

        if (idProperty.PropertyType == typeof(Guid))
        {
            var getter = CreateGuidGetter(idProperty);
            return entity => getter(entity) != Guid.Empty;
        }

        if (idProperty.PropertyType == typeof(Guid?))
        {
            var getter = CreateNullableGuidGetter(idProperty);
            return entity => getter(entity) is { } value && value != Guid.Empty;
        }

        var objectGetter = CreateObjectGetter(idProperty);

        return entity =>
        {
            var value = objectGetter(entity);

            return value switch
            {
                null => false,
                Guid guidValue => guidValue != Guid.Empty,
                _ => true
            };
        };
    }

    private static Func<object, Guid> CreateGuidGetter(PropertyInfo property)
    {
        var target = Expression.Parameter(typeof(object), "target");
        var access = Expression.Property(Expression.Convert(target, property.DeclaringType!), property);

        return Expression.Lambda<Func<object, Guid>>(access, target).Compile();
    }

    private static Func<object, Guid?> CreateNullableGuidGetter(PropertyInfo property)
    {
        var target = Expression.Parameter(typeof(object), "target");
        var access = Expression.Property(Expression.Convert(target, property.DeclaringType!), property);

        return Expression.Lambda<Func<object, Guid?>>(access, target).Compile();
    }

    private static Func<object, object?> CreateObjectGetter(PropertyInfo property)
    {
        var target = Expression.Parameter(typeof(object), "target");
        var access = Expression.Property(Expression.Convert(target, property.DeclaringType!), property);
        var box = Expression.Convert(access, typeof(object));

        return Expression.Lambda<Func<object, object?>>(box, target).Compile();
    }
}