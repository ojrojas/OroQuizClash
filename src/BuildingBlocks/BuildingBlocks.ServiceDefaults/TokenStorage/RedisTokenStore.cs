namespace BuildingBlocks.ServiceDefaults.TokenStorage;

/// <summary>
/// Stores OIDC tokens in a distributed cache (Redis via IDistributedCache) keyed
/// by a session id, so BFF auth cookies stay small and never inflate request
/// headers. Tokens carry TTLs so the cache does not grow indefinitely.
/// </summary>
public sealed class RedisTokenStore(IDistributedCache cache)
{
    public const string AccessKeyPrefix = "oidc";
    public const string AccessKeySuffix = "access";
    public const string RefreshKeySuffix = "refresh";
    public const string ExpiryKeySuffix = "expires";

    public static string AccessKey(string sid) => $"{AccessKeyPrefix}:{sid}:{AccessKeySuffix}";
    public static string RefreshKey(string sid) => $"{AccessKeyPrefix}:{sid}:{RefreshKeySuffix}";
    public static string ExpiryKey(string sid) => $"{AccessKeyPrefix}:{sid}:{ExpiryKeySuffix}";

    public async Task SaveAsync(string sid, string accessToken, string? refreshToken,
        DateTimeOffset? expiresUtc, CancellationToken ct = default)
    {
        var accessTtl = expiresUtc.HasValue
            ? expiresUtc.Value - DateTimeOffset.UtcNow
            : TimeSpan.FromHours(1);
        if (accessTtl <= TimeSpan.Zero) accessTtl = TimeSpan.FromMinutes(5);

        await cache.SetStringAsync(AccessKey(sid), accessToken, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = accessTtl
        }, ct);

        if (!string.IsNullOrEmpty(refreshToken))
        {
            // Refresh token lives longer (7 days) so it can be exchanged for a new
            // access token without forcing the user to re-authenticate.
            await cache.SetStringAsync(RefreshKey(sid), refreshToken, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
            }, ct);
        }

        if (expiresUtc.HasValue)
        {
            await cache.SetStringAsync(ExpiryKey(sid), expiresUtc.Value.ToString("O"),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = accessTtl }, ct);
        }
    }

    public Task<string?> GetAccessTokenAsync(string sid, CancellationToken ct = default) =>
        cache.GetStringAsync(AccessKey(sid), ct);

    public Task<string?> GetRefreshTokenAsync(string sid, CancellationToken ct = default) =>
        cache.GetStringAsync(RefreshKey(sid), ct);

    public async Task<DateTimeOffset?> GetExpiryAsync(string sid, CancellationToken ct = default)
    {
        var value = await cache.GetStringAsync(ExpiryKey(sid), ct);
        return DateTimeOffset.TryParse(value, out var expiry) ? expiry : null;
    }

    public async Task RemoveAsync(string sid, CancellationToken ct = default)
    {
        await cache.RemoveAsync(AccessKey(sid), ct);
        await cache.RemoveAsync(RefreshKey(sid), ct);
        await cache.RemoveAsync(ExpiryKey(sid), ct);
    }
}

public static class TokenStorageServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Redis-backed token store for BFF apps. The Redis connection
    /// string is read from configuration ("ConnectionStrings:redis" — Aspire
    /// provides it via WithReference(redis)).
    /// </summary>
    public static IServiceCollection AddRedisTokenStore(this IServiceCollection services)
    {
        services.AddStackExchangeRedisCache(options =>
        {
            // Resolve lazily so Aspire/service defaults config applies at build time.
        });

        // Configure the connection from IConfiguration explicitly (Aspire injects
        // ConnectionStrings__redis into the host's configuration).
        services.AddOptions<Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                options.Configuration = config.GetConnectionString("redis");
            });

        services.TryAddScoped<RedisTokenStore>();

        return services;
    }
}