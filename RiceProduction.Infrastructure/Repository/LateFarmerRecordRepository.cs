using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Domain.Entities;
using RiceProduction.Infrastructure.Data;
using System.Linq.Expressions;

namespace RiceProduction.Infrastructure.Repository;

public class LateFarmerRecordRepository : ILateFarmerRecordRepository
{
    private readonly ApplicationDbContext _context;
    private readonly DbSet<LateFarmerRecord> _lateFarmerRecords;

    public LateFarmerRecordRepository(ApplicationDbContext context)
    {
        _context = context;
        _lateFarmerRecords = context.Set<LateFarmerRecord>();
    }

    public async Task<LateFarmerRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _lateFarmerRecords
            .Include(l => l.Farmer)
            .Include(l => l.CultivationTask)
                .ThenInclude(ct => ct.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
            .Include(l => l.CultivationTask.PlotCultivation.Season)
            .Include(l => l.CultivationTask.PlotCultivation.Plot.GroupPlots)
                .ThenInclude(gp => gp.Group)
                    .ThenInclude(g => g.Cluster)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<LateFarmerRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _lateFarmerRecords
            .Include(l => l.Farmer)
            .Include(l => l.CultivationTask)
                .ThenInclude(ct => ct.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetLateCountByFarmerIdAsync(Guid farmerId, CancellationToken cancellationToken = default)
    {
        return await _lateFarmerRecords
            .CountAsync(l => l.FarmerId == farmerId, cancellationToken);
    }

    public async Task<int> GetLateCountByPlotIdAsync(Guid plotId, CancellationToken cancellationToken = default)
    {
        return await _lateFarmerRecords
            .Include(l => l.CultivationTask)
                .ThenInclude(ct => ct.PlotCultivation)
            .CountAsync(l => l.CultivationTask.PlotCultivation.PlotId == plotId, cancellationToken);
    }

    public async Task<IEnumerable<LateFarmerRecord>> GetLateRecordsByFarmerIdAsync(Guid farmerId, CancellationToken cancellationToken = default)
    {
        return await _lateFarmerRecords
            .Include(l => l.Farmer)
            .Include(l => l.CultivationTask)
                .ThenInclude(ct => ct.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
                        .ThenInclude(p => p.GroupPlots)
                            .ThenInclude(gp => gp.Group)
                                .ThenInclude(g => g.Cluster)
            .Include(l => l.CultivationTask.PlotCultivation.Season)
            .Where(l => l.FarmerId == farmerId)
            .OrderByDescending(l => l.RecordedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<LateFarmerRecord>> GetLateRecordsByPlotIdAsync(Guid plotId, CancellationToken cancellationToken = default)
    {
        return await _lateFarmerRecords
            .Include(l => l.Farmer)
            .Include(l => l.CultivationTask)
                .ThenInclude(ct => ct.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
                        .ThenInclude(p => p.GroupPlots)
                            .ThenInclude(gp => gp.Group)
            .Include(l => l.CultivationTask.PlotCultivation.Season)
            .Where(l => l.CultivationTask.PlotCultivation.PlotId == plotId)
            .OrderByDescending(l => l.RecordedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IEnumerable<LateFarmerRecord> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Expression<Func<LateFarmerRecord, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _lateFarmerRecords
            .Include(l => l.Farmer)
            .Include(l => l.CultivationTask)
                .ThenInclude(ct => ct.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
            .AsQueryable();

        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(l => l.RecordedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<LateFarmerRecord>> ListAsync(
        Expression<Func<LateFarmerRecord, bool>>? filter = null,
        Func<IQueryable<LateFarmerRecord>, IOrderedQueryable<LateFarmerRecord>>? orderBy = null,
        Func<IQueryable<LateFarmerRecord>, IIncludableQueryable<LateFarmerRecord, object>>? includeProperties = null)
    {
        IQueryable<LateFarmerRecord> query = _context.Set<LateFarmerRecord>();

        if (filter != null)
        {
            query = query.Where(filter);
        }

        if (includeProperties != null)
        {
            query = includeProperties(query);
        }

        if (orderBy != null)
        {
            query = orderBy(query);
        }

        return await query.ToListAsync();
    }

    public IQueryable<LateFarmerRecord> GetQueryable()
    {
        return _lateFarmerRecords.AsQueryable();
    }

    public async Task AddAsync(LateFarmerRecord entity, CancellationToken cancellationToken = default)
    {
        await _lateFarmerRecords.AddAsync(entity, cancellationToken);
    }
}
