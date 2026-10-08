using Gorm.Application.Intelligence.Algorithms.Communities;
using Gorm.Application.Intelligence.Algorithms.Planning;
using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Temporal.Scenarios;
using Gorm.Application.Temporal.Scenarios.Analysis;
using Gorm.Application.Temporal.Snapshots;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Temporal.Scenarios;

[TestClass]
public sealed class GraphScenarioComparisonTests
{
    private static readonly Guid AlphaId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid BetaId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid GammaId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly DateTimeOffset Day1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Compare_CombinesTopologyCriticalPathCommunityImpactAndDomainMetrics()
    {
        var scenario = GraphScenario.Fork(Baseline(), new GraphScenarioId("beta-failure"))
            .Apply(GraphScenarioMutation.RemoveNode(BetaId, Day1.AddDays(1), Day1.AddDays(1)), TestContext.CancellationToken)
            .Scenario;
        var options = Options();

        var comparison = GraphScenarioComparer.Compare(scenario, options, TestContext.CancellationToken);

        Assert.AreEqual(3, comparison.Baseline.NodeCount);
        Assert.AreEqual(2, comparison.Scenario.NodeCount);
        Assert.AreEqual(3, comparison.Baseline.ImpactReachableNodeCount);
        Assert.AreEqual(1, comparison.Scenario.ImpactReachableNodeCount);
        Assert.IsNotNull(comparison.Baseline.CriticalPathDuration);
        Assert.IsNotNull(comparison.Scenario.CriticalPathDuration);
        Assert.IsNotNull(comparison.Baseline.CommunityCount);
        Assert.IsNotNull(comparison.Scenario.CommunityCount);
        Assert.AreEqual(-1d, Metric(comparison, "topology.nodes").Delta);
        Assert.AreEqual(-2d, Metric(comparison, "topology.edges").Delta);
        Assert.AreEqual(-9d, Metric(comparison, "domain.capacity").Delta);
        Assert.AreEqual(-2d, Metric(comparison, "impact.reachable-nodes").Delta);
    }

    [TestMethod]
    public void Compare_ExplainsUnavailableCriticalPathForCyclicScenario()
    {
        var reverse = new TestEdge
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000012"),
            FromId = GammaId,
            ToId = AlphaId,
            Capacity = 1
        };
        var scenario = GraphScenario.Fork(Baseline(), new GraphScenarioId("cycle")).Apply(GraphScenarioMutation.AddEdge(reverse, Day1.AddDays(1), Day1.AddDays(1)), TestContext.CancellationToken).Scenario;

        var comparison = GraphScenarioComparer.Compare(scenario, Options(), TestContext.CancellationToken);

        Assert.IsNotNull(comparison.Baseline.CriticalPathDuration);
        Assert.IsNull(comparison.Scenario.CriticalPathDuration);
        Assert.IsFalse(string.IsNullOrWhiteSpace(comparison.Scenario.CriticalPathUnavailableReason));
    }

    [TestMethod]
    public void Compare_RejectsInvalidCustomMetrics()
    {
        var scenario = GraphScenario.Fork(Baseline(), new GraphScenarioId("metrics"));
        var duplicate = new GraphScenarioComparisonOptions
        {
            AdditionalMetrics =
            [
                new GraphScenarioMetricDefinition { Name = "same", Selector = _ => 1 },
                new GraphScenarioMetricDefinition { Name = "same", Selector = _ => 2 }
            ]
        };
        Assert.ThrowsExactly<ArgumentException>(() => GraphScenarioComparer.Compare(scenario, duplicate, TestContext.CancellationToken));

        var nonFinite = new GraphScenarioComparisonOptions
        {
            AdditionalMetrics =
            [
                new GraphScenarioMetricDefinition { Name = "invalid", Selector = _ => double.NaN }
            ]
        };
        Assert.ThrowsExactly<InvalidOperationException>(() => GraphScenarioComparer.Compare(scenario, nonFinite, TestContext.CancellationToken));
    }

    [TestMethod]
    public void Compare_EnforcesImpactLimitAndCancellation()
    {
        var scenario = GraphScenario.Fork(Baseline(), new GraphScenarioId("limits"));
        Assert.ThrowsExactly<InvalidOperationException>(() => GraphScenarioComparer.Compare(
            scenario,
            new GraphScenarioComparisonOptions
            {
                ImpactSourceNodeId = AlphaId,
                MaximumImpactNodes = 1
            },
            TestContext.CancellationToken));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => GraphScenarioComparer.Compare(scenario, cancellationToken: cancellation.Token));
    }

    private static GraphScenarioComparisonOptions Options() =>
        new()
        {
            IncludeCriticalPath = true,
            CriticalPathOptions = new GraphCriticalPathOptions
            {
                DurationSelector = node => ((TestNode)node).Duration,
                LagSelector = _ => 0
            },
            IncludeCommunities = true,
            CommunityOptions = new GraphCommunityDetectionOptions { RandomSeed = 42 },
            ImpactSourceNodeId = AlphaId,
            AdditionalMetrics =
            [
                new GraphScenarioMetricDefinition
                {
                    Name = "domain.capacity",
                    Unit = "units",
                    Selector = projection => projection.Edges.Sum(edge => ((TestEdge)edge).Capacity)
                }
            ]
        };

    private static GraphScenarioMetricDelta Metric(GraphScenarioComparisonResult result, string name) =>
        result.Metrics.Single(metric => metric.Name == name);

    private static GraphWorldSnapshot Baseline() =>
        GraphWorldSnapshot.Capture(
            new GraphWorldSnapshot.CaptureParameters
            {
                WorldKey = new GraphProjectionKey("orders/eu"),
                Version = 1,
                ValidAt = Day1,
                RecordedAt = Day1,
                Nodes = [
                    new TestNode { Id = AlphaId, Name = "alpha", Duration = 1 },
                    new TestNode { Id = BetaId, Name = "beta", Duration = 2 },
                    new TestNode { Id = GammaId, Name = "gamma", Duration = 3 }
                ],
                Edges = [
                    new TestEdge
                    {
                        Id = Guid.Parse("00000000-0000-0000-0000-000000000010"),
                        FromId = AlphaId,
                        ToId = BetaId,
                        Capacity = 4
                    },
                    new TestEdge
                    {
                        Id = Guid.Parse("00000000-0000-0000-0000-000000000011"),
                        FromId = BetaId,
                        ToId = GammaId,
                        Capacity = 5
                    }
                ]
            });

    private sealed class TestNode : Node
    {
        public string Name { get; set; } = string.Empty;

        public double Duration { get; set; }
    }

    private sealed class TestEdge : Edge
    {
        public double Capacity { get; set; }
    }
}