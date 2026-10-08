namespace Gorm.Application.Mutations.Options;

/// <summary>
/// Represents graph mutation execution options.
/// </summary>
public sealed class GraphMutationOptions
{
    /// <summary>
    /// Gets the default graph mutation options.
    /// </summary>
    public static GraphMutationOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether invalid mutation plans should throw before execution.
    /// </summary>
    public bool ThrowOnValidationError { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether upserted nodes with an empty Id should be inserted.
    /// </summary>
    public bool InsertUpsertNodesWithEmptyId { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether upserted nodes with a non-empty Id should be updated.
    /// </summary>
    public bool UpdateUpsertNodesWithExistingId { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether upserted edges with an empty Id should be inserted.
    /// </summary>
    public bool InsertUpsertEdgesWithEmptyId { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether upserted edges with a non-empty Id should be updated.
    /// </summary>
    public bool UpdateUpsertEdgesWithExistingId { get; init; } = true;
}