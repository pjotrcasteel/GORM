using Gorm.Application.Execution;

namespace Gorm.Tests.Execution;

[TestClass]
public sealed class GraphQueryExecutionRequestTests
{
    [TestMethod]
    [DataRow(GraphQueryExecutionMode.First, null, 1)]
    [DataRow(GraphQueryExecutionMode.First, 5, 1)]
    [DataRow(GraphQueryExecutionMode.FirstOrDefault, 5, 1)]
    [DataRow(GraphQueryExecutionMode.Single, null, 2)]
    [DataRow(GraphQueryExecutionMode.Single, 5, 2)]
    [DataRow(GraphQueryExecutionMode.SingleOrDefault, 1, 1)]
    [DataRow(GraphQueryExecutionMode.Any, null, 1)]
    [DataRow(GraphQueryExecutionMode.List, null, null)]
    [DataRow(GraphQueryExecutionMode.List, 7, 7)]
    public void GetEffectiveTake_returns_expected_value_for_each_mode(GraphQueryExecutionMode mode, int? queryTake, int? expected)
    {
        var request = new GraphQueryExecutionRequest
        {
            Mode = mode
        };

        var result = request.GetEffectiveTake(queryTake);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(GraphQueryExecutionMode.Any, true)]
    [DataRow(GraphQueryExecutionMode.List, false)]
    [DataRow(GraphQueryExecutionMode.First, false)]
    public void IsExistenceOnly_is_true_only_for_any_mode(GraphQueryExecutionMode mode, bool expected)
    {
        var request = new GraphQueryExecutionRequest
        {
            Mode = mode
        };

        Assert.AreEqual(expected, request.IsExistenceOnly);
    }
}