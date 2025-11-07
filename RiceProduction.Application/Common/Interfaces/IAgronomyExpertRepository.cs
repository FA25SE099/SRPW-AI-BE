using RiceProduction.Domain.Entities;
using System.Linq.Expressions;

namespace RiceProduction.Infrastructure.Repository
{
    public interface IAgronomyExpertRepository
    {
        Task<bool> ExistAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<AgronomyExpert>> FindAsync(Expression<Func<AgronomyExpert, bool>> predicate, CancellationToken cancellationToken = default);
        Task<AgronomyExpert?> GetAgronomyExpertByClusterId(Guid ClusterId, CancellationToken cancellationToken = default);
        Task<AgronomyExpert?> GetAgronomyExpertByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<AgronomyExpert?> GetAgronomyExpertByNameOrEmail(string search, CancellationToken cancellationToken = default);
        Task<AgronomyExpert?> GetAgronomyExpertByPhoneNumber(string phoneNumber, CancellationToken cancellationToken = default);
        Task<(IEnumerable<AgronomyExpert?>, int totalCount)> GetAllAgronomyExpertAssignedOrNotByNameOrEmailAndPhoneNumberPagingAsync(int pageNumber, int pageSize, string? search, string? phoneNumber, bool? freeOrAssigned, CancellationToken cancellationToken = default);
        Task<IEnumerable<AgronomyExpert?>> GetAllAgronomyExpertAsync(CancellationToken cancellationToken = default);
        Task<(IEnumerable<AgronomyExpert?>, int totalCount)> GetAllAgronomyExpertByNameOrEmailAndPhoneNumberPagingAsync(int pageNumber, int pageSize, string? search, string? phoneNumber, CancellationToken cancellationToken = default);
        Task<(IEnumerable<AgronomyExpert> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<AgronomyExpert, bool>>? predicate = null, CancellationToken cancellationToken = default);
        IQueryable<AgronomyExpert> GetQueryable();
        void UpdateRange(IEnumerable<AgronomyExpert> entities);
    }
}