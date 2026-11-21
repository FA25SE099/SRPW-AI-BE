using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace RiceProduction.Infrastructure.Repository
{
    public class DecoratorGenericRepository<T> : IGenericRepository<T>
        where T : BaseAuditableEntity
    {
        private const string CachePrefix = "RiceEntity";
        private static readonly string EntityName = typeof(T).Name;
        private static readonly string ListAllCacheKey = $"{CachePrefix}_{EntityName}_ListAll";

        private readonly IGenericRepository<T> _repository;
        private readonly IMemoryCache _cache;
        private readonly ILogger<DecoratorGenericRepository<T>> _logger;
        private readonly MemoryCacheEntryOptions _cacheOptions;

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
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            };
        }

        public Task<Guid> GenerateNewGuid(Guid guidInput) => _repository.GenerateNewGuid(guidInput);

        public async Task AddAsync(T entity)
        {
            await _repository.AddAsync(entity);
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
            InvalidateEntityCaches(entities);
        }

        public void DeleteRange(IEnumerable<T> entities)
        {
            _repository.DeleteRange(entities);
            InvalidateEntityCaches(entities);
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

        public Task<int> CountAsync() => _repository.CountAsync();

        public T? Delete(T entityToDelete)
        {
            var deleted = _repository.Delete(entityToDelete);
            InvalidateEntityCaches(entityToDelete.Id);
            return deleted;
        }

        public T? Delete(object id)
        {
            var idGuid = (Guid)id;
            var deleted = _repository.Delete(id);
            InvalidateEntityCaches(idGuid);
            return deleted;
        }

        public void Update(T entity)
        {
            _repository.Update(entity);
            InvalidateEntityCaches(entity.Id);
        }

        public Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate) => _repository.ExistsAsync(predicate);

        public Task<T?> FindAsync(Expression<Func<T, bool>> match) => _repository.FindAsync(match);

        public Task<T?> FindAsync(
            Expression<Func<T, bool>> match,
            Func<IQueryable<T>, IIncludableQueryable<T, object>>? includeProperties = null) =>
            _repository.FindAsync(match, includeProperties);

        public IQueryable<T?> GetQueryable() => throw new NotImplementedException();

        public async Task<T?> GetEntityByIdAsync(Guid id)
        {
            return await _cache.GetOrCreateAsync(GetByIdCacheKey(id), async entry =>
            {
                entry.SetOptions(_cacheOptions);
                _logger.LogInformation("Cache miss for {EntityType} ID {Id}; fetching from DB", EntityName, id);
                return await _repository.GetEntityByIdAsync(id);
            });
        }

        public async Task<IReadOnlyList<T>> ListAllAsync()
        {
            return await _cache.GetOrCreateAsync(ListAllCacheKey, async entry =>
            {
                entry.SetOptions(_cacheOptions);
                _logger.LogInformation("Cache miss for {EntityType} ListAll; fetching from DB", EntityName);
                return await _repository.ListAllAsync();
            });
        }

        public Task<IReadOnlyList<T>> ListAsync(
            Expression<Func<T, bool>>? filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            Func<IQueryable<T>, IIncludableQueryable<T, object>>? includeProperties = null) =>
            _repository.ListAsync(filter, orderBy, includeProperties);

        public async Task<int> SaveChangesAsync()
        {
            var changes = await _repository.SaveChangesAsync();
            InvalidateListCache();
            _logger.LogInformation("Saved {ChangeCount} changes for {EntityType}; invalidated list cache", changes, EntityName);
            return changes;
        }

        private static string GetByIdCacheKey(Guid id) => $"{CachePrefix}_{EntityName}_{id}";

        private void InvalidateEntityCaches(Guid id)
        {
            InvalidateByIdCache(id);
            InvalidateListCache();
        }

        private void InvalidateEntityCaches(IEnumerable<T> entities)
        {
            foreach (var entity in entities)
            {
                InvalidateByIdCache(entity.Id);
            }

            InvalidateListCache();
        }

        private void InvalidateByIdCache(Guid id)
        {
            _cache.Remove(GetByIdCacheKey(id));
            _logger.LogDebug("Invalidated cache for {EntityType} ID {Id}", EntityName, id);
        }

        private void InvalidateListCache()
        {
            _cache.Remove(ListAllCacheKey);
            _logger.LogDebug("Invalidated list cache for {EntityType}", EntityName);
        }
    }
}