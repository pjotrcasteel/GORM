using System.Data;
using System.Diagnostics;
using System.Globalization;
using Gorm.Application.Context;
using Gorm.Application.Diagnostics;
using Gorm.Application.Querying;
using Gorm.Application.Querying.Models;
using Gorm.Core.Models;
using Gorm.Infrastructure.Persistence.Connections;
using Gorm.Infrastructure.Providers.Abstractions;
using Gorm.Infrastructure.Sql;

namespace Gorm.Application.Execution;

/// <summary>
/// Represents graph query executor.
/// </summary>
public sealed class GraphQueryExecutor
{
    private const CommandBehavior QueryReaderBehavior = CommandBehavior.Default;

    private readonly IGormDbConnectionFactory _connectionFactory;
    private readonly IGraphQuerySqlGenerator _sqlGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQueryExecutor"/> class.
    /// </summary>
    /// <param name="connectionFactory">The connection factory.</param>
    /// <param name="sqlGenerator">The sql generator.</param>
    public GraphQueryExecutor(IGormDbConnectionFactory connectionFactory, IGraphQuerySqlGenerator sqlGenerator)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _sqlGenerator = sqlGenerator ?? throw new ArgumentNullException(nameof(sqlGenerator));
    }

    /// <summary>
    /// Executes execute async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="query">The query.</param>
    /// <param name="request">The include request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<List<T>> ExecuteAsync<T>(GraphContext context, IQueryable<T> query, GraphQueryExecutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(request);

        if (request.IsExistenceOnly)
        {
            throw new InvalidOperationException("Use ExecuteAnyAsync(...) for existence checks.");
        }

        var queryModel = Translate(query);
        var effectiveTake = request.GetEffectiveTake(queryModel.TakeCount);
        var sql = _sqlGenerator.Generate(context.Model, queryModel, effectiveTake);

        return ExecuteListSqlAsync<T>(context, queryModel, sql, cancellationToken);
    }

    /// <summary>
    /// Executes execute async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="query">The query.</param>
    /// <param name="queryModel">The query model.</param>
    /// <param name="effectiveTakeOverride">The effective take override.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    internal Task<List<T>> ExecuteAsync<T>(
        GraphContext context,
        IQueryable<T> query,
        GraphQueryModel queryModel,
        int? effectiveTakeOverride,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(queryModel);

        var sql = _sqlGenerator.Generate(context.Model, queryModel, effectiveTakeOverride);

        return ExecuteListSqlAsync<T>(context, queryModel, sql, cancellationToken);
    }

    /// <summary>
    /// Executes execute any async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<bool> ExecuteAnyAsync<T>(GraphContext context, IQueryable<T> query, CancellationToken cancellationToken = default) =>
        ExecuteScalarQueryAsync(
            context,
            query,
            (model, queryModel) => _sqlGenerator.GenerateExists(model, queryModel, effectiveTakeOverride: 1),
            static result => result is not null && result != DBNull.Value,
            cancellationToken);

    /// <summary>
    /// Executes execute count async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<int> ExecuteCountAsync<T>(GraphContext context, IQueryable<T> query, CancellationToken cancellationToken = default) =>
        ExecuteScalarQueryAsync(
            context,
            query,
            (model, queryModel) => _sqlGenerator.GenerateCount(model, queryModel),
            static result => result switch
            {
                null => 0,
                DBNull => 0,
                int intValue => intValue,
                long longValue => checked((int)longValue),
                decimal decimalValue => checked((int)decimalValue),
                _ => Convert.ToInt32(result, CultureInfo.InvariantCulture)
            },
            cancellationToken);

    /// <summary>
    /// Executes execute long count async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<long> ExecuteLongCountAsync<T>(GraphContext context, IQueryable<T> query, CancellationToken cancellationToken = default) =>
        ExecuteScalarQueryAsync(
            context,
            query,
            _sqlGenerator.GenerateLongCount,
            static result => result switch
            {
                null => 0L,
                DBNull => 0L,
                long longValue => longValue,
                int intValue => intValue,
                decimal decimalValue => checked((long)decimalValue),
                _ => Convert.ToInt64(result, CultureInfo.InvariantCulture)
            },
            cancellationToken);

    /// <summary>
    /// Executes execute sum async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <typeparam name="TResult">The type of t result.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<TResult> ExecuteSumAsync<T, TResult>(GraphContext context, IQueryable<T> query, CancellationToken cancellationToken = default) =>
        ExecuteScalarQueryAsync(
            context,
            query,
            static (model, queryModel, generator) => generator.GenerateAggregate(model, queryModel, GraphAggregateKind.Sum),
            _sqlGenerator,
            static result => result is null || result == DBNull.Value ? GetSumDefault<TResult>() : ConvertAggregateResult<TResult>(result),
            cancellationToken);

    /// <summary>
    /// Executes execute average async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<decimal> ExecuteAverageAsync<T>(GraphContext context, IQueryable<T> query, CancellationToken cancellationToken = default) =>
        ExecuteScalarQueryAsync(
            context,
            query,
            static (model, queryModel, generator) => generator.GenerateAggregate(model, queryModel, GraphAggregateKind.Average),
            _sqlGenerator,
            static result => result is null || result == DBNull.Value
                ? throw new InvalidOperationException("Sequence contains no elements.")
                : Convert.ToDecimal(result, CultureInfo.InvariantCulture),
            cancellationToken);

    /// <summary>
    /// Executes execute min async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <typeparam name="TResult">The type of t result.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<TResult> ExecuteMinAsync<T, TResult>(GraphContext context, IQueryable<T> query, CancellationToken cancellationToken = default) =>
        ExecuteScalarQueryAsync(
            context,
            query,
            static (model, queryModel, generator) => generator.GenerateAggregate(model, queryModel, GraphAggregateKind.Min),
            _sqlGenerator,
            static result => result is null || result == DBNull.Value
                ? throw new InvalidOperationException("Sequence contains no elements.")
                : ConvertAggregateResult<TResult>(result),
            cancellationToken);

    /// <summary>
    /// Executes execute max async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <typeparam name="TResult">The type of t result.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<TResult> ExecuteMaxAsync<T, TResult>(GraphContext context, IQueryable<T> query, CancellationToken cancellationToken = default) =>
        ExecuteScalarQueryAsync(
            context,
            query,
            static (model, queryModel, generator) => generator.GenerateAggregate(model, queryModel, GraphAggregateKind.Max),
            _sqlGenerator,
            static result => result is null || result == DBNull.Value
                ? throw new InvalidOperationException("Sequence contains no elements.")
                : ConvertAggregateResult<TResult>(result),
            cancellationToken);

    private async Task<List<T>> ExecuteListSqlAsync<T>(GraphContext context, GraphQueryModel queryModel, GraphSqlQuery sql, CancellationToken cancellationToken)
    {
        using var activity = GormDiagnostics.StartQueryActivity("gorm.query.list", context, sql, typeof(T));
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            await using var lease = await GraphQueryCommandLease.OpenAsync(context, _connectionFactory, cancellationToken);
            await using var command = lease.CreateCommand(sql);
            await using var reader = await command.ExecuteReaderAsync(QueryReaderBehavior, cancellationToken);

            var results = await GraphMaterializer.MaterializeListAsync<T>(context, reader, queryModel, cancellationToken);

            GormDiagnostics.RecordQuerySuccess(activity, startedAt, results.Count);
            return results;
        }
        catch (Exception ex)
        {
            GormDiagnostics.RecordQueryFailure(activity, startedAt, ex);
            throw CreateSqlExecutionException(sql, ex);
        }
    }

    private Task<TResult> ExecuteScalarQueryAsync<T, TResult>(
        GraphContext context,
        IQueryable<T> query,
        Func<GraphModel, GraphQueryModel, GraphSqlQuery> sqlFactory,
        Func<object?, TResult> converter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(sqlFactory);
        ArgumentNullException.ThrowIfNull(converter);

        var queryModel = Translate(query);
        var sql = sqlFactory(context.Model, queryModel);

        return ExecuteScalarSqlAsync(context, sql, converter, cancellationToken);
    }

    private Task<TResult> ExecuteScalarQueryAsync<T, TState, TResult>(
        GraphContext context,
        IQueryable<T> query,
        Func<GraphModel, GraphQueryModel, TState, GraphSqlQuery> sqlFactory,
        TState state,
        Func<object?, TResult> converter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(sqlFactory);
        ArgumentNullException.ThrowIfNull(converter);

        var queryModel = Translate(query);
        var sql = sqlFactory(context.Model, queryModel, state);

        return ExecuteScalarSqlAsync(context, sql, converter, cancellationToken);
    }

    private async Task<TResult> ExecuteScalarSqlAsync<TResult>(
        GraphContext context,
        GraphSqlQuery sql,
        Func<object?, TResult> converter,
        CancellationToken cancellationToken)
    {
        using var activity = GormDiagnostics.StartQueryActivity(GormDiagnostics.ActivityNames.QueryScalar, context, sql, typeof(TResult));
        var startedAt = Stopwatch.GetTimestamp();
        object? result;

        try
        {
            await using var lease = await GraphQueryCommandLease.OpenAsync(context, _connectionFactory, cancellationToken);
            await using var command = lease.CreateCommand(sql);
            result = await command.ExecuteScalarAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            GormDiagnostics.RecordQueryFailure(activity, startedAt, ex);
            throw CreateSqlExecutionException(sql, ex);
        }

        try
        {
            var converted = converter(result);
            GormDiagnostics.RecordQuerySuccess(activity, startedAt);
            return converted;
        }
        catch (Exception ex)
        {
            GormDiagnostics.RecordQueryFailure(activity, startedAt, ex);
            throw;
        }
    }

    private static GraphQueryModel Translate<T>(IQueryable<T> query)
    {
        if (query.Provider is not GraphQueryProvider)
        {
            throw new InvalidOperationException(
                $"The query provider must be '{typeof(GraphQueryProvider).FullName}'.");
        }

        return GraphQueryProvider.Translate(query.Expression);
    }

    private static TResult ConvertAggregateResult<TResult>(object result)
    {
        if (result is TResult typedResult)
        {
            return typedResult;
        }

        var targetType = Nullable.GetUnderlyingType(typeof(TResult)) ?? typeof(TResult);
        return (TResult)Convert.ChangeType(result, targetType, CultureInfo.InvariantCulture);
    }

    private static TResult GetSumDefault<TResult>()
    {
        var nullableType = Nullable.GetUnderlyingType(typeof(TResult));

        if (nullableType is not null)
        {
            return default!;
        }

        var zero = Convert.ChangeType(0, typeof(TResult), CultureInfo.InvariantCulture);
        return (TResult)zero;
    }

    private static InvalidOperationException CreateSqlExecutionException(GraphSqlQuery sql, Exception ex) =>
        new("Gorm SQL execution failed." + Environment.NewLine + sql.ToRedactedDebugString(), ex);
}