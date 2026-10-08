using System.Data.Common;
using Gorm.Application.Context;
using Gorm.Core.Metadata;
using Gorm.Infrastructure.Providers.SqlServer;
using Gorm.Infrastructure.Sql;

namespace Gorm.Application.Diagnostics;

/// <summary>
/// Represents graph schema validator.
/// </summary>
public static class GraphSchemaValidator
{
    /// <summary>
    /// Validates the graph schema.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<GraphSchemaValidationReport> ValidateAsync(GraphContext context, CancellationToken cancellationToken = default) =>
        ValidateAsync(context, new GraphSchemaValidationOptions(), cancellationToken);

    /// <summary>
    /// Validates the graph schema.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="options">The validation options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static async Task<GraphSchemaValidationReport> ValidateAsync(GraphContext context, GraphSchemaValidationOptions options, CancellationToken cancellationToken = default)
    {
        if (context.ConnectionFactory is null)
        {
            throw new InvalidOperationException("GraphContext.ConnectionFactory is required for schema validation.");
        }

        await using var connection = context.ConnectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var issues = new List<GraphSchemaValidationIssue>();

        foreach (var node in context.Model.Nodes)
        {
            await ValidateNodeAsync(connection, node, options, issues, cancellationToken).ConfigureAwait(false);
        }

        foreach (var edge in context.Model.Edges)
        {
            await ValidateEdgeAsync(connection, edge, options, issues, cancellationToken).ConfigureAwait(false);
        }

        return new GraphSchemaValidationReport
        {
            Issues = issues
        };
    }

    private static async Task ValidateNodeAsync(
        DbConnection connection,
        NodeTypeMapping node,
        GraphSchemaValidationOptions options,
        List<GraphSchemaValidationIssue> issues,
        CancellationToken cancellationToken)
    {
        var table = await LoadTableAsync(connection, node.Schema, node.TableName, cancellationToken).ConfigureAwait(false);
        if (table is null)
        {
            issues.Add(MissingTable("Schema.Table.Missing", node.Schema, node.TableName));
            return;
        }

        if (!table.IsNode)
        {
            issues.Add(new GraphSchemaValidationIssue
            {
                Code = "Schema.Table.ExpectedNode",
                Message = $"Table '{node.Schema}.{node.TableName}' exists but is not marked as a SQL Server graph NODE table.",
                Schema = node.Schema,
                Table = node.TableName,
                Expected = "NODE",
                Actual = table.IsEdge ? "EDGE" : "REGULAR"
            });
        }

        ValidateColumns(
            new ValidateColumnsParameters
            {
                Properties = GetExpectedProperties(node.ClrType, node.KeyPropertyName, node.Properties),
                ExistingColumns = table.Columns,
                Schema = node.Schema,
                Table = node.TableName,
                KeyPropertyName = node.KeyPropertyName,
                Options = options,
                Issues = issues
            });

        if (options.ValidateIndexes)
        {
            await ValidateIndexesAsync(connection, node.Schema, node.TableName, node.Indexes, issues, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task ValidateEdgeAsync(
        DbConnection connection,
        EdgeTypeMapping edge,
        GraphSchemaValidationOptions options,
        List<GraphSchemaValidationIssue> issues,
        CancellationToken cancellationToken)
    {
        var table = await LoadTableAsync(connection, edge.Schema, edge.TableName, cancellationToken).ConfigureAwait(false);
        if (table is null)
        {
            issues.Add(MissingTable("Schema.Table.Missing", edge.Schema, edge.TableName));
            return;
        }

        if (!table.IsEdge)
        {
            issues.Add(new GraphSchemaValidationIssue
            {
                Code = "Schema.Table.ExpectedEdge",
                Message = $"Table '{edge.Schema}.{edge.TableName}' exists but is not marked as a SQL Server graph EDGE table.",
                Schema = edge.Schema,
                Table = edge.TableName,
                Expected = "EDGE",
                Actual = table.IsNode ? "NODE" : "REGULAR"
            });
        }

        ValidateColumns(
            new ValidateColumnsParameters
            {
                Properties = GetExpectedProperties(edge.ClrType, edge.KeyPropertyName, edge.Properties),
                ExistingColumns = table.Columns,
                Schema = edge.Schema,
                Table = edge.TableName,
                KeyPropertyName = edge.KeyPropertyName,
                Options = options,
                Issues = issues
            });

        if (options.ValidateIndexes)
        {
            await ValidateIndexesAsync(connection, edge.Schema, edge.TableName, edge.Indexes, issues, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void ValidateColumns(ValidateColumnsParameters inputs)
    {
        var properties = inputs.Properties;
        var existingColumns = inputs.ExistingColumns;
        var schema = inputs.Schema;
        var table = inputs.Table;
        var keyPropertyName = inputs.KeyPropertyName;
        var options = inputs.Options;
        var issues = inputs.Issues;

        foreach (var property in properties)
        {
            if (!TryValidateColumnExists(property, existingColumns, schema, table, issues, out var column))
            {
                continue;
            }

            ValidateColumnType(property, column, schema, table, options, issues);
            ValidateColumnNullability(
                new ValidateColumnNullabilityParameters
                {
                    Property = property,
                    Column = column,
                    Schema = schema,
                    Table = table,
                    KeyPropertyName = keyPropertyName,
                    Options = options,
                    Issues = issues
                });
        }
    }

    private static bool TryValidateColumnExists(
        PropertyMapping property,
        IReadOnlyDictionary<string, ColumnInfo> existingColumns,
        string schema,
        string table,
        List<GraphSchemaValidationIssue> issues,
        out ColumnInfo column)
    {
        if (existingColumns.TryGetValue(property.PropertyName, out column!))
        {
            return true;
        }

        issues.Add(new GraphSchemaValidationIssue
        {
            Code = "Schema.Column.Missing",
            Message = $"Column '{property.PropertyName}' is missing from '{schema}.{table}'.",
            Schema = schema,
            Table = table,
            Column = property.PropertyName,
            Expected = SqlServerGraphTypeMapping.GetStoreTypeName(property),
            Actual = null
        });

        return false;
    }

    private static void ValidateColumnType(
        PropertyMapping property,
        ColumnInfo column,
        string schema,
        string table,
        GraphSchemaValidationOptions options,
        List<GraphSchemaValidationIssue> issues)
    {
        if (!options.ValidateColumnTypes || !column.HasTypeMetadata)
        {
            return;
        }

        var expectedType = SqlServerGraphTypeMapping.GetStoreType(property);
        var actualType = column.ToStoreType();

        if (SqlServerGraphTypeMapping.IsCompatible(expectedType, actualType))
        {
            return;
        }

        issues.Add(new GraphSchemaValidationIssue
        {
            Code = "Schema.Column.TypeMismatch",
            Message = $"Column '{property.PropertyName}' on '{schema}.{table}' has a different store type than the GORM model expects.",
            Schema = schema,
            Table = table,
            Column = property.PropertyName,
            Expected = expectedType.ToString(),
            Actual = actualType.ToString()
        });
    }

    private static void ValidateColumnNullability(ValidateColumnNullabilityParameters inputs)
    {
        var property = inputs.Property;
        var column = inputs.Column;
        var schema = inputs.Schema;
        var table = inputs.Table;
        var keyPropertyName = inputs.KeyPropertyName;
        var options = inputs.Options;
        var issues = inputs.Issues;

        if (!options.ValidateColumnNullability || column.IsNullable is null)
        {
            return;
        }

        var expectedNullable = IsColumnExpectedNullable(property, keyPropertyName);

        if (expectedNullable == column.IsNullable.Value)
        {
            return;
        }

        issues.Add(new GraphSchemaValidationIssue
        {
            Code = "Schema.Column.NullabilityMismatch",
            Message = $"Column '{property.PropertyName}' on '{schema}.{table}' has different nullability than the GORM model expects.",
            Schema = schema,
            Table = table,
            Column = property.PropertyName,
            Expected = expectedNullable ? "NULL" : "NOT NULL",
            Actual = column.IsNullable.Value ? "NULL" : "NOT NULL"
        });
    }

    private static bool IsColumnExpectedNullable(PropertyMapping property, string keyPropertyName)
    {
        return !property.IsRequired && !string.Equals(property.PropertyName, keyPropertyName, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task ValidateIndexesAsync(
        DbConnection connection,
        string schema,
        string table,
        IReadOnlyList<GraphIndexMapping> expectedIndexes,
        List<GraphSchemaValidationIssue> issues,
        CancellationToken cancellationToken)
    {
        if (expectedIndexes.Count == 0)
        {
            return;
        }

        var actualIndexes = await LoadIndexesAsync(connection, schema, table, cancellationToken).ConfigureAwait(false);

        foreach (var expectedIndex in expectedIndexes)
        {
            if (!actualIndexes.TryGetValue(expectedIndex.DatabaseName, out var actualIndex))
            {
                issues.Add(new GraphSchemaValidationIssue
                {
                    Code = "Schema.Index.Missing",
                    Message = $"Index '{expectedIndex.DatabaseName}' is missing from '{schema}.{table}'.",
                    Schema = schema,
                    Table = table,
                    Index = expectedIndex.DatabaseName,
                    Expected = string.Join(", ", expectedIndex.PropertyNames),
                    Actual = null
                });

                continue;
            }

            if (expectedIndex.IsUnique != actualIndex.IsUnique)
            {
                issues.Add(new GraphSchemaValidationIssue
                {
                    Code = "Schema.Index.UniquenessMismatch",
                    Message = $"Index '{expectedIndex.DatabaseName}' on '{schema}.{table}' has different uniqueness than the GORM model expects.",
                    Schema = schema,
                    Table = table,
                    Index = expectedIndex.DatabaseName,
                    Expected = expectedIndex.IsUnique ? "UNIQUE" : "NON_UNIQUE",
                    Actual = actualIndex.IsUnique ? "UNIQUE" : "NON_UNIQUE"
                });
            }

            if (!expectedIndex.PropertyNames.SequenceEqual(actualIndex.ColumnNames, StringComparer.OrdinalIgnoreCase))
            {
                issues.Add(new GraphSchemaValidationIssue
                {
                    Code = "Schema.Index.ColumnsMismatch",
                    Message = $"Index '{expectedIndex.DatabaseName}' on '{schema}.{table}' has different columns than the GORM model expects.",
                    Schema = schema,
                    Table = table,
                    Index = expectedIndex.DatabaseName,
                    Expected = string.Join(", ", expectedIndex.PropertyNames),
                    Actual = string.Join(", ", actualIndex.ColumnNames)
                });
            }
        }
    }

    private static List<PropertyMapping> GetExpectedProperties(Type clrType, string keyPropertyName, IReadOnlyList<PropertyMapping> properties)
    {
        var result = new List<PropertyMapping>();
        var keyProperty = properties.FirstOrDefault(x => string.Equals(x.PropertyName, keyPropertyName, StringComparison.OrdinalIgnoreCase));

        if (keyProperty is null)
        {
            var propertyInfo = clrType.GetProperty(keyPropertyName) ?? throw new InvalidOperationException(
                $"Key property '{keyPropertyName}' was not found on '{clrType.FullName}'.");

            keyProperty = new PropertyMapping
            {
                PropertyName = propertyInfo.Name,
                PropertyType = propertyInfo.PropertyType,
                IsRequired = true
            };
        }

        result.Add(keyProperty);

        foreach (var property in properties)
        {
            if (string.Equals(property.PropertyName, keyPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(property);
        }

        return result;
    }

    private static GraphSchemaValidationIssue MissingTable(string code, string schema, string table) => new()
    {
        Code = code,
        Message = $"Table '{schema}.{table}' is missing from the database.",
        Schema = schema,
        Table = table
    };

    private static async Task<TableInfo?> LoadTableAsync(DbConnection connection, string schema, string table, CancellationToken cancellationToken)
    {
        bool isNode;
        bool isEdge;

        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT t.is_node, t.is_edge
                FROM sys.tables t
                INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
                WHERE s.name = @schema AND t.name = @table;
                """;

            AddParameter(command, "@schema", schema);
            AddParameter(command, "@table", table);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            isNode = reader.GetBoolean(0);
            isEdge = reader.GetBoolean(1);
        }

        var columns = await LoadColumnsAsync(connection, schema, table, cancellationToken).ConfigureAwait(false);

        return new TableInfo(isNode, isEdge, columns);
    }

    private static async Task<Dictionary<string, ColumnInfo>> LoadColumnsAsync(DbConnection connection, string schema, string table, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE, IS_NULLABLE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table;
            """;

        AddParameter(command, "@schema", schema);
        AddParameter(command, "@table", table);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var columns = new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var column = ColumnInfo.FromReader(reader);
            columns[column.Name] = column;
        }

        return columns;
    }

    private static async Task<Dictionary<string, IndexInfo>> LoadIndexesAsync(DbConnection connection, string schema, string table, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT i.name, i.is_unique, c.name AS column_name, ic.key_ordinal
            FROM sys.indexes i
            INNER JOIN sys.tables t ON t.object_id = i.object_id
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            INNER JOIN sys.columns c ON c.object_id = i.object_id AND c.column_id = ic.column_id
            WHERE s.name = @schema AND t.name = @table AND i.is_hypothetical = 0
            ORDER BY i.name, ic.key_ordinal;
            """;

        AddParameter(command, "@schema", schema);
        AddParameter(command, "@table", table);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var indexes = new Dictionary<string, MutableIndexInfo>(StringComparer.OrdinalIgnoreCase);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var name = reader.GetString(0);
            var isUnique = reader.GetBoolean(1);
            var columnName = reader.GetString(2);

            if (!indexes.TryGetValue(name, out var index))
            {
                index = new MutableIndexInfo(name, isUnique);
                indexes.Add(name, index);
            }

            index.ColumnNames.Add(columnName);
        }

        return indexes.ToDictionary(x => x.Key, x => new IndexInfo(x.Value.Name, x.Value.IsUnique, x.Value.ColumnNames), StringComparer.OrdinalIgnoreCase);
    }

    private static void AddParameter(DbCommand command, string name, object? value) =>
        GraphSqlCommandFactory.AddParameter(command, name, value);

    private sealed record TableInfo(bool IsNode, bool IsEdge, IReadOnlyDictionary<string, ColumnInfo> Columns);

    private sealed class ColumnInfo
    {
        public required string Name { get; init; }
        public string? DataType { get; init; }
        public int? MaxLength { get; init; }
        public int? Precision { get; init; }
        public int? Scale { get; init; }
        public bool? IsNullable { get; init; }
        public bool HasTypeMetadata => !string.IsNullOrWhiteSpace(DataType);

        public SqlServerStoreType ToStoreType() => new()
        {
            DataType = DataType ?? string.Empty,
            MaxLength = MaxLength,
            Precision = Precision,
            Scale = Scale
        };

        public static ColumnInfo FromReader(DbDataReader reader)
        {
            var name = reader.GetString(0);

            return new ColumnInfo
            {
                Name = name,
                DataType = ReadString(reader, "DATA_TYPE"),
                MaxLength = ReadInt32(reader, "CHARACTER_MAXIMUM_LENGTH"),
                Precision = ReadInt32(reader, "NUMERIC_PRECISION"),
                Scale = ReadInt32(reader, "NUMERIC_SCALE"),
                IsNullable = ReadString(reader, "IS_NULLABLE") switch
                {
                    "YES" => true,
                    "NO" => false,
                    _ => null
                }
            };
        }

        private static string? ReadString(DbDataReader reader, string name)
        {
            var ordinal = TryGetOrdinal(reader, name);
            return ordinal is null || reader.IsDBNull(ordinal.Value) ? null : reader.GetValue(ordinal.Value).ToString();
        }

        private static int? ReadInt32(DbDataReader reader, string name)
        {
            var ordinal = TryGetOrdinal(reader, name);
            if (ordinal is null || reader.IsDBNull(ordinal.Value))
            {
                return null;
            }

            return Convert.ToInt32(reader.GetValue(ordinal.Value), System.Globalization.CultureInfo.InvariantCulture);
        }

        private static int? TryGetOrdinal(DbDataReader reader, string name)
        {
            try
            {
                var ordinal = reader.GetOrdinal(name);
                return ordinal < 0 ? null : ordinal;
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
        }
    }

    private sealed record IndexInfo(string Name, bool IsUnique, IReadOnlyList<string> ColumnNames);

    private sealed record MutableIndexInfo(string Name, bool IsUnique)
    {
        public List<string> ColumnNames { get; } = [];
    }

    private sealed class ValidateColumnsParameters
    {
        public required IReadOnlyList<PropertyMapping> Properties { get; init; }

        public required IReadOnlyDictionary<string, ColumnInfo> ExistingColumns { get; init; }

        public required string Schema { get; init; }

        public required string Table { get; init; }

        public required string KeyPropertyName { get; init; }

        public required GraphSchemaValidationOptions Options { get; init; }

        public required List<GraphSchemaValidationIssue> Issues { get; init; }
    }

    private sealed class ValidateColumnNullabilityParameters
    {
        public required PropertyMapping Property { get; init; }

        public required ColumnInfo Column { get; init; }

        public required string Schema { get; init; }

        public required string Table { get; init; }

        public required string KeyPropertyName { get; init; }

        public required GraphSchemaValidationOptions Options { get; init; }

        public required List<GraphSchemaValidationIssue> Issues { get; init; }
    }
}