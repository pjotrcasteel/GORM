using Gorm.Application.Context;
using Gorm.Core.Configuration;
using Gorm.Core.Sets;

namespace Gorm.Tests.TestSupport;

internal sealed class TestGraphContext : GraphContext
{
    public GraphSet<TestPerson> People => Set<TestPerson>();
    public GraphSet<TestProject> Projects => Set<TestProject>();
    public GraphEdgeSet<TestWorksOn> WorksOn => EdgeSet<TestWorksOn>();
    public GraphEdgeSet<TestKnows> Knows => EdgeSet<TestKnows>();

    protected override void OnModelCreating(GraphModelBuilder modelBuilder)
    {
        modelBuilder.Node<TestPerson>(node =>
        {
            node.ToTable("Person");
            node.HasKey(x => x.Id);
            node.Property(x => x.Name).HasMaxLength(200);
            node.Property(x => x.Age);
            node.HasOutgoingRelationship<TestWorksOn, TestProject>("Projects");
            node.HasOutgoingRelationship<TestKnows, TestPerson>("KnownPeople");
            node.HasNavigation(x => x.Projects, "Projects");
        });

        modelBuilder.Node<TestProject>(node =>
        {
            node.ToTable("Project");
            node.HasKey(x => x.Id);
            node.Property(x => x.Title).HasMaxLength(200);
        });

        modelBuilder.Edge<TestWorksOn>(edge =>
        {
            edge.ToTable("WorksOn");
            edge.HasKey(x => x.Id);
            edge.From<TestPerson>();
            edge.To<TestProject>();
            edge.Property(x => x.Role).HasMaxLength(100);
        });

        modelBuilder.Edge<TestKnows>(edge =>
        {
            edge.ToTable("Knows");
            edge.HasKey(x => x.Id);
            edge.From<TestPerson>();
            edge.To<TestPerson>();
            edge.Property(x => x.Strength);
        });
    }
}