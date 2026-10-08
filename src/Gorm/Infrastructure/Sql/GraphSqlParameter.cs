using DataDbType = System.Data.DbType;

namespace Gorm.Infrastructure.Sql;

/// <summary>
/// Represents graph sql parameter.
/// </summary>
public sealed class GraphSqlParameter
{
    private static readonly Dictionary<Type, DataDbType> DbTypesByClrType = new()
    {
        [typeof(Guid)] = DataDbType.Guid,
        [typeof(int)] = DataDbType.Int32,
        [typeof(long)] = DataDbType.Int64,
        [typeof(short)] = DataDbType.Int16,
        [typeof(byte)] = DataDbType.Byte,
        [typeof(bool)] = DataDbType.Boolean,
        [typeof(DateTime)] = DataDbType.DateTime2,
        [typeof(DateTimeOffset)] = DataDbType.DateTimeOffset,
        [typeof(DateOnly)] = DataDbType.Date,
        [typeof(TimeOnly)] = DataDbType.Time,
        [typeof(TimeSpan)] = DataDbType.Time,
        [typeof(decimal)] = DataDbType.Decimal,
        [typeof(double)] = DataDbType.Double,
        [typeof(float)] = DataDbType.Single
    };

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public required object? Value { get; init; }

    /// <summary>
    /// Gets or sets the database type.
    /// </summary>
    public DataDbType? DbType { get; init; }

    /// <summary>
    /// Gets or sets the parameter size.
    /// </summary>
    public int? Size { get; init; }

    /// <summary>
    /// Creates a SQL parameter and infers stable metadata where safe.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The parameter value.</param>
    /// <returns>The parameter.</returns>
    public static GraphSqlParameter Create(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (value is null || value is DBNull)
        {
            return CreateUntyped(name, value);
        }

        if (value is string stringValue)
        {
            return CreateString(name, stringValue);
        }

        if (value is byte[] bytes)
        {
            return CreateBinary(name, bytes);
        }

        return DbTypesByClrType.TryGetValue(value.GetType(), out var dbType) ? CreateTyped(name, value, dbType) : CreateUntyped(name, value);
    }

    private static GraphSqlParameter CreateUntyped(string name, object? value) =>
        new()
        {
            Name = name,
            Value = value
        };

    private static GraphSqlParameter CreateTyped(string name, object value, DataDbType dbType) =>
        new()
        {
            Name = name,
            Value = value,
            DbType = dbType
        };

    private static GraphSqlParameter CreateString(string name, string value) =>
        new()
        {
            Name = name,
            Value = value,
            DbType = DataDbType.String,
            Size = value.Length <= 4000 ? Math.Max(value.Length, 1) : -1
        };

    private static GraphSqlParameter CreateBinary(string name, byte[] value) =>
        new()
        {
            Name = name,
            Value = value,
            DbType = DataDbType.Binary,
            Size = value.Length <= 8000 ? Math.Max(value.Length, 1) : -1
        };
}