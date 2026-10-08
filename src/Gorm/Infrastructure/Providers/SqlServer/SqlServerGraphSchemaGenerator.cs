using System.Reflection;
using System.Text;
using Gorm.Core.Metadata;
using Gorm.Core.Models;
using Gorm.Infrastructure.Providers.SqlServer.Helpers;

namespace Gorm.Infrastructure.Providers.SqlServer;

/// <summary>
/// Represents sql server graph schema generator.
/// </summary>
public static class SqlServerGraphSchemaGenerator
{
    /// <summary>
    /// Generates the expected SQL Server graph schema script.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The expected schema script.</returns>
    public static string GenerateCreateScript(GraphModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var builder = new StringBuilder();

        foreach (var node in model.Nodes)
        {
            AppendCreateNodeTable(builder, node);
            builder.AppendLine("GO");
            builder.AppendLine();
            AppendCreateIndexes(builder, node.Schema, node.TableName, node.Indexes);
        }

        foreach (var edge in model.Edges)
        {
            AppendCreateEdgeTable(builder, edge);
            builder.AppendLine("GO");
            builder.AppendLine();
            AppendCreateIndexes(builder, edge.Schema, edge.TableName, edge.Indexes);
        }

        return builder.ToString().Trim();
    }

    private static void AppendCreateNodeTable(StringBuilder builder, NodeTypeMapping mapping)
    {
        builder.Append("IF OBJECT_ID(N'");
        builder.Append(SqlGenerationHelpers.EscapeLiteral(SqlGenerationHelpers.EscapeFullName(mapping.Schema, mapping.TableName)));
        builder.AppendLine("', N'U') IS NULL");
        builder.AppendLine("BEGIN");
        builder.Append("    CREATE TABLE ");
        builder.Append(SqlGenerationHelpers.EscapeFullName(mapping.Schema, mapping.TableName));
        builder.AppendLine();
        builder.AppendLine("    (");

        AppendColumns(builder, mapping.ClrType, mapping.KeyPropertyName, mapping.Properties);

        builder.AppendLine("    ) AS NODE;");
        builder.AppendLine("END");
    }

    private static void AppendCreateEdgeTable(StringBuilder builder, EdgeTypeMapping mapping)
    {
        builder.Append("IF OBJECT_ID(N'");
        builder.Append(SqlGenerationHelpers.EscapeLiteral(SqlGenerationHelpers.EscapeFullName(mapping.Schema, mapping.TableName)));
        builder.AppendLine("', N'U') IS NULL");
        builder.AppendLine("BEGIN");
        builder.Append("    CREATE TABLE ");
        builder.Append(SqlGenerationHelpers.EscapeFullName(mapping.Schema, mapping.TableName));
        builder.AppendLine();
        builder.AppendLine("    (");

        AppendColumns(builder, mapping.ClrType, mapping.KeyPropertyName, mapping.Properties);

        builder.AppendLine("    ) AS EDGE;");
        builder.AppendLine("END");
    }

    private static void AppendCreateIndexes(StringBuilder builder, string schema, string tableName, IReadOnlyList<GraphIndexMapping> indexes)
    {
        foreach (var index in indexes)
        {
            builder.Append("IF NOT EXISTS (");
            builder.Append("SELECT 1 FROM sys.indexes WHERE name = N'");
            builder.Append(SqlGenerationHelpers.EscapeLiteral(index.DatabaseName));
            builder.Append("' AND object_id = OBJECT_ID(N'");
            builder.Append(SqlGenerationHelpers.EscapeLiteral(SqlGenerationHelpers.EscapeFullName(schema, tableName)));
            builder.AppendLine("'))");
            builder.AppendLine("BEGIN");
            builder.Append("    ");
            builder.Append(index.IsUnique ? "CREATE UNIQUE INDEX " : "CREATE INDEX ");
            builder.Append(SqlGenerationHelpers.Escape(index.DatabaseName));
            builder.Append(" ON ");
            builder.Append(SqlGenerationHelpers.EscapeFullName(schema, tableName));
            builder.Append(" (");
            builder.Append(string.Join(", ", index.PropertyNames.Select(SqlGenerationHelpers.Escape)));
            builder.AppendLine(");");
            builder.AppendLine("END");
            builder.AppendLine("GO");
            builder.AppendLine();
        }
    }

    private static void AppendColumns(StringBuilder builder, Type clrType, string keyPropertyName, IReadOnlyList<PropertyMapping> properties)
    {
        var allProperties = GetCreateProperties(clrType, keyPropertyName, properties);

        for (var i = 0; i < allProperties.Count; i++)
        {
            var property = allProperties[i];
            var isLast = i == allProperties.Count - 1;

            builder.Append("        ");
            builder.Append(SqlGenerationHelpers.Escape(property.PropertyName));
            builder.Append(' ');
            builder.Append(SqlServerGraphTypeMapping.GetStoreTypeName(property));

            if (string.Equals(property.PropertyName, keyPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                builder.Append(" NOT NULL PRIMARY KEY");
            }
            else
            {
                builder.Append(property.IsRequired ? " NOT NULL" : " NULL");
            }

            if (!isLast)
            {
                builder.Append(',');
            }

            builder.AppendLine();
        }
    }

    private static List<PropertyMapping> GetCreateProperties(Type clrType, string keyPropertyName, IReadOnlyList<PropertyMapping> properties)
    {
        var result = new List<PropertyMapping>();

        var keyProperty = properties.FirstOrDefault(x => string.Equals(x.PropertyName, keyPropertyName, StringComparison.OrdinalIgnoreCase));

        if (keyProperty is null)
        {
            var propertyInfo = clrType.GetProperty(keyPropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) ?? throw new InvalidOperationException(
                    $"Key property '{keyPropertyName}' was not found on '{clrType.FullName}'.");

            keyProperty = new PropertyMapping
            {
                PropertyName = propertyInfo.Name,
                PropertyType = propertyInfo.PropertyType,
                IsRequired = true,
                MaxLength = null
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
}