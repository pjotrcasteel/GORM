namespace Gorm.Application.Intelligence.Registry;

internal sealed class GraphBuiltInRouteDescriptorOptions
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required Type ResultType { get; init; }

    public required Type OptionsType { get; init; }

    public required string Complexity { get; init; }

    public GraphAlgorithmCategory Category { get; init; } = GraphAlgorithmCategory.Routing;

    public bool OptionsRequired { get; init; }
}