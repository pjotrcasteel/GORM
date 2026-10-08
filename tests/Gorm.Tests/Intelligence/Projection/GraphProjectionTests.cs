using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Application.Intelligence.Projection;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Intelligence.Projection;

[TestClass]
public sealed class GraphProjectionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Create_BuildsDenseIncomingAndOutgoingIndexes()
    {
        var first = CreateNode("first");
        var second = CreateNode("second");
        var isolated = CreateNode("isolated");
        var edge = CreateEdge(first, second);

        var projection = GraphProjection.Create([first, second, isolated], [edge]);

        Assert.HasCount(3, projection.Nodes);
        Assert.HasCount(1, projection.Edges);
        Assert.AreEqual(second.Id, projection.GetOutgoingNeighbors(first.Id).Single().Id);
        Assert.AreEqual(first.Id, projection.GetIncomingNeighbors(second.Id).Single().Id);
        Assert.AreEqual(edge.Id, projection.GetOutgoingEdges(first.Id).Single().Id);
        Assert.AreNotSame(second, projection.GetOutgoingNeighbors(first.Id).Single());
        Assert.AreNotSame(edge, projection.GetOutgoingEdges(first.Id).Single());
        Assert.AreEqual(1, projection.Statistics.IsolatedNodeCount);
        Assert.AreEqual(1d / 6d, projection.Statistics.Density, 0.000000001);
    }

    [TestMethod]
    public void Create_Detaches_Scalar_State_From_Source_Entities()
    {
        var first = CreateNode("first");
        var second = CreateNode("second");
        var edge = CreateEdge(first, second, cost: 2);
        var firstId = first.Id;
        var secondId = second.Id;
        var edgeId = edge.Id;

        var projection = GraphProjection.Create([first, second], [edge]);
        first.Id = Guid.NewGuid();
        second.Name = "changed";
        edge.Id = Guid.NewGuid();
        edge.FromId = Guid.NewGuid();
        edge.Cost = 9;

        var projectedFirst = (IntelligenceNode)projection.GetNode(firstId);
        var projectedEdge = (IntelligenceEdge)projection.GetOutgoingEdges(firstId).Single();

        Assert.AreEqual("first", projectedFirst.Name);
        Assert.AreEqual(secondId, projection.GetOutgoingNeighbors(firstId).Single().Id);
        Assert.AreEqual(edgeId, projectedEdge.Id);
        Assert.AreEqual(2d, projectedEdge.Cost);
    }

    [TestMethod]
    public void Create_WithIgnoredOrphanedEdge_ReportsIgnoredEdge()
    {
        var first = CreateNode("first");
        var missing = CreateNode("missing");
        var edge = CreateEdge(first, missing);

        var projection = GraphProjection.Create(
            [first],
            [edge],
            new GraphProjectionOptions
            {
                OrphanedEdgeBehavior = GraphOrphanedEdgeBehavior.Ignore
            });

        Assert.HasCount(0, projection.Edges);
        Assert.AreEqual(1, projection.Statistics.IgnoredEdgeCount);
    }

    [TestMethod]
    public void Create_WithDuplicateNodeIdentifier_Throws()
    {
        var id = Guid.NewGuid();
        var first = new IntelligenceNode { Id = id, Name = "first" };
        var second = new IntelligenceNode { Id = id, Name = "second" };

        var exception = Assert.ThrowsExactly<ArgumentException>(() => GraphProjection.Create([first, second], []));

        Assert.Contains("duplicate node identifier", exception.Message);
    }

    [TestMethod]
    public void Create_WithMissingEndpoint_Throws()
    {
        var first = CreateNode("first");
        var missing = CreateNode("missing");
        var edge = CreateEdge(first, missing);

        var exception = Assert.ThrowsExactly<ArgumentException>(() => GraphProjection.Create([first], [edge]));

        Assert.Contains(missing.Id.ToString(), exception.Message);
    }

    [TestMethod]
    public void Run_ExecutesCustomAlgorithm()
    {
        var projection = GraphProjection.Create([CreateNode("first"), CreateNode("second")], []);

        var result = projection.Run(new CountNodesAlgorithm(), TestContext.CancellationToken);

        Assert.AreEqual(2, result);
    }

    private static IntelligenceNode CreateNode(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name
    };

    private static IntelligenceEdge CreateEdge(IntelligenceNode from, IntelligenceNode to, double cost = 1) => new()
    {
        Id = Guid.NewGuid(),
        FromId = from.Id,
        ToId = to.Id,
        Cost = cost
    };

    private sealed class CountNodesAlgorithm : IGraphAlgorithm<int>
    {
        public int Execute(GraphProjection projection, CancellationToken cancellationToken = default) =>
            projection.Statistics.NodeCount;
    }

    private sealed class IntelligenceNode : Node
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class IntelligenceEdge : Edge
    {
        public double Cost { get; set; }
    }
}