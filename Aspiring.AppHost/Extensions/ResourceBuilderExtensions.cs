using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspiring.AppHost.Extensions;

internal static class ResourceBuilderExtensions
{
    public static IResourceBuilder<ProjectResource> AddScalarEndpoint(this IResourceBuilder<ProjectResource> resourceBuilder)
    {
        resourceBuilder.WithEnvironment(context =>
        {
            context.EnvironmentVariables["Scalar__Enabled"] = bool.TrueString;
        });

        var endpointResource = new EndpointResource("scalar/v1", "scalar-docs", "Scalar API Documenation");
        return resourceBuilder.AddOpenEndpointCommand(endpointResource);
    }

    public static IResourceBuilder<ProjectResource> AddReDocEndpoint(this IResourceBuilder<ProjectResource> resourceBuilder)
    {
        resourceBuilder.WithEnvironment(context =>
        {
            context.EnvironmentVariables["ReDoc__Enabled"] = bool.TrueString;
        });

        var endpointResource = new EndpointResource("api-docs", "redoc-docs", "ReDoc API Documenation");
        return resourceBuilder.AddOpenEndpointCommand(endpointResource);
    }

    public static IResourceBuilder<ProjectResource> AddSwaggerUIEndpoint(this IResourceBuilder<ProjectResource> resourceBuilder)
    {
        resourceBuilder.WithEnvironment(context =>
        {
            context.EnvironmentVariables["SwaggerUI__Enabled"] = bool.TrueString;
        });
        var endpointResource = new EndpointResource("swagger", "swagger-ui-docs", "Swagger API Documenation");
        return resourceBuilder.AddOpenEndpointCommand(endpointResource);
    }

    public static IResourceBuilder<ProjectResource> AddOpenEndpointCommand(this IResourceBuilder<ProjectResource> resourceBuilder, EndpointResource endpointResource)
    {
        resourceBuilder.WithCommand(endpointResource.Name, endpointResource.DisplayName, executeCommand: async _ =>
        {
            try
            {
                resourceBuilder.Resource.TryGetEndpoints(out var endpoints);
                var apiBaseAddress = endpoints?.FirstOrDefault(x => x.UriScheme == endpointResource.Scheme)?.AllocatedEndpoint?.UriString;
                var url = $"{apiBaseAddress}/{endpointResource.Path}";
                using var p = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

                return await Task.FromResult(new ExecuteCommandResult { Success = true });
            }
            catch (Exception e)
            {
                return new ExecuteCommandResult { Success = false, ErrorMessage = e.Message };
            }
        },
        updateState: context => context.ResourceSnapshot.HealthStatus == HealthStatus.Healthy ?
            ResourceCommandState.Enabled : ResourceCommandState.Disabled,
        iconName: endpointResource.IconName,
        iconVariant: endpointResource.IconVariant);

        return resourceBuilder;
    }
}

internal record EndpointResource(string Path, string Name, string DisplayName, string IconName = "Document", IconVariant IconVariant = IconVariant.Filled, string Scheme = "https");
