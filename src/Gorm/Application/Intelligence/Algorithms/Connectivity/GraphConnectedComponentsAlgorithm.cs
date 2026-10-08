using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Algorithms.Connectivity;

/// <summary>
/// Finds weakly or strongly connected components.
/// </summary>
public sealed class GraphConnectedComponentsAlgorithm : IGraphAlgorithm<GraphConnectedComponentsResult>
{
    private readonly GraphComponentKind _kind;

    /// <summary>
    /// Initializes a connected-components algorithm.
    /// </summary>
    public GraphConnectedComponentsAlgorithm(GraphComponentKind kind = GraphComponentKind.Weak)
    {
        _kind = kind;
    }

    /// <inheritdoc />
    public GraphConnectedComponentsResult Execute(GraphProjection projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var components = _kind switch
        {
            GraphComponentKind.Weak => FindWeakComponents(projection, cancellationToken),
            GraphComponentKind.Strong => FindStrongComponents(projection, cancellationToken),
            _ => throw new NotSupportedException($"Unknown component kind: {_kind}")
        };

        var ordered = components
            .OrderByDescending(component => component.Count)
            .ThenBy(component => component.Min(index => projection.GetNode(index).Id))
            .Select((component, id) => new GraphConnectedComponent { Id = id, Nodes = [.. component.Select(projection.GetNode).OrderBy(node => node.Id)] })
            .ToArray();

        return new GraphConnectedComponentsResult(_kind, ordered);
    }

    private static IReadOnlyList<List<int>> FindWeakComponents(GraphProjection projection, CancellationToken cancellationToken)
    {
        var disjointSet = new DisjointSet(projection.Statistics.NodeCount);

        foreach (var edge in projection.Edges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            disjointSet.Union(projection.GetNodeIndex(edge.FromId), projection.GetNodeIndex(edge.ToId));
        }

        var components = new Dictionary<int, List<int>>();
        for (var nodeIndex = 0; nodeIndex < projection.Statistics.NodeCount; nodeIndex++)
        {
            var root = disjointSet.Find(nodeIndex);
            if (!components.TryGetValue(root, out var component))
            {
                component = [];
                components.Add(root, component);
            }

            component.Add(nodeIndex);
        }

        return [.. components.Values];
    }

    private static List<List<int>> FindStrongComponents(GraphProjection projection, CancellationToken cancellationToken)
    {
        var nodeCount = projection.Statistics.NodeCount;
        var visited = new bool[nodeCount];
        var finishOrder = new List<int>(nodeCount);

        for (var nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
        {
            if (!visited[nodeIndex])
            {
                AddDepthFirstFinishOrder(projection, nodeIndex, visited, finishOrder, cancellationToken);
            }
        }

        Array.Clear(visited);
        var components = new List<List<int>>();

        for (var orderIndex = finishOrder.Count - 1; orderIndex >= 0; orderIndex--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var nodeIndex = finishOrder[orderIndex];
            if (visited[nodeIndex])
            {
                continue;
            }

            components.Add(CollectReverseComponent(projection, nodeIndex, visited));
        }

        return components;
    }

    private static List<int> CollectReverseComponent(GraphProjection projection, int nodeIndex, bool[] visited)
    {
        var component = new List<int>();
        var pending = new Stack<int>();
        visited[nodeIndex] = true;
        pending.Push(nodeIndex);

        while (pending.TryPop(out var current))
        {
            component.Add(current);
            AddUnvisitedIncomingNodes(projection, current, visited, pending);
        }

        return component;
    }

    private static void AddUnvisitedIncomingNodes(GraphProjection projection, int nodeIndex, bool[] visited, Stack<int> pending)
    {
        foreach (var arc in projection.GetIncomingArcs(nodeIndex))
        {
            if (visited[arc.NodeIndex])
            {
                continue;
            }

            visited[arc.NodeIndex] = true;
            pending.Push(arc.NodeIndex);
        }
    }

    private static void AddDepthFirstFinishOrder(GraphProjection projection, int startNodeIndex, bool[] visited, List<int> finishOrder, CancellationToken cancellationToken)
    {
        var pending = new Stack<DepthFirstFrame>();
        visited[startNodeIndex] = true;
        pending.Push(new DepthFirstFrame(startNodeIndex, 0));

        while (pending.TryPop(out var frame))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outgoing = projection.GetOutgoingArcs(frame.NodeIndex);

            if (frame.NextArcIndex >= outgoing.Length)
            {
                finishOrder.Add(frame.NodeIndex);
                continue;
            }

            pending.Push(frame with { NextArcIndex = frame.NextArcIndex + 1 });
            var targetIndex = outgoing[frame.NextArcIndex].NodeIndex;

            if (!visited[targetIndex])
            {
                visited[targetIndex] = true;
                pending.Push(new DepthFirstFrame(targetIndex, 0));
            }
        }
    }

    private readonly record struct DepthFirstFrame(int NodeIndex, int NextArcIndex);

    private sealed class DisjointSet
    {
        private readonly int[] _parents;
        private readonly byte[] _ranks;

        public DisjointSet(int count)
        {
            _parents = [.. Enumerable.Range(0, count)];
            _ranks = new byte[count];
        }

        public int Find(int item)
        {
            while (_parents[item] != item)
            {
                _parents[item] = _parents[_parents[item]];
                item = _parents[item];
            }

            return item;
        }

        public void Union(int left, int right)
        {
            var leftRoot = Find(left);
            var rightRoot = Find(right);

            if (leftRoot == rightRoot)
            {
                return;
            }

            switch (_ranks[leftRoot].CompareTo(_ranks[rightRoot]))
            {
                case < 0:
                    _parents[leftRoot] = rightRoot;
                    break;

                case > 0:
                    _parents[rightRoot] = leftRoot;
                    break;

                default:
                    _parents[rightRoot] = leftRoot;
                    _ranks[leftRoot]++;
                    break;
            }
        }
    }
}