using Gorm.Core.Models;
using Gorm.Infrastructure.Persistence.Connections;
using Gorm.Infrastructure.Providers.Abstractions;

namespace Gorm.Infrastructure.Persistence.Schema;

/// <summary>
/// Legacy database creator. GORM is database-first and does not mutate database schema.
/// </summary>
[Obsolete(
    "GORM is database-first. Use GraphContext.GenerateCreateScript() for review and ValidateSchemaAsync() for drift checks instead. This type no longer executes schema changes."
)]
public sealed class GraphDatabaseCreator
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphDatabaseCreator"/> class.
    /// </summary>
    /// <param name="connectionFactory">The connection factory.</param>
    /// <param name="schemaGenerator">The schema generator.</param>
    public GraphDatabaseCreator(IGormDbConnectionFactory connectionFactory, IGraphSchemaGenerator schemaGenerator)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(schemaGenerator);
    }

    /// <summary>
    /// Throws because GORM is database-first and does not create schema.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that always throws.</returns>
#pragma warning disable IDE0060 // Remove unused parameter
    public Task EnsureCreatedAsync(GraphModel model, CancellationToken cancellationToken = default)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        ArgumentNullException.ThrowIfNull(model);
        throw new NotSupportedException(
            "GORM is database-first and does not create or migrate databases. " +
            "Provision SQL Server Graph objects through your normal database deployment process and use ValidateSchemaAsync() to detect drift.");
    }
}