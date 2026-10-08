using Gorm.Application.Context;
using Gorm.Application.Diagnostics;
using Gorm.Application.Querying;
using Gorm.Core.Configuration;
using Gorm.Core.Primitives;

namespace Gorm.Playground.Core;

/// <summary>
/// Authoritative SQL translation for a deliberately bounded set of playground examples.
/// The SQL is always produced by GORM's Explain() implementation, never a JavaScript formatter.
/// </summary>
public static class PlaygroundQueryEngine
{
    private static readonly Guid ExampleId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static IReadOnlyList<string> SupportedExamples { get; } = ["outgoing", "incoming", "chained", "filter"];

    public static string Explain(string example)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(example);
        var context = new PlaygroundGraphContext();

        return example switch
        {
            "outgoing" => context.Set<PersonNode>().Where(x => x.Id == ExampleId)
                .Outgoing<WorksOnEdge, ProjectNode>().Explain().Sql,
            "incoming" => context.Set<DatabaseNode>().Where(x => x.Id == ExampleId)
                .Incoming<DependsOnEdge, ServiceNode>().Explain().Sql,
            "chained" => context.Set<ServiceNode>().Where(x => x.Id == ExampleId)
                .Outgoing<RoutesToEdge, ServiceNode>().ThenOutgoing<DependsOnEdge, DatabaseNode>().Explain().Sql,
            "filter" => context.Set<ServiceNode>().Where(x => x.State == ServiceState.Active)
                .OrderBy(x => x.Name).Skip(20).Take(25).Explain().Sql,
            _ => throw new NotSupportedException($"'{example}' has no verified GORM Explain() mapping. Arbitrary C# is not executed.")
        };
    }
}

internal sealed class PlaygroundGraphContext : GraphContext
{
    protected override void OnModelCreating(GraphModelBuilder builder)
    {
        builder.Node<PersonNode>(node =>
        {
            node.ToTable("Persons");
            node.Property(x => x.Name);
            node.HasOutgoingRelationship<WorksOnEdge, ProjectNode>(nameof(PersonNode.Projects));
            node.HasNavigation(x => x.Projects, nameof(PersonNode.Projects));
        });
        builder.Node<ProjectNode>(node =>
        {
            node.ToTable("Projects");
            node.Property(x => x.Name);
            node.HasIncomingRelationship<WorksOnEdge, PersonNode>("People");
        });
        builder.Node<ServiceNode>(node =>
        {
            node.ToTable("Services");
            node.Property(x => x.Name);
            node.Property(x => x.State);
            node.HasOutgoingRelationship<RoutesToEdge, ServiceNode>("Routes");
            node.HasIncomingRelationship<RoutesToEdge, ServiceNode>("IncomingRoutes");
            node.HasOutgoingRelationship<DependsOnEdge, DatabaseNode>("Dependencies");
        });
        builder.Node<DatabaseNode>(node =>
        {
            node.ToTable("Databases");
            node.Property(x => x.Name);
            node.HasIncomingRelationship<DependsOnEdge, ServiceNode>("DependentServices");
        });
        builder.Edge<WorksOnEdge>(edge =>
        {
            edge.ToTable("WorksOn");
            edge.From<PersonNode>();
            edge.To<ProjectNode>();
        });
        builder.Edge<RoutesToEdge>(edge =>
        {
            edge.ToTable("RoutesTo");
            edge.From<ServiceNode>();
            edge.To<ServiceNode>();
        });
        builder.Edge<DependsOnEdge>(edge =>
        {
            edge.ToTable("DependsOn");
            edge.From<ServiceNode>();
            edge.To<DatabaseNode>();
        });
    }
}

internal sealed class PersonNode : Node
{
    public string Name { get; set; } = string.Empty;
    public List<ProjectNode> Projects { get; set; } = [];
}

internal sealed class ProjectNode : Node
{
    public string Name { get; set; } = string.Empty;
}

internal enum ServiceState { Unknown, Active, Inactive }

internal sealed class ServiceNode : Node
{
    public string Name { get; set; } = string.Empty;
    public ServiceState State { get; set; }
}

internal sealed class DatabaseNode : Node
{
    public string Name { get; set; } = string.Empty;
}

internal sealed class WorksOnEdge : Edge { }
internal sealed class RoutesToEdge : Edge { }
internal sealed class DependsOnEdge : Edge { }
