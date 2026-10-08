using System.Diagnostics;
using Gorm.Application.Diagnostics;
using Gorm.Application.Intelligence.Algorithms.Centrality;
using Gorm.Application.Intelligence.Caching;
using Gorm.Application.Intelligence.Diagnostics;
using Gorm.Application.Intelligence.Live;
using Gorm.Application.Intelligence.Output;
using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Intelligence.Registry;
using Gorm.Application.Intelligence.Runtime;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Intelligence.Runtime;

[TestClass]
public sealed class GraphIntelligenceRuntimeTests
{
    private static readonly GraphProjectionKey Key = new("runtime:test");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Execute_DoesNotInvokeOutputUntilExplicitDispatch()
    {
        var snapshot = Snapshot(1, [Node(1)]);
        var runtime = new GraphIntelligenceRuntime(GraphAlgorithmRegistry.CreateBuiltIn(), new GraphProjectionCache());
        var writeCount = 0;
        var dispatcher = new GraphIntelligenceOutputDispatcher(
        [
            new GraphDelegateIntelligenceOutputAdapter(
                "explicit-test",
                (_, _) =>
                {
                    writeCount++;
                    return ValueTask.CompletedTask;
                })
        ]);

        var execution = runtime.Execute(
            snapshot,
            new GraphAlgorithmInvocation("centrality.degree"),
            TestContext.CancellationToken);
        Assert.AreEqual(0, writeCount);

        var receipts = await dispatcher.DispatchAsync(execution, TestContext.CancellationToken);

        Assert.AreEqual(1, writeCount);
        Assert.HasCount(1, receipts);
        Assert.AreEqual("explicit-test", receipts[0].AdapterId);
    }

    [TestMethod]
    public void Execute_WithInvalidInvocation_ReturnsClassifiedFailureWithSnapshot()
    {
        var snapshot = Snapshot(2, [Node(1)]);
        var runtime = new GraphIntelligenceRuntime(GraphAlgorithmRegistry.CreateBuiltIn(), new GraphProjectionCache());
        GraphIntelligenceRuntimeException? failure = null;

        try
        {
            runtime.Execute(
                snapshot,
                new GraphAlgorithmInvocation("routing.shortest-path"),
                TestContext.CancellationToken);
        }
        catch (GraphIntelligenceRuntimeException exception)
        {
            failure = exception;
        }

        Assert.IsNotNull(failure);
        Assert.AreEqual(GraphIntelligenceFailureReason.InvalidInvocation, failure.Reason);
        Assert.AreEqual(snapshot.Metadata, failure.Snapshot);
    }

    [TestMethod]
    public void Execute_WhenAlgorithmObservesRuntimeTimeout_ReturnsTimedOutFailure()
    {
        var registry = new GraphAlgorithmRegistry().Register(
            new GraphAlgorithmDescriptor(
                new GraphAlgorithmDescriptor.GraphAlgorithmDescriptorParameters
                {
                    Id = "custom.wait",
                    DisplayName = "Wait",
                    Description = "Waits for cancellation.",
                    Category = GraphAlgorithmCategory.Custom,
                    ResultType = typeof(int)
                }
            ),
            (_, _, cancellationToken) =>
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Thread.SpinWait(1_000);
                }
            });
        var runtime = new GraphIntelligenceRuntime(
            registry,
            new GraphProjectionCache(),
            new GraphIntelligenceRuntimeOptions { ExecutionTimeout = TimeSpan.FromMilliseconds(20) });
        GraphIntelligenceRuntimeException? failure = null;

        try
        {
            runtime.Execute(
                Snapshot(1, [Node(1)]),
                new GraphAlgorithmInvocation("custom.wait"),
                TestContext.CancellationToken);
        }
        catch (GraphIntelligenceRuntimeException exception)
        {
            failure = exception;
        }

        Assert.IsNotNull(failure);
        Assert.AreEqual(GraphIntelligenceFailureReason.TimedOut, failure.Reason);
    }

    [TestMethod]
    public void Execute_WithCallerCancellation_PreservesOperationCanceledException()
    {
        var runtime = new GraphIntelligenceRuntime(GraphAlgorithmRegistry.CreateBuiltIn(), new GraphProjectionCache());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = false;

        try
        {
            runtime.Execute(Snapshot(1, [Node(1)]), new GraphAlgorithmInvocation("centrality.degree"), cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        Assert.IsTrue(cancelled);
    }

    [TestMethod]
    public async Task WatchAsync_YieldsCursorSnapshotAndAlgorithmProvenanceTogether()
    {
        var cache = await SeedAsync(Snapshot(1, [Node(1)]), TestContext.CancellationToken);
        var feed = new GraphChannelProjectionChangeFeed();
        await feed.PublishAsync(
            new GraphProjectionChange(11, new GraphProjectionDeltaBuilder(Key, 1, 2).AddNodes([Node(2)]).Build()),
            TestContext.CancellationToken);
        await feed.PublishAsync(
            new GraphProjectionChange(12, new GraphProjectionDeltaBuilder(Key, 2, 3).AddNodes([Node(3)]).Build()),
            TestContext.CancellationToken);
        feed.Complete(Key);
        var runtime = new GraphIntelligenceRuntime(GraphAlgorithmRegistry.CreateBuiltIn(), cache);

        var updates = await CollectAsync(
            runtime.WatchAsync(
                Key,
                feed,
                _ => throw new InvalidOperationException(),
                _ => new GraphAlgorithmInvocation("centrality.degree"),
                cancellationToken: TestContext.CancellationToken),
            TestContext.CancellationToken);

        CollectionAssert.AreEqual(new long[] { 11, 12 }, updates.Select(update => update.Cursor.Sequence).ToArray());
        CollectionAssert.AreEqual(new long[] { 2, 3 }, updates.Select(update => update.Execution.Snapshot.Version).ToArray());
        Assert.IsTrue(updates.All(update => ReferenceEquals(update.Execution.Snapshot, update.ProjectionUpdate.Snapshot.Metadata)));
        Assert.HasCount(3, updates[1].Execution.AsResult<GraphDegreeCentralityResult>()!.Scores);
    }

    [TestMethod]
    public async Task WatchAsync_WhenLimitExceeded_DoesNotApplyUnexecutedDelta()
    {
        var cache = await SeedAsync(Snapshot(1, [Node(1)]), TestContext.CancellationToken);
        var feed = new GraphChannelProjectionChangeFeed();
        await feed.PublishAsync(
            new GraphProjectionChange(1, new GraphProjectionDeltaBuilder(Key, 1, 2).Build()),
            TestContext.CancellationToken);
        await feed.PublishAsync(
            new GraphProjectionChange(2, new GraphProjectionDeltaBuilder(Key, 2, 3).Build()),
            TestContext.CancellationToken);
        feed.Complete(Key);
        var runtime = new GraphIntelligenceRuntime(
            GraphAlgorithmRegistry.CreateBuiltIn(),
            cache,
            new GraphIntelligenceRuntimeOptions { MaximumLiveExecutions = 1 });
        GraphIntelligenceRuntimeException? failure = null;

        try
        {
            await CollectAsync(
                runtime.WatchAsync(
                    Key,
                    feed,
                    _ => throw new InvalidOperationException(),
                    _ => new GraphAlgorithmInvocation("centrality.degree"),
                    cancellationToken: TestContext.CancellationToken),
                TestContext.CancellationToken);
        }
        catch (GraphIntelligenceRuntimeException exception)
        {
            failure = exception;
        }

        Assert.IsNotNull(failure);
        Assert.AreEqual(GraphIntelligenceFailureReason.LiveExecutionLimitExceeded, failure.Reason);
        Assert.IsTrue(cache.TryGet(Key, 2, out _));
        Assert.IsFalse(cache.TryGet(Key, 3, out _));
    }

    [TestMethod]
    public async Task ChannelOutput_StreamsOnlyExplicitlyDispatchedResult()
    {
        var channel = new GraphChannelIntelligenceOutputAdapter("studio-stream", capacity: 1);
        var dispatcher = new GraphIntelligenceOutputDispatcher([channel]);
        var runtime = new GraphIntelligenceRuntime(GraphAlgorithmRegistry.CreateBuiltIn(), new GraphProjectionCache());
        var execution = runtime.Execute(
            Snapshot(1, [Node(1)]),
            new GraphAlgorithmInvocation("centrality.degree"),
            TestContext.CancellationToken);

        await dispatcher.DispatchAsync(execution, TestContext.CancellationToken);
        channel.Complete();
        var streamed = new List<GraphAlgorithmExecutionResult>();
        await foreach (var item in channel.ReadAllAsync(TestContext.CancellationToken))
        {
            streamed.Add(item);
        }

        Assert.HasCount(1, streamed);
        Assert.AreSame(execution, streamed[0]);
    }

    [TestMethod]
    public async Task OutputFailure_IsClassifiedWithAdapterAndSnapshot()
    {
        var runtime = new GraphIntelligenceRuntime(GraphAlgorithmRegistry.CreateBuiltIn(), new GraphProjectionCache());
        var execution = runtime.Execute(
            Snapshot(1, [Node(1)]),
            new GraphAlgorithmInvocation("centrality.degree"),
            TestContext.CancellationToken);
        var dispatcher = new GraphIntelligenceOutputDispatcher([new GraphDelegateIntelligenceOutputAdapter("broken", (_, _) => throw new IOException("Output unavailable."))]);
        GraphIntelligenceRuntimeException? failure = null;

        try
        {
            await dispatcher.DispatchAsync(execution, TestContext.CancellationToken);
        }
        catch (GraphIntelligenceRuntimeException exception)
        {
            failure = exception;
        }

        Assert.IsNotNull(failure);
        Assert.AreEqual(GraphIntelligenceFailureReason.OutputAdapterFailed, failure.Reason);
        Assert.AreEqual("broken", failure.AdapterId);
        Assert.AreEqual(execution.Snapshot, failure.Snapshot);
    }

    [TestMethod]
    public async Task RuntimeAndOutput_EmitExistingGormActivitiesWithStableTags()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == GormDiagnostics.ActivitySourceName,
            Sample = (ref _) => ActivitySamplingResult.AllData,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);
        var runtime = new GraphIntelligenceRuntime(GraphAlgorithmRegistry.CreateBuiltIn(), new GraphProjectionCache());
        var execution = runtime.Execute(
            Snapshot(5, [Node(1)]),
            new GraphAlgorithmInvocation("centrality.degree"),
            TestContext.CancellationToken);
        var dispatcher = new GraphIntelligenceOutputDispatcher([new GraphDelegateIntelligenceOutputAdapter("diagnostic", (_, _) => ValueTask.CompletedTask)]);

        await dispatcher.DispatchAsync(execution, TestContext.CancellationToken);

        var runtimeActivity = activities.Single(activity => activity.OperationName == GormIntelligenceDiagnostics.ActivityNames.Runtime);
        var outputActivity = activities.Single(activity => activity.OperationName == GormIntelligenceDiagnostics.ActivityNames.Output);
        Assert.AreEqual("centrality.degree", runtimeActivity.GetTagItem(GormIntelligenceDiagnostics.TagNames.AlgorithmId));
        Assert.AreEqual(5L, runtimeActivity.GetTagItem(GormIntelligenceDiagnostics.TagNames.ProjectionVersion));
        Assert.AreEqual("diagnostic", outputActivity.GetTagItem(GormIntelligenceDiagnostics.TagNames.OutputAdapter));
    }

    private static async Task<GraphProjectionCache> SeedAsync(GraphProjectionSnapshot snapshot, CancellationToken cancellationToken)
    {
        var cache = new GraphProjectionCache();
        await cache.GetOrCreateAsync(snapshot.Key, snapshot.Version, _ => Task.FromResult(snapshot), cancellationToken);
        return cache;
    }

    private static async Task<List<GraphLiveAlgorithmUpdate>> CollectAsync(IAsyncEnumerable<GraphLiveAlgorithmUpdate> source, CancellationToken cancellationToken)
    {
        var result = new List<GraphLiveAlgorithmUpdate>();
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            result.Add(item);
        }

        return result;
    }

    private static GraphProjectionSnapshot Snapshot(long version, IReadOnlyList<RuntimeNode> nodes) =>
        new(Key, version, GraphProjection.Create(nodes, []));

    private static RuntimeNode Node(int identifier) => new()
    {
        Id = Guid.Parse($"00000000-0000-0000-0000-{identifier:D12}")
    };

    private sealed class RuntimeNode : Node;
}