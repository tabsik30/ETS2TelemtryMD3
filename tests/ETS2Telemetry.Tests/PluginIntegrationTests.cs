using MacroDeck.Plugin.Hosting;
using NUnit.Framework;

namespace ETS2Telemetry.Tests;

[TestFixture]
public class PluginIntegrationTests
{
    [Test]
    public void Plugin_builds()
    {
        var builder = MacroDeckPlugin.CreatePlugin([]);

        Assert.That(builder, Is.Not.Null);
    }
}
