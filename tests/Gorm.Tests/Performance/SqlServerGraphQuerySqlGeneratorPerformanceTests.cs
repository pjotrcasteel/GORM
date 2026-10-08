using System.Linq.Expressions;
using Gorm.Application.Querying.Models;
using Gorm.Infrastructure.Providers.SqlServer;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Performance;

[TestClass]
public sealed class SqlServerGraphQuerySqlGeneratorPerformanceTests
{
    [TestMethod]
    public void Generate_ScalarProjectionWithProjectedFilter_KeepsInnerAndOuterParameters()
    {
        var context = new TestGraphContext();

        var queryModel = new GraphQueryModel
        {
            RootElementType = typeof(TestPerson),
            RootElementKind = GraphQueryElementKind.Node,
            Projection = new GraphScalarProjection
            {
                ResultType = typeof(string),
                SourcePropertyName = nameof(TestPerson.Name),
                SourceKind = GraphProjectionSourceKind.Node
            }
        };

        queryModel.Steps.Add(new GraphFilterStep
        {
            InputElementType = typeof(TestPerson),
            InputElementKind = GraphQueryElementKind.Node,
            OutputElementType = typeof(TestPerson),
            OutputElementKind = GraphQueryElementKind.Node,
            Predicate = (Expression<Func<TestPerson, bool>>)(person => person.Age > 18),
            IsProjected = false
        });

        queryModel.Steps.Add(new GraphFilterStep
        {
            InputElementType = typeof(string),
            InputElementKind = GraphQueryElementKind.Node,
            OutputElementType = typeof(string),
            OutputElementKind = GraphQueryElementKind.Node,
            Predicate = (Expression<Func<string, bool>>)(name => name.StartsWith('A')),
            IsProjected = true
        });

        queryModel.Orderings.Add(new GraphOrdering
        {
            PropertyName = "Value",
            Descending = true,
            IsProjected = true
        });

        var sql = SqlServerGraphQuerySqlGenerator.Generate(context.Model, queryModel);

        Assert.Contains("FROM (", sql.CommandText);
        Assert.Contains("[q].[Value]", sql.CommandText);
        Assert.Contains("ORDER BY [q].[Value] DESC", sql.CommandText);
        Assert.HasCount(2, sql.Parameters);
        Assert.AreEqual(18, sql.Parameters[0].Value);
        Assert.AreEqual("A%", sql.Parameters[1].Value);
    }

    [TestMethod]
    public void Generate_WholeEntityProjectionWithoutProjectedOperations_DoesNotCreateOuterWrapper()
    {
        var context = new TestGraphContext();

        var projection = new GraphObjectProjection
        {
            ResultType = typeof(WholePersonProjection)
        };

        projection.Bindings.Add(new GraphProjectionBinding
        {
            TargetMemberName = "Person",
            SourcePropertyName = null,
            SourceKind = GraphProjectionSourceKind.Node,
            IsWholeEntity = true
        });

        var queryModel = new GraphQueryModel
        {
            RootElementType = typeof(TestPerson),
            RootElementKind = GraphQueryElementKind.Node,
            Projection = projection
        };

        var sql = SqlServerGraphQuerySqlGenerator.Generate(context.Model, queryModel);

        Assert.IsFalse(sql.CommandText.Contains("FROM (", StringComparison.Ordinal));
        Assert.Contains("[Person__Id]", sql.CommandText);
        Assert.Contains("[Person__Name]", sql.CommandText);
    }

    [TestMethod]
    public void Generate_SkipWithoutOrdering_AppendsSingleKeyOrdering()
    {
        var context = new TestGraphContext();

        var queryModel = new GraphQueryModel
        {
            RootElementType = typeof(TestPerson),
            RootElementKind = GraphQueryElementKind.Node,
            SkipCount = 10,
            TakeCount = 5
        };

        var sql = SqlServerGraphQuerySqlGenerator.Generate(context.Model, queryModel);

        Assert.Contains("ORDER BY [n0].[Id] ASC", sql.CommandText);
        Assert.Contains("OFFSET 10 ROWS", sql.CommandText);
        Assert.Contains("FETCH NEXT 5 ROWS ONLY", sql.CommandText);
    }

    private sealed class WholePersonProjection
    {
        public TestPerson Person { get; set; } = null!;
    }
}