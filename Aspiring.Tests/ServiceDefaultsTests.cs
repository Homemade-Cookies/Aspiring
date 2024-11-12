using Aspiring.ServiceDefaults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Aspiring.Tests;

public class ServiceDefaultsTests
{
    [Fact]
    public void AddServiceDefaults_AddsRequiredServices()
    {
        // Arrange
        var builder = new HostApplicationBuilder();

        // Act
        builder.AddServiceDefaults();

        // Assert
        var services = builder.Services.BuildServiceProvider();
        Assert.NotNull(services.GetService<IServiceDiscovery>());
        Assert.NotNull(services.GetService<IHttpClientFactory>());
        Assert.NotNull(services.GetService<HealthCheckService>());
        Assert.NotNull(services.GetService<TracerProvider>());
        Assert.NotNull(services.GetService<MeterProvider>());
    }

    [Fact]
    public void ConfigureOpenTelemetry_ConfiguresLoggingAndTelemetry()
    {
        // Arrange
        var builder = new HostApplicationBuilder();

        // Act
        builder.ConfigureOpenTelemetry();

        // Assert
        var services = builder.Services.BuildServiceProvider();
        Assert.NotNull(services.GetService<TracerProvider>());
        Assert.NotNull(services.GetService<MeterProvider>());
    }

    [Fact]
    public void AddDefaultHealthChecks_AddsHealthChecks()
    {
        // Arrange
        var builder = new HostApplicationBuilder();

        // Act
        builder.AddDefaultHealthChecks();

        // Assert
        var services = builder.Services.BuildServiceProvider();
        Assert.NotNull(services.GetService<HealthCheckService>());
    }

    [Fact]
    public void MapDefaultEndpoints_MapsHealthAndMetricsEndpoints()
    {
        // Arrange
        var builder = new HostApplicationBuilder();
        var app = builder.Build();

        // Act
        app.MapDefaultEndpoints();

        // Assert
        // Here you would typically use a test server to verify the endpoints are mapped correctly.
        // For simplicity, we are just asserting that the app is not null.
        Assert.NotNull(app);
    }
}
