using Gorm.Core.Paths;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Path;

[TestClass]
public sealed class GraphPathApiTests
{
    [TestMethod]
    public void From_creates_start_path_and_cycle_guards_require_existing_step()
    {
        var path = GraphPath.From<TestPerson>();

        Assert.IsNotNull(path);
        Assert.ThrowsExactly<InvalidOperationException>(() => path.GuardAgainstImmediateCycles());
        Assert.ThrowsExactly<InvalidOperationException>(() => path.GuardAgainstNodeRevisit());
    }

    [TestMethod]
    public void Outgoing_incoming_repeat_and_distinct_return_new_paths()
    {
        var path = GraphPath.From<TestPerson>()
            .Outgoing<TestWorksOn, TestProject>()
            .Incoming<TestWorksOn, TestPerson>()
            .RepeatOutgoing<TestKnows>(2)
            .RepeatIncoming<TestKnows>(1)
            .GuardAgainstImmediateCycles()
            .GuardAgainstNodeRevisit()
            .DistinctResults();

        Assert.IsNotNull(path);
    }

    [TestMethod]
    public void Repeat_methods_validate_hop_count()
    {
        var path = GraphPath.From<TestPerson>();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => path.RepeatOutgoing<TestKnows>(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => path.RepeatIncoming<TestKnows>(-1));
    }
}