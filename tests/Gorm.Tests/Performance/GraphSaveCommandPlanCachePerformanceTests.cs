using Gorm.Application.Context;
using Gorm.Application.Execution;
using Gorm.Core.Configuration;
using Gorm.Core.Primitives;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Performance;

[TestClass]
public sealed class GraphSaveCommandPlanCachePerformanceTests
{
    [TestMethod]
    public void GetNodePlan_SameMapping_ReturnsCachedInstance()
    {
        var context = new TestGraphContext();
        var mapping = context.Model.GetNode(typeof(TestPerson));

        var first = GraphSaveCommandPlanCache.GetNodePlan(mapping);
        var second = GraphSaveCommandPlanCache.GetNodePlan(mapping);

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void GetNodePlan_TestPerson_CreatesCompiledKeyGetterAndInsertSql()
    {
        var context = new TestGraphContext();
        var mapping = context.Model.GetNode(typeof(TestPerson));
        var person = new TestPerson
        {
            Id = Guid.NewGuid(),
            Name = "Alice",
            Age = 42
        };

        var plan = GraphSaveCommandPlanCache.GetNodePlan(mapping);

        Assert.AreEqual(person.Id, plan.KeyProperty.Get(person));
        Assert.Contains("INSERT INTO [dbo].[Person]", plan.InsertCommandText);
        Assert.Contains("[Id]", plan.InsertCommandText);
        Assert.Contains("[Name]", plan.InsertCommandText);
        Assert.Contains("[Age]", plan.InsertCommandText);
    }

    [TestMethod]
    public void GetEdgePlan_TestWorksOn_CreatesGraphEdgeInsertSql()
    {
        var context = new TestGraphContext();
        var mapping = context.Model.GetEdge(typeof(TestWorksOn));

        var plan = GraphSaveCommandPlanCache.GetEdgePlan(mapping);

        Assert.Contains("INSERT INTO [dbo].[WorksOn]", plan.InsertCommandText);
        Assert.Contains("$from_id", plan.InsertCommandText);
        Assert.Contains("$to_id", plan.InsertCommandText);
        Assert.Contains("[Id]", plan.InsertCommandText);
        Assert.Contains("[Role]", plan.InsertCommandText);
    }

    [TestMethod]
    public void GetEdgePlan_DeleteByNodeIds_Throws_For_Ambiguous_Disconnection()
    {
        var context = new TestGraphContext();
        var mapping = context.Model.GetEdge(typeof(TestWorksOn));

        var plan = GraphSaveCommandPlanCache.GetEdgePlan(mapping);

        Assert.Contains("COUNT_BIG(*)", plan.DeleteByNodeIdsCommandText);
        Assert.Contains("THROW 51001", plan.DeleteByNodeIdsCommandText);
        Assert.Contains("DELETE FROM [dbo].[WorksOn]", plan.DeleteByNodeIdsCommandText);
    }

    [TestMethod]
    public void GetNodePlan_ConcurrencyEntity_CreatesConcurrencyCommandsAndCompiledSetter()
    {
        var context = new ConcurrencyGraphContext();
        var mapping = context.Model.GetNode(typeof(ConcurrencyPerson));
        var person = new ConcurrencyPerson
        {
            Id = Guid.NewGuid(),
            Name = "Alice",
            Version = 1
        };

        var plan = GraphSaveCommandPlanCache.GetNodePlan(mapping);

        plan.ConcurrencyProperty!.Set!(person, 2);

        Assert.AreEqual(2, person.Version);
        Assert.Contains("AND [Version] = @p", plan.UpdateWithConcurrencyCommandText!);
        Assert.Contains("AND [Version] = @p1", plan.DeleteWithConcurrencyCommandText!);
    }

    private sealed class ConcurrencyGraphContext : GraphContext
    {
        protected override void OnModelCreating(GraphModelBuilder modelBuilder)
        {
            modelBuilder.Node<ConcurrencyPerson>(node =>
            {
                node.ToTable("Person");
                node.HasKey(x => x.Id);
                node.Property(x => x.Name).HasMaxLength(200);
                node.Property(x => x.Version);
            });
        }
    }

    private sealed class ConcurrencyPerson : Node, IHasConcurrencyToken
    {
        public string Name { get; set; } = string.Empty;
        public int Version { get; set; }
    }
}