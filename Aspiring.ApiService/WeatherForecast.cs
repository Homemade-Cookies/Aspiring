namespace Aspiring.ApiService;

internal sealed record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
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
