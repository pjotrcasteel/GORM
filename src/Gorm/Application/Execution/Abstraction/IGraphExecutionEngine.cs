using Gorm.Application.Context;
using Gorm.Application.Tracking;

namespace Gorm.Application.Execution.Abstraction;

/// <summary>
/// Represents an execution engine for graph queries and save operations.
/// </summary>
public interface IGraphExecutionEngine
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
    public Task<List<T>> ExecuteAsync<T>(GraphContext context, IQueryable<T> query, GraphQueryExecutionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query and materializes the results as a list.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<List<T>> ExecuteListAsync<T>(GraphContext context, IQueryable<T> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query and returns whether any results exist.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<bool> ExecuteAnyAsync<T>(GraphContext context, IQueryable<T> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query and returns the number of results.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<int> ExecuteCountAsync<T>(GraphContext context, IQueryable<T> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query and returns the long count of results.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<long> ExecuteLongCountAsync<T>(GraphContext context, IQueryable<T> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the pending changes in the change tracker.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="changeTracker">The change tracker.</param>
    /// <param name="acceptAllChangesOnSuccess">Indicates whether changes should be accepted after a successful save.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<int> SaveChangesAsync(GraphContext context, GraphChangeTracker changeTracker, bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default);
}