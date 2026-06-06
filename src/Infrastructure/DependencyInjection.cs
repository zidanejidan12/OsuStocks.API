using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Domain.Market.Interfaces;
using OsuStocks.Domain.Repositories;
using OsuStocks.Infrastructure.Authentication;
using OsuStocks.Infrastructure.Market;
using OsuStocks.Infrastructure.Market.Options;
using OsuStocks.Infrastructure.BackgroundJobs;
using OsuStocks.Infrastructure.OsuIntegration.Api;
using OsuStocks.Infrastructure.OsuIntegration.OAuth;
using OsuStocks.Infrastructure.OsuIntegration.Options;
using OsuStocks.Infrastructure.OsuIntegration.Tokens;
using OsuStocks.Infrastructure.Persistence;
using OsuStocks.Infrastructure.Persistence.Repositories;
using OsuStocks.Infrastructure.Security;
using System.Net.Http.Headers;

namespace OsuStocks.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool addHangfireServer = false)
    {
        var postgresConnection = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        var redisConnection = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Connection string 'Redis' is not configured.");

        services.Configure<OsuOAuthOptions>(configuration.GetSection(OsuOAuthOptions.SectionName));
        services.Configure<OsuApiOptions>(configuration.GetSection(OsuApiOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<MarketEngineOptions>(configuration.GetSection(MarketEngineOptions.SectionName));
        services.Configure<OAuthReturnUrlOptions>(configuration.GetSection(OAuthReturnUrlOptions.SectionName));

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(postgresConnection, npgsqlOptions =>
                npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<IWalletTransactionRepository, WalletTransactionRepository>();
        services.AddScoped<ITrackedPlayerRepository, TrackedPlayerRepository>();
        services.AddScoped<IPlayerStockRepository, PlayerStockRepository>();
        services.AddScoped<IStockPriceHistoryRepository, StockPriceHistoryRepository>();
        services.AddScoped<IPortfolioRepository, PortfolioRepository>();
        services.AddScoped<IPortfolioReadRepository, PortfolioReadRepository>();
        services.AddScoped<IHoldingRepository, HoldingRepository>();
        services.AddScoped<ITradeRepository, TradeRepository>();
        services.AddScoped<ITradeReadRepository, TradeReadRepository>();
        services.AddScoped<IPlayerSnapshotRepository, PlayerSnapshotRepository>();
        services.AddScoped<IMarketEventRepository, MarketEventRepository>();
        services.AddScoped<IMarketReadRepository, MarketReadRepository>();
        services.AddScoped<IMarketSettingsRepository, MarketSettingsRepository>();

        services.AddScoped<IMarketCoefficientsProvider, MarketCoefficientsProvider>();
        services.AddScoped<IMarketPriceEngine, OsuStocks.Domain.Market.Services.MarketPriceEngine>();

        services.AddScoped<IOsuTokenManager, DistributedOsuTokenManager>();
        services.AddScoped<IAppTokenService, JwtAppTokenService>();
        services.AddSingleton<IOAuthReturnUrlPolicy, OAuthReturnUrlPolicy>();

        services.AddScoped<OsuSynchronizationRecurringJob>();
        services.AddSingleton<IOsuSynchronizationRecurringJobRegistrar, OsuSynchronizationRecurringJobRegistrar>();

        services.AddHttpClient<IOsuOAuthService, OsuOAuthService>();

        services.AddHttpClient<IOsuApiClient, OsuApiClient>((serviceProvider, client) =>
        {
            var osuApiOptions = serviceProvider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<OsuApiOptions>>()
                .Value;

            var baseUrl = osuApiOptions.BaseUrl;
            if (!baseUrl.EndsWith('/'))
            {
                baseUrl += "/";
            }

            client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
        });

        services.AddHangfire(config =>
        {
            config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180);
            config.UseSimpleAssemblyNameTypeSerializer();
            config.UseRecommendedSerializerSettings();
            config.UsePostgreSqlStorage(storageOptions => storageOptions.UseNpgsqlConnection(postgresConnection));
        });

        if (addHangfireServer)
        {
            services.AddHangfireServer();
        }

        return services;
    }
}

