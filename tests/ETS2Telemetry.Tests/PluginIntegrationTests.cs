using MacroDeck.Plugin.Testing;
using NUnit.Framework;

namespace ETS2Telemetry.Tests;

[TestFixture]
public class PluginIntegrationTests
{
    [Test]
    public async Task Plugin_builds_and_initializes()
    {
        await using var harness = PluginTestHarness.Create(builder =>
            builder.RegisterIntegration<PluginIntegration>());
        await harness.InitializeIntegrationsAsync();

        var integrations = harness.Integrations.ToArray();
        Assert.That(integrations, Has.Length.EqualTo(1));
        Assert.That(integrations[0].Id, Is.EqualTo("com.tabsik12.ets2-telemetry"));
    }
}
