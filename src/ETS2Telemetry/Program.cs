using ETS2Telemetry.Services;
using MacroDeck.Plugin.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ETS2Telemetry;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = MacroDeckPlugin.CreatePlugin(args)
            .RegisterIntegration<PluginIntegration>();

        builder.Services.AddSingleton<TelemetryPollingService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<TelemetryPollingService>());
        builder.Services.AddHttpClient();

        var plugin = builder.Build();
        await plugin.RunAsync();
    }
}
