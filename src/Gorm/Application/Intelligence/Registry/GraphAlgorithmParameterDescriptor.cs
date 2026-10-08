namespace Gorm.Application.Intelligence.Registry;

/// <summary>
/// Describes one named algorithm invocation parameter.
/// </summary>
public sealed class GraphAlgorithmParameterDescriptor
{
    /// <summary>
    /// Initializes a parameter descriptor.
    /// </summary>
    public GraphAlgorithmParameterDescriptor(string name, Type parameterType, bool isRequired, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A parameter name cannot be empty.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(parameterType);
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("A parameter description cannot be empty.", nameof(description));
        }

        Name = name;
        ParameterType = parameterType;
        IsRequired = isRequired;
        Description = description;
    }

    /// <summary>
    /// Gets the stable parameter name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the runtime parameter type.
    /// </summary>
    public Type ParameterType { get; }

    /// <summary>
    /// Gets whether callers must supply the parameter.
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    /// Gets the user-facing description.
    /// </summary>
    public string Description { get; }
}