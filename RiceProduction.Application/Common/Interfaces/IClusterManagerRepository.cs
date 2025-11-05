using RiceProduction.Domain.Entities;
using System.Linq.Expressions;

namespace RiceProduction.Infrastructure.Repository
{
    public interface IClusterManagerRepository
    {
        Task<bool> ExistAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<ClusterManager>> FindAsync(Expression<Func<ClusterManager, bool>> predicate, CancellationToken cancellationToken = default);
        Task<IEnumerable<ClusterManager?>> GetAllClusterManagerAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<ClusterManager?>> GetAllClusterManagerByNameOrEmailAndPhoneNumberPagingAsync(int pageNumber, int pageSize, string? search, string? phoneNumber, CancellationToken cancellationToken = default);
        Task<IEnumerable<ClusterManager?>> GetAllClusterManagerAssignedOrNotByNameOrEmailAndPhoneNumberPagingAsync(int pageNumber, int pageSize, string? search, string? phoneNumber, bool? freeOrAssigned, CancellationToken cancellationToken = default);
        Task<ClusterManager?> GetClusterManagerByClusterId(Guid ClusterId, CancellationToken cancellationToken = default);
        Task<ClusterManager?> GetClusterManagerByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<ClusterManager?> GetClusterManagerByNameOrEmail(string search, CancellationToken cancellationToken = default);
        Task<ClusterManager?> GetClusterManagerByPhoneNumber(string phoneNumber, CancellationToken cancellationToken = default);
        Task<(IEnumerable<ClusterManager> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<ClusterManager, bool>>? predicate = null, CancellationToken cancellationToken = default);
        IQueryable<ClusterManager> GetQueryable();
        void UpdateRange(IEnumerable<ClusterManager> entities);
    }
}