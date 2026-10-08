using System.Globalization;
using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Application.Intelligence.Algorithms.Communities;
using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Algorithms.LinkPrediction;

/// <summary>
/// Predicts missing directed or undirected links using explainable topological metrics.
/// </summary>
public sealed class GraphLinkPredictionAlgorithm : IGraphAlgorithm<GraphLinkPredictionResult>
{
    private readonly GraphLinkPredictionOptions _options;
    private readonly GraphCommunityDetectionResult? _communities;

    /// <summary>
    /// Initializes a bounded link-prediction algorithm.
    /// </summary>
    public GraphLinkPredictionAlgorithm(GraphLinkPredictionOptions? options = null, GraphCommunityDetectionResult? communities = null)
    {
        _options = options ?? new GraphLinkPredictionOptions();
        _communities = communities;
        ValidateOptions(_options, communities);
    }

    /// <inheritdoc />
    public GraphLinkPredictionResult Execute(GraphProjection projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var topology = CreateTopology(projection, cancellationToken);
        var eligibleNodes = projection.Nodes.Select(node => _options.CandidateNodePredicate?.Invoke(node) ?? true).ToArray();
        var communityIds = CreateCommunityIds(projection);
        var search = FindCandidates(projection, topology, eligibleNodes, communityIds, cancellationToken);

        var predictions = CreatePredictions(projection, search.Candidates, communityIds is not null)
            .Where(prediction => prediction.Confidence >= _options.MinimumConfidence)
            .OrderByDescending(prediction => prediction.Confidence)
            .ThenBy(prediction => prediction.Source.Id)
            .ThenBy(prediction => prediction.Target.Id)
            .Take(_options.MaximumResults)
            .Select((prediction, rank) => new GraphLinkPrediction
            {
                Rank = rank,
                Source = prediction.Source,
                Target = prediction.Target,
                Confidence = prediction.Confidence,
                Metrics = prediction.Metrics,
                EvidenceNodes = prediction.EvidenceNodes,
                Explanation = prediction.Explanation
            })
            .ToArray();

        return new GraphLinkPredictionResult(predictions, search.EvaluatedCandidatePairs);
    }

    private CandidateSearchResult FindCandidates(GraphProjection projection, Topology topology, bool[] eligibleNodes, int[]? communityIds, CancellationToken cancellationToken)
    {
        var candidates = new List<RawCandidate>();
        var evaluatedCandidatePairs = 0;
        for (var sourceIndex = 0; sourceIndex < projection.Statistics.NodeCount; sourceIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!eligibleNodes[sourceIndex])
            {
                continue;
            }

            var firstTarget = _options.Directed ? 0 : sourceIndex + 1;
            for (var targetIndex = firstTarget; targetIndex < projection.Statistics.NodeCount; targetIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var evaluation = EvaluateCandidate(projection, topology, eligibleNodes, communityIds, sourceIndex, targetIndex);
                if (!evaluation.WasEvaluated)
                {
                    continue;
                }

                evaluatedCandidatePairs++;
                EnsureCandidateLimit(evaluatedCandidatePairs);
                if (evaluation.Candidate is not null)
                {
                    candidates.Add(evaluation.Candidate);
                }
            }
        }

        return new CandidateSearchResult([.. candidates], evaluatedCandidatePairs);
    }

    private CandidateEvaluation EvaluateCandidate(GraphProjection projection, Topology topology, bool[] eligibleNodes, int[]? communityIds, int sourceIndex, int targetIndex)
    {
        if (sourceIndex == targetIndex || !eligibleNodes[targetIndex])
        {
            return CandidateEvaluation.Skipped;
        }

        var source = projection.GetNode(sourceIndex);
        var target = projection.GetNode(targetIndex);
        if (_options.CandidatePairPredicate is not null &&
            !_options.CandidatePairPredicate(source, target))
        {
            return CandidateEvaluation.Skipped;
        }

        if (!MatchesCommunityScope(sourceIndex, targetIndex, communityIds) ||
            topology.ExistingLinks.Contains(LinkKey(sourceIndex, targetIndex, _options.Directed)))
        {
            return CandidateEvaluation.Skipped;
        }

        return new CandidateEvaluation(WasEvaluated: true, CreateCandidate(topology, communityIds, sourceIndex, targetIndex));
    }

    private RawCandidate? CreateCandidate(Topology topology, int[]? communityIds, int sourceIndex, int targetIndex)
    {
        var sourceEvidence = _options.Directed
            ? topology.OutgoingNeighbors[sourceIndex]
            : topology.UndirectedNeighbors[sourceIndex];
        var targetEvidence = _options.Directed
            ? topology.IncomingNeighbors[targetIndex]
            : topology.UndirectedNeighbors[targetIndex];
        var commonNeighbors = sourceEvidence.Where(targetEvidence.Contains).Order().ToArray();
        if (commonNeighbors.Length < _options.MinimumCommonNeighbors)
        {
            return null;
        }

        var unionCount = sourceEvidence.Union(targetEvidence).Count();
        return new RawCandidate
        {
            SourceIndex = sourceIndex,
            TargetIndex = targetIndex,
            CommonNeighbors = commonNeighbors,
            Jaccard = unionCount == 0 ? 0 : commonNeighbors.Length / (double)unionCount,
            AdamicAdar = commonNeighbors.Sum(commonIndex => CalculateAdamicAdar(topology, commonIndex)),
            PreferentialAttachment = (double)sourceEvidence.Count * targetEvidence.Count,
            CommunityAffinity = communityIds is not null &&
                                communityIds[sourceIndex] == communityIds[targetIndex]
                ? 1
                : 0
        };
    }

    private double CalculateAdamicAdar(Topology topology, int commonIndex)
    {
        var evidenceDegree = _options.Directed
            ? topology.OutgoingNeighbors[commonIndex].Count + topology.IncomingNeighbors[commonIndex].Count
            : topology.UndirectedNeighbors[commonIndex].Count;
        return evidenceDegree <= 1 ? 0 : 1 / Math.Log(evidenceDegree);
    }

    private void EnsureCandidateLimit(int evaluatedCandidatePairs)
    {
        if (evaluatedCandidatePairs > _options.MaximumCandidatePairs)
        {
            throw new GraphLinkPredictionCandidateLimitException(_options.MaximumCandidatePairs);
        }
    }

    private Topology CreateTopology(GraphProjection projection, CancellationToken cancellationToken)
    {
        var outgoing = Enumerable.Range(0, projection.Statistics.NodeCount).Select(_ => new HashSet<int>()).ToArray();
        var incoming = Enumerable.Range(0, projection.Statistics.NodeCount).Select(_ => new HashSet<int>()).ToArray();
        var undirected = Enumerable.Range(0, projection.Statistics.NodeCount).Select(_ => new HashSet<int>()).ToArray();
        var existingLinks = new HashSet<long>();

        foreach (var edge in projection.Edges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_options.EdgePredicate is not null && !_options.EdgePredicate(edge))
            {
                continue;
            }

            var fromIndex = projection.GetNodeIndex(edge.FromId);
            var toIndex = projection.GetNodeIndex(edge.ToId);

            outgoing[fromIndex].Add(toIndex);
            incoming[toIndex].Add(fromIndex);
            undirected[fromIndex].Add(toIndex);
            undirected[toIndex].Add(fromIndex);
            existingLinks.Add(LinkKey(fromIndex, toIndex, _options.Directed));
        }

        return new Topology(outgoing, incoming, undirected, existingLinks);
    }

    private int[]? CreateCommunityIds(GraphProjection projection)
    {
        if (_communities is null)
        {
            return null;
        }

        var result = new int[projection.Statistics.NodeCount];
        for (var nodeIndex = 0; nodeIndex < result.Length; nodeIndex++)
        {
            var node = projection.GetNode(nodeIndex);
            try
            {
                result[nodeIndex] = _communities.GetCommunityId(node.Id);
            }
            catch (KeyNotFoundException exception)
            {
                throw new ArgumentException($"The community result does not contain projected node '{node.Id}'.", exception);
            }
        }

        return result;
    }

    private bool MatchesCommunityScope(int sourceIndex, int targetIndex, int[]? communityIds) =>
        _options.CandidateScope switch
        {
            GraphLinkPredictionCandidateScope.All => true,
            GraphLinkPredictionCandidateScope.WithinCommunity =>
                communityIds![sourceIndex] == communityIds[targetIndex],
            GraphLinkPredictionCandidateScope.AcrossCommunities =>
                communityIds![sourceIndex] != communityIds[targetIndex],
            _ => throw new ArgumentOutOfRangeException($"Type not implemented: {_options.CandidateScope}")
        };

    private IEnumerable<GraphLinkPrediction> CreatePredictions(GraphProjection projection, RawCandidate[] candidates, bool hasCommunities)
    {
        var maximumCommonNeighbors = candidates.Length == 0 ? 0 : candidates.Max(item => item.CommonNeighbors.Length);
        var maximumAdamicAdar = candidates.Length == 0 ? 0 : candidates.Max(item => item.AdamicAdar);
        var maximumPreferentialAttachment = candidates.Length == 0 ? 0 : candidates.Max(item => item.PreferentialAttachment);
        var totalWeight =
            _options.CommonNeighborsWeight +
            _options.JaccardWeight +
            _options.AdamicAdarWeight +
            _options.PreferentialAttachmentWeight +
            (hasCommunities ? _options.CommunityAffinityWeight : 0);

        foreach (var candidate in candidates)
        {
            var normalizedCommonNeighbors = maximumCommonNeighbors == 0 ? 0 : candidate.CommonNeighbors.Length / (double)maximumCommonNeighbors;
            var normalizedAdamicAdar = GraphAlgorithmNumeric.IsZero(maximumAdamicAdar)
                ? 0
                : candidate.AdamicAdar / maximumAdamicAdar;
            var normalizedPreferentialAttachment = GraphAlgorithmNumeric.IsZero(maximumPreferentialAttachment)
                ? 0
                : candidate.PreferentialAttachment / maximumPreferentialAttachment;
            var confidence =
                ((normalizedCommonNeighbors * _options.CommonNeighborsWeight) +
                 (candidate.Jaccard * _options.JaccardWeight) +
                 (normalizedAdamicAdar * _options.AdamicAdarWeight) +
                 (normalizedPreferentialAttachment * _options.PreferentialAttachmentWeight) +
                 (candidate.CommunityAffinity * (hasCommunities ? _options.CommunityAffinityWeight : 0))) /
                totalWeight;

            var metrics = new GraphLinkPredictionMetrics
            {
                CommonNeighbors = candidate.CommonNeighbors.Length,
                Jaccard = candidate.Jaccard,
                AdamicAdar = candidate.AdamicAdar,
                PreferentialAttachment = candidate.PreferentialAttachment,
                CommunityAffinity = candidate.CommunityAffinity
            };

            yield return new GraphLinkPrediction
            {
                Rank = 0,
                Source = projection.GetNode(candidate.SourceIndex),
                Target = projection.GetNode(candidate.TargetIndex),
                Confidence = confidence,
                Metrics = metrics,
                EvidenceNodes = [.. candidate.CommonNeighbors.Select(projection.GetNode)],
                Explanation = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} shared evidence node(s); Jaccard {1:F3}; Adamic-Adar {2:F3}; preferential attachment {3:F3}; community affinity {4:F0}; confidence {5:F3}.",
                    candidate.CommonNeighbors.Length,
                    candidate.Jaccard,
                    candidate.AdamicAdar,
                    candidate.PreferentialAttachment,
                    candidate.CommunityAffinity,
                    confidence)
            };
        }
    }

    private static long LinkKey(int sourceIndex, int targetIndex, bool directed)
    {
        if (!directed && sourceIndex > targetIndex)
        {
            (sourceIndex, targetIndex) = (targetIndex, sourceIndex);
        }

        return ((long)sourceIndex << 32) | (uint)targetIndex;
    }

    private static void ValidateOptions(GraphLinkPredictionOptions options, GraphCommunityDetectionResult? communities)
    {
        if (!Enum.IsDefined(options.CandidateScope))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Unknown link-prediction candidate scope.");
        }

        if (options.CandidateScope != GraphLinkPredictionCandidateScope.All && communities is null)
        {
            throw new ArgumentException("WithinCommunity and AcrossCommunities scopes require a community result.", nameof(communities));
        }

        if (options.MaximumResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum results must be greater than zero.");
        }

        if (options.MaximumCandidatePairs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum candidate pairs must be greater than zero.");
        }

        if (options.MinimumCommonNeighbors < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Minimum common neighbours must be non-negative.");
        }

        if (!double.IsFinite(options.MinimumConfidence) ||
            options.MinimumConfidence < 0 ||
            options.MinimumConfidence > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Minimum confidence must be between zero and one.");
        }

        var metricWeights = new[]
        {
            options.CommonNeighborsWeight,
            options.JaccardWeight,
            options.AdamicAdarWeight,
            options.PreferentialAttachmentWeight,
            options.CommunityAffinityWeight
        };
        if (metricWeights.Any(weight => !double.IsFinite(weight) || weight < 0) ||
            metricWeights.Take(4).Sum() <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Metric weights must be finite and non-negative, with at least one topology metric greater than zero.");
        }
    }

    private sealed record Topology(HashSet<int>[] OutgoingNeighbors, HashSet<int>[] IncomingNeighbors, HashSet<int>[] UndirectedNeighbors, HashSet<long> ExistingLinks);

    private sealed record CandidateSearchResult(RawCandidate[] Candidates, int EvaluatedCandidatePairs);

    private readonly record struct CandidateEvaluation(bool WasEvaluated, RawCandidate? Candidate)
    {
        public static CandidateEvaluation Skipped => new(WasEvaluated: false, Candidate: null);
    }

    private sealed record RawCandidate
    {
        public required int SourceIndex { get; init; }

        public required int TargetIndex { get; init; }

        public required int[] CommonNeighbors { get; init; }

        public required double Jaccard { get; init; }

        public required double AdamicAdar { get; init; }

        public required double PreferentialAttachment { get; init; }

        public required double CommunityAffinity { get; init; }
    }
}