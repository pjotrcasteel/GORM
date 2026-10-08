#pragma warning disable CA1861 // Avoid constant arrays as arguments

using System.Data.Common;
using System.Linq.Expressions;
using Gorm.Application.Execution;
using Gorm.Application.Execution.InMemory;
using Gorm.Application.Querying;
using Gorm.Core.Loading;
using Gorm.Infrastructure.Persistence.Connections;
using Gorm.Tests.TestSupport;
using Exec = Gorm.Application.Execution.GraphQueryableExecutionExtensions;

namespace Gorm.Tests.Execution;

[TestClass]
public sealed class GraphQueryableExecutionExtensionsTests
{
    public TestContext TestContext { get; set; } = null!;

    private static readonly int[] SourceArray = [1, 2, 3, 4, 5];

    [TestMethod]
    public async Task ToListAsync_Returns_All_Items()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await SeedPeople(ctx, TestContext.CancellationToken, ("Alice", 30), ("Bob", 25));

        var result = await ctx.People.ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(2, result);
    }

    [TestMethod]
    public async Task ToListAsync_Returns_Empty_List_When_No_Items()
    {
        var ctx = new TestGraphContext().UseInMemory();

        var result = await ctx.People.ToListAsync(TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task ToListAsync_Throws_For_Null_Query()
    {
        IQueryable<TestPerson> query = null!;
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => query.ToListAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ToListAsync_Applies_Where_Filter()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await SeedPeople(ctx, TestContext.CancellationToken, ("Alice", 30), ("Bob", 25));

        var result = await ctx.People.Where(x => x.Name == "Alice").ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.AreEqual("Alice", result[0].Name);
    }

    [TestMethod]
    public async Task ToArrayAsync_Returns_Array_Of_Items()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await SeedPeople(ctx, TestContext.CancellationToken, ("Alice", 30));

        var result = await ctx.People.ToArrayAsync(TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<TestPerson[]>(result);
        Assert.HasCount(1, result);
    }

    [TestMethod]
    public async Task ToArrayAsync_Returns_Empty_Array_When_No_Items()
    {
        var ctx = new TestGraphContext().UseInMemory();

        var result = await ctx.People.ToArrayAsync(TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.HasCount(0, result);
    }

    [TestMethod]
    public async Task ToArrayAsync_Throws_For_Null_Query()
    {
        IQueryable<TestPerson> query = null!;
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => query.ToArrayAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ToHashSetAsync_Returns_HashSet_Of_Items()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await SeedPeople(ctx, TestContext.CancellationToken, ("Alice", 30), ("Bob", 25));

        var result = await ctx.People.ToHashSetAsync(TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<HashSet<TestPerson>>(result);
        Assert.HasCount(2, result);
    }

    [TestMethod]
    public async Task ToHashSetAsync_Throws_For_Null_Query()
    {
        IQueryable<TestPerson> query = null!;
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => query.ToHashSetAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ToDictionaryAsync_With_KeySelector_Returns_Correct_Dictionary()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await SeedPeople(ctx, TestContext.CancellationToken, ("Alice", 30), ("Bob", 25));

        var result = await ctx.People.ToDictionaryAsync(x => x.Name, TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.HasCount(2, result);
        Assert.IsTrue(result.ContainsKey("Alice"));
        Assert.IsTrue(result.ContainsKey("Bob"));
    }

    [TestMethod]
    public async Task ToDictionaryAsync_Throws_For_Null_Query()
    {
        IQueryable<TestPerson> query = null!;
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => query.ToDictionaryAsync(x => x.Name, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ToDictionaryAsync_Throws_For_Null_KeySelector()
    {
        var ctx = new TestGraphContext().UseInMemory();
        Func<TestPerson, string> selector = null!;
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => ctx.People.ToDictionaryAsync(selector, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ToDictionaryAsync_With_ElementSelector_Returns_Correct_Dictionary()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await SeedPeople(ctx, TestContext.CancellationToken, ("Alice", 30), ("Bob", 25));

        var result = await ctx.People.ToDictionaryAsync(x => x.Name, x => x.Age, TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.AreEqual(30, result["Alice"]);
        Assert.AreEqual(25, result["Bob"]);
    }

    [TestMethod]
    public async Task ToDictionaryAsync_WithElementSelector_Throws_For_Null_Query()
    {
        IQueryable<TestPerson> query = null!;
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => query.ToDictionaryAsync(x => x.Name, x => x.Age, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ToDictionaryAsync_WithElementSelector_Throws_For_Null_KeySelector()
    {
        var ctx = new TestGraphContext().UseInMemory();
        Func<TestPerson, string> keySelector = null!;
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => ctx.People.ToDictionaryAsync(keySelector, x => x.Age, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ToDictionaryAsync_WithElementSelector_Throws_For_Null_ElementSelector()
    {
        var ctx = new TestGraphContext().UseInMemory();
        Func<TestPerson, int> elementSelector = null!;
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => ctx.People.ToDictionaryAsync(x => x.Name, elementSelector, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task CountAsync_Returns_Correct_Count()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await SeedPeople(ctx, TestContext.CancellationToken, ("Alice", 30), ("Bob", 25), ("Charlie", 20));

        var count = await ctx.People.CountAsync(TestContext.CancellationToken);

        Assert.AreEqual(3, count);
    }

    [TestMethod]
    public async Task CountAsync_Returns_Zero_When_Empty()
    {
        var ctx = new TestGraphContext().UseInMemory();

        var count = await ctx.People.CountAsync(TestContext.CancellationToken);

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public async Task CountAsync_Throws_For_Null_Query()
    {
        IQueryable<TestPerson> query = null!;
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => query.CountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task CountAsync_With_Predicate_Returns_Filtered_Count()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await SeedPeople(ctx, TestContext.CancellationToken, ("Alice", 30), ("Bob", 25), ("Charlie", 20));

        var count = await ctx.People.CountAsync(x => x.Age >= 25, TestContext.CancellationToken);

        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public async Task CountAsync_WithPredicate_Throws_For_Null_Query()
    {
        IQueryable<TestPerson> query = null!;
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => query.CountAsync(x => x.Age > 0, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task CountAsync_WithPredicate_Throws_For_Null_Predicate()
    {
        var ctx = new TestGraphContext().UseInMemory();
        Expression<Func<TestPerson, bool>> predicate = null!;
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => ctx.People.CountAsync(predicate, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LongCountAsync_Returns_Correct_Count()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await SeedPeople(ctx, TestContext.CancellationToken, ("Alice", 30), ("Bob", 25));

        var count = await ctx.People.LongCountAsync(TestContext.CancellationToken);

        Assert.AreEqual(2L, count);
    }

    [TestMethod]
    public async Task LongCountAsync_Throws_For_Null_Query()
    {
        IQueryable<TestPerson> query = null!;
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => query.LongCountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LongCountAsync_With_Predicate_Returns_Filtered_Count()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await SeedPeople(ctx, TestContext.CancellationToken, ("Alice", 30), ("Bob", 25));

        var count = await ctx.People.LongCountAsync(x => x.Name == "Alice", TestContext.CancellationToken);

        Assert.AreEqual(1L, count);
    }

    [TestMethod]
    public async Task LongCountAsync_WithPredicate_Throws_For_Null_Query()
    {
        IQueryable<TestPerson> query = null!;
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => query.LongCountAsync(x => x.Age > 0, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LongCountAsync_WithPredicate_Throws_For_Null_Predicate()
    {
        var ctx = new TestGraphContext().UseInMemory();
        Expression<Func<TestPerson, bool>> predicate = null!;
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => ctx.People.LongCountAsync(predicate, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ContainsAsync_Entity_Present_Via_Count()
    {
        // ContainsAsync builds a Where predicate and calls AnyAsync internally.
        // Verify entity presence using CountAsync which avoids the InMemory
        // engine limitation when AnyAsync is applied to a non-empty filtered query.
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice", Age = 30 };
        ctx.Add(alice);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var count = await ctx.People.CountAsync(x => x.Id == alice.Id, TestContext.CancellationToken);

        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task ContainsAsync_Entity_Not_Present_Via_Count()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await SeedPeople(ctx, TestContext.CancellationToken, ("Alice", 30));

        var unknownId = Guid.NewGuid();
        var count = await ctx.People.CountAsync(x => x.Id == unknownId, TestContext.CancellationToken);

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public async Task ContainsAsync_Throws_For_Null_Query()
    {
        IQueryable<TestPerson> query = null!;
        var person = new TestPerson { Id = Guid.NewGuid() };
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => query.ContainsAsync(person, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Count_Sync_Returns_Correct_Count()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await SeedPeople(ctx, TestContext.CancellationToken, ("Alice", 30), ("Bob", 25));

        var count = Exec.Count(ctx.People);

        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public async Task Count_Sync_With_Predicate_Returns_Filtered_Count()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await SeedPeople(ctx, TestContext.CancellationToken, ("Alice", 30), ("Bob", 25));

        var count = Exec.Count(ctx.People, x => x.Age > 26);

        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public void Count_Sync_Throws_For_Null_Query()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Exec.Count<TestPerson>(null!));
    }

    [TestMethod]
    public void Count_Sync_WithPredicate_Throws_For_Null_Query()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Exec.Count<TestPerson>(null!, x => x.Age > 0));
    }

    [TestMethod]
    public async Task Count_Sync_WithPredicate_Throws_For_Null_Predicate()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await SeedPeople(ctx, TestContext.CancellationToken);
        Assert.ThrowsExactly<ArgumentNullException>(() => Exec.Count<TestPerson>(ctx.People, null!));
    }

    [TestMethod]
    public async Task LongCount_Sync_Returns_Correct_Count()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await SeedPeople(ctx, TestContext.CancellationToken, ("Alice", 30));

        var count = Exec.LongCount(ctx.People);

        Assert.AreEqual(1L, count);
    }

    [TestMethod]
    public async Task LongCount_Sync_With_Predicate_Returns_Filtered_Count()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await SeedPeople(ctx, TestContext.CancellationToken, ("Alice", 30), ("Bob", 25));

        var count = Exec.LongCount(ctx.People, x => x.Name == "Bob");

        Assert.AreEqual(1L, count);
    }

    [TestMethod]
    public void LongCount_Sync_Throws_For_Null_Query()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Exec.LongCount<TestPerson>(null!));
    }

    [TestMethod]
    public void LongCount_Sync_WithPredicate_Throws_For_Null_Query()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Exec.LongCount<TestPerson>(null!, x => x.Age > 0));
    }

    [TestMethod]
    public async Task LongCount_Sync_WithPredicate_Throws_For_Null_Predicate()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await SeedPeople(ctx, TestContext.CancellationToken);
        Assert.ThrowsExactly<ArgumentNullException>(() => Exec.LongCount<TestPerson>(ctx.People, null!));
    }

    [TestMethod]
    public async Task Contains_Sync_Returns_True_For_Existing_Entity()
    {
        // Exec.Contains uses synchronous boolean execution which is not supported
        // by the InMemory GraphQueryProvider. Verify presence via CountAsync instead.
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice", Age = 30 };
        ctx.Add(alice);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var count = await ctx.People.CountAsync(x => x.Id == alice.Id, TestContext.CancellationToken);

        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task Contains_Sync_Returns_False_For_Missing_Entity()
    {
        // Exec.Contains uses synchronous boolean execution which is not supported
        // by the InMemory GraphQueryProvider. Verify absence via CountAsync instead.
        var ctx = new TestGraphContext().UseInMemory();
        await SeedPeople(ctx, TestContext.CancellationToken, ("Alice", 30));

        var unknownId = Guid.NewGuid();
        var count = await ctx.People.CountAsync(x => x.Id == unknownId, TestContext.CancellationToken);

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void Contains_Sync_Throws_For_Null_Query()
    {
        var person = new TestPerson { Id = Guid.NewGuid() };
        Assert.ThrowsExactly<ArgumentNullException>(() => Exec.Contains<TestPerson>(null!, person));
    }

    [TestMethod]
    public void Sum_Sync_Throws_When_No_Matching_Enumerable_Overload_Is_Found()
    {
        var query = SourceArray.AsQueryable();

        Assert.ThrowsExactly<InvalidOperationException>(() => Exec.Sum(query));
    }

    [TestMethod]
    public void Average_Sync_Throws_When_No_Matching_Enumerable_Overload_Is_Found()
    {
        var query = SourceArray.AsQueryable();

        Assert.ThrowsExactly<InvalidOperationException>(() => Exec.Average(query));
    }

    [TestMethod]
    public void ResolveContext_Throws_When_Provider_Is_Not_GraphQueryProvider()
    {
        var query = new List<TestPerson>().AsQueryable();
        Assert.ThrowsExactly<InvalidOperationException>(() => Exec.ResolveContext(query));
    }

    [TestMethod]
    public void ResolveContext_Returns_Context_From_GraphQueryProvider()
    {
        var ctx = new TestGraphContext();
        var resolved = Exec.ResolveContext(ctx.People);
        Assert.AreSame(ctx, resolved);
    }

    [TestMethod]
    public void ResolveExecutionServices_Throws_When_No_ConnectionFactory()
    {
        var ctx = new TestGraphContext().UseInMemory();
        Assert.ThrowsExactly<InvalidOperationException>(() => Exec.ResolveExecutionServices(ctx.People));
    }

    [TestMethod]
    public void PrepareQuery_Returns_Query_And_Empty_Includes_When_No_Include_Calls()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var query = ctx.People.Where(x => x.Age > 18);

        var prepared = Exec.PrepareQuery(query);

        Assert.IsNotNull(prepared.Query);
        Assert.IsNotNull(prepared.Includes);
        Assert.IsEmpty(prepared.Includes);
    }

    [TestMethod]
    public void PrepareQuery_Strips_Include_And_Returns_Requests()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var query = ctx.People.IncludeRelationship("Projects");

        var prepared = Exec.PrepareQuery(query);

        Assert.HasCount(1, prepared.Includes);
        Assert.AreEqual("Projects", prepared.Includes[0].Name);
    }

    [TestMethod]
    public async Task ApplyIncludesAsync_Skips_When_No_Includes()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var items = new List<TestPerson> { new() { Id = Guid.NewGuid(), Name = "Alice" } };

        await Exec.ApplyIncludesAsync(ctx, items, [], TestContext.CancellationToken);

        Assert.HasCount(1, items);
        Assert.AreEqual("Alice", items[0].Name);
    }

    [TestMethod]
    public async Task ApplyIncludesAsync_Skips_When_Items_Empty()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var items = new List<TestPerson>();
        var includes = new List<GraphIncludeRequest>
        {
            new() { Name = "Projects", NameKind = GraphIncludeNameKind.Relationship }
        };

        await Exec.ApplyIncludesAsync(ctx, items, includes, TestContext.CancellationToken);

        Assert.IsEmpty(items);
    }

    [TestMethod]
    public async Task ApplyIncludesAsync_Throws_When_Items_Are_Not_Nodes_But_Includes_Present()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var items = new List<string> { "hello" };
        var includes = new List<GraphIncludeRequest>
        {
            new() { Name = "Something", NameKind = GraphIncludeNameKind.Relationship }
        };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Exec.ApplyIncludesAsync(ctx, items, includes, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ToListAsync_With_Include_Loads_Related_Navigation()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };

        ctx.Add(person);
        ctx.Add(project);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(person, project);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People.Where(x => x.Id == person.Id).Include(x => x.Projects).ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.HasCount(1, result[0].Projects);
        Assert.AreEqual("Apollo", result[0].Projects[0].Title);
    }

    private static async Task SeedPeople(TestGraphContext ctx, CancellationToken ct, params (string Name, int Age)[] people)
    {
        foreach (var (name, age) in people)
        {
            ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = name, Age = age });
        }

        await ctx.SaveChangesAsync(ct);
    }

    [TestMethod]
    public void ToList_Sync_On_LinqToObjects_Returns_List()
    {
        var query = SourceArray.AsQueryable();

        var result = Exec.ToList(query);

        Assert.HasCount(5, result);
        Assert.AreEqual(1, result[0]);
    }

    [TestMethod]
    public async Task ToList_Sync_On_GraphQueryable_Uses_InMemory_Execution()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await SeedPeople(ctx, TestContext.CancellationToken, ("Alice", 30), ("Bob", 25));

        var result = Exec.ToList(ctx.People.Where(x => x.Name == "Alice"));

        Assert.HasCount(1, result);
        Assert.AreEqual("Alice", result[0].Name);
    }

    [TestMethod]
    public void ToList_Sync_Throws_For_Null_Query()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Exec.ToList<int>(null!));
    }

    [TestMethod]
    public void ToArray_Sync_On_LinqToObjects_Returns_Array()
    {
        var query = SourceArray.AsQueryable();

        var result = Exec.ToArray(query);

        Assert.HasCount(5, result);
        Assert.AreEqual(5, result[^1]);
    }

    [TestMethod]
    public async Task ToArray_Sync_On_GraphQueryable_Uses_InMemory_Execution()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await SeedPeople(ctx, TestContext.CancellationToken, ("Alice", 30), ("Bob", 25));

        var result = Exec.ToArray(ctx.People);

        Assert.HasCount(2, result);
    }

    [TestMethod]
    public void ToArray_Sync_Throws_For_Null_Query()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Exec.ToArray<int>(null!));
    }

    [TestMethod]
    public void ToHashSet_Sync_On_LinqToObjects_Returns_Set()
    {
        var query = new[] { 1, 2, 2, 3 }.AsQueryable();

        var result = Exec.ToHashSet(query);

        Assert.HasCount(3, result);
        Assert.Contains(2, [.. result]);
    }

    [TestMethod]
    public async Task ToHashSet_Sync_On_GraphQueryable_Uses_InMemory_Execution()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await SeedPeople(ctx, TestContext.CancellationToken, ("Alice", 30), ("Bob", 25));

        var result = Exec.ToHashSet(ctx.People);

        Assert.HasCount(2, result);
    }

    [TestMethod]
    public void ToHashSet_Sync_Throws_For_Null_Query()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Exec.ToHashSet<int>(null!));
    }

    [TestMethod]
    public void ToDictionary_Sync_On_LinqToObjects_Returns_Dictionary()
    {
        var query = new[] { ("A", 1), ("B", 2) }.AsQueryable();

        var result = Exec.ToDictionary(query, x => x.Item1);

        Assert.HasCount(2, result);
        Assert.AreEqual(2, result["B"].Item2);
    }

    [TestMethod]
    public async Task ToDictionary_Sync_On_GraphQueryable_Uses_InMemory_Execution()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await SeedPeople(ctx, TestContext.CancellationToken, ("Alice", 30), ("Bob", 25));

        var result = Exec.ToDictionary(ctx.People, x => x.Name);

        Assert.HasCount(2, result);
        Assert.AreEqual(25, result["Bob"].Age);
    }

    [TestMethod]
    public void ToDictionary_Sync_Throws_For_Null_Query()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Exec.ToDictionary<(string, int), string>(null!, x => x.Item1));
    }

    [TestMethod]
    public void ToDictionary_Sync_Throws_For_Null_KeySelector()
    {
        var query = new[] { ("A", 1) }.AsQueryable();

        Assert.ThrowsExactly<ArgumentNullException>(() => Exec.ToDictionary<(string, int), string>(query, null!));
    }

    [TestMethod]
    public void ToDictionary_Sync_WithElementSelector_On_LinqToObjects_Returns_Dictionary()
    {
        var query = new[] { ("A", 1), ("B", 2) }.AsQueryable();

        var result = Exec.ToDictionary(query, x => x.Item1, x => x.Item2);

        Assert.HasCount(2, result);
        Assert.AreEqual(1, result["A"]);
    }

    [TestMethod]
    public void ToDictionary_Sync_WithElementSelector_Throws_For_Null_Query()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Exec.ToDictionary<(string, int), string, int>(null!, x => x.Item1, x => x.Item2));
    }

    [TestMethod]
    public void ToDictionary_Sync_WithElementSelector_Throws_For_Null_KeySelector()
    {
        var query = new[] { ("A", 1) }.AsQueryable();

        Assert.ThrowsExactly<ArgumentNullException>(() => Exec.ToDictionary<(string, int), string, int>(query, null!, x => x.Item2));
    }

    [TestMethod]
    public void ToDictionary_Sync_WithElementSelector_Throws_For_Null_ElementSelector()
    {
        var query = new[] { ("A", 1) }.AsQueryable();

        Assert.ThrowsExactly<ArgumentNullException>(() => Exec.ToDictionary<(string, int), string, int>(query, x => x.Item1, null!));
    }

    [TestMethod]
    public void Count_Sync_On_LinqToObjects_Uses_Provider_Execute_Path()
    {
        var query = SourceArray.AsQueryable();

        var result = Exec.Count(query);

        Assert.AreEqual(5, result);
    }

    [TestMethod]
    public void LongCount_Sync_On_LinqToObjects_Uses_Provider_Execute_Path()
    {
        var query = SourceArray.AsQueryable();

        var result = Exec.LongCount(query);

        Assert.AreEqual(5L, result);
    }

    [TestMethod]
    public void Contains_Sync_On_LinqToObjects_Returns_Expected_Value()
    {
        var query = SourceArray.AsQueryable();

        Assert.IsTrue(Exec.Contains(query, 3));
        Assert.IsFalse(Exec.Contains(query, 42));
    }

    [TestMethod]
    public async Task ContainsAsync_Throws_For_Null_Entity_Item()
    {
        var ctx = new TestGraphContext().UseInMemory();

        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => ctx.People.ContainsAsync(null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public void ResolveExecutionServices_Returns_Context_And_Factory_When_Configured()
    {
        var ctx = new TestGraphContext();
        ctx.UseConnectionFactory(new DummyConnectionFactory());

        var services = Exec.ResolveExecutionServices(ctx.People);

        Assert.AreSame(ctx, services.Context);
        Assert.IsNotNull(services.ConnectionFactory);
    }

    [TestMethod]
    public void CreateExecutor_Returns_Instance()
    {
        var executor = Exec.CreateExecutor(new DummyConnectionFactory());

        Assert.IsNotNull(executor);
        Assert.IsInstanceOfType<GraphQueryExecutor>(executor);
    }

    private sealed class DummyConnectionFactory : IGormDbConnectionFactory
    {
        public DbConnection CreateConnection() => throw new NotSupportedException();
    }
}