namespace Gorm.Infrastructure.Providers.Abstractions;

/// <summary>
/// Defines i graph provider.
/// </summary>
public interface IGraphProvider
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets the query sql generator.
    /// </summary>
    public IGraphQuerySqlGenerator QuerySqlGenerator { get; }

    /// <summary>
    /// Gets or sets the schema generator.
    /// </summary>
    public IGraphSchemaGenerator SchemaGenerator { get; }
}