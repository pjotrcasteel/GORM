using System.Text.Json;
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

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Evaluate a validated v1 query intent with GORM's actual LINQ provider.</summary>
    public static PlaygroundExplainResponse ExplainIntent(string json)
    {
        var intent = PlaygroundQueryIntent.Parse(json);
        var state = intent.State == "Active" ? ServiceState.Active : ServiceState.Inactive;
        var context = new PlaygroundGraphContext();

        var explanation = context.Set<ServiceNode>().Where(x => x.State == state)
            .OrderBy(x => x.Name).Skip(intent.Skip).Take(intent.Take).Explain();

        var parameters = explanation.Parameters.Select(parameter =>
            new PlaygroundBoundParameter(parameter.Name, parameter.Value, parameter.DbType?.ToString())).ToArray();
        return new PlaygroundExplainResponse(explanation.Sql, parameters, explanation.DebugView);
    }

    /// <summary>JSON bridge shared by native parity tests and browser WebAssembly.</summary>
    public static string ExplainIntentJson(string json) => JsonSerializer.Serialize(ExplainIntent(json), JsonOptions);

    /// <summary>Routes are discovered from the actual GORM model, not invented by the browser.</summary>
    public static IReadOnlyList<PlaygroundTraversalRoute> GetTraversalCatalog()
    {
        var context = new PlaygroundGraphContext();

        return [.. context.Model.Nodes.SelectMany(node => node.Relationships)
            .Select(relationship => new PlaygroundTraversalRoute(relationship.OwnerNodeType.Name,
                relationship.Direction.ToString().ToLowerInvariant(), relationship.EdgeType.Name, relationship.RelatedNodeType.Name))
            .Where(IsSupportedTraversal)
            .OrderBy(route => route.Root, StringComparer.Ordinal)
            .ThenBy(route => route.Direction, StringComparer.Ordinal)
            .ThenBy(route => route.Edge, StringComparer.Ordinal)
            .ThenBy(route => route.Target, StringComparer.Ordinal)];
    }

    public static string GetTraversalCatalogJson() => JsonSerializer.Serialize(GetTraversalCatalog(), JsonOptions);

    /// <summary>Generate single-hop SQL via real GORM Explain, only after model route validation.</summary>
    public static PlaygroundExplainResponse ExplainTraversal(string json)
    {
        var intent = PlaygroundTraversalIntent.Parse(json);
        var context = new PlaygroundGraphContext();
        var route = new PlaygroundTraversalRoute(intent.Root, intent.Direction, intent.Edge, intent.Target);
        if (!IsSupportedTraversal(route))
        {
            throw new NotSupportedException("The requested graph traversal is not supported by this Playground.");
        }

        var sourceType = context.Model.Nodes.FirstOrDefault(node => node.ClrType.Name == intent.Root)?.ClrType;
        if (sourceType is null || !context.Model.GetRelationships(sourceType).Any(relationship =>
                relationship.RelatedNodeType.Name == intent.Target && relationship.EdgeType.Name == intent.Edge
                && string.Equals(relationship.Direction.ToString(), intent.Direction, StringComparison.OrdinalIgnoreCase)))
        {
            throw new NotSupportedException("Traversal does not exist in GORM's active graph model.");
        }

        var id = intent.NodeId;
        var explanation = (intent.Root, intent.Direction, intent.Edge, intent.Target) switch
        {
            ("PersonNode", "outgoing", "WorksOnEdge", "ProjectNode") =>
                context.Set<PersonNode>().Where(x => x.Id == id).Outgoing<WorksOnEdge, ProjectNode>().Explain(),
            ("ProjectNode", "incoming", "WorksOnEdge", "PersonNode") =>
                context.Set<ProjectNode>().Where(x => x.Id == id).Incoming<WorksOnEdge, PersonNode>().Explain(),
            ("ServiceNode", "outgoing", "RoutesToEdge", "ServiceNode") =>
                context.Set<ServiceNode>().Where(x => x.Id == id).Outgoing<RoutesToEdge, ServiceNode>().Explain(),
            ("ServiceNode", "incoming", "RoutesToEdge", "ServiceNode") =>
                context.Set<ServiceNode>().Where(x => x.Id == id).Incoming<RoutesToEdge, ServiceNode>().Explain(),
            ("ServiceNode", "outgoing", "DependsOnEdge", "DatabaseNode") =>
                context.Set<ServiceNode>().Where(x => x.Id == id).Outgoing<DependsOnEdge, DatabaseNode>().Explain(),
            ("DatabaseNode", "incoming", "DependsOnEdge", "ServiceNode") =>
                context.Set<DatabaseNode>().Where(x => x.Id == id).Incoming<DependsOnEdge, ServiceNode>().Explain(),
            _ => throw new NotSupportedException("No executable GORM Explain mapping exists for the requested traversal.")
        };

        return new PlaygroundExplainResponse(explanation.Sql,
            [.. explanation.Parameters.Select(parameter =>
                new PlaygroundBoundParameter(parameter.Name, parameter.Value, parameter.DbType?.ToString()))],
            explanation.DebugView);
    }

    public static string ExplainTraversalJson(string json) => JsonSerializer.Serialize(ExplainTraversal(json), JsonOptions);

    /// <summary>Find two-hop paths by composing the actual GORM model's valid single-hop routes.</summary>
    public static IReadOnlyList<PlaygroundPathRoute> GetPathCatalog()
    {
        var routes = GetTraversalCatalog();
        return [.. (from first in routes
                    from second in routes
                    where first.Target == second.Root
                    let path = new PlaygroundPathRoute(first.Root,
                        [new PlaygroundPathHop(first.Direction, first.Edge, first.Target),
                            new PlaygroundPathHop(second.Direction, second.Edge, second.Target)])
                    where IsSupportedPath(path)
                    orderby path.Root, first.Direction, first.Edge, second.Direction, second.Edge
                    select path)];
    }

    public static string GetPathCatalogJson() => JsonSerializer.Serialize(GetPathCatalog(), JsonOptions);

    /// <summary>Revalidate both hops against GORM's registered model before real LINQ translation.</summary>
    public static PlaygroundExplainResponse ExplainPath(string json)
    {
        var intent = PlaygroundPathIntent.Parse(json);
        var path = new PlaygroundPathRoute(intent.Root, intent.Hops);
        if (!IsSupportedPath(path) || !GetPathCatalog().Any(candidate => SamePath(path, candidate)))
        {
            throw new NotSupportedException("The requested two-hop traversal is not mapped by this GORM model.");
        }

        var context = new PlaygroundGraphContext();
        var id = intent.NodeId;
        var first = intent.Hops[0];
        var second = intent.Hops[1];
        var explanation = (intent.Root, first.Direction, first.Edge, first.Target, second.Direction, second.Edge, second.Target) switch
        {
            ("ServiceNode", "outgoing", "RoutesToEdge", "ServiceNode", "outgoing", "DependsOnEdge", "DatabaseNode") =>
                context.Set<ServiceNode>().Where(x => x.Id == id).Outgoing<RoutesToEdge, ServiceNode>()
                    .ThenOutgoing<DependsOnEdge, DatabaseNode>().Explain(),
            ("ServiceNode", "incoming", "RoutesToEdge", "ServiceNode", "outgoing", "DependsOnEdge", "DatabaseNode") =>
                context.Set<ServiceNode>().Where(x => x.Id == id).Incoming<RoutesToEdge, ServiceNode>()
                    .ThenOutgoing<DependsOnEdge, DatabaseNode>().Explain(),
            ("DatabaseNode", "incoming", "DependsOnEdge", "ServiceNode", "outgoing", "RoutesToEdge", "ServiceNode") =>
                context.Set<DatabaseNode>().Where(x => x.Id == id).Incoming<DependsOnEdge, ServiceNode>()
                    .ThenOutgoing<RoutesToEdge, ServiceNode>().Explain(),
            ("DatabaseNode", "incoming", "DependsOnEdge", "ServiceNode", "incoming", "RoutesToEdge", "ServiceNode") =>
                context.Set<DatabaseNode>().Where(x => x.Id == id).Incoming<DependsOnEdge, ServiceNode>()
                    .ThenIncoming<RoutesToEdge, ServiceNode>().Explain(),
            _ => throw new NotSupportedException("No typed GORM LINQ translation is registered for this two-hop path.")
        };

        return new PlaygroundExplainResponse(explanation.Sql,
            [.. explanation.Parameters.Select(parameter =>
                new PlaygroundBoundParameter(parameter.Name, parameter.Value, parameter.DbType?.ToString()))],
            explanation.DebugView);
    }

    public static string ExplainPathJson(string json) => JsonSerializer.Serialize(ExplainPath(json), JsonOptions);

    private static bool SamePath(PlaygroundPathRoute left, PlaygroundPathRoute right) =>
        left.Root == right.Root && left.Hops.Count == 2 && right.Hops.Count == 2
        && left.Hops[0] == right.Hops[0] && left.Hops[1] == right.Hops[1];

    private static bool IsSupportedPath(PlaygroundPathRoute path)
    {
        if (path.Hops.Count != 2)
        {
            return false;
        }

        var first = path.Hops[0];
        var second = path.Hops[1];
        return (path.Root, first.Direction, first.Edge, first.Target, second.Direction, second.Edge, second.Target) is
            ("ServiceNode", "outgoing", "RoutesToEdge", "ServiceNode", "outgoing", "DependsOnEdge", "DatabaseNode")
            or ("ServiceNode", "incoming", "RoutesToEdge", "ServiceNode", "outgoing", "DependsOnEdge", "DatabaseNode")
            or ("DatabaseNode", "incoming", "DependsOnEdge", "ServiceNode", "outgoing", "RoutesToEdge", "ServiceNode")
            or ("DatabaseNode", "incoming", "DependsOnEdge", "ServiceNode", "incoming", "RoutesToEdge", "ServiceNode");
    }

    private static bool IsSupportedTraversal(PlaygroundTraversalRoute route) =>
        (route.Root, route.Direction, route.Edge, route.Target) is
            ("PersonNode", "outgoing", "WorksOnEdge", "ProjectNode")
            or ("ProjectNode", "incoming", "WorksOnEdge", "PersonNode")
            or ("ServiceNode", "outgoing", "RoutesToEdge", "ServiceNode")
            or ("ServiceNode", "incoming", "RoutesToEdge", "ServiceNode")
            or ("ServiceNode", "outgoing", "DependsOnEdge", "DatabaseNode")
            or ("DatabaseNode", "incoming", "DependsOnEdge", "ServiceNode");

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
