namespace Gorm.Application.Intelligence.Registry;

internal sealed class GraphBuiltInDescriptorOptions
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required GraphAlgorithmCategory Category { get; init; }

    public required Type ResultType { get; init; }

    public Type? OptionsType { get; init; }

    public bool OptionsRequired { get; init; }

    public IEnumerable<GraphAlgorithmParameterDescriptor>? Parameters { get; init; }

    public string Complexity { get; init; } = "Domain dependent";

    public bool SupportsIncremental { get; init; }
}