using System.Runtime.CompilerServices;
using Gorm.Application.Intelligence.Caching;
using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Live;

/// <summary>
/// Applies ordered provider-neutral change-feed deltas to a version-aware projection cache.
/// </summary>
public sealed class GraphLiveProjectionEngine
{
    private readonly GraphProjectionCache _cache;
    private readonly GraphLiveProjectionOptions _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a live projection engine.
    /// </summary>
    public GraphLiveProjectionEngine(GraphProjectionCache cache, GraphLiveProjectionOptions? options = null, TimeProvider? timeProvider = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        var configuredOptions = options ?? new GraphLiveProjectionOptions();
        _options = new GraphLiveProjectionOptions
        {
            MaximumChanges = configuredOptions.MaximumChanges
        };
        _timeProvider = timeProvider ?? TimeProvider.System;

        if (_options.MaximumChanges <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum changes must be greater than zero.");
        }
    }

    /// <summary>
    /// Streams successfully committed projection updates in provider sequence order.
    /// </summary>
    public IAsyncEnumerable<GraphLiveProjectionUpdate> WatchAsync(
        GraphProjectionKey key,
        IGraphProjectionChangeFeed changeFeed,
        Func<CancellationToken, Task<GraphProjectionSnapshot>> rebuildFactory,
        GraphChangeFeedCursor? after = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(changeFeed);
        ArgumentNullException.ThrowIfNull(rebuildFactory);
        return WatchCoreAsync(key, changeFeed, rebuildFactory, after, cancellationToken);
    }

    private async IAsyncEnumerable<GraphLiveProjectionUpdate> WatchCoreAsync(
        GraphProjectionKey key,
        IGraphProjectionChangeFeed changeFeed,
        Func<CancellationToken, Task<GraphProjectionSnapshot>> rebuildFactory,
        GraphChangeFeedCursor? after,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var committedSequence = after?.Sequence ?? 0;
        var processedChanges = 0;

        await foreach (var change in changeFeed
            .ReadChangesAsync(key, after, cancellationToken)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (processedChanges >= _options.MaximumChanges)
            {
                throw new GraphLiveChangeLimitException(_options.MaximumChanges);
            }

            if (change.Cursor.Sequence <= committedSequence)
            {
                throw new GraphChangeFeedOrderException(committedSequence, change.Cursor.Sequence);
            }

            if (change.Delta.Key != key)
            {
                ThrowMismatchedKey(change, key, nameof(changeFeed));
            }

            var update = await _cache.ApplyDeltaOrRebuildAsync(change.Delta, rebuildFactory, cancellationToken).ConfigureAwait(false);
            committedSequence = change.Cursor.Sequence;
            processedChanges++;

            yield return new GraphLiveProjectionUpdate
            {
                Cursor = change.Cursor,
                ProjectionUpdate = update,
                ProcessedAt = _timeProvider.GetUtcNow()
            };
        }
    }

    private static void ThrowMismatchedKey(GraphProjectionChange change, GraphProjectionKey key, string parameterName) =>
        throw new ArgumentException($"Change-feed delta key '{change.Delta.Key}' does not match watched key '{key}'.", parameterName);
}