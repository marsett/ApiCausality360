using Microsoft.Extensions.Caching.Memory;

namespace ApiCausality360.Services
{
    public interface ICacheService
    {
        T? Get<T>(string key);
        void Set<T>(string key, T value, TimeSpan? expiration = null);
        void Remove(string key);
        bool TryGet<T>(string key, out T? value);
    }

    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<CacheService> _logger;

        public CacheService(IMemoryCache cache, ILogger<CacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public T? Get<T>(string key)
        {
            return _cache.TryGetValue(key, out var value) ? (T)value : default;
        }

        public void Set<T>(string key, T value, TimeSpan? expiration = null)
        {
            var options = new MemoryCacheEntryOptions();
            
            if (expiration.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = expiration;
            }
            else
            {
                // Caché hasta las 6 AM del día siguiente
                var tomorrow6AM = DateTime.Today.AddDays(1).AddHours(6);
                options.AbsoluteExpiration = tomorrow6AM;
            }

            _cache.Set(key, value, options);
            _logger.LogInformation($" Cached: {key}");
        }

        public void Remove(string key)
        {
            _cache.Remove(key);
            _logger.LogInformation($" Removed cache: {key}");
        }

        public bool TryGet<T>(string key, out T? value)
        {
            if (_cache.TryGetValue(key, out var cachedValue))
            {
                value = (T)cachedValue;
                _logger.LogInformation($" Cache HIT: {key}");
                return true;
            }

            value = default;
            _logger.LogInformation($" Cache MISS: {key}");
            return false;
        }
    }
}