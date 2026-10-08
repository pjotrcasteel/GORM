using System.Buffers;
using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Application.Intelligence.Diagnostics;
using Gorm.Application.Intelligence.Execution;
using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Algorithms.Centrality;

/// <summary>
/// Calculates node importance using the PageRank algorithm.
/// </summary>
public sealed class GraphPageRankAlgorithm : IGraphAlgorithm<GraphPageRankResult>
{
    private readonly GraphPageRankOptions _options;

    /// <summary>
    /// Initializes a PageRank algorithm.
    /// </summary>
    public GraphPageRankAlgorithm(GraphPageRankOptions? options = null)
    {
        _options = options ?? new GraphPageRankOptions();
        ValidateOptions(_options);
    }

    /// <inheritdoc />
    public GraphPageRankResult Execute(GraphProjection projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var nodeCount = projection.Statistics.NodeCount;
        if (nodeCount == 0)
        {
            return new GraphPageRankResult([], iterations: 0, converged: true, totalDelta: 0);
        }

        var pool = ArrayPool<double>.Shared;
        var firstScores = pool.Rent(nodeCount);
        var secondScores = pool.Rent(nodeCount);
        var danglingMassByNode = pool.Rent(nodeCount);
        var deltaByNode = pool.Rent(nodeCount);
        try
        {
            var iteration = RunIterations(projection, firstScores, secondScores, danglingMassByNode, deltaByNode, cancellationToken);
            return new GraphPageRankResult(CreateScores(projection, iteration.Scores), iteration.Iterations, iteration.Converged, iteration.TotalDelta);
        }
        finally
        {
            pool.Return(firstScores);
            pool.Return(secondScores);
            pool.Return(danglingMassByNode);
            pool.Return(deltaByNode);
        }
    }

    private IterationResult RunIterations(
        GraphProjection projection,
        double[] firstScores,
        double[] secondScores,
        double[] danglingMassByNode,
        double[] deltaByNode,
        CancellationToken cancellationToken)
    {
        var nodeCount = projection.Statistics.NodeCount;
        var current = firstScores;
        var next = secondScores;
        Array.Fill(current, 1d / nodeCount, 0, nodeCount);
        var partitions = GraphDeterministicPartitioner.Create(nodeCount, _options.DegreeOfParallelism, _options.MinimumNodesPerPartition);
        var totalDelta = double.PositiveInfinity;
        var iterations = 0;

        while (iterations < _options.MaximumIterations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            iterations++;
            CalculateDanglingMass(projection, current, danglingMassByNode, partitions, cancellationToken);
            var danglingMass = Sum(danglingMassByNode, nodeCount);
            var baseScore =
                ((1d - _options.DampingFactor) / nodeCount) +
                (_options.DampingFactor * danglingMass / nodeCount);
            CalculateNextScores(projection, current, next, baseScore, partitions, cancellationToken);
            CalculateDelta(current, next, deltaByNode, partitions, cancellationToken);
            totalDelta = Sum(deltaByNode, nodeCount);
            (current, next) = (next, current);
            ReportProgress(iterations, totalDelta);

            if (totalDelta <= _options.Tolerance)
            {
                return new IterationResult(current, iterations, Converged: true, totalDelta);
            }
        }

        return new IterationResult(current, iterations, Converged: false, totalDelta);
    }

    private void CalculateDanglingMass(
        GraphProjection projection,
        double[] current,
        double[] danglingMassByNode,
        IReadOnlyList<GraphWorkPartition> partitions,
        CancellationToken cancellationToken) =>
        GraphDeterministicPartitioner.Execute(
            partitions,
            _options.DegreeOfParallelism,
            partition =>
            {
                for (var nodeIndex = partition.StartIndex; nodeIndex < partition.EndIndex; nodeIndex++)
                {
                    danglingMassByNode[nodeIndex] = projection.GetOutgoingDegree(nodeIndex) == 0
                        ? current[nodeIndex]
                        : 0;
                }
            },
            cancellationToken);

    private void CalculateNextScores(
        GraphProjection projection,
        double[] current,
        double[] next,
        double baseScore,
        IReadOnlyList<GraphWorkPartition> partitions,
        CancellationToken cancellationToken) =>
        GraphDeterministicPartitioner.Execute(
            partitions,
            _options.DegreeOfParallelism,
            partition =>
            {
                for (var nodeIndex = partition.StartIndex; nodeIndex < partition.EndIndex; nodeIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    next[nodeIndex] = CalculateNodeScore(projection, current, nodeIndex, baseScore);
                }
            },
            cancellationToken);

    private double CalculateNodeScore(GraphProjection projection, double[] current, int nodeIndex, double baseScore)
    {
        var score = baseScore;
        foreach (var arc in projection.GetIncomingArcs(nodeIndex))
        {
            var outgoingDegree = projection.GetOutgoingDegree(arc.NodeIndex);
            if (outgoingDegree > 0)
            {
                score += _options.DampingFactor * current[arc.NodeIndex] / outgoingDegree;
            }
        }

        return score;
    }

    private void CalculateDelta(double[] current, double[] next, double[] deltaByNode, IReadOnlyList<GraphWorkPartition> partitions, CancellationToken cancellationToken) =>
        GraphDeterministicPartitioner.Execute(
            partitions,
            _options.DegreeOfParallelism,
            partition =>
            {
                for (var nodeIndex = partition.StartIndex; nodeIndex < partition.EndIndex; nodeIndex++)
                {
                    deltaByNode[nodeIndex] = Math.Abs(next[nodeIndex] - current[nodeIndex]);
                }
            },
            cancellationToken);

    private void ReportProgress(int iterations, double totalDelta)
    {
        if (iterations % _options.ProgressInterval != 0 &&
            totalDelta > _options.Tolerance &&
            iterations != _options.MaximumIterations)
        {
            return;
        }

        _options.Progress?.Report(new GraphAlgorithmProgress
        {
            Operation = "pagerank",
            Stage = "iteration",
            Completed = iterations,
            Total = _options.MaximumIterations,
            Message = $"Completed PageRank iteration {iterations} of {_options.MaximumIterations}."
        });
    }

    private static GraphPageRankScore[] CreateScores(GraphProjection projection, double[] scores)
    {
        var ordered = Enumerable.Range(0, projection.Statistics.NodeCount)
            .OrderByDescending(index => scores[index])
            .ThenBy(index => projection.GetNode(index).Id)
            .ToArray();
        var result = new GraphPageRankScore[ordered.Length];
        for (var rank = 0; rank < ordered.Length; rank++)
        {
            var nodeIndex = ordered[rank];
            result[rank] = new GraphPageRankScore
            {
                Node = projection.GetNode(nodeIndex),
                Rank = rank,
                Score = scores[nodeIndex]
            };
        }

        return result;
    }

    private static void ValidateOptions(GraphPageRankOptions options)
    {
        if (options.DampingFactor is <= 0 or >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The PageRank damping factor must be between 0 and 1.");
        }

        if (options.MaximumIterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The maximum number of iterations must be greater than zero.");
        }

        if (!double.IsFinite(options.Tolerance) || options.Tolerance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The convergence tolerance must be finite and greater than zero.");
        }

        if (options.DegreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Degree of parallelism must be greater than zero.");
        }

        if (options.MinimumNodesPerPartition <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Minimum nodes per partition must be greater than zero.");
        }

        if (options.ProgressInterval <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Progress interval must be greater than zero.");
        }
    }

    private static double Sum(double[] values, int count)
    {
        var result = 0d;
        for (var index = 0; index < count; index++)
        {
            result += values[index];
        }

        return result;
    }

    private readonly record struct IterationResult(double[] Scores, int Iterations, bool Converged, double TotalDelta);
}