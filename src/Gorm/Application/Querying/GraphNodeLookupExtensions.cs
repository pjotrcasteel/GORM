using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Gorm.Application.Execution;
using Gorm.Core.Primitives;
using Gorm.Core.Sets;

namespace Gorm.Application.Querying;

/// <summary>
/// Provides QOL lookup helpers for graph nodes.
/// </summary>
public static class GraphNodeLookupExtensions
{
    /// <summary>
    /// Filters a node query by the GORM node id.
    /// </summary>
    /// <typeparam name="TNode">The node type.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="id">The node id.</param>
    /// <returns>The filtered query.</returns>
    public static IQueryable<TNode> WhereId<TNode>(this IQueryable<TNode> source, Guid id)
        where TNode : Node
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Where(x => x.Id == id);
    }

    /// <summary>
    /// Filters a node query by the GORM node ids.
    /// </summary>
    /// <typeparam name="TNode">The node type.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="ids">The node ids.</param>
    /// <returns>The filtered query.</returns>
    public static IQueryable<TNode> WhereIds<TNode>(this IQueryable<TNode> source, IEnumerable<Guid> ids)
        where TNode : Node
    {
        var materializedIds = ids.Distinct().ToArray();

        return source.Where(x => Enumerable.Contains(materializedIds, x.Id));
    }

    public static IQueryable<TNode> WhereKey<TNode, TKey>(this GraphSet<TNode> set, TKey key)
    where TNode : Node
    {
        var predicate = BuildKeyPredicate<TNode>(set.Mapping.KeyPropertyName, key);

        return set.Where(predicate);
    }

    public static Task<TNode?> FindByKeyAsync<TNode, TKey>(this GraphSet<TNode> set, TKey key, CancellationToken cancellationToken = default) where TNode : Node =>
        set.WhereKey(key)
           .FirstOrDefaultAsync(cancellationToken);

    private static Expression<Func<TNode, bool>> BuildKeyPredicate<TNode>(string keyPropertyName, object? key)
    {
        var property = typeof(TNode).GetProperty(keyPropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) ?? throw new InvalidOperationException(
                $"Key property '{keyPropertyName}' was not found on '{typeof(TNode).FullName}'.");

        var parameter = Expression.Parameter(typeof(TNode), "x");
        var left = Expression.Property(parameter, property);
        var value = ConvertKeyValue(key, property.PropertyType);
        var right = Expression.Constant(value, property.PropertyType);
        var body = Expression.Equal(left, right);

        return Expression.Lambda<Func<TNode, bool>>(body, parameter);
    }

    private static object? ConvertKeyValue(object? value, Type targetType)
    {
        if (value is null)
        {
            return Nullable.GetUnderlyingType(targetType) is not null || !targetType.IsValueType
                ? null
                : throw new InvalidOperationException($"Key type '{targetType.FullName}' cannot be null.");
        }

        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;

        return value switch
        {
            _ when type.IsInstanceOfType(value) => value,
            string text when type == typeof(Guid) => Guid.Parse(text),
            string text when type.IsEnum => Enum.Parse(type, text, ignoreCase: true),
            _ when type.IsEnum => Enum.ToObject(type, value),
            IConvertible => Convert.ChangeType(value, type, CultureInfo.InvariantCulture),
            _ => value
        };
    }

    /// <summary>
    /// Finds a node by the GORM node id.
    /// </summary>
    /// <typeparam name="TNode">The node type.</typeparam>
    /// <param name="set">The graph set.</param>
    /// <param name="id">The node id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<TNode?> FindAsync<TNode>(this GraphSet<TNode> set, Guid id, CancellationToken cancellationToken = default) where TNode : Node =>
        set.WhereId(id).FirstOrDefaultAsync(cancellationToken);
}