using Gorm.Application.Intelligence;
using Gorm.Application.Intelligence.Algorithms.Centrality;
using Gorm.Application.Intelligence.Algorithms.Connectivity;
using Gorm.Application.Intelligence.Algorithms.Planning;
using Gorm.Application.Intelligence.Projection;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Intelligence.Algorithms;

[TestClass]
public sealed class GraphAdvancedIntelligenceAlgorithmTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void BetweennessCentrality_RanksBridgeNodeHighest()
    {
        var first = CreateNode("first");
        var bridge = CreateNode("bridge");
        var last = CreateNode("last");
        var projection = CreateProjection([first, bridge, last], [CreateEdge(first, bridge), CreateEdge(bridge, last)]);

        var result = projection.BetweennessCentrality(cancellationToken: TestContext.CancellationToken);
        var bridgeScore = result.GetScore(bridge.Id);

        Assert.AreEqual(bridge.Id, result.Scores[0].Node.Id);
        Assert.AreEqual(1, bridgeScore.Score);
        Assert.AreEqual(0.5, bridgeScore.NormalizedScore);
        Assert.AreEqual(0, result.GetScore(first.Id).Score);
    }

    [TestMethod]
    public void BetweennessCentrality_WithWeights_UsesOnlyLowestCostRoutes()
    {
        var source = CreateNode("source");
        var fastBridge = CreateNode("fast");
        var slowBridge = CreateNode("slow");
        var destination = CreateNode("destination");
        var projection = CreateProjection(
            [source, fastBridge, slowBridge, destination],
            [
                CreateEdge(source, fastBridge, cost: 1),
                CreateEdge(fastBridge, destination, cost: 1),
                CreateEdge(source, slowBridge, cost: 1),
                CreateEdge(slowBridge, destination, cost: 5)
            ]);

        var result = projection.BetweennessCentrality(new GraphBetweennessCentralityOptions
        {
            WeightSelector = edge => ((IntelligenceEdge)edge).Cost
        },
        TestContext.CancellationToken);

        Assert.IsGreaterThan(result.GetScore(slowBridge.Id).Score, result.GetScore(fastBridge.Id).Score);
        Assert.AreEqual(0, result.GetScore(slowBridge.Id).Score);
    }

    [TestMethod]
    public void BetweennessCentrality_WithParallelShortestPaths_SplitsDependencyEqually()
    {
        var source = CreateNode("source");
        var firstBridge = CreateNode("first");
        var secondBridge = CreateNode("second");
        var destination = CreateNode("destination");
        var projection = CreateProjection(
            [source, firstBridge, secondBridge, destination],
            [
                CreateEdge(source, firstBridge),
                CreateEdge(source, secondBridge),
                CreateEdge(firstBridge, destination),
                CreateEdge(secondBridge, destination)
            ]);

        var result = projection.BetweennessCentrality(cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(0.5, result.GetScore(firstBridge.Id).Score, 0.000_000_000_001);
        Assert.AreEqual(0.5, result.GetScore(secondBridge.Id).Score, 0.000_000_000_001);
    }

    [TestMethod]
    public void DetectCycles_FindsSimpleCyclesAndSelfLoop()
    {
        var first = CreateNode("first");
        var second = CreateNode("second");
        var third = CreateNode("third");
        var projection = CreateProjection([first, second, third], [CreateEdge(first, second), CreateEdge(second, first), CreateEdge(second, third), CreateEdge(third, third)]);

        var result = projection.DetectCycles(cancellationToken: TestContext.CancellationToken);

        Assert.HasCount(2, result.Cycles);
        Assert.IsTrue(result.HasCycles);
        Assert.IsTrue(result.Cycles.Any(cycle => cycle.Length == 1 && cycle.Nodes[0].Id == third.Id));
        Assert.IsTrue(result.Cycles.Any(cycle => cycle.Length == 2));
    }

    [TestMethod]
    public void DetectCycles_WhenCycleLimitIsReached_MarksResultAsTruncated()
    {
        var first = CreateNode("first");
        var second = CreateNode("second");
        var third = CreateNode("third");
        var projection = CreateProjection([first, second, third], [CreateEdge(first, second), CreateEdge(second, first), CreateEdge(second, third), CreateEdge(third, second)]);

        var result = projection.DetectCycles(new GraphCycleDetectionOptions { MaximumCycles = 1 }, TestContext.CancellationToken);

        Assert.HasCount(1, result.Cycles);
        Assert.IsTrue(result.Truncated);
    }

    [TestMethod]
    public void CriticalPath_CalculatesScheduleSlackAndCriticalEdges()
    {
        var first = CreateNode("A", duration: 3);
        var criticalMiddle = CreateNode("B", duration: 4);
        var nonCriticalMiddle = CreateNode("C", duration: 2);
        var last = CreateNode("D", duration: 5);
        var firstToCritical = CreateEdge(first, criticalMiddle, lag: 0);
        var firstToNonCritical = CreateEdge(first, nonCriticalMiddle, lag: 0);
        var criticalToLast = CreateEdge(criticalMiddle, last, lag: 0);
        var nonCriticalToLast = CreateEdge(nonCriticalMiddle, last, lag: 0);
        var projection = CreateProjection([first, criticalMiddle, nonCriticalMiddle, last], [firstToCritical, firstToNonCritical, criticalToLast, nonCriticalToLast]);

        var result = projection.CriticalPath(new GraphCriticalPathOptions
        {
            DurationSelector = node => ((IntelligenceNode)node).Duration,
            LagSelector = edge => ((IntelligenceEdge)edge).Lag
        },
        TestContext.CancellationToken);

        Assert.AreEqual(12, result.ProjectDuration);
        Assert.AreEqual(2, result.GetNodeSchedule(nonCriticalMiddle.Id).Slack);
        Assert.IsTrue(result.GetNodeSchedule(criticalMiddle.Id).IsCritical);
        CollectionAssert.AreEqual(new[] { first.Id, criticalMiddle.Id, last.Id }, result.Nodes.Select(node => node.Id).ToArray());
        CollectionAssert.AreEquivalent(new[] { firstToCritical.Id, criticalToLast.Id }, result.CriticalEdges.Select(edge => edge.Id).ToArray());
        Assert.AreEqual(2, result.HopCount);
    }

    [TestMethod]
    public void CriticalPath_WhenProjectionContainsCycle_ThrowsSpecificException()
    {
        var first = CreateNode("first");
        var second = CreateNode("second");
        var projection = CreateProjection([first, second], [CreateEdge(first, second), CreateEdge(second, first)]);

        var exception = Assert.ThrowsExactly<GraphCycleDetectedException>(() => projection.CriticalPath(cancellationToken: TestContext.CancellationToken));

        Assert.HasCount(2, exception.RemainingNodeIds);
    }

    private static GraphProjection CreateProjection(IEnumerable<IntelligenceNode> nodes, IEnumerable<IntelligenceEdge> edges) =>
        GraphProjection.Create(nodes, edges);

    private static IntelligenceNode CreateNode(string name, double duration = 0) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Duration = duration
    };

    private static IntelligenceEdge CreateEdge(IntelligenceNode from, IntelligenceNode to, double cost = 1, double lag = 1) => new()
    {
        Id = Guid.NewGuid(),
        FromId = from.Id,
        ToId = to.Id,
        Cost = cost,
        Lag = lag
    };

    private sealed class IntelligenceNode : Node
    {
        public string Name { get; set; } = string.Empty;

        public double Duration { get; set; }
    }

    private sealed class IntelligenceEdge : Edge
    {
        public double Cost { get; set; }

        public double Lag { get; set; }
    }
}