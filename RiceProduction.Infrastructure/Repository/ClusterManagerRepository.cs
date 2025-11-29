using RiceProduction.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Infrastructure.Repository
{
    public class ClusterManagerRepository : IClusterManagerRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly DbSet<ClusterManager> _clusterManager;

        public ClusterManagerRepository(ApplicationDbContext context)
        {
            _context = context;
            _clusterManager = context.Set<ClusterManager>();
        }
        public void UpdateRange(IEnumerable<ClusterManager> entities)
        {
            _context.Set<ClusterManager>().UpdateRange(entities);
        }
        public void Update(ClusterManager entity)
        {
            _context.Set<ClusterManager>().Update(entity);
        }


        public async Task<bool> ExistAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _clusterManager.AnyAsync(f => f.Id == id, cancellationToken);
        }

        public async Task<IEnumerable<ClusterManager>> FindAsync(Expression<Func<ClusterManager, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _clusterManager
                .Include(s => s.ManagedCluster)
                .OrderBy(s => s.AssignedDate)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<ClusterManager?>> GetAllClusterManagerAsync(CancellationToken cancellationToken = default)
        {
            return await _clusterManager
                .Include(s => s.ManagedCluster)
                .OrderBy(s => s.AssignedDate)
                .ToListAsync(cancellationToken);
        }

        public async Task<(IEnumerable<ClusterManager> Items, int TotalCount)> GetAllClusterManagerByNameOrEmailAndPhoneNumberPagingAsync(int pageNumber, int pageSize, string? search, string? phoneNumber, CancellationToken cancellationToken = default)
        {
            var query = _clusterManager
                .Include(s => s.ManagedCluster)
                .OrderBy(s => s.AssignedDate)
                .AsQueryable();
            var totalCount = await query.CountAsync(cancellationToken);
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
            if (pageSize == 0)
            {
                return (await query
                .OrderBy(s => s.FullName)
                .ToListAsync(cancellationToken), totalCount);
            }
            return (await query
                .OrderBy(s => s.FullName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken), totalCount);
        }

        public async Task<(IEnumerable<ClusterManager?> Items, int TotalCount)> GetAllClusterManagerAssignedOrNotByNameOrEmailAndPhoneNumberPagingAsync(int pageNumber, int pageSize, string? search, string? phoneNumber, bool? freeOrAssigned, CancellationToken cancellationToken = default)
        {
            var query = _clusterManager
                .Include(s => s.ManagedCluster)
                .OrderBy(s => s.AssignedDate)
                .AsQueryable();
            var totalCount = await query.CountAsync(cancellationToken);
            search = search?.ToLower();
            if (freeOrAssigned.HasValue)
            {
                if (freeOrAssigned.Value)
                    query = query.Where(s => !s.ClusterId.HasValue);
                else
                    query = query.Where(s => s.ClusterId.HasValue);
            }
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(f => (f.FullName != null && f.FullName.ToLower().Contains(search)) ||
                                         (f.Email != null && f.Email.ToLower().Contains(search)));
            }
            if (!string.IsNullOrEmpty(phoneNumber))
            {
                query = query.Where(f => f.PhoneNumber != null && f.PhoneNumber.Contains(phoneNumber));
            }
            if (pageSize == 0)
            {
                return (await query
                .OrderBy(s => s.FullName)
                .ToListAsync(cancellationToken), totalCount);
            }
            return (await query
                .OrderBy(s => s.FullName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken), totalCount);
        }

        public async Task<ClusterManager?> GetClusterManagerByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _clusterManager
                .Include(s => s.ManagedCluster)
                .OrderBy(s => s.AssignedDate)
                .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        }


        public async Task<ClusterManager?> GetClusterManagerByPhoneNumber(string phoneNumber, CancellationToken cancellationToken = default)
        {
            return await _clusterManager
                .Include(s => s.ManagedCluster)
                .OrderBy(s => s.AssignedDate)
                .FirstOrDefaultAsync(f => f.PhoneNumber != null && f.PhoneNumber.Contains(phoneNumber), cancellationToken);
        }

        public async Task<ClusterManager?> GetClusterManagerByNameOrEmail(string search, CancellationToken cancellationToken = default)
        {
            search = search.ToLower();
            return await _clusterManager
                .Include(s => s.ManagedCluster)
                .OrderBy(s => s.AssignedDate)
                .FirstOrDefaultAsync(f => (f.FullName != null && f.FullName.ToLower().Contains(search)) ||
                                          (f.Email != null && f.Email.ToLower().Contains(search)), cancellationToken);
        }

        public async Task<ClusterManager?> GetClusterManagerByClusterId(Guid ClusterId, CancellationToken cancellationToken = default)
        {
            return await _clusterManager
                .Include(s => s.ManagedCluster)
                .OrderBy(s => s.AssignedDate)
                .FirstOrDefaultAsync(s => s.ClusterId == ClusterId, cancellationToken);
        }

        public async Task<(IEnumerable<ClusterManager> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<ClusterManager, bool>>? predicate = null, CancellationToken cancellationToken = default)
        {
            var query = _clusterManager
                .Include(s => s.ManagedCluster)
                .OrderBy(s => s.AssignedDate)
                .AsQueryable();
            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query.OrderBy(f => f.FullName).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
            return (items, totalCount);
        }

        public IQueryable<ClusterManager> GetQueryable()
        {
            return _clusterManager.AsQueryable();
        }

        public async Task<ClusterManager> GetEntityByIdAsync(Expression<Func<ClusterManager, bool>> match)
        {
            return await _context.Set<ClusterManager>().SingleOrDefaultAsync(match);
        }
    }
}
