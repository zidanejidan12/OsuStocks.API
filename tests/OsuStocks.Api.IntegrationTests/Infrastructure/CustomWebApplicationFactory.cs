using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var inMemorySettings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Port=5432;Database=osu_stocks_test;Username=postgres;Password=postgres",
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
                ["OsuApi:BaseUrl"] = "https://osu.ppy.sh/api/v2/"
            };

            configBuilder.AddInMemoryCollection(inMemorySettings);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ITrackedPlayerRepository>();
            services.AddSingleton<InMemoryTrackedPlayerRepository>();
            services.AddSingleton<ITrackedPlayerRepository>(provider => provider.GetRequiredService<InMemoryTrackedPlayerRepository>());

            services.RemoveAll<IUserRepository>();
            services.AddSingleton<InMemoryUserRepository>();
            services.AddSingleton<IUserRepository>(provider => provider.GetRequiredService<InMemoryUserRepository>());

            services.RemoveAll<IWalletRepository>();
            services.AddSingleton<InMemoryWalletRepository>();
            services.AddSingleton<IWalletRepository>(provider => provider.GetRequiredService<InMemoryWalletRepository>());

            services.RemoveAll<IWalletTransactionRepository>();
            services.AddSingleton<InMemoryWalletTransactionRepository>();
            services.AddSingleton<IWalletTransactionRepository>(provider => provider.GetRequiredService<InMemoryWalletTransactionRepository>());

            services.RemoveAll<IPortfolioRepository>();
            services.AddSingleton<InMemoryPortfolioRepository>();
            services.AddSingleton<IPortfolioRepository>(provider => provider.GetRequiredService<InMemoryPortfolioRepository>());

            services.RemoveAll<IPlayerStockRepository>();
            services.AddSingleton<InMemoryPlayerStockRepository>();
            services.AddSingleton<IPlayerStockRepository>(provider => provider.GetRequiredService<InMemoryPlayerStockRepository>());

            services.RemoveAll<IHoldingRepository>();
            services.AddSingleton<InMemoryHoldingRepository>();
            services.AddSingleton<IHoldingRepository>(provider => provider.GetRequiredService<InMemoryHoldingRepository>());

            services.RemoveAll<ITradeRepository>();
            services.AddSingleton<InMemoryTradeRepository>();
            services.AddSingleton<ITradeRepository>(provider => provider.GetRequiredService<InMemoryTradeRepository>());

            services.RemoveAll<IOsuTokenManager>();
            services.AddSingleton<InMemoryOsuTokenManager>();
            services.AddSingleton<IOsuTokenManager>(provider => provider.GetRequiredService<InMemoryOsuTokenManager>());

            services.RemoveAll<IApplicationDbContext>();
            services.AddScoped<IApplicationDbContext, NoOpApplicationDbContext>();

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
        });
    }
}
