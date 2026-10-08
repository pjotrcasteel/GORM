using Gorm.Application.Execution;
using Gorm.Application.Execution.InMemory;
using Gorm.Application.Querying;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Execution;

[TestClass]
public sealed class InMemoryGraphStoreAndEngineTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void UpsertNode_Stores_Node()
    {
        var store = new InMemoryGraphStore();
        var node = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };

        store.UpsertNode(node);

        var nodes = store.GetNodes<TestPerson>();
        Assert.HasCount(1, nodes);
    }

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
    public void UpsertEdge_Stores_Edge()
    {
        var store = new InMemoryGraphStore();
        var edge = new TestWorksOn { Id = Guid.NewGuid(), FromId = Guid.NewGuid(), ToId = Guid.NewGuid() };

        store.UpsertEdge(edge);

        var edges = store.GetEdges<TestWorksOn>();
        Assert.HasCount(1, edges);
    }

    [TestMethod]
    public void UpsertEdge_Throws_For_Null()
    {
        var store = new InMemoryGraphStore();
        Assert.ThrowsExactly<ArgumentNullException>(() => store.UpsertEdge(null!));
    }

    [TestMethod]
    public void RemoveNode_Removes_Node()
    {
        var store = new InMemoryGraphStore();
        var node = new TestPerson { Id = Guid.NewGuid() };
        store.UpsertNode(node);

        store.RemoveNode(node);

        Assert.IsEmpty(store.GetNodes<TestPerson>());
    }

    [TestMethod]
    public void RemoveNode_Throws_For_Null()
    {
        var store = new InMemoryGraphStore();
        Assert.ThrowsExactly<ArgumentNullException>(() => store.RemoveNode(null!));
    }

    [TestMethod]
    public void RemoveEdge_Removes_Edge()
    {
        var store = new InMemoryGraphStore();
        var edge = new TestWorksOn { Id = Guid.NewGuid(), FromId = Guid.NewGuid(), ToId = Guid.NewGuid() };
        store.UpsertEdge(edge);

        store.RemoveEdge(edge);

        Assert.IsEmpty(store.GetEdges<TestWorksOn>());
    }

    [TestMethod]
    public void RemoveEdge_Throws_For_Null()
    {
        var store = new InMemoryGraphStore();
        Assert.ThrowsExactly<ArgumentNullException>(() => store.RemoveEdge(null!));
    }

    [TestMethod]
    public void GetNodes_Returns_Empty_Collection_When_Type_Not_Present()
    {
        var store = new InMemoryGraphStore();
        var nodes = store.GetNodes<TestPerson>();
        Assert.IsEmpty(nodes);
    }

    [TestMethod]
    public void GetEdges_Returns_Empty_Collection_When_Type_Not_Present()
    {
        var store = new InMemoryGraphStore();
        var edges = store.GetEdges<TestWorksOn>();
        Assert.IsEmpty(edges);
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
    public void GetNodeBucket_Returns_Typed_Dictionary()
    {
        var store = new InMemoryGraphStore();
        var id = Guid.NewGuid();
        store.UpsertNode(new TestPerson { Id = id, Name = "Alice" });

        var bucket = store.GetNodeBucket<TestPerson>();

        Assert.IsTrue(bucket.ContainsKey(id));
        Assert.AreEqual("Alice", bucket[id].Name);
    }

    [TestMethod]
    public void GetEdgeBucket_Returns_Typed_Dictionary()
    {
        var store = new InMemoryGraphStore();
        var id = Guid.NewGuid();
        store.UpsertEdge(new TestWorksOn { Id = id, FromId = Guid.NewGuid(), ToId = Guid.NewGuid() });

        var bucket = store.GetEdgeBucket<TestWorksOn>();

        Assert.IsTrue(bucket.ContainsKey(id));
    }

    [TestMethod]
    public void UpsertNode_Overwrites_Existing_Node_With_Same_Id()
    {
        var store = new InMemoryGraphStore();
        var id = Guid.NewGuid();
        store.UpsertNode(new TestPerson { Id = id, Name = "Alice" });
        store.UpsertNode(new TestPerson { Id = id, Name = "Alice Updated" });

        var nodes = store.GetNodes<TestPerson>();
        Assert.HasCount(1, nodes);
        Assert.AreEqual("Alice Updated", nodes.Single().Name);
    }

    [TestMethod]
    public async Task InMemoryEngine_ExecuteAnyAsync_Returns_True_When_Items_Exist()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice" });
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People.AnyAsync(TestContext.CancellationToken);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task InMemoryEngine_ExecuteCountAsync_Returns_Correct_Count()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice" });
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Bob" });
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var count = await ctx.People.CountAsync(TestContext.CancellationToken);

        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public async Task InMemoryEngine_ExecuteLongCountAsync_Returns_Correct_Count()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice" });
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var count = await ctx.People.LongCountAsync(TestContext.CancellationToken);

        Assert.AreEqual(1L, count);
    }

    [TestMethod]
    public async Task InMemoryEngine_SaveChanges_Removes_Deleted_Entity()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        ctx.Add(person);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        ctx.Remove(person);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People.ToListAsync(TestContext.CancellationToken);
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task InMemoryEngine_SaveChanges_Updates_Modified_Entity()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        ctx.Add(person);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        person.Name = "Alice Updated";
        ctx.Update(person);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People.ToListAsync(TestContext.CancellationToken);
        Assert.AreEqual("Alice Updated", result[0].Name);
    }

    [TestMethod]
    public void GraphQueryExecutionRequest_List_Mode_Respects_QueryTake()
    {
        var req = new GraphQueryExecutionRequest { Mode = GraphQueryExecutionMode.List };
        Assert.AreEqual(5, req.GetEffectiveTake(5));
        Assert.IsNull(req.GetEffectiveTake(null));
    }

    [TestMethod]
    public void GraphQueryExecutionRequest_First_Mode_Is_Not_ExistenceOnly()
    {
        var req = new GraphQueryExecutionRequest { Mode = GraphQueryExecutionMode.First };
        Assert.IsFalse(req.IsExistenceOnly);
    }

    [TestMethod]
    public void GraphQueryExecutionRequest_Single_Mode_Returns_2_Take()
    {
        var req = new GraphQueryExecutionRequest { Mode = GraphQueryExecutionMode.Single };
        Assert.AreEqual(2, req.GetEffectiveTake(null));
        Assert.AreEqual(1, req.GetEffectiveTake(1));
    }

    [TestMethod]
    public void GraphQueryable_Throws_For_Null_Provider()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new GraphQueryable<TestPerson>(null!, System.Linq.Expressions.Expression.Constant(0)));
    }

    [TestMethod]
    public void GraphQueryable_Throws_For_Null_Expression()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => new GraphQueryable<TestPerson>(ctx.People.Provider, null!));
    }

    [TestMethod]
    public void GraphQueryable_ElementType_Is_T()
    {
        var ctx = new TestGraphContext();
        Assert.AreEqual(typeof(TestPerson), ctx.People.ElementType);
    }

    [TestMethod]
    public void GraphQueryable_ToString_Returns_Non_Empty()
    {
        var ctx = new TestGraphContext();
        var str = ctx.People.ToString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(str));
    }

    [TestMethod]
    public void GraphSet_ToQueryModel_Returns_Valid_Model()
    {
        var ctx = new TestGraphContext();
        var model = ctx.People.ToQueryModel();

        Assert.IsNotNull(model);
        Assert.AreEqual(typeof(TestPerson), model.RootElementType);
    }

    [TestMethod]
    public void GraphSet_Provider_Is_GraphQueryProvider()
    {
        var ctx = new TestGraphContext();
        Assert.IsInstanceOfType<Gorm.Application.Querying.GraphQueryProvider>(ctx.People.Provider);
    }
}