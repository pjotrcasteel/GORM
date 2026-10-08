using System.Data;
using Gorm.Application.Execution;
using Gorm.Application.Execution.InMemory;
using Gorm.Application.Querying;
using Gorm.Core.Loading;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Loading;

[TestClass]
public sealed class GraphBatchRelationshipLoadingTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void LoadOutgoingWithEdgesBySourceAsync_WhenSourceNodeIdsAreNull_ThrowsArgumentNullException()
    {
        var context = new TestGraphContext();

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            context.LoadOutgoingWithEdgesBySourceAsync<TestPerson, TestWorksOn, TestProject>(null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LoadOutgoingWithEdgesBySourceAsync_WhenSourceNodeIdsAreEmpty_ReturnsEmptyDictionary()
    {
        var context = new TestGraphContext();

        var result = await context.LoadOutgoingWithEdgesBySourceAsync<TestPerson, TestWorksOn, TestProject>([], TestContext.CancellationToken);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task LoadOutgoingWithEdgesBySourceAsync_WhenSourceNodeDoesNotExist_ReturnsEmptyDictionary()
    {
        var context = new TestGraphContext().UseInMemory();

        var result = await context.LoadOutgoingWithEdgesBySourceAsync<TestPerson, TestWorksOn, TestProject>(
            [Guid.NewGuid()],
            TestContext.CancellationToken);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task LoadOutgoingWithEdgesBySourceAsync_WhenSourceNodeHasNoRelationships_ReturnsEmptyDictionary()
    {
        var context = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };

        context.Add(person);

        await context.SaveChangesAsync(TestContext.CancellationToken);

        var result = await context.LoadOutgoingWithEdgesBySourceAsync<TestPerson, TestWorksOn, TestProject>(
            [person.Id],
            TestContext.CancellationToken);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task LoadOutgoingWithEdgesBySourceAsync_WhenMultipleSourcesExist_GroupsRelationshipsBySource()
    {
        var context = new TestGraphContext().UseInMemory();

        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var bob = new TestPerson { Id = Guid.NewGuid(), Name = "Bob" };

        var sharedProject = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };
        var exclusiveProject = new TestProject { Id = Guid.NewGuid(), Title = "Artemis" };

        context.Add(alice);
        context.Add(bob);
        context.Add(sharedProject);
        context.Add(exclusiveProject);

        context.Connect<TestWorksOn, TestPerson, TestProject>(alice, sharedProject, edge => edge.Role = "Architect");
        context.Connect<TestWorksOn, TestPerson, TestProject>(alice, exclusiveProject, edge => edge.Role = "Developer");
        context.Connect<TestWorksOn, TestPerson, TestProject>(bob, sharedProject, edge => edge.Role = "Reviewer");

        await context.SaveChangesAsync(TestContext.CancellationToken);

        var result = await context.LoadOutgoingWithEdgesBySourceAsync<TestPerson, TestWorksOn, TestProject>(
            [alice.Id, bob.Id],
            TestContext.CancellationToken);

        Assert.HasCount(2, result);
        Assert.HasCount(2, result[alice.Id]);
        Assert.HasCount(1, result[bob.Id]);

        Assert.AreEqual("Reviewer", result[bob.Id][0].Edge.Role);
        Assert.AreEqual(sharedProject.Id, result[bob.Id][0].Node.Id);

        CollectionAssert.AreEquivalent(
            new[] { sharedProject.Id, exclusiveProject.Id },
            result[alice.Id].Select(relationship => relationship.Node.Id).ToArray());

        foreach (var (sourceId, relationships) in result)
        {
            foreach (var (node, edge) in relationships)
            {
                Assert.AreEqual(sourceId, edge.FromId);
                Assert.AreEqual(node.Id, edge.ToId);
            }
        }
    }

    [TestMethod]
    public async Task LoadOutgoingWithEdgesBySourceAsync_WhenOnlyOneSourceIsRequested_ExcludesOtherSources()
    {
        var context = new TestGraphContext().UseInMemory();

        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var bob = new TestPerson { Id = Guid.NewGuid(), Name = "Bob" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };

        context.Add(alice);
        context.Add(bob);
        context.Add(project);

        context.Connect<TestWorksOn, TestPerson, TestProject>(alice, project);
        context.Connect<TestWorksOn, TestPerson, TestProject>(bob, project);

        await context.SaveChangesAsync(TestContext.CancellationToken);

        var result = await context.LoadOutgoingWithEdgesBySourceAsync<TestPerson, TestWorksOn, TestProject>(
            [alice.Id],
            TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.IsTrue(result.ContainsKey(alice.Id));
        Assert.IsFalse(result.ContainsKey(bob.Id));
    }

    [TestMethod]
    public async Task LoadOutgoingWithEdgesBySourceAsync_WhenSourceNodeIdsAreDuplicated_ReturnsRelationshipsOnce()
    {
        var context = new TestGraphContext().UseInMemory();

        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };

        context.Add(person);
        context.Add(project);
        context.Connect<TestWorksOn, TestPerson, TestProject>(person, project);

        await context.SaveChangesAsync(TestContext.CancellationToken);

        var result = await context.LoadOutgoingWithEdgesBySourceAsync<TestPerson, TestWorksOn, TestProject>(
            [person.Id, person.Id],
            TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.HasCount(1, result[person.Id]);
    }

    [TestMethod]
    public void SelectWithEdge_WhenTraversalIsOutgoing_GeneratesCorrectEndpointColumns()
    {
        var context = new TestGraphContext();

        var sql = context.People
            .Outgoing<TestWorksOn, TestProject>()
            .SelectWithEdge<TestWorksOn, TestProject, GraphRelatedEdgeResult<TestWorksOn, TestProject>>(
                (node, edge) => new GraphRelatedEdgeResult<TestWorksOn, TestProject>
                {
                    Node = node,
                    Edge = edge
                })
            .ToSql();

        Assert.Contains("[n0].[Id] AS [Edge__FromId]", sql.CommandText);
        Assert.Contains("[n2].[Id] AS [Edge__ToId]", sql.CommandText);
    }

    [TestMethod]
    public void SelectWithEdge_WhenTraversalIsIncoming_GeneratesCorrectEndpointColumns()
    {
        var context = new TestGraphContext();

        var sql = context.Projects
            .Incoming<TestWorksOn, TestPerson>()
            .SelectWithEdge<TestWorksOn, TestPerson, GraphRelatedEdgeResult<TestWorksOn, TestPerson>>(
                (node, edge) => new GraphRelatedEdgeResult<TestWorksOn, TestPerson>
                {
                    Node = node,
                    Edge = edge
                })
            .ToSql();

        Assert.Contains("[n2].[Id] AS [Edge__FromId]", sql.CommandText);
        Assert.Contains("[n0].[Id] AS [Edge__ToId]", sql.CommandText);
    }

    [TestMethod]
    public void SelectWithEdge_WhenTraversalsAreChained_UsesEndpointsFromLastTraversal()
    {
        var context = new TestGraphContext();

        var sql = context.People
            .Outgoing<TestKnows, TestPerson>()
            .ThenOutgoing<TestWorksOn, TestProject>()
            .SelectWithEdge<TestWorksOn, TestProject, GraphRelatedEdgeResult<TestWorksOn, TestProject>>(
                (node, edge) => new GraphRelatedEdgeResult<TestWorksOn, TestProject>
                {
                    Node = node,
                    Edge = edge
                })
            .ToSql();

        Assert.Contains("[n2].[Id] AS [Edge__FromId]", sql.CommandText);
        Assert.Contains("[n4].[Id] AS [Edge__ToId]", sql.CommandText);
    }

    [TestMethod]
    public void SelectWithEdge_WhenMultipleSourceIdsAreProvided_GeneratesSingleInPredicate()
    {
        var context = new TestGraphContext();
        var sourceNodeIds = new[] { Guid.NewGuid(), Guid.NewGuid() };

        var sql = context.People
            .WhereIds(sourceNodeIds)
            .Outgoing<TestWorksOn, TestProject>()
            .SelectWithEdge<TestWorksOn, TestProject, GraphRelatedEdgeResult<TestWorksOn, TestProject>>(
                (node, edge) => new GraphRelatedEdgeResult<TestWorksOn, TestProject>
                {
                    Node = node,
                    Edge = edge
                })
            .ToSql();

        Assert.Contains(" IN ", sql.CommandText);
        Assert.HasCount(2, sql.Parameters);
        Assert.Contains("[n0].[Id] AS [Edge__FromId]", sql.CommandText);
    }

    [TestMethod]
    public async Task MaterializeListAsync_WhenEdgeEndpointColumnsArePresent_PopulatesFromIdAndToId()
    {
        var context = new TestGraphContext();

        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var edgeId = Guid.NewGuid();

        using var table = new DataTable();

        table.Columns.Add("Node__Id", typeof(Guid));
        table.Columns.Add("Node__Title", typeof(string));
        table.Columns.Add("Edge__Id", typeof(Guid));
        table.Columns.Add("Edge__Role", typeof(string));
        table.Columns.Add("Edge__FromId", typeof(Guid));
        table.Columns.Add("Edge__ToId", typeof(Guid));

        table.Rows.Add(targetId, "Apollo", edgeId, "Developer", sourceId, targetId);

        var query = context.People
            .Outgoing<TestWorksOn, TestProject>()
            .SelectWithEdge<TestWorksOn, TestProject, GraphRelatedEdgeResult<TestWorksOn, TestProject>>(
                (node, edge) => new GraphRelatedEdgeResult<TestWorksOn, TestProject>
                {
                    Node = node,
                    Edge = edge
                });

        using var reader = table.CreateDataReader();

        var result = await GraphMaterializer.MaterializeListAsync<GraphRelatedEdgeResult<TestWorksOn, TestProject>>(
            context,
            reader,
            query.ToQueryModel(),
            TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.AreEqual(sourceId, result[0].Edge.FromId);
        Assert.AreEqual(targetId, result[0].Edge.ToId);
        Assert.AreEqual(targetId, result[0].Node.Id);
        Assert.AreEqual("Developer", result[0].Edge.Role);
    }
}