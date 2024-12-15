namespace Aspiring.Web.Playwright.Tests;
public class PlaywrightTests : IAsyncLifetime
{
    private IPlaywright _playwright;
    private IBrowser _browser;

    public async Task InitializeAsync()
    {
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    [Fact]
    public async Task HomePage_ShouldLoadSuccessfully()
    {
        var page = await _browser.NewPageAsync();
        await page.GotoAsync("http://localhost:5280");
        var title = await page.TitleAsync();
        Assert.Equal("Home Page - Aspiring.Web", title);
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnHealthy()
    {
        var page = await _browser.NewPageAsync();
        await page.GotoAsync("http://localhost:5280/health");
        var content = await page.ContentAsync();
        Assert.Contains("Healthy", content, StringComparison.OrdinalIgnoreCase);
    }
}
