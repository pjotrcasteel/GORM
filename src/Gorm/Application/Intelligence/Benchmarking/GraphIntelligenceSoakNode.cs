using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Benchmarking;

internal sealed class GraphIntelligenceSoakNode : Node
{
    public int Revision { get; init; }
}