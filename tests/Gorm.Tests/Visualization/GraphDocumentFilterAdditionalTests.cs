using Gorm.Core.Visualization;

namespace Gorm.Tests.Visualization;

[TestClass]
public sealed class GraphDocumentFilterAdditionalTests
{

    [TestMethod]
    public void Focus_With_Empty_Document_Returns_Same_Document()
    {
        var doc = new GraphDocument
        {
            Nodes = [],
            Edges = []
        };

        var result = GraphDocumentFilter.Focus(doc);

        Assert.IsNotNull(result);
        Assert.IsEmpty(result.Nodes);
        Assert.IsEmpty(result.Edges);
    }

    [TestMethod]
    public void Focus_Throws_For_Null_Document()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => GraphDocumentFilter.Focus(null!));
    }

    [TestMethod]
    public void Focus_With_Unknown_RootNodeId_Returns_Original_Document()
    {
        var doc = new GraphDocument
        {
            Nodes = [new GraphDocumentNode { Id = "1", Label = "A", Type = "T" }],
            Edges = []
        };

        var result = GraphDocumentFilter.Focus(doc, rootNodeId: "NOTEXISTING");

        Assert.AreSame(doc, result);
    }

    [TestMethod]
    public void Focus_With_IncludeIncoming_Includes_Upstream_Nodes()
    {
        // A -> B -> C (root=B, includeIncoming=true should include A)
        var doc = new GraphDocument
        {
            Nodes =
            [
                new GraphDocumentNode { Id = "A", Label = "A", Type = "T" },
                new GraphDocumentNode { Id = "B", Label = "B", Type = "T" },
                new GraphDocumentNode { Id = "C", Label = "C", Type = "T" },
                new GraphDocumentNode { Id = "D", Label = "D", Type = "T" }  // disconnected
            ],
            Edges =
            [
                new GraphDocumentEdge { Id = "e1", From = "A", To = "B", Type = "E" },
                new GraphDocumentEdge { Id = "e2", From = "B", To = "C", Type = "E" }
            ]
        };

        var result = GraphDocumentFilter.Focus(doc, rootNodeId: "B", includeIncoming: true);

        var nodeIds = result.Nodes.Select(n => n.Id).ToHashSet();
        Assert.Contains("A", nodeIds, "Should include upstream node A");
        Assert.Contains("B", nodeIds, "Should include root B");
        Assert.Contains("C", nodeIds, "Should include downstream node C");
        Assert.DoesNotContain("D", nodeIds, "Should not include disconnected node D");
    }

    [TestMethod]
    public void Focus_With_MaxDepth_Zero_Only_Returns_Root()
    {
        var doc = new GraphDocument
        {
            Nodes =
            [
                new GraphDocumentNode { Id = "1", Label = "Root", Type = "T" },
                new GraphDocumentNode { Id = "2", Label = "Child", Type = "T" }
            ],
            Edges =
            [
                new GraphDocumentEdge { Id = "e1", From = "1", To = "2", Type = "E" }
            ]
        };

        var result = GraphDocumentFilter.Focus(doc, rootNodeId: "1", maxDepth: 0);

        Assert.HasCount(1, result.Nodes);
        Assert.AreEqual("1", result.Nodes[0].Id);
    }

    [TestMethod]
    public void Focus_With_MaxDepth_One_Includes_Direct_Neighbors()
    {
        var doc = new GraphDocument
        {
            Nodes =
            [
                new GraphDocumentNode { Id = "1", Label = "Root", Type = "T" },
                new GraphDocumentNode { Id = "2", Label = "Child", Type = "T" },
                new GraphDocumentNode { Id = "3", Label = "GrandChild", Type = "T" }
            ],
            Edges =
            [
                new GraphDocumentEdge { Id = "e1", From = "1", To = "2", Type = "E" },
                new GraphDocumentEdge { Id = "e2", From = "2", To = "3", Type = "E" }
            ]
        };

        var result = GraphDocumentFilter.Focus(doc, rootNodeId: "1", maxDepth: 1);

        var ids = result.Nodes.Select(n => n.Id).ToHashSet();
        Assert.Contains("1", ids);
        Assert.Contains("2", ids);
        Assert.DoesNotContain("3", ids);
    }

    [TestMethod]
    public void Focus_Uses_Document_RootNodeId_When_Arg_Is_Null()
    {
        var doc = new GraphDocument
        {
            RootNodeId = "A",
            Nodes =
            [
                new GraphDocumentNode { Id = "A", Label = "A", Type = "T" },
                new GraphDocumentNode { Id = "B", Label = "B", Type = "T" },
                new GraphDocumentNode { Id = "C", Label = "C", Type = "T" }
            ],
            Edges =
            [
                new GraphDocumentEdge { Id = "e1", From = "A", To = "B", Type = "E" }
            ]
        };

        var result = GraphDocumentFilter.Focus(doc);

        var ids = result.Nodes.Select(n => n.Id).ToHashSet();
        Assert.Contains("A", ids);
        Assert.Contains("B", ids);
        Assert.DoesNotContain("C", ids);
    }

    [TestMethod]
    public void Focus_Falls_Back_To_First_Node_When_No_RootNodeId()
    {
        var doc = new GraphDocument
        {
            Nodes =
            [
                new GraphDocumentNode { Id = "X", Label = "X", Type = "T" },
                new GraphDocumentNode { Id = "Y", Label = "Y", Type = "T" },
                new GraphDocumentNode { Id = "Z", Label = "Z", Type = "T" }
            ],
            Edges =
            [
                new GraphDocumentEdge { Id = "e1", From = "X", To = "Y", Type = "E" }
            ]
        };

        var result = GraphDocumentFilter.Focus(doc);

        var ids = result.Nodes.Select(n => n.Id).ToHashSet();
        Assert.Contains("X", ids);
        Assert.Contains("Y", ids);
        Assert.DoesNotContain("Z", ids);
    }
}