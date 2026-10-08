using Gorm.Application.Context;
using Gorm.Core.Configuration;
using Gorm.Core.Sets;
using Gorm.Demo.Domain.Edges;
using Gorm.Demo.Domain.Nodes;

namespace Gorm.Demo.Infrastructure;

/// <summary>
/// Represents sql specification graph context.
/// </summary>
public sealed class SqlSpecificationGraphContext : GraphContext
{
    /// <inheritdoc/>
    public GraphSet<CharacteristicSpecificationNode> CharacteristicSpecifications => Set<CharacteristicSpecificationNode>();

    /// <inheritdoc/>
    public GraphEdgeSet<CharacteristicSpecificationMapEdge> CharacteristicMaps => EdgeSet<CharacteristicSpecificationMapEdge>();

    /// <summary>
    /// Executes on model creating.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(GraphModelBuilder modelBuilder)
    {
        modelBuilder.Node<CharacteristicSpecificationNode>(b =>
        {
            b.ToTable("CharacteristicSpecifications");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.Payload);

            b.HasOutgoingRelationship<CharacteristicSpecificationMapEdge, CharacteristicSpecificationNode>("MapsInto");
            b.HasIncomingRelationship<CharacteristicSpecificationMapEdge, CharacteristicSpecificationNode>("MappedFrom");

            b.HasNavigation(x => x.MapsInto, "MapsInto");
            b.HasNavigation(x => x.MappedFrom, "MappedFrom");
        });

        modelBuilder.Edge<CharacteristicSpecificationMapEdge>(b =>
        {
            b.ToTable("CharacteristicSpecificationMapsIntoCharacteristicSpecifications");
            b.HasKey(x => x.Id);
            b.From<CharacteristicSpecificationNode>();
            b.To<CharacteristicSpecificationNode>();
            b.Property(x => x.Payload);
        });
    }
}