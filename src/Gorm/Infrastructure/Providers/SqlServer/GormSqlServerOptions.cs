namespace Gorm.Infrastructure.Providers.SqlServer;

/// <summary>
/// Represents gorm sql server options.
/// </summary>
public sealed class GormSqlServerOptions
{
    /// <summary>
    /// Gets empty.
    /// </summary>
    /// <returns>The value.</returns>
    public string ConnectionString { get; set; } = string.Empty;
}