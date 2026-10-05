using System.Net;
using ETS2Telemetry.Services;
using Microsoft.Extensions.Http;
using NUnit.Framework;
using Serilog;

namespace ETS2Telemetry.Tests;

[TestFixture]
public class TelemetryPollingServiceTests
{
    [Test]
    public async Task ReadsIndicatorAndRouteTelemetry()
    {
        const string response = """
            {
              "truck": {
                "engineRpm": 1561.4,
                "engineRpmMax": 2500
              },
              "game": {
                "nextRestStopTime": "0001-01-01T10:52:00Z"
              },
              "navigation": {
                "estimatedDistance": 132500
              }
            }
            """;
        using var clientFactory = new TestHttpClientFactory(response);
        using var logger = new LoggerConfiguration().CreateLogger();
        using var telemetry = new TelemetryPollingService(clientFactory, logger);

        telemetry.Start();
        await telemetry.StartAsync(CancellationToken.None);
        await telemetry.WaitForFirstSnapshotAsync(CancellationToken.None);
        await telemetry.StopAsync(CancellationToken.None);

        Assert.That(telemetry.EngineRpm, Is.EqualTo(1561.4));
        Assert.That(telemetry.EngineRpmMax, Is.EqualTo(2500));
        Assert.That(telemetry.TimeUntilYawning, Is.EqualTo(TimeSpan.FromHours(10) + TimeSpan.FromMinutes(52)));
        Assert.That(telemetry.EstimatedDistanceMeters, Is.EqualTo(132500));
    }

    private sealed class TestHttpClientFactory(string response) : IHttpClientFactory, IDisposable
    {
        private readonly HttpClient _client = new(new StaticResponseHandler(response));

        public HttpClient CreateClient(string name) => _client;

        public void Dispose() => _client.Dispose();
    }

    private sealed class StaticResponseHandler(string response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response),
            });
    }
}
