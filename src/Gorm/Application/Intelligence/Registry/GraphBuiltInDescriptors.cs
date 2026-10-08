namespace Gorm.Application.Intelligence.Registry;

internal static class GraphBuiltInDescriptors
{
    public const string StartNodeId = "startNodeId";
    public const string DestinationNodeId = "destinationNodeId";

    public static GraphAlgorithmDescriptor Create(GraphBuiltInDescriptorOptions options) =>
        new(
            new GraphAlgorithmDescriptor.GraphAlgorithmDescriptorParameters
            {
                Id = options.Id,
                DisplayName = options.Name,
                Description = options.Description,
                Category = options.Category,
                ResultType = options.ResultType,
                OptionsType = options.OptionsType,
                OptionsRequired = options.OptionsRequired,
                Parameters = options.Parameters,
                Complexity = options.Complexity,
                SupportsIncremental = options.SupportsIncremental,
                SupportsLive = true
            });

    public static GraphAlgorithmDescriptor Route(GraphBuiltInRouteDescriptorOptions options) =>
        Create(
            new GraphBuiltInDescriptorOptions
            {
                Id = options.Id,
                Name = options.Name,
                Description = options.Description,
                Category = options.Category,
                ResultType = options.ResultType,
                OptionsType = options.OptionsType,
                OptionsRequired = options.OptionsRequired,
                Parameters =
                [
                    Parameter(StartNodeId, typeof(Guid), true, "Route source node identifier."),
                    Parameter(DestinationNodeId, typeof(Guid), true, "Route destination node identifier.")
                ],
                Complexity = options.Complexity
            });

    public static GraphAlgorithmParameterDescriptor Parameter(string name, Type type, bool required, string description) => new(name, type, required, description);
}