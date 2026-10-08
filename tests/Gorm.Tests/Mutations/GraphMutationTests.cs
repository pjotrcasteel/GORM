using Gorm.Application.Mutations;
using Gorm.Application.Mutations.Edges;
using Gorm.Application.Mutations.Nodes;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Mutations;

[TestClass]
public sealed class GraphMutationTests
{
    [TestMethod]
    public void Create_ValidName_SetsName()
    {
        var mutation = GraphMutation.Create("service-specification-activation");

        Assert.AreEqual("service-specification-activation", mutation.Name);
    }

    [TestMethod]
    public void Create_EmptyName_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GraphMutation.Create(string.Empty));
    }

    [TestMethod]
    public void WithCorrelationId_Value_SetsCorrelationId()
    {
        var mutation = GraphMutation.Create().WithCorrelationId("correlation-1");

        Assert.AreEqual("correlation-1", mutation.CorrelationId);
    }

    [TestMethod]
    public void WithSourceEventId_Value_SetsSourceEventId()
    {
        var mutation = GraphMutation.Create().WithSourceEventId("event-1");

        Assert.AreEqual("event-1", mutation.SourceEventId);
    }

    [TestMethod]
    public void UpsertNode_Node_AddsNodeMutation()
    {
        var person = new TestPerson { Name = "Alice" };

        var mutation = GraphMutation.Create().UpsertNode(person, x => x.Key("person-alice"));

        Assert.HasCount(1, mutation.Nodes);
        Assert.AreSame(person, mutation.Nodes[0].Node);
        Assert.AreEqual(GraphMutationNodeOperation.Upsert, mutation.Nodes[0].Operation);
        Assert.AreEqual("person-alice", mutation.Nodes[0].Key);
    }

    [TestMethod]
    public void AddNode_SameNodeSameOperationAndKey_IsIdempotent()
    {
        var person = new TestPerson { Name = "Alice" };

        var mutation = GraphMutation.Create().AddNode(person, x => x.Key("person-alice")).AddNode(person, x => x.Key("person-alice"));

        Assert.HasCount(1, mutation.Nodes);
    }

    [TestMethod]
    public void AddNode_SameNodeDifferentOperation_ThrowsInvalidOperationException()
    {
        var person = new TestPerson { Name = "Alice" };

        var mutation = GraphMutation.Create().AddNode(person);

        Assert.ThrowsExactly<InvalidOperationException>(() => mutation.UpdateNode(person));
    }

    [TestMethod]
    public void UpsertEdge_NewEdge_AddsEdgeMutation()
    {
        var person = new TestPerson { Name = "Alice" };
        var project = new TestProject { Title = "Alpha" };

        var mutation = GraphMutation.Create().UpsertEdge<TestWorksOn, TestPerson, TestProject>(person, project, configure: x => x.Key("alice-works-on-alpha"));

        Assert.HasCount(1, mutation.Edges);
        Assert.AreSame(person, mutation.Edges[0].From);
        Assert.AreSame(project, mutation.Edges[0].To);
        Assert.AreEqual(typeof(TestWorksOn), mutation.Edges[0].Edge.GetType());
        Assert.AreEqual(GraphMutationEdgeOperation.Upsert, mutation.Edges[0].Operation);
        Assert.AreEqual("alice-works-on-alpha", mutation.Edges[0].Key);
    }

    [TestMethod]
    public void AddEdge_SameEdgeInstanceTwice_ThrowsInvalidOperationException()
    {
        var person = new TestPerson { Name = "Alice" };
        var project1 = new TestProject { Title = "Alpha" };
        var project2 = new TestProject { Title = "Beta" };
        var edge = new TestWorksOn();

        var mutation = GraphMutation.Create().AddEdge(person, project1, edge);

        Assert.ThrowsExactly<InvalidOperationException>(() => mutation.AddEdge(person, project2, edge));
    }

    [TestMethod]
    public void NodeOptions_KeyEmpty_ThrowsArgumentException()
    {
        var person = new TestPerson { Name = "Alice" };

        Assert.ThrowsExactly<ArgumentException>(() => GraphMutation.Create().UpsertNode(person, x => x.Key(string.Empty)));
    }

    [TestMethod]
    public void EdgeOptions_KeyEmpty_ThrowsArgumentException()
    {
        var person = new TestPerson { Name = "Alice" };
        var project = new TestProject { Title = "Alpha" };

        Assert.ThrowsExactly<ArgumentException>(
            () => GraphMutation.Create().UpsertEdge<TestWorksOn, TestPerson, TestProject>(person, project, configure: x => x.Key(string.Empty))
        );
    }
}