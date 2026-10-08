using Gorm.Core.Visualization;

namespace Gorm.Tests.Common;

[TestClass]
public sealed class GraphDocumentFilterTests
{
    [TestMethod]
    public void Focus_filters_to_root_and_descendants_up_to_depth()
    {
        var document = new GraphDocument
        {
            RootNodeId = "A",
            Nodes =
            [
                new GraphDocumentNode { Id = "A", Label = "A", Type = "Node", IsRoot = true },
                new GraphDocumentNode { Id = "B", Label = "B", Type = "Node" },
                new GraphDocumentNode { Id = "C", Label = "C", Type = "Node" },
                new GraphDocumentNode { Id = "D", Label = "D", Type = "Node" }
            ],
            Edges =
            [
                new GraphDocumentEdge { Id = "E1", From = "A", To = "B", Type = "Link" },
                new GraphDocumentEdge { Id = "E2", From = "B", To = "C", Type = "Link" },
                new GraphDocumentEdge { Id = "E3", From = "C", To = "D", Type = "Link" }
            ]
        };

        var focused = GraphDocumentFilter.Focus(document, maxDepth: 1);

        Assert.AreEqual("A", focused.RootNodeId);
        Assert.HasCount(2, focused.Nodes);
        Assert.HasCount(1, focused.Edges);
        Assert.IsTrue(focused.Nodes.Any(x => x.Id == "A" && x.IsRoot));
        Assert.IsTrue(focused.Nodes.Any(x => x.Id == "B" && x.Depth == 1));
    }
}