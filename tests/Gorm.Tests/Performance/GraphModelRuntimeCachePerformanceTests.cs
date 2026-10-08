using Gorm.Application.Querying.Models;
using Gorm.Core.Metadata;
using Gorm.Core.Models;
using Gorm.Core.Primitives;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Performance;

[TestClass]
public sealed class GraphModelRuntimeCachePerformanceTests
{
    [TestMethod]
    public void GetNavigation_ConfiguredNavigation_ReturnsCachedNavigation()
    {
        var context = new TestGraphContext();

        var navigation = context.Model.GetNavigation(typeof(TestPerson), nameof(TestPerson.Projects));

        Assert.AreEqual(nameof(TestPerson.Projects), navigation.PropertyName);
        Assert.AreEqual("Projects", navigation.RelationshipName);
        Assert.AreEqual(GraphNavigationKind.Collection, navigation.Kind);
    }

    [TestMethod]
    public void GetNavigationsForRelationship_ConfiguredRelationship_ReturnsNavigationArray()
    {
        var context = new TestGraphContext();

        var navigations = context.Model.GetNavigationsForRelationship(typeof(TestPerson), "Projects");

        Assert.HasCount(1, navigations);
        Assert.AreEqual(nameof(TestPerson.Projects), navigations[0].PropertyName);
    }

    [TestMethod]
    public void GetNavigationsForRelationship_RelationshipWithoutNavigation_ReturnsEmptyArray()
    {
        var context = new TestGraphContext();

        var navigations = context.Model.GetNavigationsForRelationship(typeof(TestPerson), "KnownPeople");

        Assert.IsEmpty(navigations);
    }

    [TestMethod]
    public void GetInverseRelationships_ConfiguredInverseRelationship_ReturnsPrecomputedInverse()
    {
        var model = CreateModelWithInverseRelationship();

        var forward = model.GetRelationship<TestPerson>("Projects");
        var inverse = model.GetInverseRelationships(forward, typeof(TestProject), typeof(TestPerson));

        Assert.HasCount(1, inverse);
        Assert.AreEqual("People", inverse[0].Name);
        Assert.AreEqual(GraphTraversalDirection.Incoming, inverse[0].Direction);
    }

    [TestMethod]
    public void GetNodeProjectionPropertyNames_KeyAndMappedProperties_ReturnsKeyFirstWithoutDuplicateKey()
    {
        var context = new TestGraphContext();

        var propertyNames = context.Model.GetNodeProjectionPropertyNames(typeof(TestPerson));

        CollectionAssert.AreEqual(
            new[]
            {
                nameof(Node.Id),
                nameof(TestPerson.Name),
                nameof(TestPerson.Age)
            },
            propertyNames);
    }

    [TestMethod]
    public void GetEdgeProjectionPropertyNames_KeyAndMappedProperties_ReturnsKeyFirstWithoutDuplicateKey()
    {
        var context = new TestGraphContext();

        var propertyNames = context.Model.GetEdgeProjectionPropertyNames(typeof(TestWorksOn));

        CollectionAssert.AreEqual(
            new[]
            {
                nameof(Edge.Id),
                nameof(TestWorksOn.Role)
            },
            propertyNames);
    }

    [TestMethod]
    public void TryGetNode_KnownType_ReturnsTrueAndMapping()
    {
        var context = new TestGraphContext();

        var found = context.Model.TryGetNode(typeof(TestPerson), out var mapping);

        Assert.IsTrue(found);
        Assert.IsNotNull(mapping);
        Assert.AreEqual(typeof(TestPerson), mapping.ClrType);
    }

    [TestMethod]
    public void TryGetEdge_KnownType_ReturnsTrueAndMapping()
    {
        var context = new TestGraphContext();

        var found = context.Model.TryGetEdge(typeof(TestWorksOn), out var mapping);

        Assert.IsTrue(found);
        Assert.IsNotNull(mapping);
        Assert.AreEqual(typeof(TestWorksOn), mapping.ClrType);
    }

    private static GraphModel CreateModelWithInverseRelationship() => new(
        nodes:
        [
            new NodeTypeMapping
            {
                ClrType = typeof(TestPerson),
                TableName = "Person",
                Schema = "dbo",
                KeyPropertyName = nameof(Node.Id),
                Properties =
                [
                    new PropertyMapping
                    {
                        PropertyName = nameof(TestPerson.Name),
                        PropertyType = typeof(string)
                    }
                ],
                Relationships =
                [
                    new GraphRelationshipMapping
                    {
                        Name = "Projects",
                        OwnerNodeType = typeof(TestPerson),
                        RelatedNodeType = typeof(TestProject),
                        EdgeType = typeof(TestWorksOn),
                        Direction = GraphTraversalDirection.Outgoing
                    }
                ],
                Navigations = [],
                Indexes = []
            },
            new NodeTypeMapping
            {
                ClrType = typeof(TestProject),
                TableName = "Project",
                Schema = "dbo",
                KeyPropertyName = nameof(Node.Id),
                Properties =
                [
                    new PropertyMapping
                    {
                        PropertyName = nameof(TestProject.Title),
                        PropertyType = typeof(string)
                    }
                ],
                Relationships =
                [
                    new GraphRelationshipMapping
                    {
                        Name = "People",
                        OwnerNodeType = typeof(TestProject),
                        RelatedNodeType = typeof(TestPerson),
                        EdgeType = typeof(TestWorksOn),
                        Direction = GraphTraversalDirection.Incoming
                    }
                ],
                Navigations = [],
                Indexes = []
            }
        ],
        edges:
        [
            new EdgeTypeMapping
            {
                ClrType = typeof(TestWorksOn),
                TableName = "WorksOn",
                Schema = "dbo",
                KeyPropertyName = nameof(Edge.Id),
                FromNodeType = typeof(TestPerson),
                ToNodeType = typeof(TestProject),
                Properties =
                [
                    new PropertyMapping
                    {
                        PropertyName = nameof(TestWorksOn.Role),
                        PropertyType = typeof(string)
                    }
                ],
                Indexes = []
            }
        ]);
}