using System.Runtime.CompilerServices;
using Gorm.Application.Intelligence.Caching;
using Gorm.Application.Intelligence.Live;
using Gorm.Application.Intelligence.Projection;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Intelligence.Projection;

[TestClass]
public sealed class GraphLiveProjectionTests
{
    private static readonly GraphProjectionKey Key = new("live:services");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WatchAsync_AppliesOrderedDeltasAndYieldsCommittedCursors()
    {
        var first = CreateNode(1);
        var second = CreateNode(2);
        var third = CreateNode(3);
        var initial = Snapshot(1, [first]);
        var cache = await SeedCacheAsync(initial, TestContext.CancellationToken);
        var feed = new GraphChannelProjectionChangeFeed();
        await feed.PublishAsync(
            new GraphProjectionChange(1, new GraphProjectionDeltaBuilder(Key, 1, 2).AddNodes([second]).Build()),
            TestContext.CancellationToken);
        await feed.PublishAsync(
            new GraphProjectionChange(2, new GraphProjectionDeltaBuilder(Key, 2, 3).AddNodes([third]).Build()),
            TestContext.CancellationToken);
        feed.Complete(Key);

        var updates = await CollectAsync(new GraphLiveProjectionEngine(cache).WatchAsync(
            Key,
            feed,
            _ => throw new InvalidOperationException("Ordered compatible deltas must not rebuild."),
            cancellationToken: TestContext.CancellationToken),
            TestContext.CancellationToken);

        CollectionAssert.AreEqual(new long[] { 1, 2 }, updates.Select(update => update.Cursor.Sequence).ToArray());
        CollectionAssert.AreEqual(new long[] { 2, 3 }, updates.Select(update => update.ProjectionUpdate.Snapshot.Version).ToArray());
        Assert.IsTrue(updates.All(update => update.ProjectionUpdate.Mode == GraphProjectionUpdateMode.Incremental));
        Assert.IsTrue(cache.TryGet(Key, 3, out var current));
        Assert.HasCount(3, current!.Projection.Nodes);
    }

    [TestMethod]
    public async Task WatchAsync_WithoutCachedBase_UsesExplicitRebuild()
    {
        var cache = new GraphProjectionCache();
        var feed = new GraphChannelProjectionChangeFeed();
        var rebuilt = Snapshot(2, [CreateNode(1), CreateNode(2)]);
        await feed.PublishAsync(
            new GraphProjectionChange(1, new GraphProjectionDeltaBuilder(Key, 1, 2).Build()),
            TestContext.CancellationToken);
        feed.Complete(Key);

        var updates = await CollectAsync(
            new GraphLiveProjectionEngine(cache).WatchAsync(
                Key,
                feed,
                _ => Task.FromResult(rebuilt),
                cancellationToken: TestContext.CancellationToken),
            TestContext.CancellationToken);

        Assert.HasCount(1, updates);
        Assert.AreEqual(GraphProjectionUpdateMode.FullRebuild, updates[0].ProjectionUpdate.Mode);
        Assert.AreSame(rebuilt, updates[0].ProjectionUpdate.Snapshot);
    }

    [TestMethod]
    public async Task WatchAsync_WithRegressingCursor_ThrowsBeforeSecondDelta()
    {
        var cache = await SeedCacheAsync(Snapshot(1, [CreateNode(1)]), TestContext.CancellationToken);
        var firstDelta = new GraphProjectionDeltaBuilder(Key, 1, 2).Build();
        var feed = new EnumerableChangeFeed(new GraphProjectionChange(2, firstDelta), new GraphProjectionChange(1, new GraphProjectionDeltaBuilder(Key, 2, 3).Build()));
        GraphChangeFeedOrderException? exception = null;

        try
        {
            await CollectAsync(
                new GraphLiveProjectionEngine(cache).WatchAsync(
                    Key,
                    feed,
                    _ => throw new InvalidOperationException(),
                    cancellationToken: TestContext.CancellationToken),
                TestContext.CancellationToken);
        }
        catch (GraphChangeFeedOrderException caught)
        {
            exception = caught;
        }

        Assert.IsNotNull(exception);
        Assert.AreEqual(2, exception.PreviousSequence);
        Assert.AreEqual(1, exception.ReceivedSequence);
        Assert.IsTrue(cache.TryGet(Key, 2, out _));
    }

    [TestMethod]
    public async Task WatchAsync_WithWrongDeltaKey_RejectsChange()
    {
        var wrongKey = new GraphProjectionKey("live:wrong");
        var feed = new EnumerableChangeFeed(new GraphProjectionChange(1, new GraphProjectionDeltaBuilder(wrongKey, 1, 2).Build()));
        var cache = new GraphProjectionCache();
        ArgumentException? exception = null;

        try
        {
            await CollectAsync(
                new GraphLiveProjectionEngine(cache).WatchAsync(
                    Key,
                    feed,
                    _ => Task.FromResult(Snapshot(2, [])),
                    cancellationToken: TestContext.CancellationToken),
                TestContext.CancellationToken);
        }
        catch (ArgumentException caught)
        {
            exception = caught;
        }

        Assert.IsNotNull(exception);
        Assert.AreEqual(0, cache.Count);
    }

    [TestMethod]
    public async Task WatchAsync_WhenChangeLimitIsExceeded_ThrowsWithLastCursorCommitted()
    {
        var cache = await SeedCacheAsync(Snapshot(1, [CreateNode(1)]), TestContext.CancellationToken);
        var feed = new EnumerableChangeFeed(
            new GraphProjectionChange(1, new GraphProjectionDeltaBuilder(Key, 1, 2).Build()),
            new GraphProjectionChange(2, new GraphProjectionDeltaBuilder(Key, 2, 3).Build()));
        var engine = new GraphLiveProjectionEngine(
            cache,
            new GraphLiveProjectionOptions { MaximumChanges = 1 });
        GraphLiveChangeLimitException? exception = null;

        try
        {
            await CollectAsync(
                engine.WatchAsync(
                    Key,
                    feed,
                    _ => throw new InvalidOperationException(),
                    cancellationToken: TestContext.CancellationToken),
                TestContext.CancellationToken);
        }
        catch (GraphLiveChangeLimitException caught)
        {
            exception = caught;
        }

        Assert.IsNotNull(exception);
        Assert.AreEqual(1, exception.Limit);
        Assert.IsTrue(cache.TryGet(Key, 2, out _));
        Assert.IsFalse(cache.TryGet(Key, 3, out _));
    }

    [TestMethod]
    public async Task ChannelFeed_WithResumeCursor_SkipsAlreadyCommittedChanges()
    {
        var cache = await SeedCacheAsync(Snapshot(2, [CreateNode(1)]), TestContext.CancellationToken);
        var feed = new GraphChannelProjectionChangeFeed();
        await feed.PublishAsync(
            new GraphProjectionChange(1, new GraphProjectionDeltaBuilder(Key, 1, 2).Build()),
            TestContext.CancellationToken);
        await feed.PublishAsync(
            new GraphProjectionChange(2, new GraphProjectionDeltaBuilder(Key, 2, 3).Build()),
            TestContext.CancellationToken);
        feed.Complete(Key);

        var updates = await CollectAsync(
            new GraphLiveProjectionEngine(cache).WatchAsync(
                Key,
                feed,
                _ => throw new InvalidOperationException(),
                new GraphChangeFeedCursor(1),
                TestContext.CancellationToken),
            TestContext.CancellationToken);

        Assert.HasCount(1, updates);
        Assert.AreEqual(2, updates[0].Cursor.Sequence);
        Assert.AreEqual(3, updates[0].ProjectionUpdate.Snapshot.Version);
    }

    [TestMethod]
    public async Task WatchAsync_WhenCancelled_StopsWithoutPublishingChange()
    {
        var cache = await SeedCacheAsync(Snapshot(1, [CreateNode(1)]), TestContext.CancellationToken);
        var feed = new GraphChannelProjectionChangeFeed();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var wasCancelled = false;

        try
        {
            await CollectAsync(
                new GraphLiveProjectionEngine(cache).WatchAsync(
                    Key,
                    feed,
                    _ => throw new InvalidOperationException(),
                    cancellationToken: cancellation.Token),
                cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            wasCancelled = true;
        }

        Assert.IsTrue(wasCancelled);
        Assert.IsTrue(cache.TryGet(Key, 1, out _));
    }

    private static async Task<GraphProjectionCache> SeedCacheAsync(GraphProjectionSnapshot snapshot, CancellationToken cancellationToken)
    {
        var cache = new GraphProjectionCache();
        await cache.GetOrCreateAsync(snapshot.Key, snapshot.Version, _ => Task.FromResult(snapshot), cancellationToken);
        return cache;
    }

    private static async Task<List<GraphLiveProjectionUpdate>> CollectAsync(IAsyncEnumerable<GraphLiveProjectionUpdate> source, CancellationToken cancellationToken)
    {
        var result = new List<GraphLiveProjectionUpdate>();
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            result.Add(item);
        }

        return result;
    }

    private static GraphProjectionSnapshot Snapshot(long version, IReadOnlyList<LiveNode> nodes) =>
        new(Key, version, GraphProjection.Create(nodes, []));

    private static LiveNode CreateNode(int identifier) => new()
    {
        Id = Guid.Parse($"00000000-0000-0000-0000-{identifier:D12}")
    };

    private sealed class EnumerableChangeFeed(params GraphProjectionChange[] changes) : IGraphProjectionChangeFeed
    {
        public async IAsyncEnumerable<GraphProjectionChange> ReadChangesAsync(
            GraphProjectionKey key,
            GraphChangeFeedCursor? after = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            foreach (var change in changes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (change.Cursor.Sequence > (after?.Sequence ?? 0))
                {
                    yield return change;
                }
            }
        }
    }

    private sealed class LiveNode : Node;
}