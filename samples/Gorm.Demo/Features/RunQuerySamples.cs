using Gorm.Application.Diagnostics;
using Gorm.Application.Execution;
using Gorm.Application.Querying;
using Gorm.Core.Visualization;
using Gorm.Demo.Domain.Edges;
using Gorm.Demo.Domain.Nodes;
using Gorm.Demo.Infrastructure;

namespace Gorm.Demo.Features;

/// <summary>
/// Represents run query samples.
/// </summary>
public static class RunQuerySamples
{
    private readonly static Dictionary<int, string> Chapters = new()
    {
        { 1, "Validate database schema" },
        { 2, "Seed nodes and edges through Gorm" },
        { 3, "Read all characteristic nodes" },
        { 4, "Traverse outgoing edges from Internet speed" },
        { 5, "Traverse incoming edges into Speed tier" },
        { 6, "Eager load with Include" },
        { 7, "Eager load with Include + ThenInclude" },
        { 8, "No-tracking query" },
        { 9, "Query diagnostics / explain" },
        { 10, "Release-grade query operators" },
        { 11, "Build GraphDocument for visualization" }
    };

    /// <summary>
    /// Runs the operation.
    /// </summary>
    /// <param name="db">The graph context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task RunAsync(SqlSpecificationGraphContext db, CancellationToken cancellationToken = default)
    {
        await RunChapterAsync(1, () => RunSchemaValidationAsync(db, cancellationToken));
        await RunChapterAsync(2, () => RunSeedAsync(db, cancellationToken));
        await RunChapterAsync(3, () => RunListAllNodesAsync(db, cancellationToken));
        await RunChapterAsync(4, () => RunOutgoingMappingsAsync(db, cancellationToken));
        await RunChapterAsync(5, () => RunIncomingMappingsAsync(db, cancellationToken));
        await RunChapterAsync(6, () => RunIncludeAsync(db, cancellationToken));
        await RunChapterAsync(7, () => RunNestedIncludeAsync(db, cancellationToken));
        await RunChapterAsync(8, () => RunAsNoTrackingAsync(db, cancellationToken));
        await RunChapterAsync(9, () => RunExplainAsync(db));
        await RunChapterAsync(10, () => RunAsyncOperatorsAsync(db, cancellationToken));
        await RunChapterAsync(11, () => RunGraphDocumentAsync(db, cancellationToken));
    }

    private static async Task RunChapterAsync(int chapterKey, Func<Task> action)
    {
        if (Chapters.TryGetValue(chapterKey, out var chapterTitle))
        {
            Console.WriteLine();
            Console.WriteLine($"=== {chapterKey}. {chapterTitle} ===");
        }

        await action().ConfigureAwait(false);

        Console.WriteLine();
    }

    private static async Task RunSchemaValidationAsync(SqlSpecificationGraphContext db, CancellationToken cancellationToken)
    {
        var schemaReport = await db.ValidateSchemaAsync(cancellationToken);

        Console.WriteLine(schemaReport.IsValid ? "Schema matches the Gorm model." : schemaReport.ToString());
    }

    private static async Task RunSeedAsync(SqlSpecificationGraphContext db, CancellationToken cancellationToken)
    {
        await SeedDemoData.RunAsync(db, cancellationToken);
        Console.WriteLine("Seed completed.");
    }

    private static async Task RunListAllNodesAsync(SqlSpecificationGraphContext db, CancellationToken cancellationToken)
    {
        var allNodes = await db.CharacteristicSpecifications.OrderBy(x => x.Name).ToListAsync(cancellationToken);

        foreach (var node in allNodes)
        {
            Console.WriteLine($"- {node.Name} ({node.Id})");
        }
    }

    private static async Task RunOutgoingMappingsAsync(SqlSpecificationGraphContext db, CancellationToken cancellationToken)
    {
        var mappedTargets = await db.CharacteristicSpecifications
            .Where(x => x.Id == SeedDemoData.InternetSpeedId)
            .Outgoing<CharacteristicSpecificationMapEdge, CharacteristicSpecificationNode>()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        foreach (var target in mappedTargets)
        {
            Console.WriteLine($"- Internet speed maps into {target.Name}");
        }
    }

    private static async Task RunIncomingMappingsAsync(SqlSpecificationGraphContext db, CancellationToken cancellationToken)
    {
        var incomingSources = await db.CharacteristicSpecifications
            .Where(x => x.Id == SeedDemoData.SpeedTierId)
            .Incoming<CharacteristicSpecificationMapEdge, CharacteristicSpecificationNode>()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        foreach (var source in incomingSources)
        {
            Console.WriteLine($"- {source.Name} maps into Speed tier");
        }
    }

    private static async Task RunIncludeAsync(SqlSpecificationGraphContext db, CancellationToken cancellationToken)
    {
        var includeRoots = await db.CharacteristicSpecifications.Where(x => x.Id == SeedDemoData.InternetSpeedId).Include(x => x.MapsInto).ToListAsync(cancellationToken);

        foreach (var root in includeRoots)
        {
            Console.WriteLine($"- {root.Name} has {root.MapsInto.Count} direct children");

            foreach (var child in root.MapsInto.OrderBy(x => x.Name))
            {
                Console.WriteLine($"  -> {child.Name}");
            }
        }
    }

    private static async Task RunNestedIncludeAsync(SqlSpecificationGraphContext db, CancellationToken cancellationToken)
    {
        var nestedRoots = await db.CharacteristicSpecifications
            .Where(x => x.Id == SeedDemoData.InternetSpeedId)
            .Include(x => x.MapsInto)
            .ThenInclude(x => x.MapsInto)
            .ToListAsync(cancellationToken);

        foreach (var root in nestedRoots)
        {
            Console.WriteLine($"- {root.Name}");

            foreach (var child in root.MapsInto.OrderBy(x => x.Name))
            {
                Console.WriteLine($"  -> {child.Name} ({child.MapsInto.Count} nested)");

                foreach (var grandChild in child.MapsInto.OrderBy(x => x.Name))
                {
                    Console.WriteLine($"     -> {grandChild.Name}");
                }
            }
        }
    }

    private static async Task RunAsNoTrackingAsync(SqlSpecificationGraphContext db, CancellationToken cancellationToken)
    {
        var readOnlyNodes = await db.CharacteristicSpecifications.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);

        Console.WriteLine($"- Read {readOnlyNodes.Count} nodes with AsNoTracking()");
    }

    private static Task RunExplainAsync(SqlSpecificationGraphContext db)
    {
        var explain = db.CharacteristicSpecifications
            .Where(x => x.Id == SeedDemoData.InternetSpeedId)
            .Outgoing<CharacteristicSpecificationMapEdge, CharacteristicSpecificationNode>()
            .Explain();

        Console.WriteLine("SQL:");
        Console.WriteLine(explain.Sql);
        Console.WriteLine();
        Console.WriteLine("Debug view:");
        Console.WriteLine(explain.DebugView);

        return Task.CompletedTask;
    }

    private static async Task RunAsyncOperatorsAsync(SqlSpecificationGraphContext db, CancellationToken cancellationToken)
    {
        var allHaveNames = await db.CharacteristicSpecifications.AllAsync(x => !string.IsNullOrWhiteSpace(x.Name), cancellationToken);

        Console.WriteLine($"- All nodes have a name: {allHaveNames}");

        var secondNode = await db.CharacteristicSpecifications.OrderBy(x => x.Name).ElementAtAsync(1, cancellationToken);

        Console.WriteLine($"- Second node by name: {secondNode.Name}");

        var byId = await db.CharacteristicSpecifications.OrderBy(x => x.Name).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        Console.WriteLine($"- Dictionary entries: {byId.Count}");
    }

    private static async Task RunGraphDocumentAsync(SqlSpecificationGraphContext db, CancellationToken cancellationToken)
    {
        var nodes = await db.CharacteristicSpecifications.OrderBy(x => x.Name).ToListAsync(cancellationToken);

        var edges = await db.CharacteristicMaps.OrderBy(x => x.Id).ToListAsync(cancellationToken);

        var envelope = new GraphEntityCollectionEnvelope<CharacteristicSpecificationNode, CharacteristicSpecificationMapEdge>
        {
            RootNodeId = SeedDemoData.InternetSpeedId.ToString(),
            Nodes = nodes,
            Edges = edges
        };

        var document = GraphDocumentBuilder.FromEnvelope(
            envelope,
            new GraphDocumentMappingOptions<CharacteristicSpecificationNode, CharacteristicSpecificationMapEdge>
            {
                NodeId = x => x.Name,
                NodeLabel = _ => "CharacteristicSpecification",
                NodeType = _ => "MapsInto",
                EdgeId = _ => "mapsInto",
            });

        Console.WriteLine($"- GraphDocument nodes: {document.Nodes.Count}");
        Console.WriteLine($"- GraphDocument edges: {document.Edges.Count}");
        Console.WriteLine($"- Root node: {document.RootNodeId}");
    }
}