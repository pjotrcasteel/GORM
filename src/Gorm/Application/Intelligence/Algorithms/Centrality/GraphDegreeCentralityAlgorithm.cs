using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Algorithms.Centrality;

/// <summary>
/// Calculates incoming, outgoing and total degree centrality.
/// </summary>
public sealed class GraphDegreeCentralityAlgorithm : IGraphAlgorithm<GraphDegreeCentralityResult>
{
    private readonly GraphDegreeCentralityOptions _options;

    /// <summary>
    /// Initializes a degree centrality algorithm.
    /// </summary>
    public GraphDegreeCentralityAlgorithm(GraphDegreeCentralityOptions? options = null)
    {
        _options = options ?? new GraphDegreeCentralityOptions();
    }

    /// <inheritdoc />
    public GraphDegreeCentralityResult Execute(GraphProjection projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return GraphDegreeCentralityCalculator.Calculate(projection, _options, null, null, cancellationToken).Result;
    }
}