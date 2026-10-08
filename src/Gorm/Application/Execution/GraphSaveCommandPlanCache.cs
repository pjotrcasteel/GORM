using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Gorm.Core.Metadata;
using Gorm.Core.Primitives;
using Gorm.Infrastructure.Providers.SqlServer.Helpers;

namespace Gorm.Application.Execution;

/// <summary>
/// Caches SQL command text and compiled property accessors used by SaveChanges.
/// </summary>
internal static class GraphSaveCommandPlanCache
{
    private const string ConcurrencyPropertyName = "Version";
    private const string ParameterTarget = " = @p";
    private static readonly ConcurrentDictionary<SavePlanKey, GraphNodeSaveCommandPlan> NodePlans = [];
    private static readonly ConcurrentDictionary<SavePlanKey, GraphEdgeSaveCommandPlan> EdgePlans = [];

    public static GraphNodeSaveCommandPlan GetNodePlan(NodeTypeMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        return NodePlans.GetOrAdd(CreateKey(mapping.ClrType, mapping.Schema, mapping.TableName, mapping.KeyPropertyName, mapping.Properties), _ => CreateNodePlan(mapping));
    }

    public static GraphEdgeSaveCommandPlan GetEdgePlan(EdgeTypeMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        return EdgePlans.GetOrAdd(CreateKey(mapping.ClrType, mapping.Schema, mapping.TableName, mapping.KeyPropertyName, mapping.Properties), _ => CreateEdgePlan(mapping));
    }

    private static GraphNodeSaveCommandPlan CreateNodePlan(NodeTypeMapping mapping)
    {
        var keyProperty = CreatePropertyPlan(mapping.ClrType, mapping.KeyPropertyName);
        var insertProperties = CreateInsertProperties(mapping.ClrType, mapping.KeyPropertyName, mapping.Properties);
        var updatableProperties = CreateUpdatableProperties(mapping.ClrType, mapping.KeyPropertyName, mapping.Properties);
        var concurrencyProperty = CreateConcurrencyProperty(mapping.ClrType);
        var nonConcurrencyUpdatableProperties = RemoveConcurrencyProperty(updatableProperties, concurrencyProperty?.Name);

        return new GraphNodeSaveCommandPlan
        {
            ClrType = mapping.ClrType,
            KeyPropertyName = mapping.KeyPropertyName,
            KeyProperty = keyProperty,
            InsertProperties = insertProperties,
            UpdatableProperties = updatableProperties,
            NonConcurrencyUpdatableProperties = nonConcurrencyUpdatableProperties,
            ConcurrencyProperty = concurrencyProperty,
            InsertParameterNames = CreateParameterNames(insertProperties.Length),
            UpdateWithoutConcurrencyParameterNames = CreateParameterNames(updatableProperties.Length + 1),
            UpdateWithConcurrencyParameterNames = CreateParameterNames(nonConcurrencyUpdatableProperties.Length + 3),
            DeleteWithoutConcurrencyParameterNames = CreateParameterNames(1),
            DeleteWithConcurrencyParameterNames = CreateParameterNames(2),
            LoadNodeIdParameterNames = CreateParameterNames(1),
            InsertCommandText = BuildInsertSql(mapping.Schema, mapping.TableName, insertProperties),
            UpdateWithoutConcurrencyCommandText = BuildUpdateWithoutConcurrencySql(mapping.Schema, mapping.TableName, mapping.KeyPropertyName, updatableProperties),
            UpdateWithConcurrencyCommandText = concurrencyProperty is null
                ? null
                : BuildUpdateWithConcurrencySql(mapping.Schema, mapping.TableName, mapping.KeyPropertyName, nonConcurrencyUpdatableProperties, concurrencyProperty),
            DeleteWithoutConcurrencyCommandText = BuildDeleteWithoutConcurrencySql(mapping.Schema, mapping.TableName, mapping.KeyPropertyName),
            DeleteWithConcurrencyCommandText = concurrencyProperty is null
                ? null
                : BuildDeleteWithConcurrencySql(mapping.Schema, mapping.TableName, mapping.KeyPropertyName, concurrencyProperty),
            LoadNodeIdCommandText = BuildLoadNodeIdSql(mapping.Schema, mapping.TableName, mapping.KeyPropertyName)
        };
    }

    private static GraphEdgeSaveCommandPlan CreateEdgePlan(EdgeTypeMapping mapping)
    {
        var keyProperty = CreatePropertyPlan(mapping.ClrType, mapping.KeyPropertyName);
        var insertProperties = CreateInsertProperties(mapping.ClrType, mapping.KeyPropertyName, mapping.Properties);
        var updatableProperties = CreateUpdatableProperties(mapping.ClrType, mapping.KeyPropertyName, mapping.Properties);
        var concurrencyProperty = CreateConcurrencyProperty(mapping.ClrType);
        var nonConcurrencyUpdatableProperties = RemoveConcurrencyProperty(updatableProperties, concurrencyProperty?.Name);

        return new GraphEdgeSaveCommandPlan
        {
            ClrType = mapping.ClrType,
            KeyPropertyName = mapping.KeyPropertyName,
            KeyProperty = keyProperty,
            InsertProperties = insertProperties,
            UpdatableProperties = updatableProperties,
            NonConcurrencyUpdatableProperties = nonConcurrencyUpdatableProperties,
            ConcurrencyProperty = concurrencyProperty,
            InsertParameterNames = CreateParameterNames(insertProperties.Length + 2),
            UpdateWithoutConcurrencyParameterNames = CreateParameterNames(updatableProperties.Length + 1),
            UpdateWithConcurrencyParameterNames = CreateParameterNames(nonConcurrencyUpdatableProperties.Length + 3),
            DeleteWithoutConcurrencyParameterNames = CreateParameterNames(1),
            DeleteWithConcurrencyParameterNames = CreateParameterNames(2),
            DeleteByNodeIdsParameterNames = CreateParameterNames(2),
            InsertCommandText = BuildInsertEdgeSql(mapping.Schema, mapping.TableName, insertProperties),
            UpdateWithoutConcurrencyCommandText = BuildUpdateWithoutConcurrencySql(mapping.Schema, mapping.TableName, mapping.KeyPropertyName, updatableProperties),
            UpdateWithConcurrencyCommandText = concurrencyProperty is null
                ? null
                : BuildUpdateWithConcurrencySql(mapping.Schema, mapping.TableName, mapping.KeyPropertyName, nonConcurrencyUpdatableProperties, concurrencyProperty),
            DeleteWithoutConcurrencyCommandText = BuildDeleteWithoutConcurrencySql(mapping.Schema, mapping.TableName, mapping.KeyPropertyName),
            DeleteWithConcurrencyCommandText = concurrencyProperty is null
                ? null
                : BuildDeleteWithConcurrencySql(mapping.Schema, mapping.TableName, mapping.KeyPropertyName, concurrencyProperty),
            DeleteByNodeIdsCommandText = BuildDeleteEdgeByNodeIdsSql(mapping.Schema, mapping.TableName)
        };
    }

    private static GraphSavePropertyPlan[] CreateInsertProperties(Type clrType, string keyPropertyName, IReadOnlyList<PropertyMapping> mappedProperties)
    {
        var result = new List<GraphSavePropertyPlan>(mappedProperties.Count + 1)
        {
            CreatePropertyPlan(clrType, keyPropertyName)
        };

        for (var i = 0; i < mappedProperties.Count; i++)
        {
            var propertyName = mappedProperties[i].PropertyName;

            if (!string.Equals(propertyName, keyPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(CreatePropertyPlan(clrType, propertyName));
            }
        }

        return [.. result];
    }

    private static GraphSavePropertyPlan[] CreateUpdatableProperties(Type clrType, string keyPropertyName, IReadOnlyList<PropertyMapping> mappedProperties)
    {
        var result = new List<GraphSavePropertyPlan>(mappedProperties.Count);

        for (var i = 0; i < mappedProperties.Count; i++)
        {
            var propertyName = mappedProperties[i].PropertyName;

            if (!string.Equals(propertyName, keyPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(CreatePropertyPlan(clrType, propertyName));
            }
        }

        return [.. result];
    }

    private static GraphSavePropertyPlan[] RemoveConcurrencyProperty(GraphSavePropertyPlan[] properties, string? concurrencyPropertyName)
    {
        if (concurrencyPropertyName is null)
        {
            return [.. properties];
        }

        var result = new List<GraphSavePropertyPlan>(properties.Length);

        for (var i = 0; i < properties.Length; i++)
        {
            if (!string.Equals(properties[i].Name, concurrencyPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(properties[i]);
            }
        }

        return [.. result];
    }

    private static GraphSavePropertyPlan? CreateConcurrencyProperty(Type clrType)
    {
        if (!typeof(IHasConcurrencyToken).IsAssignableFrom(clrType))
        {
            return null;
        }

        return CreatePropertyPlan(clrType, ConcurrencyPropertyName);
    }

    private static GraphSavePropertyPlan CreatePropertyPlan(Type clrType, string propertyName)
    {
        var property = clrType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) ?? throw new InvalidOperationException(
                $"Property '{propertyName}' was not found on '{clrType.FullName}'.");

        return new GraphSavePropertyPlan(property.Name, SqlGenerationHelpers.Escape(property.Name), CreateGetter(property), property.CanWrite ? CreateSetter(property) : null);
    }

    private static Func<object, object?> CreateGetter(PropertyInfo property)
    {
        var target = Expression.Parameter(typeof(object), "target");
        var access = Expression.Property(Expression.Convert(target, property.DeclaringType!), property);
        var box = Expression.Convert(access, typeof(object));

        return Expression.Lambda<Func<object, object?>>(box, target).Compile();
    }

    private static Action<object, object?> CreateSetter(PropertyInfo property)
    {
        var target = Expression.Parameter(typeof(object), "target");
        var value = Expression.Parameter(typeof(object), "value");
        var body = Expression.Assign(Expression.Property(Expression.Convert(target, property.DeclaringType!), property), Expression.Convert(value, property.PropertyType));

        return Expression.Lambda<Action<object, object?>>(body, target, value).Compile();
    }

    private static string BuildInsertSql(string schema, string tableName, GraphSavePropertyPlan[] properties)
    {
        var builder = new StringBuilder(96 + (properties.Length * 16));
        builder.Append("INSERT INTO ");
        builder.Append(Table(schema, tableName));
        builder.Append(" (");

        AppendColumns(builder, properties);
        builder.Append(") VALUES (");
        AppendParameterNames(builder, properties.Length, startIndex: 0);
        builder.Append(");");

        return builder.ToString();
    }

    private static string BuildInsertEdgeSql(string schema, string tableName, GraphSavePropertyPlan[] properties)
    {
        var builder = new StringBuilder(112 + (properties.Length * 16));

        builder.Append("INSERT INTO ");
        builder.Append(Table(schema, tableName));
        builder.Append(" ($from_id, $to_id");

        for (var i = 0; i < properties.Length; i++)
        {
            builder.Append(", ");
            builder.Append(properties[i].ColumnSql);
        }

        builder.Append(") VALUES (");
        AppendParameterNames(builder, properties.Length + 2, startIndex: 0);
        builder.Append(");");

        return builder.ToString();
    }

    private static string BuildUpdateWithoutConcurrencySql(string schema, string tableName, string keyPropertyName, GraphSavePropertyPlan[] updatableProperties)
    {
        if (updatableProperties.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(112 + (updatableProperties.Length * 20));

        builder.Append("UPDATE ");
        builder.Append(Table(schema, tableName));
        builder.Append(" SET ");

        AppendSetClauses(builder, updatableProperties, startParameterIndex: 0);

        builder.Append(" WHERE ");
        builder.Append(Column(keyPropertyName));
        builder.Append(ParameterTarget);
        builder.Append(updatableProperties.Length);
        builder.Append(';');

        return builder.ToString();
    }

    private static string BuildUpdateWithConcurrencySql(
        string schema,
        string tableName,
        string keyPropertyName,
        GraphSavePropertyPlan[] nonConcurrencyProperties,
        GraphSavePropertyPlan concurrencyProperty)
    {
        var versionParameterIndex = nonConcurrencyProperties.Length;
        var keyParameterIndex = versionParameterIndex + 1;
        var originalVersionParameterIndex = versionParameterIndex + 2;
        var builder = new StringBuilder(144 + (nonConcurrencyProperties.Length * 20));

        builder.Append("UPDATE ");
        builder.Append(Table(schema, tableName));
        builder.Append(" SET ");

        if (nonConcurrencyProperties.Length > 0)
        {
            AppendSetClauses(builder, nonConcurrencyProperties, startParameterIndex: 0);
            builder.Append(", ");
        }

        builder.Append(concurrencyProperty.ColumnSql);
        builder.Append(ParameterTarget);
        builder.Append(versionParameterIndex);
        builder.Append(" WHERE ");
        builder.Append(Column(keyPropertyName));
        builder.Append(ParameterTarget);
        builder.Append(keyParameterIndex);
        builder.Append(" AND ");
        builder.Append(concurrencyProperty.ColumnSql);
        builder.Append(ParameterTarget);
        builder.Append(originalVersionParameterIndex);
        builder.Append(';');

        return builder.ToString();
    }

    private static string BuildDeleteWithoutConcurrencySql(string schema, string tableName, string keyPropertyName) =>
        $"DELETE FROM {Table(schema, tableName)} WHERE {Column(keyPropertyName)} = @p0;";

    private static string BuildDeleteWithConcurrencySql(string schema, string tableName, string keyPropertyName, GraphSavePropertyPlan concurrencyProperty) =>
        $"DELETE FROM {Table(schema, tableName)} WHERE {Column(keyPropertyName)} = @p0 AND {concurrencyProperty.ColumnSql} = @p1;";

    private static string BuildDeleteEdgeByNodeIdsSql(string schema, string tableName)
    {
        var table = Table(schema, tableName);

        return $"IF (SELECT COUNT_BIG(*) FROM {table} WHERE $from_id = @p0 AND $to_id = @p1) > 1" + Environment.NewLine +
               "BEGIN" + Environment.NewLine +
               "    THROW 51001, 'Ambiguous GORM edge disconnection matched multiple edge rows. Remove a specific edge entity instead.', 1;" + Environment.NewLine +
               "END;" + Environment.NewLine +
               $"DELETE FROM {table} WHERE $from_id = @p0 AND $to_id = @p1;";
    }

    private static string BuildLoadNodeIdSql(string schema, string tableName, string keyPropertyName) =>
        $"SELECT $node_id FROM {Table(schema, tableName)} WITH (UPDLOCK, HOLDLOCK) WHERE {Column(keyPropertyName)} = @p0;";

    private static void AppendColumns(StringBuilder builder, GraphSavePropertyPlan[] properties)
    {
        for (var i = 0; i < properties.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(properties[i].ColumnSql);
        }
    }

    private static void AppendSetClauses(StringBuilder builder, GraphSavePropertyPlan[] properties, int startParameterIndex)
    {
        for (var i = 0; i < properties.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(properties[i].ColumnSql);
            builder.Append(ParameterTarget);
            builder.Append(startParameterIndex + i);
        }
    }

    private static void AppendParameterNames(StringBuilder builder, int count, int startIndex)
    {
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append("@p");
            builder.Append(startIndex + i);
        }
    }

    private static string[] CreateParameterNames(int count)
    {
        var result = new string[count];

        for (var i = 0; i < count; i++)
        {
            result[i] = string.Concat("@p", i);
        }

        return result;
    }

    private static SavePlanKey CreateKey(Type clrType, string schema, string tableName, string keyPropertyName, IReadOnlyList<PropertyMapping> mappedProperties)
    {
        var signatureBuilder = new StringBuilder(mappedProperties.Count * 24);

        for (var i = 0; i < mappedProperties.Count; i++)
        {
            if (i > 0)
            {
                signatureBuilder.Append('|');
            }

            signatureBuilder.Append(mappedProperties[i].PropertyName);
        }

        return new SavePlanKey(clrType, schema, tableName, keyPropertyName, signatureBuilder.ToString());
    }

    private static string Table(string schema, string tableName) => SqlGenerationHelpers.EscapeFullName(schema, tableName);

    private static string Column(string columnName) => SqlGenerationHelpers.Escape(columnName);

    private readonly record struct SavePlanKey(
        Type ClrType,
        string Schema,
        string TableName,
        string KeyPropertyName,
        string PropertySignature);
}