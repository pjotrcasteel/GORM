using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using Gorm.Application.Context;
using Gorm.Application.Context.Extensions;
using Gorm.Application.Querying;
using Gorm.Application.Querying.Translation;
using Gorm.Core.Loading;
using Gorm.Core.Primitives;
using Gorm.Infrastructure.Persistence.Connections;
using Gorm.Infrastructure.Providers.SqlServer;

namespace Gorm.Application.Execution;

/// <summary>
/// Represents graph queryable execution extensions.
/// </summary>
public static class GraphQueryableExecutionExtensions
{
    /// <summary>
    /// Executes to list.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>The items.</returns>
    public static List<T> ToList<T>(this IQueryable<T> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Provider is GraphQueryProvider)
        {
            return GraphQueryableOperatorsExtensions.ExecuteSynchronously(query);
        }

        return query.Provider.Execute<List<T>>(Expression.Call(instance: null, method: GetEnumerableToListMethodInfo<T>(), arguments: query.Expression));
    }

    /// <summary>
    /// Executes to list async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<List<T>> ToListAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ToListCoreAsync(query, cancellationToken);
    }

    private static async Task<List<T>> ToListCoreAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
    {
        var context = ResolveContext(query);
        var preparedQuery = PrepareQuery(query);

        var result = await context.ExecutionEngine.ExecuteListAsync(context, preparedQuery.Query, cancellationToken);

        await ApplyIncludesAsync(context, result, preparedQuery.Includes, cancellationToken);
        return result;
    }

    /// <summary>
    /// Executes to array.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>The value.</returns>
    public static T[] ToArray<T>(this IQueryable<T> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Provider is GraphQueryProvider)
        {
            return [.. query.ToList()];
        }

        return query.Provider.Execute<T[]>(Expression.Call(instance: null, method: GetEnumerableToArrayMethodInfo<T>(), arguments: query.Expression));
    }

    /// <summary>
    /// Executes to array async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<T[]> ToArrayAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ToArrayCoreAsync(query, cancellationToken);
    }

    private static async Task<T[]> ToArrayCoreAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
    {
        var list = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        return [.. list];
    }

    /// <summary>
    /// Executes to hash set.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>The value.</returns>
    public static HashSet<T> ToHashSet<T>(this IQueryable<T> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Provider is GraphQueryProvider)
        {
            return [.. query.ToList()];
        }

        return query.Provider.Execute<HashSet<T>>(Expression.Call(instance: null, method: GetEnumerableToHashSetMethodInfo<T>(), arguments: query.Expression));
    }

    /// <summary>
    /// Executes to hash set async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<HashSet<T>> ToHashSetAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ToHashSetCoreAsync(query, cancellationToken);
    }

    private static async Task<HashSet<T>> ToHashSetCoreAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
    {
        var list = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        return [.. list];
    }

    /// <summary>
    /// Executes to dictionary.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <typeparam name="TKey">The type of t key.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="keySelector">The key selector.</param>
    /// <returns>The value.</returns>
    public static Dictionary<TKey, T> ToDictionary<T, TKey>(this IQueryable<T> query, Func<T, TKey> keySelector)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(keySelector);

        if (query.Provider is GraphQueryProvider)
        {
            return query.ToList().ToDictionary(keySelector);
        }

        return query.Provider.Execute<Dictionary<TKey, T>>(
            Expression.Call(instance: null, method: GetEnumerableToDictionaryMethodInfo<T, TKey>(), query.Expression, Expression.Constant(keySelector)));
    }

    /// <summary>
    /// Executes to dictionary.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <typeparam name="TKey">The type of t key.</typeparam>
    /// <typeparam name="TValue">The type of t value.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="keySelector">The key selector.</param>
    /// <param name="elementSelector">The element selector.</param>
    /// <returns>The value.</returns>
    public static Dictionary<TKey, TValue> ToDictionary<T, TKey, TValue>(this IQueryable<T> query, Func<T, TKey> keySelector, Func<T, TValue> elementSelector)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(elementSelector);

        if (query.Provider is GraphQueryProvider)
        {
            return query.ToList().ToDictionary(keySelector, elementSelector);
        }

        return query.Provider.Execute<Dictionary<TKey, TValue>>(
            Expression.Call(
                instance: null,
                method: GetEnumerableToDictionaryWithElementSelectorMethodInfo<T, TKey, TValue>(),
                query.Expression,
                Expression.Constant(keySelector),
                Expression.Constant(elementSelector)));
    }

    /// <summary>
    /// Executes to dictionary async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <typeparam name="TKey">The type of t key.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="keySelector">The key selector.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<Dictionary<TKey, T>> ToDictionaryAsync<T, TKey>(this IQueryable<T> query, Func<T, TKey> keySelector, CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(keySelector);
        return ToDictionaryCoreAsync(query, keySelector, cancellationToken);
    }

    /// <summary>
    /// Executes to dictionary async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <typeparam name="TKey">The type of t key.</typeparam>
    /// <typeparam name="TValue">The type of t value.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="keySelector">The key selector.</param>
    /// <param name="elementSelector">The element selector.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<Dictionary<TKey, TValue>> ToDictionaryAsync<T, TKey, TValue>(
        this IQueryable<T> query,
        Func<T, TKey> keySelector,
        Func<T, TValue> elementSelector,
        CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(elementSelector);
        return ToDictionaryCoreAsync(query, keySelector, elementSelector, cancellationToken);
    }

    private static async Task<Dictionary<TKey, T>> ToDictionaryCoreAsync<T, TKey>(IQueryable<T> query, Func<T, TKey> keySelector, CancellationToken cancellationToken)
        where TKey : notnull
    {
        var list = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        return list.ToDictionary(keySelector);
    }

    private static async Task<Dictionary<TKey, TValue>> ToDictionaryCoreAsync<T, TKey, TValue>(
        IQueryable<T> query,
        Func<T, TKey> keySelector,
        Func<T, TValue> elementSelector,
        CancellationToken cancellationToken)
        where TKey : notnull
    {
        var list = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        return list.ToDictionary(keySelector, elementSelector);
    }

    /// <summary>
    /// Executes count.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>The value.</returns>
    public static int Count<T>(this IQueryable<T> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Provider is GraphQueryProvider)
        {
            return GraphQueryableOperatorsExtensions.ExecuteSynchronously(query).Count;
        }

        return query.Provider.Execute<int>(Expression.Call(instance: null, method: GetQueryableCountMethodInfo<T>(), arguments: query.Expression));
    }

    /// <summary>
    /// Executes count.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <returns>The value.</returns>
    public static int Count<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);

        return query.Where(predicate).Count();
    }

    /// <summary>
    /// Executes count async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<int> CountAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var context = ResolveContext(query);
        var preparedQuery = PrepareQuery(query);

        return context.ExecutionEngine.ExecuteCountAsync(context, preparedQuery.Query, cancellationToken);
    }

    /// <summary>
    /// Executes count async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<int> CountAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);

        return query.Where(predicate).CountAsync(cancellationToken);
    }

    /// <summary>
    /// Executes long count.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>The value.</returns>
    public static long LongCount<T>(this IQueryable<T> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Provider is GraphQueryProvider)
        {
            return GraphQueryableOperatorsExtensions.ExecuteSynchronously(query).Count;
        }

        return query.Provider.Execute<long>(Expression.Call(instance: null, method: GetQueryableLongCountMethodInfo<T>(), arguments: query.Expression));
    }

    /// <summary>
    /// Executes long count.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <returns>The value.</returns>
    public static long LongCount<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);

        return query.Where(predicate).LongCount();
    }

    /// <summary>
    /// Executes long count async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<long> LongCountAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var context = ResolveContext(query);
        var preparedQuery = PrepareQuery(query);

        return context.ExecutionEngine.ExecuteLongCountAsync(context, preparedQuery.Query, cancellationToken);
    }

    /// <summary>
    /// Executes long count async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<long> LongCountAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);

        return query.Where(predicate).LongCountAsync(cancellationToken);
    }

    /// <summary>
    /// Executes sum.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>The value.</returns>
    public static T Sum<T>(this IQueryable<T> query)
        where T : INumber<T>
    {
        ArgumentNullException.ThrowIfNull(query);

        return query.Provider.Execute<T>(Expression.Call(instance: null, method: GetEnumerableSumMethodInfo<T>(), arguments: query.Expression));
    }

    /// <summary>
    /// Executes sum.
    /// </summary>
    /// <typeparam name="TSource">The type of t source.</typeparam>
    /// <typeparam name="TNumber">The type of t number.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="selector">The selector.</param>
    /// <returns>The value.</returns>
    public static TNumber Sum<TSource, TNumber>(this IQueryable<TSource> query, Expression<Func<TSource, TNumber>> selector)
        where TNumber : INumber<TNumber>
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(selector);

        return query.Select(selector).Sum();
    }

    /// <summary>
    /// Executes sum async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<T> SumAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
        where T : INumber<T>
    {
        ArgumentNullException.ThrowIfNull(query);

        var (context, connectionFactory) = ResolveExecutionServices(query);
        var executor = CreateExecutor(connectionFactory);
        var preparedQuery = PrepareQuery(query);

        return executor.ExecuteSumAsync<T, T>(context, preparedQuery.Query, cancellationToken);
    }

    /// <summary>
    /// Executes sum async.
    /// </summary>
    /// <typeparam name="TSource">The type of t source.</typeparam>
    /// <typeparam name="TNumber">The type of t number.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<TNumber> SumAsync<TSource, TNumber>(
        this IQueryable<TSource> query,
        Expression<Func<TSource, TNumber>> selector,
        CancellationToken cancellationToken = default)
        where TNumber : INumber<TNumber>
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(selector);

        return query.Select(selector).SumAsync(cancellationToken);
    }

    /// <summary>
    /// Executes average.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>The value.</returns>
    public static decimal Average<T>(this IQueryable<T> query)
        where T : INumber<T>
    {
        ArgumentNullException.ThrowIfNull(query);

        return query.Provider.Execute<decimal>(Expression.Call(instance: null, method: GetEnumerableAverageMethodInfo<T>(), arguments: query.Expression));
    }

    /// <summary>
    /// Executes average.
    /// </summary>
    /// <typeparam name="TSource">The type of t source.</typeparam>
    /// <typeparam name="TNumber">The type of t number.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="selector">The selector.</param>
    /// <returns>The value.</returns>
    public static decimal Average<TSource, TNumber>(this IQueryable<TSource> query, Expression<Func<TSource, TNumber>> selector)
        where TNumber : INumber<TNumber>
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(selector);

        return query.Select(selector).Average();
    }

    /// <summary>
    /// Executes average async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<decimal> AverageAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
        where T : INumber<T>
    {
        ArgumentNullException.ThrowIfNull(query);

        var (context, connectionFactory) = ResolveExecutionServices(query);
        var executor = CreateExecutor(connectionFactory);
        var preparedQuery = PrepareQuery(query);

        return executor.ExecuteAverageAsync(context, preparedQuery.Query, cancellationToken);
    }

    /// <summary>
    /// Executes average async.
    /// </summary>
    /// <typeparam name="TSource">The type of t source.</typeparam>
    /// <typeparam name="TNumber">The type of t number.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<decimal> AverageAsync<TSource, TNumber>(
        this IQueryable<TSource> query,
        Expression<Func<TSource, TNumber>> selector,
        CancellationToken cancellationToken = default)
        where TNumber : INumber<TNumber>
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(selector);

        return query.Select(selector).AverageAsync(cancellationToken);
    }

    /// <summary>
    /// Executes min.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>The value.</returns>
    public static T Min<T>(this IQueryable<T> query)
        where T : IComparable<T>
    {
        ArgumentNullException.ThrowIfNull(query);

        return query.Provider.Execute<T>(Expression.Call(instance: null, method: GetEnumerableMinMethodInfo<T>(), arguments: query.Expression));
    }

    /// <summary>
    /// Executes min.
    /// </summary>
    /// <typeparam name="TSource">The type of t source.</typeparam>
    /// <typeparam name="TValue">The type of t value.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="selector">The selector.</param>
    /// <returns>The value.</returns>
    public static TValue Min<TSource, TValue>(this IQueryable<TSource> query, Expression<Func<TSource, TValue>> selector)
        where TValue : IComparable<TValue>
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(selector);

        return query.Select(selector).Min();
    }

    /// <summary>
    /// Executes min async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<T> MinAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
        where T : IComparable<T>
    {
        ArgumentNullException.ThrowIfNull(query);

        var (context, connectionFactory) = ResolveExecutionServices(query);
        var executor = CreateExecutor(connectionFactory);
        var preparedQuery = PrepareQuery(query);

        return executor.ExecuteMinAsync<T, T>(context, preparedQuery.Query, cancellationToken);
    }

    /// <summary>
    /// Executes min async.
    /// </summary>
    /// <typeparam name="TSource">The type of t source.</typeparam>
    /// <typeparam name="TValue">The type of t value.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<TValue> MinAsync<TSource, TValue>(this IQueryable<TSource> query, Expression<Func<TSource, TValue>> selector, CancellationToken cancellationToken = default)
        where TValue : IComparable<TValue>
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(selector);

        return query.Select(selector).MinAsync(cancellationToken);
    }

    /// <summary>
    /// Executes max.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>The value.</returns>
    public static T Max<T>(this IQueryable<T> query)
        where T : IComparable<T>
    {
        ArgumentNullException.ThrowIfNull(query);

        return query.Provider.Execute<T>(Expression.Call(instance: null, method: GetEnumerableMaxMethodInfo<T>(), arguments: query.Expression));
    }

    /// <summary>
    /// Executes max.
    /// </summary>
    /// <typeparam name="TSource">The type of t source.</typeparam>
    /// <typeparam name="TValue">The type of t value.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="selector">The selector.</param>
    /// <returns>The value.</returns>
    public static TValue Max<TSource, TValue>(this IQueryable<TSource> query, Expression<Func<TSource, TValue>> selector)
        where TValue : IComparable<TValue>
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(selector);

        return query.Select(selector).Max();
    }

    /// <summary>
    /// Executes max async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<T> MaxAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
        where T : IComparable<T>
    {
        ArgumentNullException.ThrowIfNull(query);

        var (context, connectionFactory) = ResolveExecutionServices(query);
        var executor = CreateExecutor(connectionFactory);
        var preparedQuery = PrepareQuery(query);

        return executor.ExecuteMaxAsync<T, T>(context, preparedQuery.Query, cancellationToken);
    }

    /// <summary>
    /// Executes max async.
    /// </summary>
    /// <typeparam name="TSource">The type of t source.</typeparam>
    /// <typeparam name="TValue">The type of t value.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="selector">The selector.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<TValue> MaxAsync<TSource, TValue>(this IQueryable<TSource> query, Expression<Func<TSource, TValue>> selector, CancellationToken cancellationToken = default)
        where TValue : IComparable<TValue>
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(selector);

        return query.Select(selector).MaxAsync(cancellationToken);
    }

    /// <summary>
    /// Executes contains.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="item">The item.</param>
    /// <returns>True when successful; otherwise, false.</returns>
    public static bool Contains<T>(this IQueryable<T> query, T item)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query.Provider.Execute<bool>(Expression.Call(instance: null, method: GetEnumerableContainsMethodInfo<T>(), query.Expression, Expression.Constant(item, typeof(T))));
    }

    /// <summary>
    /// Executes contains async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="item">The item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<bool> ContainsAsync<T>(this IQueryable<T> query, T item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var predicate = BuildContainsPredicate(item);
        var filtered = query.Where(predicate);

        return filtered.AnyAsync(cancellationToken);
    }

    /// <summary>
    /// Executes prepare query.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>The value.</returns>
    internal static PreparedGraphQuery<T> PrepareQuery<T>(IQueryable<T> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var extraction = GraphIncludeExpressionHelper.Extract(query.Expression);
        var strippedQuery = ReferenceEquals(extraction.QueryExpression, query.Expression) ? query : query.Provider.CreateQuery<T>(extraction.QueryExpression);

        return new PreparedGraphQuery<T>
        {
            Query = strippedQuery,
            Includes = extraction.Includes
        };
    }

    /// <summary>
    /// Executes apply includes async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="context">The context.</param>
    /// <param name="items">The items.</param>
    /// <param name="includes">The includes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task ApplyIncludesAsync<T>(GraphContext context, List<T> items, IReadOnlyList<GraphIncludeRequest> includes, CancellationToken cancellationToken)
    {
        if (includes.Count == 0 || items.Count == 0)
        {
            return;
        }

        var uniqueNodes = new List<Node>(items.Count);
        var seenNodeIds = new HashSet<GraphIncludeNodeKey>();

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is not Node node)
            {
                throw new InvalidOperationException("Include/IncludeRelationship can only be used on node queries.");
            }

            if (seenNodeIds.Add(new GraphIncludeNodeKey(node.GetType(), node.Id)))
            {
                uniqueNodes.Add(node);
            }
        }

        var visited = new HashSet<GraphIncludeVisitKey>();

        for (var nodeIndex = 0; nodeIndex < uniqueNodes.Count; nodeIndex++)
        {
            var node = uniqueNodes[nodeIndex];

            for (var includeIndex = 0; includeIndex < includes.Count; includeIndex++)
            {
                await ApplyIncludeAsync(context, node, includes[includeIndex], visited, cancellationToken);
            }
        }
    }

    private static async Task ApplyIncludeAsync(
        GraphContext context,
        Node owner,
        GraphIncludeRequest include,
        HashSet<GraphIncludeVisitKey> visited,
        CancellationToken cancellationToken)
    {
        var visitedKey = new GraphIncludeVisitKey(owner.GetType(), owner.Id, include);

        if (!visited.Add(visitedKey))
        {
            return;
        }

        var loaded = await context.LoadRelationshipAsync(owner, include, cancellationToken);

        if (include.Children.Count == 0)
        {
            return;
        }

        var relatedNodes = ExtractDistinctRelatedNodes(loaded, include.IncludeEdge);

        for (var nodeIndex = 0; nodeIndex < relatedNodes.Count; nodeIndex++)
        {
            var relatedNode = relatedNodes[nodeIndex];

            for (var includeIndex = 0; includeIndex < include.Children.Count; includeIndex++)
            {
                await ApplyIncludeAsync(context, relatedNode, include.Children[includeIndex], visited, cancellationToken);
            }
        }
    }

    private static List<Node> ExtractDistinctRelatedNodes(IReadOnlyList<object> loaded, bool includeEdge)
    {
        var relatedNodes = new List<Node>(loaded.Count);
        var seenNodeIds = new HashSet<GraphIncludeNodeKey>();

        for (var i = 0; i < loaded.Count; i++)
        {
            var node = includeEdge ? GraphRelationshipRuntimeCache.ExtractNodeFromEdgeResult(loaded[i]) : loaded[i] as Node;

            if (node is not null && seenNodeIds.Add(new GraphIncludeNodeKey(node.GetType(), node.Id)))
            {
                relatedNodes.Add(node);
            }
        }

        return relatedNodes;
    }

    private static Expression<Func<T, bool>> BuildContainsPredicate<T>(T item) =>
        GraphContainsPredicateCache.Create(item);

    private readonly record struct GraphIncludeNodeKey(Type NodeType, Guid NodeId);

    private readonly record struct GraphIncludeVisitKey(Type OwnerType, Guid OwnerId, GraphIncludeRequest Include);

    /// <summary>
    /// Resolves the graph context from the query provider.
    /// </summary>
    /// <typeparam name="T">The query item type.</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>The graph context.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the query provider is not a graph query provider.</exception>
    internal static GraphContext ResolveContext<T>(IQueryable<T> query)
    {
        if (query.Provider is not GraphQueryProvider provider)
        {
            throw new InvalidOperationException($"The query provider must be '{typeof(GraphQueryProvider).FullName}'.");
        }

        return provider.Context;
    }

    /// <summary>
    /// ResolveExecutionServices
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="query"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    internal static (GraphContext Context, IGormDbConnectionFactory ConnectionFactory) ResolveExecutionServices<T>(IQueryable<T> query)
    {
        var context = ResolveContext(query);

        if (context.ConnectionFactory is null)
        {
            throw new InvalidOperationException(
                $"No {nameof(IGormDbConnectionFactory)} is configured on the current {nameof(GraphContext)}. " +
                $"Call {nameof(GraphContext.UseConnectionFactory)}(...) first or construct the context with a connection factory.");
        }

        return (context, context.ConnectionFactory);
    }

    /// <summary>
    /// Creates the item.
    /// </summary>
    /// <param name="connectionFactory">The connection factory.</param>
    /// <returns>The value.</returns>
    internal static GraphQueryExecutor CreateExecutor(IGormDbConnectionFactory connectionFactory) =>
        new(connectionFactory, ResolveProvider().QuerySqlGenerator);

    private static SqlServerGraphProvider ResolveProvider() => SqlServerGraphProvider.Instance;

    private static MethodInfo GetQueryableCountMethodInfo<T>() =>
        ResolveClosedGenericMethod(typeof(Queryable), nameof(Queryable.Count), [typeof(T)], typeof(int), [typeof(IQueryable<T>)]);

    private static MethodInfo GetQueryableLongCountMethodInfo<T>() =>
        ResolveClosedGenericMethod(typeof(Queryable), nameof(Queryable.LongCount), [typeof(T)], typeof(long), [typeof(IQueryable<T>)]);

    private static MethodInfo GetEnumerableToListMethodInfo<T>() =>
        ResolveClosedGenericMethod(typeof(Enumerable), nameof(Enumerable.ToList), [typeof(T)], typeof(List<T>), [typeof(IEnumerable<T>)]);

    private static MethodInfo GetEnumerableToArrayMethodInfo<T>() =>
        ResolveClosedGenericMethod(typeof(Enumerable), nameof(Enumerable.ToArray), [typeof(T)], typeof(T[]), [typeof(IEnumerable<T>)]);

    private static MethodInfo GetEnumerableToHashSetMethodInfo<T>() =>
        ResolveClosedGenericMethod(typeof(Enumerable), nameof(Enumerable.ToHashSet), [typeof(T)], typeof(HashSet<T>), [typeof(IEnumerable<T>)]);

    private static MethodInfo GetEnumerableToDictionaryMethodInfo<T, TKey>()
        where TKey : notnull =>
        ResolveClosedGenericMethod(
            typeof(Enumerable),
            nameof(Enumerable.ToDictionary),
            [typeof(T), typeof(TKey)],
            typeof(Dictionary<TKey, T>),
            [typeof(IEnumerable<T>), typeof(Func<T, TKey>)]);

    private static MethodInfo GetEnumerableToDictionaryWithElementSelectorMethodInfo<T, TKey, TValue>()
        where TKey : notnull =>
        ResolveClosedGenericMethod(
            typeof(Enumerable),
            nameof(Enumerable.ToDictionary),
            [typeof(T), typeof(TKey), typeof(TValue)],
            typeof(Dictionary<TKey, TValue>),
            [typeof(IEnumerable<T>), typeof(Func<T, TKey>), typeof(Func<T, TValue>)]);

    private static MethodInfo GetEnumerableContainsMethodInfo<T>() =>
        ResolveClosedGenericMethod(typeof(Enumerable), nameof(Enumerable.Contains), [typeof(T)], typeof(bool), [typeof(IEnumerable<T>), typeof(T)]);

    private static MethodInfo GetEnumerableSumMethodInfo<T>()
        where T : INumber<T> =>
        ResolveClosedGenericMethod(typeof(Enumerable), nameof(Enumerable.Sum), [typeof(T)], typeof(T), [typeof(IEnumerable<T>)]);

    private static MethodInfo GetEnumerableAverageMethodInfo<T>()
        where T : INumber<T> =>
        ResolveClosedGenericMethod(typeof(Enumerable), nameof(Enumerable.Average), [typeof(T)], typeof(decimal), [typeof(IEnumerable<T>)]);

    private static MethodInfo GetEnumerableMinMethodInfo<T>()
        where T : IComparable<T> =>
        ResolveClosedGenericMethod(typeof(Enumerable), nameof(Enumerable.Min), [typeof(T)], typeof(T), [typeof(IEnumerable<T>)]);

    private static MethodInfo GetEnumerableMaxMethodInfo<T>()
        where T : IComparable<T> =>
        ResolveClosedGenericMethod(typeof(Enumerable), nameof(Enumerable.Max), [typeof(T)], typeof(T), [typeof(IEnumerable<T>)]);

    private static MethodInfo ResolveClosedGenericMethod(Type declaringType, string methodName, Type[] genericArguments, Type expectedReturnType, Type[] expectedParameterTypes)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ArgumentNullException.ThrowIfNull(genericArguments);
        ArgumentNullException.ThrowIfNull(expectedReturnType);
        ArgumentNullException.ThrowIfNull(expectedParameterTypes);

        var matches = declaringType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method =>
                method.Name == methodName &&
                method.IsGenericMethodDefinition &&
                method.GetGenericArguments().Length == genericArguments.Length &&
                method.GetParameters().Length == expectedParameterTypes.Length)
            .Select(method => TryMakeGenericMethod(method, genericArguments))
            .Where(method => method is not null)
            .Select(method => method!)
            .Where(method => method.ReturnType == expectedReturnType && ParametersMatch(method, expectedParameterTypes))
            .ToList();

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException($"No matching overload was found for '{declaringType.FullName}.{methodName}'."),
            _ => throw new InvalidOperationException($"Multiple matching overloads were found for '{declaringType.FullName}.{methodName}'.")
        };
    }

    private static MethodInfo? TryMakeGenericMethod(MethodInfo method, Type[] genericArguments)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(genericArguments);

        try
        {
            return method.MakeGenericMethod(genericArguments);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool ParametersMatch(MethodInfo method, Type[] expectedParameterTypes)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(expectedParameterTypes);

        var parameters = method.GetParameters();

        if (parameters.Length != expectedParameterTypes.Length)
        {
            return false;
        }

        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType != expectedParameterTypes[i])
            {
                return false;
            }
        }

        return true;
    }
}