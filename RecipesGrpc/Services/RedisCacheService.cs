using System.Text.Json;
using StackExchange.Redis;

namespace RecipesGrpc.Services;

public class RedisCacheService
{
    private readonly IDatabase _cache;

    public RedisCacheService(IConfiguration configuration)
    {
        try
        {
            var redisConnection = configuration.GetValue<string>("Redis:ConnectionString");
            var redis = ConnectionMultiplexer.Connect(redisConnection);
            _cache = redis.GetDatabase();
        }
        catch (Exception ex)
        {
            // Логирование ошибки
            Console.WriteLine($"Ошибка подключения к Redis: {ex.Message}");
            throw;
        }
    }

    public async Task SetCacheAsync<T>(string key, T value, TimeSpan expiry)
    {
        var json = JsonSerializer.Serialize(value);
        await _cache.StringSetAsync(key, json, expiry);

        Console.WriteLine($"Ключ {key} добавлен в кэш с истечением через {expiry.TotalMinutes} минут.");
    }

    public async Task<T?> GetCacheAsync<T>(string key)
    {
        var json = await _cache.StringGetAsync(key);

        if (!json.IsNullOrEmpty)
        {
            Console.WriteLine($"Ключ {key} найден в кэше.");
            return JsonSerializer.Deserialize<T>(json);
        }

        Console.WriteLine($"Ключ {key} не найден в кэше.");
        return default;
    }

    public async Task RemoveCacheAsync(string key)
    {
        await _cache.KeyDeleteAsync(key);
        Console.WriteLine($"Ключ {key} удален из кэша.");
    }
}