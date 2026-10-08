using System.Linq.Expressions;
using Gorm.Application.Context.Extensions;
using Gorm.Application.Execution.InMemory;
using Gorm.Application.Tracking;
using Gorm.Core.Loading;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Performance;

[TestClass]
public sealed class GraphRelationshipPerformanceHotPathTests
{
    [TestMethod]
    public async Task LoadRelationshipAsync_EdgePredicateAndRelatedOrder_AppliesCompiledPostProcessing()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var alpha = new TestProject { Id = Guid.NewGuid(), Title = "Alpha" };
        var beta = new TestProject { Id = Guid.NewGuid(), Title = "Beta" };
        var gamma = new TestProject { Id = Guid.NewGuid(), Title = "Gamma" };

        ctx.Add(person);
        ctx.Add(alpha);
        ctx.Add(beta);
        ctx.Add(gamma);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(person, alpha, edge => edge.Role = "Skip");
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(person, beta, edge => edge.Role = "Keep");
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(person, gamma, edge => edge.Role = "Keep");
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var request = new GraphIncludeRequest
        {
            Name = "Projects",
            NameKind = GraphIncludeNameKind.Relationship,
            IncludeEdge = false,
            EdgePredicate = (Expression<Func<TestWorksOn, bool>>)(edge => edge.Role == "Keep"),
            RelatedOrderBy = (Expression<Func<TestProject, string>>)(project => project.Title),
            RelatedOrderDescending = true
        };

        var loaded = await ctx.LoadRelationshipAsync(person, request, TestContext.CancellationToken);
        var projects = loaded.Cast<TestProject>().ToArray();

        Assert.HasCount(2, projects);
        Assert.AreEqual("Gamma", projects[0].Title);
        Assert.AreEqual("Beta", projects[1].Title);
    }

    [TestMethod]
    public async Task LoadRelationshipWithEdgesAsync_InMemory_ReturnsTypedEdgeResult()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };

        ctx.Add(person);
        ctx.Add(project);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(person, project, edge => edge.Role = "Lead");
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var loaded = await ctx.LoadRelationshipWithEdgesAsync(person, "Projects", TestContext.CancellationToken);
        var result = (GraphRelatedEdgeResult<TestWorksOn, TestProject>)loaded[0];

        Assert.AreEqual(project.Id, result.Node.Id);
        Assert.AreEqual("Lead", result.Edge.Role);
    }

    [TestMethod]
    public void GraphRelationshipFixupStore_AddSameNodeTwice_StoresSingleItem()
    {
        var store = new GraphRelationshipFixupStore();
        var owner = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };

        store.Add(owner, "Projects", includeEdge: false, project);
        store.Add(owner, "Projects", includeEdge: false, project);

        var found = store.TryGetRelated<TestProject>(owner, "Projects", out var related);

        var relatedItems = related ?? [];

        Assert.IsTrue(found);
        Assert.HasCount(1, relatedItems);
        Assert.AreSame(project, relatedItems[0]);
    }

    public TestContext TestContext { get; set; } = null!;
}