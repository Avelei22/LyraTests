using AltTester.AltTesterSDK.Driver;
using LyraTests.Config;
using NUnit.Framework;

namespace LyraTests.Smoke;

[TestFixture]
public abstract class SmokeTestBase
{
    protected AltDriver Driver { get; private set; } = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        Driver = new AltDriver(
            host: AltDriverConfig.Host,
            port: AltDriverConfig.Port,
            appName: AltDriverConfig.AppName,
            connectTimeout: AltDriverConfig.ConnectTimeout
        );
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Driver?.Stop();
    }
}
