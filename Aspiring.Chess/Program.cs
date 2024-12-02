using Aspiring.Chess;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MongoDB.Driver;
using StackExchange.Redis;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string>
{
    ["Local:ClientId"] = "Aspiring.Chess",
    ["Local:Client"] = "Aspiring.Chess",
    ["Local:Authority"] = "https://localhost:5001",
    ["Local:RedirectUri"] = "https://localhost:5001/authentication/login-callback",
    ["Local:PostLogoutRedirectUri"] = "https://localhost:5001/authentication/logout-callback",
    ["Local:ResponseType"] = "code",
    ["Local:Scope"] = "openid profile email",
    ["Local:UsePkce"] = "true",
    ["Local:MetadataAddress"] = "https://localhost:5001/.well-known/openid-configuration",
    ["Local:RequireHttpsMetadata"] = "false",
    ["Local:ClientSecret"] = "secret"
});

builder.Services.AddSingleton<IMongoClient, MongoClient>(sp =>
{
    var settings = MongoClientSettings.FromConnectionString("mongodb://localhost:27017");
    return new MongoClient(settings);
});

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse("localhost:6379", true);
    return ConnectionMultiplexer.Connect(configuration);
});

//builder.Services.AddOidcAuthentication(options =>
//{
//    // Configure your authentication provider options here.
//    // For more information, see https://aka.ms/blazor-standalone-auth
//    builder.Configuration.Bind("Local", options.ProviderOptions);
//    builder.Configuration.Bind("Local", options.UserOptions);
//});

await builder.Build().RunAsync();
