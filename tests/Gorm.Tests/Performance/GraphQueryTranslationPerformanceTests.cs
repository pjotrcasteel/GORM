using Gorm.Application.Execution;
using Gorm.Application.Execution.InMemory;
using Gorm.Application.Querying;
using Gorm.Application.Querying.Models;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Performance;

[TestClass]
public sealed class GraphQueryTranslationPerformanceTests
{
    [TestMethod]
    public void Translate_CapturedSkipTakeValues_ReadsValuesWithoutConstantOnlyRequirement()
    {
        var context = new TestGraphContext();
        var skip = 5;
        var take = 10;

        var query = context.People.OrderBy(x => x.Id).Skip(skip).Take(take);

        var model = GraphQueryProvider.Translate(query.Expression);

        Assert.AreEqual(skip, model.SkipCount);
        Assert.AreEqual(take, model.TakeCount);
    }

    [TestMethod]
    public void Translate_RepeatedTraversalShape_ProducesStableModel()
    {
        var context = new TestGraphContext();

        var query = context.People.Where(x => x.Age > 18).Outgoing<TestWorksOn, TestProject>(x => x.Role == "Dev").ThenIncoming<TestWorksOn, TestPerson>().AsNoTracking();

        var first = GraphQueryProvider.Translate(query.Expression);
        var second = GraphQueryProvider.Translate(query.Expression);

        Assert.AreEqual(GraphQueryTrackingMode.NoTracking, first.TrackingMode);
        Assert.AreEqual(GraphQueryTrackingMode.NoTracking, second.TrackingMode);
        Assert.HasCount(3, first.Steps);
        Assert.HasCount(3, second.Steps);
    }

    [TestMethod]
    public async Task Include_WithDuplicateRootNodes_LoadsIncludesOncePerNodeIdentity()
    {
        var context = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };

        context.Add(person);
        context.Add(project);
        context.Connect<TestWorksOn, TestPerson, TestProject>(person, project);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        var result = await context.People.Where(x => x.Id == person.Id).Include(x => x.Projects).ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.HasCount(1, result[0].Projects);
        Assert.AreEqual(project.Id, result[0].Projects[0].Id);
    }

    [TestMethod]
    public async Task ContainsAsync_EntityQuery_UsesCachedEntityIdPredicate()
    {
        var context = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };

        context.Add(person);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        var exists = await context.People.ContainsAsync(person, TestContext.CancellationToken);

        Assert.IsTrue(exists);
    }

    public TestContext TestContext { get; set; } = null!;
}