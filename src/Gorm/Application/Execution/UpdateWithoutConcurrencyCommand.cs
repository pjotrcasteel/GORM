using System.Data.Common;
using System.Reflection;

namespace Gorm.Application.Execution;

public sealed class UpdateWithoutConcurrencyCommand
{
    public required DbConnection Connection { get; init; }

    public required DbTransaction Transaction { get; init; }

    public required string Schema { get; init; }

    public required string TableName { get; init; }

    public required string KeyPropertyName { get; init; }

    public required object? KeyValue { get; init; }

    public required IReadOnlyList<PropertyInfo> UpdatableProperties { get; init; }

    public required object Entity { get; init; }
}