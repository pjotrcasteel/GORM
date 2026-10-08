namespace Gorm.Application.Intelligence.Algorithms.Centrality;

internal sealed record GraphDegreeCentralityCalculation(
    GraphDegreeCentralityResult Result,
    IReadOnlyDictionary<Guid, GraphRawDegree> DegreesByNodeId,
    int ReusedNodeCount,
    int RecomputedNodeCount);