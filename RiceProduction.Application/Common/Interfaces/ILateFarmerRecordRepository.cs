using Microsoft.EntityFrameworkCore.Query;
using RiceProduction.Domain.Entities;
using System.Linq.Expressions;

namespace RiceProduction.Application.Common.Interfaces;

public interface ILateFarmerRecordRepository
{
    Task<LateFarmerRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<LateFarmerRecord>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<int> GetLateCountByFarmerIdAsync(Guid farmerId, CancellationToken cancellationToken = default);
    Task<int> GetLateCountByPlotIdAsync(Guid plotId, CancellationToken cancellationToken = default);
    Task<IEnumerable<LateFarmerRecord>> GetLateRecordsByFarmerIdAsync(Guid farmerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<LateFarmerRecord>> GetLateRecordsByPlotIdAsync(Guid plotId, CancellationToken cancellationToken = default);
    Task<(IEnumerable<LateFarmerRecord> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Expression<Func<LateFarmerRecord, bool>>? predicate = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LateFarmerRecord>> ListAsync(
        Expression<Func<LateFarmerRecord, bool>>? filter = null,
        Func<IQueryable<LateFarmerRecord>, IOrderedQueryable<LateFarmerRecord>>? orderBy = null,
        Func<IQueryable<LateFarmerRecord>, IIncludableQueryable<LateFarmerRecord, object>>? includeProperties = null);
    IQueryable<LateFarmerRecord> GetQueryable();
    Task AddAsync(LateFarmerRecord entity, CancellationToken cancellationToken = default);
}
