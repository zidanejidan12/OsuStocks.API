using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Infrastructure.Persistence;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class PostgresWebApplicationFactory(
    PostgresTestcontainerFixture fixture,
    QueryCountingCommandInterceptor? queryCounter = null)
    : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"osu_stocks_it_{Guid.NewGuid():N}";
    private bool _databaseCreated;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
        });

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var inMemorySettings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = fixture.BuildDatabaseConnectionString(_databaseName),
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["Jwt:Issuer"] = "osu-stocks-test",
                ["Jwt:Audience"] = "osu-stocks-test-client",
                ["Jwt:SigningKey"] = "test-signing-key-that-is-at-least-32-characters-long",
                ["Jwt:ExpirationMinutes"] = "120",
                ["OsuOAuth:ClientId"] = "test-client-id",
                ["OsuOAuth:ClientSecret"] = "test-client-secret",
                ["OsuOAuth:RedirectUri"] = "http://localhost/api/v1/auth/callback",
                ["OsuOAuth:AuthorizationEndpoint"] = "https://osu.ppy.sh/oauth/authorize",
                ["OsuOAuth:TokenEndpoint"] = "https://osu.ppy.sh/oauth/token",
                ["OsuOAuth:Scopes:0"] = "public",
                ["OsuOAuth:Scopes:1"] = "identify",
                ["OsuApi:BaseUrl"] = "https://osu.ppy.sh/api/v2/",
                ["Security:EnableSwagger"] = "false"
            };

            configBuilder.AddInMemoryCollection(inMemorySettings);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IOsuTokenManager>();
            services.AddSingleton<InMemoryOsuTokenManager>();
            services.AddSingleton<IOsuTokenManager>(provider => provider.GetRequiredService<InMemoryOsuTokenManager>());

            services.RemoveAll<IOsuOAuthService>();
            services.RemoveAll<IOsuApiClient>();
            services.AddSingleton<IOsuOAuthService, FakeOsuOAuthService>();
            services.AddSingleton<IOsuApiClient, FakeOsuApiClient>();

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ =>
            {
            });

            if (queryCounter is null)
            {
                return;
            }

            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IApplicationDbContext>();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(
                    fixture.BuildDatabaseConnectionString(_databaseName),
                    npgsqlOptions => npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
                options.AddInterceptors(queryCounter);
            });

            services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        EnsureDatabaseCreated();

        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.Migrate();

        return host;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        if (!_databaseCreated)
        {
            return;
        }

        await fixture.DropDatabaseAsync(_databaseName);
    }

    private void EnsureDatabaseCreated()
    {
        if (_databaseCreated)
        {
            return;
        }

        fixture.CreateDatabaseAsync(_databaseName).GetAwaiter().GetResult();
        _databaseCreated = true;
    }
}
