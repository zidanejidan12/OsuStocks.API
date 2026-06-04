using OsuStocks.Application.Common.Interfaces;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class NoOpApplicationDbContext : IApplicationDbContext
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(1);
    }
}
