using Gorm.Application.Tracking;

namespace Gorm.Tests.Tracking;

[TestClass]
public sealed class GraphRelationshipFixupKeyTests
{
    [TestMethod]
    public void RecordStruct_Equality_Works_For_Same_Values()
    {
        var id = Guid.NewGuid();
        var left = new GraphRelationshipFixupKey(typeof(string), id, "Friends", true);
        var right = new GraphRelationshipFixupKey(typeof(string), id, "Friends", true);

        Assert.AreEqual(left, right);
        Assert.AreEqual(left.GetHashCode(), right.GetHashCode());
    }

    [TestMethod]
    public void RecordStruct_Inequality_Works_For_Different_Values()
    {
        var id = Guid.NewGuid();
        var left = new GraphRelationshipFixupKey(typeof(string), id, "Friends", true);
        var right = new GraphRelationshipFixupKey(typeof(string), id, "Friends", false);

        Assert.AreNotEqual(left, right);
    }
}