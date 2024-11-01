using System.Net;

namespace Aspiring.Tests;

public class WebTests
{
    [Fact]
    public async Task GetWebResourceRootReturnsOkStatusCode()
    {
        // Arrange
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Aspiring_AppHost>();
        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        // Act
        using var httpClient = app.CreateHttpClient("webfrontend");
        var response = await httpClient.GetAsync(new Uri("/"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
