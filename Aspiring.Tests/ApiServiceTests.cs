using System.Net;
using Aspiring.ApiService;

namespace Aspiring.Tests;

public class ApiServiceTests
{
    [Fact]
    public async Task GetWeatherForecastEndpointReturnsExpectedData()
    {
        // Arrange
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Aspiring_AppHost>();
        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        // Act
        using var httpClient = app.CreateHttpClient("apiservice");
        var response = await httpClient.GetAsync(new Uri("/weatherforecast", UriKind.Relative));
        var responseData = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Date", responseData);
        Assert.Contains("TemperatureC", responseData);
        Assert.Contains("TemperatureF", responseData);
        Assert.Contains("Summary", responseData);
    }
}

public class WeatherForecastTests
{
    [Fact]
    public void TemperatureF_CalculatesCorrectly()
    {
        // Arrange
        var weatherForecast = new WeatherForecast(DateOnly.FromDateTime(DateTime.Now), 0, "Freezing");

        // Act
        var temperatureF = weatherForecast.TemperatureF;

        // Assert
        Assert.Equal(32, temperatureF);
    }

    [Fact]
    public void TemperatureF_CalculatesCorrectlyForNegativeTemperature()
    {
        // Arrange
        var weatherForecast = new WeatherForecast(DateOnly.FromDateTime(DateTime.Now), -10, "Freezing");

        // Act
        var temperatureF = weatherForecast.TemperatureF;

        // Assert
        Assert.Equal(14, temperatureF);
    }

    [Fact]
    public void TemperatureF_CalculatesCorrectlyForPositiveTemperature()
    {
        // Arrange
        var weatherForecast = new WeatherForecast(DateOnly.FromDateTime(DateTime.Now), 25, "Warm");

        // Act
        var temperatureF = weatherForecast.TemperatureF;

        // Assert
        Assert.Equal(77, temperatureF);
    }
}
