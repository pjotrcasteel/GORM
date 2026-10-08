using Gorm.Application.History.Storage;
using Gorm.Infrastructure.Persistence.Connections;
using Gorm.Infrastructure.Providers.SqlServer;

namespace Gorm.Application.Context;

/// <summary>
/// SQL Server history extensions for graph contexts.
/// </summary>
public static class GraphContextSqlServerHistoryExtensions
{
    /// <summary>
    /// Configures SQL Server generic history persistence on the graph context.
    /// </summary>
    /// <typeparam name="TContext">The graph context type.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="connectionFactory">The connection factory.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <returns>The configured graph context.</returns>
    public static TContext UseSqlServerHistory<TContext>(this TContext context, IGormDbConnectionFactory connectionFactory, string schemaName = "dbo")
        where TContext : GraphContext
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(connectionFactory);

        context.UseHistoryRecorder(new SqlServerGraphHistoryRecorder(connectionFactory, schemaName));
        SqlServerGraphHistoryReaderRegistry.Register(context, connectionFactory, schemaName);

        return context;
    }
}