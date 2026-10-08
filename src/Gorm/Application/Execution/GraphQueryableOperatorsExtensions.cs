using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Gorm.Application.Context;
using Gorm.Application.Execution;
using Gorm.Application.Execution.InMemory;
using Gorm.Application.Querying;
using Gorm.Application.Querying.Models;

namespace Gorm.Application.Execution;

/// <summary>
/// Represents graph queryable operators extensions.
/// </summary>
public static class GraphQueryableOperatorsExtensions
{
    private const string EmptySequenceMessage = "Sequence contains no elements.";
    private const string MultipleElementsMessage = "Sequence contains more than one element.";

    /// <summary>
    /// Executes first.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>The value.</returns>
    public static T First<T>(this IQueryable<T> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var list = ExecuteSynchronously(query, GraphQueryExecutionMode.First);
        return list.Count == 0
            ? throw new InvalidOperationException(EmptySequenceMessage)
            : list[0];
    }

    /// <summary>
    /// Executes first.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <returns>The value.</returns>
    public static T First<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);

        return query.Where(predicate).First();
    }

    /// <summary>
    /// Executes first async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<T> FirstAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return FirstCoreAsync(query, cancellationToken);
    }

    /// <summary>
    /// Executes first async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<T> FirstAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);

        return query.Where(predicate).FirstAsync(cancellationToken);
    }

    private static async Task<T> FirstCoreAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
    {
        var context = ResolveContext(query);
        var preparedQuery = GraphQueryableExecutionExtensions.PrepareQuery(query);

        var list = await context.ExecutionEngine.ExecuteAsync(
            context,
            preparedQuery.Query,
            new GraphQueryExecutionRequest
            {
                Mode = GraphQueryExecutionMode.First
            },
            cancellationToken);

        await GraphQueryableExecutionExtensions.ApplyIncludesAsync(context, list, [.. preparedQuery.Includes], cancellationToken);

        if (list.Count == 0)
        {
            throw new InvalidOperationException(EmptySequenceMessage);
        }

        return list[0];
    }

    /// <summary>
    /// Executes first or default.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <returns>The value.</returns>
    public static T? FirstOrDefault<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);

        return query.Where(predicate).FirstOrDefault();
    }

    /// <summary>
    /// Executes first or default.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>The value.</returns>
    public static T? FirstOrDefault<T>(this IQueryable<T> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return ExecuteSynchronously(query, GraphQueryExecutionMode.FirstOrDefault).FirstOrDefault();
    }

    /// <summary>
    /// Executes first or default async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<T?> FirstOrDefaultAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return FirstOrDefaultCoreAsync(query, cancellationToken);
    }

    /// <summary>
    /// Executes first or default async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<T?> FirstOrDefaultAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);

        return query.Where(predicate).FirstOrDefaultAsync(cancellationToken);
    }

    private static async Task<T?> FirstOrDefaultCoreAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
    {
        var context = ResolveContext(query);
        var preparedQuery = GraphQueryableExecutionExtensions.PrepareQuery(query);

        var list = await context.ExecutionEngine.ExecuteAsync(
            context,
            preparedQuery.Query,
            new GraphQueryExecutionRequest
            {
                Mode = GraphQueryExecutionMode.FirstOrDefault
            },
            cancellationToken);

        await GraphQueryableExecutionExtensions.ApplyIncludesAsync(context, list, [.. preparedQuery.Includes], cancellationToken);

        return list.FirstOrDefault();
    }

    /// <summary>
    /// Executes single.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>The value.</returns>
    public static T Single<T>(this IQueryable<T> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var list = ExecuteSynchronously(query, GraphQueryExecutionMode.Single);
        return list.Count switch
        {
            0 => throw new InvalidOperationException(EmptySequenceMessage),
            > 1 => throw new InvalidOperationException(MultipleElementsMessage),
            _ => list[0]
        };
    }

    /// <summary>
    /// Executes single.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <returns>The value.</returns>
    public static T Single<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);

        return query.Where(predicate).Single();
    }

    /// <summary>
    /// Executes single async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<T> SingleAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return SingleCoreAsync(query, cancellationToken);
    }

    /// <summary>
    /// Executes single async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<T> SingleAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);

        return query.Where(predicate).SingleAsync(cancellationToken);
    }

    private static async Task<T> SingleCoreAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
    {
        var context = ResolveContext(query);
        var preparedQuery = GraphQueryableExecutionExtensions.PrepareQuery(query);

        var list = await context.ExecutionEngine.ExecuteAsync(
            context,
            preparedQuery.Query,
            new GraphQueryExecutionRequest
            {
                Mode = GraphQueryExecutionMode.Single
            },
            cancellationToken);

        await GraphQueryableExecutionExtensions.ApplyIncludesAsync(context, list, [.. preparedQuery.Includes], cancellationToken);

        return list.Count switch
        {
            0 => throw new InvalidOperationException(EmptySequenceMessage),
            > 1 => throw new InvalidOperationException(MultipleElementsMessage),
            _ => list[0]
        };
    }

    /// <summary>
    /// Executes single or default.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>The value.</returns>
    public static T? SingleOrDefault<T>(this IQueryable<T> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var list = ExecuteSynchronously(query, GraphQueryExecutionMode.SingleOrDefault);
        return list.Count switch
        {
            0 => default,
            > 1 => throw new InvalidOperationException(MultipleElementsMessage),
            _ => list[0]
        };
    }

    /// <summary>
    /// Executes single or default.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <returns>The value.</returns>
    public static T? SingleOrDefault<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);

        return query.Where(predicate).SingleOrDefault();
    }

    /// <summary>
    /// Executes single or default async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<T?> SingleOrDefaultAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return SingleOrDefaultCoreAsync(query, cancellationToken);
    }

    /// <summary>
    /// Executes single or default async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<T?> SingleOrDefaultAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);

        return query.Where(predicate).SingleOrDefaultAsync(cancellationToken);
    }

    private static async Task<T?> SingleOrDefaultCoreAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
    {
        var context = ResolveContext(query);
        var preparedQuery = GraphQueryableExecutionExtensions.PrepareQuery(query);

        var list = await context.ExecutionEngine.ExecuteAsync(
            context,
            preparedQuery.Query,
            new GraphQueryExecutionRequest
            {
                Mode = GraphQueryExecutionMode.SingleOrDefault
            },
            cancellationToken);

        await GraphQueryableExecutionExtensions.ApplyIncludesAsync(context, list, [.. preparedQuery.Includes], cancellationToken);

        return list.Count switch
        {
            0 => default,
            > 1 => throw new InvalidOperationException(MultipleElementsMessage),
            _ => list[0]
        };
    }

    /// <summary>
    /// Executes any.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>True when successful; otherwise, false.</returns>
    public static bool Any<T>(this IQueryable<T> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return ExecuteAnySynchronously(query);
    }

    /// <summary>
    /// Executes any.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <returns>True when successful; otherwise, false.</returns>
    public static bool Any<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);

        return ExecuteAnySynchronously(query.Where(predicate));
    }

    /// <summary>
    /// Executes any async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<bool> AnyAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var context = ResolveContext(query);
        var preparedQuery = GraphQueryableExecutionExtensions.PrepareQuery(query);

        return context.ExecutionEngine.ExecuteAnyAsync(context, preparedQuery.Query, cancellationToken);
    }

    /// <summary>
    /// Executes any async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<bool> AnyAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);

        return query.Where(predicate).AnyAsync(cancellationToken);
    }

    /// <summary>
    /// Executes all.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <returns>True when successful; otherwise, false.</returns>
    public static bool All<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);

        return !ExecuteAnySynchronously(query.Where(NegatePredicate(predicate)));
    }

    /// <summary>
    /// Executes all async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<bool> AllAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);
        return AllCoreAsync(query, predicate, cancellationToken);
    }

    private static async Task<bool> AllCoreAsync<T>(IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
    {
        var hasNonMatchingItem = await query.Where(NegatePredicate(predicate)).AnyAsync(cancellationToken).ConfigureAwait(false);

        return !hasNonMatchingItem;
    }

    private static GraphContext ResolveContext<T>(IQueryable<T> query)
    {
        if (query.Provider is not GraphQueryProvider provider)
        {
            throw new InvalidOperationException($"The query provider must be '{typeof(GraphQueryProvider).FullName}'.");
        }

        return provider.Context;
    }

    /// <summary>
    /// Executes last async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<T> LastAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return LastCoreAsync(query, cancellationToken);
    }

    /// <summary>
    /// Executes last async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<T> LastAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);

        return query.Where(predicate).LastAsync(cancellationToken);
    }

    private static async Task<T> LastCoreAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
    {
        var result = await TryExecuteLastServerSideAsync(query, cancellationToken).ConfigureAwait(false);

        if (!result.HasValue)
        {
            throw new InvalidOperationException(EmptySequenceMessage);
        }

        return result.Value;
    }

    /// <summary>
    /// Executes last or default async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<T?> LastOrDefaultAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return LastOrDefaultCoreAsync(query, cancellationToken);
    }

    /// <summary>
    /// Executes last or default async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<T?> LastOrDefaultAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);

        return query.Where(predicate).LastOrDefaultAsync(cancellationToken);
    }

    private static async Task<T?> LastOrDefaultCoreAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
    {
        var result = await TryExecuteLastServerSideAsync(query, cancellationToken).ConfigureAwait(false);
        return result.HasValue ? result.Value : default;
    }

    /// <summary>
    /// Executes last.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>The value.</returns>
    public static T Last<T>(this IQueryable<T> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var list = ExecuteSynchronously(query, GraphQueryExecutionMode.List);
        return list.Count == 0
            ? throw new InvalidOperationException(EmptySequenceMessage)
            : list[^1];
    }

    /// <summary>
    /// Executes last.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <returns>The value.</returns>
    public static T Last<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);

        return query.Where(predicate).Last();
    }

    /// <summary>
    /// Executes last or default.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>The value.</returns>
    public static T? LastOrDefault<T>(this IQueryable<T> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var list = ExecuteSynchronously(query, GraphQueryExecutionMode.List);
        return list.Count == 0 ? default : list[^1];
    }

    /// <summary>
    /// Executes last or default.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <returns>The value.</returns>
    public static T? LastOrDefault<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);

        return query.Where(predicate).LastOrDefault();
    }

    /// <summary>
    /// Executes element at.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="index">The index.</param>
    /// <returns>The value.</returns>
    public static T ElementAt<T>(this IQueryable<T> query, int index)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        var list = ExecuteSynchronously(query.Skip(index), GraphQueryExecutionMode.First);
        return list.Count == 0
            ? throw new InvalidOperationException("Sequence contains no element at the specified index.")
            : list[0];
    }

    /// <summary>
    /// Executes element at async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="index">The index.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<T> ElementAtAsync<T>(this IQueryable<T> query, int index, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return ElementAtCoreAsync(query, index, cancellationToken);
    }

    private static async Task<T> ElementAtCoreAsync<T>(IQueryable<T> query, int index, CancellationToken cancellationToken)
    {
        var list = await query.Skip(index).Take(1).ToListAsync(cancellationToken).ConfigureAwait(false);

        if (list.Count == 0)
        {
            throw new InvalidOperationException("Sequence contains no element at the specified index.");
        }

        return list[0];
    }

    /// <summary>
    /// Executes element at or default.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="index">The index.</param>
    /// <returns>The value.</returns>
    public static T? ElementAtOrDefault<T>(this IQueryable<T> query, int index)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        return ExecuteSynchronously(query.Skip(index), GraphQueryExecutionMode.FirstOrDefault).FirstOrDefault();
    }

    /// <summary>
    /// Executes element at or default async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="index">The index.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<T?> ElementAtOrDefaultAsync<T>(this IQueryable<T> query, int index, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return ElementAtOrDefaultCoreAsync(query, index, cancellationToken);
    }

    private static async Task<T?> ElementAtOrDefaultCoreAsync<T>(IQueryable<T> query, int index, CancellationToken cancellationToken)
    {
        var list = await query.Skip(index).Take(1).ToListAsync(cancellationToken).ConfigureAwait(false);

        return list.Count == 0 ? default : list[0];
    }

    private static Expression<Func<T, bool>> NegatePredicate<T>(Expression<Func<T, bool>> predicate)
    {
        var body = Expression.Not(predicate.Body);
        return Expression.Lambda<Func<T, bool>>(body, predicate.Parameters);
    }

    private static async Task<OptionalResult<T>> TryExecuteLastServerSideAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
    {
        if (query.Provider is not GraphQueryProvider _)
        {
            return await ExecuteLastClientSideAsync(query, cancellationToken).ConfigureAwait(false);
        }

        var context = GraphQueryableExecutionExtensions.ResolveContext(query);

        if (context.ConnectionFactory is null)
        {
            return await ExecuteLastClientSideAsync(query, cancellationToken).ConfigureAwait(false);
        }

        var preparedQuery = GraphQueryableExecutionExtensions.PrepareQuery(query);
        var queryModel = GraphQueryProvider.Translate(preparedQuery.Query.Expression);

        if (queryModel.SkipCount is not null || queryModel.TakeCount is not null)
        {
            return await ExecuteLastClientSideAsync(query, cancellationToken).ConfigureAwait(false);
        }

        var (executionContext, connectionFactory) = GraphQueryableExecutionExtensions.ResolveExecutionServices(query);
        var optimizedModel = ReverseForLast(executionContext, queryModel);
        var executor = GraphQueryableExecutionExtensions.CreateExecutor(connectionFactory);

        var results = await executor.ExecuteAsync(executionContext, preparedQuery.Query, optimizedModel, effectiveTakeOverride: 1, cancellationToken).ConfigureAwait(false);

        await GraphQueryableExecutionExtensions.ApplyIncludesAsync(executionContext, results, [.. preparedQuery.Includes], cancellationToken).ConfigureAwait(false);

        return results.Count == 0 ? OptionalResult<T>.None : OptionalResult<T>.Some(results[0]);
    }

    private static async Task<OptionalResult<T>> ExecuteLastClientSideAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
    {
        var results = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        return results.Count == 0 ? OptionalResult<T>.None : OptionalResult<T>.Some(results[^1]);
    }

    internal static List<T> ExecuteSynchronously<T>(IQueryable<T> query, GraphQueryExecutionMode mode = GraphQueryExecutionMode.List)
    {
        var context = ResolveContext(query);
        var preparedQuery = GraphQueryableExecutionExtensions.PrepareQuery(query);
        if (preparedQuery.Includes.Count != 0)
        {
            throw new NotSupportedException("Synchronous query operators do not support relationship includes. Use the asynchronous operator instead.");
        }

        if (context.ExecutionEngine is not InMemoryGraphExecutionEngine inMemory)
        {
            throw new NotSupportedException("Synchronous query operators are supported only by the in-memory provider. Use the asynchronous operator instead.");
        }

        return inMemory.Execute(context, preparedQuery.Query, new GraphQueryExecutionRequest { Mode = mode });
    }

    private static bool ExecuteAnySynchronously<T>(IQueryable<T> query)
    {
        var context = ResolveContext(query);
        var preparedQuery = GraphQueryableExecutionExtensions.PrepareQuery(query);
        if (preparedQuery.Includes.Count != 0)
        {
            throw new NotSupportedException("Synchronous query operators do not support relationship includes. Use the asynchronous operator instead.");
        }

        return context.ExecutionEngine is InMemoryGraphExecutionEngine inMemory
            ? inMemory.ExecuteAny(context, preparedQuery.Query)
            : throw new NotSupportedException("Synchronous query operators are supported only by the in-memory provider. Use the asynchronous operator instead.");
    }

    private static GraphQueryModel ReverseForLast(GraphContext context, GraphQueryModel queryModel)
    {
        var reversed = queryModel.Clone();
        reversed.Orderings.Clear();

        if (queryModel.Orderings.Count == 0)
        {
            reversed.Orderings.Add(new GraphOrdering { PropertyName = GetFallbackOrderingPropertyName(context, queryModel), Descending = true, IsProjected = false });

            return reversed;
        }

        foreach (var ordering in queryModel.Orderings)
        {
            reversed.Orderings.Add(new GraphOrdering { PropertyName = ordering.PropertyName, Descending = !ordering.Descending, IsProjected = ordering.IsProjected });
        }

        return reversed;
    }

    private static string GetFallbackOrderingPropertyName(GraphContext context, GraphQueryModel queryModel) => queryModel.CurrentElementKind switch
    {
        GraphQueryElementKind.Node => context.Model.GetNode(queryModel.CurrentElementType).KeyPropertyName,
        GraphQueryElementKind.Edge => context.Model.GetEdge(queryModel.CurrentElementType).KeyPropertyName,
        _ => throw new InvalidOperationException($"Unsupported graph element kind '{queryModel.CurrentElementKind}'.")
    };

    /// <summary>
    /// Represents optional result.
    /// </summary>
    private readonly struct OptionalResult<TValue> : IEquatable<OptionalResult<TValue>>
    {
        private readonly TValue? _value;

        private OptionalResult(bool hasValue, TValue? value)
        {
            HasValue = hasValue;
            _value = value;
        }

        /// <summary>
        /// Gets a value indicating whether has value.
        /// </summary>
        [MemberNotNullWhen(true, nameof(_value))]
        public bool HasValue { get; }

        /// <summary>
        /// Gets the value.
        /// </summary>
        public TValue Value
        {
            get
            {
                if (!HasValue)
                {
                    throw new InvalidOperationException("No value is available.");
                }

                return _value!;
            }
        }

        public static OptionalResult<TValue> None => new(false, default);

        /// <summary>
        /// Executes some.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static OptionalResult<TValue> Some(TValue value) => new(true, value);

        public bool Equals(OptionalResult<TValue> other)
        {
            if (HasValue != other.HasValue)
            {
                return false;
            }

            if (!HasValue)
            {
                return true;
            }

            return EqualityComparer<TValue>.Default.Equals(_value!, other._value!);
        }

        public override bool Equals(object? obj)
        {
            return obj is OptionalResult<TValue> other && Equals(other);
        }

        public override int GetHashCode()
        {
            if (!HasValue)
            {
                return 0;
            }

            return EqualityComparer<TValue>.Default.GetHashCode(_value!);
        }

        public static bool operator ==(OptionalResult<TValue> left, OptionalResult<TValue> right) =>
            left.Equals(right);

        public static bool operator !=(OptionalResult<TValue> left, OptionalResult<TValue> right) =>
            !left.Equals(right);
    }
}