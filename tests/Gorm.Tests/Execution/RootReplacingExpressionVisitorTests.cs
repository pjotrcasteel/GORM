using System.Linq.Expressions;
using Gorm.Application.Execution.InMemory.Visitors;
using Gorm.Application.Querying.Models;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Execution;

[TestClass]
public sealed class RootReplacingExpressionVisitorTests
{
    [TestMethod]
    public void Visit_Replaces_Matching_GraphQueryRootExpression()
    {
        var root = new GraphQueryRootExpression(typeof(TestPerson), GraphQueryElementKind.Node);
        var replacement = new List<TestPerson>().AsQueryable();
        var visitor = new RootReplacingExpressionVisitor(root, replacement);

        var result = visitor.Visit(root);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ConstantExpression>(result);
        Assert.AreSame(replacement, ((ConstantExpression)result).Value);
    }

    [TestMethod]
    public void Visit_NonMatching_GraphQueryRootExpression_Throws()
    {
        var root = new GraphQueryRootExpression(typeof(TestPerson), GraphQueryElementKind.Node);
        var other = new GraphQueryRootExpression(typeof(TestProject), GraphQueryElementKind.Node);
        var replacement = new List<TestPerson>().AsQueryable();
        var visitor = new RootReplacingExpressionVisitor(root, replacement);

        Assert.ThrowsExactly<ArgumentException>(() => visitor.Visit(other));
    }

    [TestMethod]
    public void Visit_Null_Returns_Null()
    {
        var root = new GraphQueryRootExpression(typeof(TestPerson), GraphQueryElementKind.Node);
        var replacement = new List<TestPerson>().AsQueryable();
        var visitor = new RootReplacingExpressionVisitor(root, replacement);

        var result = visitor.Visit((Expression?)null);

        Assert.IsNull(result);
    }
}