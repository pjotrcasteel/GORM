using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Temporal.Operations.Drift;
using Gorm.Application.Temporal.Snapshots;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Temporal.Operations;

[TestClass]
public sealed class GraphTwinDriftTests
{
    public TestContext TestContext { get; set; }

    private static readonly Guid AlphaId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid BetaId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Day1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Compare_ReturnsNoneForMatchingDetachedWorlds()
    {
        var expected = World(1, "alpha", includeBeta: true);
        var report = GraphTwinDriftDetector.Compare(expected, World(2, "alpha", includeBeta: true), cancellationToken: TestContext.CancellationToken);

        Assert.IsFalse(report.HasDrift);
        Assert.AreEqual(GraphTwinDriftSeverity.None, report.Severity);
        Assert.AreEqual(0d, report.EntityChangeRatio);
    }

    [TestMethod]
    public void Compare_ClassifiesPropertyAndTopologyDrift()
    {
        var options = new GraphTwinDriftOptions { HighChangeRatio = 0.75, CriticalChangeRatio = 1 };
        var property = GraphTwinDriftDetector.Compare(World(1, "alpha", includeBeta: true), World(2, "changed", includeBeta: true),
            options, cancellationToken: TestContext.CancellationToken);
        var topology = GraphTwinDriftDetector.Compare(World(1, "alpha", includeBeta: true), World(2, "alpha", includeBeta: false),
            options, cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(GraphTwinDriftSeverity.Low, property.Severity);
        Assert.IsFalse(property.Difference.TopologyChanged);
        Assert.AreEqual(GraphTwinDriftSeverity.High, topology.Severity);
        Assert.IsTrue(topology.Difference.TopologyChanged);
    }

    [TestMethod]
    public void Compare_RejectsDifferentWorldKeysAndInvalidRatios()
    {
        var differentKey = GraphWorldSnapshot.Capture(
            new GraphWorldSnapshot.CaptureParameters
            {
                WorldKey = new GraphProjectionKey("other/eu"),
                Version = 2,
                ValidAt = Day1,
                RecordedAt = Day1,
                Nodes = [],
                Edges = []
            });
        Assert.ThrowsExactly<ArgumentException>(() => GraphTwinDriftDetector.Compare(World(1, "alpha", true), differentKey, cancellationToken: TestContext.CancellationToken));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => GraphTwinDriftDetector.Compare(
            World(1, "alpha", true),
            World(2, "alpha", true),
            new GraphTwinDriftOptions { HighChangeRatio = 0.5, CriticalChangeRatio = 0.5 },
            cancellationToken: TestContext.CancellationToken));
    }

    [TestMethod]
    public void WatchAsync_EmitsOrderedDriftEvidence()
    {
        var results = ConsumeAsync(GraphTwinSynchronizer.WatchAsync(Observations(
            Observation(10, Day1, "alpha"),
            Observation(20, Day1.AddMinutes(1), "changed")),
            cancellationToken: TestContext.CancellationToken)).GetAwaiter().GetResult();

        CollectionAssert.AreEqual(new long[] { 10, 20 }, results.Select(result => result.Sequence).ToArray());
        Assert.IsFalse(results[0].Drift.HasDrift);
        Assert.IsTrue(results[1].Drift.HasDrift);
    }

    [TestMethod]
    public void WatchAsync_RejectsOrderingAndObservationOverflow()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => ConsumeAsync(GraphTwinSynchronizer.WatchAsync(
            Observations(
            Observation(2, Day1, "alpha"),
            Observation(1, Day1.AddMinutes(1), "alpha")),
            cancellationToken: TestContext.CancellationToken)).GetAwaiter().GetResult());

        Assert.ThrowsExactly<InvalidOperationException>(() => ConsumeAsync(GraphTwinSynchronizer.WatchAsync(
            Observations(Observation(1, Day1, "alpha"), Observation(2, Day1, "alpha")),
            new GraphTwinDriftOptions { MaximumObservations = 1 },
            cancellationToken: TestContext.CancellationToken)).GetAwaiter().GetResult());
    }

    [TestMethod]
    public void WatchAsync_ObservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => ConsumeAsync(GraphTwinSynchronizer.WatchAsync(
            Observations(Observation(1, Day1, "alpha")),
            cancellationToken: cancellation.Token)).GetAwaiter().GetResult());
    }

    private static GraphTwinObservation Observation(long sequence, DateTimeOffset observedAt, string actualName) =>
        new()
        {
            Sequence = sequence,
            ObservedAt = observedAt,
            Expected = World(sequence * 2, "alpha", includeBeta: true),
            Actual = World((sequence * 2) + 1, actualName, includeBeta: true)
        };

    private static async IAsyncEnumerable<GraphTwinObservation> Observations(params GraphTwinObservation[] observations)
    {
        foreach (var observation in observations)
        {
            await Task.Yield();
            yield return observation;
        }
    }

    private static async Task<List<GraphTwinSynchronizationResult>> ConsumeAsync(IAsyncEnumerable<GraphTwinSynchronizationResult> source)
    {
        var results = new List<GraphTwinSynchronizationResult>();
        await foreach (var item in source)
        {
            results.Add(item);
        }

        return results;
    }

    private static GraphWorldSnapshot World(long version, string alphaName, bool includeBeta) =>
        GraphWorldSnapshot.Capture(
            new GraphWorldSnapshot.CaptureParameters
            {
                WorldKey = new GraphProjectionKey("twin/eu"),
                Version = version,
                ValidAt = Day1,
                RecordedAt = Day1,
                Nodes = includeBeta
                ? [new TestNode { Id = AlphaId, Name = alphaName }, new TestNode { Id = BetaId, Name = "beta" }]
                : [new TestNode { Id = AlphaId, Name = alphaName }],
                Edges = []
            });

    private sealed class TestNode : Node
    {
        public string Name { get; set; } = string.Empty;
    }
}