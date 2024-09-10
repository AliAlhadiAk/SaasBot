namespace SoftwareAsAServiceBot.Caching
{
    public interface ICacheService
    {
        public Task<T> GetData<T>(string key);
        public Task<bool> SetData<T>(string key, T value, TimeSpan expirationTime);
        public Task<object> RemoveData(string key);
        public Task<bool> UpdateCacheIfExists<T>(string key, T value, TimeSpan expirationTime);

    }
}
