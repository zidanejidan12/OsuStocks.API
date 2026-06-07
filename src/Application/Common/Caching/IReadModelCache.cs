namespace OsuStocks.Application.Common.Caching;

public interface IReadModelCache
{
    Task<T> GetOrSetAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default);
}
