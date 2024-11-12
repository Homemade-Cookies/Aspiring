namespace Aspiring.Web;

internal sealed class WeatherApiClient(HttpClient httpClient)
{
    public async Task<WeatherForecast[]> GetWeatherAsync(int maxItems = 10, CancellationToken cancellationToken = default)
    {
        List<WeatherForecast>? forecasts = null;

        await foreach (var forecast in httpClient.GetFromJsonAsAsyncEnumerable<WeatherForecast>("/weatherforecast", cancellationToken))
        {
            if (forecasts?.Count >= maxItems)
            {
                break;
            }
            if (forecast is not null)
            {
                forecasts ??= [];
                forecasts.Add(forecast);
            }
        }

        return forecasts?.ToArray() ?? [];
    }
}

internal sealed record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public class WeatherApiClientTests
{
    [Fact]
    public async Task GetWeatherAsync_ReturnsCorrectNumberOfForecasts()
    {
        // Arrange
        var httpClient = new HttpClient(new MockHttpMessageHandler());
        var weatherApiClient = new WeatherApiClient(httpClient);

        // Act
        var forecasts = await weatherApiClient.GetWeatherAsync(maxItems: 5);

        // Assert
        Assert.Equal(5, forecasts.Length);
    }

    [Fact]
    public async Task GetWeatherAsync_ReturnsEmptyArrayWhenNoForecasts()
    {
        // Arrange
        var httpClient = new HttpClient(new MockHttpMessageHandler(returnEmpty: true));
        var weatherApiClient = new WeatherApiClient(httpClient);

        // Act
        var forecasts = await weatherApiClient.GetWeatherAsync();

        // Assert
        Assert.Empty(forecasts);
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly bool _returnEmpty;

        public MockHttpMessageHandler(bool returnEmpty = false)
        {
            _returnEmpty = returnEmpty;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(_returnEmpty ? "[]" : "[{\"Date\":\"2023-01-01\",\"TemperatureC\":0,\"Summary\":\"Freezing\"}]")
            };

            return Task.FromResult(response);
        }
    }
}
