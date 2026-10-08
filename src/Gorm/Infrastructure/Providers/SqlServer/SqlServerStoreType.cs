namespace Gorm.Infrastructure.Providers.SqlServer;

/// <summary>
/// Represents normalized SQL Server store type metadata.
/// </summary>
public sealed class SqlServerStoreType
{
    /// <summary>
    /// Gets or sets the SQL Server data type name.
    /// </summary>
    public required string DataType { get; init; }

    /// <summary>
    /// Gets or sets the character or binary maximum length. -1 means max.
    /// </summary>
    public int? MaxLength { get; init; }

    /// <summary>
    /// Gets or sets the numeric precision.
    /// </summary>
    public int? Precision { get; init; }

    /// <summary>
    /// Gets or sets the numeric scale.
    /// </summary>
    public int? Scale { get; init; }

    /// <summary>
    /// Gets the display name.
    /// </summary>
    /// <returns>The store type display name.</returns>
    public override string ToString()
    {
        if (MaxLength is not null)
        {
            return MaxLength == -1 ? $"{DataType}(max)" : $"{DataType}({MaxLength})";
        }

        if (Precision is not null && Scale is not null)
        {
            return $"{DataType}({Precision},{Scale})";
        }

        return DataType;
    }
}