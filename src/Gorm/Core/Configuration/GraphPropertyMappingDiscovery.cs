using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Gorm.Core.Metadata;
using Gorm.Core.Primitives;

namespace Gorm.Core.Configuration;

internal static class GraphPropertyMappingDiscovery
{
    private static readonly HashSet<Type> AutoMappedTypes =
    [
        typeof(string),
        typeof(Guid),
        typeof(bool),
        typeof(byte),
        typeof(short),
        typeof(int),
        typeof(long),
        typeof(float),
        typeof(double),
        typeof(decimal),
        typeof(DateTime),
        typeof(DateTimeOffset),
        typeof(DateOnly),
        typeof(TimeOnly),
        typeof(TimeSpan),
        typeof(byte[])
    ];

    public static IReadOnlyList<PropertyMapping> BuildProperties(
        Type clrType,
        string keyPropertyName,
        IEnumerable<PropertyBuilder> configuredProperties,
        params string[] excludedPropertyNames)
    {
        ArgumentNullException.ThrowIfNull(clrType);
        ArgumentNullException.ThrowIfNull(configuredProperties);

        var properties = BuildPropertyMap(clrType, keyPropertyName, configuredProperties, excludedPropertyNames.ToHashSet(StringComparer.Ordinal));

        EnsureKeyPropertyExists(clrType, keyPropertyName, properties);

        return MoveKeyFirst(properties, keyPropertyName);
    }

    private static Dictionary<string, PropertyMapping> BuildPropertyMap(
        Type clrType,
        string keyPropertyName,
        IEnumerable<PropertyBuilder> configuredProperties,
        IReadOnlySet<string> excludedPropertyNames)
    {
        var properties = new Dictionary<string, PropertyMapping>(StringComparer.Ordinal);

        foreach (var property in DiscoverAutoMappedProperties(clrType, excludedPropertyNames))
        {
            properties[property.Name] = CreateDefaultMapping(property, keyPropertyName);
        }

        foreach (var property in configuredProperties.Select(property => property.Build()))
        {
            properties[property.PropertyName] = property;
        }

        return properties;
    }

    private static IReadOnlyList<PropertyMapping> MoveKeyFirst(IReadOnlyDictionary<string, PropertyMapping> properties, string keyPropertyName)
    {
        var key = properties.Values.Single(property => IsKey(property, keyPropertyName));

        return [key, .. properties.Values.Where(property => !IsKey(property, keyPropertyName))];
    }

    private static IEnumerable<PropertyInfo> DiscoverAutoMappedProperties(Type clrType, IReadOnlySet<string> excludedPropertyNames) =>
        clrType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property =>
                property is { CanRead: true, CanWrite: true } &&
                property.GetIndexParameters() is [] &&
                property.GetCustomAttribute<NotMappedAttribute>() is null &&
                !excludedPropertyNames.Contains(property.Name) &&
                !IsNavigationPropertyType(property.PropertyType) &&
                IsAutoMappedPropertyType(property.PropertyType));

    private static PropertyMapping CreateDefaultMapping(PropertyInfo property, string keyPropertyName) => new()
    {
        PropertyName = property.Name,
        PropertyType = property.PropertyType,
        IsRequired = IsKey(property.Name, keyPropertyName) ||
            property.GetCustomAttribute<RequiredAttribute>() is not null,
        MaxLength = GetMaxLength(property)
    };

    private static PropertyMapping CreateRequiredKeyMapping(PropertyInfo property) => new()
    {
        PropertyName = property.Name,
        PropertyType = property.PropertyType,
        IsRequired = true,
        MaxLength = null
    };

    private static void EnsureKeyPropertyExists(Type clrType, string keyPropertyName, IDictionary<string, PropertyMapping> properties)
    {
        if (properties.Values.Any(property => IsKey(property, keyPropertyName)))
        {
            return;
        }

        var keyProperty = clrType.GetProperty(keyPropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) ?? throw new InvalidOperationException(
                $"Key property '{keyPropertyName}' was not found on '{clrType.FullName}'.");

        properties[keyProperty.Name] = CreateRequiredKeyMapping(keyProperty);
    }

    private static bool IsNavigationPropertyType(Type propertyType) =>
        propertyType switch
        {
            _ when typeof(Node).IsAssignableFrom(propertyType) => true,
            _ when propertyType is { } type && IsScalarEnumerableException(type) => false,
            _ when !typeof(IEnumerable).IsAssignableFrom(propertyType) => false,
            _ => TryGetEnumerableElementType(propertyType, out var elementType) &&
                typeof(Node).IsAssignableFrom(elementType)
        };

    private static bool IsScalarEnumerableException(Type propertyType) =>
        propertyType == typeof(string) || propertyType == typeof(byte[]);

    private static bool IsAutoMappedPropertyType(Type propertyType) =>
        (Nullable.GetUnderlyingType(propertyType) ?? propertyType) switch
        {
            { IsEnum: true } => true,
            var type => AutoMappedTypes.Contains(type)
        };

    private static bool TryGetEnumerableElementType(Type type, out Type elementType)
    {
        elementType = type switch
        {
            { IsArray: true } => type.GetElementType()!,
            { IsGenericType: true } when type.GetGenericArguments() is [var genericArgument] => genericArgument,
            _ => type
                .GetInterfaces()
                .FirstOrDefault(@interface => @interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ?.GetGenericArguments()[0] ?? typeof(object)
        };

        return elementType != typeof(object);
    }

    private static int? GetMaxLength(PropertyInfo property) =>
        property.GetCustomAttribute<MaxLengthAttribute>()?.Length switch
        {
            > 0 and var maxLength => maxLength,
            _ => property.GetCustomAttribute<StringLengthAttribute>()?.MaximumLength
        };

    private static bool IsKey(PropertyMapping property, string keyPropertyName) =>
        IsKey(property.PropertyName, keyPropertyName);

    private static bool IsKey(string propertyName, string keyPropertyName) =>
        string.Equals(propertyName, keyPropertyName, StringComparison.OrdinalIgnoreCase);
}