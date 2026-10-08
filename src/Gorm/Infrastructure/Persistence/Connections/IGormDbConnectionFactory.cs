using System.Data.Common;

namespace Gorm.Infrastructure.Persistence.Connections;

/// <summary>
/// Defines i gorm db connection factory.
/// </summary>
public interface IGormDbConnectionFactory
{
    /// <summary>
    /// Creates the item.
    /// </summary>
    /// <returns>The value.</returns>
    public DbConnection CreateConnection();
}