namespace Gorm.Application.Intelligence.Algorithms.Connectivity;

/// <summary>
/// Represents a connected-components result ordered from largest to smallest component.
/// </summary>
public sealed class GraphConnectedComponentsResult
{
    internal GraphConnectedComponentsResult(GraphComponentKind kind, IReadOnlyList<GraphConnectedComponent> components)
    {
        Kind = kind;
        Components = components;
    }

    /// <summary>
    /// Gets the component semantics used by the algorithm.
    /// </summary>
    public GraphComponentKind Kind { get; }

    /// <summary>
    /// Gets the discovered components.
    /// </summary>
    public IReadOnlyList<GraphConnectedComponent> Components { get; }
}