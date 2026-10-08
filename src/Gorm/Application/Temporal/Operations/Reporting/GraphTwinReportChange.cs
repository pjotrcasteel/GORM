using Gorm.Application.Temporal.Diff;

namespace Gorm.Application.Temporal.Operations.Reporting;

/// <summary>
/// Serializable entity-level report evidence.
/// </summary>
public sealed class GraphTwinReportChange
{
    /// <summary>
    /// Gets entity kind.
    /// </summary>
    public required GraphWorldEntityKind EntityKind { get; init; }

    /// <summary>
    /// Gets change kind.
    /// </summary>
    public required GraphWorldChangeKind ChangeKind { get; init; }

    /// <summary>
    /// Gets entity id.
    /// </summary>
    public required Guid EntityId { get; init; }

    /// <summary>
    /// Gets before type name.
    /// </summary>
    public string? BeforeType { get; init; }

    /// <summary>
    /// Gets after type name.
    /// </summary>
    public string? AfterType { get; init; }

    /// <summary>
    /// Gets before state fingerprint.
    /// </summary>
    public string? BeforeFingerprint { get; init; }

    /// <summary>
    /// Gets after state fingerprint.
    /// </summary>
    public string? AfterFingerprint { get; init; }
}