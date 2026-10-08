using Gorm.Application.Execution;
using Gorm.Application.Querying.Models;
using Gorm.Core.Loading;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Common;

[TestClass]
public sealed class SmallTypesAndExceptionsTests
{
    [TestMethod]
    public void GraphConcurrencyException_preserves_message_and_inner_exception()
    {
        var inner = new InvalidOperationException("boom");
        var singleArgument = new GraphConcurrencyException("conflict");
        var twoArgument = new GraphConcurrencyException("conflict", inner);

        Assert.AreEqual("conflict", singleArgument.Message);
        Assert.AreEqual("conflict", twoArgument.Message);
        Assert.AreSame(inner, twoArgument.InnerException);
    }

    [TestMethod]
    public void GraphRelatedEdgeResult_stores_node_and_edge()
    {
        var edge = new TestWorksOn { Id = Guid.NewGuid(), Role = "Lead" };
        var node = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };

        var result = new GraphRelatedEdgeResult<TestWorksOn, TestProject>
        {
            Edge = edge,
            Node = node
        };

        Assert.AreSame(edge, result.Edge);
        Assert.AreSame(node, result.Node);
    }

    [TestMethod]
    public void Object_and_scalar_projections_store_bindings_and_source_metadata()
    {
        var objectProjection = new GraphObjectProjection
        {
            ResultType = typeof(TestProject)
        };

        objectProjection.Bindings.Add(new GraphProjectionBinding
        {
            TargetMemberName = nameof(TestProject.Title),
            SourcePropertyName = nameof(TestProject.Title),
            SourceKind = GraphProjectionSourceKind.Node
        });

        var scalarProjection = new GraphScalarProjection
        {
            ResultType = typeof(string),
            SourcePropertyName = nameof(TestProject.Title),
            SourceKind = GraphProjectionSourceKind.Node
        };

        Assert.HasCount(1, objectProjection.Bindings);
        Assert.AreEqual(nameof(TestProject.Title), objectProjection.Bindings[0].TargetMemberName);
        Assert.AreEqual(nameof(TestProject.Title), scalarProjection.SourcePropertyName);
        Assert.AreEqual(GraphProjectionSourceKind.Node, scalarProjection.SourceKind);
    }
}