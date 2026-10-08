using Gorm.Application.Context;
using Gorm.Application.Execution.Abstraction;
using Gorm.Application.Tracking;
using Gorm.Infrastructure.Persistence.Connections;

namespace Gorm.Application.Execution;

/// <summary>
/// Represents the default SQL Server backed graph execution engine.
/// </summary>
public sealed class SqlServerGraphExecutionEngine : IGraphExecutionEngine
{
    /// <summary>
    /// Executes the query using the specified execution request.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="query">The query to execute.</param>
    /// <param name="request">The execution request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<List<T>> ExecuteAsync<T>(GraphContext context, IQueryable<T> query, GraphQueryExecutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(request);

        return CreateQueryExecutor(context).ExecuteAsync(context, query, request, cancellationToken);
    }

    /// <summary>
    /// Executes the query and materializes the results as a list.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<List<T>> ExecuteListAsync<T>(GraphContext context, IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);

        return ExecuteAsync(context, query, new GraphQueryExecutionRequest { Mode = GraphQueryExecutionMode.List }, cancellationToken);
    }

    /// <summary>
    /// Executes the query and returns whether any results exist.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<bool> ExecuteAnyAsync<T>(GraphContext context, IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);

        return CreateQueryExecutor(context).ExecuteAnyAsync(context, query, cancellationToken);
    }

    /// <summary>
    /// Executes the query and returns the number of results.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<int> ExecuteCountAsync<T>(GraphContext context, IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);

        return CreateQueryExecutor(context).ExecuteCountAsync(context, query, cancellationToken);
    }

    /// <summary>
    /// Executes the query and returns the long count of results.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<long> ExecuteLongCountAsync<T>(GraphContext context, IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);

        return CreateQueryExecutor(context).ExecuteLongCountAsync(context, query, cancellationToken);
    }

    /// <summary>
    /// Executes the pending changes in the change tracker.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="changeTracker">The change tracker.</param>
    /// <param name="acceptAllChangesOnSuccess">Indicates whether changes should be accepted after a successful save.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<int> SaveChangesAsync(GraphContext context, GraphChangeTracker changeTracker, bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(changeTracker);

        var connectionFactory = GetConnectionFactory(context);

        return context.SaveChangesAsync(connectionFactory, acceptAllChangesOnSuccess, cancellationToken);
    }

    private static GraphQueryExecutor CreateQueryExecutor(GraphContext context)
    {
        var connectionFactory = GetConnectionFactory(context);
        return new GraphQueryExecutor(connectionFactory, context.Provider.QuerySqlGenerator);
    }

    private static IGormDbConnectionFactory GetConnectionFactory(GraphContext context)
    {
        if (context.ConnectionFactory is null)
        {
            throw new InvalidOperationException(
                $"No {nameof(IGormDbConnectionFactory)} is configured on the current {nameof(GraphContext)}. " +
                $"Call {nameof(GraphContext.UseConnectionFactory)}(...) first or construct the {nameof(context)} with a connection factory.");
        }

        return context.ConnectionFactory;
    }
}