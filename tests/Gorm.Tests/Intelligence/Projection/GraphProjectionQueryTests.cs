using Gorm.Application.Context;
using Gorm.Application.Execution.InMemory;
using Gorm.Application.Intelligence;
using Gorm.Application.Intelligence.Algorithms.Pathfinding;
using Gorm.Application.Intelligence.Caching;
using Gorm.Core.Configuration;
using Gorm.Core.Metadata;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Intelligence.Projection;

[TestClass]
public sealed class GraphProjectionQueryTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ProjectGraphAsync_LoadsPureGormQueriesFromInMemoryProvider()
    {
        var context = new IntelligenceContext().UseInMemory();
        var first = CreateNode("first");
        var second = CreateNode("second");
        var third = CreateNode("third");
        context.Add(first);
        context.Add(second);
        context.Add(third);
        context.Connect<IntelligenceEdge, IntelligenceNode, IntelligenceNode>(first, second, edge => edge.Cost = 2);
        context.Connect<IntelligenceEdge, IntelligenceNode, IntelligenceNode>(second, third, edge => edge.Cost = 3);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        var projection = await context.ProjectGraphAsync(
            builder => builder
                .Nodes(context.Set<IntelligenceNode>().Where(node => node.IsActive))
                .Edges(context.EdgeSet<IntelligenceEdge>()),
            TestContext.CancellationToken);

        var path = projection.ShortestPath(
            first.Id,
            third.Id,
            new GraphShortestPathOptions
            {
                WeightSelector = edge => ((IntelligenceEdge)edge).Cost
            },
            TestContext.CancellationToken);

        Assert.AreEqual(3, projection.Statistics.NodeCount);
        Assert.AreEqual(2, projection.Statistics.EdgeCount);
        Assert.IsNotNull(path);
        Assert.AreEqual(5, path.TotalWeight);
    }

    [TestMethod]
    public async Task ProjectGraphSnapshotAsync_PreservesExplicitKeyAndSourceVersion()
    {
        var context = new IntelligenceContext().UseInMemory();
        var node = CreateNode("versioned");
        context.Add(node);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        var key = new Application.Intelligence.Projection.GraphProjectionKey("active-services");

        var snapshot = await context.ProjectGraphSnapshotAsync(key, 17, builder => builder.Nodes(context.Set<IntelligenceNode>()), TestContext.CancellationToken);

        Assert.AreEqual(key, snapshot.Key);
        Assert.AreEqual(17, snapshot.Version);
        Assert.AreEqual(node.Id, snapshot.Projection.Nodes.Single().Id);
    }

    [TestMethod]
    public async Task ProjectGraphCachedAsync_ReusesVersionCheckedPureGormProjection()
    {
        var context = new IntelligenceContext().UseInMemory();
        var node = CreateNode("cached");
        context.Add(node);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        var key = new Application.Intelligence.Projection.GraphProjectionKey("active-services:cached");
        var cache = new GraphProjectionCache();

        var loaded = await context.ProjectGraphCachedAsync(cache, key, 23, builder => builder.Nodes(context.Set<IntelligenceNode>()), TestContext.CancellationToken);
        var hit = await context.ProjectGraphCachedAsync(
            cache,
            key,
            23,
            builder => throw new InvalidOperationException("A cache hit must not execute configuration."),
            TestContext.CancellationToken);

        Assert.AreEqual(GraphProjectionCacheResultStatus.Loaded, loaded.Status);
        Assert.AreEqual(GraphProjectionCacheResultStatus.Hit, hit.Status);
        Assert.AreSame(loaded.Snapshot, hit.Snapshot);
    }

    [TestMethod]
    public void ProjectGraphAsync_WithQueryFromDifferentContext_Throws()
    {
        var context = new IntelligenceContext().UseInMemory();
        var differentContext = new IntelligenceContext().UseInMemory();

        var exception = Assert.ThrowsExactly<ArgumentException>(() => context.ProjectGraphAsync(
            builder => builder.Nodes(differentContext.Set<IntelligenceNode>()),
            TestContext.CancellationToken));

        Assert.Contains("created by the projected context", exception.Message);
    }

    private static IntelligenceNode CreateNode(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        IsActive = true
    };

    private sealed class IntelligenceContext : GraphContext
    {
        protected override void OnModelCreating(GraphModelBuilder modelBuilder)
        {
            modelBuilder.Node<IntelligenceNode>(node =>
            {
                node.ToTable("IntelligenceNodes");
                node.Property(value => value.Name);
                node.Property(value => value.IsActive);
                node.HasOutgoingRelationship<IntelligenceEdge, IntelligenceNode>("Outgoing", GraphRelationshipMultiplicity.Many);
                node.HasIncomingRelationship<IntelligenceEdge, IntelligenceNode>("Incoming", GraphRelationshipMultiplicity.Many);
            });

            modelBuilder.Edge<IntelligenceEdge>(edge =>
            {
                edge.ToTable("IntelligenceEdges");
                edge.From<IntelligenceNode>();
                edge.To<IntelligenceNode>();
                edge.Property(value => value.Cost);
            });
        }
    }

    private sealed class IntelligenceNode : Node
    {
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }

    private sealed class IntelligenceEdge : Edge
    {
        public double Cost { get; set; }
    }
}