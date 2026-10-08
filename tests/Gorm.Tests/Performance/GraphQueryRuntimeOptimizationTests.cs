using System.Linq.Expressions;
using Gorm.Application.Execution;
using Gorm.Application.Execution.InMemory;
using Gorm.Application.Querying;
using Gorm.Core.Loading;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Performance;

[TestClass]
public sealed class GraphQueryRuntimeOptimizationTests
{
    [TestMethod]
    public void RuntimeCache_ApplyWhere_UsesNonIndexedQueryableWhereOverload()
    {
        var context = new TestGraphContext();
        Expression<Func<TestPerson, bool>> predicate = person => person.Age > 18;

        var query = GraphQueryProviderRuntimeCache.ApplyWhere(context.People, typeof(TestPerson), predicate);

        Assert.IsNotNull(query);
        Assert.AreEqual(typeof(TestPerson), query.ElementType);
    }

    [TestMethod]
    public void RuntimeCache_ApplySelect_UsesNonIndexedQueryableSelectOverload()
    {
        var context = new TestGraphContext();
        Expression<Func<TestPerson, string>> selector = person => person.Name;

        var query = GraphQueryProviderRuntimeCache.ApplySelect(context.People, typeof(TestPerson), selector, out var resultType);

        Assert.IsNotNull(query);
        Assert.AreEqual(typeof(string), resultType);
        Assert.AreEqual(typeof(string), query.ElementType);
    }

    [TestMethod]
    public void PrepareQuery_WithoutIncludes_ReusesOriginalQuery()
    {
        var context = new TestGraphContext();
        var query = context.People.Where(x => x.Age > 10);

        var prepared = GraphQueryableExecutionExtensions.PrepareQuery(query);

        Assert.AreSame(query, prepared.Query);
        Assert.IsEmpty(prepared.Includes);
    }

    [TestMethod]
    public void PrepareQuery_WithInclude_StripsIncludeExpression()
    {
        var context = new TestGraphContext();
        var query = context.People.Include(x => x.Projects);

        var prepared = GraphQueryableExecutionExtensions.PrepareQuery(query);

        Assert.AreNotSame(query, prepared.Query);
        Assert.HasCount(1, prepared.Includes);
        Assert.AreEqual(nameof(TestPerson.Projects), prepared.Includes[0].Name);
        Assert.AreEqual(GraphIncludeNameKind.Navigation, prepared.Includes[0].NameKind);
    }

    [TestMethod]
    public void ContainsPredicate_Entity_UsesCachedIdProperty()
    {
        var person = new TestPerson
        {
            Id = Guid.NewGuid(),
            Name = "Alice",
            Age = 42
        };

        var predicate = GraphContainsPredicateCache.Create(person).Compile();

        Assert.IsTrue(predicate(person));
        Assert.IsFalse(predicate(new TestPerson
        {
            Id = Guid.NewGuid(),
            Name = "Other",
            Age = 42
        }));
    }

    [TestMethod]
    public async Task Include_DuplicateRoots_DoesNotDuplicateNavigationItems()
    {
        var context = new TestGraphContext().UseInMemory();

        var person = new TestPerson
        {
            Id = Guid.NewGuid(),
            Name = "Alice",
            Age = 42
        };

        var project = new TestProject
        {
            Id = Guid.NewGuid(),
            Title = "Apollo"
        };

        context.Add(person);
        context.Add(project);
        context.Connect<TestWorksOn, TestPerson, TestProject>(person, project);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        var result = await context.People.Where(x => x.Id == person.Id).Include(x => x.Projects).ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.HasCount(1, result[0].Projects);
        Assert.AreEqual(project.Id, result[0].Projects[0].Id);
    }

    public TestContext TestContext { get; set; } = null!;
}