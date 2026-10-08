using Gorm.Application.Querying;
using Gorm.Application.Querying.Models;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Query;

[TestClass]
public sealed class GraphQueryTrackingTests
{
    [TestMethod]
    public void AsNoTracking_Sets_Query_Model_Tracking_Mode()
    {
        var context = new TestGraphContext();

        var model = context.People.AsNoTracking().ToQueryModel();

        Assert.AreEqual(GraphQueryTrackingMode.NoTracking, model.TrackingMode);
    }

    [TestMethod]
    public void AsTracking_Can_Reenable_Tracking_After_AsNoTracking()
    {
        var context = new TestGraphContext();

        var model = context.People.AsNoTracking().AsTracking().ToQueryModel();

        Assert.AreEqual(GraphQueryTrackingMode.TrackAll, model.TrackingMode);
    }

    [TestMethod]
    public void Debug_View_Shows_Tracking_Mode()
    {
        var context = new TestGraphContext();

        var debugView = context.People.AsNoTracking().ToDebugView();

        Assert.Contains("Tracking: NoTracking", debugView);
    }

    [TestMethod]
    public void ToSql_Returns_Command_Text_For_Query_Inspection()
    {
        var context = new TestGraphContext();

        var sql = context.People.Where(x => x.Name == "Alice").ToSql();

        Assert.Contains("SELECT", sql.CommandText);
        Assert.Contains("Person", sql.CommandText);
    }
}