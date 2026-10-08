using Gorm.Application.Querying.Models;
using Gorm.Core.Loading;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Loading;

[TestClass]
public sealed class GraphLoadedRelationshipKeyTests
{
    [TestMethod]
    public void Equality_uses_reference_identity_owner_and_all_other_components()
    {
        var owner = new object();
        var same = new GraphLoadedRelationshipKey(owner, GraphTraversalDirection.Outgoing, typeof(TestWorksOn), typeof(TestProject), includeEdge: true);
        var equal = new GraphLoadedRelationshipKey(owner, GraphTraversalDirection.Outgoing, typeof(TestWorksOn), typeof(TestProject), includeEdge: true);
        var differentOwnerReference = new GraphLoadedRelationshipKey(new object(), GraphTraversalDirection.Outgoing, typeof(TestWorksOn), typeof(TestProject), includeEdge: true);
        var differentDirection = new GraphLoadedRelationshipKey(owner, GraphTraversalDirection.Incoming, typeof(TestWorksOn), typeof(TestProject), includeEdge: true);

        Assert.IsTrue(same.Equals(equal));
        Assert.AreEqual(same.GetHashCode(), equal.GetHashCode());
        Assert.IsFalse(same.Equals(differentOwnerReference));
        Assert.IsFalse(same.Equals(differentDirection));
        Assert.IsFalse(same.Equals(null));
        Assert.IsFalse(same.Equals("not a key"));
    }

    [TestMethod]
    public void Constructor_validates_required_arguments()
    {
        var owner = new object();

        Assert.ThrowsExactly<ArgumentNullException>(
            () => new GraphLoadedRelationshipKey(null!, GraphTraversalDirection.Outgoing, typeof(TestWorksOn), typeof(TestProject), false)
        );
        Assert.ThrowsExactly<ArgumentNullException>(() => new GraphLoadedRelationshipKey(owner, GraphTraversalDirection.Outgoing, null!, typeof(TestProject), false));
        Assert.ThrowsExactly<ArgumentNullException>(() => new GraphLoadedRelationshipKey(owner, GraphTraversalDirection.Outgoing, typeof(TestWorksOn), null!, false));
    }
}