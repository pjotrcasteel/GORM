using Gorm.Application.History.Querying;

namespace Gorm.Tests.History;

[TestClass]
public sealed class SqlServerGraphHistoryReaderTests
{
    [TestMethod]
    public void GraphHistoryQueryableExtensions_Throws_When_History_Is_Not_Configured()
    {
        var context = new DemoGraphContextWithoutHistory();

        Assert.Throws<NotSupportedException>(() =>
        {
            _ = context.History<DemoNode>().ToList();
        });
    }

    private sealed class DemoGraphContextWithoutHistory : Application.Context.GraphContext
    {
        protected override void OnModelCreating(Core.Configuration.GraphModelBuilder modelBuilder)
        {
            modelBuilder.Node<DemoNode>(node =>
            {
                node.ToTable("DemoNodes");
                node.Property(x => x.Name);
            });
        }
    }

    private sealed class DemoNode : Core.Primitives.Node
    {
        public string Name { get; set; } = string.Empty;
    }
}