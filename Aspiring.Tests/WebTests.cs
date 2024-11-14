using System.Net;
using Aspire.Hosting;
using Aspiring.AppHost;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspiring.Tests;

public class WebTests
{
    [Fact]
    public async Task GetWebResourcePathsReturnOkStatusCode()
    {
        // Arrange
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Aspiring_AppHost>();
        appHost.Services.AddSingleton<IRedactorProvider, NullRedactorProvider>();
        appHost.Services.AddExtendedHttpClientLogging();
        appHost.Services.AddLogging(configure =>
        {
            configure.AddConsole()
                .SetMinimumLevel(LogLevel.Debug);
        });
        using var handler = new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        appHost.Services.ConfigureHttpClientDefaults(configure =>
        {
            configure.ConfigurePrimaryHttpMessageHandler(() => handler);
        });

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        var paths = new[]
        {
            "/",
            "/health",
            "/metrics"
        };

        var tasks = paths.Select(paths =>
            app.CreateHttpClient("AspiringWeb").GetAsync(new Uri(paths, UriKind.Relative)));

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
    }

    [Fact]
    public void TestMongoDBConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder();
        var mongo = builder.AddMongoDB("MongoDB");
        var mongoDb = mongo.AddDatabase("MongoDB-Database");

        Assert.NotNull(mongo);
        Assert.NotNull(mongoDb);
    }

    [Fact]
    public void TestRedisConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cache = builder.AddRedis("cache").PublishAsContainer();

        Assert.NotNull(cache);
    }

    [Fact]
    public void TestApiConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder();
        var mongo = builder.AddMongoDB("MongoDB");
        var mongoDb = mongo.AddDatabase("MongoDB-Database");
        var cache = builder.AddRedis("cache").PublishAsContainer();
        var api = builder.AddProject<Projects.Aspiring_ApiService>("AspiringAPI")
            .WithExternalHttpEndpoints()
            .WithReference(cache)
            .WithReference(mongoDb);

        Assert.NotNull(api);
    }

    [Fact]
    public void TestGrafanaConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder();
        var grafana = builder.AddContainer("Grafana", "grafana/grafana")
            .WithBindMount("../grafana/config", "/etc/grafana", isReadOnly: true)
            .WithBindMount("../grafana/dashboards", "/var/lib/grafana/dashboards", isReadOnly: true)
            .WithHttpEndpoint(targetPort: 3000, name: "http");

        Assert.NotNull(grafana);
    }

    [Fact]
    public void TestSpaConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cache = builder.AddRedis("cache").PublishAsContainer();
        var api = builder.AddProject<Projects.Aspiring_ApiService>("AspiringAPI")
            .WithExternalHttpEndpoints()
            .WithReference(cache);
        var grafana = builder.AddContainer("Grafana", "grafana/grafana")
            .WithHttpEndpoint(targetPort: 3000, name: "http");
        var spa = builder.AddProject<Projects.Aspiring_Web>("AspiringWeb")
            .WithExternalHttpEndpoints()
            .WithReference(cache)
            .WithReference(api)
            .WithEnvironment("GRAFANA_URL", grafana.GetEndpoint("http"));

        Assert.NotNull(spa);
    }

    [Fact]
    public void TestHealthChecksUIConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder();
        var api = builder.AddProject<Projects.Aspiring_ApiService>("AspiringAPI")
            .WithExternalHttpEndpoints();
        var spa = builder.AddProject<Projects.Aspiring_Web>("AspiringWeb")
            .WithExternalHttpEndpoints();
        var healthChecksUI = builder.AddHealthChecksUI("Health-Checks-UI")
            .WithReference(api)
            .WithReference(spa)
            .WithExternalHttpEndpoints();

        Assert.NotNull(healthChecksUI);
    }

    [Fact]
    public void TestPrometheusConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder();
        var prometheus = builder.AddContainer("Prometheus", "prom/prometheus")
            .WithBindMount("../prometheus", "/etc/prometheus", isReadOnly: true)
            .WithHttpEndpoint(port: 9090, targetPort: 9090);

        Assert.NotNull(prometheus);
    }
}
