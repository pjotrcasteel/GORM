using System.Collections.ObjectModel;
using Gorm.Application.Temporal.Diff;
using Gorm.Application.Temporal.Snapshots;

namespace Gorm.Application.Temporal.Timeline;

/// <summary>
/// Represents an ordered, validated history of immutable world versions and their changes.
/// </summary>
public sealed class GraphWorldTimeline
{
    private GraphWorldTimeline(GraphWorldTimelinePoint[] points)
    {
        Points = Array.AsReadOnly(points);
    }

    /// <summary>
    /// Gets timeline points in strictly increasing version order.
    /// </summary>
    public ReadOnlyCollection<GraphWorldTimelinePoint> Points { get; }

    /// <summary>
    /// Builds a deterministic timeline from compatible world versions.
    /// </summary>
    /// <param name="snapshots">World snapshots, in any input order.</param>
    /// <param name="options">Timeline and diff limits.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validated temporal timeline.</returns>
    public static GraphWorldTimeline Create(IEnumerable<GraphWorldSnapshot> snapshots, GraphWorldTimelineOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        options ??= new GraphWorldTimelineOptions();
        if (options.MaximumSnapshots <= 0 || options.MaximumChangesPerSnapshot <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Timeline limits must be positive.");
        }

        var ordered = new List<GraphWorldSnapshot>();
        foreach (var snapshot in snapshots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(snapshot);
            if (ordered.Count == options.MaximumSnapshots)
            {
                throw new InvalidOperationException(
                    $"Timeline exceeded MaximumSnapshots ({options.MaximumSnapshots}).");
            }

            ordered.Add(snapshot);
        }

        ordered.Sort((left, right) => left.Identity.Version.CompareTo(right.Identity.Version));
        ValidateSequence(ordered);

        var points = new GraphWorldTimelinePoint[ordered.Count];
        for (var index = 0; index < ordered.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            points[index] = new GraphWorldTimelinePoint
            {
                Snapshot = ordered[index].Identity,
                ChangesFromPrevious = index == 0
                    ? null
                    : GraphWorldSnapshotDiffer.Compare(
                        ordered[index - 1],
                        ordered[index],
                        new GraphWorldDiffOptions { MaximumChanges = options.MaximumChangesPerSnapshot },
                        cancellationToken)
            };
        }

        return new GraphWorldTimeline(points);
    }

    private static void ValidateSequence(List<GraphWorldSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return;
        }

        var key = snapshots[0].Identity.WorldKey;
        for (var index = 1; index < snapshots.Count; index++)
        {
            var previous = snapshots[index - 1].Identity;
            var current = snapshots[index].Identity;
            if (current.WorldKey != key)
            {
                throw new ArgumentException("Every snapshot in a timeline must have the same world key.", nameof(snapshots));
            }

            if (current.Version <= previous.Version)
            {
                throw new ArgumentException("Timeline versions must be unique and strictly increasing.", nameof(snapshots));
            }

            if (current.RecordedAt < previous.RecordedAt)
            {
                throw new ArgumentException("Recorded time cannot move backwards across increasing versions.", nameof(snapshots));
            }
        }
    }
}