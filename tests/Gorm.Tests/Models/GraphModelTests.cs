using Gorm.Application.Querying.Models;
using Gorm.Core.Metadata;
using Gorm.Core.Models;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Models;

[TestClass]
public sealed class GraphModelTests
{
    [TestMethod]
    public void GetNode_GetEdge_and_relationship_lookups_return_expected_mappings()
    {
        var model = TestGraphModelFactory.CreateValidModel();

        var node = model.GetNode(typeof(TestPerson));
        var edge = model.GetEdge(typeof(TestWorksOn));
        var relationships = model.GetRelationships(typeof(TestPerson));
        var relationship = model.GetRelationship<TestPerson>("Projects");
        var genericRelationship = model.GetRelationship<TestPerson>("Projects");
        var navigation = model.GetNavigation(typeof(TestPerson), nameof(TestPerson.Projects));

        Assert.AreEqual(typeof(TestPerson), node.ClrType);
        Assert.AreEqual(typeof(TestWorksOn), edge.ClrType);
        Assert.HasCount(1, relationships);
        Assert.AreSame(relationship, genericRelationship);
        Assert.AreEqual(nameof(TestPerson.Projects), navigation.PropertyName);
        Assert.AreEqual(GraphNavigationKind.Collection, navigation.Kind);
    }

    [TestMethod]
    public void GetNode_throws_when_type_is_not_mapped()
    {
        var model = TestGraphModelFactory.CreateValidModel();

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => model.GetNode(typeof(TestProject[])));

        Assert.Contains("No node mapping exists", ex.Message);
    }

    [TestMethod]
    public void GetEdge_throws_when_type_is_not_mapped()
    {
        var model = TestGraphModelFactory.CreateValidModel();

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => model.GetEdge(typeof(TestKnows)));

        Assert.Contains("No edge mapping exists", ex.Message);
    }

    [TestMethod]
    public void GetRelationship_throws_for_blank_name()
    {
        var model = TestGraphModelFactory.CreateValidModel();

        var ex = Assert.ThrowsExactly<ArgumentException>(() => model.GetRelationship<TestPerson>(" "));

        Assert.Contains("Relationship name cannot be null or whitespace", ex.Message);
    }

    [TestMethod]
    public void GetRelationship_throws_when_relationship_does_not_exist()
    {
        var model = TestGraphModelFactory.CreateValidModel();

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => model.GetRelationship<TestPerson>("Missing"));

        Assert.Contains("No relationship named 'Missing' exists", ex.Message);
    }

    [TestMethod]
    public void GetNavigation_throws_for_blank_name()
    {
        var model = TestGraphModelFactory.CreateValidModel();

        var ex = Assert.ThrowsExactly<ArgumentException>(() => model.GetNavigation(typeof(TestPerson), " "));

        Assert.Contains("Navigation property name cannot be null or whitespace", ex.Message);
    }

    [TestMethod]
    public void GetNavigation_throws_when_navigation_does_not_exist()
    {
        var model = TestGraphModelFactory.CreateValidModel();

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => model.GetNavigation(typeof(TestPerson), nameof(TestPerson.Name)));

        Assert.Contains("No navigation 'Name' exists", ex.Message);
    }

    [TestMethod]
    public void Constructor_throws_when_relationship_references_unmapped_edge()
    {
        var ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new GraphModel(
                nodes:
                [
                    new NodeTypeMapping
                    {
                        ClrType = typeof(TestPerson),
                        TableName = "Person",
                        Schema = "dbo",
                        KeyPropertyName = nameof(TestPerson.Id),
                        Properties = [],
                        Navigations = [],
                        Indexes = [],
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
                        ]
                    }
                ],
                edges: []));

        Assert.Contains("references edge type", ex.Message);
    }

    [TestMethod]
    public void Constructor_throws_when_outgoing_edge_starts_from_different_node()
    {
        var ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new GraphModel(
                nodes:
                [
                    new NodeTypeMapping
                    {
                        ClrType = typeof(TestPerson),
                        TableName = "Person",
                        Schema = "dbo",
                        KeyPropertyName = nameof(TestPerson.Id),
                        Properties = [],
                        Navigations = [],
                        Indexes = [],
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
                        ]
                    }
                ],
                edges:
                [
                    new EdgeTypeMapping
                    {
                        ClrType = typeof(TestWorksOn),
                        TableName = "WorksOn",
                        Schema = "dbo",
                        KeyPropertyName = nameof(TestWorksOn.Id),
                        FromNodeType = typeof(TestProject),
                        ToNodeType = typeof(TestProject),
                        Properties = [],
                        Indexes = []
                    }
                ]));

        Assert.Contains("starts from", ex.Message);
    }

    [TestMethod]
    public void Constructor_throws_when_outgoing_edge_targets_different_related_node()
    {
        var ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new GraphModel(
                nodes:
                [
                    new NodeTypeMapping
                    {
                        ClrType = typeof(TestPerson),
                        TableName = "Person",
                        Schema = "dbo",
                        KeyPropertyName = nameof(TestPerson.Id),
                        Properties = [],
                        Navigations = [],
                        Indexes = [],
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
                        ]
                    }
                ],
                edges:
                [
                    new EdgeTypeMapping
                    {
                        ClrType = typeof(TestWorksOn),
                        TableName = "WorksOn",
                        Schema = "dbo",
                        KeyPropertyName = nameof(TestWorksOn.Id),
                        FromNodeType = typeof(TestPerson),
                        ToNodeType = typeof(TestPerson),
                        Properties = [],
                        Indexes = []
                    }
                ]));

        Assert.Contains("points to related node", ex.Message);
    }

    [TestMethod]
    public void Constructor_throws_when_incoming_edge_ends_at_different_owner()
    {
        var ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new GraphModel(
                nodes:
                [
                    new NodeTypeMapping
                    {
                        ClrType = typeof(TestProject),
                        TableName = "Project",
                        Schema = "dbo",
                        KeyPropertyName = nameof(TestProject.Id),
                        Properties = [],
                        Navigations = [],
                        Indexes = [],
                        Relationships =
                        [
                            new GraphRelationshipMapping
                            {
                                Name = "Workers",
                                OwnerNodeType = typeof(TestProject),
                                RelatedNodeType = typeof(TestPerson),
                                EdgeType = typeof(TestWorksOn),
                                Direction = GraphTraversalDirection.Incoming
                            }
                        ]
                    }
                ],
                edges:
                [
                    new EdgeTypeMapping
                    {
                        ClrType = typeof(TestWorksOn),
                        TableName = "WorksOn",
                        Schema = "dbo",
                        KeyPropertyName = nameof(TestWorksOn.Id),
                        FromNodeType = typeof(TestPerson),
                        ToNodeType = typeof(TestPerson),
                        Properties = [],
                        Indexes = []
                    }
                ]));

        Assert.Contains("ends at", ex.Message);
    }

    [TestMethod]
    public void Constructor_throws_when_incoming_edge_starts_from_different_related_node()
    {
        var ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new GraphModel(
                nodes:
                [
                    new NodeTypeMapping
                    {
                        ClrType = typeof(TestProject),
                        TableName = "Project",
                        Schema = "dbo",
                        KeyPropertyName = nameof(TestProject.Id),
                        Properties = [],
                        Navigations = [],
                        Indexes = [],
                        Relationships =
                        [
                            new GraphRelationshipMapping
                            {
                                Name = "Workers",
                                OwnerNodeType = typeof(TestProject),
                                RelatedNodeType = typeof(TestPerson),
                                EdgeType = typeof(TestWorksOn),
                                Direction = GraphTraversalDirection.Incoming
                            }
                        ]
                    }
                ],
                edges:
                [
                    new EdgeTypeMapping
                    {
                        ClrType = typeof(TestWorksOn),
                        TableName = "WorksOn",
                        Schema = "dbo",
                        KeyPropertyName = nameof(TestWorksOn.Id),
                        FromNodeType = typeof(TestProject),
                        ToNodeType = typeof(TestProject),
                        Properties = [],
                        Indexes = []
                    }
                ]));

        Assert.Contains("expects related node", ex.Message);
    }

    [TestMethod]
    public void Constructor_throws_for_duplicate_relationship_names_per_owner()
    {
        var relationship = new GraphRelationshipMapping
        {
            Name = "Projects",
            OwnerNodeType = typeof(TestPerson),
            RelatedNodeType = typeof(TestProject),
            EdgeType = typeof(TestWorksOn),
            Direction = GraphTraversalDirection.Outgoing
        };

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new GraphModel(
                nodes:
                [
                    new NodeTypeMapping
                    {
                        ClrType = typeof(TestPerson),
                        TableName = "Person",
                        Schema = "dbo",
                        KeyPropertyName = nameof(TestPerson.Id),
                        Properties = [],
                        Navigations = [],
                        Indexes = [],
                        Relationships = [relationship, relationship]
                    },
                    new NodeTypeMapping
                    {
                        ClrType = typeof(TestProject),
                        TableName = "Project",
                        Schema = "dbo",
                        KeyPropertyName = nameof(TestProject.Id),
                        Properties = [],
                        Navigations = [],
                        Indexes = [],
                        Relationships = []
                    }
                ],
                edges:
                [
                    new EdgeTypeMapping
                    {
                        ClrType = typeof(TestWorksOn),
                        TableName = "WorksOn",
                        Schema = "dbo",
                        KeyPropertyName = nameof(TestWorksOn.Id),
                        FromNodeType = typeof(TestPerson),
                        ToNodeType = typeof(TestProject),
                        Properties = [],
                        Indexes = []
                    }
                ]));

        Assert.Contains("Duplicate relationship 'Projects'", ex.Message);
    }
}