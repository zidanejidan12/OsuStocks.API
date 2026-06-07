using Microsoft.Extensions.Caching.Distributed;
using OsuStocks.Application.Common.Caching;
using System.Text.Json;

namespace OsuStocks.Infrastructure.Caching;

internal sealed class RedisReadModelCache(IDistributedCache cache) : IReadModelCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<T> GetOrSetAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default)
    {
        var cached = await cache.GetStringAsync(key, cancellationToken);
        if (cached is not null)
        {
            var deserialized = JsonSerializer.Deserialize<T>(cached, SerializerOptions);
            if (deserialized is not null)
            {
                return deserialized;
            }
        }

        var value = await factory(cancellationToken);

        var payload = JsonSerializer.Serialize(value, SerializerOptions);

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        };

        await cache.SetStringAsync(key, payload, options, cancellationToken);

        return value;
    }
}
