using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System.Linq.Expressions;

namespace RiceProduction.Application.Common.Interfaces
{
    public interface IClusterRepository
    {
        IQueryable<Cluster> ClusterQueryable();
        Task<int> CountClusterAsync(Expression<Func<Cluster, bool>>? predicate = null, CancellationToken cancellationToken = default);
        Task<bool> ExistClusterAsync(Guid id, CancellationToken cancellationToken = default);
        Task<(List<Cluster>, int TotalCount)> GetAllClusterPagedAsync(int pageNumber, int pageSize, Expression<Func<Cluster, bool>>? predicate = null, CancellationToken cancellationToken = default);
        Task<(List<Cluster>, int TotalCount)> GetAllClusterPagedSearchSortAsync(int pageNumber, int pageSize, string? ClusterNameSearch, string? ManagerExpertNameSearch, string? PhoneNumber, SortBy SortBy, CancellationToken cancellationToken = default);
        Task<Cluster?> GetClusterByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<List<Cluster>> GetClustersByAgronomyExpertIdAsync(Guid agronomyExpertId, CancellationToken cancellationToken = default);
        Task<List<Cluster>> GetClustersByClusterManagerIdAsync(Guid clusterManagerId, CancellationToken cancellationToken = default);
        Task<Guid> UpdateCluster(Guid id, Cluster updatedCluster, CancellationToken cancellationToken = default);
    }
}