using Aspiring.AppHost;
using Xunit;

public class HealthChecksUIExtensionsTests
{
    [Fact]
    public void AddHealthChecksUI_AddsResource()
    {
        // Arrange
        var builder = new DistributedApplicationBuilder();

        // Act
        var resourceBuilder = builder.AddHealthChecksUI("TestResource");

        // Assert
        Assert.NotNull(resourceBuilder);
        Assert.Equal("TestResource", resourceBuilder.Resource.Name);
    }

    [Fact]
    public void WithReference_AddsMonitoredProject()
    {
        // Arrange
        var builder = new DistributedApplicationBuilder();
        var projectBuilder = new ProjectResourceBuilder("TestProject");

        // Act
        var resourceBuilder = builder.AddHealthChecksUI("TestResource")
            .WithReference(projectBuilder, "TestEndpoint", "/test");

        // Assert
        Assert.Single(resourceBuilder.Resource.MonitoredProjects);
        var monitoredProject = resourceBuilder.Resource.MonitoredProjects.First();
        Assert.Equal("TestProject", monitoredProject.Project.Resource.Name);
        Assert.Equal("TestEndpoint", monitoredProject.EndpointName);
        Assert.Equal("/test", monitoredProject.ProbePath);
    }
}

public class HealthChecksUIResourceTests
{
    [Fact]
    public void MonitoredProjects_IsInitialized()
    {
        // Arrange
        var resource = new HealthChecksUIResource("TestResource");

        // Act
        var monitoredProjects = resource.MonitoredProjects;

        // Assert
        Assert.NotNull(monitoredProjects);
        Assert.Empty(monitoredProjects);
    }

    [Fact]
    public void KnownEnvVars_Constants_AreCorrect()
    {
        // Assert
        Assert.Equal("ui_path", HealthChecksUIResource.KnownEnvVars.UiPath);
        Assert.Equal("HealthChecksUI__HealthChecks", HealthChecksUIResource.KnownEnvVars.HealthChecksConfigSection);
        Assert.Equal("Name", HealthChecksUIResource.KnownEnvVars.HealthCheckName);
        Assert.Equal("Uri", HealthChecksUIResource.KnownEnvVars.HealthCheckUri);
    }

    [Fact]
    public void KnownEnvVars_GetHealthCheckNameKey_ReturnsCorrectKey()
    {
        // Act
        var key = HealthChecksUIResource.KnownEnvVars.GetHealthCheckNameKey(0);

        // Assert
        Assert.Equal("HealthChecksUI__HealthChecks__0__Name", key);
    }

    [Fact]
    public void KnownEnvVars_GetHealthCheckUriKey_ReturnsCorrectKey()
    {
        // Act
        var key = HealthChecksUIResource.KnownEnvVars.GetHealthCheckUriKey(0);

        // Assert
        Assert.Equal("HealthChecksUI__HealthChecks__0__Uri", key);
    }
}
