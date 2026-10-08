using Gorm.Application.Temporal.Snapshots;

namespace Gorm.Application.Temporal.History;

/// <summary>
/// Describes a resolved point-in-time world and the evidence selection that produced it.
/// </summary>
public sealed class GraphWorldHistoryProjectionResult
{
    /// <summary>
    /// Gets the detached immutable world snapshot.
    /// </summary>
    public required GraphWorldSnapshot Snapshot { get; init; }

    /// <summary>
    /// Gets the number of history envelopes inspected.
    /// </summary>
    public required int TotalHistoryEntries { get; init; }

    /// <summary>
    /// Gets the number of envelopes known at the recorded-time cutoff.
    /// </summary>
    public required int KnownHistoryEntries { get; init; }

    /// <summary>
    /// Gets the number of known envelopes whose validity interval contains the valid-time instant.
    /// </summary>
    public required int ApplicableHistoryEntries { get; init; }

    /// <summary>
    /// Gets the number of latest logical entity states selected before inactive operations are removed.
    /// </summary>
    public required int SelectedEntityStates { get; init; }

    /// <summary>
    /// Gets the number of selected entity states excluded as deleted or disconnected.
    /// </summary>
    public required int InactiveEntityStates { get; init; }

    /// <summary>
    /// Gets a reproducible explanation of the temporal selection.
    /// </summary>
    public required string Explanation { get; init; }
}