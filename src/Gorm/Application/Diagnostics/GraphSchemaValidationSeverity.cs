namespace Gorm.Application.Diagnostics;

/// <summary>
/// Represents graph schema validation severity.
/// </summary>
public enum GraphSchemaValidationSeverity
{
    /// <summary>
    /// The issue is informational.
    /// </summary>
    Information = 0,

    /// <summary>
    /// The issue is a warning.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// The issue is an error.
    /// </summary>
    Error = 2
}