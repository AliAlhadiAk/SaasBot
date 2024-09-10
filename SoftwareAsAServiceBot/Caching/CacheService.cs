using StackExchange.Redis;
using System.Text.Json;

namespace SoftwareAsAServiceBot.Caching
{
    public class CacheService : ICacheService
    {
        private readonly IDatabase _cachedb;

        public CacheService(IConnectionMultiplexer connectionMultiplexer)
        {
            _cachedb = connectionMultiplexer.GetDatabase();
        }

        public async Task<T> GetData<T>(string key)
        {
            try
            {
                var value = await _cachedb.StringGetAsync(key);
                if (!value.IsNullOrEmpty)
                {
                    return value.HasValue ? JsonSerializer.Deserialize<T>(value) : default;
                }
                return default;
            }
            catch (RedisConnectionException ex)
            {
                throw new Exception("Error retrieving data from cache.", ex);
            }
        }

        public async Task<bool> SetData<T>(string key, T value, TimeSpan expirationTime)
        {
            try
            {
                var adjustedExpiration = expirationTime.Add(TimeSpan.FromMinutes(2));
                return await _cachedb.StringSetAsync(key, JsonSerializer.Serialize(value), adjustedExpiration);
            }
            catch (RedisConnectionException ex)
            {
                throw new Exception("Error setting data in cache.", ex);
            }
        }

        public async Task<bool> UpdateCacheIfExists<T>(string key, T value, TimeSpan expirationTime)
        {
            try
            {
                var exists = await _cachedb.KeyExistsAsync(key);
                if (exists)
                {
                    var serializedValue = JsonSerializer.Serialize(value);
                    return await _cachedb.StringSetAsync(key, serializedValue, expirationTime);
                }
                return false;
            }
            catch (RedisConnectionException ex)
            {
                throw new Exception("Error updating data in cache.", ex);
            }
        }

        public async Task<object> RemoveData(string key)
        {
            try
            {
                var exists = await _cachedb.KeyExistsAsync(key);
                if (exists)
                {
                    return await _cachedb.KeyDeleteAsync(key);
                }
                return false;
            }
            catch (RedisConnectionException ex)
            {
                throw new Exception("Error removing data from cache.", ex);
            }
        }
    }
}
