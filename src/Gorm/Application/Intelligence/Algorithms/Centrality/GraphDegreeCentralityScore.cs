using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Centrality;

/// <summary>
/// Represents degree centrality values for a projected node.
/// </summary>
public sealed class GraphDegreeCentralityScore
{
    /// <summary>
    /// Gets the projected node.
    /// </summary>
    public required Node Node { get; init; }

    /// <summary>
    /// Gets the number of incoming edges.
    /// </summary>
    public required int IncomingDegree { get; init; }

    /// <summary>
    /// Gets the number of outgoing edges.
    /// </summary>
    public required int OutgoingDegree { get; init; }

    /// <summary>
    /// Gets normalized incoming degree centrality.
    /// </summary>
    public required double IncomingCentrality { get; init; }

    /// <summary>
    /// Gets normalized outgoing degree centrality.
    /// </summary>
    public required double OutgoingCentrality { get; init; }

    /// <summary>
    /// Gets normalized total degree centrality.
    /// </summary>
    public required double TotalCentrality { get; init; }
}