using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace RiceProduction.Infrastructure.Repository
{
    public class ClusterRepository : IClusterRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly DbSet<Cluster> _clusters;

        public ClusterRepository(ApplicationDbContext context)
        {
            _context = context;
            _clusters = context.Set<Cluster>();
        }

        public async Task<int> CountClusterAsync(Expression<Func<Cluster, bool>>? predicate = null, CancellationToken cancellationToken = default)
        {
            return predicate != null
                ? await _clusters.CountAsync(predicate, cancellationToken)
                : await _clusters.CountAsync(cancellationToken);
        }

        public async Task<bool> ExistClusterAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _clusters.AnyAsync(p => p.Id == id, cancellationToken);
        }

        public async Task<Cluster?> GetClusterByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await QueryWithDetails()
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        }

        public async Task<Guid> UpdateCluster(Guid id, Cluster updatedCluster, CancellationToken cancellationToken = default)
        {
            var existingCluster = await _clusters.FindAsync(new object[] { id }, cancellationToken);
            if (existingCluster == null)
            {
                throw new KeyNotFoundException($"Cluster with ID {id} not found.");
            }
            _context.Entry(existingCluster).CurrentValues.SetValues(updatedCluster);
            await _context.SaveChangesAsync(cancellationToken);
            return existingCluster.Id;
        }

        public async Task<(List<Cluster>, int TotalCount)> GetAllClusterPagedAsync(int pageNumber, int pageSize, Expression<Func<Cluster, bool>>? predicate = null, CancellationToken cancellationToken = default)
        {
            var query = QueryWithDetails();
            var totalCount = await query.CountAsync(cancellationToken);

            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            var items = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
            return (items, totalCount);
        }

        public async Task<(List<Cluster>, int TotalCount)> GetAllClusterPagedSearchSortAsync(int pageNumber, int pageSize, string? ClusterNameSearch, string? ManagerExpertNameSearch, string? PhoneNumber, SortBy SortBy, CancellationToken cancellationToken = default)
        {
            var query = QueryWithDetails();
            var totalCount = await query.CountAsync(cancellationToken);

            query = query.Where(p =>
                (string.IsNullOrEmpty(ClusterNameSearch) || p.ClusterName.ToLower().Contains(ClusterNameSearch.ToLower())) &&
                (string.IsNullOrEmpty(ManagerExpertNameSearch) ||
                 (p.ClusterManager != null && p.ClusterManager.FullName.ToLower().Contains(ManagerExpertNameSearch.ToLower())) ||
                 (p.AgronomyExpert != null && p.AgronomyExpert.FullName.ToLower().Contains(ManagerExpertNameSearch.ToLower())) ||
                 (p.ClusterManager != null && p.ClusterManager.Email.ToLower().Contains(ManagerExpertNameSearch.ToLower())) ||
                 (p.AgronomyExpert != null && p.AgronomyExpert.Email.ToLower().Contains(ManagerExpertNameSearch.ToLower()))) &&
                (string.IsNullOrEmpty(PhoneNumber) ||
                 (p.ClusterManager != null && p.ClusterManager.PhoneNumber != null && p.ClusterManager.PhoneNumber.Contains(PhoneNumber)) ||
                 (p.AgronomyExpert != null && p.AgronomyExpert.PhoneNumber != null && p.AgronomyExpert.PhoneNumber.Contains(PhoneNumber))));

            query = SortBy switch
            {
                SortBy.NameAscending => query.OrderBy(p => p.ClusterName),
                SortBy.NameDescending => query.OrderByDescending(p => p.ClusterName),
                SortBy.DateCreatedAscending => query.OrderBy(p => p.CreatedAt),
                _ => query.OrderByDescending(p => p.CreatedAt)
            };

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
            return (items, totalCount);
        }

        public async Task<List<Cluster>> GetClustersByAgronomyExpertIdAsync(Guid agronomyExpertId, CancellationToken cancellationToken = default)
        {
            return await QueryWithDetails()
                .Where(p => p.AgronomyExpert.Id == agronomyExpertId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Cluster>> GetClustersByClusterManagerIdAsync(Guid clusterManagerId, CancellationToken cancellationToken = default)
        {
            return await QueryWithDetails()
                .Where(p => p.ClusterManager.Id == clusterManagerId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public IQueryable<Cluster> ClusterQueryable()
        {
            return _clusters.AsQueryable();
        }

        private IQueryable<Cluster> QueryWithDetails()
        {
            return _clusters
                .Include(p => p.Groups)
                .Include(p => p.AgronomyExpert)
                .Include(p => p.ClusterManager)
                .Include(p => p.SupervisorsInCluster);
        }
    }
}

