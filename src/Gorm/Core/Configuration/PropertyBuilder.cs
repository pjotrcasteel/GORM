using Gorm.Core.Metadata;

namespace Gorm.Core.Configuration;

/// <summary>
/// Represents property builder.
/// </summary>
public sealed class PropertyBuilder
{
    private readonly string _propertyName;
    private readonly Type _propertyType;
    private bool _isRequired;
    private int? _maxLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyBuilder"/> class.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="propertyType">The property type.</param>
    internal PropertyBuilder(string propertyName, Type propertyType)
    {
        _propertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        _propertyType = propertyType ?? throw new ArgumentNullException(nameof(propertyType));
    }

    /// <summary>
    /// Executes is required.
    /// </summary>
    /// <returns>The builder.</returns>
    public PropertyBuilder IsRequired()
    {
        var underlyingType = Nullable.GetUnderlyingType(_propertyType);

        if (_propertyType.IsValueType && underlyingType is null && _propertyType != typeof(string))
        {
            _isRequired = true;
            return this;
        }

        _isRequired = true;
        return this;
    }

    /// <summary>
    /// Executes has max length.
    /// </summary>
    /// <param name="maxLength">The max length.</param>
    /// <returns>The builder.</returns>
    public PropertyBuilder HasMaxLength(int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        var type = Nullable.GetUnderlyingType(_propertyType) ?? _propertyType;

        if (type != typeof(string) && type != typeof(byte[]))
        {
            throw new InvalidOperationException(
                $"HasMaxLength(...) is only supported for string and byte[] properties. Property '{_propertyName}' is '{_propertyType.FullName}'.");
        }

        _maxLength = maxLength;
        return this;
    }

    /// <summary>
    /// Builds the result.
    /// </summary>
    /// <returns>The value.</returns>
    internal PropertyMapping Build() => new()
    {
        PropertyName = _propertyName,
        PropertyType = _propertyType,
        IsRequired = _isRequired,
        MaxLength = _maxLength
    };
}