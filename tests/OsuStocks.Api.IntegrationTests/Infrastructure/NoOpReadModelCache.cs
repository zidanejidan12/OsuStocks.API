using OsuStocks.Application.Common.Caching;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Pass-through <see cref="IReadModelCache"/> for integration tests: always invokes the factory and
/// never caches. This keeps cached endpoints (leaderboards, trending) deterministic — every request
/// hits the per-test Testcontainer database — and removes any dependency on a shared Redis instance
/// (which would otherwise risk stale cross-test / cross-run results under the cache TTL).
/// </summary>
internal sealed class NoOpReadModelCache : IReadModelCache
{
    public Task<T> GetOrSetAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default)
    {
        return factory(cancellationToken);
    }
}
