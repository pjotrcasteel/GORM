using Gorm.Application.Intelligence.Algorithms.Planning;

namespace Gorm.Application.Intelligence.Registry;

internal static class GraphPlanningRegistrations
{
    public static void AddTo(GraphAlgorithmRegistry registry) =>
        registry.Register(
            GraphBuiltInDescriptors.Create(
                new GraphBuiltInDescriptorOptions
                {
                    Id = "planning.critical-path",
                    Name = "Critical path",
                    Description = "Calculates deterministic project timing, slack and critical dependencies.",
                    Category = GraphAlgorithmCategory.Planning,
                    ResultType = typeof(GraphCriticalPathResult),
                    OptionsType = typeof(GraphCriticalPathOptions),
                    Complexity = "O(V + E) after DAG validation"
                }),
            (projection, invocation, cancellationToken) => projection.Run(new GraphCriticalPathAlgorithm(invocation.AsOptions<GraphCriticalPathOptions>()), cancellationToken));
}