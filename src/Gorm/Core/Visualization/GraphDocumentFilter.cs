namespace Gorm.Core.Visualization;

/// <summary>
/// Represents graph document filter.
/// </summary>
public static class GraphDocumentFilter
{
    /// <summary>
    /// Executes focus.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="rootNodeId">The root node id.</param>
    /// <param name="maxDepth">The max depth.</param>
    /// <param name="includeIncoming">The include incoming.</param>
    /// <returns>The value.</returns>
    public static GraphDocument Focus(GraphDocument document, string? rootNodeId = null, int? maxDepth = null, bool includeIncoming = false)
    {
        ArgumentNullException.ThrowIfNull(document);

        var effectiveRootId = ResolveEffectiveRootId(document, rootNodeId);

        if (string.IsNullOrWhiteSpace(effectiveRootId))
        {
            return document;
        }

        var nodesById = document.Nodes.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

        if (!nodesById.ContainsKey(effectiveRootId))
        {
            return document;
        }

        var outgoing = document.Edges.GroupBy(x => x.From, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.OrdinalIgnoreCase);

        var incoming = document.Edges.GroupBy(x => x.To, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.OrdinalIgnoreCase);

        var visited = TraverseVisitedNodes(effectiveRootId, maxDepth, includeIncoming, outgoing, incoming);
        var filteredNodes = CreateFilteredNodes(
            new CreateFilteredNodesParameters
            {
                Document = document,
                Visited = visited,
                EffectiveRootId = effectiveRootId,
                Outgoing = outgoing,
                Incoming = incoming,
                IncludeIncoming = includeIncoming,
                MaxDepth = maxDepth
            });
        var filteredEdges = document.Edges.Where(x => visited.Contains(x.From) && visited.Contains(x.To)).ToArray();

        return new GraphDocument
        {
            RootNodeId = effectiveRootId,
            Nodes = filteredNodes,
            Edges = filteredEdges,
            Metadata = document.Metadata
        };
    }

    private static string? ResolveEffectiveRootId(GraphDocument document, string? rootNodeId)
    {
        if (!string.IsNullOrWhiteSpace(rootNodeId))
        {
            return rootNodeId;
        }

        if (!string.IsNullOrWhiteSpace(document.RootNodeId))
        {
            return document.RootNodeId;
        }

        if (document.Nodes.Count == 0)
        {
            return null;
        }

        return document.Nodes[0].Id;
    }

    private static HashSet<string> TraverseVisitedNodes(
        string effectiveRootId,
        int? maxDepth,
        bool includeIncoming,
        IReadOnlyDictionary<string, GraphDocumentEdge[]> outgoing,
        IReadOnlyDictionary<string, GraphDocumentEdge[]> incoming)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(string NodeId, int Depth)>();
        queue.Enqueue((effectiveRootId, 0));

        while (queue.Count > 0)
        {
            var (nodeId, depth) = queue.Dequeue();

            if (!visited.Add(nodeId))
            {
                continue;
            }

            if (maxDepth is not null && depth >= maxDepth.Value)
            {
                continue;
            }

            EnqueueOutgoing(queue, outgoing, nodeId, depth);

            if (includeIncoming)
            {
                EnqueueIncoming(queue, incoming, nodeId, depth);
            }
        }

        return visited;
    }

    private static void EnqueueOutgoing(Queue<(string NodeId, int Depth)> queue, IReadOnlyDictionary<string, GraphDocumentEdge[]> outgoing, string nodeId, int depth)
    {
        foreach (var edge in outgoing.GetValueOrDefault(nodeId) ?? [])
        {
            queue.Enqueue((edge.To, depth + 1));
        }
    }

    private static void EnqueueIncoming(Queue<(string NodeId, int Depth)> queue, IReadOnlyDictionary<string, GraphDocumentEdge[]> incoming, string nodeId, int depth)
    {
        foreach (var edge in incoming.GetValueOrDefault(nodeId) ?? [])
        {
            queue.Enqueue((edge.From, depth + 1));
        }
    }

    private static GraphDocumentNode[] CreateFilteredNodes(CreateFilteredNodesParameters inputs)
    {
        var document = inputs.Document;
        var visited = inputs.Visited;
        var effectiveRootId = inputs.EffectiveRootId;
        var outgoing = inputs.Outgoing;
        var incoming = inputs.Incoming;
        var includeIncoming = inputs.IncludeIncoming;
        var maxDepth = inputs.MaxDepth;

        return [.. document.Nodes
            .Where(x => visited.Contains(x.Id))
            .Select(x => new GraphDocumentNode
            {
                Id = x.Id,
                Label = x.Label,
                Type = x.Type,
                Depth = ComputeDepth(x.Id, effectiveRootId, outgoing, includeIncoming ? incoming : null, maxDepth),
                IsRoot = string.Equals(x.Id, effectiveRootId, StringComparison.OrdinalIgnoreCase),
                Properties = x.Properties
            })];
    }

    private static int ComputeDepth(
        string nodeId,
        string rootNodeId,
        IReadOnlyDictionary<string, GraphDocumentEdge[]> outgoing,
        IReadOnlyDictionary<string, GraphDocumentEdge[]>? incoming,
        int? maxDepth)
    {
        if (string.Equals(nodeId, rootNodeId, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(string NodeId, int Depth)>();
        queue.Enqueue((rootNodeId, 0));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (!visited.Add(current.NodeId))
            {
                continue;
            }

            if (maxDepth is not null && current.Depth >= maxDepth.Value)
            {
                continue;
            }

            var outgoingDepth = TryGetNextDepth(queue, current, nodeId, outgoing.GetValueOrDefault(current.NodeId) ?? [], static edge => edge.To);

            if (outgoingDepth is not null)
            {
                return outgoingDepth.Value;
            }

            if (incoming is null)
            {
                continue;
            }

            var incomingDepth = TryGetNextDepth(queue, current, nodeId, incoming.GetValueOrDefault(current.NodeId) ?? [], static edge => edge.From);

            if (incomingDepth is not null)
            {
                return incomingDepth.Value;
            }
        }

        return 0;
    }

    private static int? TryGetNextDepth(
        Queue<(string NodeId, int Depth)> queue,
        (string NodeId, int Depth) current,
        string targetNodeId,
        IEnumerable<GraphDocumentEdge> edges,
        Func<GraphDocumentEdge, string> nextNodeSelector)
    {
        foreach (var nextNodeId in edges.Select(nextNodeSelector))
        {
            if (string.Equals(nextNodeId, targetNodeId, StringComparison.OrdinalIgnoreCase))
            {
                return current.Depth + 1;
            }

            queue.Enqueue((nextNodeId, current.Depth + 1));
        }

        return null;
    }

    private sealed class CreateFilteredNodesParameters
    {
        public required GraphDocument Document { get; init; }

        public required HashSet<string> Visited { get; init; }

        public required string EffectiveRootId { get; init; }

        public required IReadOnlyDictionary<string, GraphDocumentEdge[]> Outgoing { get; init; }

        public required IReadOnlyDictionary<string, GraphDocumentEdge[]> Incoming { get; init; }

        public required bool IncludeIncoming { get; init; }

        public required int? MaxDepth { get; init; }
    }
}