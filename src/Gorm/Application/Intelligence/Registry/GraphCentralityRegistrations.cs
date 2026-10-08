using Gorm.Application.Intelligence.Algorithms.Centrality;

namespace Gorm.Application.Intelligence.Registry;

internal static class GraphCentralityRegistrations
{
    public static void AddTo(GraphAlgorithmRegistry registry)
    {
        RegisterPageRank(registry);
        RegisterDegreeCentrality(registry);
        RegisterBetweennessCentrality(registry);
    }

    private static void RegisterPageRank(GraphAlgorithmRegistry registry) =>
        registry.Register(
            GraphBuiltInDescriptors.Create(
                new GraphBuiltInDescriptorOptions
                {
                    Id = "centrality.pagerank",
                    Name = "PageRank",
                    Description = "Ranks nodes by recursively propagated incoming importance.",
                    Category = GraphAlgorithmCategory.Centrality,
                    ResultType = typeof(GraphPageRankResult),
                    OptionsType = typeof(GraphPageRankOptions),
                    Complexity = "O(iterations × (V + E))"
                }),
            (projection, invocation, cancellationToken) => projection.Run(new GraphPageRankAlgorithm(invocation.AsOptions<GraphPageRankOptions>()), cancellationToken));

    private static void RegisterDegreeCentrality(GraphAlgorithmRegistry registry) =>
        registry.Register(
            GraphBuiltInDescriptors.Create(
                new GraphBuiltInDescriptorOptions
                {
                    Id = "centrality.degree",
                    Name = "Degree centrality",
                    Description = "Calculates incoming, outgoing and total degree scores.",
                    Category = GraphAlgorithmCategory.Centrality,
                    ResultType = typeof(GraphDegreeCentralityResult),
                    OptionsType = typeof(GraphDegreeCentralityOptions),
                    Complexity = "O(V + E)",
                    SupportsIncremental = true
                }),
            (projection, invocation, cancellationToken) => projection.Run(
                new GraphDegreeCentralityAlgorithm(invocation.AsOptions<GraphDegreeCentralityOptions>()),
                cancellationToken));

    private static void RegisterBetweennessCentrality(GraphAlgorithmRegistry registry) =>
        registry.Register(
            GraphBuiltInDescriptors.Create(
                new GraphBuiltInDescriptorOptions
                {
                    Id = "centrality.betweenness",
                    Name = "Betweenness centrality",
                    Description = "Finds structural bridges using exact weighted or unweighted shortest paths.",
                    Category = GraphAlgorithmCategory.Centrality,
                    ResultType = typeof(GraphBetweennessCentralityResult),
                    OptionsType = typeof(GraphBetweennessCentralityOptions),
                    Complexity = "O(VE) unweighted; higher with weighted paths"
                }),
            (projection, invocation, cancellationToken) => projection.Run(
                new GraphBetweennessCentralityAlgorithm(invocation.AsOptions<GraphBetweennessCentralityOptions>()),
                cancellationToken));
}