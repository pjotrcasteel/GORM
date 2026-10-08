namespace Gorm.Application.Intelligence.Studio;

/// <summary>
/// Describes how a desktop tool can edit an invocation value.
/// </summary>
public enum GraphStudioValueKind
{
    /// <summary>
    /// True/false input.
    /// </summary>
    Boolean,

    /// <summary>
    /// Integral numeric input.
    /// </summary>
    Integer,

    /// <summary>
    /// Floating-point or decimal numeric input.
    /// </summary>
    Number,

    /// <summary>
    /// Text input.
    /// </summary>
    Text,

    /// <summary>
    /// Guid input.
    /// </summary>
    Guid,

    /// <summary>
    /// Time-span input.
    /// </summary>
    Duration,

    /// <summary>
    /// Selection from declared enum values.
    /// </summary>
    Enumeration,

    /// <summary>
    /// Complex CLR value that requires an application-owned editor.
    /// </summary>
    Custom
}