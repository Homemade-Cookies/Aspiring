using System.Net;
using Aspire.Hosting;
using Aspiring.AppHost;
using Microsoft.Extensions.DependencyInjection;

namespace Aspiring.Tests;

public class WebTests
{
    [Fact]
    public async Task GetWebResourcePathsReturnOkStatusCode()
    {
        // Arrange
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Aspiring_AppHost>();
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

        var tasks = paths.Select(path =>
            app.CreateHttpClient("AspiringWeb").GetAsync(new Uri(path, UriKind.Relative)));

        // Act
        foreach(var response in (await Task.WhenAll(tasks)))
        {
            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
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
    public void TestSqlServerConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder();
        var sqlResource = builder.AddSqlServer("sql");
        var sqlResourceDb = sqlResource.AddDatabase("sql-Database");

        Assert.NotNull(sqlResource);
        Assert.NotNull(sqlResourceDb);
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

    [Fact]
    public void TestSqlApiConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder();
        var sqlDb = builder.AddSqlServer("sql")
            .WithExternalHttpEndpoints()
            .AddDatabase("sql-Database");
        var cache = builder.AddRedis("cache").PublishAsContainer();
        var sqlApi = builder.AddProject<Projects.Aspiring_ApiService_Sql>("AspiringAPI-SQL")
            .WithExternalHttpEndpoints()
            .WithReference(sqlDb)
            .WithReference(cache);

        Assert.NotNull(sqlApi);
    }

    [Fact]
    public async Task GetWeatherForecastsReturnOkStatusCode()
    {
        // Arrange
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Aspiring_AppHost>();
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

        // Ensure the app is not null
        if (app == null)
        {
            throw new InvalidOperationException("The app failed to start.");
        }

        var paths = new[]
        {
            "/health",
            "/metrics",
            "/WeatherForecast"
        };

        var tasks = paths.Select(path =>
            app.CreateHttpClient("AspiringWeb").GetAsync(new Uri(path, UriKind.Relative)));

        // Act
        foreach (var response in (await Task.WhenAll(tasks)))
        {
            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}

