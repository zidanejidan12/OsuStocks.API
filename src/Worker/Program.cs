using Microsoft.Extensions.DependencyInjection;
using OsuStocks.Application;
using OsuStocks.Infrastructure;
using OsuStocks.Infrastructure.BackgroundJobs;

var builder = Host.CreateApplicationBuilder(args);

ValidateProductionSecretEnvironmentVariables(builder.Configuration, builder.Environment);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, addHangfireServer: true);

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var registrar = scope.ServiceProvider.GetRequiredService<IOsuSynchronizationRecurringJobRegistrar>();
    registrar.Register();
}

host.Run();

static void ValidateProductionSecretEnvironmentVariables(IConfiguration configuration, IHostEnvironment environment)
{
    if (!environment.IsProduction())
    {
        return;
    }

    string[] requiredEnvironmentVariables =
    [
        "ConnectionStrings__Postgres",
        "ConnectionStrings__Redis",
        "OsuOAuth__ClientId",
        "OsuOAuth__ClientSecret",
        "Jwt__Issuer",
        "Jwt__Audience",
        "Jwt__SigningKey"
    ];

    var missingVariables = requiredEnvironmentVariables
        .Where(static key => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
        .ToArray();

    if (missingVariables.Length > 0)
    {
        throw new InvalidOperationException(
            "Production requires secrets via environment variables. Missing: " + string.Join(", ", missingVariables));
    }

    EnsureNotPlaceholder("ConnectionStrings:Postgres", configuration.GetConnectionString("Postgres"));
    EnsureNotPlaceholder("OsuOAuth:ClientSecret", configuration["OsuOAuth:ClientSecret"]);
    EnsureNotPlaceholder("Jwt:SigningKey", configuration["Jwt:SigningKey"]);
}

static void EnsureNotPlaceholder(string key, string? value)
{
    if (string.IsNullOrWhiteSpace(value) || value.Contains("replace-with-", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            $"Configuration '{key}' must be a non-placeholder value in production.");
    }
}
