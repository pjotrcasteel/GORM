using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Registry;
/// <summary>
/// Executes a registered algorithm without provider-specific or persistence behaviour.
/// </summary>
public delegate object? GraphAlgorithmExecutor(GraphProjection projection, GraphAlgorithmInvocation invocation, CancellationToken cancellationToken);