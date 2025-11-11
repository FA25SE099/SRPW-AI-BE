using RiceProduction.Domain.Entities;
using System.Linq.Expressions;

namespace RiceProduction.Infrastructure.Repository
{
    public interface IUavVendorRepository
    {
        Task<bool> ExistAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<UavVendor>> FindAsync(Expression<Func<UavVendor, bool>> predicate, CancellationToken cancellationToken = default);
        Task<IEnumerable<UavVendor?>> GetAllUavVendorAsync(CancellationToken cancellationToken = default);
        Task<(IEnumerable<UavVendor?>, int totalCount)> GetAllUavVendorByNameOrEmailAndPhoneNumberAndByGroupIdOrClusterIdOrNamePagingAsync(int pageNumber, int pageSize, string? search, string? groupSearch, string? phoneNumber, CancellationToken cancellationToken = default);
        Task<(IEnumerable<UavVendor?>, int totalCount)> GetAllUavVendorByNameOrEmailAndPhoneNumberPagingAsync(int pageNumber, int pageSize, string? search, string? phoneNumber, CancellationToken cancellationToken = default);
        Task<(IEnumerable<UavVendor> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<UavVendor, bool>>? predicate = null, CancellationToken cancellationToken = default);
        IQueryable<UavVendor> GetQueryable();
        Task<UavVendor?> GetUavVendorByClusterId(Guid ClusterId, CancellationToken cancellationToken = default);
        Task<UavVendor?> GetUavVendorByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<UavVendor?> GetUavVendorByNameOrEmail(string search, CancellationToken cancellationToken = default);
        Task<UavVendor?> GetUavVendorByPhoneNumber(string phoneNumber, CancellationToken cancellationToken = default);
        void UpdateRange(IEnumerable<UavVendor> entities);
    }
}