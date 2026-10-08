using System.Collections.ObjectModel;

namespace Gorm.Application.Intelligence.Registry;

/// <summary>
/// Contains one explicit, provider-neutral algorithm invocation.
/// </summary>
public sealed class GraphAlgorithmInvocation
{

    /// <summary>
    /// Initializes an invocation.
    /// </summary>
    public GraphAlgorithmInvocation(string algorithmId, object? options = null, IReadOnlyDictionary<string, object?>? parameters = null, Guid? invocationId = null)
    {
        if (string.IsNullOrWhiteSpace(algorithmId))
        {
            throw new ArgumentException("An algorithm identifier cannot be empty.", nameof(algorithmId));
        }

        AlgorithmId = algorithmId;
        Options = options;
        InvocationId = invocationId ?? Guid.NewGuid();
        Parameters = new ReadOnlyDictionary<string, object?>(
            parameters is null
                ? new Dictionary<string, object?>(StringComparer.Ordinal)
                : new Dictionary<string, object?>(parameters, StringComparer.Ordinal));
    }

    /// <summary>
    /// Gets the correlation identifier supplied by the caller or generated locally.
    /// </summary>
    public Guid InvocationId { get; }

    /// <summary>
    /// Gets the registered algorithm identifier.
    /// </summary>
    public string AlgorithmId { get; }

    /// <summary>
    /// Gets the typed algorithm options, when supplied.
    /// </summary>
    public object? Options { get; }

    /// <summary>
    /// Gets named invocation parameters.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; }

    /// <summary>
    /// Gets typed options and verifies their runtime type.
    /// </summary>
    public TOptions? AsOptions<TOptions>()
        where TOptions : class
    {
        if (Options is null)
        {
            return null;
        }

        return Options as TOptions
            ?? throw new InvalidCastException(
                $"Algorithm '{AlgorithmId}' expects options of type '{typeof(TOptions).FullName}', " +
                $"but received '{Options.GetType().FullName}'.");
    }

    /// <summary>
    /// Gets a required named parameter and verifies its runtime type.
    /// </summary>
    public T GetRequiredParameter<T>(string name)
    {
        if (!Parameters.TryGetValue(name, out var value) || value is null)
        {
            throw new ArgumentException($"Algorithm '{AlgorithmId}' requires parameter '{name}'.", nameof(name));
        }

        return value is T typed
            ? typed
            : throw new ArgumentException($"Parameter '{name}' must be '{typeof(T).FullName}', but received '{value.GetType().FullName}'.", nameof(name));
    }

    /// <summary>
    /// Gets an optional named parameter and verifies its runtime type when present.
    /// </summary>
    public T? GetOptionalParameter<T>(string name)
        where T : class
    {
        if (!Parameters.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        return value as T
            ?? throw new ArgumentException($"Parameter '{name}' must be '{typeof(T).FullName}', but received '{value.GetType().FullName}'.", nameof(name));
    }
}