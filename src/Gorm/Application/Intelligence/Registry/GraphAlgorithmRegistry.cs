using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Registry;

/// <summary>
/// Thread-safe registry for built-in and application-defined graph algorithms.
/// </summary>
public sealed class GraphAlgorithmRegistry
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Registration> _registrations = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes an empty registry.
    /// </summary>
    public GraphAlgorithmRegistry(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Creates a registry containing all supported built-in algorithms.
    /// </summary>
    public static GraphAlgorithmRegistry CreateBuiltIn(TimeProvider? timeProvider = null)
    {
        var registry = new GraphAlgorithmRegistry(timeProvider);
        GraphBuiltInAlgorithmRegistrations.AddTo(registry);
        return registry;
    }

    /// <summary>
    /// Registers one descriptor and executor. Duplicate identifiers are rejected.
    /// </summary>
    public GraphAlgorithmRegistry Register(GraphAlgorithmDescriptor descriptor, GraphAlgorithmExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(executor);
        lock (_gate)
        {
            if (!_registrations.TryAdd(descriptor.Id, new Registration(descriptor, executor)))
            {
                throw new ArgumentException($"Algorithm '{descriptor.Id}' is already registered.", nameof(descriptor));
            }
        }

        return this;
    }

    /// <summary>
    /// Gets a stable identifier-ordered descriptor snapshot.
    /// </summary>
    public IReadOnlyList<GraphAlgorithmDescriptor> Descriptors
    {
        get
        {
            lock (_gate)
            {
                return Array.AsReadOnly(
                    _registrations.Values.Select(registration => registration.Descriptor).OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal).ToArray());
            }
        }
    }

    /// <summary>
    /// Executes an invocation and associates its result with the exact projection metadata.
    /// </summary>
    public GraphAlgorithmExecutionResult Execute(GraphProjectionSnapshot snapshot, GraphAlgorithmInvocation invocation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(invocation);
        cancellationToken.ThrowIfCancellationRequested();

        Registration registration;
        lock (_gate)
        {
            if (!_registrations.TryGetValue(invocation.AlgorithmId, out registration!))
            {
                throw new KeyNotFoundException($"Algorithm '{invocation.AlgorithmId}' is not registered.");
            }
        }

        ValidateInvocation(registration.Descriptor, invocation);
        var startedAt = _timeProvider.GetUtcNow();
        var startedTimestamp = _timeProvider.GetTimestamp();
        var result = registration.Executor(snapshot.Projection, invocation, cancellationToken);
        var completedAt = _timeProvider.GetUtcNow();

        if (result is not null && !registration.Descriptor.ResultType.IsInstanceOfType(result))
        {
            throw new InvalidOperationException(
                $"Registered algorithm '{invocation.AlgorithmId}' returned '{result.GetType().FullName}', " +
                $"but declares '{registration.Descriptor.ResultType.FullName}'.");
        }

        return new GraphAlgorithmExecutionResult
        {
            InvocationId = invocation.InvocationId,
            AlgorithmId = invocation.AlgorithmId,
            Snapshot = snapshot.Metadata,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            Duration = _timeProvider.GetElapsedTime(startedTimestamp),
            ResultType = registration.Descriptor.ResultType,
            Result = result
        };
    }

    private static void ValidateInvocation(GraphAlgorithmDescriptor descriptor, GraphAlgorithmInvocation invocation)
    {
        ValidateOptions(descriptor, invocation);
        ValidateParameters(descriptor, invocation);
    }

    private static void ValidateOptions(GraphAlgorithmDescriptor descriptor, GraphAlgorithmInvocation invocation)
    {
        if (descriptor.OptionsRequired && invocation.Options is null)
        {
            throw new ArgumentException($"Algorithm '{descriptor.Id}' requires options '{descriptor.OptionsType!.FullName}'.", nameof(invocation));
        }

        if (invocation.Options is not null &&
            (descriptor.OptionsType is null || !descriptor.OptionsType.IsInstanceOfType(invocation.Options)))
        {
            throw new ArgumentException(
                descriptor.OptionsType is null
                    ? $"Algorithm '{descriptor.Id}' does not accept an options object."
                    : $"Algorithm '{descriptor.Id}' expects options '{descriptor.OptionsType.FullName}'.",
                nameof(invocation));
        }
    }

    private static void ValidateParameters(GraphAlgorithmDescriptor descriptor, GraphAlgorithmInvocation invocation)
    {
        foreach (var parameter in descriptor.Parameters)
        {
            if (!invocation.Parameters.TryGetValue(parameter.Name, out var value) || value is null)
            {
                if (parameter.IsRequired)
                {
                    throw new ArgumentException($"Algorithm '{descriptor.Id}' requires parameter '{parameter.Name}'.", nameof(invocation));
                }

                continue;
            }

            if (!parameter.ParameterType.IsInstanceOfType(value))
            {
                throw new ArgumentException($"Parameter '{parameter.Name}' must be '{parameter.ParameterType.FullName}'.", nameof(invocation));
            }
        }

        var knownNames = descriptor.Parameters.Select(parameter => parameter.Name).ToHashSet(StringComparer.Ordinal);
        var unknown = invocation.Parameters.Keys.FirstOrDefault(name => !knownNames.Contains(name));
        if (unknown is not null)
        {
            throw new ArgumentException($"Algorithm '{descriptor.Id}' does not declare parameter '{unknown}'.", nameof(invocation));
        }
    }

    private sealed record Registration(GraphAlgorithmDescriptor Descriptor, GraphAlgorithmExecutor Executor);
}