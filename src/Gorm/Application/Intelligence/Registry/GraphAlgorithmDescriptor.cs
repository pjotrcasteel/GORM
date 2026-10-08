namespace Gorm.Application.Intelligence.Registry;

/// <summary>
/// Provides immutable discovery metadata for one registered graph algorithm.
/// </summary>
public sealed class GraphAlgorithmDescriptor
{
    /// <summary>
    /// Initializes algorithm discovery metadata.
    /// </summary>
    public GraphAlgorithmDescriptor(GraphAlgorithmDescriptorParameters inputs)
    {
        var id = inputs.Id;
        var displayName = inputs.DisplayName;
        var description = inputs.Description;
        var category = inputs.Category;
        var resultType = inputs.ResultType;
        var optionsType = inputs.OptionsType;
        var optionsRequired = inputs.OptionsRequired;
        var parameters = inputs.Parameters;
        var complexity = inputs.Complexity;
        var supportsIncremental = inputs.SupportsIncremental;
        var supportsLive = inputs.SupportsLive;

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("An algorithm identifier cannot be empty.", nameof(inputs));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("An algorithm display name cannot be empty.", nameof(inputs));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("An algorithm description cannot be empty.", nameof(inputs));
        }

        ArgumentNullException.ThrowIfNull(resultType);
        if (optionsRequired && optionsType is null)
        {
            throw new ArgumentException("Required options must declare an options type.", nameof(inputs));
        }
        if (string.IsNullOrWhiteSpace(complexity))
        {
            throw new ArgumentException("An algorithm complexity description cannot be empty.", nameof(inputs));
        }

        var parameterArray = parameters?.ToArray() ?? [];
        var duplicateParameter = parameterArray.GroupBy(parameter => parameter.Name, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicateParameter is not null)
        {
            throw new ArgumentException($"Parameter '{duplicateParameter.Key}' is registered more than once.", nameof(inputs));
        }

        Id = id;
        DisplayName = displayName;
        Description = description;
        Category = category;
        ResultType = resultType;
        OptionsType = optionsType;
        OptionsRequired = optionsRequired;
        Parameters = Array.AsReadOnly(parameterArray);
        Complexity = complexity;
        SupportsIncremental = supportsIncremental;
        SupportsLive = supportsLive;
    }

    /// <summary>
    /// Gets the stable algorithm identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the user-facing name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the user-facing purpose.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the discovery category.
    /// </summary>
    public GraphAlgorithmCategory Category { get; }

    /// <summary>
    /// Gets the CLR result type.
    /// </summary>
    public Type ResultType { get; }

    /// <summary>
    /// Gets the optional CLR options type.
    /// </summary>
    public Type? OptionsType { get; }

    /// <summary>
    /// Gets whether callers must supply an options object.
    /// </summary>
    public bool OptionsRequired { get; }

    /// <summary>
    /// Gets named invocation parameters.
    /// </summary>
    public IReadOnlyList<GraphAlgorithmParameterDescriptor> Parameters { get; }

    /// <summary>
    /// Gets a concise complexity/safety hint.
    /// </summary>
    public string Complexity { get; }

    /// <summary>
    /// Gets whether an exact incremental implementation is available.
    /// </summary>
    public bool SupportsIncremental { get; }

    /// <summary>
    /// Gets whether the algorithm can be executed for successive live snapshots.
    /// </summary>
    public bool SupportsLive { get; }

    /// <summary>
    /// Groups the inputs for GraphAlgorithmDescriptor.
    /// </summary>
    public sealed class GraphAlgorithmDescriptorParameters
    {
        /// <summary>
        /// Gets or initializes id.
        /// </summary>
        public required string Id { get; init; }

        /// <summary>
        /// Gets or initializes displayName.
        /// </summary>
        public required string DisplayName { get; init; }

        /// <summary>
        /// Gets or initializes description.
        /// </summary>
        public required string Description { get; init; }

        /// <summary>
        /// Gets or initializes category.
        /// </summary>
        public required GraphAlgorithmCategory Category { get; init; }

        /// <summary>
        /// Gets or initializes resultType.
        /// </summary>
        public required Type ResultType { get; init; }

        /// <summary>
        /// Gets or initializes optionsType.
        /// </summary>
        public Type? OptionsType { get; init; } = null;

        /// <summary>
        /// Gets or initializes optionsRequired.
        /// </summary>
        public bool OptionsRequired { get; init; } = false;

        /// <summary>
        /// Gets or initializes parameters.
        /// </summary>
        public IEnumerable<GraphAlgorithmParameterDescriptor>? Parameters { get; init; } = null;

        /// <summary>
        /// Gets or initializes complexity.
        /// </summary>
        public string Complexity { get; init; } = "Domain dependent";

        /// <summary>
        /// Gets or initializes supportsIncremental.
        /// </summary>
        public bool SupportsIncremental { get; init; } = false;

        /// <summary>
        /// Gets or initializes supportsLive.
        /// </summary>
        public bool SupportsLive { get; init; } = true;
    }
}