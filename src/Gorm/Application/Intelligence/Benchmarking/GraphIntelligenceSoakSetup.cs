using Gorm.Application.Intelligence.Caching;
using Gorm.Application.Intelligence.Live;
using Gorm.Application.Intelligence.Output;
using Gorm.Application.Intelligence.Runtime;

namespace Gorm.Application.Intelligence.Benchmarking;

internal sealed class GraphIntelligenceSoakSetup
{
    public required GraphIntelligenceSoakNode[] Nodes { get; init; }

    public required string InitialFingerprint { get; init; }

    public required GraphProjectionCache Cache { get; init; }

    public required GraphIntelligenceRuntime Runtime { get; init; }

    public required GraphIntelligenceOutputDispatcher Dispatcher { get; init; }

    public required GraphChannelProjectionChangeFeed Feed { get; init; }

    public required GraphIntelligenceSoakOutputCounter OutputCounter { get; init; }
}