using System.Runtime.CompilerServices;
using Gorm.Infrastructure.Persistence.Connections;

namespace Gorm.Application.History.Storage;

/// <summary>
/// Provides SQL Server history readers for graph contexts.
/// </summary>
internal static class SqlServerGraphHistoryReaderRegistry
{
    private static readonly ConditionalWeakTable<object, SqlServerGraphHistoryReader> Readers = new();

    /// <summary>
    /// Registers a SQL Server history reader for the specified graph context.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="connectionFactory">The connection factory.</param>
    /// <param name="schemaName">The schema name.</param>
    public static void Register(object context, IGormDbConnectionFactory connectionFactory, string schemaName)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(connectionFactory);

        Readers.Remove(context);
        Readers.Add(context, new SqlServerGraphHistoryReader(connectionFactory, schemaName));
    }

    /// <summary>
    /// Gets the registered SQL Server history reader for the specified graph context.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="reader">The reader.</param>
    /// <returns>True when found; otherwise, false.</returns>
    public static bool TryGet(object context, out SqlServerGraphHistoryReader reader)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Readers.TryGetValue(context, out reader!);
    }
}