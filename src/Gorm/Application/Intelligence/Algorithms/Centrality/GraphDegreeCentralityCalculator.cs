using System.Buffers;
using Gorm.Application.Intelligence.Diagnostics;
using Gorm.Application.Intelligence.Execution;
using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Algorithms.Centrality;

internal static class GraphDegreeCentralityCalculator
{
    private static readonly IComparer<GraphDegreeCentralityScore> ScoreComparer =
        Comparer<GraphDegreeCentralityScore>.Create((left, right) =>
        {
            var centralityComparison = right.TotalCentrality.CompareTo(left.TotalCentrality);
            return centralityComparison != 0 ? centralityComparison : left.Node.Id.CompareTo(right.Node.Id);
        });

    public static GraphDegreeCentralityCalculation Calculate(
        GraphProjection projection,
        GraphDegreeCentralityOptions options,
        IReadOnlyDictionary<Guid, GraphRawDegree>? previousDegrees,
        IReadOnlySet<Guid>? affectedNodeIds,
        CancellationToken cancellationToken)
    {
        ValidateOptions(options);
        var nodeCount = projection.Statistics.NodeCount;
        var rawDegreePool = ArrayPool<GraphRawDegree>.Shared;
        var scorePool = ArrayPool<GraphDegreeCentralityScore>.Shared;
        var rawDegrees = rawDegreePool.Rent(nodeCount);
        var scores = scorePool.Rent(nodeCount);
        var partitions = GraphDeterministicPartitioner.Create(nodeCount, options.DegreeOfParallelism, options.MinimumNodesPerPartition);
        var reusedByPartition = new int[partitions.Count];
        var recomputedByPartition = new int[partitions.Count];
        var singleDirectionDivisor = Math.Max(nodeCount - 1, 1);
        var totalDivisor = singleDirectionDivisor * 2d;

        try
        {
            GraphDeterministicPartitioner.Execute(
                partitions,
                options.DegreeOfParallelism,
                partition =>
                {
                    var reused = 0;
                    var recomputed = 0;
                    for (var nodeIndex = partition.StartIndex; nodeIndex < partition.EndIndex; nodeIndex++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var node = projection.GetNode(nodeIndex);
                        GraphRawDegree degree;
                        if (previousDegrees is not null &&
                            affectedNodeIds is not null &&
                            !affectedNodeIds.Contains(node.Id) &&
                            previousDegrees.TryGetValue(node.Id, out var previous))
                        {
                            degree = previous;
                            reused++;
                        }
                        else
                        {
                            degree = new GraphRawDegree(projection.GetIncomingDegree(nodeIndex), projection.GetOutgoingDegree(nodeIndex));
                            recomputed++;
                        }

                        rawDegrees[nodeIndex] = degree;
                        scores[nodeIndex] = new GraphDegreeCentralityScore
                        {
                            Node = node,
                            IncomingDegree = degree.Incoming,
                            OutgoingDegree = degree.Outgoing,
                            IncomingCentrality = degree.Incoming / (double)singleDirectionDivisor,
                            OutgoingCentrality = degree.Outgoing / (double)singleDirectionDivisor,
                            TotalCentrality = (degree.Incoming + degree.Outgoing) / totalDivisor
                        };
                    }

                    reusedByPartition[partition.Index] = reused;
                    recomputedByPartition[partition.Index] = recomputed;
                },
                cancellationToken);

            Array.Sort(scores, 0, nodeCount, ScoreComparer);
            var ordered = new GraphDegreeCentralityScore[nodeCount];

            Array.Copy(scores, ordered, nodeCount);
            var degreesByNodeId = new Dictionary<Guid, GraphRawDegree>(nodeCount);

            for (var nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                degreesByNodeId.Add(projection.GetNode(nodeIndex).Id, rawDegrees[nodeIndex]);
            }

            options.Progress?.Report(new GraphAlgorithmProgress
            {
                Operation = "degree-centrality",
                Stage = "complete",
                Completed = nodeCount,
                Total = nodeCount,
                Message = $"Calculated degree centrality for {nodeCount} node(s)."
            });

            return new GraphDegreeCentralityCalculation(new GraphDegreeCentralityResult(ordered), degreesByNodeId, reusedByPartition.Sum(), recomputedByPartition.Sum());
        }
        finally
        {
            rawDegreePool.Return(rawDegrees);
            scorePool.Return(scores, clearArray: true);
        }
    }

    private static void ValidateOptions(GraphDegreeCentralityOptions options)
    {
        if (options.DegreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Degree of parallelism must be greater than zero.");
        }

        if (options.MinimumNodesPerPartition <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Minimum nodes per partition must be greater than zero.");
        }
    }
}