using Gorm.Application.Execution.InMemory;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Execution;

[TestClass]
public sealed class InMemoryGraphStoreAdditionalTests
{

    [TestMethod]
    public void UpsertNode_Throws_For_Null()
    {
        var store = new InMemoryGraphStore();
        Assert.ThrowsExactly<ArgumentNullException>(() => store.UpsertNode(null!));
    }

    [TestMethod]
    public void UpsertNode_Throws_For_Empty_Id()
    {
        var store = new InMemoryGraphStore();
        var node = new TestPerson { Id = Guid.Empty };
        Assert.ThrowsExactly<InvalidOperationException>(() => store.UpsertNode(node));
    }

    [TestMethod]
    public void UpsertNode_Adds_Node()
    {
        var store = new InMemoryGraphStore();
        var node = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        store.UpsertNode(node);
        var result = store.GetNodes<TestPerson>();
        Assert.HasCount(1, result);
    }

    [TestMethod]
    public void UpsertEdge_Throws_For_Null()
    {
        var store = new InMemoryGraphStore();
        Assert.ThrowsExactly<ArgumentNullException>(() => store.UpsertEdge(null!));
    }

    [TestMethod]
    public void UpsertEdge_Throws_For_Empty_Id()
    {
        var store = new InMemoryGraphStore();
        var edge = new TestWorksOn { Id = Guid.Empty };
        Assert.ThrowsExactly<InvalidOperationException>(() => store.UpsertEdge(edge));
    }

    [TestMethod]
    public void UpsertEdge_Adds_Edge()
    {
        var store = new InMemoryGraphStore();
        var fromId = Guid.NewGuid();
        var toId = Guid.NewGuid();
        var edge = new TestWorksOn { Id = Guid.NewGuid(), FromId = fromId, ToId = toId };
        store.UpsertEdge(edge);
        var result = store.GetEdges<TestWorksOn>();
        Assert.HasCount(1, result);
    }

    [TestMethod]
    public void RemoveNode_Returns_True_When_Existing()
    {
        var store = new InMemoryGraphStore();
        var node = new TestPerson { Id = Guid.NewGuid() };
        store.UpsertNode(node);
        var removed = store.RemoveNode(node);
        Assert.IsTrue(removed);
    }

    [TestMethod]
    public void RemoveNode_Returns_False_When_Not_Existing()
    {
        var store = new InMemoryGraphStore();
        var node = new TestPerson { Id = Guid.NewGuid() };
        var removed = store.RemoveNode(node);
        Assert.IsFalse(removed);
    }

    [TestMethod]
    public void RemoveNode_Throws_For_Null()
    {
        var store = new InMemoryGraphStore();
        Assert.ThrowsExactly<ArgumentNullException>(() => store.RemoveNode(null!));
    }

    [TestMethod]
    public void RemoveEdge_Returns_True_When_Existing()
    {
        var store = new InMemoryGraphStore();
        var edge = new TestWorksOn { Id = Guid.NewGuid(), FromId = Guid.NewGuid(), ToId = Guid.NewGuid() };
        store.UpsertEdge(edge);
        var removed = store.RemoveEdge(edge);
        Assert.IsTrue(removed);
    }

    [TestMethod]
    public void RemoveEdge_Returns_False_When_Not_Existing()
    {
        var store = new InMemoryGraphStore();
        var edge = new TestWorksOn { Id = Guid.NewGuid() };
        var removed = store.RemoveEdge(edge);
        Assert.IsFalse(removed);
    }

    [TestMethod]
    public void RemoveEdge_Throws_For_Null()
    {
        var store = new InMemoryGraphStore();
        Assert.ThrowsExactly<ArgumentNullException>(() => store.RemoveEdge(null!));
    }

    [TestMethod]
    public void RemoveConnectedEdges_Removes_Edges_Connected_To_Node()
    {
        var store = new InMemoryGraphStore();
        var fromId = Guid.NewGuid();
        var toId = Guid.NewGuid();
        var node = new TestPerson { Id = fromId };
        var edge = new TestWorksOn { Id = Guid.NewGuid(), FromId = fromId, ToId = toId };
        store.UpsertNode(node);
        store.UpsertEdge(edge);

        var count = store.RemoveConnectedEdges(node);

        Assert.AreEqual(1, count);
        Assert.IsEmpty(store.GetEdges<TestWorksOn>());
    }

    [TestMethod]
    public void RemoveConnectedEdges_Returns_Zero_When_No_Edges()
    {
        var store = new InMemoryGraphStore();
        var node = new TestPerson { Id = Guid.NewGuid() };
        var count = store.RemoveConnectedEdges(node);
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void RemoveEdgeConnections_Removes_Matching_Edges()
    {
        var store = new InMemoryGraphStore();
        var fromId = Guid.NewGuid();
        var toId = Guid.NewGuid();
        var edge = new TestWorksOn { Id = Guid.NewGuid(), FromId = fromId, ToId = toId };
        store.UpsertEdge(edge);

        var count = store.RemoveEdgeConnections(typeof(TestWorksOn), fromId, toId);

        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public void RemoveEdgeConnections_Throws_When_Multiple_Edges_Match()
    {
        var store = new InMemoryGraphStore();
        var fromId = Guid.NewGuid();
        var toId = Guid.NewGuid();
        store.UpsertEdge(new TestWorksOn { Id = Guid.NewGuid(), FromId = fromId, ToId = toId, Role = "primary" });
        store.UpsertEdge(new TestWorksOn { Id = Guid.NewGuid(), FromId = fromId, ToId = toId, Role = "backup" });

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => store.RemoveEdgeConnections(typeof(TestWorksOn), fromId, toId));

        Assert.Contains("Ambiguous GORM edge disconnection", ex.Message);
        Assert.HasCount(2, store.GetEdges<TestWorksOn>());
    }

    [TestMethod]
    public void RemoveEdgeConnections_Returns_Zero_When_No_Match()
    {
        var store = new InMemoryGraphStore();
        var count = store.RemoveEdgeConnections(typeof(TestWorksOn), Guid.NewGuid(), Guid.NewGuid());
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void RemoveEdgeConnections_Returns_Zero_For_Unknown_EdgeType()
    {
        var store = new InMemoryGraphStore();
        var count = store.RemoveEdgeConnections(typeof(TestKnows), Guid.NewGuid(), Guid.NewGuid());
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void RemoveEdgeConnections_Throws_For_Null_Type()
    {
        var store = new InMemoryGraphStore();
        Assert.ThrowsExactly<ArgumentNullException>(() => store.RemoveEdgeConnections(null!, Guid.NewGuid(), Guid.NewGuid()));
    }

    [TestMethod]
    public void GetNodeBucket_Returns_Empty_When_No_Nodes()
    {
        var store = new InMemoryGraphStore();
        var bucket = store.GetNodeBucket<TestPerson>();
        Assert.IsEmpty(bucket);
    }

    [TestMethod]
    public void GetEdgeBucket_Returns_Empty_When_No_Edges()
    {
        var store = new InMemoryGraphStore();
        var bucket = store.GetEdgeBucket<TestWorksOn>();
        Assert.IsEmpty(bucket);
    }

    [TestMethod]
    public void GetNodeBucket_Returns_Stored_Nodes()
    {
        var store = new InMemoryGraphStore();
        var node = new TestPerson { Id = Guid.NewGuid() };
        store.UpsertNode(node);
        var bucket = store.GetNodeBucket<TestPerson>();
        Assert.HasCount(1, bucket);
    }

    [TestMethod]
    public void NodesByType_Returns_All_Types()
    {
        var store = new InMemoryGraphStore();
        store.UpsertNode(new TestPerson { Id = Guid.NewGuid() });
        store.UpsertNode(new TestProject { Id = Guid.NewGuid() });
        Assert.HasCount(2, store.NodesByType);
    }

    [TestMethod]
    public void EdgesByType_Returns_All_Types()
    {
        var store = new InMemoryGraphStore();
        store.UpsertEdge(new TestWorksOn { Id = Guid.NewGuid(), FromId = Guid.NewGuid(), ToId = Guid.NewGuid() });
        Assert.HasCount(1, store.EdgesByType);
    }
}