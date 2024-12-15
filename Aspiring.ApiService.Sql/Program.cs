using System.Security.Cryptography;
using Aspiring.ApiService.Sql;
using Aspiring.ServiceDefaults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddAuthentication().AddBearerToken(IdentityConstants.BearerScheme);
builder.Services.AddAuthorizationBuilder();

builder.AddSqlServerDbContext<ApiContext>("sql-Database");

var requireConfirmedAccount = builder.Configuration.GetValue<bool>("RequireConfirmedAccount");
builder.Services.AddIdentityCore<UserAccount>(options => options.SignIn.RequireConfirmedAccount = requireConfirmedAccount)
                .AddEntityFrameworkStores<ApiContext>()
                .AddPasswordValidator<PasswordValidator<UserAccount>>()
                .AddApiEndpoints();

builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi

#if NET9_0_OR_GREATER
builder.Services.AddOpenApi();
#endif

var app = builder.Build();

app.MapDefaultEndpoints();

// Ensure the database is created and all migrations are applied
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApiContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Apply any pending migrations
    dbContext.Database.Migrate();

    // Check for pending migrations
    var pendingMigrations = dbContext.Database.GetPendingMigrations();
    if (pendingMigrations.Any())
    {
        var migrations = string.Join(", ", pendingMigrations);
        throw new DataMisalignedException($"There are pending migrations: {migrations}");
    }

    // Seed data
    if (!dbContext.WeatherForecasts.Any())
    {
        dbContext.WeatherForecasts.AddRange(
            new WeatherForecast(DateOnly.FromDateTime(DateTime.Now), 25, "Warm"),
            new WeatherForecast(DateOnly.FromDateTime(DateTime.Now.AddDays(1)), 28, "Hot")
        );
        dbContext.SaveChanges();
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
#if NET9_0_OR_GREATER
    app.MapOpenApi();
#endif
    app.UseDeveloperExceptionPage();
    app.UseHealthChecksUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapIdentityApi<UserAccount>();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            RandomNumberGenerator.GetInt32(-20, 55),
            summaries[RandomNumberGenerator.GetInt32(summaries.Length)]
        ))
        .ToArray();
    return forecast;
});
app.MapControllers();

app.Run();

internal sealed class UserAccount : IdentityUser
{
}

internal sealed class ApiContext(DbContextOptions<ApiContext> options)
    : IdentityDbContext<UserAccount>(options)
{
    public DbSet<WeatherForecast> WeatherForecasts { get; set; } = default!;
}
