using Aspiring.Web;
using Aspiring.Web.Components.Pages;
using Xunit;

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

public class WeatherComponentTests
{
    [Fact]
    public async Task WeatherComponent_DisplaysWeatherData()
    {
        // Arrange
        var weatherApiClient = new WeatherApiClient(new HttpClient(new MockHttpMessageHandler()));
        var weatherComponent = new Weather(weatherApiClient);

        // Act
        await weatherComponent.OnInitializedAsync();

        // Assert
        Assert.NotNull(weatherComponent.Forecasts);
        Assert.NotEmpty(weatherComponent.Forecasts);
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[{\"Date\":\"2023-01-01\",\"TemperatureC\":0,\"Summary\":\"Freezing\"}]")
            };

            return Task.FromResult(response);
        }
    }
}

public class CounterTests
{
    [Fact]
    public void IncrementCount_IncreasesCurrentCount()
    {
        // Arrange
        var counter = new Counter();

        // Act
        counter.IncrementCount();

        // Assert
        Assert.Equal(1, counter.CurrentCount);
    }

    [Fact]
    public void IncrementCount_IncreasesCurrentCountMultipleTimes()
    {
        // Arrange
        var counter = new Counter();

        // Act
        counter.IncrementCount();
        counter.IncrementCount();
        counter.IncrementCount();

        // Assert
        Assert.Equal(3, counter.CurrentCount);
    }
}
