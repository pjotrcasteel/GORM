using Gorm.Application.History;
using Gorm.Application.History.Envelopes;
using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Temporal.Diff;
using Gorm.Application.Temporal.History;

namespace Gorm.Application.Temporal.Bitemporal;

/// <summary>
/// Stores a detached, bounded history dataset that can resolve repeatable worlds at multiple bitemporal coordinates.
/// </summary>
public sealed class GraphWorldHistoryDataset
{
    private readonly GraphHistoryEnvelope[] _history;

    private GraphWorldHistoryDataset(GraphHistoryEnvelope[] history)
    {
        _history = history;
    }

    /// <summary>
    /// Gets the number of detached history envelopes.
    /// </summary>
    public int Count => _history.Length;

    /// <summary>
    /// Captures history into a detached repeatable-query dataset.
    /// </summary>
    /// <param name="history">History returned by ordinary GORM node and edge history queries.</param>
    /// <param name="maximumHistoryEntries">Maximum source entries to capture.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The immutable history dataset.</returns>
    public static GraphWorldHistoryDataset Capture(IEnumerable<GraphHistoryEnvelope> history, int maximumHistoryEntries = 1_000_000, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumHistoryEntries);

        var result = new List<GraphHistoryEnvelope>();
        foreach (var envelope in history)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(envelope);
            if (result.Count == maximumHistoryEntries)
            {
                throw new GraphWorldHistoryProjectionException(
                    GraphWorldHistoryProjectionFailureReason.EntryLimitExceeded,
                    $"History dataset exceeded {nameof(maximumHistoryEntries)} ({maximumHistoryEntries}).");
            }

            result.Add(Clone(envelope));
        }

        return new GraphWorldHistoryDataset([.. result]);
    }

    /// <summary>
    /// Resolves one repeatable point-in-time world.
    /// </summary>
    /// <param name="worldKey">Stable world key.</param>
    /// <param name="version">Monotonic output version.</param>
    /// <param name="coordinate">Valid and recorded temporal coordinate.</param>
    /// <param name="options">Projection semantics and limits.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved world and selection evidence.</returns>
    public GraphWorldHistoryProjectionResult Project(
        GraphProjectionKey worldKey,
        long version,
        GraphBitemporalCoordinate coordinate,
        GraphWorldHistoryProjectionOptions? options = null,
        CancellationToken cancellationToken = default) =>
        GraphWorldHistoryProjector.Project(
            new GraphWorldHistoryProjector.ProjectParameters
            {
                WorldKey = worldKey,
                Version = version,
                ValidAt = coordinate.ValidAt,
                RecordedAt = coordinate.RecordedAt,
                History = _history,
                Options = options,
                CancellationToken = cancellationToken
            });

    /// <summary>
    /// Resolves and compares two temporal coordinates from the exact same captured evidence dataset.
    /// </summary>
    /// <param name="worldKey">Stable world key.</param>
    /// <param name="firstVersion">Output version for the first world.</param>
    /// <param name="firstCoordinate">First valid/recorded coordinate.</param>
    /// <param name="secondVersion">Output version for the second world.</param>
    /// <param name="secondCoordinate">Second valid/recorded coordinate.</param>
    /// <returns>The two worlds, difference and temporal-axis classification.</returns>
    public GraphBitemporalComparisonResult Compare(
        GraphProjectionKey worldKey,
        long firstVersion,
        GraphBitemporalCoordinate firstCoordinate,
        long secondVersion,
        GraphBitemporalCoordinate secondCoordinate) =>
        Compare(
            new CompareParameters
            {
                WorldKey = worldKey,
                FirstVersion = firstVersion,
                FirstCoordinate = firstCoordinate,
                SecondVersion = secondVersion,
                SecondCoordinate = secondCoordinate
            });

    /// <summary>
    /// Resolves and compares two temporal coordinates from the exact same captured evidence dataset.
    /// </summary>
    /// <param name="inputs">Comparison coordinates, versions, options and cancellation.</param>
    /// <returns>The two worlds, difference and temporal-axis classification.</returns>
    public GraphBitemporalComparisonResult Compare(CompareParameters inputs)
    {
        var worldKey = inputs.WorldKey;
        var firstVersion = inputs.FirstVersion;
        var firstCoordinate = inputs.FirstCoordinate;
        var secondVersion = inputs.SecondVersion;
        var secondCoordinate = inputs.SecondCoordinate;
        var projectionOptions = inputs.ProjectionOptions;
        var diffOptions = inputs.DiffOptions;
        var cancellationToken = inputs.CancellationToken;

        var first = Project(worldKey, firstVersion, firstCoordinate, projectionOptions, cancellationToken);
        var second = Project(worldKey, secondVersion, secondCoordinate, projectionOptions, cancellationToken);
        var kind = Classify(firstCoordinate, secondCoordinate);
        return new GraphBitemporalComparisonResult
        {
            Kind = kind,
            First = first,
            Second = second,
            Difference = GraphWorldSnapshotDiffer.Compare(first.Snapshot, second.Snapshot, diffOptions, cancellationToken),
            Explanation = kind switch
            {
                GraphBitemporalComparisonKind.SameCoordinate => "Both worlds represent the same valid and recorded time.",
                GraphBitemporalComparisonKind.ValidTimeEvolution =>
                    "Business-valid time changed while recorded knowledge remained fixed.",
                GraphBitemporalComparisonKind.RecordedKnowledgeCorrection =>
                    "Recorded knowledge changed for the same represented business instant.",
                _ => "Both business-valid time and recorded knowledge changed."
            }
        };
    }

    private static GraphBitemporalComparisonKind Classify(GraphBitemporalCoordinate first, GraphBitemporalCoordinate second)
    {
        var validChanged = first.ValidAt != second.ValidAt;
        var recordedChanged = first.RecordedAt != second.RecordedAt;
        return (validChanged, recordedChanged) switch
        {
            (false, false) => GraphBitemporalComparisonKind.SameCoordinate,
            (true, false) => GraphBitemporalComparisonKind.ValidTimeEvolution,
            (false, true) => GraphBitemporalComparisonKind.RecordedKnowledgeCorrection,
            _ => GraphBitemporalComparisonKind.Mixed
        };
    }

    private static GraphHistoryEnvelope Clone(GraphHistoryEnvelope source) =>
        new()
        {
            EntityType = source.EntityType,
            EntityId = source.EntityId,
            OperationKind = source.OperationKind,
            CapturedAtUtc = source.CapturedAtUtc,
            ValidFromUtc = source.ValidFromUtc,
            ValidToUtc = source.ValidToUtc,
            IsEdge = source.IsEdge,
            FromId = source.FromId,
            ToId = source.ToId,
            Snapshot = GraphHistorySnapshotCloner.Clone(source.Snapshot, source.EntityType),
            QueryAsOfUtc = source.QueryAsOfUtc
        };

    /// <summary>
    /// Groups the inputs for Compare.
    /// </summary>
    public sealed class CompareParameters
    {
        /// <summary>
        /// Gets or initializes worldKey.
        /// </summary>
        public required GraphProjectionKey WorldKey { get; init; }

        /// <summary>
        /// Gets or initializes firstVersion.
        /// </summary>
        public required long FirstVersion { get; init; }

        /// <summary>
        /// Gets or initializes firstCoordinate.
        /// </summary>
        public required GraphBitemporalCoordinate FirstCoordinate { get; init; }

        /// <summary>
        /// Gets or initializes secondVersion.
        /// </summary>
        public required long SecondVersion { get; init; }

        /// <summary>
        /// Gets or initializes secondCoordinate.
        /// </summary>
        public required GraphBitemporalCoordinate SecondCoordinate { get; init; }

        /// <summary>
        /// Gets or initializes projectionOptions.
        /// </summary>
        public GraphWorldHistoryProjectionOptions? ProjectionOptions { get; init; } = null;

        /// <summary>
        /// Gets or initializes diffOptions.
        /// </summary>
        public GraphWorldDiffOptions? DiffOptions { get; init; } = null;

        /// <summary>
        /// Gets or initializes cancellationToken.
        /// </summary>
        public CancellationToken CancellationToken { get; init; } = default;
    }
}