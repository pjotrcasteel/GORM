using Gorm.Application.History;
using Gorm.Application.History.Envelopes;
using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Temporal.Snapshots;
using Gorm.Core.Primitives;

namespace Gorm.Application.Temporal.History;

/// <summary>
/// Projects existing provider-neutral GORM history envelopes into a bitemporal graph world.
/// </summary>
public static class GraphWorldHistoryProjector
{
    /// <summary>
    /// Resolves the state valid at <paramref name="validAt"/> using only knowledge captured no later than
    /// <paramref name="recordedAt"/>.
    /// </summary>
    /// <param name="worldKey">Stable source and scope key.</param>
    /// <param name="version">Monotonic world version.</param>
    /// <param name="validAt">Business-valid instant to resolve.</param>
    /// <param name="recordedAt">Knowledge cutoff used to resolve later corrections.</param>
    /// <param name="history">Node and edge envelopes obtained through ordinary GORM history queries.</param>
    /// <param name="options">Projection limits and inactive-operation semantics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved immutable world and selection evidence.</returns>
    public static GraphWorldHistoryProjectionResult Project(ProjectParameters inputs)
    {
        var worldKey = inputs.WorldKey;
        var version = inputs.Version;
        var validAt = inputs.ValidAt;
        var recordedAt = inputs.RecordedAt;
        var history = inputs.History;
        var options = inputs.Options;
        var cancellationToken = inputs.CancellationToken;

        ArgumentNullException.ThrowIfNull(worldKey);
        ArgumentNullException.ThrowIfNull(history);
        options ??= new GraphWorldHistoryProjectionOptions();
        ValidateOptions(options);

        var normalizedValidAt = validAt.ToUniversalTime();
        var normalizedRecordedAt = recordedAt.ToUniversalTime();
        var entries = MaterializeBounded(history, options.MaximumHistoryEntries, cancellationToken);
        var known = entries.Where(entry => EnsureUtc(entry.CapturedAtUtc) <= normalizedRecordedAt.UtcDateTime).ToArray();
        var applicable = known.Where(entry => IsValidAt(entry, normalizedValidAt.UtcDateTime)).ToArray();
        var selected = SelectLatestStates(applicable, cancellationToken);
        var nodes = new List<Node>();
        var edges = new List<Edge>();
        var inactiveCount = 0;

        foreach (var envelope in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateEnvelope(envelope);

            if (!IsActive(envelope, options))
            {
                inactiveCount++;
                continue;
            }

            if (envelope.IsEdge)
            {
                edges.Add((Edge)envelope.Snapshot);
            }
            else
            {
                nodes.Add((Node)envelope.Snapshot);
            }
        }

        var snapshot = GraphWorldSnapshot.Capture(
            new GraphWorldSnapshot.CaptureParameters
            {
                WorldKey = worldKey,
                Version = version,
                ValidAt = normalizedValidAt,
                RecordedAt = normalizedRecordedAt,
                Nodes = nodes,
                Edges = edges,
                CancellationToken = cancellationToken
            });

        return new GraphWorldHistoryProjectionResult
        {
            Snapshot = snapshot,
            TotalHistoryEntries = entries.Count,
            KnownHistoryEntries = known.Length,
            ApplicableHistoryEntries = applicable.Length,
            SelectedEntityStates = selected.Count,
            InactiveEntityStates = inactiveCount,
            Explanation =
                $"Resolved {snapshot.Nodes.Count} nodes and {snapshot.Edges.Count} edges at valid time " +
                $"{normalizedValidAt:O} using knowledge recorded through {normalizedRecordedAt:O}; " +
                $"{inactiveCount} selected deleted/disconnected states were excluded."
        };
    }

    private static List<GraphHistoryEnvelope> MaterializeBounded(IEnumerable<GraphHistoryEnvelope> history, int maximumEntries, CancellationToken cancellationToken)
    {
        var entries = new List<GraphHistoryEnvelope>();
        foreach (var entry in history)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(entry);

            if (entries.Count == maximumEntries)
            {
                throw new GraphWorldHistoryProjectionException(
                    GraphWorldHistoryProjectionFailureReason.EntryLimitExceeded,
                    $"History projection exceeded MaximumHistoryEntries ({maximumEntries}). " +
                    "Narrow the queried entity types or time range.");
            }

            entries.Add(entry);
        }

        return entries;
    }

    private static List<GraphHistoryEnvelope> SelectLatestStates(IEnumerable<GraphHistoryEnvelope> applicable, CancellationToken cancellationToken)
    {
        var selected = new List<GraphHistoryEnvelope>();
        var groups = applicable
            .GroupBy(entry => new EntityStateKey(entry.IsEdge, entry.EntityType, entry.EntityId))
            .OrderBy(group => group.Key.IsEdge)
            .ThenBy(group => group.Key.EntityType.FullName, StringComparer.Ordinal)
            .ThenBy(group => group.Key.EntityId);

        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ordered = group
                .OrderByDescending(entry => EnsureUtc(entry.CapturedAtUtc))
                .ThenByDescending(entry => EnsureUtc(entry.ValidFromUtc))
                .ThenByDescending(entry => entry.ValidToUtc.HasValue)
                .ToArray();
            var latest = ordered[0];

            if (ordered.Length > 1 &&
                EnsureUtc(ordered[1].CapturedAtUtc) == EnsureUtc(latest.CapturedAtUtc) &&
                EnsureUtc(ordered[1].ValidFromUtc) == EnsureUtc(latest.ValidFromUtc))
            {
                throw new GraphWorldHistoryProjectionException(
                    GraphWorldHistoryProjectionFailureReason.AmbiguousState,
                    $"Entity '{group.Key.EntityType.FullName}/{group.Key.EntityId}' has multiple equally recent " +
                    "history states for the requested temporal coordinate.");
            }

            selected.Add(latest);
        }

        return selected;
    }

    private static void ValidateEnvelope(GraphHistoryEnvelope envelope)
    {
        var expectedBaseType = envelope.IsEdge ? typeof(Edge) : typeof(Node);
        if (!expectedBaseType.IsAssignableFrom(envelope.EntityType) ||
            !envelope.EntityType.IsInstanceOfType(envelope.Snapshot))
        {
            throw InvalidEnvelope(envelope, "the entity type and captured snapshot are inconsistent");
        }

        if (envelope.IsEdge)
        {
            var edge = (Edge)envelope.Snapshot;
            if (edge.Id != envelope.EntityId ||
                (envelope.FromId is not null && envelope.FromId != edge.FromId) ||
                (envelope.ToId is not null && envelope.ToId != edge.ToId))
            {
                throw InvalidEnvelope(envelope, $"edge identifiers or endpoints do not match the {nameof(envelope)}");
            }

            return;
        }

        if (((Node)envelope.Snapshot).Id != envelope.EntityId)
        {
            throw InvalidEnvelope(envelope, $"node identifier does not match the {nameof(envelope)}");
        }
    }

    private static GraphWorldHistoryProjectionException InvalidEnvelope(GraphHistoryEnvelope envelope, string detail) =>
        new(
            GraphWorldHistoryProjectionFailureReason.InvalidEnvelope,
            $"History envelope '{envelope.EntityType.FullName}/{envelope.EntityId}' is invalid: {detail}.");

    private static bool IsValidAt(GraphHistoryEnvelope entry, DateTime validAtUtc)
    {
        var validFrom = EnsureUtc(entry.ValidFromUtc);
        DateTime? validTo = entry.ValidToUtc is null ? null : EnsureUtc(entry.ValidToUtc.Value);
        return validFrom <= validAtUtc && (validTo is null || validAtUtc < validTo.Value);
    }

    private static bool IsActive(GraphHistoryEnvelope envelope, GraphWorldHistoryProjectionOptions options) =>
        envelope.OperationKind switch
        {
            GraphHistoryOperationKind.Deleted when envelope.IsEdge => !options.DeletedEdgesAreInactive,
            GraphHistoryOperationKind.Deleted => !options.DeletedNodesAreInactive,
            GraphHistoryOperationKind.Disconnected when envelope.IsEdge => !options.DisconnectedEdgesAreInactive,
            _ => true
        };

    private static void ValidateOptions(GraphWorldHistoryProjectionOptions options)
    {
        if (options.MaximumHistoryEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MaximumHistoryEntries, "MaximumHistoryEntries must be positive.");
        }
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private readonly record struct EntityStateKey(bool IsEdge, Type EntityType, Guid EntityId);

    /// <summary>
    /// Groups the inputs for Project.
    /// </summary>
    public sealed class ProjectParameters
    {
        /// <summary>
        /// Gets or initializes worldKey.
        /// </summary>
        public required GraphProjectionKey WorldKey { get; init; }

        /// <summary>
        /// Gets or initializes version.
        /// </summary>
        public required long Version { get; init; }

        /// <summary>
        /// Gets or initializes validAt.
        /// </summary>
        public required DateTimeOffset ValidAt { get; init; }

        /// <summary>
        /// Gets or initializes recordedAt.
        /// </summary>
        public required DateTimeOffset RecordedAt { get; init; }

        /// <summary>
        /// Gets or initializes history.
        /// </summary>
        public required IEnumerable<GraphHistoryEnvelope> History { get; init; }

        /// <summary>
        /// Gets or initializes options.
        /// </summary>
        public GraphWorldHistoryProjectionOptions? Options { get; init; } = null;

        /// <summary>
        /// Gets or initializes cancellationToken.
        /// </summary>
        public CancellationToken CancellationToken { get; init; } = default;
    }
}