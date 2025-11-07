using RiceProduction.Domain.Entities;
using System.Linq.Expressions;

namespace RiceProduction.Infrastructure.Repository
{
    public interface ISupervisorRepository
    {
        Task<bool> ExistAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<Supervisor>> FindAsync(Expression<Func<Supervisor, bool>> predicate, CancellationToken cancellationToken = default);
        Task<IEnumerable<Supervisor?>> GetAllSupervisorAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<Supervisor?>> GetAllSupervisorByNameOrEmailAndPhoneNumberPagingAsync(int pageNumber, int pageSize, string? search, string? phoneNumber, CancellationToken cancellationToken = default);
        Task<(IEnumerable<Supervisor> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<Supervisor, bool>>? predicate = null, CancellationToken cancellationToken = default);
        IQueryable<Supervisor> GetQueryable();
        Task<Supervisor?> GetSupervisorByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<Supervisor?> GetSupervisorByNameOrEmail(string search, CancellationToken cancellationToken = default);
        Task<Supervisor?> GetSupervisorByPhoneNumber(string phoneNumber, CancellationToken cancellationToken = default);
        Task<Supervisor?> GetSupervisorByPlotId(Guid plotId, CancellationToken cancellationToken = default);
        void UpdateRange(IEnumerable<Supervisor> entities);
    }
}