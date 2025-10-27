using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using System.Text;
using System.Text.Json;

namespace YourApp.Application.Common.Behaviors;

public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICacheable
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    public CachingBehavior(IMemoryCache cache, ILogger<CachingBehavior<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request.BypassCache)
        {
            _logger.LogInformation("Bypassing cache for request: {RequestType}", typeof(TRequest).Name);
            return await next();
        }

        var cacheKey = request.CacheKey;
        if (string.IsNullOrEmpty(cacheKey))
        {
            _logger.LogWarning("Invalid cache key for {RequestType}; falling back to handler", typeof(TRequest).Name);
            return await next();
        }

        if (_cache.TryGetValue(cacheKey, out TResponse? cachedResponse))
        {
            _logger.LogInformation("Cache hit for key: {CacheKey}", cacheKey);
            return cachedResponse;
        }

        _logger.LogInformation("Cache miss for key: {CacheKey}; executing handler", cacheKey);
        var response = await next();

        if (response is not null)
        {
            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(request.SlidingExpirationInMinutes),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(request.AbsoluteExpirationInMinutes)
            };

            _cache.Set(cacheKey, response, cacheEntryOptions);
            _logger.LogInformation("Cached response for key: {CacheKey}", cacheKey);
        }

        return response;
    }
}