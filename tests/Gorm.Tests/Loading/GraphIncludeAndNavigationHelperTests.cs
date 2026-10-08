using System.Linq.Expressions;
using System.Reflection;
using Gorm.Application.Querying;
using Gorm.Application.Querying.Translation;
using Gorm.Core.Loading;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Loading;

[TestClass]
public sealed class GraphIncludeAndNavigationHelperTests
{
    [TestMethod]
    public void GetPropertyName_reads_direct_member_access()
    {
        Expression<Func<TestPerson, object>> expression = x => x.Name;

        var result = GraphNavigationExpressionHelper.GetPropertyName(expression);

        Assert.AreEqual(nameof(TestPerson.Name), result);
    }

    [TestMethod]
    public void GetPropertyName_reads_converted_member_access()
    {
        Expression<Func<TestPerson, object>> expression = x => x.Age;

        var result = GraphNavigationExpressionHelper.GetPropertyName(expression);

        Assert.AreEqual(nameof(TestPerson.Age), result);
    }

    [TestMethod]
    public void GetPropertyName_throws_for_non_member_expression()
    {
        Expression<Func<TestPerson, object>> expression = x => x.Age + 1;

        Assert.ThrowsExactly<InvalidOperationException>(() => GraphNavigationExpressionHelper.GetPropertyName(expression));
    }

    [TestMethod]
    public void Extract_reads_name_based_include_chain_in_original_order()
    {
        var query = new List<TestPerson>().AsQueryable().IncludeRelationship("Projects").IncludeRelationshipWithEdges("Colleagues");

        var result = GraphIncludeExpressionHelper.Extract(query.Expression);

        Assert.HasCount(2, result.Includes);
        Assert.AreEqual("Projects", result.Includes[0].Name);
        Assert.AreEqual(GraphIncludeNameKind.Relationship, result.Includes[0].NameKind);
        Assert.IsFalse(result.Includes[0].IncludeEdge);
        Assert.AreEqual("Colleagues", result.Includes[1].Name);
        Assert.IsTrue(result.Includes[1].IncludeEdge);
        Assert.AreEqual(ExpressionType.Constant, result.QueryExpression.NodeType);
    }

    [TestMethod]
    public void Extract_reads_navigation_include_and_copies_configuration()
    {
        var query = new List<TestPerson>()
            .AsQueryable()
            .IncludeRelationship(x => x.Projects, include => include.Where(x => x.Title != "Archived").OrderByDescending(x => x.Title).Skip(2).Take(5));

        var result = GraphIncludeExpressionHelper.Extract(query.Expression);
        var include = result.Includes.Single();

        Assert.AreEqual(nameof(TestPerson.Projects), include.Name);
        Assert.AreEqual(GraphIncludeNameKind.Navigation, include.NameKind);
        Assert.IsFalse(include.IncludeEdge);
        Assert.IsNotNull(include.RelatedPredicate);
        Assert.IsNotNull(include.RelatedOrderBy);
        Assert.IsTrue(include.RelatedOrderDescending);
    }

    [TestMethod]
    public void Extract_reads_navigation_include_with_edges()
    {
        var query = new List<TestPerson>()
            .AsQueryable()
            .IncludeRelationshipWithEdges(
                x => x.Projects,
                include => include
                    .WhereEdge<TestWorksOn>(x => x.Role == "Lead")
                    .OrderByEdgeDescending<TestWorksOn, string>(x => x.Role));

        var result = GraphIncludeExpressionHelper.Extract(query.Expression);
        var include = result.Includes.Single();

        Assert.AreEqual(nameof(TestPerson.Projects), include.Name);
        Assert.IsTrue(include.IncludeEdge);
        Assert.IsNotNull(include.EdgePredicate);
        Assert.IsNotNull(include.EdgeOrderBy);
        Assert.IsTrue(include.EdgeOrderDescending);
    }

    [TestMethod]
    public void Extract_throws_when_relationship_name_is_not_a_constant_string()
    {
        var source = new List<TestPerson>().AsQueryable();
        var parameter = Expression.Parameter(typeof(string), "name");
        var method = typeof(GraphQueryExtensions)
            .GetMethods()
            .Single(x =>
                x.Name == nameof(GraphQueryExtensions.IncludeRelationship) &&
                x.IsGenericMethodDefinition &&
                x.GetParameters().Length == 2 &&
                x.GetParameters()[1].ParameterType == typeof(string))
            .MakeGenericMethod(typeof(TestPerson));

        var call = Expression.Call(null, method, source.Expression, parameter);

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => GraphIncludeExpressionHelper.Extract(call));

        Assert.Contains("non-empty constant string", ex.Message);
    }

    [TestMethod]
    public void Extract_throws_when_navigation_request_is_not_constant()
    {
        var source = new List<TestPerson>().AsQueryable();
        Expression<Func<TestPerson, IEnumerable<TestProject>>> navigation = x => x.Projects;
        var request = Expression.Parameter(typeof(GraphIncludeRequest), "request");
        var method = typeof(GraphQueryExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(x =>
                x.Name == nameof(GraphQueryExtensions.IncludeRelationshipCore) &&
                x.IsGenericMethodDefinition &&
                x.GetParameters().Length == 3 &&
                x.GetGenericArguments().Length == 2)
            .MakeGenericMethod(typeof(TestPerson), typeof(TestProject));

        var call = Expression.Call(null, method, source.Expression, Expression.Quote(navigation), request);

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => GraphIncludeExpressionHelper.Extract(call));

        Assert.Contains("Include request must be a constant GraphIncludeRequest", ex.Message);
    }

    [TestMethod]
    public void Extract_throws_when_navigation_argument_is_not_a_lambda()
    {
        var source = new List<TestPerson>().AsQueryable();
        var request = new GraphRelationshipIncludeBuilder<TestProject>(nameof(TestPerson.Projects), GraphIncludeNameKind.Navigation, includeEdge: false).Build();

        var method = typeof(GraphQueryExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(x => x.Name == nameof(GraphQueryExtensions.IncludeRelationshipCore) && x.IsGenericMethodDefinition && x.GetParameters().Length == 3)
            .MakeGenericMethod(typeof(TestPerson), typeof(TestProject));

        var call = Expression.Call(
            null,
            method,
            source.Expression,
            Expression.Constant(null, typeof(Expression<Func<TestPerson, IEnumerable<TestProject>>>)),
            Expression.Constant(request));

        Assert.ThrowsExactly<InvalidOperationException>(() => GraphIncludeExpressionHelper.Extract(call));
    }
}