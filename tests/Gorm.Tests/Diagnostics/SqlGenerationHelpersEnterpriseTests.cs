using Gorm.Infrastructure.Providers.SqlServer.Helpers;

namespace Gorm.Tests.Diagnostics;

[TestClass]
public sealed class SqlGenerationHelpersEnterpriseTests
{
    [TestMethod]
    public void Escape_WhenIdentifierContainsClosingBracket_EscapesClosingBracket()
    {
        var result = SqlGenerationHelpers.Escape("My]Table");

        Assert.AreEqual("[My]]Table]", result);
    }

    [TestMethod]
    public void EscapeFullName_WhenCalled_EscapesSchemaAndTable()
    {
        var result = SqlGenerationHelpers.EscapeFullName("dbo", "My]Table");

        Assert.AreEqual("[dbo].[My]]Table]", result);
    }
}