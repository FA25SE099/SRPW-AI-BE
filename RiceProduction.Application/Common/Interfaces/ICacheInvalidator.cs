namespace RiceProduction.Application.Common.Interfaces;

public interface ICacheInvalidator
{
    void InvalidateCache(string cacheKey);
    void InvalidateCaches(params string[] cacheKeys);
    void InvalidateCachesByPattern(string pattern);
    void ClearAllCache();
}
