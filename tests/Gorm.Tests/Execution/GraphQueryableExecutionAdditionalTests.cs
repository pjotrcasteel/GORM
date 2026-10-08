using Gorm.Application.Execution;
using Gorm.Application.Execution.InMemory;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Execution;

[TestClass]
public sealed class GraphQueryableExecutionAdditionalTests
{
    public TestContext TestContext { get; set; } = null!;

    private static TestGraphContext CreatePopulated(out TestPerson alice, out TestProject proj)
    {
        var ctx = new TestGraphContext().UseInMemory();
        alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        proj = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };
        ctx.Add(alice);
        ctx.Add(proj);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(alice, proj, e => e.Role = "Dev");
        ctx.SaveChangesAsync().GetAwaiter().GetResult();
        return ctx;
    }

    [TestMethod]
    public async Task ToArrayAsync_Returns_Array()
    {
        var ctx = CreatePopulated(out _, out _);
        var result = await ctx.People.ToArrayAsync(TestContext.CancellationToken);
        Assert.IsInstanceOfType<TestPerson[]>(result);
        Assert.HasCount(1, result);
    }

    [TestMethod]
    public async Task ToArrayAsync_Throws_For_Null()
    {
        IQueryable<TestPerson> query = null!;
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => query.ToArrayAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ToHashSetAsync_Returns_HashSet()
    {
        var ctx = CreatePopulated(out _, out _);
        var result = await ctx.People.ToHashSetAsync(TestContext.CancellationToken);
        Assert.IsInstanceOfType<HashSet<TestPerson>>(result);
        Assert.HasCount(1, result);
    }

    [TestMethod]
    public async Task ToHashSetAsync_Throws_For_Null()
    {
        IQueryable<TestPerson> query = null!;
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => query.ToHashSetAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task CountAsync_Returns_Count()
    {
        var ctx = CreatePopulated(out _, out _);
        var count = await ctx.People.CountAsync(TestContext.CancellationToken);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task CountAsync_With_Predicate_Returns_Filtered_Count()
    {
        var ctx = CreatePopulated(out _, out _);
        var count = await ctx.People.CountAsync(x => x.Name == "Alice", TestContext.CancellationToken);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task CountAsync_WhenCancelled_ThrowsBeforeExecuting()
    {
        var ctx = CreatePopulated(out _, out _);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => ctx.People.CountAsync(cancellation.Token));
    }

    [TestMethod]
    public async Task CountAsync_Throws_For_Null()
    {
        IQueryable<TestPerson> query = null!;
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => query.CountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LongCountAsync_Returns_Count()
    {
        var ctx = CreatePopulated(out _, out _);
        var count = await ctx.People.LongCountAsync(TestContext.CancellationToken);
        Assert.AreEqual(1L, count);
    }

    [TestMethod]
    public async Task LongCountAsync_With_Predicate_Returns_Filtered_Count()
    {
        var ctx = CreatePopulated(out _, out _);
        var count = await ctx.People.LongCountAsync(x => x.Name == "Alice", TestContext.CancellationToken);
        Assert.AreEqual(1L, count);
    }

    [TestMethod]
    public async Task LongCountAsync_WhenCancelled_ThrowsBeforeExecuting()
    {
        var ctx = CreatePopulated(out _, out _);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => ctx.People.LongCountAsync(cancellation.Token));
    }

    [TestMethod]
    public async Task SaveChangesAsync_WhenCancelled_DoesNotPersistPendingChanges()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice" });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => ctx.SaveChangesAsync(cancellation.Token));

        Assert.IsTrue(ctx.ChangeTracker.HasChanges());
        Assert.IsEmpty(await ctx.People.ToListAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LongCountAsync_Throws_For_Null()
    {
        IQueryable<TestPerson> query = null!;
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => query.LongCountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task AnyAsync_Returns_True_When_Items_Exist()
    {
        var ctx = CreatePopulated(out _, out _);
        var any = await ctx.People.AnyAsync(TestContext.CancellationToken);
        Assert.IsTrue(any);
    }

    [TestMethod]
    public async Task AnyAsync_Returns_False_When_Empty()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var any = await ctx.People.AnyAsync(TestContext.CancellationToken);
        Assert.IsFalse(any);
    }

    [TestMethod]
    public async Task AnyAsync_With_Predicate_Returns_True()
    {
        var ctx = CreatePopulated(out _, out _);
        var any = await ctx.People.AnyAsync(x => x.Name == "Alice", TestContext.CancellationToken);
        Assert.IsTrue(any);
    }

    [TestMethod]
    public async Task AnyAsync_With_Predicate_Returns_False()
    {
        var ctx = CreatePopulated(out _, out _);
        var any = await ctx.People.AnyAsync(x => x.Name == "Nobody", TestContext.CancellationToken);
        Assert.IsFalse(any);
    }

    [TestMethod]
    public async Task AnyAsync_Throws_For_Null()
    {
        IQueryable<TestPerson> query = null!;
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => query.AnyAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task FirstAsync_Returns_First_Item()
    {
        var ctx = CreatePopulated(out _, out _);
        var person = await ctx.People.FirstAsync(TestContext.CancellationToken);
        Assert.IsNotNull(person);
        Assert.AreEqual("Alice", person.Name);
    }

    [TestMethod]
    public async Task FirstAsync_Throws_When_Empty()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => ctx.People.FirstAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task FirstOrDefaultAsync_Returns_Null_When_Empty()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var result = await ctx.People.FirstOrDefaultAsync(TestContext.CancellationToken);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task FirstOrDefaultAsync_Returns_Item_With_Predicate()
    {
        var ctx = CreatePopulated(out _, out _);
        var result = await ctx.People.FirstOrDefaultAsync(x => x.Name == "Alice", TestContext.CancellationToken);
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task FirstOrDefaultAsync_Returns_Null_With_Non_Matching_Predicate()
    {
        var ctx = CreatePopulated(out _, out _);
        var result = await ctx.People.FirstOrDefaultAsync(x => x.Name == "Nobody", TestContext.CancellationToken);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SingleAsync_Returns_Single_Item()
    {
        var ctx = CreatePopulated(out _, out _);
        var person = await ctx.People.SingleAsync(TestContext.CancellationToken);
        Assert.AreEqual("Alice", person.Name);
    }

    [TestMethod]
    public async Task SingleAsync_Throws_When_Empty()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => ctx.People.SingleAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task SingleAsync_Throws_When_Multiple()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice" });
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Bob" });
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => ctx.People.SingleAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task SingleOrDefaultAsync_Returns_Null_When_Empty()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var result = await ctx.People.SingleOrDefaultAsync(TestContext.CancellationToken);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SingleOrDefaultAsync_Returns_Item_When_Single()
    {
        var ctx = CreatePopulated(out _, out _);
        var result = await ctx.People.SingleOrDefaultAsync(TestContext.CancellationToken);
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task ToDictionaryAsync_Returns_Keyed_Items()
    {
        var ctx = CreatePopulated(out var alice, out _);
        var dict = await ctx.People.ToDictionaryAsync(x => x.Id, TestContext.CancellationToken);
        Assert.IsTrue(dict.ContainsKey(alice.Id));
    }

    [TestMethod]
    public async Task ToDictionaryAsync_With_Selector_Returns_Keyed_Values()
    {
        var ctx = CreatePopulated(out var alice, out _);
        var dict = await ctx.People.ToDictionaryAsync(x => x.Id, x => x.Name, TestContext.CancellationToken);
        Assert.AreEqual("Alice", dict[alice.Id]);
    }

    [TestMethod]
    public async Task ToDictionaryAsync_Throws_For_Null()
    {
        IQueryable<TestPerson> query = null!;
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => query.ToDictionaryAsync(x => x.Id, TestContext.CancellationToken));
    }
}