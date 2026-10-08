using Gorm.Core.Configuration;
using Gorm.Core.Metadata;
using Gorm.Core.Models;
using Gorm.Core.Primitives;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Models;

[TestClass]
public sealed class GraphModelExtendedTests
{

    [TestMethod]
    public void GetNode_Throws_For_Unknown_Type()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<InvalidOperationException>(() => ctx.Model.GetNode(typeof(UnmappedNode)));
    }

    [TestMethod]
    public void GetEdge_Throws_For_Unknown_Type()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<InvalidOperationException>(() => ctx.Model.GetEdge(typeof(UnmappedEdge)));
    }

    [TestMethod]
    public void GetNode_Throws_For_Null_Type()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.Model.GetNode(null!));
    }

    [TestMethod]
    public void GetEdge_Throws_For_Null_Type()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.Model.GetEdge(null!));
    }

    [TestMethod]
    public void GetRelationship_Throws_For_Null_OwnerType()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.Model.GetRelationship(null!, "Projects"));
    }

    [TestMethod]
    public void GetRelationship_Throws_For_Null_RelationshipName()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentException>(() => ctx.Model.GetRelationship(typeof(TestPerson), null!));
    }

    [TestMethod]
    public void GetRelationship_Throws_For_WhiteSpace_RelationshipName()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentException>(() => ctx.Model.GetRelationship<TestPerson>("  "));
    }

    [TestMethod]
    public void GetRelationship_Throws_For_Unknown_Name()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<InvalidOperationException>(() => ctx.Model.GetRelationship<TestPerson>("DoesNotExist"));
    }

    [TestMethod]
    public void GetRelationships_Throws_For_Null_Type()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.Model.GetRelationships(null!));
    }

    [TestMethod]
    public void GetRelationship_Generic_Returns_Mapping()
    {
        var ctx = new TestGraphContext();
        var rel = ctx.Model.GetRelationship<TestPerson>("Projects");
        Assert.IsNotNull(rel);
        Assert.AreEqual("Projects", rel.Name);
    }

    [TestMethod]
    public void GraphModel_Throws_For_Duplicate_Node_Tables()
    {
        var nodes = new[]
        {
            new NodeTypeMapping {
                ClrType = typeof(TestPerson),
                TableName = "Same",
                Schema = "dbo",
                KeyPropertyName = "Id",
                Properties = [],
                Relationships = [],
                Navigations = [],
                Indexes = []
            },
            new NodeTypeMapping {
                ClrType = typeof(TestProject),
                TableName = "Same",
                Schema = "dbo",
                KeyPropertyName = "Id",
                Properties = [],
                Relationships = [],
                Navigations = [],
                Indexes = []
            }
        };

        Assert.ThrowsExactly<InvalidOperationException>(() => new GraphModel(nodes, []));
    }

    [TestMethod]
    public void GraphModel_Throws_For_Duplicate_Edge_Tables()
    {
        var edges = new[]
        {
            new EdgeTypeMapping {
                ClrType = typeof(TestWorksOn),
                TableName = "Same",
                Schema = "dbo",
                KeyPropertyName = "Id",
                Properties = [],
                Indexes = [],
                FromNodeType = typeof(TestPerson),
                ToNodeType = typeof(TestProject)
            },
            new EdgeTypeMapping {
                ClrType = typeof(TestKnows),
                TableName = "Same",
                Schema = "dbo",
                KeyPropertyName = "Id",
                Properties = [],
                Indexes = [],
                FromNodeType = typeof(TestPerson),
                ToNodeType = typeof(TestPerson)
            }
        };

        Assert.ThrowsExactly<InvalidOperationException>(() => new GraphModel([], edges));
    }

    [TestMethod]
    public void GraphModel_Throws_For_Null_Nodes()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new GraphModel(null!, []));
    }

    [TestMethod]
    public void GraphModel_Throws_For_Null_Edges()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new GraphModel([], null!));
    }

    [TestMethod]
    public void GetNavigation_Returns_Navigation_Mapping()
    {
        var ctx = new TestGraphContext();
        var nav = ctx.Model.GetNavigation(typeof(TestPerson), nameof(TestPerson.Projects));
        Assert.IsNotNull(nav);
        Assert.AreEqual(nameof(TestPerson.Projects), nav.PropertyName);
    }

    [TestMethod]
    public void GetNavigation_Throws_For_Null_Model()
    {
        GraphModel model = null!;
        Assert.ThrowsExactly<ArgumentNullException>(() => model.GetNavigation(typeof(TestPerson), "Projects"));
    }

    [TestMethod]
    public void GetNavigation_Throws_For_Null_OwnerType()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.Model.GetNavigation(null!, "Projects"));
    }

    [TestMethod]
    public void GetNavigation_Throws_For_Null_PropertyName()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentException>(() => ctx.Model.GetNavigation(typeof(TestPerson), null!));
    }

    [TestMethod]
    public void GetNavigation_Throws_For_Unknown_Navigation()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<InvalidOperationException>(() => ctx.Model.GetNavigation(typeof(TestPerson), "DoesNotExist"));
    }

    [TestMethod]
    public void GraphModelBuilder_Node_Throws_For_Null_Configure()
    {
        var builder = new GraphModelBuilder();
        Assert.ThrowsExactly<ArgumentNullException>(() => builder.Node<TestPerson>(null!));
    }

    [TestMethod]
    public void GraphModelBuilder_Edge_Throws_For_Null_Configure()
    {
        var builder = new GraphModelBuilder();
        Assert.ThrowsExactly<ArgumentNullException>(() => builder.Edge<TestWorksOn>(null!));
    }

    [TestMethod]
    public void GraphModelBuilder_Build_Returns_Valid_Model()
    {
        var builder = new GraphModelBuilder();
        builder.Node<TestPerson>(n =>
        {
            n.ToTable("Persons");
            n.HasKey(x => x.Id);
            n.Property(x => x.Name);
        });

        var model = builder.Build();

        Assert.IsNotNull(model);
        Assert.HasCount(1, model.Nodes);
    }
    private sealed class UnmappedNode : Node { }
    private sealed class UnmappedEdge : Edge { }
}