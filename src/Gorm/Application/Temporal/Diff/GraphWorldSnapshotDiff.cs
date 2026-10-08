using System.Collections.ObjectModel;
using Gorm.Application.Temporal.Snapshots;

namespace Gorm.Application.Temporal.Diff;

/// <summary>
/// Contains the deterministic entity-level difference between two temporal worlds.
/// </summary>
public sealed class GraphWorldSnapshotDiff
{
    internal GraphWorldSnapshotDiff(GraphWorldSnapshotIdentity before, GraphWorldSnapshotIdentity after, GraphWorldEntityChange[] changes)
    {
        Before = before;
        After = after;
        Changes = Array.AsReadOnly(changes);
        AddedCount = changes.Count(change => change.ChangeKind == GraphWorldChangeKind.Added);
        RemovedCount = changes.Count(change => change.ChangeKind == GraphWorldChangeKind.Removed);
        ModifiedCount = changes.Count(change => change.ChangeKind == GraphWorldChangeKind.Modified);
        TopologyChanged = changes.Any(change =>
            change.ChangeKind != GraphWorldChangeKind.Modified ||
            change.BeforeType != change.AfterType ||
            change.BeforeFromId != change.AfterFromId ||
            change.BeforeToId != change.AfterToId);
        Explanation =
            $"Compared world versions {before.Version} and {after.Version}: " +
            $"{AddedCount} added, {RemovedCount} removed and {ModifiedCount} modified entities.";
    }

    /// <summary>
    /// Gets the older world identity.
    /// </summary>
    public GraphWorldSnapshotIdentity Before { get; }

    /// <summary>
    /// Gets the newer world identity.
    /// </summary>
    public GraphWorldSnapshotIdentity After { get; }

    /// <summary>
    /// Gets changes ordered by entity kind and identifier.
    /// </summary>
    public ReadOnlyCollection<GraphWorldEntityChange> Changes { get; }

    /// <summary>
    /// Gets the number of added entities.
    /// </summary>
    public int AddedCount { get; }

    /// <summary>
    /// Gets the number of removed entities.
    /// </summary>
    public int RemovedCount { get; }

    /// <summary>
    /// Gets the number of modified entities.
    /// </summary>
    public int ModifiedCount { get; }

    /// <summary>
    /// Gets whether entity existence, type or edge endpoints changed.
    /// </summary>
    public bool TopologyChanged { get; }

    /// <summary>
    /// Gets a reproducible comparison explanation.
    /// </summary>
    public string Explanation { get; }
}