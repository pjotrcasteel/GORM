using System.Globalization;
using System.Reflection;
using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Intelligence.Registry;

namespace Gorm.Application.Intelligence.Studio;

/// <summary>
/// Exposes registry discovery, projection provenance and explicit execution hooks to a desktop GormStudio application.
/// </summary>
public sealed class GraphStudioIntelligenceBridge
{
    private const int CurrentSchemaVersion = 1;
    private static readonly Dictionary<Type, GraphStudioValueKind> ValueKinds =
        new()
        {
            [typeof(bool)] = GraphStudioValueKind.Boolean,
            [typeof(byte)] = GraphStudioValueKind.Integer,
            [typeof(sbyte)] = GraphStudioValueKind.Integer,
            [typeof(short)] = GraphStudioValueKind.Integer,
            [typeof(ushort)] = GraphStudioValueKind.Integer,
            [typeof(int)] = GraphStudioValueKind.Integer,
            [typeof(uint)] = GraphStudioValueKind.Integer,
            [typeof(long)] = GraphStudioValueKind.Integer,
            [typeof(ulong)] = GraphStudioValueKind.Integer,
            [typeof(float)] = GraphStudioValueKind.Number,
            [typeof(double)] = GraphStudioValueKind.Number,
            [typeof(decimal)] = GraphStudioValueKind.Number,
            [typeof(string)] = GraphStudioValueKind.Text,
            [typeof(char)] = GraphStudioValueKind.Text,
            [typeof(Guid)] = GraphStudioValueKind.Guid,
            [typeof(TimeSpan)] = GraphStudioValueKind.Duration
        };
    private readonly GraphAlgorithmRegistry _registry;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes the bridge.
    /// </summary>
    public GraphStudioIntelligenceBridge(GraphAlgorithmRegistry registry, TimeProvider? timeProvider = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Creates an immutable, identifier-ordered discovery catalog.
    /// </summary>
    public GraphStudioIntelligenceCatalog Catalog
    {
        get
        {
            var algorithms = _registry.Descriptors.Select(CreateAlgorithmMetadata).ToArray();
            return new GraphStudioIntelligenceCatalog
            {
                SchemaVersion = CurrentSchemaVersion,
                GeneratedAt = _timeProvider.GetUtcNow(),
                Algorithms = Array.AsReadOnly(algorithms)
            };
        }
    }

    /// <summary>
    /// Creates a serializable summary of an immutable projection snapshot.
    /// </summary>
    public static GraphStudioProjectionSummary Describe(GraphProjectionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new GraphStudioProjectionSummary
        {
            Key = snapshot.Key.Value,
            Version = snapshot.Version,
            ParentVersion = snapshot.Metadata.ParentVersion,
            CreatedAt = snapshot.Metadata.CreatedAt,
            Origin = snapshot.Metadata.Origin,
            TopologyFingerprint = snapshot.Metadata.TopologyFingerprint,
            NodeCount = snapshot.Projection.Statistics.NodeCount,
            EdgeCount = snapshot.Projection.Statistics.EdgeCount,
            IsolatedNodeCount = snapshot.Projection.Statistics.IsolatedNodeCount,
            Density = snapshot.Projection.Statistics.Density
        };
    }

    /// <summary>
    /// Runs a discovered algorithm in-process. This method returns data only and never writes graph mutations.
    /// </summary>
    public GraphAlgorithmExecutionResult Execute(GraphProjectionSnapshot snapshot, GraphAlgorithmInvocation invocation, CancellationToken cancellationToken = default) =>
        _registry.Execute(snapshot, invocation, cancellationToken);

    private static GraphStudioAlgorithmMetadata CreateAlgorithmMetadata(GraphAlgorithmDescriptor descriptor)
    {
        var defaults = CreateDefaultOptions(descriptor.OptionsType);
        var options = descriptor.OptionsType is null
            ? []
            : descriptor.OptionsType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.GetMethod is not null)
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(property => CreateValueMetadata(
                    property.Name,
                    property.PropertyType,
                    required: false,
                    description: null,
                    property.SetMethod is not null,
                    defaults is null ? null : property.GetValue(defaults)))
                .ToArray();
        var parameters = descriptor.Parameters
            .Select(parameter => CreateValueMetadata(parameter.Name, parameter.ParameterType, parameter.IsRequired, parameter.Description, isWritable: true, defaultValue: null))
            .ToArray();

        return new GraphStudioAlgorithmMetadata
        {
            Id = descriptor.Id,
            DisplayName = descriptor.DisplayName,
            Description = descriptor.Description,
            Category = descriptor.Category,
            ResultTypeName = GetTypeName(descriptor.ResultType),
            Complexity = descriptor.Complexity,
            SupportsIncremental = descriptor.SupportsIncremental,
            SupportsLive = descriptor.SupportsLive,
            OptionsRequired = descriptor.OptionsRequired,
            Options = Array.AsReadOnly(options),
            Parameters = Array.AsReadOnly(parameters)
        };
    }

    private static object? CreateDefaultOptions(Type? optionsType)
    {
        if (optionsType is null || optionsType.GetConstructor(Type.EmptyTypes) is null)
        {
            return null;
        }

        try
        {
            return Activator.CreateInstance(optionsType);
        }
        catch (Exception exception) when (exception is TargetInvocationException or MemberAccessException)
        {
            return null;
        }
    }

    private static GraphStudioValueMetadata CreateValueMetadata(string name, Type type, bool required, string? description, bool isWritable, object? defaultValue)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        var kind = GetValueKind(underlying);
        var editable = isWritable && kind != GraphStudioValueKind.Custom;
        return new GraphStudioValueMetadata
        {
            Name = name,
            TypeName = GetTypeName(type),
            Kind = kind,
            IsRequired = required,
            IsEditable = editable,
            DefaultValue = editable ? FormatDefault(defaultValue) : null,
            AllowedValues = underlying.IsEnum
                ? Array.AsReadOnly(Enum.GetNames(underlying))
                : [],
            Description = description
        };
    }

    private static GraphStudioValueKind GetValueKind(Type type)
    {
        if (ValueKinds.TryGetValue(type, out var kind))
        {
            return kind;
        }

        return type.IsEnum ? GraphStudioValueKind.Enumeration : GraphStudioValueKind.Custom;
    }

    private static string? FormatDefault(object? value) => value switch
    {
        null => null,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    private static string GetTypeName(Type type) => type.FullName ?? type.Name;
}