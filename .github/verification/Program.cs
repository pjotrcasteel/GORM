using Gorm.Application.Context;
using Gorm.Application.Execution.InMemory;
using Gorm.Core.Configuration;
using Gorm.Core.Primitives;

var graph = new PackageConsumerContext().UseInMemory();
var person = new PersonNode { Id = Guid.NewGuid(), Name = "Package consumer" };
var edge = new KnowsEdge { Id = Guid.NewGuid(), FromId = person.Id, ToId = person.Id };
if (graph.Set<PersonNode>() is null || person.Name != "Package consumer" || edge.FromId != edge.ToId)
{
    throw new InvalidOperationException("GORM package smoke test failed.");
}

Console.WriteLine("GORM package consumer passed.");

internal sealed class PackageConsumerContext : GraphContext
{
    protected override void OnModelCreating(GraphModelBuilder modelBuilder)
    {
        modelBuilder.Node<PersonNode>();
        modelBuilder.Edge<KnowsEdge, PersonNode, PersonNode>();
    }
}

internal sealed class PersonNode : Node
{
    public string Name { get; set; } = string.Empty;
}

internal sealed class KnowsEdge : Edge
{
}
