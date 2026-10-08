using System.Diagnostics.CodeAnalysis;
using Gorm.Core.Metadata;

namespace Gorm.Infrastructure.Providers.SqlServer;

/// <summary>
/// Maps CLR graph property types to SQL Server store types.
/// </summary>
public static class SqlServerGraphTypeMapping
{
    private static readonly Dictionary<Type, SqlServerStoreType> StoreTypes = new()
    {
        [typeof(Guid)] = new SqlServerStoreType { DataType = "uniqueidentifier" },
        [typeof(int)] = new SqlServerStoreType { DataType = "int" },
        [typeof(long)] = new SqlServerStoreType { DataType = "bigint" },
        [typeof(short)] = new SqlServerStoreType { DataType = "smallint" },
        [typeof(byte)] = new SqlServerStoreType { DataType = "tinyint" },
        [typeof(bool)] = new SqlServerStoreType { DataType = "bit" },
        [typeof(DateTime)] = new SqlServerStoreType { DataType = "datetime2" },
        [typeof(DateTimeOffset)] = new SqlServerStoreType { DataType = "datetimeoffset" },
        [typeof(DateOnly)] = new SqlServerStoreType { DataType = "date" },
        [typeof(TimeOnly)] = new SqlServerStoreType { DataType = "time" },
        [typeof(TimeSpan)] = new SqlServerStoreType { DataType = "time" },
        [typeof(decimal)] = new SqlServerStoreType { DataType = "decimal", Precision = 18, Scale = 2 },
        [typeof(double)] = new SqlServerStoreType { DataType = "float" },
        [typeof(float)] = new SqlServerStoreType { DataType = "real" },
        [typeof(byte[])] = new SqlServerStoreType { DataType = "varbinary", MaxLength = -1 }
    };

    /// <summary>
    /// Gets the SQL Server store type for a mapped graph property.
    /// </summary>
    /// <param name="property">The property mapping.</param>
    /// <returns>The store type.</returns>
    public static SqlServerStoreType GetStoreType(PropertyMapping property)
    {
        ArgumentNullException.ThrowIfNull(property);

        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (type == typeof(string))
        {
            return new SqlServerStoreType
            {
                DataType = "nvarchar",
                MaxLength = property.MaxLength ?? -1
            };
        }

        if (type.IsEnum)
        {
            return new SqlServerStoreType { DataType = "int" };
        }

        return TryGetKnownStoreType(type, out var storeType)
            ? storeType
            : throw new NotSupportedException($"No SQL store type mapping exists for CLR type '{property.PropertyType.FullName}'.");
    }

    /// <summary>
    /// Gets the SQL Server store type text for a mapped graph property.
    /// </summary>
    /// <param name="property">The property mapping.</param>
    /// <returns>The store type text.</returns>
    public static string GetStoreTypeName(PropertyMapping property) => GetStoreType(property).ToString();

    /// <summary>
    /// Compares two SQL Server store type descriptors using GORM's supported type semantics.
    /// </summary>
    /// <param name="expected">The expected store type.</param>
    /// <param name="actual">The actual store type.</param>
    /// <returns>True when the actual type is compatible with the expected type.</returns>
    public static bool IsCompatible(SqlServerStoreType expected, SqlServerStoreType actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        if (!string.Equals(expected.DataType, actual.DataType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (expected.MaxLength is not null && actual.MaxLength is not null && expected.MaxLength != actual.MaxLength)
        {
            return false;
        }

        if (expected.Precision is not null && actual.Precision is not null && expected.Precision != actual.Precision)
        {
            return false;
        }

        if (expected.Scale is not null && actual.Scale is not null && expected.Scale != actual.Scale)
        {
            return false;
        }

        return true;
    }

    private static bool TryGetKnownStoreType(Type type, [NotNullWhen(true)] out SqlServerStoreType? storeType)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (StoreTypes.TryGetValue(type, out var mappedStoreType))
        {
            storeType = mappedStoreType;
            return true;
        }

        storeType = null;
        return false;
    }
}