using System.Text.Json;
using StackExchange.Redis;

public class RedisCacheService
{
    private readonly IDatabase _cache;

    public RedisCacheService(IConfiguration configuration)
    {
        var redisConnection = configuration.GetValue<string>("Redis:ConnectionString");
        var redis = ConnectionMultiplexer.Connect(redisConnection);
        _cache = redis.GetDatabase();
    }

    public async Task SetCacheAsync<T>(string key, T value, TimeSpan expiry)
    {
        var json = JsonSerializer.Serialize(value);
        await _cache.StringSetAsync(key, json, expiry);
    }

    public async Task<T?> GetCacheAsync<T>(string key)
    {
        var json = await _cache.StringGetAsync(key);
        return json.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>(json);
    }
}