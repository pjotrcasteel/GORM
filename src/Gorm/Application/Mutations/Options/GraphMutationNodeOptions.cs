namespace Gorm.Application.Mutations.Options;

/// <summary>
/// Represents node mutation options.
/// </summary>
public sealed class GraphMutationNodeOptions
{
    /// <summary>
    /// Gets the optional business key for the node mutation.
    /// </summary>
    public string? KeyValue { get; private set; }

    /// <summary>
    /// Sets the business key for the node mutation.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The options.</returns>
    public GraphMutationNodeOptions Key(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Mutation key cannot be null or whitespace.", nameof(key));
        }

        KeyValue = key;
        return this;
    }
}