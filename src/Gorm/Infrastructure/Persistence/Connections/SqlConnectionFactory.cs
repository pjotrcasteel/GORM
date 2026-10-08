using System.Data.Common;
using Gorm.Infrastructure.Providers.SqlServer;
using Microsoft.Data.SqlClient;

namespace Gorm.Infrastructure.Persistence.Connections;

/// <summary>
/// Represents sql connection factory.
/// </summary>
public sealed class SqlConnectionFactory : IGormDbConnectionFactory
{
    private readonly GormSqlServerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlConnectionFactory"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    public SqlConnectionFactory(GormSqlServerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Creates the item.
    /// </summary>
    /// <returns>The value.</returns>
    public DbConnection CreateConnection()
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new InvalidOperationException("The SQL Server connection string is not configured.");
        }

        return new SqlConnection(_options.ConnectionString);
    }
}