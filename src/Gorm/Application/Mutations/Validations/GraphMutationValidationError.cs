namespace Gorm.Application.Mutations.Validations;

/// <summary>
/// Represents a graph mutation validation error.
/// </summary>
public sealed class GraphMutationValidationError
{
    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets or sets the optional path.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Gets or sets the optional mutation key.
    /// </summary>
    public string? Key { get; init; }
}