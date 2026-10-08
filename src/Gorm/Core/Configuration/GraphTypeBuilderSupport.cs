using System.Linq.Expressions;
using Gorm.Core.Metadata;

namespace Gorm.Core.Configuration;

internal static class GraphTypeBuilderSupport
{
    public static (string TableName, string Schema) BuildTable(string tableName, string schema) =>
        (
            string.IsNullOrWhiteSpace(tableName)
                ? throw new ArgumentException("Table name cannot be null or whitespace.", nameof(tableName))
                : tableName,
            string.IsNullOrWhiteSpace(schema)
                ? throw new ArgumentException("Schema cannot be null or whitespace.", nameof(schema))
                : schema
        );

    public static PropertyBuilder GetOrAddProperty<TModel, TProperty>(IDictionary<string, PropertyBuilder> properties, Expression<Func<TModel, TProperty>> propertyExpression)
    {
        var propertyName = GetPropertyName(propertyExpression);

        if (properties.TryGetValue(propertyName, out var builder))
        {
            return builder;
        }

        builder = new PropertyBuilder(propertyName, typeof(TProperty));
        properties[propertyName] = builder;

        return builder;
    }

    public static void Ignore<TModel, TProperty>(
        IDictionary<string, PropertyBuilder> properties,
        ISet<string> ignoredProperties,
        Expression<Func<TModel, TProperty>> propertyExpression)
    {
        var propertyName = GetPropertyName(propertyExpression);

        properties.Remove(propertyName);
        ignoredProperties.Add(propertyName);
    }

    public static GraphIndexBuilder AddIndex<TModel>(ICollection<GraphIndexBuilder> indexes, string tableName, params Expression<Func<TModel, object?>>[] propertyExpressions)
    {
        ArgumentNullException.ThrowIfNull(propertyExpressions);

        if (propertyExpressions.Length == 0)
        {
            throw new ArgumentException("At least one property expression is required.", nameof(propertyExpressions));
        }

        var propertyNames = propertyExpressions.SelectMany(GetPropertyNames).Distinct(StringComparer.Ordinal).ToArray();

        if (propertyNames.Length == 0)
        {
            throw new InvalidOperationException("At least one index property must be specified.");
        }

        var builder = new GraphIndexBuilder(BuildDefaultIndexName(tableName, propertyNames), propertyNames);

        indexes.Add(builder);

        return builder;
    }

    public static void ValidateIndexes<TModel>(string modelKind, IEnumerable<GraphIndexBuilder> indexBuilders, IEnumerable<PropertyMapping> properties)
    {
        var propertyNames = properties.Select(property => property.PropertyName).ToHashSet(StringComparer.Ordinal);

        var indexes = indexBuilders.Select(index => index.Build()).ToArray();

        var invalidProperty = indexes
            .SelectMany(index => index.PropertyNames.Select(propertyName => new { Index = index, PropertyName = propertyName }))
            .FirstOrDefault(indexProperty => !propertyNames.Contains(indexProperty.PropertyName));

        if (invalidProperty is not null)
        {
            throw new InvalidOperationException(
                $"Index '{invalidProperty.Index.DatabaseName}' on {modelKind} '{typeof(TModel).FullName}' references property " +
                $"'{invalidProperty.PropertyName}', but that property is not mapped.");
        }

        var duplicateIndex = indexes.GroupBy(index => index.DatabaseName, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);

        if (duplicateIndex is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate index name '{duplicateIndex.Key}' detected for {modelKind} '{typeof(TModel).FullName}'.");
        }
    }

    public static string GetPropertyName(LambdaExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return expression.Body switch
        {
            MemberExpression memberExpression => memberExpression.Member.Name,
            UnaryExpression { Operand: MemberExpression memberExpression } => memberExpression.Member.Name,
            _ => throw new InvalidOperationException("Expression must point to a property.")
        };
    }

    private static string[] GetPropertyNames(LambdaExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return expression.Body switch
        {
            MemberExpression memberExpression => [memberExpression.Member.Name],
            UnaryExpression { Operand: MemberExpression memberExpression } => [memberExpression.Member.Name],
            NewExpression newExpression => [.. newExpression.Arguments.SelectMany(GetPropertyNamesFromExpression).Distinct(StringComparer.Ordinal)],
            _ => throw new InvalidOperationException("Expression must point to a property or an anonymous object of properties.")
        };
    }

    private static IEnumerable<string> GetPropertyNamesFromExpression(Expression expression) => expression switch
    {
        MemberExpression memberExpression => [memberExpression.Member.Name],
        UnaryExpression { Operand: MemberExpression memberExpression } => [memberExpression.Member.Name],
        _ => throw new InvalidOperationException("Expression must point to a property.")
    };

    private static string BuildDefaultIndexName(string tableName, IReadOnlyList<string> propertyNames) =>
        $"IX_{tableName}_{string.Join("_", propertyNames)}";
}