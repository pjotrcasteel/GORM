using System.Collections;
using System.Reflection;
using Gorm.Core.Metadata;
using Gorm.Core.Primitives;

namespace Gorm.Application.History;

/// <summary>
/// Creates detached snapshot clones for history capture.
/// </summary>
internal static class GraphHistorySnapshotCloner
{
    /// <summary>
    /// Clones the provided value using all public scalar properties.
    /// </summary>
    /// <param name="value">The value to clone.</param>
    /// <param name="runtimeType">The runtime type.</param>
    /// <returns>The detached scalar clone.</returns>
    public static object Clone(object value, Type runtimeType) =>
        Clone(value, runtimeType, GetScalarPropertyNames(runtimeType));

    /// <summary>
    /// Clones the provided value to a detached snapshot instance.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="runtimeType">The runtime type.</param>
    /// <param name="properties">The mapped scalar properties to copy.</param>
    /// <returns>The cloned value.</returns>
    public static object Clone(object value, Type runtimeType, IReadOnlyList<PropertyMapping> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        return Clone(value, runtimeType, properties.Select(property => property.PropertyName));
    }

    private static object Clone(object value, Type runtimeType, IEnumerable<string> mappedPropertyNames)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(runtimeType);
        ArgumentNullException.ThrowIfNull(mappedPropertyNames);

        var clone = Activator.CreateInstance(runtimeType, nonPublic: true) ?? throw new InvalidOperationException(
            $"Failed to create a history snapshot instance of type '{runtimeType.FullName}'.");

        var propertyNames = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(Node.Id)
        };

        if (value is Edge)
        {
            propertyNames.Add(nameof(Edge.FromId));
            propertyNames.Add(nameof(Edge.ToId));
        }

        foreach (var propertyName in mappedPropertyNames)
        {
            propertyNames.Add(propertyName);
        }

        foreach (var propertyName in propertyNames)
        {
            CopyProperty(value, clone, runtimeType, propertyName);
        }

        return clone;
    }

    private static IEnumerable<string> GetScalarPropertyNames(Type runtimeType) =>
        runtimeType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property =>
                property.CanRead &&
                property.CanWrite &&
                property.GetIndexParameters().Length == 0 &&
                IsScalarProperty(property.PropertyType))
            .Select(property => property.Name);

    private static bool IsScalarProperty(Type propertyType)
    {
        if (propertyType == typeof(string) || propertyType == typeof(byte[]))
        {
            return true;
        }

        if (typeof(Node).IsAssignableFrom(propertyType) ||
            typeof(Edge).IsAssignableFrom(propertyType) ||
            typeof(IEnumerable).IsAssignableFrom(propertyType))
        {
            return false;
        }

        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        return underlyingType.IsEnum ||
            underlyingType.IsPrimitive ||
            underlyingType == typeof(decimal) ||
            underlyingType == typeof(Guid) ||
            underlyingType == typeof(DateTime) ||
            underlyingType == typeof(DateTimeOffset) ||
            underlyingType == typeof(DateOnly) ||
            underlyingType == typeof(TimeOnly) ||
            underlyingType == typeof(TimeSpan);
    }

    private static void CopyProperty(object source, object destination, Type runtimeType, string propertyName)
    {
        var property = runtimeType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) ?? throw new InvalidOperationException(
            $"Mapped history property '{propertyName}' was not found on '{runtimeType.FullName}'.");

        if (!property.CanRead || !property.CanWrite)
        {
            throw new InvalidOperationException(
                $"Mapped history property '{propertyName}' on '{runtimeType.FullName}' must be readable and writable.");
        }

        property.SetValue(destination, CloneValue(property.GetValue(source)));
    }

    private static object? CloneValue(object? value) =>
        value is byte[] bytes ? bytes.ToArray() : value;
}