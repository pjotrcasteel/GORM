using Gorm.Application.Intelligence.Caching;
using Gorm.Application.Intelligence.Projection;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Intelligence.Caching;

[TestClass]
public sealed class GraphProjectionCacheTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task GetOrCreateAsync_ReusesUnexpiredMinimumVersion()
    {
        var key = new GraphProjectionKey("services:active");
        var cache = new GraphProjectionCache();
        var snapshot = CreateSnapshot(key, 7, 1);
        var loadCalls = 0;

        var first = await cache.GetOrCreateAsync(
            key,
            7,
            _ =>
            {
                loadCalls++;
                return Task.FromResult(snapshot);
            },
            TestContext.CancellationToken);
        var second = await cache.GetOrCreateAsync(
            key,
            5,
            _ => throw new InvalidOperationException("Cache hit must not invoke the factory."),
            TestContext.CancellationToken);

        Assert.AreEqual(1, loadCalls);
        Assert.AreEqual(GraphProjectionCacheResultStatus.Loaded, first.Status);
        Assert.AreEqual(GraphProjectionCacheResultStatus.Hit, second.Status);
        Assert.AreSame(snapshot, second.Snapshot);
    }

    [TestMethod]
    public async Task GetOrCreateAsync_CoalescesConcurrentLoadsForSameKey()
    {
        var key = new GraphProjectionKey("services:coalesced");
        var cache = new GraphProjectionCache();
        var snapshot = CreateSnapshot(key, 1, 1);
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<GraphProjectionSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var loadCalls = 0;

        Task<GraphProjectionSnapshot> Factory(CancellationToken _)
        {
            Interlocked.Increment(ref loadCalls);
            started.TrySetResult(true);
            return release.Task;
        }

        var calls = Enumerable.Range(0, 8)
            .Select(_ => cache.GetOrCreateAsync(key, 1, Factory, TestContext.CancellationToken))
            .ToArray();
        await started.Task;
        release.SetResult(snapshot);
        var results = await Task.WhenAll(calls);

        Assert.AreEqual(1, loadCalls);
        Assert.IsTrue(results.All(result => ReferenceEquals(snapshot, result.Snapshot)));
        Assert.IsGreaterThanOrEqualTo(7, results.Count(result => result.WasCoalesced));
        Assert.AreEqual(1, cache.Statistics.Loads);
    }

    [TestMethod]
    public async Task GetOrCreateAsync_AfterTtlExpiry_RefreshesSnapshot()
    {
        var key = new GraphProjectionKey("services:ttl");
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-17T10:00:00Z"));
        var cache = new GraphProjectionCache(
            new GraphProjectionCacheOptions { TimeToLive = TimeSpan.FromMinutes(5) },
            time);
        await cache.GetOrCreateAsync(
            key,
            1,
            _ => Task.FromResult(CreateSnapshot(key, 1, 1)),
            TestContext.CancellationToken);

        time.Advance(TimeSpan.FromMinutes(5));
        var refreshed = await cache.GetOrCreateAsync(
            key,
            1,
            _ => Task.FromResult(CreateSnapshot(key, 2, 2)),
            TestContext.CancellationToken);

        Assert.AreEqual(GraphProjectionCacheResultStatus.Refreshed, refreshed.Status);
        Assert.AreEqual(2, refreshed.Snapshot.Version);
        Assert.AreEqual(1, cache.Statistics.Expirations);
    }

    [TestMethod]
    public async Task GetOrCreateAsync_WithStaleFactorySnapshot_IsRejected()
    {
        var key = new GraphProjectionKey("services:minimum-version");
        var cache = new GraphProjectionCache();
        GraphProjectionCacheException? exception = null;

        try
        {
            await cache.GetOrCreateAsync(
                key,
                10,
                _ => Task.FromResult(CreateSnapshot(key, 9, 1)),
                TestContext.CancellationToken);
        }
        catch (GraphProjectionCacheException caught)
        {
            exception = caught;
        }

        Assert.IsNotNull(exception);
        Assert.AreEqual(GraphProjectionCacheFailureReason.StaleSnapshot, exception.Reason);
        Assert.AreEqual(0, cache.Count);
        Assert.AreEqual(1, cache.Statistics.RejectedLoads);
    }

    [TestMethod]
    public async Task Invalidate_DuringLoad_PreventsOlderLoadFromPublishingAndRetries()
    {
        var key = new GraphProjectionKey("services:race");
        var cache = new GraphProjectionCache();
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var loadCalls = 0;

        async Task<GraphProjectionSnapshot> Factory(CancellationToken _)
        {
            var call = Interlocked.Increment(ref loadCalls);
            if (call == 1)
            {
                firstStarted.TrySetResult(true);
                await releaseFirst.Task;
                return CreateSnapshot(key, 1, 1);
            }

            return CreateSnapshot(key, 2, 2);
        }

        var lookup = cache.GetOrCreateAsync(key, 1, Factory, TestContext.CancellationToken);
        await firstStarted.Task;
        Assert.IsTrue(cache.Invalidate(key));
        releaseFirst.SetResult(true);
        var result = await lookup;

        Assert.AreEqual(2, loadCalls);
        Assert.AreEqual(2, result.Snapshot.Version);
        Assert.IsTrue(cache.TryGet(key, 2, out var cached));
        Assert.AreSame(result.Snapshot, cached);
    }

    [TestMethod]
    public async Task GetOrCreateAsync_CancelledWaiter_DoesNotCancelSharedLoad()
    {
        var key = new GraphProjectionKey("services:cancellation");
        var cache = new GraphProjectionCache();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<GraphProjectionSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var cancelledLookup = cache.GetOrCreateAsync(
            key,
            1,
            _ =>
            {
                started.TrySetResult(true);
                return release.Task;
            },
            cancellation.Token);
        await started.Task;
        cancellation.Cancel();
        var wasCancelled = false;

        try
        {
            await cancelledLookup;
        }
        catch (OperationCanceledException)
        {
            wasCancelled = true;
        }

        release.SetResult(CreateSnapshot(key, 1, 1));
        var survivingLookup = await cache.GetOrCreateAsync(
            key,
            1,
            _ => throw new InvalidOperationException("The original shared load should complete."),
            TestContext.CancellationToken);

        Assert.IsTrue(wasCancelled);
        Assert.AreEqual(1, survivingLookup.Snapshot.Version);
    }

    [TestMethod]
    public async Task Capacity_UsesDeterministicLeastRecentlyUsedEviction()
    {
        var firstKey = new GraphProjectionKey("cache:1");
        var secondKey = new GraphProjectionKey("cache:2");
        var thirdKey = new GraphProjectionKey("cache:3");
        var cache = new GraphProjectionCache(new GraphProjectionCacheOptions { MaximumEntries = 2 });
        await cache.GetOrCreateAsync(
            firstKey,
            1,
            _ => Task.FromResult(CreateSnapshot(firstKey, 1, 1)),
            TestContext.CancellationToken);
        await cache.GetOrCreateAsync(
            secondKey,
            1,
            _ => Task.FromResult(CreateSnapshot(secondKey, 1, 1)),
            TestContext.CancellationToken);
        Assert.IsTrue(cache.TryGet(firstKey, 1, out _));

        await cache.GetOrCreateAsync(
            thirdKey,
            1,
            _ => Task.FromResult(CreateSnapshot(thirdKey, 1, 1)),
            TestContext.CancellationToken);

        Assert.IsTrue(cache.TryGet(firstKey, 1, out _));
        Assert.IsFalse(cache.TryGet(secondKey, 1, out _));
        Assert.IsTrue(cache.TryGet(thirdKey, 1, out _));
        Assert.AreEqual(1, cache.Statistics.Evictions);
    }

    [TestMethod]
    public async Task ApplyDeltaOrRebuildAsync_PublishesIncrementalSnapshotAtomically()
    {
        var key = new GraphProjectionKey("services:delta");
        var first = CreateNode(1);
        var second = CreateNode(2);
        var cache = new GraphProjectionCache();
        var initial = CreateSnapshot(key, 1, [first]);
        await cache.GetOrCreateAsync(key, 1, _ => Task.FromResult(initial), TestContext.CancellationToken);
        var delta = new GraphProjectionDeltaBuilder(key, 1, 2).AddNodes([second]).Build();

        var update = await cache.ApplyDeltaOrRebuildAsync(
            delta,
            _ => throw new InvalidOperationException("Compatible delta must not rebuild."),
            TestContext.CancellationToken);

        Assert.AreEqual(GraphProjectionUpdateMode.Incremental, update.Mode);
        Assert.IsTrue(cache.TryGet(key, 2, out var cached));
        Assert.HasCount(2, cached!.Projection.Nodes);
        Assert.AreEqual(1, cache.Statistics.IncrementalUpdates);
        Assert.HasCount(1, update.AffectedNodeIds);
    }

    [TestMethod]
    public async Task ApplyDeltaOrRebuildAsync_WithoutCachedBase_LoadsCurrentSnapshot()
    {
        var key = new GraphProjectionKey("services:missing-base");
        var cache = new GraphProjectionCache();
        var delta = new GraphProjectionDeltaBuilder(key, 4, 5).Build();
        var rebuilt = CreateSnapshot(key, 6, 2);

        var update = await cache.ApplyDeltaOrRebuildAsync(
            delta,
            _ => Task.FromResult(rebuilt),
            TestContext.CancellationToken);

        Assert.AreEqual(GraphProjectionUpdateMode.FullRebuild, update.Mode);
        Assert.AreSame(rebuilt, update.Snapshot);
        Assert.IsTrue(cache.TryGet(key, 6, out _));
        Assert.AreEqual(1, cache.Statistics.FullRebuilds);
    }

    [TestMethod]
    public async Task ApplyDeltaOrRebuildAsync_WhenInvalidatedDuringRebuild_RejectsPublication()
    {
        var key = new GraphProjectionKey("services:delta-race");
        var cache = new GraphProjectionCache();
        await cache.GetOrCreateAsync(
            key,
            1,
            _ => Task.FromResult(CreateSnapshot(key, 1, 1)),
            TestContext.CancellationToken);
        var incompatibleDelta = new GraphProjectionDeltaBuilder(key, 2, 3).Build();
        var rebuildStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRebuild = new TaskCompletionSource<GraphProjectionSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var update = cache.ApplyDeltaOrRebuildAsync(
            incompatibleDelta,
            _ =>
            {
                rebuildStarted.TrySetResult(true);
                return releaseRebuild.Task;
            },
            TestContext.CancellationToken);
        await rebuildStarted.Task;
        cache.Invalidate(key);
        releaseRebuild.SetResult(CreateSnapshot(key, 3, 2));
        GraphProjectionCacheException? exception = null;

        try
        {
            await update;
        }
        catch (GraphProjectionCacheException caught)
        {
            exception = caught;
        }

        Assert.IsNotNull(exception);
        Assert.AreEqual(GraphProjectionCacheFailureReason.ConcurrentModification, exception.Reason);
        Assert.IsFalse(cache.TryGet(key, 0, out _));
    }

    [TestMethod]
    public async Task GetOrCreateAsync_WithWrongFactoryKey_IsRejected()
    {
        var requestedKey = new GraphProjectionKey("services:requested");
        var wrongKey = new GraphProjectionKey("services:wrong");
        var cache = new GraphProjectionCache();
        GraphProjectionCacheException? exception = null;

        try
        {
            await cache.GetOrCreateAsync(
                requestedKey,
                1,
                _ => Task.FromResult(CreateSnapshot(wrongKey, 1, 1)),
                TestContext.CancellationToken);
        }
        catch (GraphProjectionCacheException caught)
        {
            exception = caught;
        }

        Assert.IsNotNull(exception);
        Assert.AreEqual(GraphProjectionCacheFailureReason.ProjectionKeyMismatch, exception.Reason);
        Assert.AreEqual(0, cache.Count);
    }

    private static GraphProjectionSnapshot CreateSnapshot(GraphProjectionKey key, long version, int nodeCount) =>
        CreateSnapshot(key, version, [.. Enumerable.Range(1, nodeCount).Select(CreateNode)]);

    private static GraphProjectionSnapshot CreateSnapshot(GraphProjectionKey key, long version, IReadOnlyList<CacheNode> nodes) =>
        new(key, version, GraphProjection.Create(nodes, []));

    private static CacheNode CreateNode(int identifier) => new()
    {
        Id = Guid.Parse($"00000000-0000-0000-0000-{identifier:D12}"),
        Name = $"node-{identifier}"
    };

    private sealed class CacheNode : Node
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class ManualTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = initialUtcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }
}