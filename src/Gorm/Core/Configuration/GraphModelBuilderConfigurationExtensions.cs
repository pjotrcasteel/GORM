using System.Reflection;
using Gorm.Core.Configuration.Interfaces;
using Gorm.Core.Primitives;

namespace Gorm.Core.Configuration;

/// <summary>
/// Provides configuration discovery for graph model building.
/// </summary>
public sealed partial class GraphModelBuilder
{
    private static readonly MethodInfo ApplyNodeConfigurationMethod =
        typeof(GraphModelBuilder)
            .GetMethod(nameof(ApplyNodeConfiguration), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo ApplyEdgeConfigurationMethod =
        typeof(GraphModelBuilder)
            .GetMethod(nameof(ApplyEdgeConfiguration), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Applies one node configuration.
    /// </summary>
    /// <typeparam name="TNode">The node type.</typeparam>
    /// <param name="configuration">The node configuration.</param>
    /// <returns>The model builder.</returns>
    public GraphModelBuilder ApplyConfiguration<TNode>(IGraphNodeTypeConfiguration<TNode> configuration)
        where TNode : Node
    {
        ArgumentNullException.ThrowIfNull(configuration);

        Node<TNode>(configuration.Configure);

        return this;
    }

    /// <summary>
    /// Applies one edge configuration.
    /// </summary>
    /// <typeparam name="TEdge">The edge type.</typeparam>
    /// <param name="configuration">The edge configuration.</param>
    /// <returns>The model builder.</returns>
    public GraphModelBuilder ApplyConfiguration<TEdge>(IGraphEdgeTypeConfiguration<TEdge> configuration)
        where TEdge : Edge
    {
        ArgumentNullException.ThrowIfNull(configuration);

        Edge<TEdge>(configuration.Configure);

        return this;
    }

    /// <summary>
    /// Applies all node and edge configurations found in an assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="predicate">An optional type filter.</param>
    /// <returns>The model builder.</returns>
    public GraphModelBuilder ApplyConfigurationsFromAssembly(Assembly assembly, Func<Type, bool>? predicate = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var configurations = FindConfigurations(assembly, predicate).ToArray();

        ValidateNoDuplicateConfigurations(configurations);

        foreach (var configurationGroup in configurations.GroupBy(configuration => configuration.ConfigurationType))
        {
            var instance = CreateConfiguration(configurationGroup.Key);

            foreach (var configuration in configurationGroup)
            {
                ApplyConfiguration(this, instance, configuration);
            }
        }

        return this;
    }

    private static IEnumerable<DiscoveredGraphConfiguration> FindConfigurations(Assembly assembly, Func<Type, bool>? predicate)
    {
        foreach (var type in assembly.GetTypes().Where(type => IsConcreteConfigurationType(type) && (predicate?.Invoke(type) ?? true)))
        {
            foreach (var configurationInterface in GetConfigurationInterfaces(type))
            {
                yield return new DiscoveredGraphConfiguration(
                    type,
                    configurationInterface.GetGenericArguments()[0],
                    GetConfigurationKind(configurationInterface),
                    configurationInterface);
            }
        }
    }

    private static bool IsConcreteConfigurationType(Type type) =>
        type is { IsAbstract: false, IsInterface: false, ContainsGenericParameters: false };

    private static IEnumerable<Type> GetConfigurationInterfaces(Type type) =>
        type
            .GetInterfaces()
            .Where(@interface => @interface.IsGenericType && IsSupportedConfigurationInterface(@interface.GetGenericTypeDefinition()));

    private static bool IsSupportedConfigurationInterface(Type interfaceDefinition) =>
        interfaceDefinition == typeof(IGraphNodeTypeConfiguration<>) ||
        interfaceDefinition == typeof(IGraphEdgeTypeConfiguration<>);

    private static GraphConfigurationKind GetConfigurationKind(Type configurationInterface) =>
        configurationInterface.GetGenericTypeDefinition() switch
        {
            var type when type == typeof(IGraphNodeTypeConfiguration<>) => GraphConfigurationKind.Node,
            var type when type == typeof(IGraphEdgeTypeConfiguration<>) => GraphConfigurationKind.Edge,
            _ => throw new InvalidOperationException($"Unsupported graph configuration interface '{configurationInterface.FullName}'.")
        };

    private static object CreateConfiguration(Type configurationType) =>
        Activator.CreateInstance(configurationType, nonPublic: true) ?? throw new InvalidOperationException(
            $"Could not create graph configuration '{configurationType.FullName}'.");

    private static void ApplyConfiguration(GraphModelBuilder modelBuilder, object configuration, DiscoveredGraphConfiguration discoveredConfiguration)
    {
        var method = discoveredConfiguration.Kind switch
        {
            GraphConfigurationKind.Node => ApplyNodeConfigurationMethod,
            GraphConfigurationKind.Edge => ApplyEdgeConfigurationMethod,
            _ => throw new InvalidOperationException($"Unsupported graph configuration kind '{discoveredConfiguration.Kind}'.")
        };

        method.MakeGenericMethod(discoveredConfiguration.ModelType).Invoke(null, [modelBuilder, configuration]);
    }

    private static void ApplyNodeConfiguration<TNode>(GraphModelBuilder modelBuilder, object configuration)
        where TNode : Node =>
        modelBuilder.ApplyConfiguration((IGraphNodeTypeConfiguration<TNode>)configuration);

    private static void ApplyEdgeConfiguration<TEdge>(GraphModelBuilder modelBuilder, object configuration)
        where TEdge : Edge =>
        modelBuilder.ApplyConfiguration((IGraphEdgeTypeConfiguration<TEdge>)configuration);

    private static void ValidateNoDuplicateConfigurations(IReadOnlyCollection<DiscoveredGraphConfiguration> configurations)
    {
        var duplicate = configurations.GroupBy(configuration => (configuration.Kind, configuration.ModelType)).FirstOrDefault(group => group.Count() > 1);

        if (duplicate is null)
        {
            return;
        }

        var names = duplicate.Select(configuration => configuration.ConfigurationType.FullName).Order(StringComparer.Ordinal).ToArray();

        throw new InvalidOperationException(
            $"Multiple {duplicate.Key.Kind.ToString().ToLowerInvariant()} configurations were found for " +
            $"'{duplicate.Key.ModelType.FullName}': {string.Join(", ", names)}.");
    }

    private sealed record DiscoveredGraphConfiguration(Type ConfigurationType, Type ModelType, GraphConfigurationKind Kind, Type InterfaceType);

    private enum GraphConfigurationKind
    {
        Node,
        Edge
    }
}