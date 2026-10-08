using Gorm.Application.Intelligence.Algorithms.Communities;
using Gorm.Application.Intelligence.Algorithms.LinkPrediction;

namespace Gorm.Application.Intelligence.Registry;

internal static class GraphCommunityRegistrations
{
    public static void AddTo(GraphAlgorithmRegistry registry)
    {
        RegisterLeiden(registry);
        RegisterLinkPrediction(registry);
    }

    private static void RegisterLeiden(GraphAlgorithmRegistry registry) =>
        registry.Register(
            GraphBuiltInDescriptors.Create(
                new GraphBuiltInDescriptorOptions
                {
                    Id = "community.leiden",
                    Name = "Leiden communities",
                    Description = "Detects hierarchical weighted communities, boundaries and anomalies.",
                    Category = GraphAlgorithmCategory.Community,
                    ResultType = typeof(GraphCommunityDetectionResult),
                    OptionsType = typeof(GraphCommunityDetectionOptions),
                    Complexity = "Iterative and bounded by hierarchy/pass options"
                }),
            (projection, invocation, cancellationToken) => projection.Run(
                new GraphLeidenCommunityAlgorithm(invocation.AsOptions<GraphCommunityDetectionOptions>()),
                cancellationToken));

    private static void RegisterLinkPrediction(GraphAlgorithmRegistry registry) =>
        registry.Register(
            GraphBuiltInDescriptors.Create(
                new GraphBuiltInDescriptorOptions
                {
                    Id = "prediction.links",
                    Name = "Link prediction",
                    Description = "Ranks missing relationships with explainable topology evidence.",
                    Category = GraphAlgorithmCategory.Prediction,
                    ResultType = typeof(GraphLinkPredictionResult),
                    OptionsType = typeof(GraphLinkPredictionOptions),
                    Parameters =
                    [
                        GraphBuiltInDescriptors.Parameter("communities", typeof(GraphCommunityDetectionResult), false, "Optional precomputed community evidence.")
                    ],
                    Complexity = "Up to O(V²); bounded by candidate and result limits"
                }),
            (projection, invocation, cancellationToken) => projection.Run(
                new GraphLinkPredictionAlgorithm(invocation.AsOptions<GraphLinkPredictionOptions>(), invocation.GetOptionalParameter<GraphCommunityDetectionResult>("communities")),
                cancellationToken));
}