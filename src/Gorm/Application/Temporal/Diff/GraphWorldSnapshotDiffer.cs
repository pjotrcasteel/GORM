using System.Security.Cryptography;
using Gorm.Application.Temporal.Snapshots;

namespace Gorm.Application.Temporal.Diff;

/// <summary>
/// Calculates deterministic entity-level differences between immutable temporal worlds.
/// </summary>
public static class GraphWorldSnapshotDiffer
{
    /// <summary>
    /// Compares two worlds with the same logical key.
    /// </summary>
    /// <param name="before">Older world.</param>
    /// <param name="after">Newer world.</param>
    /// <param name="options">Comparison safety limits.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deterministic world difference.</returns>
    public static GraphWorldSnapshotDiff Compare(
        GraphWorldSnapshot before,
        GraphWorldSnapshot after,
        GraphWorldDiffOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        options ??= new GraphWorldDiffOptions();
        if (options.MaximumChanges <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaximumChanges must be positive.");
        }

        if (before.Identity.WorldKey != after.Identity.WorldKey)
        {
            throw new ArgumentException("Temporal worlds with different world keys cannot be compared.", nameof(after));
        }

        var changes = new List<GraphWorldEntityChange>();
        CompareNodes(before, after, changes, options.MaximumChanges, cancellationToken);
        CompareEdges(before, after, changes, options.MaximumChanges, cancellationToken);

        return new GraphWorldSnapshotDiff(before.Identity, after.Identity, [.. changes]);
    }

    private static void CompareNodes(
        GraphWorldSnapshot before,
        GraphWorldSnapshot after,
        List<GraphWorldEntityChange> changes,
        int maximumChanges,
        CancellationToken cancellationToken)
    {
        var beforeById = before.Nodes.ToDictionary(state => state.Id);
        var afterById = after.Nodes.ToDictionary(state => state.Id);
        foreach (var id in beforeById.Keys.Union(afterById.Keys).Order())
        {
            cancellationToken.ThrowIfCancellationRequested();
            beforeById.TryGetValue(id, out var beforeState);
            afterById.TryGetValue(id, out var afterState);
            if (beforeState is not null && afterState is not null &&
                beforeState.EntityType == afterState.EntityType &&
                beforeState.Payload.SequenceEqual(afterState.Payload))
            {
                continue;
            }

            AddBounded(changes, new GraphWorldEntityChange
            {
                EntityKind = GraphWorldEntityKind.Node,
                ChangeKind = GetChangeKind(beforeState, afterState),
                EntityId = id,
                BeforeType = beforeState?.EntityType,
                AfterType = afterState?.EntityType,
                BeforeStateFingerprint = Fingerprint(beforeState),
                AfterStateFingerprint = Fingerprint(afterState)
            }, maximumChanges);
        }
    }

    private static void CompareEdges(
        GraphWorldSnapshot before,
        GraphWorldSnapshot after,
        List<GraphWorldEntityChange> changes,
        int maximumChanges,
        CancellationToken cancellationToken)
    {
        var beforeById = before.Edges.ToDictionary(state => state.Id);
        var afterById = after.Edges.ToDictionary(state => state.Id);
        foreach (var id in beforeById.Keys.Union(afterById.Keys).Order())
        {
            cancellationToken.ThrowIfCancellationRequested();
            beforeById.TryGetValue(id, out var beforeState);
            afterById.TryGetValue(id, out var afterState);
            if (beforeState is not null && afterState is not null &&
                beforeState.EntityType == afterState.EntityType &&
                beforeState.FromId == afterState.FromId &&
                beforeState.ToId == afterState.ToId &&
                beforeState.Payload.SequenceEqual(afterState.Payload))
            {
                continue;
            }

            AddBounded(changes, new GraphWorldEntityChange
            {
                EntityKind = GraphWorldEntityKind.Edge,
                ChangeKind = GetChangeKind(beforeState, afterState),
                EntityId = id,
                BeforeType = beforeState?.EntityType,
                AfterType = afterState?.EntityType,
                BeforeFromId = beforeState?.FromId,
                BeforeToId = beforeState?.ToId,
                AfterFromId = afterState?.FromId,
                AfterToId = afterState?.ToId,
                BeforeStateFingerprint = Fingerprint(beforeState),
                AfterStateFingerprint = Fingerprint(afterState)
            }, maximumChanges);
        }
    }

    private static GraphWorldChangeKind GetChangeKind<TState>(TState? before, TState? after)
        where TState : class
    {
        if (before is null)
        {
            return GraphWorldChangeKind.Added;
        }

        return after is null ? GraphWorldChangeKind.Removed : GraphWorldChangeKind.Modified;
    }

    private static string? Fingerprint(GraphWorldNodeState? state) =>
        state is null ? null : Fingerprint(state.Payload);

    private static string? Fingerprint(GraphWorldEdgeState? state) =>
        state is null ? null : Fingerprint(state.Payload);

    private static string Fingerprint(ReadOnlySpan<byte> payload) =>
        Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();

    private static void AddBounded(List<GraphWorldEntityChange> changes, GraphWorldEntityChange change, int maximumChanges)
    {
        if (changes.Count == maximumChanges)
        {
            throw new GraphWorldDiffLimitException(maximumChanges);
        }

        changes.Add(change);
    }
}