using Gorm.Application.Context;
using Gorm.Application.Execution.InMemory;
using Gorm.Core.Primitives;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Context;

[TestClass]
public sealed class GraphContextGraphBatchTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task StoreGraphAsync_MultipleNodesAndEdges_PersistsGraphInOneSave()
    {
        var context = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Name = "Alice" };
        var apollo = new TestProject { Title = "Apollo" };
        var hermes = new TestProject { Title = "Hermes" };

        var nodes = new Node[]
        {
            alice,
            apollo,
            hermes
        };

        var edgeConnections = new[]
        {
            GraphEdgeBatchItem.Create(alice, apollo, new TestWorksOn { Role = "Lead" }),
            GraphEdgeBatchItem.Create(alice, hermes, new TestWorksOn { Role = "Reviewer" })
        };

        var affectedRows = await context.StoreGraphAsync(nodes, edgeConnections, TestContext.CancellationToken);

        var outgoing = await context.LoadOutgoingWithEdgesAsync<TestWorksOn, TestPerson, TestProject>(alice, TestContext.CancellationToken);

        Assert.AreEqual(5, affectedRows);
        Assert.HasCount(2, outgoing!);
        Assert.IsTrue(outgoing!.Any(x => x.Node.Title == "Apollo" && x.Edge.Role == "Lead"));
        Assert.IsTrue(outgoing!.Any(x => x.Node.Title == "Hermes" && x.Edge.Role == "Reviewer"));
    }

    [TestMethod]
    public void AddEdges_MultipleEdgesBetweenSameNodes_TracksEveryEdgeInstance()
    {
        var context = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var apollo = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };

        context.Add(alice);
        context.Add(apollo);

        context.AddEdges(
        [
            GraphEdgeBatchItem.Create(alice, apollo, new TestWorksOn { Role = "Lead" }),
            GraphEdgeBatchItem.Create(alice, apollo, new TestWorksOn { Role = "Reviewer" })
        ]);

        Assert.HasCount(2, context.ChangeTracker.PendingEdgeConnections);
        Assert.IsTrue(context.ChangeTracker.PendingEdgeConnections.Select(x => ((TestWorksOn)x.Edge).Role).SequenceEqual(["Lead", "Reviewer"]));
    }

    [TestMethod]
    public void AddEdges_SameEdgeInstanceUsedTwice_ThrowsInvalidOperationException()
    {
        var context = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var apollo = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };
        var edge = new TestWorksOn { Role = "Lead" };

        context.Add(alice);
        context.Add(apollo);

        Assert.ThrowsExactly<InvalidOperationException>(() => context.AddEdges([GraphEdgeBatchItem.Create(alice, apollo, edge), GraphEdgeBatchItem.Create(alice, apollo, edge)]));
    }

    [TestMethod]
    public void StoreGraphAsync_NullConnectionFactory_ThrowsBeforeTrackingGraph()
    {
        var context = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Name = "Alice" };
        var project = new TestProject { Title = "Apollo" };

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            context.StoreGraphAsync(
                null!,
                [person, project],
                [GraphEdgeBatchItem.Create(person, project, new TestWorksOn())],
                TestContext.CancellationToken).GetAwaiter().GetResult());

        Assert.IsEmpty(context.ChangeTracker.Entries);
        Assert.IsEmpty(context.ChangeTracker.PendingEdgeConnections);
    }
}