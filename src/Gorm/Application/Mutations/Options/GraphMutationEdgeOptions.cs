namespace Gorm.Application.Mutations.Options;

/// <summary>
/// Represents edge mutation options.
/// </summary>
public sealed class GraphMutationEdgeOptions
{
    /// <summary>
    /// Gets the optional business key for the edge mutation.
    /// </summary>
    public string? KeyValue { get; private set; }

    /// <summary>
    /// Sets the business key for the edge mutation.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The options.</returns>
    public GraphMutationEdgeOptions Key(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Mutation key cannot be null or whitespace.", nameof(key));
        }

        KeyValue = key;
        return this;
    }
}