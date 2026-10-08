using Gorm.Application.Intelligence.Registry;

namespace Gorm.Application.Intelligence.Studio;

/// <summary>
/// Serializable desktop discovery metadata for a graph algorithm.
/// </summary>
public sealed class GraphStudioAlgorithmMetadata
{
    /// <summary>
    /// Gets the stable registry identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the user-facing name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the user-facing description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the algorithm category.
    /// </summary>
    public required GraphAlgorithmCategory Category { get; init; }

    /// <summary>
    /// Gets the declared result CLR type name.
    /// </summary>
    public required string ResultTypeName { get; init; }

    /// <summary>
    /// Gets the complexity and safety hint.
    /// </summary>
    public required string Complexity { get; init; }

    /// <summary>
    /// Gets whether exact incremental reuse is available.
    /// </summary>
    public required bool SupportsIncremental { get; init; }

    /// <summary>
    /// Gets whether successive live snapshots are supported.
    /// </summary>
    public required bool SupportsLive { get; init; }

    /// <summary>
    /// Gets whether an options object must be supplied for invocation.
    /// </summary>
    public required bool OptionsRequired { get; init; }

    /// <summary>
    /// Gets discoverable option fields.
    /// </summary>
    public IReadOnlyList<GraphStudioValueMetadata> Options { get; init; } = [];

    /// <summary>
    /// Gets discoverable named invocation parameters.
    /// </summary>
    public IReadOnlyList<GraphStudioValueMetadata> Parameters { get; init; } = [];
}