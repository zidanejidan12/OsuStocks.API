using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class InMemoryMarketSettingsRepository : IMarketSettingsRepository
{
    private readonly object _gate = new();
    private MarketSettings? _current;

    public Task<MarketSettings?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(Clone(_current));
        }
    }

    public Task<MarketSettings?> GetCurrentForUpdateAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(Clone(_current));
        }
    }

    public Task AddAsync(MarketSettings settings, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _current = Clone(settings);
        }

        return Task.CompletedTask;
    }

    public void Update(MarketSettings settings)
    {
        lock (_gate)
        {
            _current = Clone(settings);
        }
    }

    public void Seed(MarketSettings settings)
    {
        lock (_gate)
        {
            _current = Clone(settings);
        }
    }

    private static MarketSettings? Clone(MarketSettings? source)
    {
        if (source is null)
        {
            return null;
        }

        return new MarketSettings
        {
            Id = source.Id,
            PpMultiplier = source.PpMultiplier,
            TradeMultiplier = source.TradeMultiplier,
            DecayMultiplier = source.DecayMultiplier,
            CreatedAt = source.CreatedAt,
            CreatedBy = source.CreatedBy,
            UpdatedAt = source.UpdatedAt,
            UpdatedBy = source.UpdatedBy
        };
    }
}
