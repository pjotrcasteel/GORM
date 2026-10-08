using Gorm.Application.Context;
using Gorm.Application.History.Recording;
using Gorm.Application.History.Storage;

namespace Gorm.Application.Execution.InMemory;

/// <summary>
/// Represents graph context in-memory extensions.
/// </summary>
public static class GraphContextInMemoryExtensions
{
    /// <summary>
    /// Configures the graph context to use the provided in-memory graph store.
    /// </summary>
    /// <typeparam name="TContext">The graph context type.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="store">The in-memory graph store.</param>
    /// <returns>The configured graph context.</returns>
    public static TContext UseInMemory<TContext>(this TContext context, InMemoryGraphStore store)
        where TContext : GraphContext
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(store);

        context.UseExecutionEngine(new InMemoryGraphExecutionEngine(store));
        return context;
    }

    /// <summary>
    /// Configures the graph context to use the provided in-memory graph store and history store.
    /// </summary>
    /// <typeparam name="TContext">The graph context type.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="store">The in-memory graph store.</param>
    /// <param name="historyStore">The in-memory history store.</param>
    /// <returns>The configured graph context.</returns>
    public static TContext UseInMemory<TContext>(this TContext context, InMemoryGraphStore store, InMemoryGraphHistoryStore historyStore)
        where TContext : GraphContext
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(historyStore);

        context.UseExecutionEngine(new InMemoryGraphExecutionEngine(store));
        context.UseHistoryRecorder(new InMemoryGraphHistoryRecorder(historyStore));

        return context;
    }

    /// <summary>
    /// Configures the graph context to use a new in-memory graph store.
    /// </summary>
    /// <typeparam name="TContext">The graph context type.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <returns>The configured graph context.</returns>
    public static TContext UseInMemory<TContext>(this TContext context)
        where TContext : GraphContext
    {
        ArgumentNullException.ThrowIfNull(context);

        context.UseExecutionEngine(new InMemoryGraphExecutionEngine(new InMemoryGraphStore()));
        return context;
    }
}