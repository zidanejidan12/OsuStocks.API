using OsuStocks.Domain.Entities;
using OsuStocks.Domain.Repositories;
using System.Collections.Concurrent;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class InMemoryPortfolioRepository : IPortfolioRepository
{
    private readonly ConcurrentDictionary<Guid, Portfolio> _portfoliosById = new();
    private readonly ConcurrentDictionary<Guid, Guid> _portfolioIdsByUserId = new();

    public int Count => _portfoliosById.Count;

    public Task<Portfolio?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _portfoliosById.TryGetValue(id, out var portfolio);
        return Task.FromResult(Clone(portfolio));
    }

    public Task<Portfolio?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!_portfolioIdsByUserId.TryGetValue(userId, out var portfolioId))
        {
            return Task.FromResult<Portfolio?>(null);
        }

        _portfoliosById.TryGetValue(portfolioId, out var portfolio);
        return Task.FromResult(Clone(portfolio));
    }

    public Task AddAsync(Portfolio portfolio, CancellationToken cancellationToken = default)
    {
        if (!_portfolioIdsByUserId.TryAdd(portfolio.UserId, portfolio.Id))
        {
            throw new InvalidOperationException($"Portfolio for user '{portfolio.UserId}' already exists.");
        }

        _portfoliosById[portfolio.Id] = Clone(portfolio)!;
        return Task.CompletedTask;
    }

    public void Update(Portfolio portfolio)
    {
        _portfolioIdsByUserId[portfolio.UserId] = portfolio.Id;
        _portfoliosById[portfolio.Id] = Clone(portfolio)!;
    }

    private static Portfolio? Clone(Portfolio? portfolio)
    {
        if (portfolio is null)
        {
            return null;
        }

        return new Portfolio
        {
            Id = portfolio.Id,
            UserId = portfolio.UserId,
            CreatedAt = portfolio.CreatedAt,
            CreatedBy = portfolio.CreatedBy,
            UpdatedAt = portfolio.UpdatedAt,
            UpdatedBy = portfolio.UpdatedBy
        };
    }
}
