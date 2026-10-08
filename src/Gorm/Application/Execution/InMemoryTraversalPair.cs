using Gorm.Core.Primitives;

namespace Gorm.Application.Execution;

internal sealed class InMemoryTraversalPair<TEdge, TNode>
    where TEdge : Edge
    where TNode : Node
{
    public required TEdge Edge { get; init; }

    public required TNode Node { get; init; }
}