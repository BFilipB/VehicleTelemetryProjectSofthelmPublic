using Microsoft.Extensions.Caching.Memory;

namespace VehicleTelemetryAPI.Services;

/// <summary>
/// Cache service interface
/// Improvement: Added caching layer to reduce database queries
/// </summary>
public interface ICacheService
{
    T? Get<T>(string key);
    Task<T?> GetAsync<T>(string key);
    void Set<T>(string key, T value, TimeSpan? expiration = null);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);
    void Remove(string key);
    Task RemoveAsync(string key);
}

/// <summary>
/// In-memory cache service implementation
/// </summary>
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryCacheService> _logger;
    private const int DEFAULT_EXPIRATION_MINUTES = 5;

    public MemoryCacheService(IMemoryCache cache, ILogger<MemoryCacheService> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public T? Get<T>(string key)
    {
        try
        {
            return _cache.TryGetValue(key, out T? value) ? value : default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache get error for key: {Key}", key);
            return default;
        }
    }

    public Task<T?> GetAsync<T>(string key)
    {
        return Task.FromResult(Get<T>(key));
    }

    public void Set<T>(string key, T value, TimeSpan? expiration = null)
    {
        try
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(DEFAULT_EXPIRATION_MINUTES)
            };

            _cache.Set(key, value, cacheOptions);
            _logger.LogDebug("Cache set for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache set error for key: {Key}", key);
        }
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        Set(key, value, expiration);
        return Task.CompletedTask;
    }

    public void Remove(string key)
    {
        try
        {
            _cache.Remove(key);
            _logger.LogDebug("Cache removed for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache remove error for key: {Key}", key);
        }
    }

    public Task RemoveAsync(string key)
    {
        Remove(key);
        return Task.CompletedTask;
    }
}
