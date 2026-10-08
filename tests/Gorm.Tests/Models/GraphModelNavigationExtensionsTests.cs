using Gorm.Core.Models;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Models;

[TestClass]
public sealed class GraphModelNavigationExtensionsTests
{
    [TestMethod]
    public void GetNavigation_returns_configured_navigation()
    {
        var model = TestGraphModelFactory.CreateValidModel();

        var navigation = model.GetNavigation(typeof(TestPerson), nameof(TestPerson.Projects));

        Assert.AreEqual(nameof(TestPerson.Projects), navigation.PropertyName);
        Assert.AreEqual("Projects", navigation.RelationshipName);
    }

    [TestMethod]
    public void GetNavigation_validates_arguments_and_missing_navigation()
    {
        var model = TestGraphModelFactory.CreateValidModel();

        Assert.ThrowsExactly<ArgumentNullException>(() => GraphModelNavigationExtensions.GetNavigation(null!, typeof(TestPerson), nameof(TestPerson.Projects)));
        Assert.ThrowsExactly<ArgumentNullException>(() => model.GetNavigation(null!, nameof(TestPerson.Projects)));
        Assert.ThrowsExactly<ArgumentException>(() => model.GetNavigation(typeof(TestPerson), " "));

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => model.GetNavigation(typeof(TestPerson), "Missing"));
        Assert.Contains("No navigation 'Missing' exists", ex.Message);
    }
}