using Gorm.Application.History.Envelopes;

namespace Gorm.Application.History.Storage;

/// <summary>
/// Represents an in-memory store for graph history envelopes.
/// </summary>
public sealed class InMemoryGraphHistoryStore
{
    private readonly List<GraphHistoryEnvelope> _entries = [];

    /// <summary>
    /// Gets all history entries.
    /// </summary>
    public IReadOnlyList<GraphHistoryEnvelope> Entries => _entries;

    /// <summary>
    /// Adds a history entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    public void Add(GraphHistoryEnvelope entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _entries.Add(entry);
    }

    /// <summary>
    /// Adds multiple history entries.
    /// </summary>
    /// <param name="entries">The entries.</param>
    public void AddRange(IEnumerable<GraphHistoryEnvelope> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        _entries.AddRange(entries);
    }

    /// <summary>
    /// Gets history entries for the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <returns>The matching history entries.</returns>
    public IReadOnlyList<GraphHistoryEnvelope> Get<TEntity>() =>
        [.. _entries.Where(x => x.EntityType == typeof(TEntity))];

    /// <summary>
    /// Gets history entries for the specified entity type and identifier.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entityId">The entity identifier.</param>
    /// <returns>The matching history entries.</returns>
    public IReadOnlyList<GraphHistoryEnvelope> Get<TEntity>(Guid entityId) =>
        [.. _entries.Where(x => x.EntityType == typeof(TEntity) && x.EntityId == entityId)];

    /// <summary>
    /// Clears the store.
    /// </summary>
    public void Clear() =>
        _entries.Clear();
}