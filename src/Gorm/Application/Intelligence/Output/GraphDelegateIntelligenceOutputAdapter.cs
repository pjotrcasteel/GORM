using Gorm.Application.Intelligence.Registry;

namespace Gorm.Application.Intelligence.Output;

/// <summary>
/// Adapts an explicit application delegate, including an opt-in persistence delegate, to the output contract.
/// </summary>
public sealed class GraphDelegateIntelligenceOutputAdapter : IGraphIntelligenceOutputAdapter
{
    private readonly Func<GraphAlgorithmExecutionResult, CancellationToken, ValueTask> _writer;

    /// <summary>
    /// Initializes the delegate adapter.
    /// </summary>
    public GraphDelegateIntelligenceOutputAdapter(string id, Func<GraphAlgorithmExecutionResult, CancellationToken, ValueTask> writer)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("An output adapter identifier cannot be empty.", nameof(id));
        }

        Id = id;
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public ValueTask WriteAsync(GraphAlgorithmExecutionResult execution, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(execution);
        cancellationToken.ThrowIfCancellationRequested();
        return _writer(execution, cancellationToken);
    }
}