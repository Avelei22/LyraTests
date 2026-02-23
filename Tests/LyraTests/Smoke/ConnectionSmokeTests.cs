using NUnit.Framework;

namespace LyraTests.Smoke;

public class ConnectionSmokeTests : SmokeTestBase
{
    [Test]
    public void Game_IsRunning_AndResponds()
    {
        var elements = Driver.GetAllElements(enabled: true);

        Assert.That(elements, Is.Not.Null, "GetAllElements must not return null.");
        Assert.That(elements.Count, Is.GreaterThan(0),
            "Game should have at least one enabled object. If 0, the app may not be loaded or AltTester may not be connected.");
    }

    [Test]
    public void Application_Has_Valid_Viewport()
    {
        var size = Driver.GetApplicationScreenSize();
        Assert.That(size, Is.Not.Null, "GetApplicationScreenSize must not return null.");
        Assert.That(size.x, Is.GreaterThan(0), "Viewport width must be greater than 0.");
        Assert.That(size.y, Is.GreaterThan(0), "Viewport height must be greater than 0.");
        Assert.That(size.x, Is.LessThanOrEqualTo(7680), "Viewport width should be within expected range.");
        Assert.That(size.y, Is.LessThanOrEqualTo(4320), "Viewport height should be within expected range.");
    }
}
