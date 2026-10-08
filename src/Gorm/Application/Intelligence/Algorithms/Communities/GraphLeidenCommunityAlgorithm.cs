using System.Globalization;
using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Algorithms.Communities;

/// <summary>
/// Detects hierarchical weighted communities using deterministic Leiden local moving,
/// connectivity refinement and graph aggregation.
/// </summary>
public sealed class GraphLeidenCommunityAlgorithm : IGraphAlgorithm<GraphCommunityDetectionResult>
{
    private readonly GraphCommunityDetectionOptions _options;

    /// <summary>
    /// Initializes a deterministic weighted Leiden community-detection algorithm.
    /// </summary>
    public GraphLeidenCommunityAlgorithm(GraphCommunityDetectionOptions? options = null)
    {
        _options = options ?? new GraphCommunityDetectionOptions();
        GraphCommunityDetectionOptionsValidator.Validate(_options);
    }

    /// <inheritdoc />
    public GraphCommunityDetectionResult Execute(GraphProjection projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var nodeCount = projection.Statistics.NodeCount;
        if (nodeCount == 0)
        {
            return new GraphCommunityDetectionResult([], [], []);
        }

        var edgeConfiguration = SelectEdges(projection, cancellationToken);
        var originalNetwork = CreateOriginalNetwork(projection, edgeConfiguration);
        var currentNetwork = originalNetwork;
        var snapshots = new List<PartitionSnapshot>();

        for (var level = 0; level < _options.MaximumLevels; level++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var partition = OptimizePartition(currentNetwork, unchecked(_options.RandomSeed + (level * 1_000_003)), cancellationToken);
            partition = RefineDisconnectedCommunities(currentNetwork, partition, cancellationToken);
            partition = RenumberPartition(currentNetwork, partition);

            var originalPartition = MapToOriginalNodes(currentNetwork, partition, nodeCount);
            var modularity = CalculateModularity(originalNetwork, originalPartition);
            if (snapshots.Count == 0 || !snapshots[^1].Partition.SequenceEqual(originalPartition))
            {
                snapshots.Add(new PartitionSnapshot(originalPartition, modularity));
            }

            var communityCount = partition.Max() + 1;
            if (communityCount == currentNetwork.VertexCount)
            {
                break;
            }

            currentNetwork = AggregateNetwork(currentNetwork, partition, communityCount);
        }

        var levels = snapshots
            .Select((snapshot, level) => new GraphCommunityLevel
            {
                Level = level,
                Modularity = snapshot.Modularity,
                Communities = CreateCommunities(projection, snapshot.Partition, edgeConfiguration, cancellationToken)
            })
            .ToArray();

        var finalPartition = snapshots[^1].Partition;
        var (boundaryEdges, bridgeNodes) = CreateBoundaryAnalysis(projection, finalPartition, edgeConfiguration, cancellationToken);

        return new GraphCommunityDetectionResult(levels, boundaryEdges, bridgeNodes);
    }

    private EdgeConfiguration SelectEdges(GraphProjection projection, CancellationToken cancellationToken)
    {
        var included = new bool[projection.Statistics.EdgeCount];
        var weights = new double[projection.Statistics.EdgeCount];

        for (var edgeIndex = 0; edgeIndex < included.Length; edgeIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var edge = projection.GetEdge(edgeIndex);
            if (_options.EdgePredicate is not null && !_options.EdgePredicate(edge))
            {
                continue;
            }

            var weight = _options.WeightSelector?.Invoke(edge) ?? 1;
            if (!double.IsFinite(weight) || weight < 0)
            {
                throw new InvalidOperationException($"The community weight for edge '{edge.Id}' must be finite and non-negative but was '{weight}'.");
            }

            if (GraphAlgorithmNumeric.IsZero(weight))
            {
                continue;
            }

            included[edgeIndex] = true;
            weights[edgeIndex] = weight;
        }

        return new EdgeConfiguration(included, weights);
    }

    private WeightedNetwork CreateOriginalNetwork(GraphProjection projection, EdgeConfiguration edgeConfiguration)
    {
        var arcs = new List<WeightedArc>();
        for (var edgeIndex = 0; edgeIndex < edgeConfiguration.Included.Length; edgeIndex++)
        {
            if (!edgeConfiguration.Included[edgeIndex])
            {
                continue;
            }

            var edge = projection.GetEdge(edgeIndex);
            var fromIndex = projection.GetNodeIndex(edge.FromId);
            var toIndex = projection.GetNodeIndex(edge.ToId);
            var weight = edgeConfiguration.Weights[edgeIndex];
            arcs.Add(new WeightedArc(fromIndex, toIndex, weight));

            if (_options.EdgeMode == GraphCommunityEdgeMode.Undirected)
            {
                arcs.Add(new WeightedArc(toIndex, fromIndex, weight));
            }
        }

        var members = Enumerable.Range(0, projection.Statistics.NodeCount)
            .Select(nodeIndex => new[] { nodeIndex })
            .ToArray();

        return new WeightedNetwork(projection.Statistics.NodeCount, arcs, members);
    }

    private int[] OptimizePartition(WeightedNetwork network, int seed, CancellationToken cancellationToken)
    {
        var partition = Enumerable.Range(0, network.VertexCount).ToArray();
        if (GraphAlgorithmNumeric.IsZero(network.TotalWeight))
        {
            return partition;
        }

        var state = new PartitionState(
            partition,
            CalculateInternalWeights(network),
            (double[])network.OutWeights.Clone(),
            (double[])network.InWeights.Clone(),
            [.. Enumerable.Range(0, network.VertexCount)],
            new DeterministicRandom(seed));
        for (var pass = 0; pass < _options.MaximumLocalMovingPasses; pass++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!RunMovingPass(network, state, cancellationToken))
            {
                break;
            }
        }

        return partition;
    }

    private static double[] CalculateInternalWeights(WeightedNetwork network)
    {
        var internalWeights = new double[network.VertexCount];
        foreach (var arc in network.Arcs)
        {
            if (arc.Source == arc.Target)
            {
                internalWeights[arc.Source] += arc.Weight;
            }
        }

        return internalWeights;
    }

    private bool RunMovingPass(WeightedNetwork network, PartitionState state, CancellationToken cancellationToken)
    {
        state.Random.Shuffle(state.Order);
        var moved = false;
        foreach (var vertex in state.Order)
        {
            cancellationToken.ThrowIfCancellationRequested();
            moved |= TryMoveVertex(network, state, vertex);
        }

        return moved;
    }

    private bool TryMoveVertex(WeightedNetwork network, PartitionState state, int vertex)
    {
        var currentCommunity = state.Partition[vertex];
        var outgoingByCommunity = SumWeightsByCommunity(network.Outgoing[vertex], state.Partition, useTarget: true);
        var incomingByCommunity = SumWeightsByCommunity(network.Incoming[vertex], state.Partition, useTarget: false);
        var selfLoopWeight = network.Outgoing[vertex]
            .Where(arc => arc.Target == vertex)
            .Sum(arc => arc.Weight);
        var moveWeights = new CommunityMoveWeights(selfLoopWeight, outgoingByCommunity, incomingByCommunity);
        var bestCommunity = FindBestCommunity(network, state, vertex, currentCommunity, moveWeights);
        if (bestCommunity == currentCommunity)
        {
            return false;
        }

        MoveVertex(
            new MoveVertexParameters
            {
                Network = network,
                Vertex = vertex,
                CurrentCommunity = currentCommunity,
                CandidateCommunity = bestCommunity,
                SelfLoopWeight = selfLoopWeight,
                OutgoingByCommunity = outgoingByCommunity,
                IncomingByCommunity = incomingByCommunity,
                Partition = state.Partition,
                InternalWeights = state.InternalWeights,
                CommunityOutWeights = state.CommunityOutWeights,
                CommunityInWeights = state.CommunityInWeights
            });
        return true;
    }

    private int FindBestCommunity(WeightedNetwork network, PartitionState state, int vertex, int currentCommunity, CommunityMoveWeights moveWeights)
    {
        var candidateCommunities = new SortedSet<int>(moveWeights.OutgoingByCommunity.Keys);
        candidateCommunities.UnionWith(moveWeights.IncomingByCommunity.Keys);
        candidateCommunities.Add(currentCommunity);
        var bestCommunity = currentCommunity;
        var bestDelta = _options.MinimumModularityGain;
        foreach (var candidateCommunity in candidateCommunities)
        {
            if (candidateCommunity == currentCommunity)
            {
                continue;
            }

            var delta = CalculateMoveDelta(
                new CalculateMoveDeltaParameters
                {
                    Network = network,
                    Vertex = vertex,
                    CurrentCommunity = currentCommunity,
                    CandidateCommunity = candidateCommunity,
                    SelfLoopWeight = moveWeights.SelfLoopWeight,
                    OutgoingByCommunity = moveWeights.OutgoingByCommunity,
                    IncomingByCommunity = moveWeights.IncomingByCommunity,
                    InternalWeights = state.InternalWeights,
                    CommunityOutWeights = state.CommunityOutWeights,
                    CommunityInWeights = state.CommunityInWeights
                });
            if (delta > bestDelta ||
                (GraphAlgorithmNumeric.AreEqual(delta, bestDelta) &&
                 candidateCommunity < bestCommunity))
            {
                bestDelta = delta;
                bestCommunity = candidateCommunity;
            }
        }

        return bestCommunity;
    }

    private static Dictionary<int, double> SumWeightsByCommunity(IEnumerable<WeightedArc> arcs, int[] partition, bool useTarget)
    {
        var result = new Dictionary<int, double>();
        foreach (var arc in arcs)
        {
            var vertex = useTarget ? arc.Target : arc.Source;
            var community = partition[vertex];
            result[community] = result.GetValueOrDefault(community) + arc.Weight;
        }

        return result;
    }

    private double CalculateMoveDelta(CalculateMoveDeltaParameters inputs)
    {
        var network = inputs.Network;
        var vertex = inputs.Vertex;
        var currentCommunity = inputs.CurrentCommunity;
        var candidateCommunity = inputs.CandidateCommunity;
        var selfLoopWeight = inputs.SelfLoopWeight;
        var outgoingByCommunity = inputs.OutgoingByCommunity;
        var incomingByCommunity = inputs.IncomingByCommunity;
        var internalWeights = inputs.InternalWeights;
        var communityOutWeights = inputs.CommunityOutWeights;
        var communityInWeights = inputs.CommunityInWeights;

        var oldContribution =
            CommunityContribution(internalWeights[currentCommunity], communityOutWeights[currentCommunity], communityInWeights[currentCommunity], network.TotalWeight) +
            CommunityContribution(internalWeights[candidateCommunity], communityOutWeights[candidateCommunity], communityInWeights[candidateCommunity], network.TotalWeight);

        var outgoingToCurrent = outgoingByCommunity.GetValueOrDefault(currentCommunity);
        var incomingFromCurrent = incomingByCommunity.GetValueOrDefault(currentCommunity);
        var outgoingToCandidate = outgoingByCommunity.GetValueOrDefault(candidateCommunity);
        var incomingFromCandidate = incomingByCommunity.GetValueOrDefault(candidateCommunity);
        var newCurrentInternal = internalWeights[currentCommunity] - outgoingToCurrent - incomingFromCurrent + selfLoopWeight;
        var newCandidateInternal = internalWeights[candidateCommunity] + outgoingToCandidate + incomingFromCandidate + selfLoopWeight;

        var newContribution =
            CommunityContribution(
                newCurrentInternal,
                communityOutWeights[currentCommunity] - network.OutWeights[vertex],
                communityInWeights[currentCommunity] - network.InWeights[vertex],
                network.TotalWeight) +
            CommunityContribution(
                newCandidateInternal,
                communityOutWeights[candidateCommunity] + network.OutWeights[vertex],
                communityInWeights[candidateCommunity] + network.InWeights[vertex],
                network.TotalWeight);

        return newContribution - oldContribution;
    }

    private double CommunityContribution(double internalWeight, double outgoingWeight, double incomingWeight, double totalWeight) =>
        (internalWeight / totalWeight) - (_options.Resolution * outgoingWeight * incomingWeight / (totalWeight * totalWeight));

    private static void MoveVertex(MoveVertexParameters inputs)
    {
        var network = inputs.Network;
        var vertex = inputs.Vertex;
        var currentCommunity = inputs.CurrentCommunity;
        var candidateCommunity = inputs.CandidateCommunity;
        var selfLoopWeight = inputs.SelfLoopWeight;
        var outgoingByCommunity = inputs.OutgoingByCommunity;
        var incomingByCommunity = inputs.IncomingByCommunity;
        var partition = inputs.Partition;
        var internalWeights = inputs.InternalWeights;
        var communityOutWeights = inputs.CommunityOutWeights;
        var communityInWeights = inputs.CommunityInWeights;

        internalWeights[currentCommunity] -= outgoingByCommunity.GetValueOrDefault(currentCommunity) + incomingByCommunity.GetValueOrDefault(currentCommunity) - selfLoopWeight;
        internalWeights[candidateCommunity] +=
            outgoingByCommunity.GetValueOrDefault(candidateCommunity) +
            incomingByCommunity.GetValueOrDefault(candidateCommunity) +
            selfLoopWeight;
        communityOutWeights[currentCommunity] -= network.OutWeights[vertex];
        communityInWeights[currentCommunity] -= network.InWeights[vertex];
        communityOutWeights[candidateCommunity] += network.OutWeights[vertex];
        communityInWeights[candidateCommunity] += network.InWeights[vertex];
        partition[vertex] = candidateCommunity;
    }

    private static int[] RefineDisconnectedCommunities(WeightedNetwork network, int[] partition, CancellationToken cancellationToken)
    {
        var refined = new int[network.VertexCount];
        Array.Fill(refined, -1);
        var nextCommunity = 0;

        for (var start = 0; start < network.VertexCount; start++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (refined[start] >= 0)
            {
                continue;
            }

            var originalCommunity = partition[start];
            var pending = new Queue<int>();
            refined[start] = nextCommunity;
            pending.Enqueue(start);

            while (pending.TryDequeue(out var vertex))
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddConnectedVertices(
                    new AddConnectedVerticesParameters
                    {
                        Arcs = network.Outgoing[vertex],
                        Partition = partition,
                        OriginalCommunity = originalCommunity,
                        Refined = refined,
                        RefinedCommunity = nextCommunity,
                        Pending = pending,
                        UseTarget = true
                    });
                AddConnectedVertices(
                    new AddConnectedVerticesParameters
                    {
                        Arcs = network.Incoming[vertex],
                        Partition = partition,
                        OriginalCommunity = originalCommunity,
                        Refined = refined,
                        RefinedCommunity = nextCommunity,
                        Pending = pending,
                        UseTarget = false
                    });
            }

            nextCommunity++;
        }

        return refined;
    }

    private static void AddConnectedVertices(AddConnectedVerticesParameters inputs)
    {
        var arcs = inputs.Arcs;
        var partition = inputs.Partition;
        var originalCommunity = inputs.OriginalCommunity;
        var refined = inputs.Refined;
        var refinedCommunity = inputs.RefinedCommunity;
        var pending = inputs.Pending;
        var useTarget = inputs.UseTarget;

        foreach (var arc in arcs)
        {
            var adjacent = useTarget ? arc.Target : arc.Source;
            if (partition[adjacent] != originalCommunity || refined[adjacent] >= 0)
            {
                continue;
            }

            refined[adjacent] = refinedCommunity;
            pending.Enqueue(adjacent);
        }
    }

    private static int[] RenumberPartition(WeightedNetwork network, int[] partition)
    {
        var orderedCommunities = Enumerable.Range(0, network.VertexCount)
            .GroupBy(vertex => partition[vertex])
            .Select(group => new { OldCommunity = group.Key, FirstOriginalNode = group.SelectMany(vertex => network.Members[vertex]).Min() })
            .OrderBy(group => group.FirstOriginalNode)
            .ToArray();

        var mapping = orderedCommunities.Select((community, newId) => (community.OldCommunity, NewId: newId)).ToDictionary(item => item.OldCommunity, item => item.NewId);

        return [.. partition.Select(community => mapping[community])];
    }

    private static int[] MapToOriginalNodes(WeightedNetwork network, int[] partition, int originalNodeCount)
    {
        var result = new int[originalNodeCount];
        for (var vertex = 0; vertex < network.VertexCount; vertex++)
        {
            foreach (var originalNode in network.Members[vertex])
            {
                result[originalNode] = partition[vertex];
            }
        }

        return result;
    }

    private double CalculateModularity(WeightedNetwork network, int[] partition)
    {
        if (GraphAlgorithmNumeric.IsZero(network.TotalWeight))
        {
            return 0;
        }

        var communityCount = partition.Max() + 1;
        var internalWeights = new double[communityCount];
        var outgoingWeights = new double[communityCount];
        var incomingWeights = new double[communityCount];

        for (var vertex = 0; vertex < network.VertexCount; vertex++)
        {
            outgoingWeights[partition[vertex]] += network.OutWeights[vertex];
            incomingWeights[partition[vertex]] += network.InWeights[vertex];
        }

        foreach (var arc in network.Arcs)
        {
            if (partition[arc.Source] == partition[arc.Target])
            {
                internalWeights[partition[arc.Source]] += arc.Weight;
            }
        }

        var modularity = 0d;
        for (var community = 0; community < communityCount; community++)
        {
            modularity += CommunityContribution(internalWeights[community], outgoingWeights[community], incomingWeights[community], network.TotalWeight);
        }

        return modularity;
    }

    private static WeightedNetwork AggregateNetwork(WeightedNetwork network, int[] partition, int communityCount)
    {
        var aggregatedWeights = new Dictionary<(int Source, int Target), double>();
        foreach (var arc in network.Arcs)
        {
            var key = (partition[arc.Source], partition[arc.Target]);
            aggregatedWeights[key] = aggregatedWeights.GetValueOrDefault(key) + arc.Weight;
        }

        var arcs = aggregatedWeights
            .OrderBy(item => item.Key.Source)
            .ThenBy(item => item.Key.Target)
            .Select(item => new WeightedArc(item.Key.Source, item.Key.Target, item.Value))
            .ToArray();

        var members = Enumerable.Range(0, communityCount)
            .Select(community => Enumerable.Range(0, network.VertexCount)
                .Where(vertex => partition[vertex] == community)
                .SelectMany(vertex => network.Members[vertex])
                .Order()
                .ToArray())
            .ToArray();

        return new WeightedNetwork(communityCount, arcs, members);
    }

    private GraphCommunity[] CreateCommunities(GraphProjection projection, int[] partition, EdgeConfiguration edgeConfiguration, CancellationToken cancellationToken)
    {
        var communityCount = partition.Max() + 1;
        var statistics = CalculateCommunityStatistics(projection, partition, edgeConfiguration, communityCount, cancellationToken);
        var result = new GraphCommunity[communityCount];
        for (var community = 0; community < communityCount; community++)
        {
            result[community] = CreateCommunity(projection, partition, statistics, community);
        }

        return result;
    }

    private static CommunityStatistics CalculateCommunityStatistics(
        GraphProjection projection,
        int[] partition,
        EdgeConfiguration edgeConfiguration,
        int communityCount,
        CancellationToken cancellationToken)
    {
        var statistics = new CommunityStatistics(new double[communityCount], new double[communityCount], new int[communityCount]);
        for (var edgeIndex = 0; edgeIndex < edgeConfiguration.Included.Length; edgeIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!edgeConfiguration.Included[edgeIndex])
            {
                continue;
            }

            AccumulateCommunityEdge(projection, partition, edgeConfiguration, edgeIndex, statistics);
        }

        return statistics;
    }

    private static void AccumulateCommunityEdge(GraphProjection projection, int[] partition, EdgeConfiguration edgeConfiguration, int edgeIndex, CommunityStatistics statistics)
    {
        var edge = projection.GetEdge(edgeIndex);
        var fromCommunity = partition[projection.GetNodeIndex(edge.FromId)];
        var toCommunity = partition[projection.GetNodeIndex(edge.ToId)];
        var weight = edgeConfiguration.Weights[edgeIndex];
        statistics.TotalGraphVolume += 2 * weight;
        if (fromCommunity != toCommunity)
        {
            statistics.BoundaryWeights[fromCommunity] += weight;
            statistics.BoundaryWeights[toCommunity] += weight;
            return;
        }

        statistics.InternalWeights[fromCommunity] += weight;
        if (edge.FromId != edge.ToId)
        {
            statistics.InternalEdgeCounts[fromCommunity]++;
        }
    }

    private GraphCommunity CreateCommunity(GraphProjection projection, int[] partition, CommunityStatistics statistics, int community)
    {
        var nodes = Enumerable.Range(0, projection.Statistics.NodeCount)
            .Where(nodeIndex => partition[nodeIndex] == community)
            .Select(projection.GetNode)
            .OrderBy(node => node.Id)
            .ToArray();
        var volume =
            (2 * statistics.InternalWeights[community]) +
            statistics.BoundaryWeights[community];
        var conductanceDenominator = Math.Min(volume, statistics.TotalGraphVolume - volume);
        var possiblePairs = (double)nodes.Length * (nodes.Length - 1);
        if (_options.EdgeMode != GraphCommunityEdgeMode.Directed)
        {
            possiblePairs /= 2;
        }

        return new GraphCommunity
        {
            Id = community,
            Nodes = nodes,
            InternalWeight = statistics.InternalWeights[community],
            BoundaryWeight = statistics.BoundaryWeights[community],
            Conductance = conductanceDenominator <= 0
                ? 0
                : statistics.BoundaryWeights[community] / conductanceDenominator,
            Density = possiblePairs <= 0
                ? 0
                : statistics.InternalEdgeCounts[community] / possiblePairs
        };
    }

    private (GraphCommunityBoundaryEdge[] BoundaryEdges, GraphCommunityBridgeNode[] BridgeNodes) CreateBoundaryAnalysis(
        GraphProjection projection,
        int[] partition,
        EdgeConfiguration edgeConfiguration,
        CancellationToken cancellationToken)
    {
        var statistics = CalculateBoundaryStatistics(projection, partition, edgeConfiguration, cancellationToken);
        var boundaryEdges = CreateBoundaryEdges(projection, partition, edgeConfiguration, statistics, cancellationToken);
        var bridgeNodes = CreateBridgeNodes(projection, partition, statistics);
        return (boundaryEdges, bridgeNodes);
    }

    private static BoundaryStatistics CalculateBoundaryStatistics(
        GraphProjection projection,
        int[] partition,
        EdgeConfiguration edgeConfiguration,
        CancellationToken cancellationToken)
    {
        var nodeCount = projection.Statistics.NodeCount;
        var statistics = new BoundaryStatistics(
            new double[nodeCount],
            new double[nodeCount],
            new int[nodeCount],
            new int[nodeCount],
            [.. Enumerable.Range(0, nodeCount).Select(_ => new HashSet<int>())]);
        for (var edgeIndex = 0; edgeIndex < edgeConfiguration.Included.Length; edgeIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (edgeConfiguration.Included[edgeIndex])
            {
                AccumulateBoundaryEdge(projection, partition, edgeConfiguration, edgeIndex, statistics);
            }
        }

        return statistics;
    }

    private static void AccumulateBoundaryEdge(GraphProjection projection, int[] partition, EdgeConfiguration edgeConfiguration, int edgeIndex, BoundaryStatistics statistics)
    {
        var edge = projection.GetEdge(edgeIndex);
        var fromIndex = projection.GetNodeIndex(edge.FromId);
        var toIndex = projection.GetNodeIndex(edge.ToId);
        var weight = edgeConfiguration.Weights[edgeIndex];
        statistics.IncidentWeights[fromIndex] += weight;
        statistics.IncidentWeights[toIndex] += weight;
        statistics.IncidentEdgeCounts[fromIndex]++;
        statistics.IncidentEdgeCounts[toIndex]++;
        if (partition[fromIndex] == partition[toIndex])
        {
            return;
        }

        statistics.ExternalWeights[fromIndex] += weight;
        statistics.ExternalWeights[toIndex] += weight;
        statistics.ExternalEdgeCounts[fromIndex]++;
        statistics.ExternalEdgeCounts[toIndex]++;
        statistics.NeighboringCommunities[fromIndex].Add(partition[toIndex]);
        statistics.NeighboringCommunities[toIndex].Add(partition[fromIndex]);
    }

    private GraphCommunityBoundaryEdge[] CreateBoundaryEdges(
        GraphProjection projection,
        int[] partition,
        EdgeConfiguration edgeConfiguration,
        BoundaryStatistics statistics,
        CancellationToken cancellationToken)
    {
        var boundaryEdges = new List<GraphCommunityBoundaryEdge>();
        for (var edgeIndex = 0; edgeIndex < edgeConfiguration.Included.Length; edgeIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!edgeConfiguration.Included[edgeIndex] ||
                !IsBoundaryEdge(projection, partition, edgeIndex))
            {
                continue;
            }

            boundaryEdges.Add(CreateBoundaryEdge(projection, partition, edgeConfiguration, statistics, edgeIndex));
        }

        return [.. boundaryEdges
            .OrderByDescending(edge => edge.AnomalyScore)
            .ThenBy(edge => edge.Edge.Id)];
    }

    private static bool IsBoundaryEdge(GraphProjection projection, int[] partition, int edgeIndex)
    {
        var edge = projection.GetEdge(edgeIndex);
        return partition[projection.GetNodeIndex(edge.FromId)] !=
               partition[projection.GetNodeIndex(edge.ToId)];
    }

    private GraphCommunityBoundaryEdge CreateBoundaryEdge(
        GraphProjection projection,
        int[] partition,
        EdgeConfiguration edgeConfiguration,
        BoundaryStatistics statistics,
        int edgeIndex)
    {
        var edge = projection.GetEdge(edgeIndex);
        var fromIndex = projection.GetNodeIndex(edge.FromId);
        var toIndex = projection.GetNodeIndex(edge.ToId);
        var fromEmbeddedness = CalculateEmbeddedness(statistics, fromIndex);
        var toEmbeddedness = CalculateEmbeddedness(statistics, toIndex);
        var expectedWeight =
            (CalculateAverageWeight(statistics, fromIndex) +
             CalculateAverageWeight(statistics, toIndex)) / 2;
        var weight = edgeConfiguration.Weights[edgeIndex];
        var relativeStrength = GraphAlgorithmNumeric.IsZero(expectedWeight)
            ? 0
            : Math.Min(1, weight / expectedWeight);
        var anomalyScore = Math.Clamp((fromEmbeddedness + toEmbeddedness) / 2 * relativeStrength, 0, 1);
        return new GraphCommunityBoundaryEdge
        {
            Edge = edge,
            FromCommunityId = partition[fromIndex],
            ToCommunityId = partition[toIndex],
            Weight = weight,
            AnomalyScore = anomalyScore,
            IsAnomalous = anomalyScore >= _options.AnomalyThreshold,
            Explanation = string.Create(
                CultureInfo.InvariantCulture,
                $"Cross-community edge strength {relativeStrength:F3}; endpoint embeddedness {fromEmbeddedness:F3}/{toEmbeddedness:F3}; anomaly score {anomalyScore:F3}.")
        };
    }

    private static double CalculateEmbeddedness(BoundaryStatistics statistics, int nodeIndex) =>
        GraphAlgorithmNumeric.IsZero(statistics.IncidentWeights[nodeIndex])
            ? 0
            : 1 -
              (statistics.ExternalWeights[nodeIndex] /
               statistics.IncidentWeights[nodeIndex]);

    private static double CalculateAverageWeight(BoundaryStatistics statistics, int nodeIndex) =>
        statistics.IncidentEdgeCounts[nodeIndex] == 0
            ? 0
            : statistics.IncidentWeights[nodeIndex] /
              statistics.IncidentEdgeCounts[nodeIndex];

    private static GraphCommunityBridgeNode[] CreateBridgeNodes(GraphProjection projection, int[] partition, BoundaryStatistics statistics) =>
        [.. Enumerable.Range(0, projection.Statistics.NodeCount)
            .Where(nodeIndex => statistics.ExternalEdgeCounts[nodeIndex] > 0)
            .Select(nodeIndex => new GraphCommunityBridgeNode
            {
                Node = projection.GetNode(nodeIndex),
                CommunityId = partition[nodeIndex],
                ExternalEdgeCount = statistics.ExternalEdgeCounts[nodeIndex],
                NeighboringCommunityCount = statistics.NeighboringCommunities[nodeIndex].Count,
                ExternalWeight = statistics.ExternalWeights[nodeIndex],
                ExternalWeightRatio = GraphAlgorithmNumeric.IsZero(statistics.IncidentWeights[nodeIndex])
                    ? 0
                    : statistics.ExternalWeights[nodeIndex] /
                      statistics.IncidentWeights[nodeIndex]
            })
            .OrderByDescending(node => node.ExternalWeightRatio)
            .ThenByDescending(node => node.NeighboringCommunityCount)
            .ThenBy(node => node.Node.Id)];

    private sealed class WeightedNetwork
    {
        public WeightedNetwork(int vertexCount, IEnumerable<WeightedArc> arcs, int[][] members)
        {
            VertexCount = vertexCount;
            Arcs = [.. arcs.OrderBy(arc => arc.Source).ThenBy(arc => arc.Target)];
            Members = members;
            Outgoing = [.. Enumerable.Range(0, vertexCount).Select(_ => new List<WeightedArc>())];
            Incoming = [.. Enumerable.Range(0, vertexCount).Select(_ => new List<WeightedArc>())];
            OutWeights = new double[vertexCount];
            InWeights = new double[vertexCount];

            foreach (var arc in Arcs)
            {
                Outgoing[arc.Source].Add(arc);
                Incoming[arc.Target].Add(arc);
                OutWeights[arc.Source] += arc.Weight;
                InWeights[arc.Target] += arc.Weight;
                TotalWeight += arc.Weight;
            }
        }

        public int VertexCount { get; }

        public WeightedArc[] Arcs { get; }

        public List<WeightedArc>[] Outgoing { get; }

        public List<WeightedArc>[] Incoming { get; }

        public double[] OutWeights { get; }

        public double[] InWeights { get; }

        public double TotalWeight { get; }

        public int[][] Members { get; }
    }

    private sealed class DeterministicRandom(int seed)
    {
        private uint _state = unchecked((uint)seed) + 0x9E3779B9u;

        public void Shuffle(int[] values)
        {
            for (var index = values.Length - 1; index > 0; index--)
            {
                var replacement = (int)(NextUInt32() % (uint)(index + 1));
                (values[index], values[replacement]) = (values[replacement], values[index]);
            }
        }

        private uint NextUInt32()
        {
            var value = _state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _state = value == 0 ? 0xA341316Cu : value;
            return _state;
        }
    }

    private sealed record EdgeConfiguration(bool[] Included, double[] Weights);

    private sealed record PartitionSnapshot(int[] Partition, double Modularity);

    private sealed record CommunityMoveWeights(double SelfLoopWeight, Dictionary<int, double> OutgoingByCommunity, Dictionary<int, double> IncomingByCommunity);

    private sealed record PartitionState(
        int[] Partition,
        double[] InternalWeights,
        double[] CommunityOutWeights,
        double[] CommunityInWeights,
        int[] Order,
        DeterministicRandom Random);

    private sealed record CommunityStatistics(double[] InternalWeights, double[] BoundaryWeights, int[] InternalEdgeCounts)
    {
        public double TotalGraphVolume { get; set; }
    }

    private sealed record BoundaryStatistics(
        double[] IncidentWeights,
        double[] ExternalWeights,
        int[] IncidentEdgeCounts,
        int[] ExternalEdgeCounts,
        HashSet<int>[] NeighboringCommunities);

    private readonly record struct WeightedArc(int Source, int Target, double Weight);

    private sealed class CalculateMoveDeltaParameters
    {
        public required WeightedNetwork Network { get; init; }

        public required int Vertex { get; init; }

        public required int CurrentCommunity { get; init; }

        public required int CandidateCommunity { get; init; }

        public required double SelfLoopWeight { get; init; }

        public required IReadOnlyDictionary<int, double> OutgoingByCommunity { get; init; }

        public required IReadOnlyDictionary<int, double> IncomingByCommunity { get; init; }

        public required double[] InternalWeights { get; init; }

        public required double[] CommunityOutWeights { get; init; }

        public required double[] CommunityInWeights { get; init; }
    }

    private sealed class MoveVertexParameters
    {
        public required WeightedNetwork Network { get; init; }

        public required int Vertex { get; init; }

        public required int CurrentCommunity { get; init; }

        public required int CandidateCommunity { get; init; }

        public required double SelfLoopWeight { get; init; }

        public required IReadOnlyDictionary<int, double> OutgoingByCommunity { get; init; }

        public required IReadOnlyDictionary<int, double> IncomingByCommunity { get; init; }

        public required int[] Partition { get; init; }

        public required double[] InternalWeights { get; init; }

        public required double[] CommunityOutWeights { get; init; }

        public required double[] CommunityInWeights { get; init; }
    }

    private sealed class AddConnectedVerticesParameters
    {
        public required IEnumerable<WeightedArc> Arcs { get; init; }

        public required int[] Partition { get; init; }

        public required int OriginalCommunity { get; init; }

        public required int[] Refined { get; init; }

        public required int RefinedCommunity { get; init; }

        public required Queue<int> Pending { get; init; }

        public required bool UseTarget { get; init; }
    }
}