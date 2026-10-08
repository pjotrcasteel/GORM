using Gorm.Application.Tracking;
using Gorm.Core.Loading;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Tracking;

[TestClass]
public sealed class GraphRelationshipFixupStoreTests
{
    [TestMethod]
    public void Set_and_try_get_related_round_trip_plain_related_nodes()
    {
        var owner = new TestPerson { Id = Guid.NewGuid() };
        var project1 = new TestProject { Id = Guid.NewGuid(), Title = "A" };
        var project2 = new TestProject { Id = Guid.NewGuid(), Title = "B" };
        var store = new GraphRelationshipFixupStore();

        store.Set(owner, "Projects", includeEdge: false, [project1, null!, project2]);

        var found = store.TryGetRelated<TestProject>(owner, "Projects", out var related);

        Assert.IsTrue(found);
        Assert.IsNotNull(related);
        CollectionAssert.AreEquivalent(new[] { project1, project2 }, related.ToArray());
    }

    [TestMethod]
    public void TryGetRelated_returns_false_when_relationship_was_not_set()
    {
        var owner = new TestPerson { Id = Guid.NewGuid() };
        var store = new GraphRelationshipFixupStore();

        var found = store.TryGetRelated<TestProject>(owner, "Projects", out var related);

        Assert.IsFalse(found);
        Assert.IsNull(related);
    }

    [TestMethod]
    public void TryGetRelatedWithEdges_returns_items_for_edge_includes()
    {
        var owner = new TestPerson { Id = Guid.NewGuid() };
        var result = new GraphRelatedEdgeResult<TestWorksOn, TestProject>
        {
            Edge = new TestWorksOn { Id = Guid.NewGuid(), Role = "Lead" },
            Node = new TestProject { Id = Guid.NewGuid(), Title = "A" }
        };
        var store = new GraphRelationshipFixupStore();

        store.Set(owner, "Projects", includeEdge: true, [result]);

        var found = store.TryGetRelatedWithEdges(owner, "Projects", out var related);

        Assert.IsTrue(found);
        Assert.IsNotNull(related);
        Assert.HasCount(1, related);
        Assert.AreSame(result, related[0]);
    }

    [TestMethod]
    public void Add_deduplicates_entities_by_node_id()
    {
        var owner = new TestPerson { Id = Guid.NewGuid() };
        var firstInstance = new TestProject { Id = Guid.NewGuid(), Title = "A" };
        var secondInstanceWithSameId = new TestProject { Id = firstInstance.Id, Title = "A copy" };
        var store = new GraphRelationshipFixupStore();

        store.Add(owner, "Projects", includeEdge: false, firstInstance);
        store.Add(owner, "Projects", includeEdge: false, secondInstanceWithSameId);

        store.TryGetRelated<TestProject>(owner, "Projects", out var related);

        Assert.IsNotNull(related);
        Assert.HasCount(1, related);
        Assert.AreSame(firstInstance, related[0]);
    }

    [TestMethod]
    public void Add_deduplicates_related_edge_results_by_edge_id()
    {
        var owner = new TestPerson { Id = Guid.NewGuid() };
        var edgeId = Guid.NewGuid();
        var first = new GraphRelatedEdgeResult<TestWorksOn, TestProject>
        {
            Edge = new TestWorksOn { Id = edgeId, Role = "Lead" },
            Node = new TestProject { Id = Guid.NewGuid(), Title = "A" }
        };
        var second = new GraphRelatedEdgeResult<TestWorksOn, TestProject>
        {
            Edge = new TestWorksOn { Id = edgeId, Role = "Lead copy" },
            Node = new TestProject { Id = Guid.NewGuid(), Title = "B" }
        };
        var store = new GraphRelationshipFixupStore();

        store.Add(owner, "Projects", includeEdge: true, first);
        store.Add(owner, "Projects", includeEdge: true, second);

        store.TryGetRelatedWithEdges(owner, "Projects", out var related);

        Assert.IsNotNull(related);
        Assert.HasCount(1, related);
        Assert.AreSame(first, related[0]);
    }

    [TestMethod]
    public void Add_deduplicates_unknown_objects_by_reference()
    {
        var owner = new TestPerson { Id = Guid.NewGuid() };
        var tag = new object();
        var store = new GraphRelationshipFixupStore();

        store.Add(owner, "Tags", includeEdge: true, tag);
        store.Add(owner, "Tags", includeEdge: true, tag);

        var found = store.TryGetRelatedWithEdges(owner, "Tags", out var related);

        Assert.IsTrue(found);
        Assert.IsNotNull(related);
        Assert.HasCount(1, related);
    }

    [TestMethod]
    public void Remove_deletes_by_id_or_reference_and_ignores_missing_bucket()
    {
        var owner = new TestPerson { Id = Guid.NewGuid() };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "A" };
        var tag = new object();
        var store = new GraphRelationshipFixupStore();

        store.Add(owner, "Projects", includeEdge: false, project);
        store.Add(owner, "Tags", includeEdge: true, tag);
        store.Remove(owner, "Missing", includeEdge: false, new TestProject { Id = project.Id });
        store.Remove(owner, "Projects", includeEdge: false, new TestProject { Id = project.Id });
        store.Remove(owner, "Tags", includeEdge: true, tag);

        Assert.IsTrue(store.TryGetRelated<TestProject>(owner, "Projects", out var relatedProjects));
        Assert.IsEmpty(relatedProjects ?? []);
        Assert.IsTrue(store.TryGetRelatedWithEdges(owner, "Tags", out var relatedTags));
        Assert.IsEmpty(relatedTags ?? []);
    }

    [TestMethod]
    public void Clear_removes_all_relationship_state()
    {
        var owner = new TestPerson { Id = Guid.NewGuid() };
        var store = new GraphRelationshipFixupStore();
        store.Add(owner, "Projects", includeEdge: false, new TestProject { Id = Guid.NewGuid() });

        store.Clear();

        Assert.IsFalse(store.TryGetRelated<TestProject>(owner, "Projects", out _));
    }

    [TestMethod]
    public void Methods_validate_arguments_and_relationship_names()
    {
        var owner = new TestPerson { Id = Guid.NewGuid() };
        var store = new GraphRelationshipFixupStore();

        Assert.ThrowsExactly<ArgumentNullException>(() => store.Set(null!, "Projects", false, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.Set(owner, "Projects", false, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.Add(null!, "Projects", false, new TestProject()));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.Add(owner, "Projects", false, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.Remove(null!, "Projects", false, new TestProject()));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.Remove(owner, "Projects", false, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.TryGetRelated<TestProject>(null!, "Projects", out _));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.TryGetRelatedWithEdges(null!, "Projects", out _));

        var ex1 = Assert.ThrowsExactly<ArgumentException>(() => store.Set(owner, " ", false, []));
        var ex2 = Assert.ThrowsExactly<ArgumentException>(() => store.Add(owner, " ", false, new TestProject()));
        var ex3 = Assert.ThrowsExactly<ArgumentException>(() => store.Remove(owner, " ", false, new TestProject()));

        Assert.Contains("Relationship name cannot be null or whitespace", ex1.Message);
        Assert.Contains("Relationship name cannot be null or whitespace", ex2.Message);
        Assert.Contains("Relationship name cannot be null or whitespace", ex3.Message);
    }
}