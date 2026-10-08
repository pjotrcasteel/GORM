using Gorm.Application.Intelligence;
using Gorm.Application.Intelligence.Algorithms.Connectivity;
using Gorm.Application.Intelligence.Algorithms.Pathfinding;
using Gorm.Application.Intelligence.Projection;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Intelligence.Algorithms;

[TestClass]
public sealed class GraphIntelligenceAlgorithmTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void PageRank_RanksNodeWithMultipleIncomingEdgesHighest()
    {
        var first = CreateNode("first");
        var important = CreateNode("important");
        var third = CreateNode("third");
        var projection = CreateProjection([first, important, third], [CreateEdge(first, important), CreateEdge(third, important)]);

        var result = projection.PageRank(cancellationToken: TestContext.CancellationToken);

        Assert.IsTrue(result.Converged);
        Assert.AreEqual(important.Id, result.Scores[0].Node.Id);
        Assert.AreEqual(1, result.Scores.Sum(score => score.Score), 0.0000001);
        Assert.IsGreaterThan(result.GetScore(first.Id), result.GetScore(important.Id));
    }

    [TestMethod]
    public void DegreeCentrality_CalculatesIncomingOutgoingAndNormalizedScores()
    {
        var first = CreateNode("first");
        var hub = CreateNode("hub");
        var third = CreateNode("third");
        var projection = CreateProjection([first, hub, third], [CreateEdge(first, hub), CreateEdge(hub, third), CreateEdge(hub, first)]);

        var result = projection.DegreeCentrality(cancellationToken: TestContext.CancellationToken);
        var hubScore = result.Scores.Single(score => score.Node.Id == hub.Id);

        Assert.AreEqual(1, hubScore.IncomingDegree);
        Assert.AreEqual(2, hubScore.OutgoingDegree);
        Assert.AreEqual(0.75, hubScore.TotalCentrality);
        Assert.AreEqual(hub.Id, result.Scores[0].Node.Id);
    }

    [TestMethod]
    public void ConnectedComponents_FindsWeakAndStrongComponents()
    {
        var first = CreateNode("first");
        var second = CreateNode("second");
        var third = CreateNode("third");
        var fourth = CreateNode("fourth");
        var isolated = CreateNode("isolated");
        var projection = CreateProjection(
            [first, second, third, fourth, isolated],
            [
                CreateEdge(first, second),
                CreateEdge(second, first),
                CreateEdge(second, third),
                CreateEdge(third, fourth),
                CreateEdge(fourth, third)
            ]);

        var weak = projection.ConnectedComponents(cancellationToken: TestContext.CancellationToken);
        var strong = projection.ConnectedComponents(GraphComponentKind.Strong, cancellationToken: TestContext.CancellationToken);

        Assert.HasCount(2, weak.Components);
        Assert.HasCount(4, weak.Components[0].Nodes);
        Assert.HasCount(3, strong.Components);
        CollectionAssert.AreEquivalent(
            new[] { first.Id, second.Id },
            strong.Components.Single(component => component.Nodes.Any(node => node.Id == first.Id))
                .Nodes.Select(node => node.Id).ToArray());
    }

    [TestMethod]
    public void ShortestPath_UsesWeightsAndReturnsOrderedEntities()
    {
        var start = CreateNode("start");
        var fast = CreateNode("fast");
        var slow = CreateNode("slow");
        var destination = CreateNode("destination");
        var startToFast = CreateEdge(start, fast, cost: 1);
        var fastToDestination = CreateEdge(fast, destination, cost: 1);
        var startToSlow = CreateEdge(start, slow, cost: 0.5);
        var slowToDestination = CreateEdge(slow, destination, cost: 10);
        var projection = CreateProjection([start, fast, slow, destination], [startToFast, fastToDestination, startToSlow, slowToDestination]);

        var result = projection.ShortestPath(
            start.Id,
            destination.Id,
            new GraphShortestPathOptions
            {
                WeightSelector = edge => ((IntelligenceEdge)edge).Cost
            },
            TestContext.CancellationToken);

        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(
            new[] { start.Id, fast.Id, destination.Id },
            result.Nodes.Select(node => node.Id).ToArray());
        CollectionAssert.AreEqual(
            new[] { startToFast.Id, fastToDestination.Id },
            result.Edges.Select(edge => edge.Id).ToArray());
        Assert.AreEqual(2, result.TotalWeight);
    }

    [TestMethod]
    public void ShortestPath_WithEdgePredicate_ExcludesEdges()
    {
        var start = CreateNode("start");
        var blocked = CreateNode("blocked");
        var destination = CreateNode("destination");
        var blockedEdge = CreateEdge(start, blocked, cost: 1, isAvailable: false);
        var availableEdge = CreateEdge(blocked, destination);
        var projection = CreateProjection([start, blocked, destination], [blockedEdge, availableEdge]);

        var result = projection.ShortestPath(
            start.Id,
            destination.Id,
            new GraphShortestPathOptions
            {
                EdgePredicate = edge => ((IntelligenceEdge)edge).IsAvailable
            },
            TestContext.CancellationToken);

        Assert.IsNull(result);
    }

    private static GraphProjection CreateProjection(IEnumerable<IntelligenceNode> nodes, IEnumerable<IntelligenceEdge> edges) =>
        GraphProjection.Create(nodes, edges);

    private static IntelligenceNode CreateNode(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name
    };

    private static IntelligenceEdge CreateEdge(IntelligenceNode from, IntelligenceNode to, double cost = 1, bool isAvailable = true) => new()
    {
        Id = Guid.NewGuid(),
        FromId = from.Id,
        ToId = to.Id,
        Cost = cost,
        IsAvailable = isAvailable
    };

    private sealed class IntelligenceNode : Node
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class IntelligenceEdge : Edge
    {
        public double Cost { get; set; }

        public bool IsAvailable { get; set; }
    }
}