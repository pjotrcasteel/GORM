using Gorm.Core.Visualization;

namespace Gorm.Tests.Common;

[TestClass]
public sealed class GraphDocumentBuilderAndMergerTests
{
    private sealed class DemoNode
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Score { get; set; }
    }

    private sealed class DemoEdge
    {
        public string Id { get; set; } = string.Empty;
        public string FromId { get; set; } = string.Empty;
        public string ToId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
    }

    [TestMethod]
    public void FromEnvelope_builds_document_using_default_property_conventions()
    {
        var envelope = new GraphEntityCollectionEnvelope<DemoNode, DemoEdge>
        {
            RootNodeId = "A",
            Nodes =
            [
                new DemoNode { Id = "A", Name = "Alpha", Score = 1 },
                new DemoNode { Id = "B", Name = "Beta", Score = 2 }
            ],
            Edges =
            [
                new DemoEdge { Id = "E1", FromId = "A", ToId = "B", Kind = "link" }
            ]
        };

        var document = GraphDocumentBuilder.FromEnvelope(envelope);

        Assert.AreEqual("A", document.RootNodeId);
        Assert.HasCount(2, document.Nodes);
        Assert.HasCount(1, document.Edges);
        Assert.IsTrue(document.Nodes.Single(x => x.Id == "A").IsRoot);
        Assert.AreEqual("Alpha", document.Nodes.Single(x => x.Id == "A").Label);
        Assert.IsTrue(document.Nodes.Single(x => x.Id == "A").Properties.ContainsKey(nameof(DemoNode.Score)));
        Assert.AreEqual("DemoEdge", document.Edges[0].Type);
    }

    [TestMethod]
    public void FromEnvelope_throws_when_required_default_properties_are_missing()
    {
        var envelope = new GraphEntityCollectionEnvelope<object, DemoEdge>
        {
            RootNodeId = "root",
            Nodes = [new object()],
            Edges = []
        };

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => GraphDocumentBuilder.FromEnvelope(envelope));
        Assert.Contains("Property 'Id' is required", ex.Message);
    }

    [TestMethod]
    public void Merge_combines_nodes_edges_metadata_and_preserves_first_root()
    {
        var left = new GraphDocument
        {
            RootNodeId = "A",
            Nodes = [new GraphDocumentNode { Id = "A", Label = "Alpha", Type = "Node" }],
            Edges = [],
            Metadata = new Dictionary<string, object?> { ["version"] = 1 }
        };

        var right = new GraphDocument
        {
            RootNodeId = "B",
            Nodes =
            [
                new GraphDocumentNode { Id = "A", Label = "Alpha updated", Type = "Node" },
                new GraphDocumentNode { Id = "B", Label = "Beta", Type = "Node" }
            ],
            Edges = [new GraphDocumentEdge { Id = "E1", From = "A", To = "B", Type = "Link" }],
            Metadata = new Dictionary<string, object?> { ["version"] = 2, ["tenant"] = "demo" }
        };

        var merged = GraphDocumentMerger.Merge(left, right);

        Assert.AreEqual("A", merged.RootNodeId);
        Assert.HasCount(2, merged.Nodes);
        Assert.HasCount(1, merged.Edges);
        Assert.AreEqual("Alpha updated", merged.Nodes.Single(x => x.Id == "A").Label);
        Assert.AreEqual(2, merged.Metadata["version"]);
        Assert.AreEqual("demo", merged.Metadata["tenant"]);
    }
}