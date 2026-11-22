using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace RiceProduction.Infrastructure.Repository
{
    public class ClusterManagerRepository : IClusterManagerRepository
    {
        private readonly DbSet<ClusterManager> _clusterManager;

        public ClusterManagerRepository(ApplicationDbContext context)
        {
            _clusterManager = context.Set<ClusterManager>();
        }

        public void UpdateRange(IEnumerable<ClusterManager> entities)
        {
            _clusterManager.UpdateRange(entities);
        }

        public void Update(ClusterManager entity)
        {
            _clusterManager.Update(entity);
        }

        public async Task<bool> ExistAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _clusterManager.AnyAsync(f => f.Id == id, cancellationToken);
        }

        public async Task<IEnumerable<ClusterManager>> FindAsync(Expression<Func<ClusterManager, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await QueryWithDetails().ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<ClusterManager?>> GetAllClusterManagerAsync(CancellationToken cancellationToken = default)
        {
            return await QueryWithDetails().ToListAsync(cancellationToken);
        }

        public async Task<(IEnumerable<ClusterManager> Items, int TotalCount)> GetAllClusterManagerByNameOrEmailAndPhoneNumberPagingAsync(int pageNumber, int pageSize, string? search, string? phoneNumber, CancellationToken cancellationToken = default)
        {
            var query = QueryWithDetails();
            var totalCount = await query.CountAsync(cancellationToken);
            query = ApplySearchFilters(query, search, phoneNumber);

            var items = await ApplyOrderingAndPaging(query, pageNumber, pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public async Task<(IEnumerable<ClusterManager?> Items, int TotalCount)> GetAllClusterManagerAssignedOrNotByNameOrEmailAndPhoneNumberPagingAsync(int pageNumber, int pageSize, string? search, string? phoneNumber, bool? freeOrAssigned, CancellationToken cancellationToken = default)
        {
            var query = QueryWithDetails();
            var totalCount = await query.CountAsync(cancellationToken);

            if (freeOrAssigned.HasValue)
            {
                query = freeOrAssigned.Value
                    ? query.Where(s => !s.ClusterId.HasValue)
                    : query.Where(s => s.ClusterId.HasValue);
            }

            query = ApplySearchFilters(query, search, phoneNumber);

            var items = await ApplyOrderingAndPaging(query, pageNumber, pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public async Task<ClusterManager?> GetClusterManagerByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await QueryWithDetails()
                .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        }

        public async Task<ClusterManager?> GetClusterManagerByPhoneNumber(string phoneNumber, CancellationToken cancellationToken = default)
        {
            return await QueryWithDetails()
                .FirstOrDefaultAsync(f => f.PhoneNumber != null && f.PhoneNumber.Contains(phoneNumber), cancellationToken);
        }

        public async Task<ClusterManager?> GetClusterManagerByNameOrEmail(string search, CancellationToken cancellationToken = default)
        {
            search = search.ToLower();
            return await QueryWithDetails()
                .FirstOrDefaultAsync(f => (f.FullName != null && f.FullName.ToLower().Contains(search)) ||
                                          (f.Email != null && f.Email.ToLower().Contains(search)), cancellationToken);
        }

        public async Task<ClusterManager?> GetClusterManagerByClusterId(Guid ClusterId, CancellationToken cancellationToken = default)
        {
            return await QueryWithDetails()
                .FirstOrDefaultAsync(s => s.ClusterId == ClusterId, cancellationToken);
        }

        public async Task<ClusterManager?> GetClusterByManagerId(Guid ClusterId, CancellationToken cancellationToken = default)
        {
            return await GetClusterManagerByClusterId(ClusterId, cancellationToken);
        }

        public async Task<(IEnumerable<ClusterManager> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<ClusterManager, bool>>? predicate = null, CancellationToken cancellationToken = default)
        {
            var query = QueryWithDetails();
            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderBy(f => f.FullName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
            return (items, totalCount);
        }

        public IQueryable<ClusterManager> GetQueryable()
        {
            return _clusterManager.AsQueryable();
        }

        public async Task<ClusterManager> GetEntityByIdAsync(Expression<Func<ClusterManager, bool>> match)
        {
            return await _clusterManager.SingleOrDefaultAsync(match);
        }

        private IQueryable<ClusterManager> QueryWithDetails()
        {
            return _clusterManager
                .Include(s => s.ManagedCluster)
                .OrderBy(s => s.AssignedDate);
        }

        private static IQueryable<ClusterManager> ApplySearchFilters(IQueryable<ClusterManager> query, string? search, string? phoneNumber)
        {
            search = search?.ToLower();
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(f => (f.FullName != null && f.FullName.ToLower().Contains(search)) ||
                                         (f.Email != null && f.Email.ToLower().Contains(search)));
            }

            if (!string.IsNullOrEmpty(phoneNumber))
            {
                query = query.Where(f => f.PhoneNumber != null && f.PhoneNumber.Contains(phoneNumber));
            }

            return query;
        }

        private static IQueryable<ClusterManager> ApplyOrderingAndPaging(IQueryable<ClusterManager> query, int pageNumber, int pageSize)
        {
            var ordered = query.OrderBy(s => s.FullName);
            return pageSize == 0
                ? ordered
                : ordered.Skip((pageNumber - 1) * pageSize).Take(pageSize);
        }
    }
}
