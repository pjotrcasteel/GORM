using Gorm.Application.Intelligence.Algorithms.Flow;
using Gorm.Application.Intelligence.Algorithms.Pathfinding;
using Gorm.Application.Intelligence.Algorithms.Planning;

namespace Gorm.Application.Intelligence.Registry;

internal static class GraphRoutingRegistrations
{
    public static void AddTo(GraphAlgorithmRegistry registry)
    {
        RegisterAStar(registry);
        RegisterKShortestPaths(registry);
        RegisterMaximumFlow(registry);
        RegisterParetoRoutes(registry);
        RegisterImpactRerouting(registry);
        RegisterShortestPath(registry);
    }

    private static void RegisterAStar(GraphAlgorithmRegistry registry) =>
        registry.Register(
            GraphBuiltInDescriptors.Route(
                new GraphBuiltInRouteDescriptorOptions
                {
                    Id = "routing.astar",
                    Name = "A* optimal route",
                    Description = "Finds an optimal route using an optional admissible heuristic.",
                    ResultType = typeof(GraphAStarResult),
                    OptionsType = typeof(GraphAStarOptions),
                    Complexity = "O((V + E) log V) with a binary-heap frontier"
                }),
            (projection, invocation, cancellationToken) => projection.Run(
                new GraphAStarAlgorithm(
                    invocation.GetRequiredParameter<Guid>(GraphBuiltInDescriptors.StartNodeId),
                    invocation.GetRequiredParameter<Guid>(GraphBuiltInDescriptors.DestinationNodeId),
                    invocation.AsOptions<GraphAStarOptions>()),
                cancellationToken));

    private static void RegisterKShortestPaths(GraphAlgorithmRegistry registry) =>
        registry.Register(
            GraphBuiltInDescriptors.Route(
                new GraphBuiltInRouteDescriptorOptions
                {
                    Id = "routing.k-shortest",
                    Name = "K shortest paths",
                    Description = "Enumerates bounded unique loopless route alternatives.",
                    ResultType = typeof(GraphKShortestPathsResult),
                    OptionsType = typeof(GraphKShortestPathsOptions),
                    Complexity = "Bounded Yen search; proportional to returned paths and spur searches"
                }),
            (projection, invocation, cancellationToken) => projection.Run(
                new GraphYenKShortestPathsAlgorithm(
                    invocation.GetRequiredParameter<Guid>(GraphBuiltInDescriptors.StartNodeId),
                    invocation.GetRequiredParameter<Guid>(GraphBuiltInDescriptors.DestinationNodeId),
                    invocation.AsOptions<GraphKShortestPathsOptions>()),
                cancellationToken));

    private static void RegisterMaximumFlow(GraphAlgorithmRegistry registry) =>
        registry.Register(
            GraphBuiltInDescriptors.Route(
                new GraphBuiltInRouteDescriptorOptions
                {
                    Id = "flow.maximum",
                    Name = "Maximum flow / minimum cut",
                    Description = "Calculates directed capacity flow, cut sets and bottlenecks.",
                    ResultType = typeof(GraphMaximumFlowResult),
                    OptionsType = typeof(GraphMaximumFlowOptions),
                    Complexity = "O(VE²), bounded by augmentations",
                    Category = GraphAlgorithmCategory.Flow
                }),
            (projection, invocation, cancellationToken) => projection.Run(
                new GraphMaximumFlowAlgorithm(
                    invocation.GetRequiredParameter<Guid>(GraphBuiltInDescriptors.StartNodeId),
                    invocation.GetRequiredParameter<Guid>(GraphBuiltInDescriptors.DestinationNodeId),
                    invocation.AsOptions<GraphMaximumFlowOptions>()),
                cancellationToken));

    private static void RegisterParetoRoutes(GraphAlgorithmRegistry registry) =>
        registry.Register(
            GraphBuiltInDescriptors.Route(
                new GraphBuiltInRouteDescriptorOptions
                {
                    Id = "routing.pareto",
                    Name = "Pareto routes",
                    Description = "Preserves and ranks non-dominated multi-criteria route choices.",
                    ResultType = typeof(GraphParetoRouteResult),
                    OptionsType = typeof(GraphParetoRouteOptions),
                    Complexity = "Potentially exponential; bounded by label and route limits",
                    OptionsRequired = true
                }),
            (projection, invocation, cancellationToken) => projection.Run(
                new GraphParetoRouteAlgorithm(
                    invocation.GetRequiredParameter<Guid>(GraphBuiltInDescriptors.StartNodeId),
                    invocation.GetRequiredParameter<Guid>(GraphBuiltInDescriptors.DestinationNodeId),
                    invocation.AsOptions<GraphParetoRouteOptions>()!),
                cancellationToken));

    private static void RegisterImpactRerouting(GraphAlgorithmRegistry registry) =>
        registry.Register(
            GraphBuiltInDescriptors.Route(
                new GraphBuiltInRouteDescriptorOptions
                {
                    Id = "routing.impact",
                    Name = "Impact-aware rerouting",
                    Description = "Compares baseline and surviving routes after topology failures.",
                    ResultType = typeof(GraphImpactReroutingResult),
                    OptionsType = typeof(GraphImpactReroutingOptions),
                    Complexity = "Bounded K-shortest rerouting"
                }),
            (projection, invocation, cancellationToken) => projection.Run(
                new GraphImpactReroutingAlgorithm(
                    invocation.GetRequiredParameter<Guid>(GraphBuiltInDescriptors.StartNodeId),
                    invocation.GetRequiredParameter<Guid>(GraphBuiltInDescriptors.DestinationNodeId),
                    invocation.AsOptions<GraphImpactReroutingOptions>()),
                cancellationToken));

    private static void RegisterShortestPath(GraphAlgorithmRegistry registry) =>
        registry.Register(
            GraphBuiltInDescriptors.Route(
                new GraphBuiltInRouteDescriptorOptions
                {
                    Id = "routing.shortest-path",
                    Name = "Shortest path",
                    Description = "Finds a deterministic weighted shortest path.",
                    ResultType = typeof(GraphShortestPathResult),
                    OptionsType = typeof(GraphShortestPathOptions),
                    Complexity = "O((V + E) log V)"
                }),
            (projection, invocation, cancellationToken) => projection.Run(
                new GraphShortestPathAlgorithm(
                    invocation.GetRequiredParameter<Guid>(GraphBuiltInDescriptors.StartNodeId),
                    invocation.GetRequiredParameter<Guid>(GraphBuiltInDescriptors.DestinationNodeId),
                    invocation.AsOptions<GraphShortestPathOptions>()),
                cancellationToken));
}