using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Query;

namespace RiceProduction.Infrastructure.Repository
{
    public class DecoratorGenericRepository<T> : IGenericRepository<T>
        where T : BaseAuditableEntity
    {
        private readonly IGenericRepository<T> _repository;
        private readonly IMemoryCache _cache;
        private readonly ILogger<DecoratorGenericRepository<T>> _logger;
        private readonly MemoryCacheEntryOptions _cacheOptions;

        private const string CachePrefix = "RiceEntity"; // Prefix for your app/domain

        public DecoratorGenericRepository(
            IGenericRepository<T> repository,
            IMemoryCache cache,
            ILogger<DecoratorGenericRepository<T>> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) // Adjust as needed
                // SlidingExpiration = TimeSpan.FromMinutes(2), // Uncomment for sliding
            };
        }

        public async Task<Guid> GenerateNewGuid(Guid guidInput)
        {
            // Always uncached for uniqueness checks
            return await _repository.GenerateNewGuid(guidInput);
        }

        public async Task AddAsync(T entity)
        {
            await _repository.AddAsync(entity);
            // Invalidate list (new item will be in next refresh)
            InvalidateListCache();
        }

        public async Task AddRangeAsync(IEnumerable<T> entities)
        {
            await _repository.AddRangeAsync(entities);
            InvalidateListCache();
        }

        public void UpdateRange(IEnumerable<T> entities)
        {
            _repository.UpdateRange(entities);
            foreach (var entity in entities)
            {
                InvalidateByIdCache(entity.Id);
            }
            InvalidateListCache();
        }

        public void DeleteRange(IEnumerable<T> entities)
        {
            _repository.DeleteRange(entities);
            foreach (var entity in entities)
            {
                InvalidateByIdCache(entity.Id);
            }
            InvalidateListCache();
        }

        public async Task<bool> DeleteRangeAsync(Expression<Func<T, bool>> predicate)
        {
            var result = await _repository.DeleteRangeAsync(predicate);
            if (result)
            {
                InvalidateListCache();
            }
            return result;
        }

        public async Task<int> CountAsync()
        {
            // Uncached for accuracy
            return await _repository.CountAsync();
        }

        public T? Delete(T entityToDelete)
        {
            var deleted = _repository.Delete(entityToDelete);
            InvalidateByIdCache(entityToDelete.Id);
            InvalidateListCache();
            return deleted;
        }

        public T? Delete(object id)
        {
            var idGuid = (Guid)id; // Assuming Guid; cast as needed
            var deleted = _repository.Delete(id);
            InvalidateByIdCache(idGuid);
            InvalidateListCache();
            return deleted;
        }

        public void Update(T entity)
        {
            _repository.Update(entity);
            InvalidateByIdCache(entity.Id);
            InvalidateListCache();
        }

        public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
        {
            // Uncached to avoid false positives from stale data
            return await _repository.ExistsAsync(predicate);
        }

        public async Task<T?> FindAsync(Expression<Func<T, bool>> match)
        {
            // Uncached due to variable match (extend with key gen if needed)
            return await _repository.FindAsync(match);
        }

        public async Task<T?> FindAsync(
            Expression<Func<T, bool>> match,
            Func<IQueryable<T>, IIncludableQueryable<T, object>>? includeProperties = null)
        {
            // Uncached due to variable match/include
            return await _repository.FindAsync(match, includeProperties);
        }

        public async Task<T?> GetEntityByIdAsync(Guid id)
        {
            var key = $"{CachePrefix}_{typeof(T).Name}_{id}";
            return await _cache.GetOrCreateAsync(key, async entry =>
            {
                entry.SetOptions(_cacheOptions);
                _logger.LogInformation("Cache miss for {EntityType} ID {Id}; fetching from DB", typeof(T).Name, id);
                return await _repository.GetEntityByIdAsync(id);
            });
        }

        public async Task<IReadOnlyList<T>> ListAllAsync()
        {
            var key = $"{CachePrefix}_{typeof(T).Name}_ListAll";
            return await _cache.GetOrCreateAsync(key, async entry =>
            {
                entry.SetOptions(_cacheOptions);
                _logger.LogInformation("Cache miss for {EntityType} ListAll; fetching from DB", typeof(T).Name);
                return await _repository.ListAllAsync();
            });
        }

        public async Task<IReadOnlyList<T>> ListAsync(
            Expression<Func<T, bool>>? filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            Func<IQueryable<T>, IIncludableQueryable<T, object>>? includeProperties = null)
        {
            // Uncached due to variable params (simple case: if all null, could cache as ListAll)
            return await _repository.ListAsync(filter, orderBy, includeProperties);
        }

        public async Task<int> SaveChangesAsync()
        {
            var changes = await _repository.SaveChangesAsync();
            // Invalidate after save (covers any pending adds/updates/deletes)
            InvalidateListCache();
            _logger.LogInformation("Saved {ChangeCount} changes for {EntityType}; invalidated list cache", changes, typeof(T).Name);
            return changes;
        }

        private void InvalidateByIdCache(Guid id)
        {
            var key = $"{CachePrefix}_{typeof(T).Name}_{id}";
            _cache.Remove(key);
            _logger.LogDebug("Invalidated cache for {EntityType} ID {Id}", typeof(T).Name, id);
        }

        private void InvalidateListCache()
        {
            var key = $"{CachePrefix}_{typeof(T).Name}_ListAll";
            _cache.Remove(key);
            _logger.LogDebug("Invalidated list cache for {EntityType}", typeof(T).Name);
        }
    }
}