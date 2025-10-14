using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Infrastructure.Data;

namespace RiceProduction.Infrastructure.Repository
{
    public class PlotRepository : IPlotRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly DbSet<Plot> _plots;

        public PlotRepository (ApplicationDbContext context)
        {
            _context = context;
            _plots = context.Set<Plot>(); 
        }

        public async Task<int> CountPlotAsync(Expression<Func<Plot, bool>>? predicate = null, CancellationToken cancellationToken = default)
        {
            if (predicate != null)
            { 
                return await _plots.CountAsync(predicate, cancellationToken);
            }
            return await _plots.CountAsync(cancellationToken);
        }

        public async Task<bool> ExistPlotAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _plots.AnyAsync(p => p.Id == id, cancellationToken);
        }

        public async Task<Plot?> GetPlotByIDAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _plots
                .Include (p => p.Farmer)
                .Include (p => p.Group)
                .ThenInclude(p => p.RiceVariety)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        }

        public async Task<(IEnumerable<Plot> Items, int TotalCount)> GetAllPlotPagedAsync(int pageNumber, int pageSize, Expression<Func<Plot, bool>>? predicate = null, CancellationToken cancellationToken = default)
        {
            var query = _plots
                .Include(p => p.Farmer)
                .Include(p => p.Group)
                .ThenInclude(p => p.RiceVariety)
                .AsQueryable();

            if (predicate != null)
            {
                query = query.Where(predicate);
            }
            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query.OrderByDescending(p => p.SoThua).Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
            return (items, totalCount);
        }

        public async Task<IEnumerable<Plot?>> GetPlotsByFarmerIdAsync(Guid farmerId, CancellationToken cancellationToken = default)
        {
            return await _plots
                .Include(p => p.Farmer)
                .Include(p => p.Group)
                .ThenInclude(p => p.RiceVariety)
                .Where (p => p.Farmer.Id == farmerId && p.Status == PlotStatus.Active )
                .OrderByDescending(p => p.SoThua)
                .ToListAsync(cancellationToken);             
        }

        public async Task<IEnumerable<Plot?>> GetPlotsByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default)
        {
            return await _plots
                .Include (p => p.Farmer)
                .Include (p => p.Group)
                .ThenInclude(p => p.RiceVariety)
                .Where(p => p.GroupId == groupId && p.Status == PlotStatus.Active )
                .OrderByDescending (p => p.SoThua)
                .ToListAsync(cancellationToken);          

        }

        public IQueryable<Plot> PlotQueryable()
        {
            return _plots.AsQueryable();
        }
    }
}
