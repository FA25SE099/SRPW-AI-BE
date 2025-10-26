using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using System.Collections;
using System.Reflection;

namespace RiceProduction.Infrastructure.Services;

public class CacheInvalidator : ICacheInvalidator
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CacheInvalidator> _logger;

    public CacheInvalidator(IMemoryCache cache, ILogger<CacheInvalidator> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public void InvalidateCache(string cacheKey)
    {
        if (string.IsNullOrEmpty(cacheKey))
        {
            _logger.LogWarning("Attempted to invalidate cache with null or empty key");
            return;
        }

        _cache.Remove(cacheKey);
        _logger.LogInformation("Invalidated cache for key: {CacheKey}", cacheKey);
    }

    public void InvalidateCaches(params string[] cacheKeys)
    {
        foreach (var key in cacheKeys.Where(k => !string.IsNullOrEmpty(k)))
        {
            InvalidateCache(key);
        }
    }

    public void InvalidateCachesByPattern(string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            _logger.LogWarning("Attempted to invalidate cache with null or empty pattern");
            return;
        }

        var cacheKeys = GetAllCacheKeys();
        var matchingKeys = cacheKeys.Where(k => IsMatch(k, pattern)).ToList();

        foreach (var key in matchingKeys)
        {
            InvalidateCache(key);
        }

        _logger.LogInformation(
            "Invalidated {Count} cache entries matching pattern: {Pattern}", 
            matchingKeys.Count, pattern);
    }

    public void ClearAllCache()
    {
        var field = typeof(MemoryCache).GetField("_coherentState", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (field != null)
        {
            var coherentState = field.GetValue(_cache);
            if (coherentState != null)
            {
                var entriesField = coherentState.GetType().GetField("_entries", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (entriesField?.GetValue(coherentState) is IDictionary entries)
                {
                    var keys = entries.Keys.Cast<object>().ToList();
                    foreach (var key in keys)
                    {
                        _cache.Remove(key);
                    }
                    _logger.LogInformation("Cleared all cache entries. Count: {Count}", keys.Count);
                }
            }
        }
    }

    private List<string> GetAllCacheKeys()
    {
        var keys = new List<string>();
        
        var field = typeof(MemoryCache).GetField("_coherentState", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (field != null)
        {
            var coherentState = field.GetValue(_cache);
            if (coherentState != null)
            {
                var entriesField = coherentState.GetType().GetField("_entries", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (entriesField?.GetValue(coherentState) is IDictionary entries)
                {
                    keys.AddRange(entries.Keys.Cast<object>().Select(k => k.ToString() ?? string.Empty));
                }
            }
        }
        
        return keys;
    }

    private bool IsMatch(string key, string pattern)
    {
        if (pattern.EndsWith("*"))
        {
            return key.StartsWith(pattern.TrimEnd('*'), StringComparison.OrdinalIgnoreCase);
        }
        else if (pattern.StartsWith("*"))
        {
            return key.EndsWith(pattern.TrimStart('*'), StringComparison.OrdinalIgnoreCase);
        }
        else if (pattern.Contains("*"))
        {
            var parts = pattern.Split('*');
            return key.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase) 
                && key.EndsWith(parts[1], StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            return key.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }
    }
}
