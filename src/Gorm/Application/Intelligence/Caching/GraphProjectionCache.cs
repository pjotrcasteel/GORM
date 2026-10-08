using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Caching;

/// <summary>
/// Provides a bounded, version-aware and concurrency-safe cache for immutable graph projection snapshots.
/// </summary>
public sealed class GraphProjectionCache
{
    private readonly Lock _gate = new();
    private readonly Dictionary<GraphProjectionKey, CacheEntry> _entries = [];
    private readonly Dictionary<GraphProjectionKey, Task<LoadOutcome>> _inFlightLoads = [];
    private readonly Dictionary<GraphProjectionKey, long> _generations = [];
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _timeToLive;
    private readonly int _maximumEntries;
    private readonly int _maximumRefreshAttempts;
    private long _accessSequence;
    private long _generationSequence;
    private long _hits;
    private long _misses;
    private long _loads;
    private long _coalescedLoads;
    private long _expirations;
    private long _invalidations;
    private long _evictions;
    private long _incrementalUpdates;
    private long _fullRebuilds;
    private long _rejectedLoads;

    /// <summary>
    /// Initializes a graph projection cache.
    /// </summary>
    public GraphProjectionCache(GraphProjectionCacheOptions? options = null, TimeProvider? timeProvider = null)
    {
        options ??= new GraphProjectionCacheOptions();
        ValidateOptions(options);
        _timeToLive = options.TimeToLive;
        _maximumEntries = options.MaximumEntries;
        _maximumRefreshAttempts = options.MaximumRefreshAttempts;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Gets the number of currently unexpired cached keys.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                RemoveExpiredEntriesLocked(_timeProvider.GetUtcNow());
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// Gets cache diagnostics without exposing internal mutable entries.
    /// </summary>
    public GraphProjectionCacheStatistics Statistics
    {
        get
        {
            var entryCount = Count;
            return new GraphProjectionCacheStatistics
            {
                EntryCount = entryCount,
                Hits = Interlocked.Read(ref _hits),
                Misses = Interlocked.Read(ref _misses),
                Loads = Interlocked.Read(ref _loads),
                CoalescedLoads = Interlocked.Read(ref _coalescedLoads),
                Expirations = Interlocked.Read(ref _expirations),
                Invalidations = Interlocked.Read(ref _invalidations),
                Evictions = Interlocked.Read(ref _evictions),
                IncrementalUpdates = Interlocked.Read(ref _incrementalUpdates),
                FullRebuilds = Interlocked.Read(ref _fullRebuilds),
                RejectedLoads = Interlocked.Read(ref _rejectedLoads)
            };
        }
    }

    /// <summary>
    /// Gets an unexpired snapshot at or above a minimum source version, loading it once for concurrent callers when needed.
    /// </summary>
    /// <remarks>
    /// Cancelling one waiter does not cancel a coalesced source load that may still serve other callers.
    /// </remarks>
    public Task<GraphProjectionCacheResult> GetOrCreateAsync(
        GraphProjectionKey key,
        long minimumVersion,
        Func<CancellationToken, Task<GraphProjectionSnapshot>> loadFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(loadFactory);

        if (minimumVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumVersion), minimumVersion, "A minimum projection version cannot be negative.");
        }
        return GetOrCreateCoreAsync(key, minimumVersion, loadFactory, cancellationToken);
    }

    private async Task<GraphProjectionCacheResult> GetOrCreateCoreAsync(
        GraphProjectionKey key,
        long minimumVersion,
        Func<CancellationToken, Task<GraphProjectionSnapshot>> loadFactory,
        CancellationToken cancellationToken)
    {
        var recordedMiss = false;
        var wasCoalesced = false;

        for (var attempt = 0; attempt < _maximumRefreshAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var loadAttempt = PrepareLoadAttempt(key, minimumVersion, recordedMiss);
            if (loadAttempt.Hit is not null)
            {
                return loadAttempt.Hit;
            }

            recordedMiss = true;
            wasCoalesced |= loadAttempt.WasCoalesced;
            if (loadAttempt.Owner is not null)
            {
                _ = CompleteLoadAsync(key, minimumVersion, loadFactory, loadAttempt.CapturedGeneration, loadAttempt.HadEntry, loadAttempt.Owner);
            }

            var sharedLoad = loadAttempt.SharedLoad ??
                throw new InvalidOperationException("A projection cache miss must provide a shared load operation.");
            var outcome = await sharedLoad.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (outcome.Superseded)
            {
                continue;
            }

            var outcomeSnapshot = outcome.Snapshot ?? throw new GraphProjectionCacheException(
                GraphProjectionCacheFailureReason.MissingSnapshot,
                $"Projection cache key '{key}' completed without a snapshot.");
            if (outcomeSnapshot.Version < minimumVersion)
            {
                continue;
            }

            if (!IsCurrentGeneration(key, outcome.Generation))
            {
                continue;
            }

            return new GraphProjectionCacheResult
            {
                Snapshot = outcomeSnapshot,
                Status = outcome.Status,
                WasCoalesced = wasCoalesced
            };
        }

        throw new GraphProjectionCacheException(
            GraphProjectionCacheFailureReason.RefreshLimitExceeded,
            $"Projection cache key '{key}' changed repeatedly and did not stabilize within {_maximumRefreshAttempts} refresh attempts.");
    }

    private LoadAttempt PrepareLoadAttempt(GraphProjectionKey key, long minimumVersion, bool recordedMiss)
    {
        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            var hadEntry = _entries.ContainsKey(key);
            TryGetLiveEntryLocked(key, now, out var entry);
            if (entry is not null && entry.Snapshot.Version >= minimumVersion)
            {
                TouchEntryLocked(entry);
                Interlocked.Increment(ref _hits);
                return LoadAttempt.FromHit(entry.Snapshot);
            }

            if (!recordedMiss)
            {
                Interlocked.Increment(ref _misses);
            }

            if (_inFlightLoads.TryGetValue(key, out var inFlight))
            {
                Interlocked.Increment(ref _coalescedLoads);
                return new LoadAttempt(null, null, inFlight, 0, hadEntry, WasCoalesced: true);
            }

            var owner = new TaskCompletionSource<LoadOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            var capturedGeneration = GetGenerationLocked(key);
            _inFlightLoads.Add(key, owner.Task);
            return new LoadAttempt(null, owner, owner.Task, capturedGeneration, hadEntry, WasCoalesced: false);
        }
    }

    private bool IsCurrentGeneration(GraphProjectionKey key, long generation)
    {
        lock (_gate)
        {
            return GetGenerationLocked(key) == generation;
        }
    }

    /// <summary>
    /// Attempts to get an unexpired cached snapshot at or above a minimum source version.
    /// </summary>
    public bool TryGet(GraphProjectionKey key, long minimumVersion, out GraphProjectionSnapshot? snapshot)
    {
        ArgumentNullException.ThrowIfNull(key);

        lock (_gate)
        {
            if (TryGetLiveEntryLocked(key, _timeProvider.GetUtcNow(), out var entry) &&
                entry!.Snapshot.Version >= minimumVersion)
            {
                TouchEntryLocked(entry);
                snapshot = entry.Snapshot;
                Interlocked.Increment(ref _hits);
                return true;
            }
        }

        snapshot = null;
        Interlocked.Increment(ref _misses);
        return false;
    }

    /// <summary>
    /// Invalidates one key and prevents an already-running older load from publishing afterwards.
    /// </summary>
    public bool Invalidate(GraphProjectionKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        lock (_gate)
        {
            var removedEntry = _entries.Remove(key);
            var hasInFlightLoad = _inFlightLoads.ContainsKey(key);
            var affected = removedEntry || hasInFlightLoad;
            if (affected)
            {
                IncrementGenerationLocked(key);
                Interlocked.Increment(ref _invalidations);
            }

            PruneGenerationLocked(key);

            return affected;
        }
    }

    /// <summary>
    /// Invalidates every cached or in-flight key. In-flight loads may finish but cannot publish their stale result.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            var keys = _entries.Keys.Concat(_inFlightLoads.Keys).Distinct().ToArray();
            foreach (var key in keys)
            {
                IncrementGenerationLocked(key);
            }

            var affected = keys.Length;
            _entries.Clear();
            foreach (var key in keys)
            {
                PruneGenerationLocked(key);
            }

            if (affected > 0)
            {
                Interlocked.Add(ref _invalidations, affected);
            }
        }
    }

    /// <summary>
    /// Applies a graph delta to a cached base snapshot, or uses the explicit rebuild factory when no compatible base exists.
    /// </summary>
    public Task<GraphProjectionUpdateResult> ApplyDeltaOrRebuildAsync(
        GraphProjectionDelta delta,
        Func<CancellationToken, Task<GraphProjectionSnapshot>> rebuildFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delta);
        ArgumentNullException.ThrowIfNull(rebuildFactory);
        cancellationToken.ThrowIfCancellationRequested();
        return ApplyDeltaOrRebuildCoreAsync(delta, rebuildFactory, cancellationToken);
    }

    private async Task<GraphProjectionUpdateResult> ApplyDeltaOrRebuildCoreAsync(
        GraphProjectionDelta delta,
        Func<CancellationToken, Task<GraphProjectionSnapshot>> rebuildFactory,
        CancellationToken cancellationToken)
    {
        GraphProjectionSnapshot? cachedSnapshot;
        long capturedGeneration;

        lock (_gate)
        {
            TryGetLiveEntryLocked(delta.Key, _timeProvider.GetUtcNow(), out var entry);
            cachedSnapshot = entry?.Snapshot;
            capturedGeneration = GetGenerationLocked(delta.Key);
        }

        if (cachedSnapshot is null)
        {
            var loaded = await GetOrCreateAsync(delta.Key, delta.TargetVersion, rebuildFactory, cancellationToken).ConfigureAwait(false);
            Interlocked.Increment(ref _fullRebuilds);
            return CreateFullRebuildResult(loaded.Snapshot, delta.BaseVersion, $"No unexpired cached base snapshot was available for delta '{delta.Key}'.");
        }

        var update = await cachedSnapshot.ApplyDeltaOrRebuildAsync(delta, rebuildFactory, cancellationToken).ConfigureAwait(false);

        lock (_gate)
        {
            if (GetGenerationLocked(delta.Key) != capturedGeneration ||
                !_entries.TryGetValue(delta.Key, out var currentEntry) ||
                !ReferenceEquals(currentEntry.Snapshot, cachedSnapshot))
            {
                throw new GraphProjectionCacheException(
                    GraphProjectionCacheFailureReason.ConcurrentModification,
                    $"Projection cache key '{delta.Key}' changed while its delta was being applied; the result was not published.");
            }

            PublishEntryLocked(update.Snapshot, _timeProvider.GetUtcNow());
        }

        ref var updateCounter = ref (update.Mode == GraphProjectionUpdateMode.Incremental
            ? ref _incrementalUpdates
            : ref _fullRebuilds);
        Interlocked.Increment(ref updateCounter);

        return update;
    }

    /// <summary>
    /// Gets deterministic read-only information for all currently unexpired entries.
    /// </summary>
    public IReadOnlyList<GraphProjectionCacheEntryInfo> Entries
    {
        get
        {
            lock (_gate)
            {
                RemoveExpiredEntriesLocked(_timeProvider.GetUtcNow());
                return [.. _entries.Values
                    .OrderBy(entry => entry.Snapshot.Key.Value, StringComparer.Ordinal)
                    .Select(entry => new GraphProjectionCacheEntryInfo
                    {
                        Key = entry.Snapshot.Key,
                        Version = entry.Snapshot.Version,
                        ExpiresAt = entry.ExpiresAt,
                        NodeCount = entry.Snapshot.Projection.Statistics.NodeCount,
                        EdgeCount = entry.Snapshot.Projection.Statistics.EdgeCount
                    })];
            }
        }
    }

    private async Task CompleteLoadAsync(
        GraphProjectionKey key,
        long minimumVersion,
        Func<CancellationToken, Task<GraphProjectionSnapshot>> loadFactory,
        long capturedGeneration,
        bool hadEntry,
        TaskCompletionSource<LoadOutcome> completion)
    {
        Interlocked.Increment(ref _loads);
        try
        {
            var loaded = await loadFactory(CancellationToken.None).ConfigureAwait(false);
            ValidateLoadedSnapshot(key, minimumVersion, loaded);

            LoadOutcome outcome;
            lock (_gate)
            {
                if (GetGenerationLocked(key) != capturedGeneration)
                {
                    outcome = LoadOutcome.CreateSuperseded();
                }
                else
                {
                    var now = _timeProvider.GetUtcNow();
                    if (TryGetLiveEntryLocked(key, now, out var current) &&
                        current!.Snapshot.Version > loaded.Version)
                    {
                        TouchEntryLocked(current);
                        outcome = new LoadOutcome(current.Snapshot, GraphProjectionCacheResultStatus.Hit, GetGenerationLocked(key), false);
                    }
                    else
                    {
                        PublishEntryLocked(loaded, now);
                        outcome = new LoadOutcome(
                            loaded,
                            hadEntry ? GraphProjectionCacheResultStatus.Refreshed : GraphProjectionCacheResultStatus.Loaded,
                            GetGenerationLocked(key),
                            false);
                    }
                }
            }

            completion.TrySetResult(outcome);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
        finally
        {
            lock (_gate)
            {
                if (_inFlightLoads.TryGetValue(key, out var registered) &&
                    ReferenceEquals(registered, completion.Task))
                {
                    _inFlightLoads.Remove(key);
                    PruneGenerationLocked(key);
                }
            }
        }
    }

    private void ValidateLoadedSnapshot(GraphProjectionKey expectedKey, long minimumVersion, GraphProjectionSnapshot? loaded)
    {
        if (loaded is null)
        {
            Interlocked.Increment(ref _rejectedLoads);
            throw new GraphProjectionCacheException(
                GraphProjectionCacheFailureReason.MissingSnapshot,
                $"The load factory for projection cache key '{expectedKey}' returned no snapshot.");
        }

        if (loaded.Key != expectedKey)
        {
            Interlocked.Increment(ref _rejectedLoads);
            throw new GraphProjectionCacheException(
                GraphProjectionCacheFailureReason.ProjectionKeyMismatch,
                $"The load factory returned key '{loaded.Key}' for requested cache key '{expectedKey}'.");
        }

        if (loaded.Version < minimumVersion)
        {
            Interlocked.Increment(ref _rejectedLoads);
            throw new GraphProjectionCacheException(
                GraphProjectionCacheFailureReason.StaleSnapshot,
                $"The load factory returned version {loaded.Version} for key '{expectedKey}', below required version {minimumVersion}.");
        }
    }

    private bool TryGetLiveEntryLocked(GraphProjectionKey key, DateTimeOffset now, out CacheEntry? entry)
    {
        if (!_entries.TryGetValue(key, out entry))
        {
            return false;
        }

        if (entry.ExpiresAt > now)
        {
            return true;
        }

        _entries.Remove(key);
        IncrementGenerationLocked(key);
        PruneGenerationLocked(key);
        Interlocked.Increment(ref _expirations);
        entry = null;
        return false;
    }

    private void RemoveExpiredEntriesLocked(DateTimeOffset now)
    {
        var expiredKeys = _entries.Where(pair => pair.Value.ExpiresAt <= now).Select(pair => pair.Key).ToArray();

        foreach (var key in expiredKeys)
        {
            _entries.Remove(key);
            IncrementGenerationLocked(key);
            PruneGenerationLocked(key);
            Interlocked.Increment(ref _expirations);
        }
    }

    private void PublishEntryLocked(GraphProjectionSnapshot snapshot, DateTimeOffset now)
    {
        if (!_entries.ContainsKey(snapshot.Key) && _entries.Count >= _maximumEntries)
        {
            var victim = _entries.Values.OrderBy(entry => entry.LastAccessSequence).ThenBy(entry => entry.Snapshot.Key.Value, StringComparer.Ordinal).First();
            _entries.Remove(victim.Snapshot.Key);
            IncrementGenerationLocked(victim.Snapshot.Key);
            PruneGenerationLocked(victim.Snapshot.Key);
            Interlocked.Increment(ref _evictions);
        }

        var expiresAt = CalculateExpiration(now);
        _entries[snapshot.Key] = new CacheEntry(snapshot, expiresAt, NextAccessSequenceLocked());
        IncrementGenerationLocked(snapshot.Key);
    }

    private void TouchEntryLocked(CacheEntry entry) => entry.LastAccessSequence = NextAccessSequenceLocked();

    private long NextAccessSequenceLocked() => ++_accessSequence;

    private long GetGenerationLocked(GraphProjectionKey key) =>
        _generations.TryGetValue(key, out var generation) ? generation : 0;

    private void IncrementGenerationLocked(GraphProjectionKey key) =>
        _generations[key] = checked(++_generationSequence);

    private void PruneGenerationLocked(GraphProjectionKey key)
    {
        if (!_entries.ContainsKey(key) && !_inFlightLoads.ContainsKey(key))
        {
            _generations.Remove(key);
        }
    }

    private DateTimeOffset CalculateExpiration(DateTimeOffset now)
    {
        if (_timeToLive == Timeout.InfiniteTimeSpan || DateTimeOffset.MaxValue - now <= _timeToLive)
        {
            return DateTimeOffset.MaxValue;
        }

        return now.Add(_timeToLive);
    }

    private static GraphProjectionUpdateResult CreateFullRebuildResult(GraphProjectionSnapshot snapshot, long baseVersion, string reason) =>
        new()
        {
            BaseVersion = baseVersion,
            Mode = GraphProjectionUpdateMode.FullRebuild,
            Snapshot = snapshot,
            AffectedNodeIds = [.. snapshot.Projection.Nodes.Select(node => node.Id).Order()],
            AffectedEdgeIds = [.. snapshot.Projection.Edges.Select(edge => edge.Id).Order()],
            Explanation = $"{reason} Used an explicit full rebuild at version {snapshot.Version}."
        };

    private static void ValidateOptions(GraphProjectionCacheOptions options)
    {
        if (options.TimeToLive != Timeout.InfiniteTimeSpan && options.TimeToLive <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.TimeToLive, "Projection cache time-to-live must be positive or infinite.");
        }

        if (options.MaximumEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MaximumEntries, "Projection cache maximum entries must be greater than zero.");
        }

        if (options.MaximumRefreshAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MaximumRefreshAttempts, "Projection cache maximum refresh attempts must be greater than zero.");
        }
    }

    private sealed class CacheEntry(GraphProjectionSnapshot snapshot, DateTimeOffset expiresAt, long lastAccessSequence)
    {
        public GraphProjectionSnapshot Snapshot { get; } = snapshot;

        public DateTimeOffset ExpiresAt { get; } = expiresAt;

        public long LastAccessSequence { get; set; } = lastAccessSequence;
    }

    private sealed record LoadAttempt(
        GraphProjectionCacheResult? Hit,
        TaskCompletionSource<LoadOutcome>? Owner,
        Task<LoadOutcome>? SharedLoad,
        long CapturedGeneration,
        bool HadEntry,
        bool WasCoalesced)
    {
        public static LoadAttempt FromHit(GraphProjectionSnapshot snapshot) =>
            new(
                new GraphProjectionCacheResult
                {
                    Snapshot = snapshot,
                    Status = GraphProjectionCacheResultStatus.Hit,
                    WasCoalesced = false
                },
                null,
                null,
                0,
                HadEntry: true,
                WasCoalesced: false);
    }

    private sealed record LoadOutcome(GraphProjectionSnapshot? Snapshot, GraphProjectionCacheResultStatus Status, long Generation, bool Superseded)
    {
        public static LoadOutcome CreateSuperseded() =>
            new(null, GraphProjectionCacheResultStatus.Refreshed, -1, true);
    }
}