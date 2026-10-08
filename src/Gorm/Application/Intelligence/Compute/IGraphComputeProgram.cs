using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Compute;

/// <summary>
/// Defines a vertex-centric graph computation executed in synchronized supersteps.
/// Implementations must be thread-safe when <see cref="GraphComputeOptions.DegreeOfParallelism"/> is greater than one.
/// </summary>
public interface IGraphComputeProgram<TState, TMessage>
{
    /// <summary>
    /// Creates the initial state for a projected node.
    /// </summary>
    public TState Initialize(Node node);

    /// <summary>
    /// Processes messages for one node and optionally updates state or sends new messages.
    /// </summary>
    public void Compute(GraphComputeContext<TState, TMessage> context, IReadOnlyList<TMessage> messages);
}