using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Gorm.Core.Primitives;

namespace Gorm.Application.Execution;

/// <summary>
/// Caches entity Id access used to build ContainsAsync predicates.
/// </summary>
internal static class GraphContainsPredicateCache
{
    private static readonly ConcurrentDictionary<Type, EntityContainsPlan> EntityPlans = [];

    public static Expression<Func<T, bool>> Create<T>(T item)
    {
        var itemType = typeof(T);

        return IsEntityType(itemType) ? CreateEntityPredicate(item, itemType) : CreateValuePredicate(item, itemType);
    }

    private static Expression<Func<T, bool>> CreateEntityPredicate<T>(T item, Type itemType)
    {
        if (item is null)
        {
            throw new NotSupportedException("ContainsAsync(null) is not supported for entity queries.");
        }

        var plan = EntityPlans.GetOrAdd(itemType, CreateEntityPlan);
        var itemId = plan.GetId(item) ?? throw new InvalidOperationException($"Entity '{itemType.FullName}' has a null Id value.");

        var parameter = Expression.Parameter(itemType, "x");
        var left = Expression.Property(parameter, plan.IdProperty);
        var right = Expression.Constant(itemId, plan.IdProperty.PropertyType);
        var body = Expression.Equal(left, right);

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    private static Expression<Func<T, bool>> CreateValuePredicate<T>(T item, Type itemType)
    {
        var parameter = Expression.Parameter(itemType, "x");
        var body = Expression.Equal(parameter, Expression.Constant(item, itemType));

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    private static EntityContainsPlan CreateEntityPlan(Type itemType)
    {
        var idProperty = ResolveIdProperty(itemType);

        var target = Expression.Parameter(typeof(object), "target");
        var idAccess = Expression.Property(Expression.Convert(target, itemType), idProperty);
        var idGetter = Expression.Lambda<Func<object, object?>>(Expression.Convert(idAccess, typeof(object)), target).Compile();

        return new EntityContainsPlan(idProperty, idGetter);
    }

    private static PropertyInfo ResolveIdProperty(Type itemType)
    {
        var idProperty = itemType.GetProperty(nameof(Node.Id), BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) ?? throw new InvalidOperationException(
                $"Type '{itemType.FullName}' does not expose an Id property.");

        if (!idProperty.CanRead || idProperty.GetIndexParameters().Length != 0)
        {
            throw new InvalidOperationException($"Type '{itemType.FullName}' does not expose a readable non-indexer Id property.");
        }

        return idProperty;
    }

    private static bool IsEntityType(Type itemType) =>
        typeof(Node).IsAssignableFrom(itemType) || typeof(Edge).IsAssignableFrom(itemType);

    private sealed record EntityContainsPlan(PropertyInfo IdProperty, Func<object, object?> GetId);
}