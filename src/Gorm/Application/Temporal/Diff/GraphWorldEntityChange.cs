namespace Gorm.Application.Temporal.Diff;

/// <summary>
/// Describes one deterministic node or edge change between two worlds.
/// </summary>
public sealed class GraphWorldEntityChange
{
    /// <summary>
    /// Gets whether the changed entity is a node or edge.
    /// </summary>
    public required GraphWorldEntityKind EntityKind { get; init; }

    /// <summary>
    /// Gets whether the entity was added, removed or modified.
    /// </summary>
    public required GraphWorldChangeKind ChangeKind { get; init; }

    /// <summary>
    /// Gets the stable GORM entity identifier.
    /// </summary>
    public required Guid EntityId { get; init; }

    /// <summary>
    /// Gets the concrete type in the older world, when present.
    /// </summary>
    public Type? BeforeType { get; init; }

    /// <summary>
    /// Gets the concrete type in the newer world, when present.
    /// </summary>
    public Type? AfterType { get; init; }

    /// <summary>
    /// Gets the source node before the change for an edge.
    /// </summary>
    public Guid? BeforeFromId { get; init; }

    /// <summary>
    /// Gets the destination node before the change for an edge.
    /// </summary>
    public Guid? BeforeToId { get; init; }

    /// <summary>
    /// Gets the source node after the change for an edge.
    /// </summary>
    public Guid? AfterFromId { get; init; }

    /// <summary>
    /// Gets the destination node after the change for an edge.
    /// </summary>
    public Guid? AfterToId { get; init; }

    /// <summary>
    /// Gets the SHA-256 state fingerprint before the change, when present.
    /// </summary>
    public string? BeforeStateFingerprint { get; init; }

    /// <summary>
    /// Gets the SHA-256 state fingerprint after the change, when present.
    /// </summary>
    public string? AfterStateFingerprint { get; init; }
}