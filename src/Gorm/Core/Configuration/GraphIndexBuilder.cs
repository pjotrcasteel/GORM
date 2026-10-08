using Gorm.Core.Metadata;

namespace Gorm.Core.Configuration;

/// <summary>
/// Represents graph index builder.
/// </summary>
public sealed class GraphIndexBuilder
{
    private readonly string[] _propertyNames;
    private string _databaseName;
    private bool _isUnique;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphIndexBuilder"/> class.
    /// </summary>
    /// <param name="defaultDatabaseName">The default database name.</param>
    /// <param name="propertyNames">The property names.</param>
    internal GraphIndexBuilder(string defaultDatabaseName, string[] propertyNames)
    {
        _databaseName = string.IsNullOrWhiteSpace(defaultDatabaseName)
            ? throw new ArgumentException("Default database name cannot be null or whitespace.", nameof(defaultDatabaseName))
            : defaultDatabaseName;

        _propertyNames = propertyNames ?? throw new ArgumentNullException(nameof(propertyNames));
    }

    /// <summary>
    /// Executes has database name.
    /// </summary>
    /// <param name="databaseName">The database name.</param>
    /// <returns>The builder.</returns>
    public GraphIndexBuilder HasDatabaseName(string databaseName)
    {
        _databaseName = string.IsNullOrWhiteSpace(databaseName) ? throw new ArgumentException("Database name cannot be null or whitespace.", nameof(databaseName)) : databaseName;

        return this;
    }

    /// <summary>
    /// Executes is unique.
    /// </summary>
    /// <returns>The builder.</returns>
    public GraphIndexBuilder IsUnique()
    {
        _isUnique = true;
        return this;
    }

    /// <summary>
    /// Builds the result.
    /// </summary>
    /// <returns>The value.</returns>
    internal GraphIndexMapping Build() => new()
    {
        DatabaseName = _databaseName,
        PropertyNames = _propertyNames,
        IsUnique = _isUnique
    };
}