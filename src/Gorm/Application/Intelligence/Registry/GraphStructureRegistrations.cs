using Gorm.Application.Intelligence.Algorithms.Connectivity;

namespace Gorm.Application.Intelligence.Registry;

internal static class GraphStructureRegistrations
{
    public static void AddTo(GraphAlgorithmRegistry registry)
    {
        RegisterConnectedComponents(registry);
        RegisterCycleDetection(registry);
    }

    private static void RegisterConnectedComponents(GraphAlgorithmRegistry registry) =>
        registry.Register(
            GraphBuiltInDescriptors.Create(
                new GraphBuiltInDescriptorOptions
                {
                    Id = "structure.connected-components",
                    Name = "Connected components",
                    Description = "Finds weakly or strongly connected graph regions.",
                    Category = GraphAlgorithmCategory.Structure,
                    ResultType = typeof(GraphConnectedComponentsResult),
                    Parameters =
                    [
                        GraphBuiltInDescriptors.Parameter("kind", typeof(GraphComponentKind), false, "Weak or strong component mode.")
                    ],
                    Complexity = "O(V + E)"
                }),
            (projection, invocation, cancellationToken) => projection.Run(
                new GraphConnectedComponentsAlgorithm(invocation.Parameters.TryGetValue("kind", out var kind) ? (GraphComponentKind)kind! : GraphComponentKind.Weak),
                cancellationToken));

    private static void RegisterCycleDetection(GraphAlgorithmRegistry registry) =>
        registry.Register(
            GraphBuiltInDescriptors.Create(
                new GraphBuiltInDescriptorOptions
                {
                    Id = "structure.cycles",
                    Name = "Cycle detection",
                    Description = "Enumerates simple directed cycles under explicit limits.",
                    Category = GraphAlgorithmCategory.Structure,
                    ResultType = typeof(GraphCycleDetectionResult),
                    OptionsType = typeof(GraphCycleDetectionOptions),
                    Complexity = "Potentially exponential; bounded by options"
                }),
            (projection, invocation, cancellationToken) => projection.Run(new GraphCycleDetectionAlgorithm(invocation.AsOptions<GraphCycleDetectionOptions>()), cancellationToken));
}