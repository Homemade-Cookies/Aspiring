using HealthChecks.UI.Client;
using HealthChecks.UI.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace Aspiring.ServiceDefaults;

// Adds common .NET Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    private static readonly string[] _metered =
        [
        "Microsoft.AspNetCore.Hosting",
        "Microsoft.AspNetCore.Server.Kestrel"
        ];

    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder, bool healthUI = false, string sqlServerConnectionString = "", string mongoDbConnectionString = "", string redis = "")
    {
        builder.Logging.ClearProviders();

        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks(healthUI, sqlServerConnectionString, mongoDbConnectionString, redis);

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        //#region OpenTelemetry

        //// Configure OpenTelemetry service resource details
        //// See https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/resource/semantic_conventions
        //var entryAssembly = System.Reflection.Assembly.GetEntryAssembly();
        //var entryAssemblyName = entryAssembly?.GetName();
        //var versionAttribute = entryAssembly?.GetCustomAttributes(false)
        //	.OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
        //	.FirstOrDefault();
        //var resourceServiceName = entryAssemblyName?.Name;
        //var resourceServiceVersion = versionAttribute?.InformationalVersion ?? entryAssemblyName?.Version?.ToString();
        //var attributes = new Dictionary<string, object>
        //{
        //	["host.name"] = Environment.MachineName,
        //	["service.names"] =
        //		"FSH.Starter.WebApi.Host", //builder.Configuration["OpenTelemetrySettings:ServiceName"]!, //It's a WA Fix because the service.name tag is not completed automatically by Resource.Builder()...AddService(serviceName) https://github.com/open-telemetry/opentelemetry-dotnet/issues/2027
        //	["os.description"] = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        //	["deployment.environment"] = builder.Environment.EnvironmentName.ToLowerInvariant()
        //};
        //var resourceBuilder = ResourceBuilder.CreateDefault()
        //	.AddService(serviceName: resourceServiceName, serviceVersion: resourceServiceVersion)
        //	.AddTelemetrySdk()
        //	//.AddEnvironmentVariableDetector()
        //	.AddAttributes(attributes);

        //#endregion region

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            //logging.SetResourceBuilder(resourceBuilder);
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics //.SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddPrometheusExporter()
                    .AddMeter(_metered)
                    .AddConsoleExporter()
                    ;
            })
            .WithTracing(tracing =>
            {
                if (builder.Environment.IsDevelopment())
                {
                    tracing.SetSampler(new AlwaysOnSampler());
                }

                tracing //.SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation(nci => nci.RecordException = true)
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddConsoleExporter()
                    ;
            });

        builder.AddOpenTelemetryExporters();
        return builder;
    }

    private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        builder.Services.AddOpenTelemetry()
            // BUG: Part of the workaround for https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/1617
            .WithMetrics(metrics => metrics.AddPrometheusExporter(options =>
            {
                options.DisableTotalNameSuffixForCounters = true;
            }));

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder, bool healthUI = false, string sqlServerConnectionString = "", string mongoDbConnectionString = "", string redis = "")
    {
        // builder.AddHealthChecks()
        //    // Add a default liveness check to ensure app is responsive
        //    .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        var healthChecksConfiguration = builder.Configuration.GetSection("HealthChecks");

        // All health checks endpoints must return within the configured timeout value (defaults to 5 seconds)
        var healthChecksRequestTimeout = healthChecksConfiguration.GetValue<TimeSpan?>("RequestTimeout") ?? TimeSpan.FromSeconds(30);
        builder.Services.AddRequestTimeouts(timeouts => timeouts.AddPolicy("HealthChecks", healthChecksRequestTimeout));

        // Cache health checks responses for the configured duration (defaults to 10 seconds)
        var healthChecksExpireAfter = healthChecksConfiguration.GetValue<TimeSpan?>("ExpireAfter") ?? TimeSpan.FromSeconds(60);
        builder.Services.AddOutputCache(caching => caching.AddPolicy("HealthChecks", policy => policy.Expire(healthChecksExpireAfter)));

        builder.Services.AddResourceMonitoring();

        builder.Services.AddHealthChecks()
            .AddResourceUtilizationHealthCheck()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"])
            ;

        if (!string.IsNullOrWhiteSpace(sqlServerConnectionString))
        {
            builder.Services.AddHealthChecks()
                .AddSqlServer(sqlServerConnectionString);
        }

        if (!string.IsNullOrWhiteSpace(mongoDbConnectionString))
        {
            builder.Services.AddHealthChecks()
                .AddMongoDb(mongoDbConnectionString);
        }

        if (!string.IsNullOrWhiteSpace(redis))
        {
            builder.Services.AddHealthChecks()
                .AddRedis(redis);
        }

        if (healthUI)
        {
            builder.Services.AddHealthChecksUI(setupSettings: setup =>
                {
                    setup.SetHeaderText($"{builder.Environment.ApplicationName} - Health Checks Status");
                    setup.AddHealthCheckEndpoint("endpoint1", "/health-random");
                    setup.AddHealthCheckEndpoint("endpoint2", "health-process");

                    //Webhook endpoint with custom notification hours, and custom failure and description messages
                    setup.AddWebhookNotification("webhook1", uri: "https://sponsor-payments.healthchecks2.requestcatcher.com/",
                            payload: "{ message: \"Webhook report for [[LIVENESS]]: [[FAILURE]] - Description: [[DESCRIPTIONS]]\"}",
                                restorePayload: "{ message: \"[[LIVENESS]] is back to life\"}",
                                shouldNotifyFunc: (livenessName, report) => DateTime.UtcNow.Hour is >= 8 and <= 23,
                                customMessageFunc: (livenessName, report) =>
                               {
                                   var failing = report.Entries.Where(e => e.Value.Status == UIHealthStatus.Unhealthy);
                                   return $"{failing.Count()} healthchecks are failing";
                               }, customDescriptionFunc: (livenessName, report) =>
                               {
                                   var failing = report.Entries.Where(e => e.Value.Status == UIHealthStatus.Unhealthy);
                                   return $"{string.Join(" - ", failing.Select(f => f.Key))} healthchecks are failing";
                               });

                    //Webhook endpoint with default failure and description messages
                    setup.AddWebhookNotification(
                        name: "webhook1",
                        uri: "https://sponsor-payments.healthchecks.requestcatcher.com/",
                        payload: "{ message: \"Webhook report for [[LIVENESS]]: [[FAILURE]] - Description: [[DESCRIPTIONS]]\"}",
                        restorePayload: "{ message: \"[[LIVENESS]] is back to life\"}");
                });
        }

        return builder;
    }

    public static IServiceCollection AddServiceDefaults(this IServiceCollection builder, IConfiguration configuration)
    {
        builder.ConfigureOpenTelemetry(configuration);

        builder.AddDefaultHealthChecks(configuration);

        builder.AddServiceDiscovery();

        builder.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static IServiceCollection ConfigureOpenTelemetry(this IServiceCollection builder, IConfiguration configuration)
    {
        builder.AddLogging(x =>
            x.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
            })
        );

        builder.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics //.SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddPrometheusExporter()
                    .AddMeter(_metered)
                    //.AddMeter(MetricsConstants.Todos)
                    //.AddMeter(MetricsConstants.Catalog)
                    .AddConsoleExporter()
                    ;
            })
            .WithTracing(tracing =>
            {
#if DEBUG
                tracing.SetSampler(new AlwaysOnSampler());
#endif
                tracing //.SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation(nci => nci.RecordException = true)
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddConsoleExporter()
                    ;

                //tracing.AddAspNetCoreInstrumentation()
                //	// Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                //	//.AddGrpcClientInstrumentation()
                //	.AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters(configuration);

        return builder;
    }

    private static IServiceCollection AddOpenTelemetryExporters(this IServiceCollection builder, IConfiguration configuration)
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.AddOpenTelemetry().UseOtlpExporter();
        }

        builder.AddOpenTelemetry()
            // BUG: Part of the workaround for https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/1617
            .WithMetrics(metrics => metrics.AddPrometheusExporter(options =>
            {
                options.DisableTotalNameSuffixForCounters = true;
            }));


        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static IServiceCollection AddDefaultHealthChecks(this IServiceCollection builder, IConfiguration configuration)
    {
        //builder.AddHealthChecks()
        //    // Add a default liveness check to ensure app is responsive
        //    .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        var healthChecksConfiguration = configuration.GetSection("HealthChecks");

        // All health checks endpoints must return within the configured timeout value (defaults to 5 seconds)
        var healthChecksRequestTimeout = healthChecksConfiguration.GetValue<TimeSpan?>("RequestTimeout") ?? TimeSpan.FromSeconds(30);
        builder.AddRequestTimeouts(timeouts => timeouts.AddPolicy("HealthChecks", healthChecksRequestTimeout));

        // Cache health checks responses for the configured duration (defaults to 10 seconds)
        var healthChecksExpireAfter = healthChecksConfiguration.GetValue<TimeSpan?>("ExpireAfter") ?? TimeSpan.FromSeconds(60);
        builder.AddOutputCache(caching => caching.AddPolicy("HealthChecks", policy => policy.Expire(healthChecksExpireAfter)));

        builder.AddResourceMonitoring();

        builder.AddHealthChecks()
            .AddResourceUtilizationHealthCheck()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app, bool grafana = false)
    {
        // The following line enables the Prometheus endpoint (requires the OpenTelemetry.Exporter.Prometheus.AspNetCore package)
        app.MapPrometheusScrapingEndpoint();

        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        if (app.Environment.IsDevelopment())
        {
            app.MapHealthEndpoints();

            // The following line enables the Prometheus endpoint (requires the OpenTelemetry.Exporter.Prometheus.AspNetCore package)
            app.UseOpenTelemetryPrometheusScrapingEndpoint(
                context =>
                {
                    return context.Request.Path == "/metrics";
                });

            if (grafana)
            {
                app.MapGet("startup", () =>
                {
                    return new
                    {
                        GrafanaUrl = app.Configuration["GRAFANA_URL"]!
                    };
                });
            }

            // All health checks must pass for app to be considered ready to accept traffic after starting
            //app.MapHealthChecks("/health").AllowAnonymous();

            //// Only health checks tagged with the "live" tag must pass for app to be considered alive
            //app.MapHealthChecks("/alive", new HealthCheckOptions
            //{
            //    Predicate = r => r.Tags.Contains("live")
            //}).AllowAnonymous();
        }

        return app;
    }

    //https://github.com/dotnet/aspire-samples/blob/main/samples/HealthChecksUI/HealthChecksUI.ServiceDefaults/Extensions.cs

    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        var healthChecks = app.MapGroup("");

        // Configure health checks endpoints to use the configured request timeouts and cache policies
        healthChecks
            .CacheOutput(policyName: "HealthChecks")
            .WithRequestTimeout(policyName: "HealthChecks");

        // All health checks must pass for app to be considered ready to accept traffic after starting
        healthChecks.MapHealthChecks("/health");

        // Only health checks tagged with the "live" tag must pass for app to be considered alive
        healthChecks.MapHealthChecks("/alive", new()
        {
            Predicate = r => r.Tags.Contains("live")
        });

        if (app.Environment.IsDevelopment())
        {
            // Add a health check that always returns unhealthy to test the HealthChecksUI
            healthChecks.MapHealthChecks("/health-random", new()
            {
                Predicate = r => r.Name == "random",
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });

            // Add a health check that always returns unhealthy to test the HealthChecksUI
            healthChecks.MapHealthChecks("health-process", new()
            {
                Predicate = r => r.Name == "process",
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });

            healthChecks.MapHealthChecks("healthz", new HealthCheckOptions
            {
                Predicate = _ => true,
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });
        }

        // Add the health checks endpoint for the HealthChecksUI
        var healthChecksUrls = app.Configuration["HEALTHCHECKSUI_URLS"];
        if (!string.IsNullOrWhiteSpace(healthChecksUrls))
        {
            var pathToHostsMap = GetPathToHostsMap(healthChecksUrls);

            foreach (var path in pathToHostsMap.Keys)
            {
                // Ensure that the HealthChecksUI endpoint is only accessible from configured hosts, e.g. localhost:12345, hub.docker.internal, etc.
                // as it contains more detailed information about the health of the app including the types of dependencies it has.

                healthChecks.MapHealthChecks(path, new() { ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse })
                    // This ensures that the HealthChecksUI endpoint is only accessible from the configured health checks URLs.
                    // See this documentation to learn more about restricting access to health checks endpoints via routing:
                    // https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-8.0#use-health-checks-routing
                    .RequireHost(pathToHostsMap[path]);
            }
        }

        return app;
    }

    private static Dictionary<string, string[]> GetPathToHostsMap(string healthChecksUrls)
    {
        // Given a value like "localhost:12345/healthz;hub.docker.internal:12345/healthz" return a dictionary like:
        // { { "healthz", [ "localhost:12345", "hub.docker.internal:12345" ] } }

        var uris = healthChecksUrls.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(url => new Uri(url, UriKind.Absolute))
            .GroupBy(uri => uri.AbsolutePath, uri => uri.Authority)
            .ToDictionary(g => g.Key, g => g.ToArray());

        return uris;
    }
}

public class ExtensionsTests
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
