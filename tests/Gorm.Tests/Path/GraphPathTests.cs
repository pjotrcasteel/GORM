using Gorm.Application.Querying.Models;
using Gorm.Core.Paths;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Path;

[TestClass]
public sealed class GraphPathTests
{
    [TestMethod]
    public void From_starts_with_no_steps_and_non_distinct_results()
    {
        var path = GraphPath.From<TestPerson>();

        Assert.IsEmpty(path.Steps);
        Assert.IsFalse(path.DistinctResultsEnabled);
    }

    [TestMethod]
    public void Outgoing_and_incoming_append_expected_steps()
    {
        var path = GraphPath
            .From<TestPerson>()
            .Outgoing<TestWorksOn, TestProject>(x => x.Role == "Lead", GraphTraversalSafeties.PreventImmediateCycles)
            .Incoming<TestWorksOn, TestPerson>(GraphTraversalSafeties.PreventNodeRevisit);

        Assert.HasCount(2, path.Steps);
        Assert.AreEqual(GraphPathDirection.Outgoing, path.Steps[0].Direction);
        Assert.AreEqual(typeof(TestWorksOn), path.Steps[0].EdgeType);
        Assert.AreEqual(typeof(TestProject), path.Steps[0].NodeType);
        Assert.AreEqual(GraphTraversalSafeties.PreventImmediateCycles, path.Steps[0].Safety);
        Assert.AreEqual(GraphPathDirection.Incoming, path.Steps[1].Direction);
        Assert.AreEqual(GraphTraversalSafeties.PreventNodeRevisit, path.Steps[1].Safety);
    }

    [TestMethod]
    public void RepeatOutgoing_adds_requested_number_of_hops()
    {
        var path = GraphPath.From<TestPerson>().RepeatOutgoing<TestKnows>(3);

        Assert.HasCount(3, path.Steps);
        Assert.IsTrue(path.Steps.All(x => x.Direction == GraphPathDirection.Outgoing));
        Assert.IsTrue(path.Steps.All(x => x.NodeType == typeof(TestPerson)));
    }

    [TestMethod]
    public void RepeatIncoming_adds_requested_number_of_hops()
    {
        var path = GraphPath.From<TestPerson>().RepeatIncoming<TestKnows>(2, x => x.Strength > 10, GraphTraversalSafeties.PreventNodeRevisit);

        Assert.HasCount(2, path.Steps);
        Assert.IsTrue(path.Steps.All(x => x.Direction == GraphPathDirection.Incoming));
        Assert.IsTrue(path.Steps.All(x => x.Safety == GraphTraversalSafeties.PreventNodeRevisit));
        Assert.IsNotNull(path.Steps[0].EdgePredicate);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void RepeatOutgoing_throws_for_non_positive_hop_count(int hopCount)
    {
        var ex = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => GraphPath.From<TestPerson>().RepeatOutgoing<TestKnows>(hopCount));

        Assert.Contains("Hop count must be greater than zero.", ex.Message);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-5)]
    public void RepeatIncoming_throws_for_non_positive_hop_count(int hopCount)
    {
        var ex = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => GraphPath.From<TestPerson>().RepeatIncoming<TestKnows>(hopCount));

        Assert.Contains("Hop count must be greater than zero.", ex.Message);
    }

    [TestMethod]
    public void Guard_methods_update_only_the_last_step()
    {
        var path = GraphPath.From<TestPerson>().Outgoing<TestKnows, TestPerson>().Outgoing<TestKnows, TestPerson>().GuardAgainstImmediateCycles().GuardAgainstNodeRevisit();

        Assert.AreEqual(GraphTraversalSafeties.None, path.Steps[0].Safety);
        Assert.AreEqual(GraphTraversalSafeties.PreventImmediateCycles | GraphTraversalSafeties.PreventNodeRevisit, path.Steps[1].Safety);
    }

    [TestMethod]
    public void Guard_methods_throw_when_path_has_no_steps()
    {
        var ex1 = Assert.ThrowsExactly<InvalidOperationException>(() => GraphPath.From<TestPerson>().GuardAgainstImmediateCycles());

        var ex2 = Assert.ThrowsExactly<InvalidOperationException>(() => GraphPath.From<TestPerson>().GuardAgainstNodeRevisit());

        Assert.Contains("requires at least one path step", ex1.Message);
        Assert.Contains("requires at least one path step", ex2.Message);
    }

    [TestMethod]
    public void DistinctResults_returns_new_path_with_same_steps_and_distinct_enabled()
    {
        var original = GraphPath.From<TestPerson>().Outgoing<TestWorksOn, TestProject>();
        var distinct = original.DistinctResults();

        Assert.HasCount(original.Steps.Count, distinct.Steps);
        Assert.IsFalse(original.DistinctResultsEnabled);
        Assert.IsTrue(distinct.DistinctResultsEnabled);
    }
}