namespace Gorm.Application.Intelligence.Studio;

/// <summary>
/// Serializable editor metadata for an option property or invocation parameter.
/// </summary>
public sealed class GraphStudioValueMetadata
{
    /// <summary>
    /// Gets the stable value name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the CLR type name.
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// Gets the suggested desktop editor kind.
    /// </summary>
    public required GraphStudioValueKind Kind { get; init; }

    /// <summary>
    /// Gets whether the value is required.
    /// </summary>
    public required bool IsRequired { get; init; }

    /// <summary>
    /// Gets whether a generic GormStudio editor can safely change the value.
    /// </summary>
    public required bool IsEditable { get; init; }

    /// <summary>
    /// Gets the invariant default value, when it has a useful scalar representation.
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// Gets allowed enum values.
    /// </summary>
    public IReadOnlyList<string> AllowedValues { get; init; } = [];

    /// <summary>
    /// Gets the user-facing value description.
    /// </summary>
    public string? Description { get; init; }
}